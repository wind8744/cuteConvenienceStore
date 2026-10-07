using System.Collections.Generic;
using UnityEngine;

namespace ConvenienceStore
{
    public enum Upgrade { ShelfSize, Clerk, Stocker, Marketing }

    /// <summary>마을 사람의 부탁. 상품을 손에 들고 그 사람에게 가져다주면 보상과 호감도를 받습니다.</summary>
    [System.Serializable]
    public class Quest
    {
        public int Resident;
        public int Product;
        public int Quantity;
        public int Reward;
        public bool Done;
    }

    /// <summary>그날그날 달라지는 마을 사정. 잘 팔리는 상품과 손님 수가 바뀝니다.</summary>
    public enum DayEvent { None, Rainy, Picnic, HeatWave, Payday }

    /// <summary>상품 가격대. 싸게 팔면 잘 팔리고 평판이 빨리 오르고, 비싸게 팔면 그 반대입니다.</summary>
    public enum PriceTier { Cheap, Normal, Premium }

    /// <summary>마을 집에 사는 단골. 계산해 줄 때마다 호감도가 오릅니다.</summary>
    public class Resident
    {
        public readonly string Name;
        /// <summary>좋아하는 상품의 Id. 가게에 들여놓았다면 주로 이것을 사러 옵니다.</summary>
        public readonly int Favorite;
        public int Friendship { get; internal set; }

        public Resident(string name, int favorite)
        {
            Name = name;
            Favorite = favorite;
        }
    }

    /// <summary>이어 하기용 저장 데이터. 하루를 시작할 때의 가게 상태입니다.</summary>
    [System.Serializable]
    public class StoreSave
    {
        public int Money, Day, UnlockedProducts, ShelfLevel, MarketingLevel, Event;
        public bool HasClerk;
        public bool HasStocker;
        public float Reputation;
        public int[] Stock;
        public int[] Tiers;
        public int[] Friendship;
        public bool[] Open;
        public bool[] Selling;
        public List<Quest> Quests;
    }

    /// <summary>하루 영업 정산 결과.</summary>
    public struct DayReport
    {
        public int Day;
        public int Sales;
        public int RestockCost;
        public int Rent;
        public int Wage;
        public int Tips;
        /// <summary>친해진 단골에게 받은 선물.</summary>
        public int Gifts;
        /// <summary>낚시처럼 장사 밖에서 번 돈.</summary>
        public int Extra;
        /// <summary>마을 사람의 부탁을 들어주고 받은 보상.</summary>
        public int QuestReward;
        public int QuestsDone;
        /// <summary>오늘의 목표를 달성해 받은 보너스. 못 채웠으면 0.</summary>
        public int GoalBonus;
        public int Goal;
        public int Served;
        public int Lost;
        public int MoneyAfter;
        public int Profit => Sales + Tips + Gifts + Extra + QuestReward + GoalBonus - RestockCost - Rent - Wage;
        public bool Bankrupt => MoneyAfter < 0;
    }

    /// <summary>
    /// 가게 경영 상태: 잔고, 평판, 진열대 재고, 업그레이드, 하루 정산.
    /// Unity 오브젝트에 의존하지 않아 EditMode 테스트로 검증할 수 있습니다.
    /// </summary>
    public class StoreEconomy
    {
        public const int StartMoney = 10000;
        public const int StartProducts = 3;
        public const int BaseCapacity = 6;
        public const int CapacityPerLevel = 2;
        public const int MaxShelfLevel = 3;
        public const int MaxMarketingLevel = 3;
        public const int ClerkCost = 60000;
        public const int ClerkWage = 4000;
        public const int StockerCost = 45000;
        public const int StockerWage = 3000;
        /// <summary>인내심이 이 비율 넘게 남았을 때 계산해 주면 팁을 받습니다.</summary>
        public const float TipPatience = 0.7f;
        public const float TipRate = 0.15f;
        /// <summary>손님을 놓치지 않고 이만큼 연속으로 응대할 때마다 평판 보너스.</summary>
        public const int StreakStep = 5;
        public const int MaxFriendship = 10;
        public const int MaxQuests = 2;

