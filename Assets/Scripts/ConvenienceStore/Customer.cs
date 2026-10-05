using System.Collections.Generic;
using UnityEngine;

namespace ConvenienceStore
{
    /// <summary>
    /// 손님. 문으로 들어와 원하는 상품의 진열대로 가서 물건을 집고, 계산 줄에 서서 기다렸다가 나갑니다.
    /// 진열대가 비어 있거나 줄에서 너무 오래 기다리면 화를 내며 그냥 나갑니다.
    /// </summary>
    public enum CustomerKind
    {
        Normal,
        /// <summary>많이 사고 팁도 후한 손님.</summary>
        Vip,
        /// <summary>걸음이 빠르고 금방 지치지만, 제때 계산해 주면 팁을 더 주는 손님.</summary>
        Hurried,
    }

    public class Customer : MonoBehaviour
    {
        private const float BarWidth = 0.8f;
        private const float BarHeight = 0.12f;

        private const float BrowseSeconds = 0.7f;
        private const float WaitForStockSeconds = 6f;
        private const float ExitY = -2.5f;

        private enum State { Entering, ToShelf, Browsing, ToQueue, InQueue, Leaving, Exiting }

        private ConvenienceStoreGame _game;
        private CharacterView _view;
        private State _state;
        private int _shelfIndex, _want, _have, _doorX;
        private float _speed, _patience, _patienceMax, _timer;
        private List<Vector2Int> _path = new List<Vector2Int>();
        private int _pathIndex;
        private Vector2Int _pathGoal;
        private bool _moved, _atQueueSpot, _warned;
        private GameObject _bar;
        private SpriteRenderer _barFill;

        /// <summary>마을에서 걸어올 때와 같은 모습으로 집에 돌아가도록 기억해 둡니다.</summary>
        public CharacterSprites Sprites { get; set; }
        /// <summary>이 손님이 사는 집 번호.</summary>
        public int Home { get; set; }
        public CustomerKind Kind { get; set; }
        public float TipMultiplier => Kind == CustomerKind.Vip ? 3f : Kind == CustomerKind.Hurried ? 2f : 1f;
        public Product Product => _game.Economy.Shelves[_shelfIndex].Product;
        public int Carrying => _have;
        /// <summary>남은 인내심 비율 (0~1).</summary>
        public float PatienceRatio => Mathf.Clamp01(_patience / _patienceMax);
        /// <summary>줄 맨 앞에 도착해서 계산을 기다리는 중인지.</summary>
        public bool ReadyForCheckout => _state == State.InQueue && _atQueueSpot && _game.QueueIndex(this) == 0;

        public void Init(ConvenienceStoreGame game, CharacterView view, int shelfIndex, int want, int doorX,
            float speed, float patience)
        {
            _game = game;
            _view = view;
            _shelfIndex = shelfIndex;
            _want = want;
            _doorX = doorX;
            _speed = speed;
            _patience = _patienceMax = patience;
            transform.position = new Vector3(doorX + 0.5f, ExitY, 0f);
            _state = State.Entering;
            BuildPatienceBar();
        }

        /// <summary>줄 서 있는 동안 머리 위에 보이는 인내심 막대. 초록일 때 계산하면 팁을 받습니다.</summary>
        private void BuildPatienceBar()
        {
            _bar = new GameObject("Patience");
            _bar.transform.SetParent(transform, false);
            _bar.transform.localPosition = new Vector3(0f, 1.26f, 0f);
            var back = _bar.AddComponent<SpriteRenderer>();
            back.sprite = _game.Art.WhitePixel;
            back.color = new Color(0.28f, 0.17f, 0.16f);
            back.sortingOrder = 8990;
            _bar.transform.localScale = new Vector3(BarWidth + 0.12f, BarHeight + 0.12f, 1f);

            var fill = new GameObject("Fill");
            fill.transform.SetParent(transform, false);
            _barFill = fill.AddComponent<SpriteRenderer>();
            _barFill.sprite = _game.Art.WhitePixel;
            _barFill.sortingOrder = 8991;
            ShowPatienceBar(false);
        }

        private void ShowPatienceBar(bool show)
        {
            _bar.SetActive(show);
            _barFill.gameObject.SetActive(show);
            if (show) UpdatePatienceBar();
        }

        private void UpdatePatienceBar()
        {
            float ratio = PatienceRatio;
            _barFill.transform.localPosition = new Vector3(-BarWidth * 0.5f * (1f - ratio), 1.26f, 0f);
            _barFill.transform.localScale = new Vector3(BarWidth * ratio, BarHeight, 1f);
            _barFill.color = ratio >= StoreEconomy.TipPatience ? new Color(0.45f, 0.85f, 0.4f)
                : ratio >= 0.35f ? new Color(1f, 0.8f, 0.25f)
                : new Color(0.95f, 0.3f, 0.3f);
        }

