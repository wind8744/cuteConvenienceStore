using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ConvenienceStore.Tests
{
    public class StoreMapTests
    {
        private StoreMap _map;

        [SetUp]
        public void SetUp() => _map = new StoreMap();

        [Test]
        public void Layout_RowsAreSameWidth()
        {
            foreach (string row in StoreMap.DefaultLayout)
            {
                Assert.AreEqual(StoreMap.DefaultLayout[0].Length, row.Length);
            }
        }

        [Test]
        public void Layout_HasOneShelfPerProduct()
        {
            Assert.AreEqual(ProductCatalog.All.Length, _map.Shelves.Count);
            Assert.AreEqual(3, _map.Counter.Count);
            Assert.AreEqual(2, _map.Entrances.Count);
        }

        [Test]
        public void FurnitureAndWalls_AreNotWalkable()
        {
            foreach (Vector2Int shelf in _map.Shelves)
            {
                Assert.IsFalse(_map.IsWalkable(shelf));
                Assert.IsFalse(_map.IsWalkable(shelf + Vector2Int.right));
            }
            foreach (Vector2Int counter in _map.Counter) Assert.IsFalse(_map.IsWalkable(counter));
            Assert.IsFalse(_map.IsWalkable(new Vector2Int(3, 5)), "벽");
            Assert.IsFalse(_map.IsWalkable(new Vector2Int(-1, 0)), "맵 밖");
        }

        [Test]
        public void QueueAndClerkSpots_AreWalkable()
        {
            Assert.AreEqual(StoreMap.QueueLength, _map.QueueSpots.Count);
            foreach (Vector2Int spot in _map.QueueSpots) Assert.IsTrue(_map.IsWalkable(spot), spot.ToString());
            Assert.IsTrue(_map.IsWalkable(_map.ClerkSpot));
            Assert.IsTrue(_map.IsClerkZone(StoreMap.FeetPos(_map.ClerkSpot)));
            Assert.IsFalse(_map.IsClerkZone(StoreMap.FeetPos(_map.QueueSpots[0])));
        }

        [Test]
        public void FindPath_ReachesEveryShopSpotAndQueueFromEntrance()
        {
            Vector2Int entrance = _map.Entrances[0];
            var goals = new List<Vector2Int>(_map.QueueSpots) { _map.ClerkSpot };
            for (int i = 0; i < _map.Shelves.Count; i++) goals.AddRange(_map.ShopSpots(i));

            foreach (Vector2Int goal in goals)
            {
                List<Vector2Int> path = _map.FindPath(entrance, goal);
                Assert.IsNotNull(path, $"{goal} 로 가는 길이 없음");
                Assert.AreEqual(goal, path[path.Count - 1]);
                Vector2Int prev = entrance;
                foreach (Vector2Int step in path)
                {
                    Assert.IsTrue(_map.IsWalkable(step), $"{step} 는 걸을 수 없는 칸");
                    Assert.AreEqual(1, Mathf.Abs(step.x - prev.x) + Mathf.Abs(step.y - prev.y), "한 칸씩 이동해야 함");
                    prev = step;
                }
            }
        }

        [Test]
        public void FindPath_SameTileIsEmpty_BlockedIsNull()
        {
            Vector2Int entrance = _map.Entrances[0];
            Assert.AreEqual(0, _map.FindPath(entrance, entrance).Count);
            Assert.IsNull(_map.FindPath(entrance, _map.Shelves[0]));
        }
    }

    public class StoreEconomyTests
    {
        private StoreEconomy _economy;

        [SetUp]
        public void SetUp() => _economy = new StoreEconomy();

        [Test]
        public void Starts_WithFirstProductsStocked()
        {
            for (int i = 0; i < _economy.Shelves.Length; i++)
            {
                ShelfStock shelf = _economy.Shelves[i];
                Assert.AreEqual(i < StoreEconomy.StartProducts, shelf.Open);
                Assert.AreEqual(shelf.Open ? StoreEconomy.BaseCapacity : 0, shelf.Stock);
            }
        }

        [Test]
        public void SellThenRestock_MovesMoneyAndStock()
        {
            ShelfStock shelf = _economy.Shelves[0];
            Assert.AreEqual(2, shelf.Take(2));
            Assert.AreEqual(2 * shelf.Product.Price, _economy.Sell(shelf.Product, 2));
            int money = _economy.Money;

            Assert.AreEqual(2, _economy.Restock(shelf));
            Assert.AreEqual(money - 2 * shelf.Product.Cost, _economy.Money);
            Assert.AreEqual(0, shelf.Missing);
            Assert.AreEqual(0, _economy.Restock(shelf), "가득 차 있으면 더 사지 않음");
        }

        [Test]
        public void Take_NeverReturnsMoreThanStock()
        {
            ShelfStock shelf = _economy.Shelves[0];
            Assert.AreEqual(StoreEconomy.BaseCapacity, shelf.Take(100));
            Assert.AreEqual(0, shelf.Take(1));
        }

        [Test]
        public void Restock_IsLimitedByMoney()
        {
            // 잔고 10,000원으로 매입가 900원짜리 6개(5,400원)와 800원짜리 5개(4,000원)까지만 살 수 있다.
            ShelfStock milk = _economy.Shelves[2], ramen = _economy.Shelves[1];
            milk.Take(6);
            ramen.Take(6);
            Assert.AreEqual(6, _economy.Restock(milk));
            Assert.AreEqual(5, _economy.AffordableRestock(ramen));
            Assert.AreEqual(5, _economy.Restock(ramen));
            Assert.AreEqual(600, _economy.Money);
            Assert.AreEqual(0, _economy.Restock(ramen));
        }

        [Test]
        public void Reputation_GoesUpOnSaleAndDownOnLostCustomer()
        {
            float start = _economy.Reputation;
            _economy.Sell(ProductCatalog.All[0], 1);
            Assert.Greater(_economy.Reputation, start);
            _economy.LoseCustomer();
            _economy.LoseCustomer();
            Assert.Less(_economy.Reputation, start);
        }

        [Test]
        public void SpawnInterval_IsShorterAtRushHourAndWithHighReputation()
        {
            Assert.Less(_economy.SpawnInterval(12.5f), _economy.SpawnInterval(10f));
            float before = _economy.SpawnInterval(10f);
            for (int i = 0; i < 10; i++) _economy.Sell(ProductCatalog.All[0], 1);
            Assert.Less(_economy.SpawnInterval(10f), before);
        }

        [Test]
        public void EndDay_ChargesRentAndReportsProfit()
        {
            ShelfStock shelf = _economy.Shelves[0];
            shelf.Take(3);
            _economy.Sell(shelf.Product, 3);
            _economy.Restock(shelf);
            _economy.LoseCustomer();

            DayReport report = _economy.EndDay();
            Assert.AreEqual(1, report.Day);
            Assert.AreEqual(3600, report.Sales);
            Assert.AreEqual(1800, report.RestockCost);
            Assert.AreEqual(5000, report.Rent);
            Assert.AreEqual(1, report.Served);
            Assert.AreEqual(1, report.Lost);
            Assert.AreEqual(3600 - 1800 - 5000, report.Profit);
            Assert.AreEqual(StoreEconomy.StartMoney + report.Profit, report.MoneyAfter);
            Assert.IsFalse(report.Bankrupt);

            _economy.StartNextDay();
            Assert.AreEqual(2, _economy.Day);
            Assert.AreEqual(6500, _economy.Rent);
            Assert.AreEqual(0, _economy.EndDay().Sales, "새 날에는 통계가 초기화됨");
        }

        [Test]
        public void EndDay_BankruptWhenMoneyGoesNegative()
        {
            DayReport report = default;
            for (int day = 0; day < 3; day++)
            {
                report = _economy.EndDay();
                _economy.StartNextDay();
            }
            Assert.IsTrue(report.Bankrupt);
        }

        [Test]
        public void Upgrades_CostMoneyAndApply()
        {
            Assert.IsFalse(_economy.TryUnlock(5), "돈이 모자라면 들여올 수 없음");
            Assert.AreEqual(StoreEconomy.StartProducts, _economy.UnlockedProducts);

            // 삼각김밥을 넉넉히 팔아 돈을 번다.
            _economy.Sell(ProductCatalog.All[0], 500);
            int money = _economy.Money;

            // 순서와 상관없이 원하는 상품을 고를 수 있다.
            int cost = ProductCatalog.All[5].UnlockCost;
            Assert.IsTrue(_economy.TryUnlock(5));
            Assert.AreEqual(money - cost, _economy.Money);
            ShelfStock opened = _economy.Shelves[5];
            Assert.IsTrue(opened.Open);
            Assert.AreEqual(opened.Capacity, opened.Stock, "신상품은 가득 채워서 입고");
            Assert.IsFalse(_economy.Shelves[3].Open);
            Assert.IsFalse(_economy.TryUnlock(5), "이미 들여온 상품은 다시 못 산다");
            Assert.AreEqual(StoreEconomy.StartProducts + 1, _economy.UnlockedProducts);

            Assert.IsTrue(_economy.TryBuy(Upgrade.ShelfSize));
            Assert.AreEqual(StoreEconomy.BaseCapacity + StoreEconomy.CapacityPerLevel, _economy.Shelves[0].Capacity);
            Assert.AreEqual(StoreEconomy.CapacityPerLevel, _economy.Shelves[0].Missing);

            Assert.IsTrue(_economy.TryBuy(Upgrade.Clerk));
            Assert.AreEqual(-1, _economy.UpgradeCost(Upgrade.Clerk));
            Assert.IsFalse(_economy.TryBuy(Upgrade.Clerk));
            Assert.AreEqual(StoreEconomy.ClerkWage, _economy.EndDay().Wage);

            float before = _economy.SpawnInterval(10f);
            Assert.IsTrue(_economy.TryBuy(Upgrade.Marketing));
            Assert.Less(_economy.SpawnInterval(10f), before);
        }
    }

    public class StoreEventTests
    {
        private StoreEconomy _economy;

        [SetUp]
        public void SetUp() => _economy = new StoreEconomy();

        [Test]
        public void Tip_OnlyWhenCustomerDidNotWaitLong()
        {
            Product ramen = ProductCatalog.All[1];
            Assert.AreEqual(0, _economy.TipFor(ramen, 2, 0.5f));
            Assert.AreEqual(500, _economy.TipFor(ramen, 2, 0.9f), "3,000원의 15%(450원)를 100원 단위로 올림");
            Assert.AreEqual(200, _economy.TipFor(ProductCatalog.All[0], 1, 1f));

            int money = _economy.Money;
            Assert.AreEqual(3000, _economy.Sell(ramen, 2, 500));
            Assert.AreEqual(money + 3500, _economy.Money);
            DayReport report = _economy.EndDay();
            Assert.AreEqual(3000, report.Sales);
            Assert.AreEqual(500, report.Tips);
            Assert.AreEqual(3000 + 500 - 5000, report.Profit);
        }

        [Test]
        public void Streak_GrowsUntilACustomerIsLost()
        {
            Product p = ProductCatalog.All[0];
            float start = _economy.Reputation;
            for (int i = 0; i < StoreEconomy.StreakStep; i++) _economy.Sell(p, 1);
            Assert.AreEqual(StoreEconomy.StreakStep, _economy.Streak);
            Assert.AreEqual(start + 2f * (StoreEconomy.StreakStep - 1) + 5f, _economy.Reputation, 0.001f);
            _economy.LoseCustomer();
            Assert.AreEqual(0, _economy.Streak);
        }

        [Test]
        public void DailyGoal_PaysBonusOnlyWhenReached()
        {
            Product p = ProductCatalog.All[0];
            for (int i = 0; i < _economy.DailyGoal - 1; i++) _economy.Sell(p, 1);
            Assert.AreEqual(_economy.DailyGoal - 1, _economy.ServedToday);

            var missed = new StoreEconomy();
            Assert.AreEqual(0, missed.EndDay().GoalBonus);

            _economy.Sell(p, 1);
            int money = _economy.Money, reward = _economy.GoalReward;
            DayReport report = _economy.EndDay();
            Assert.AreEqual(reward, report.GoalBonus);
            Assert.AreEqual(money + reward - report.Rent, report.MoneyAfter);

            _economy.StartNextDay(DayEvent.None);
            Assert.AreEqual(0, _economy.ServedToday);
            Assert.Greater(_economy.DailyGoal, report.Goal);
        }

        [Test]
        public void RollEvent_CoversEveryEvent()
        {
            Assert.AreEqual(DayEvent.None, StoreEconomy.RollEvent(0f));
            Assert.AreEqual(DayEvent.Rainy, StoreEconomy.RollEvent(0.41f));
            Assert.AreEqual(DayEvent.Picnic, StoreEconomy.RollEvent(0.6f));
            Assert.AreEqual(DayEvent.HeatWave, StoreEconomy.RollEvent(0.75f));
            Assert.AreEqual(DayEvent.Payday, StoreEconomy.RollEvent(0.9f));
            Assert.AreEqual(DayEvent.Payday, StoreEconomy.RollEvent(1f));
            Assert.AreEqual(DayEvent.None, _economy.Event, "첫날은 평범한 날");
        }

        [Test]
        public void Events_ChangeDemandAndCustomerFlow()
        {
            float normal = _economy.SpawnInterval(10f);
            _economy.StartNextDay(DayEvent.Rainy);
            Assert.Greater(_economy.SpawnInterval(10f), normal);
            Assert.AreEqual(3f, _economy.DemandWeight(1), "비 오는 날은 컵라면");
            Assert.AreEqual(1f, _economy.DemandWeight(0));

            _economy.StartNextDay(DayEvent.Payday);
            Assert.Less(_economy.SpawnInterval(10f), normal);
            Assert.AreEqual(1, _economy.ExtraQuantity);

            _economy.StartNextDay(DayEvent.HeatWave);
            Assert.AreEqual(3f, _economy.DemandWeight(2), "무더위에는 바나나우유");
        }

        [Test]
        public void PickShelf_FollowsDemandAndStaysWithinUnlockedProducts()
        {
            // 평소에는 세 상품이 똑같이 나뉜다.
            Assert.AreEqual(0, _economy.PickShelf(0.1f));
            Assert.AreEqual(1, _economy.PickShelf(0.5f));
            Assert.AreEqual(2, _economy.PickShelf(0.9f));
            Assert.AreEqual(2, _economy.PickShelf(1f));

            // 비 오는 날: 삼각김밥 1 · 컵라면 3 · 바나나우유 1 → 컵라면이 20%~80% 구간.
            _economy.StartNextDay(DayEvent.Rainy);
            Assert.AreEqual(0, _economy.PickShelf(0.1f));
            Assert.AreEqual(1, _economy.PickShelf(0.25f));
            Assert.AreEqual(1, _economy.PickShelf(0.75f));
            Assert.AreEqual(2, _economy.PickShelf(0.9f));
        }

        [Test]
        public void Audio_TonesClipLastsAsLongAsItsNotes()
        {
            AudioClip clip = StoreAudio.Tones("Test", true, (440f, 0.1f), (880f, 0.2f));
            Assert.AreEqual(0.3f, clip.length, 0.01f);
            var data = new float[clip.samples];
            clip.GetData(data, 0);
            float peak = 0f;
            foreach (float v in data) peak = Mathf.Max(peak, Mathf.Abs(v));
            Assert.Greater(peak, 0.05f);
            Assert.LessOrEqual(peak, 1f);
        }
    }

    public class StoreSaveTests
    {
        [Test]
        public void SaveAndLoad_RestoresTheStore()
        {
            var economy = new StoreEconomy();
            economy.Sell(ProductCatalog.All[0], 300);
            Assert.IsTrue(economy.TryUnlock(4));
            economy.Shelves[1].Selling = false;
            Assert.IsTrue(economy.TryBuy(Upgrade.ShelfSize));
            Assert.IsTrue(economy.TryBuy(Upgrade.Clerk));
            Assert.IsTrue(economy.TryBuy(Upgrade.Marketing));
            economy.Shelves[1].Take(4);
            economy.EndDay();
            economy.StartNextDay(DayEvent.Picnic);

            // 실제 저장과 똑같이 JSON 을 거친다.
            string json = JsonUtility.ToJson(economy.ToSave());
            var loaded = new StoreEconomy(JsonUtility.FromJson<StoreSave>(json));

            Assert.AreEqual(economy.Money, loaded.Money);
            Assert.AreEqual(2, loaded.Day);
            Assert.AreEqual(economy.Reputation, loaded.Reputation, 0.001f);
            Assert.AreEqual(DayEvent.Picnic, loaded.Event);
            Assert.AreEqual(economy.UnlockedProducts, loaded.UnlockedProducts);
            Assert.IsTrue(loaded.Shelves[4].Open);
            Assert.IsFalse(loaded.Shelves[3].Open);
            Assert.IsFalse(loaded.Shelves[1].Selling, "보류 상태도 저장");
            Assert.IsTrue(loaded.HasClerk);
            Assert.AreEqual(1, loaded.ShelfLevel);
            Assert.AreEqual(1, loaded.MarketingLevel);
            for (int i = 0; i < economy.Shelves.Length; i++)
            {
                Assert.AreEqual(economy.Shelves[i].Open, loaded.Shelves[i].Open, $"진열대 {i}");
                Assert.AreEqual(economy.Shelves[i].Capacity, loaded.Shelves[i].Capacity, $"진열대 {i}");
                Assert.AreEqual(economy.Shelves[i].Stock, loaded.Shelves[i].Stock, $"진열대 {i}");
            }
            Assert.AreEqual(economy.SpawnInterval(10f), loaded.SpawnInterval(10f), 0.001f);
        }

        [Test]
        public void Load_ClampsBrokenValues()
        {
            var loaded = new StoreEconomy(new StoreSave
            {
                Day = -3, UnlockedProducts = 99, ShelfLevel = 99, Event = 42, Reputation = 500f, Stock = new[] { 999 },
            });
            Assert.AreEqual(1, loaded.Day);
            Assert.AreEqual(ProductCatalog.All.Length, loaded.UnlockedProducts);
            Assert.AreEqual(StoreEconomy.MaxShelfLevel, loaded.ShelfLevel);
            Assert.AreEqual(100f, loaded.Reputation);
            Assert.AreEqual(loaded.ShelfCapacity, loaded.Shelves[0].Stock);
            Assert.AreEqual(loaded.ShelfCapacity, loaded.Shelves[1].Stock, "저장에 없는 진열대는 가득 찬 채로");
        }

        [Test]
        public void SpecialCustomers_TipMore()
        {
            var economy = new StoreEconomy();
            Product ramen = ProductCatalog.All[1];
            Assert.AreEqual(300, economy.TipFor(ramen, 1, 1f));
            Assert.AreEqual(500, economy.TipFor(ramen, 1, 1f, 2f));
            Assert.AreEqual(700, economy.TipFor(ramen, 1, 1f, 3f));
            Assert.AreEqual(0, economy.TipFor(ramen, 1, 0.2f, 3f));
        }

        [Test]
        public void Music_IsAnEightSecondLoop()
        {
            AudioClip clip = StoreAudio.BuildMusic();
            Assert.AreEqual(8f, clip.length, 0.01f);
            var data = new float[clip.samples];
            clip.GetData(data, 0);
            float peak = 0f;
            foreach (float v in data) peak = Mathf.Max(peak, Mathf.Abs(v));
            Assert.Greater(peak, 0.05f);
            Assert.Less(peak, 0.6f, "효과음보다 조용해야 함");
        }
    }

    public class PriceAndResidentTests
    {
        private StoreEconomy _economy;

        [SetUp]
        public void SetUp() => _economy = new StoreEconomy();

        [Test]
        public void PriceTier_ChangesPriceDemandAndReputationGain()
        {
            ShelfStock shelf = _economy.Shelves[0];
            Product kimbap = shelf.Product;
            Assert.AreEqual(1000, StoreEconomy.PriceOf(kimbap, PriceTier.Cheap), "1,200원의 80%를 100원 단위로");
            Assert.AreEqual(1200, StoreEconomy.PriceOf(kimbap, PriceTier.Normal));
            Assert.AreEqual(1600, StoreEconomy.PriceOf(kimbap, PriceTier.Premium));

            Assert.AreEqual(PriceTier.Normal, shelf.Tier);
            _economy.CyclePrice(shelf);
            Assert.AreEqual(PriceTier.Premium, shelf.Tier);
            Assert.Less(_economy.DemandWeight(0), _economy.DemandWeight(1), "비싸면 덜 찾는다");
            float before = _economy.Reputation;
            Assert.AreEqual(3200, _economy.Sell(kimbap, 2, 0, shelf.Tier));
            Assert.AreEqual(before + 1f, _economy.Reputation, 0.001f);

            _economy.CyclePrice(shelf);
            Assert.AreEqual(PriceTier.Cheap, shelf.Tier);
            Assert.Greater(_economy.DemandWeight(0), _economy.DemandWeight(1), "싸면 더 찾는다");
            before = _economy.Reputation;
            Assert.AreEqual(1000, _economy.Sell(kimbap, 1, 0, shelf.Tier));
            Assert.AreEqual(before + 3f, _economy.Reputation, 0.001f);

            _economy.CyclePrice(shelf);
            Assert.AreEqual(PriceTier.Normal, shelf.Tier);
        }

        [Test]
        public void Residents_OnePerHouseWithDistinctFavorites()
        {
            Assert.AreEqual(new VillageMap().Houses.Count, _economy.Residents.Length);
            var favorites = new HashSet<int>();
            foreach (Resident r in _economy.Residents)
            {
                Assert.IsNotEmpty(r.Name);
                Assert.IsTrue(favorites.Add(r.Favorite));
                Assert.Less(r.Favorite, ProductCatalog.All.Length);
            }
        }

        [Test]
        public void ServingAResident_BuildsFriendshipAndEarnsGifts()
        {
            int money = _economy.Money, gifts = 0;
            for (int i = 1; i <= StoreEconomy.MaxFriendship; i++)
            {
                int gift = _economy.ServeResident(2);
                Assert.AreEqual(i, _economy.Residents[2].Friendship);
                Assert.AreEqual(i == 5 ? 5000 : i == 10 ? 20000 : 0, gift, $"호감도 {i}");
                gifts += gift;
            }
            Assert.AreEqual(0, _economy.ServeResident(2), "끝까지 친해지면 더 오르지 않음");
            Assert.AreEqual(StoreEconomy.MaxFriendship, _economy.Residents[2].Friendship);
            Assert.AreEqual(money + 25000, _economy.Money);
            Assert.AreEqual(2f, _economy.ResidentTipMultiplier(2), 0.001f);
            Assert.AreEqual(1f, _economy.ResidentTipMultiplier(0), 0.001f);

            DayReport report = _economy.EndDay();
            Assert.AreEqual(gifts, report.Gifts);
            Assert.AreEqual(25000 - report.Rent, report.Profit);
        }

        [Test]
        public void Save_KeepsPriceTiersAndFriendship()
        {
            _economy.CyclePrice(_economy.Shelves[1]);
            _economy.ServeResident(4);
            _economy.ServeResident(4);

            var loaded = new StoreEconomy(JsonUtility.FromJson<StoreSave>(JsonUtility.ToJson(_economy.ToSave())));
            Assert.AreEqual(PriceTier.Premium, loaded.Shelves[1].Tier);
            Assert.AreEqual(PriceTier.Normal, loaded.Shelves[0].Tier);
            Assert.AreEqual(2, loaded.Residents[4].Friendship);
            Assert.AreEqual(0, loaded.Residents[0].Friendship);
        }

        [Test]
        public void RoadEnds_LeadToTheStore()
        {
            var village = new VillageMap();
            Assert.Greater(village.RoadEnds.Count, 0);
            foreach (Vector2Int end in village.RoadEnds)
            {
                Assert.IsNotNull(village.FindPath(end, village.Entrances[0], village.IsRoad), end.ToString());
            }
        }
    }

    public class StaffAndCustomerKindTests
    {
        private StoreEconomy _economy;

        [SetUp]
        public void SetUp() => _economy = new StoreEconomy();

        [Test]
        public void TwelveProducts_EachHaveAShelf()
        {
            Assert.AreEqual(12, ProductCatalog.All.Length);
            Assert.AreEqual(12, new StoreMap().Shelves.Count);
            Assert.AreEqual(12, new StoreArt(new StoreMap()).ProductIcons.Length);
            for (int i = 1; i < ProductCatalog.All.Length; i++)
            {
                Assert.GreaterOrEqual(ProductCatalog.All[i].UnlockCost, ProductCatalog.All[i - 1].UnlockCost,
                    "들여오는 값은 차례로 비싸진다");
                Assert.Greater(ProductCatalog.All[i].Price, ProductCatalog.All[i].Cost);
            }
        }

        [Test]
        public void Stocker_IsASeparateHireWithItsOwnWage()
        {
            _economy.Sell(ProductCatalog.All[0], 200);
            Assert.AreEqual(StoreEconomy.StockerCost, _economy.UpgradeCost(Upgrade.Stocker));
            Assert.IsTrue(_economy.TryBuy(Upgrade.Stocker));
            Assert.IsTrue(_economy.HasStocker);
            Assert.IsFalse(_economy.HasClerk);
            Assert.AreEqual(-1, _economy.UpgradeCost(Upgrade.Stocker));
            Assert.IsTrue(_economy.TryBuy(Upgrade.Clerk));
            Assert.AreEqual(StoreEconomy.ClerkWage + StoreEconomy.StockerWage, _economy.EndDay().Wage);

            var loaded = new StoreEconomy(JsonUtility.FromJson<StoreSave>(JsonUtility.ToJson(_economy.ToSave())));
            Assert.IsTrue(loaded.HasStocker);
        }

        [Test]
        public void RudeCustomer_PaysCheapPriceAndGivesNoReputation()
        {
            Product p = ProductCatalog.All[1];
            float rep = _economy.Reputation;
            Assert.AreEqual(StoreEconomy.PriceOf(p, PriceTier.Cheap), _economy.Sell(p, 1, 0, PriceTier.Cheap, 0f));
            Assert.AreEqual(rep, _economy.Reputation, 0.001f);
        }

        [Test]
        public void KindCustomer_TipsEvenAfterWaitingAndBoostsReputation()
        {
            Product p = ProductCatalog.All[1];
            Assert.AreEqual(0, _economy.TipFor(p, 1, 0.1f));
            Assert.AreEqual(300, _economy.TipFor(p, 1, 0.1f, 1f, true));
            float rep = _economy.Reputation;
            _economy.Sell(p, 1, 300, PriceTier.Normal, 4f);
            Assert.AreEqual(rep + 4f, _economy.Reputation, 0.001f);
        }

        [Test]
        public void FishingMoney_CountsAsExtraIncome()
        {
            int money = _economy.Money;
            _economy.Earn(3000);
            Assert.AreEqual(money + 3000, _economy.Money);
            DayReport report = _economy.EndDay();
            Assert.AreEqual(3000, report.Extra);
            Assert.AreEqual(0, report.Sales);
            Assert.AreEqual(3000 - report.Rent, report.Profit);
        }

        [Test]
        public void Events_AlsoBoostNewProducts()
        {
            _economy.Sell(ProductCatalog.All[0], 2000);
            for (int i = 0; i < _economy.Shelves.Length; i++) _economy.TryUnlock(i);
            Assert.AreEqual(ProductCatalog.All.Length, _economy.UnlockedProducts);
            _economy.StartNextDay(DayEvent.HeatWave);
            Assert.AreEqual(3f, _economy.DemandWeight(10), "무더위에는 생수");
            _economy.StartNextDay(DayEvent.Rainy);
            Assert.AreEqual(2f, _economy.DemandWeight(6), "비 오는 날에는 커피");
        }
    }

    public class QuestTests
    {
        private StoreEconomy _economy;

        [SetUp]
        public void SetUp() => _economy = new StoreEconomy();

        [Test]
        public void MakeQuests_OnePerResidentUsingProductsOnSale()
        {
            _economy.MakeQuests();
            Assert.AreEqual(1, _economy.Quests.Count, "첫날은 하나");
            _economy.StartNextDay(DayEvent.None);
            Assert.AreEqual(StoreEconomy.MaxQuests, _economy.Quests.Count, "둘째 날부터 둘");
            Assert.AreNotEqual(_economy.Quests[0].Resident, _economy.Quests[1].Resident);
            foreach (Quest q in _economy.Quests)
            {
                Assert.IsTrue(_economy.Shelves[q.Product].OnSale, "팔고 있는 상품만 부탁");
                Assert.That(q.Quantity, Is.InRange(1, 3));
                Assert.Greater(q.Reward, 0);
                Assert.IsFalse(q.Done);
                Assert.AreSame(q, _economy.QuestFor(q.Resident));
            }
        }

        [Test]
        public void MakeQuests_PrefersFavoriteWhenOnSale()
        {
            // 처음 세 상품만 팔 때, 0~2번 집 사람은 좋아하는 상품을 부탁한다.
            for (int trial = 0; trial < 20; trial++)
            {
                _economy.MakeQuests();
                Quest q = _economy.Quests[0];
                int favorite = _economy.Residents[q.Resident].Favorite;
                if (favorite < StoreEconomy.StartProducts) Assert.AreEqual(favorite, q.Product);
            }
        }

        [Test]
        public void CompleteQuest_PaysRewardAndRaisesFriendshipByTwo()
        {
            _economy.MakeQuests();
            Quest q = _economy.Quests[0];
            int money = _economy.Money;
            int earned = _economy.CompleteQuest(q);
            Assert.AreEqual(q.Reward, earned);
            Assert.AreEqual(money + q.Reward, _economy.Money);
            Assert.AreEqual(2, _economy.Residents[q.Resident].Friendship);
            Assert.IsTrue(q.Done);
            Assert.IsNull(_economy.QuestFor(q.Resident));

            DayReport report = _economy.EndDay();
            Assert.AreEqual(1, report.QuestsDone);
            Assert.AreEqual(q.Reward, report.QuestReward);
            Assert.AreEqual(q.Reward - report.Rent, report.Profit);
        }

        [Test]
        public void Quests_SurviveSaveAndLoad()
        {
            _economy.StartNextDay(DayEvent.None);
            _economy.CompleteQuest(_economy.Quests[0]);
            var loaded = new StoreEconomy(JsonUtility.FromJson<StoreSave>(JsonUtility.ToJson(_economy.ToSave())));
            Assert.AreEqual(2, loaded.Quests.Count);
            Assert.IsTrue(loaded.Quests[0].Done);
            Assert.IsFalse(loaded.Quests[1].Done);
            Assert.AreEqual(_economy.Quests[1].Reward, loaded.Quests[1].Reward);
        }

        [Test]
        public void PausedProduct_IsNotPickedByCustomers()
        {
            _economy.Shelves[1].Selling = false;
            for (float roll = 0f; roll <= 1f; roll += 0.05f) Assert.AreNotEqual(1, _economy.PickShelf(roll));
            Assert.IsFalse(_economy.Shelves[1].OnSale);
            Assert.IsTrue(_economy.Shelves[1].Open);
        }
    }

    public class StoreArtTests
    {
        [Test]
        public void Build_CreatesEverySprite()
        {
            var map = new StoreMap();
            var art = new StoreArt(map);
            Assert.AreEqual((map.Width + StoreArt.BackgroundMargin * 2) * StoreArt.Tile, art.Background.texture.width);
            Assert.AreEqual(ProductCatalog.All.Length, art.ProductIcons.Length);
            foreach (Sprite[] frames in new[] { art.Owner.Down, art.Owner.Up, art.Owner.Side })
            {
                Assert.AreEqual(4, frames.Length);
                foreach (Sprite frame in frames) Assert.AreEqual(FilterMode.Point, frame.texture.filterMode);
            }
            Assert.IsNotNull(art.RandomCustomer(7).Down[0]);
        }

        [Test]
        public void ShelfSlots_FitInsideShelfSprite()
        {
            var art = new StoreArt(new StoreMap());
            Vector2 shelf = art.Shelf.bounds.size, icon = art.ProductIcons[0].bounds.size;
            for (int i = 0; i < StoreArt.ShelfSlots; i++)
            {
                Vector2 p = StoreArt.ShelfSlotOffset(i);
                Assert.GreaterOrEqual(p.x, 0f);
                Assert.GreaterOrEqual(p.y, 0f);
                Assert.LessOrEqual(p.x + icon.x, shelf.x);
                Assert.LessOrEqual(p.y + icon.y, shelf.y);
            }
        }
    }

    public class VillageMapTests
    {
        private VillageMap _village;

        [SetUp]
        public void SetUp() => _village = new VillageMap();

        [Test]
        public void Layout_HasSixHousesAndStoreInTheMiddle()
        {
            foreach (string row in VillageMap.DefaultLayout)
            {
                Assert.AreEqual(VillageMap.DefaultLayout[0].Length, row.Length);
            }
            Assert.AreEqual(6, _village.Houses.Count);
            Assert.AreEqual(2, _village.Entrances.Count);

            float storeCenter = _village.Store.x + VillageMap.StoreWidth * 0.5f;
            Assert.AreEqual(_village.Width * 0.5f, storeCenter, 0.01f, "편의점은 마을 가로 가운데");
            Assert.AreEqual(_village.Height * 0.5f, _village.Store.y + VillageMap.StoreHeight * 0.5f, 3f);
        }

        [Test]
        public void Buildings_BlockTheirWholeFootprint()
        {
            foreach (Vector2Int house in _village.Houses)
            {
                for (int y = 0; y < VillageMap.HouseHeight; y++)
                    for (int x = 0; x < VillageMap.HouseWidth; x++)
                        Assert.IsFalse(_village.IsWalkable(house + new Vector2Int(x, y)));
            }
            for (int y = 0; y < VillageMap.StoreHeight; y++)
                for (int x = 0; x < VillageMap.StoreWidth; x++)
                    Assert.IsFalse(_village.IsWalkable(_village.Store + new Vector2Int(x, y)));
            foreach (Vector2Int tree in _village.Trees) Assert.IsFalse(_village.IsWalkable(tree));
        }

        [Test]
        public void Entrances_AreRightBelowTheStoreDoor()
        {
            Assert.AreEqual(_village.Store + new Vector2Int(2, -1), _village.Entrances[0]);
            Assert.AreEqual(_village.Store + new Vector2Int(3, -1), _village.Entrances[1]);
        }

        [Test]
        public void EveryHouse_HasARoadToTheStore()
        {
            for (int i = 0; i < _village.Houses.Count; i++)
            {
                Vector2Int door = _village.HouseDoor(i);
                Assert.IsTrue(_village.IsRoad(door), $"{i + 1}번 집 문 앞 {door} 이 길이 아님");
                foreach (Vector2Int entrance in _village.Entrances)
                {
                    List<Vector2Int> path = _village.FindPath(door, entrance, _village.IsRoad);
                    Assert.IsNotNull(path, $"{i + 1}번 집에서 편의점으로 가는 길이 끊김");
                    foreach (Vector2Int step in path) Assert.IsTrue(_village.IsRoad(step));
                }
            }
        }

        [Test]
        public void Origin_ShiftsWorldCoordinates()
        {
            _village.Origin = new Vector2(100f, 0f);
            Vector2Int tile = _village.Entrances[0];
            Vector2 world = _village.TileToWorld(tile);
            Assert.Greater(world.x, 100f);
            Assert.AreEqual(tile, _village.WorldToTile(world));
        }

        [Test]
        public void Art_BuildsOneHousePerLot()
        {
            var art = new VillageArt(_village);
            Assert.AreEqual(_village.Width * StoreArt.Tile, art.Background.texture.width);
            Assert.AreEqual(_village.Houses.Count, art.Houses.Length);
            Assert.AreEqual(VillageMap.HouseWidth, art.Houses[0].bounds.size.x, 0.001f);
            Assert.AreEqual(VillageMap.StoreWidth, art.Store.bounds.size.x, 0.001f);
            Assert.IsNotNull(art.TreeAt(_village.Trees[0]));
        }
    }
}
