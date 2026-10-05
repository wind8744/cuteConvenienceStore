using UnityEngine;

namespace ConvenienceStore
{
    public enum Upgrade { NewProduct, ShelfSize, Clerk, Marketing }

    /// <summary>그날그날 달라지는 마을 사정. 잘 팔리는 상품과 손님 수가 바뀝니다.</summary>
    public enum DayEvent { None, Rainy, Picnic, HeatWave, Payday }

    /// <summary>이어 하기용 저장 데이터. 하루를 시작할 때의 가게 상태입니다.</summary>
    [System.Serializable]
    public class StoreSave
    {
        public int Money, Day, UnlockedProducts, ShelfLevel, MarketingLevel, Event;
        public bool HasClerk;
        public float Reputation;
        public int[] Stock;
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
        /// <summary>오늘의 목표를 달성해 받은 보너스. 못 채웠으면 0.</summary>
        public int GoalBonus;
        public int Goal;
        public int Served;
        public int Lost;
        public int MoneyAfter;
        public int Profit => Sales + Tips + GoalBonus - RestockCost - Rent - Wage;
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
        /// <summary>인내심이 이 비율 넘게 남았을 때 계산해 주면 팁을 받습니다.</summary>
        public const float TipPatience = 0.7f;
        public const float TipRate = 0.15f;
        /// <summary>손님을 놓치지 않고 이만큼 연속으로 응대할 때마다 평판 보너스.</summary>
        public const int StreakStep = 5;

        public int Money { get; private set; } = StartMoney;
        public int Day { get; private set; } = 1;
        /// <summary>0~100. 높을수록 손님이 자주 옵니다.</summary>
        public float Reputation { get; private set; } = 50f;
        public int UnlockedProducts { get; private set; } = StartProducts;
        public int ShelfLevel { get; private set; }
        public int MarketingLevel { get; private set; }
        public bool HasClerk { get; private set; }
        public ShelfStock[] Shelves { get; }

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

        private int _sales, _restockCost, _served, _lost, _tips;

        public StoreEconomy() : this(null) { }

        /// <summary>save 가 있으면 그 상태에서 이어 하고, 없으면 새 가게로 시작합니다.</summary>
        public StoreEconomy(StoreSave save)
        {
            Product[] products = ProductCatalog.All;
            if (save != null)
            {
                Money = save.Money;
                Day = Mathf.Max(1, save.Day);
                Reputation = Mathf.Clamp(save.Reputation, 0f, 100f);
                UnlockedProducts = Mathf.Clamp(save.UnlockedProducts, StartProducts, products.Length);
                ShelfLevel = Mathf.Clamp(save.ShelfLevel, 0, MaxShelfLevel);
                MarketingLevel = Mathf.Clamp(save.MarketingLevel, 0, MaxMarketingLevel);
                HasClerk = save.HasClerk;
                Event = (DayEvent)Mathf.Clamp(save.Event, 0, (int)DayEvent.Payday);
            }

            Shelves = new ShelfStock[products.Length];
            for (int i = 0; i < products.Length; i++)
            {
                Shelves[i] = new ShelfStock(products[i], ShelfCapacity, i < UnlockedProducts);
                if (save?.Stock != null && i < save.Stock.Length) Shelves[i].Restore(save.Stock[i]);
            }
        }

        public StoreSave ToSave()
        {
            var save = new StoreSave
            {
                Money = Money, Day = Day, UnlockedProducts = UnlockedProducts, ShelfLevel = ShelfLevel,
                MarketingLevel = MarketingLevel, Event = (int)Event, HasClerk = HasClerk, Reputation = Reputation,
                Stock = new int[Shelves.Length],
            };
            for (int i = 0; i < Shelves.Length; i++) save.Stock[i] = Shelves[i].Stock;
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
        public int TipFor(Product product, int quantity, float patienceRatio, float multiplier = 1f)
        {
            if (patienceRatio < TipPatience) return 0;
            return Mathf.CeilToInt(product.Price * quantity * TipRate * multiplier / 100f) * 100;
        }

        /// <summary>손님 한 명 계산 완료. 팁을 뺀 물건값을 돌려줍니다.</summary>
        public int Sell(Product product, int quantity, int tip = 0)
        {
            int income = product.Price * quantity;
            Money += income + tip;
            _sales += income;
            _tips += tip;
            _served++;
            Streak++;
            AddReputation(Streak % StreakStep == 0 ? 5f : 2f);
            return income;
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
                case Upgrade.NewProduct:
                    return UnlockedProducts < Shelves.Length ? Shelves[UnlockedProducts].Product.UnlockCost : -1;
                case Upgrade.ShelfSize:
                    return ShelfLevel < MaxShelfLevel ? 15000 * (ShelfLevel + 1) : -1;
                case Upgrade.Clerk:
                    return HasClerk ? -1 : ClerkCost;
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
                case Upgrade.NewProduct:
                    Shelves[UnlockedProducts].OpenAndFill();
                    UnlockedProducts++;
                    break;
                case Upgrade.ShelfSize:
                    ShelfLevel++;
                    foreach (ShelfStock shelf in Shelves) shelf.SetCapacity(ShelfCapacity);
                    break;
                case Upgrade.Clerk:
                    HasClerk = true;
                    break;
                case Upgrade.Marketing:
                    MarketingLevel++;
                    break;
            }
            return true;
        }

        /// <summary>
        /// 임대료와 알바비를 내고(목표를 채웠으면 보너스를 받고) 오늘의 정산서를 돌려줍니다.
        /// 날짜는 StartNextDay 에서 넘어갑니다.
        /// </summary>
        public DayReport EndDay()
        {
            int rent = Rent;
            int wage = HasClerk ? ClerkWage : 0;
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
            _sales = _restockCost = _served = _lost = _tips = 0;
            Event = nextEvent ?? RollEvent(Random.value);
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
            int id = Shelves[shelfIndex].Product.Id;
            switch (Event)
            {
                case DayEvent.Rainy: return id == 1 ? 3f : 1f;
                case DayEvent.Picnic: return id == 0 || id == 3 ? 3f : id == 5 ? 2f : 1f;
                case DayEvent.HeatWave: return id == 4 ? 4f : id == 2 ? 3f : 1f;
                default: return 1f;
            }
        }

        /// <summary>0~1 난수로, 들여온 상품 가운데 손님이 찾을 진열대를 수요에 비례해 고릅니다.</summary>
        public int PickShelf(float roll01)
        {
            float total = 0f;
            for (int i = 0; i < UnlockedProducts; i++) total += DemandWeight(i);
            float target = roll01 * total;
            for (int i = 0; i < UnlockedProducts; i++)
            {
                target -= DemandWeight(i);
                if (target < 0f) return i;
            }
            return UnlockedProducts - 1;
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