        public int Money { get; private set; } = StartMoney;
        public int Day { get; private set; } = 1;
        /// <summary>0~100. 높을수록 손님이 자주 옵니다.</summary>
        public float Reputation { get; private set; } = 50f;
        /// <summary>들여온 상품 수.</summary>
        public int UnlockedProducts
        {
            get
            {
                int count = 0;
                foreach (ShelfStock shelf in Shelves) if (shelf.Open) count++;
                return count;
            }
        }
        /// <summary>오늘 들어온 부탁들.</summary>
        public List<Quest> Quests { get; } = new List<Quest>();
        public int ShelfLevel { get; private set; }
        public int MarketingLevel { get; private set; }
        public bool HasClerk { get; private set; }
        /// <summary>비는 진열대를 알아서 채우는 진열 알바.</summary>
        public bool HasStocker { get; private set; }
        public ShelfStock[] Shelves { get; }
        /// <summary>마을 집 번호 순서대로 그 집에 사는 사람.</summary>
        public Resident[] Residents { get; } =
        {
            new Resident("민지", 0), new Resident("준호", 1), new Resident("순자 할머니", 2),
            new Resident("서연", 3), new Resident("태양", 4), new Resident("하루", 5),
        };

        public int ShelfCapacity => BaseCapacity + CapacityPerLevel * ShelfLevel;
        public int Rent => 5000 + 1500 * (Day - 1);
        public DayEvent Event { get; private set; }
        /// <summary>손님을 놓치지 않고 연속으로 응대한 수.</summary>
        public int Streak { get; private set; }
        public int ServedToday => _served;
        /// <summary>오늘 응대해야 할 손님 수 목표.</summary>
        public int DailyGoal => Mathf.Min(8 + 2 * (Day - 1), 24);
        public int GoalReward => 2000 + 1000 * Day;
        /// <summary>월급날에는 손님이 하나씩 더 삽니다.</summary>
        public int ExtraQuantity => Event == DayEvent.Payday ? 1 : 0;

        private int _sales, _restockCost, _served, _lost, _tips, _gifts, _extra, _questReward, _questsDone;

        public StoreEconomy() : this(null) { }

        /// <summary>save 가 있으면 그 상태에서 이어 하고, 없으면 새 가게로 시작합니다.</summary>
        public StoreEconomy(StoreSave save)
        {
            Product[] products = ProductCatalog.All;
            int unlocked = StartProducts;
            if (save != null)
            {
                Money = save.Money;
                Day = Mathf.Max(1, save.Day);
                Reputation = Mathf.Clamp(save.Reputation, 0f, 100f);
                unlocked = Mathf.Clamp(save.UnlockedProducts, StartProducts, products.Length);
                ShelfLevel = Mathf.Clamp(save.ShelfLevel, 0, MaxShelfLevel);
                MarketingLevel = Mathf.Clamp(save.MarketingLevel, 0, MaxMarketingLevel);
                HasClerk = save.HasClerk;
                HasStocker = save.HasStocker;
                Event = (DayEvent)Mathf.Clamp(save.Event, 0, (int)DayEvent.Payday);
            }

            Shelves = new ShelfStock[products.Length];
            for (int i = 0; i < products.Length; i++)
            {
                // 예전 저장은 상품을 번호 순서로 들여왔고, 지금은 고른 상품만 들여온다.
                bool open = save?.Open != null && i < save.Open.Length ? save.Open[i] : i < unlocked;
                Shelves[i] = new ShelfStock(products[i], ShelfCapacity, open || i < StartProducts);
                if (save?.Stock != null && i < save.Stock.Length) Shelves[i].Restore(save.Stock[i]);
                if (save?.Selling != null && i < save.Selling.Length) Shelves[i].Selling = save.Selling[i];
                if (save?.Tiers != null && i < save.Tiers.Length)
                {
                    Shelves[i].Tier = (PriceTier)Mathf.Clamp(save.Tiers[i], 0, (int)PriceTier.Premium);
                }
            }
            for (int i = 0; save?.Friendship != null && i < Residents.Length && i < save.Friendship.Length; i++)
            {
                Residents[i].Friendship = Mathf.Clamp(save.Friendship[i], 0, MaxFriendship);
            }
            if (save?.Quests != null)
            {
                foreach (Quest q in save.Quests)
                {
                    if (q.Resident < 0 || q.Resident >= Residents.Length || q.Product < 0 || q.Product >= Shelves.Length)
                        continue;
                    Quests.Add(q);
                }
            }
        }

