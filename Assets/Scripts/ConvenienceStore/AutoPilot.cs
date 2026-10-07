using System.Collections.Generic;
using UnityEngine;

namespace ConvenienceStore
{
    /// <summary>
    /// 자동 플레이 (F1). 주인을 대신 움직여 줄 선 손님을 계산하고, 재고가 줄어든 진열대를 채우고,
    /// 정산 화면에서 다음 날로 넘어갑니다. 시연용이라 마을에는 나가지 않습니다.
    /// </summary>
    public class AutoPilot
    {
        private const float ArriveDistance = 0.12f;
        private const float SummaryWaitSeconds = 5f;

        private readonly ConvenienceStoreGame _game;
        private readonly StoreOwner _owner;
        private readonly List<Vector2Int> _path = new List<Vector2Int>();
        private int _pathIndex;
        private int _shelfTarget = -1;
        private bool _goingToCounter;
        private bool _pressQueued;
        private float _summaryTimer, _pressCooldown;

        public bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                if (!value) _owner.ExternalInput = null;
            }
        }
        private bool _enabled;

        /// <summary>HUD 에 보여 줄 지금 하는 일.</summary>
        public string Status { get; private set; } = "";

        public AutoPilot(ConvenienceStoreGame game, StoreOwner owner)
        {
            _game = game;
            _owner = owner;
        }

        /// <summary>자동 플레이가 누른 Space 를 한 번 가져갑니다.</summary>
        public bool TakePress()
        {
            if (!_pressQueued) return false;
            _pressQueued = false;
            return true;
        }

        public void Tick()
        {
            if (!Enabled) return;
            _pressCooldown -= Time.deltaTime;

            if (_game.InSummary)
            {
                _owner.ExternalInput = Vector2.zero;
                _summaryTimer += Time.deltaTime;
                Status = "정산 확인 중";
                if (_summaryTimer >= SummaryWaitSeconds)
                {
                    _summaryTimer = 0f;
                    _pressQueued = true;
                }
                return;
            }
            _summaryTimer = 0f;

            StoreMap map = _game.Map;
            Vector2Int here = StoreMap.TileOf(_owner.Position);

            // 할 일 고르기: 계산 > 진열 > 계산대에서 대기
            if (_shelfTarget < 0 && _game.FrontCustomerReady)
            {
                if (map.IsClerkZone(_owner.Position))
                {
                    Stop();
                    Status = "계산 중";
                    if (_pressCooldown <= 0f)
                    {
                        _pressQueued = true;
                        _pressCooldown = 0.35f;
                    }
                    return;
                }
                if (!_goingToCounter) SetPath(here, map.ClerkSpot);
                _goingToCounter = true;
                Status = "계산대로";
            }
            else if (_shelfTarget < 0)
            {
                int shelf = PickShelf();
                if (shelf >= 0)
                {
                    _shelfTarget = shelf;
                    _goingToCounter = false;
                    SetPath(here, map.ShopSpots(shelf)[0]);
                    Status = $"{_game.Economy.Shelves[shelf].Product.Name} 채우러";
                }
                else if (!map.IsClerkZone(_owner.Position))
                {
                    if (!_goingToCounter) SetPath(here, map.ClerkSpot);
                    _goingToCounter = true;
                    Status = "계산대로";
                }
                else
                {
                    Stop();
                    Status = "손님 기다리는 중";
                    return;
                }
            }

            if (!FollowPath()) return;

            // 도착
            if (_shelfTarget >= 0)
            {
                ShelfStock stock = _game.Economy.Shelves[_shelfTarget];
                if (_game.Economy.AffordableRestock(stock) > 0 && _pressCooldown <= 0f)
                {
                    _pressQueued = true;
                    _pressCooldown = 0.35f;
                    Status = "채우는 중";
                }
                else
                {
                    _shelfTarget = -1;
                }
            }
            else
            {
                _goingToCounter = false;
            }
        }

        /// <summary>팔고 있는 진열대 가운데 재고가 절반 아래이고 채울 돈이 있는, 가장 빈 곳.</summary>
        private int PickShelf()
        {
            int best = -1, bestStock = int.MaxValue;
            IReadOnlyList<ShelfView> views = _game.ShelfViews;
            for (int i = 0; i < views.Count; i++)
            {
                ShelfStock stock = views[i].Stock;
                if (!stock.OnSale || stock.Stock > stock.Capacity / 2) continue;
                if (_game.Economy.AffordableRestock(stock) <= 0 || stock.Stock >= bestStock) continue;
                bestStock = stock.Stock;
                best = i;
            }
            return best;
        }

        private void SetPath(Vector2Int from, Vector2Int to)
        {
            _path.Clear();
            List<Vector2Int> found = _game.Map.FindPath(from, to);
            if (found != null) _path.AddRange(found);
            _pathIndex = 0;
        }

        /// <summary>경로를 따라 입력을 넣습니다. 끝에 닿으면 true.</summary>
        private bool FollowPath()
        {
            if (_pathIndex >= _path.Count)
            {
                Stop();
                return true;
            }
            Vector2 target = StoreMap.FeetPos(_path[_pathIndex]);
            Vector2 delta = target - _owner.Position;
            if (delta.magnitude < ArriveDistance)
            {
                _pathIndex++;
                return FollowPath();
            }
            // 모서리에 걸리지 않게 한 축씩 움직인다.
            Vector2 input = Mathf.Abs(delta.x) > ArriveDistance ? new Vector2(Mathf.Sign(delta.x), 0f)
                : new Vector2(0f, Mathf.Sign(delta.y));
            _owner.ExternalInput = input;
            return false;
        }

        private void Stop() => _owner.ExternalInput = Vector2.zero;
    }
}