        private void Update()
        {
            _moved = false;
            switch (_state)
            {
                case State.Entering:
                    if (MoveTo(StoreMap.FeetPos(new Vector2Int(_doorX, 0))))
                    {
                        Vector2Int[] spots = _game.Map.ShopSpots(_shelfIndex);
                        SetPath(spots[Random.Range(0, spots.Length)]);
                        _state = State.ToShelf;
                    }
                    break;

                case State.ToShelf:
                    if (FollowPath())
                    {
                        _view.Face(Vector2.up);
                        _timer = 0f;
                        _state = State.Browsing;
                    }
                    break;

                case State.Browsing:
                    UpdateBrowsing();
                    break;

                case State.ToQueue:
                    Vector2Int spot = _game.QueueSpot(this);
                    if (spot != _pathGoal) SetPath(spot);
                    if (FollowPath()) _state = State.InQueue;
                    break;

                case State.InQueue:
                    UpdateInQueue();
                    break;

                case State.Leaving:
                    if (FollowPath()) _state = State.Exiting;
                    break;

                case State.Exiting:
                    if (MoveTo(new Vector2(_doorX + 0.5f, ExitY)))
                    {
                        _game.OnCustomerGone(this);
                        Destroy(gameObject);
                    }
                    break;
            }
            _view.Tick(_moved);
        }

        private void UpdateBrowsing()
        {
            _timer += Time.deltaTime;
            if (_timer < BrowseSeconds) return;

            ShelfStock shelf = _game.Economy.Shelves[_shelfIndex];
            int taken = shelf.Take(_want - _have);
            if (taken > 0)
            {
                _have += taken;
                _game.RefreshShelf(_shelfIndex);
            }

            if (_have > 0)
            {
                _view.HideBubble();
                _warned = false;
                _game.JoinQueue(this);
                ShowPatienceBar(true);
                _pathGoal = new Vector2Int(-1, -1);
                _state = State.ToQueue;
            }
            else if (_timer > BrowseSeconds + WaitForStockSeconds)
            {
                GiveUp();
            }
            else if (!_warned)
            {
                // 원하는 상품이 없다: 잠깐 기다려 준다.
                _warned = true;
                _view.ShowBubble(_game.Art.ProductIcons[Product.Id], 0f);
            }
        }

        private void UpdateInQueue()
        {
            _atQueueSpot = MoveTo(StoreMap.FeetPos(_game.QueueSpot(this)));
            if (_atQueueSpot) _view.Face(Vector2.right);

            _patience -= Time.deltaTime;
            UpdatePatienceBar();
            if (_patience > 0f) return;

            // 기다리다 지쳐 물건을 도로 놓고 나간다.
            _game.Economy.Shelves[_shelfIndex].Add(_have);
            _game.RefreshShelf(_shelfIndex);
            _have = 0;
            _game.LeaveQueue(this);
            GiveUp();
        }

        /// <summary>계산이 끝났습니다. 하트를 띄우고 나갑니다.</summary>
        public void CompleteCheckout()
        {
            _view.ShowBubble(_game.Art.Heart, 2.5f);
            StartLeaving();
        }

        private void GiveUp()
        {
            _game.OnCustomerLost(this);
            _view.ShowBubble(_game.Art.Anger, 3f);
            StartLeaving();
        }

        private void StartLeaving()
        {
            ShowPatienceBar(false);
            SetPath(new Vector2Int(_doorX, 0));
            _state = State.Leaving;
        }

        private void SetPath(Vector2Int goal)
        {
            _pathGoal = goal;
            _path = _game.Map.FindPath(StoreMap.TileOf(transform.position), goal) ?? new List<Vector2Int>();
            _pathIndex = 0;
        }

        /// <summary>경로를 따라 걷습니다. 끝까지 가면 true.</summary>
        private bool FollowPath()
        {
            if (_pathIndex < _path.Count && MoveTo(StoreMap.FeetPos(_path[_pathIndex]))) _pathIndex++;
            return _pathIndex >= _path.Count;
        }

        /// <summary>목표 지점으로 곧장 걷습니다. 도착하면 true.</summary>
        private bool MoveTo(Vector2 target)
        {
            Vector2 pos = transform.position;
            Vector2 delta = target - pos;
            if (delta.sqrMagnitude < 0.0001f) return true;
            _view.Face(delta);
            _moved = true;
            pos = Vector2.MoveTowards(pos, target, _speed * Time.deltaTime);
            transform.position = pos;
            return (target - pos).sqrMagnitude < 0.0001f;
        }
    }
}