        public StoreSave ToSave()
        {
            var save = new StoreSave
            {
                Money = Money, Day = Day, UnlockedProducts = UnlockedProducts, ShelfLevel = ShelfLevel,
                MarketingLevel = MarketingLevel, Event = (int)Event, HasClerk = HasClerk, HasStocker = HasStocker, Reputation = Reputation,
                Stock = new int[Shelves.Length],
                Tiers = new int[Shelves.Length],
                Open = new bool[Shelves.Length],
                Selling = new bool[Shelves.Length],
                Friendship = new int[Residents.Length],
                Quests = new List<Quest>(Quests),
            };
            for (int i = 0; i < Shelves.Length; i++)
            {
                save.Stock[i] = Shelves[i].Stock;
                save.Tiers[i] = (int)Shelves[i].Tier;
                save.Open[i] = Shelves[i].Open;
                save.Selling[i] = Shelves[i].Selling;
            }
            for (int i = 0; i < Residents.Length; i++) save.Friendship[i] = Residents[i].Friendship;
            return save;
        }

        /// <summary>지금 잔고로 이 진열대에 채울 수 있는 개수.</summary>
        public int AffordableRestock(ShelfStock shelf)
        {
            if (Money <= 0) return 0;
            return Mathf.Min(shelf.Missing, Money / shelf.Product.Cost);
        }

        /// <summary>잔고가 허락하는 만큼 진열대를 채우고 채운 개수를 돌려줍니다.</summary>
        public int Restock(ShelfStock shelf)
        {
            int count = AffordableRestock(shelf);
            if (count <= 0) return 0;
            int cost = count * shelf.Product.Cost;
            Money -= cost;
            _restockCost += cost;
            shelf.Add(count);
            return count;
        }

        /// <summary>남은 인내심 비율(0~1)에 따른 팁. 오래 기다리게 했으면 0, 아니면 100원 단위로 올림.</summary>
        public int TipFor(Product product, int quantity, float patienceRatio, float multiplier = 1f,
            bool ignorePatience = false)
        {
            if (patienceRatio < TipPatience && !ignorePatience) return 0;
            return Mathf.CeilToInt(product.Price * quantity * TipRate * multiplier / 100f) * 100;
        }

        /// <summary>가격대를 반영한 판매가. 100원 단위.</summary>
        public static int PriceOf(Product product, PriceTier tier)
        {
            float factor = tier == PriceTier.Cheap ? 0.8f : tier == PriceTier.Premium ? 1.3f : 1f;
            return Mathf.RoundToInt(product.Price * factor / 100f) * 100;
        }

        public static string TierName(PriceTier tier) =>
            tier == PriceTier.Cheap ? "싸게" : tier == PriceTier.Premium ? "비싸게" : "보통";

        /// <summary>싸게 → 보통 → 비싸게 → 싸게 순서로 가격대를 바꿉니다.</summary>
        public void CyclePrice(ShelfStock shelf) => shelf.Tier = (PriceTier)(((int)shelf.Tier + 1) % 3);

        /// <summary>단골이 물건을 사 갔습니다. 호감도가 오르고, 5와 10이 되는 날 선물을 받습니다. 선물 금액을 돌려줍니다.</summary>
        public int ServeResident(int index)
        {
            Resident resident = Residents[index];
            if (resident.Friendship >= MaxFriendship) return 0;
            resident.Friendship++;
            int gift = resident.Friendship == MaxFriendship ? 20000 : resident.Friendship == MaxFriendship / 2 ? 5000 : 0;
            Money += gift;
            _gifts += gift;
            return gift;
        }

        /// <summary>친한 단골일수록 팁이 후합니다.</summary>
        public float ResidentTipMultiplier(int index) => 1f + 0.1f * Residents[index].Friendship;

        /// <summary>
        /// 손님 한 명 계산 완료. 팁을 뺀 물건값을 돌려줍니다.
        /// reputationGain 을 주면 가격대에 따른 기본 평판 상승 대신 그 값을 씁니다.
        /// </summary>
        public int Sell(Product product, int quantity, int tip = 0, PriceTier tier = PriceTier.Normal,
            float reputationGain = -1f)
        {
            int income = PriceOf(product, tier) * quantity;
            Money += income + tip;
            _sales += income;
            _tips += tip;
            _served++;
            Streak++;
            float gain = tier == PriceTier.Cheap ? 3f : tier == PriceTier.Premium ? 1f : 2f;
            if (reputationGain >= 0f) gain = reputationGain;
            AddReputation(Streak % StreakStep == 0 ? gain + 3f : gain);
            return income;
        }

        /// <summary>장사 밖에서 번 돈(낚시 등).</summary>
        public void Earn(int amount)
        {
            Money += amount;
            _extra += amount;
        }

        public void AddReputation(float amount) => Reputation = Mathf.Clamp(Reputation + amount, 0f, 100f);

        /// <summary>손님이 물건이 없거나 기다리다 지쳐 그냥 나갔습니다.</summary>
        public void LoseCustomer()
        {
            _lost++;
            Streak = 0;
            Reputation = Mathf.Clamp(Reputation - 5f, 0f, 100f);
        }

        /// <summary>업그레이드 가격. 더 살 수 없으면 -1.</summary>
        public int UpgradeCost(Upgrade upgrade)
        {
            switch (upgrade)
            {
                case Upgrade.ShelfSize:
                    return ShelfLevel < MaxShelfLevel ? 15000 * (ShelfLevel + 1) : -1;
                case Upgrade.Clerk:
                    return HasClerk ? -1 : ClerkCost;
                case Upgrade.Stocker:
                    return HasStocker ? -1 : StockerCost;
                case Upgrade.Marketing:
                    return MarketingLevel < MaxMarketingLevel ? 20000 * (MarketingLevel + 1) : -1;
                default:
                    return -1;
            }
        }

        public bool TryBuy(Upgrade upgrade)
        {
            int cost = UpgradeCost(upgrade);
            if (cost < 0 || Money < cost) return false;
            Money -= cost;
            switch (upgrade)
            {
                case Upgrade.ShelfSize:
                    ShelfLevel++;
                    foreach (ShelfStock shelf in Shelves) shelf.SetCapacity(ShelfCapacity);
                    break;
                case Upgrade.Clerk:
                    HasClerk = true;
                    break;
                case Upgrade.Stocker:
                    HasStocker = true;
                    break;
                case Upgrade.Marketing:
                    MarketingLevel++;
                    break;
            }
            return true;
        }

        /// <summary>아직 안 들여온 상품을 들여옵니다. 첫 물량은 가득 채워서 옵니다.</summary>
        public bool TryUnlock(int shelfIndex)
        {
            ShelfStock shelf = Shelves[shelfIndex];
            if (shelf.Open || Money < shelf.Product.UnlockCost) return false;
            Money -= shelf.Product.UnlockCost;
            shelf.OpenAndFill();
            return true;
        }

        // ── 부탁 ──

        /// <summary>
        /// 오늘의 부탁을 새로 만듭니다. 첫날은 하나, 그 뒤로는 둘. 서로 다른 사람이 부탁하고,
        /// 좋아하는 상품을 팔고 있으면 그것을, 아니면 팔고 있는 아무 상품이나 부탁합니다.
        /// </summary>
        public void MakeQuests()
        {
            Quests.Clear();
            var onSale = new List<int>();
            for (int i = 0; i < Shelves.Length; i++) if (Shelves[i].OnSale) onSale.Add(i);
            if (onSale.Count == 0) return;

            var residents = new List<int>();
            for (int i = 0; i < Residents.Length; i++) residents.Add(i);
            int count = Mathf.Min(Day == 1 ? 1 : MaxQuests, residents.Count);
            for (int n = 0; n < count; n++)
            {
                int who = residents[Random.Range(0, residents.Count)];
                residents.Remove(who);
                int favorite = Residents[who].Favorite;
                int product = Shelves[favorite].OnSale ? favorite : onSale[Random.Range(0, onSale.Count)];
                int quantity = Random.Range(1, 4);
                Quests.Add(new Quest
                {
                    Resident = who, Product = product, Quantity = quantity,
                    Reward = PriceOf(Shelves[product].Product, PriceTier.Normal) * quantity + 3000 + 1000 * Day,
                });
            }
        }

        /// <summary>이 사람의 아직 안 끝난 부탁. 없으면 null.</summary>
        public Quest QuestFor(int resident)
        {
            foreach (Quest q in Quests) if (q.Resident == resident && !q.Done) return q;
            return null;
        }

        /// <summary>부탁을 들어줬습니다. 보상을 받고 호감도가 2 오릅니다. 선물까지 합친 금액을 돌려줍니다.</summary>
        public int CompleteQuest(Quest quest)
        {
            quest.Done = true;
            Money += quest.Reward;
            _questReward += quest.Reward;
            _questsDone++;
            int gifts = ServeResident(quest.Resident) + ServeResident(quest.Resident);
            return quest.Reward + gifts;
        }

        /// <summary>
        /// 임대료와 알바비를 내고(목표를 채웠으면 보너스를 받고) 오늘의 정산서를 돌려줍니다.
        /// 날짜는 StartNextDay 에서 넘어갑니다.
        /// </summary>
        public DayReport EndDay()
        {
            int rent = Rent;
            int wage = (HasClerk ? ClerkWage : 0) + (HasStocker ? StockerWage : 0);
            int bonus = _served >= DailyGoal ? GoalReward : 0;
            Money += bonus - rent - wage;
            return new DayReport
            {
                Day = Day,
                Sales = _sales,
                RestockCost = _restockCost,
                Rent = rent,
                Wage = wage,
                Tips = _tips,
                Gifts = _gifts,
                Extra = _extra,
                QuestReward = _questReward,
                QuestsDone = _questsDone,
                GoalBonus = bonus,
                Goal = DailyGoal,
                Served = _served,
                Lost = _lost,
                MoneyAfter = Money,
            };
        }

        /// <summary>다음 날로 넘어갑니다. nextEvent 를 주지 않으면 그날의 사정을 무작위로 정합니다.</summary>
        public void StartNextDay(DayEvent? nextEvent = null)
        {
            Day++;
            _sales = _restockCost = _served = _lost = _tips = _gifts = _extra = _questReward = _questsDone = 0;
            Event = nextEvent ?? RollEvent(Random.value);
            MakeQuests();
        }

        /// <summary>절반쯤은 평범한 날이고, 나머지는 네 가지 사정 중 하나입니다.</summary>
        public static DayEvent RollEvent(float roll01)
        {
            if (roll01 < 0.4f) return DayEvent.None;
            int index = Mathf.Min(3, Mathf.FloorToInt((roll01 - 0.4f) / 0.6f * 4f));
            return DayEvent.Rainy + index;
        }

        /// <summary>오늘 이 진열대의 상품이 얼마나 잘 팔리는지. 평소는 1.</summary>
        public float DemandWeight(int shelfIndex)
        {
            PriceTier tier = Shelves[shelfIndex].Tier;
            float price = tier == PriceTier.Cheap ? 1.5f : tier == PriceTier.Premium ? 0.6f : 1f;
            return price * EventDemand(Shelves[shelfIndex].Product.Id);
        }

        private float EventDemand(int id)
        {
            switch (Event)
            {
                case DayEvent.Rainy: return id == 1 ? 3f : id == 6 ? 2f : 1f;
                case DayEvent.Picnic: return id == 0 || id == 3 || id == 9 ? 3f : id == 5 ? 2f : 1f;
                case DayEvent.HeatWave: return id == 4 ? 4f : id == 2 || id == 10 ? 3f : 1f;
                default: return 1f;
            }
        }

        /// <summary>0~1 난수로, 팔고 있는 상품 가운데 손님이 찾을 진열대를 수요에 비례해 고릅니다. 파는 게 없으면 -1.</summary>
        public int PickShelf(float roll01)
        {
            float total = 0f;
            int last = -1;
            for (int i = 0; i < Shelves.Length; i++)
            {
                if (!Shelves[i].OnSale) continue;
                total += DemandWeight(i);
                last = i;
            }
            float target = roll01 * total;
            for (int i = 0; i < Shelves.Length; i++)
            {
                if (!Shelves[i].OnSale) continue;
                target -= DemandWeight(i);
                if (target < 0f) return i;
            }
            return last;
        }

        public static string EventName(DayEvent dayEvent)
        {
            switch (dayEvent)
            {
                case DayEvent.Rainy: return "비 오는 날";
                case DayEvent.Picnic: return "소풍날";
                case DayEvent.HeatWave: return "무더위";
                case DayEvent.Payday: return "월급날";
                default: return "평범한 날";
            }
        }

        public static string EventHint(DayEvent dayEvent)
        {
            switch (dayEvent)
            {
                case DayEvent.Rainy: return "손님은 줄지만 컵라면이 잘 팔려요";
                case DayEvent.Picnic: return "삼각김밥·과자·도시락이 잘 팔려요";
                case DayEvent.HeatWave: return "아이스크림과 바나나우유가 잘 팔려요";
                case DayEvent.Payday: return "손님이 몰리고 하나씩 더 사 가요";
                default: return "오늘도 힘내요";
            }
        }

        /// <summary>손님이 오는 간격(초). 평판·홍보가 높을수록, 점심·저녁 피크일수록 짧아집니다.</summary>
        public float SpawnInterval(float hour)
        {
            float interval = Mathf.Lerp(7f, 3f, Reputation / 100f);
            bool rush = (hour >= 12f && hour < 13.5f) || (hour >= 18f && hour < 20f);
            if (rush) interval *= 0.6f;
            if (Event == DayEvent.Rainy) interval *= 1.3f;
            else if (Event == DayEvent.Payday) interval *= 0.75f;
            return interval / (1f + 0.2f * MarketingLevel);
        }
    }
}
