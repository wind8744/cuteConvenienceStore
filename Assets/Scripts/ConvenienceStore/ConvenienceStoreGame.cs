using System.Collections.Generic;
using UnityEngine;

namespace ConvenienceStore
{
    /// <summary>
    /// 편의점 타이쿤 진행 관리. 하루 영업(개점~마감), 손님 등장, 계산·진열 상호작용, 정산과 업그레이드, HUD를 담당합니다.
    /// 가게 안과 마을은 같은 씬의 서로 떨어진 자리에 있고, 주인이 문을 드나들 때 카메라가 옮겨 갑니다.
    /// 씬에는 이 컴포넌트가 붙은 오브젝트와 메인 카메라만 있으면 됩니다. 나머지는 런타임에 생성합니다.
    /// </summary>
    public class ConvenienceStoreGame : MonoBehaviour
    {
        [Header("Day")]
        [SerializeField] private float _dayLengthSeconds = 150f;
        [SerializeField] private int _openHour = 8;
        [SerializeField] private int _closeHour = 22;
        [Header("Characters")]
        [SerializeField] private float _ownerSpeed = 5.5f;
        [SerializeField] private float _customerSpeed = 3f;
        [SerializeField] private int _maxCustomers = 9;
        [SerializeField] private float _patienceSeconds = 28f;
        [SerializeField] private float _clerkCheckoutSeconds = 2.2f;
        [SerializeField] private float _restockRange = 1.8f;

        /// <summary>마을 맵을 가게 안과 겹치지 않게 놓는 자리.</summary>
        private static readonly Vector2 VillageOrigin = new Vector2(100f, 0f);

        private enum State { Open, Closing, Summary }

        private struct FloatingText
        {
            public Vector2 Pos;
            public string Text;
            public Color Color;
            public float Age;
            public float Life;
        }

        private static readonly Upgrade[] UpgradeKeys =
        {
            Upgrade.ShelfSize, Upgrade.Clerk, Upgrade.Stocker, Upgrade.Marketing
        };

        private const float FloatingTextSeconds = 1.2f;
        private static readonly Color Ink = new Color(0.3f, 0.19f, 0.15f);
        private static readonly Color InkFaded = new Color(0.3f, 0.19f, 0.15f, 0.45f);
        private static readonly Color Good = new Color(0.2f, 0.55f, 0.3f);
        private static readonly Color Bad = new Color(0.82f, 0.25f, 0.28f);
        private static readonly Color Gold = new Color(0.85f, 0.55f, 0.1f);
        private const float FlyerRange = 1.3f;
        private const float BannerSeconds = 5f;
        private const string SaveKey = "ConvenienceStore.Save";

        private State _state;
        private float _hour;
        private float _spawnTimer;
        private float _clerkTimer;
        private int _customerSeed;
        private int _inbound;
        private bool _inVillage;
        private VillageArt _villageArt;
        private StoreAudio _audio;
        private bool[] _flyered;
        private bool[] _residentOut;
        private CharacterSprites[] _residentSprites;
        private bool _showNotebook, _showProducts;
        private int _productCursor;
        private bool[] _talked;
        private readonly List<ResidentNpc> _residentNpcs = new List<ResidentNpc>();
        /// <summary>주인이 손에 든 상품의 진열대 번호(-1 이면 빈손)와 개수.</summary>
        private int _handProduct = -1, _handCount;
        private const int HandMax = 3;
        private string _banner;
        private float _bannerTimer;
        private DayReport _report;
        private string _prompt;

        private StoreOwner _owner;
        private GameObject _clerk;
        private Stocker _stocker;
        private CharacterView _ownerView;
        private Fishing _fishing;
        private AutoPilot _autoPilot;
        private SpriteRenderer _daylight;
        private readonly List<ShelfView> _shelfViews = new List<ShelfView>();
        private readonly List<Customer> _customers = new List<Customer>();
        private readonly List<Customer> _queue = new List<Customer>();
        private readonly List<FloatingText> _texts = new List<FloatingText>();
        private readonly List<SpriteRenderer> _glows = new List<SpriteRenderer>();
        private GUIStyle _panelStyle, _labelStyle, _titleStyle, _floatStyle;

        public StoreMap Map { get; private set; }
        internal IReadOnlyList<ShelfView> ShelfViews => _shelfViews;
        internal bool FrontCustomerReady => _queue.Count > 0 && _queue[0].ReadyForCheckout;
        internal bool InSummary => _state == State.Summary;
        internal bool StoreOpen => _state == State.Open;
        public VillageMap Village { get; private set; }
        public StoreArt Art { get; private set; }
        public StoreEconomy Economy { get; private set; }

        private void Start()
        {
            Map = new StoreMap();
            Art = new StoreArt(Map);
            Economy = LoadEconomy();
            Village = new VillageMap { Origin = VillageOrigin };
            _villageArt = new VillageArt(Village);
            _audio = new StoreAudio(gameObject);
            // 집에 사는 단골은 늘 같은 모습이다.
            _residentSprites = new CharacterSprites[Village.Houses.Count];
            for (int i = 0; i < _residentSprites.Length; i++) _residentSprites[i] = Art.RandomCustomer(9000 + i * 37);
            SetupCamera();
            BuildStore();
            BuildVillage();
            BindShelves();
            StartDay();
        }

        private void Update()
        {
            _prompt = null;
            if (Input.GetKeyDown(KeyCode.M)) _audio.ToggleMusic();
            if (Input.GetKeyDown(KeyCode.Tab)) _showNotebook = !_showNotebook;
            if (Input.GetKeyDown(KeyCode.F1)) _autoPilot.Enabled = !_autoPilot.Enabled;
            _autoPilot.Tick();
            UpdateProductPanel();
            switch (_state)
            {
                case State.Open:
                    _hour += Time.deltaTime * (_closeHour - _openHour) / _dayLengthSeconds;
                    UpdateSpawning();
                    UpdateWork();
                    UpdateDoors();
                    if (_hour >= _closeHour)
                    {
                        _state = State.Closing;
                        Float(_owner.Position + Vector2.up * 1.6f, "영업 종료! 남은 손님만 받아요", Ink);
                    }
                    break;

                case State.Closing:
                    UpdateWork();
                    UpdateDoors();
                    if (_customers.Count == 0) EndDay();
                    break;

                case State.Summary:
                    UpdateSummary();
                    break;
            }

            UpdateDaylight();
            if (_bannerTimer > 0f) _bannerTimer -= Time.deltaTime;
            for (int i = _texts.Count - 1; i >= 0; i--)
            {
                FloatingText t = _texts[i];
                t.Age += Time.deltaTime;
                if (t.Age >= t.Life) _texts.RemoveAt(i);
                else _texts[i] = t;
            }
        }

        // ── 하루 진행 ─────────────────────────────────────────────────────

        private void StartDay()
        {
            _hour = _openHour;
            _spawnTimer = 2f;
            _clerkTimer = 0f;
            _clerk.SetActive(Economy.HasClerk);
            _stocker.gameObject.SetActive(Economy.HasStocker);
            _owner.MoveTo(Map, StoreMap.FeetPos(Map.ClerkSpot));
            _inVillage = false;
            _owner.CanMove = true;
            foreach (ShelfView view in _shelfViews) view.Refresh();
            _flyered = new bool[Village.Houses.Count];
            _residentOut = new bool[Village.Houses.Count];
            _talked = new bool[Village.Houses.Count];
            foreach (ResidentNpc npc in _residentNpcs) npc.gameObject.SetActive(true);
            if (Economy.Quests.Count == 0) Economy.MakeQuests();
            PutHandBack();
            // 하루를 시작할 때마다 자동 저장한다.
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(Economy.ToSave()));
            PlayerPrefs.Save();
            _banner = $"DAY {Economy.Day} · {StoreEconomy.EventName(Economy.Event)} · {StoreEconomy.EventHint(Economy.Event)}";
            _bannerTimer = BannerSeconds;
            _state = State.Open;
            Debug.Log($"[ConvenienceStore] Day {Economy.Day} started (money {Economy.Money})");
        }

        private void EndDay()
        {
            _report = Economy.EndDay();
            _audio.Play(_audio.DayEnd);
            _owner.CanMove = false;
            _state = State.Summary;
        }

        private void UpdateSummary()
        {
            bool next = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || _autoPilot.TakePress();
            if (Input.GetKeyDown(KeyCode.Backspace) || (next && _report.Bankrupt))
            {
                // 새 게임: 저장을 지우고 첫날부터.
                PlayerPrefs.DeleteKey(SaveKey);
                Economy = new StoreEconomy();
                BindShelves();
                StartDay();
                return;
            }
            if (next)
            {
                Economy.StartNextDay();
                StartDay();
                return;
            }
            if (_report.Bankrupt) return;

            for (int i = 0; i < UpgradeKeys.Length; i++)
            {
                if (!Input.GetKeyDown(KeyCode.Alpha1 + i) && !Input.GetKeyDown(KeyCode.Keypad1 + i)) continue;
                if (Economy.TryBuy(UpgradeKeys[i]))
                {
                    _audio.Play(_audio.Buy);
                    foreach (ShelfView view in _shelfViews) view.Refresh();
                }
            }
        }

        private void UpdateSpawning()
        {
            _spawnTimer -= Time.deltaTime;
            if (_spawnTimer > 0f) return;
            _spawnTimer = Economy.SpawnInterval(_hour) * Random.Range(0.7f, 1.3f);
            if (_customers.Count + _inbound >= _maxCustomers) return;

            // 절반쯤은 마을 집에 사는 단골, 나머지는 마을 밖에서 길을 따라 오는 손님.
            int house = Random.Range(0, Village.Houses.Count);
            SendVillager(Random.value < 0.5f && !_residentOut[house] ? house : -1, 0);
        }

        /// <summary>
        /// 손님이 편의점까지 걸어온다. resident 가 0 이상이면 그 집 단골이 집에서 나오고,
        /// -1 이면 마을 밖 손님이 길 끝에서 들어온다. want 가 0 이면 살 개수를 무작위로 정한다.
        /// </summary>
        private CharacterView SendVillager(int resident, int want)
        {
            CharacterSprites sprites;
            Vector2Int home;
            if (resident >= 0)
            {
                sprites = _residentSprites[resident];
                home = Village.HouseDoor(resident);
                _residentOut[resident] = true;
                _residentNpcs[resident].gameObject.SetActive(false);
            }
            else
            {
                sprites = Art.RandomCustomer(++_customerSeed + Economy.Day * 1000);
                home = Village.RoadEnds[Random.Range(0, Village.RoadEnds.Count)];
            }

            Vector2Int entrance = Village.Entrances[Random.Range(0, Village.Entrances.Count)];
            _inbound++;
            CharacterView view = SpawnWalker(sprites, home, entrance, () =>
            {
                _inbound--;
                if (_state == State.Open) SpawnCustomer(sprites, resident, home, want);
                else WalkHome(sprites, resident, home);
            });
            if (resident >= 0) view.SetBadge(Art.Heart);
            return view;
        }

        /// <summary>편의점 입구에서 집(또는 길 끝)으로 돌아간다. 단골은 집에 도착해야 다시 나올 수 있다.</summary>
        private void WalkHome(CharacterSprites sprites, int resident, Vector2Int home)
        {
            Vector2Int entrance = Village.Entrances[Random.Range(0, Village.Entrances.Count)];
            CharacterView view = SpawnWalker(sprites, entrance, home, () =>
            {
                if (resident < 0) return;
                _residentOut[resident] = false;
                _residentNpcs[resident].gameObject.SetActive(true);
            });
            if (resident >= 0) view.SetBadge(Art.Heart);
        }

        private void SpawnCustomer(CharacterSprites sprites, int resident, Vector2Int home, int want)
        {
            int shelfIndex = Economy.PickShelf(Random.value);
            if (resident >= 0)
            {
                // 단골은 좋아하는 상품이 가게에 있으면 주로 그것을 찾는다.
                int favorite = Economy.Residents[resident].Favorite;
                if (Economy.Shelves[favorite].OnSale && Random.value < 0.6f) shelfIndex = favorite;
            }
            float speed = _customerSpeed * Random.Range(0.85f, 1.15f);
            float patience = _patienceSeconds * Random.Range(0.85f, 1.15f);
            var kind = CustomerKind.Normal;
            if (want <= 0)
            {
                float roll = Random.value;
                want = (roll < 0.6f ? 1 : roll < 0.9f ? 2 : 3) + Economy.ExtraQuantity;

                // 가끔 특별한 손님이 온다. VIP 는 가게가 조금 알려진 뒤부터, 진상은 둘째 날부터.
                float special = resident >= 0 ? 1f : Random.value;
                if (special < 0.08f && Economy.Reputation >= 55f)
                {
                    kind = CustomerKind.Vip;
                    want = 4;
                    patience *= 1.2f;
                }
                else if (special < 0.24f)
                {
                    kind = CustomerKind.Hurried;
                    speed *= 1.5f;
                    patience *= 0.55f;
                }
                else if (special < 0.32f && Economy.Day >= 2)
                {
                    kind = CustomerKind.Rude;
                    patience *= 0.8f;
                }
                else if (special < 0.44f)
                {
                    kind = CustomerKind.Kind;
                    patience *= 1.6f;
                }
            }
            if (!_inVillage) _audio.Play(_audio.Chime, 0.6f);
            int doorX = Map.Entrances[Random.Range(0, Map.Entrances.Count)].x;

            var go = new GameObject("Customer");
            var view = go.AddComponent<CharacterView>();
            view.Init(sprites, Art.Bubble);
            var customer = go.AddComponent<Customer>();
            customer.Init(this, view, shelfIndex, want, doorX, speed, patience);
            customer.Kind = kind;
            if (kind == CustomerKind.Vip) view.SetBadge(Art.Crown);
            else if (kind == CustomerKind.Hurried) view.SetBadge(Art.Bolt);
            else if (kind == CustomerKind.Rude) view.SetBadge(Art.Anger);
            else if (kind == CustomerKind.Kind) view.SetBadge(Art.Star);
            else if (resident >= 0) view.SetBadge(Art.Heart);
            customer.Sprites = sprites;
            customer.Resident = resident;
            customer.HomeTile = home;
            _customers.Add(customer);
        }

        /// <summary>마을 길을 따라 from 에서 to 까지 걷는 사람을 만듭니다. 도착하면 onArrive 를 부르고 사라집니다.</summary>
        private CharacterView SpawnWalker(CharacterSprites sprites, Vector2Int from, Vector2Int to,
            System.Action onArrive)
        {
            List<Vector2Int> path = Village.FindPath(from, to, Village.IsRoad) ?? Village.FindPath(from, to)
                ?? new List<Vector2Int>();
            var go = new GameObject("Villager");
            var view = go.AddComponent<CharacterView>();
            view.Init(sprites, Art.Bubble);
            go.AddComponent<VillageWalker>().Init(Village, view, from, path, _customerSpeed * Random.Range(0.9f, 1.2f),
                onArrive);
            return view;
        }

        /// <summary>주인이 가게 문 밖으로 걸어 나가면 마을로, 마을에서 편의점 입구로 들어가면 가게 안으로 옮깁니다.</summary>
        private void UpdateDoors()
        {
            Vector2 input = _owner.MoveInput;
            if (!_inVillage)
            {
                Vector2Int tile = Map.WorldToTile(_owner.Position);
                if (input.y >= 0f || Map.TileAt(tile.x, tile.y) != 'p') return;
                int door = tile.x == Map.Entrances[0].x ? 0 : Map.Entrances.Count - 1;
                _owner.MoveTo(Village, Village.TileToWorld(Village.Entrances[Mathf.Min(door, Village.Entrances.Count - 1)]));
                _inVillage = true;
            }
            else
            {
                Vector2Int tile = Village.WorldToTile(_owner.Position);
                if (input.y <= 0f || Village.TileAt(tile.x, tile.y) != 'E') return;
                int door = tile.x == Village.Entrances[0].x ? 0 : Map.Entrances.Count - 1;
                _owner.MoveTo(Map, StoreMap.FeetPos(Map.Entrances[door]));
                _inVillage = false;
            }
        }

        // ── 계산·진열 ─────────────────────────────────────────────────────

        /// <summary>주인의 상호작용(Space/E)과 알바생의 자동 계산을 처리합니다.</summary>
        private void UpdateWork()
        {
            Customer front = _queue.Count > 0 && _queue[0].ReadyForCheckout ? _queue[0] : null;

            if (Economy.HasClerk && front != null)
            {
                _clerkTimer += Time.deltaTime;
                if (_clerkTimer >= _clerkCheckoutSeconds)
                {
                    Checkout(front, false);
                    front = null;
                }
            }

            bool pressed = Input.GetKeyDown(KeyCode.Space) || _autoPilot.TakePress();
            if (_showProducts) return;
            if (_inVillage)
            {
                if (_fishing.Active)
                {
                    _fishing.Update(pressed);
                    _prompt = _fishing.Prompt;
                }
                else if (!UpdateResidents(pressed) && !UpdateFishingPrompt(pressed)) UpdateFlyers(pressed);
                return;
            }
            if (Input.GetKeyDown(KeyCode.G)) PutHandBack();
            if (front != null && Map.IsClerkZone(_owner.Position))
            {
                _prompt = $"[Space] 계산하기 · {front.Product.Name} {front.Carrying}개";
                if (pressed) Checkout(front, true);
                return;
            }

            ShelfView shelf = NearestOpenShelf();
            if (shelf == null) return;
            Product product = shelf.Stock.Product;
            if (Input.GetKeyDown(KeyCode.Q))
            {
                Economy.CyclePrice(shelf.Stock);
                shelf.Refresh();
                _audio.Play(_audio.Flyer);
                Float(shelf.Center + Vector2.up, $"{product.Name} {StoreEconomy.TierName(shelf.Stock.Tier)}", Ink);
            }
            string price = $"[Q] 가격 {StoreEconomy.TierName(shelf.Stock.Tier)} "
                + Won(StoreEconomy.PriceOf(product, shelf.Stock.Tier));
            if (Input.GetKeyDown(KeyCode.F)) PickUp(shelf);
            price += _handProduct < 0 || _handProduct == shelf.Stock.Product.Id ? " · [F] 하나 집기" : "";
            if (shelf.Stock.Missing <= 0)
            {
                _prompt = $"{product.Name} · {price}";
                return;
            }
            int count = Economy.AffordableRestock(shelf.Stock);
            if (count <= 0)
            {
                _prompt = $"{product.Name} 채울 돈이 부족해요 (개당 {Won(product.Cost)}) · {price}";
                return;
            }
            _prompt = $"[Space] {product.Name} {count}개 채우기 -{Won(count * product.Cost)} · {price}";
            if (!pressed) return;
            Economy.Restock(shelf.Stock);
            shelf.Refresh();
            _audio.Play(_audio.Restock);
            Float(shelf.Center + Vector2.up, $"-{Won(count * product.Cost)}", Bad);
        }

        /// <summary>
        /// 마을에서 집 문 앞에 서면 전단지를 넣을 수 있습니다. 하루에 집마다 한 번,
        /// 그 집 사람이 곧바로 물건을 넉넉히 사러 나옵니다.
        /// </summary>
        private void UpdateFlyers(bool pressed)
        {
            if (_state != State.Open) return;
            for (int i = 0; i < Village.Houses.Count; i++)
            {
                Vector2 door = Village.TileToWorld(Village.HouseDoor(i));
                if (Vector2.Distance(door, _owner.Position) > FlyerRange) continue;
                string name = Economy.Residents[i].Name;
                if (_flyered[i])
                {
                    _prompt = $"{name}네 집에는 오늘 전단지를 넣었어요";
                    return;
                }
                if (_residentOut[i])
                {
                    _prompt = $"{name}네 집 · 지금은 아무도 없어요";
                    return;
                }
                _prompt = $"[E] {name}네 집에 전단지 넣기 · 바로 장 보러 나와요";
                if (Input.GetKeyDown(KeyCode.E)) GiveFlyer(i);
                return;
            }
        }

        private void GiveFlyer(int house)
        {
            _flyered[house] = true;
            Economy.AddReputation(1f);
            SendVillager(house, 3).ShowBubble(Art.Heart, 2.5f);
            Float(Village.TileToWorld(Village.HouseDoor(house)) + Vector2.up * 1.6f, "전단지 쏙!", Good);
            _audio.Play(_audio.Flyer);
        }

        // ── 마을 사람과 어울리기 ──────────────────────────────────────────

        /// <summary>
        /// 집 앞에 있는 마을 사람 곁에 서면 말을 걸거나(하루 한 번 호감도 +1) 부탁한 물건을 건넬 수 있습니다.
        /// 안내를 띄웠으면 true.
        /// </summary>
        private bool UpdateResidents(bool pressed)
        {
            for (int i = 0; i < _residentNpcs.Count; i++)
            {
                ResidentNpc npc = _residentNpcs[i];
                if (!npc.gameObject.activeSelf || Vector2.Distance(npc.Position, _owner.Position) > FlyerRange) continue;
                Resident resident = Economy.Residents[i];
                Quest quest = Economy.QuestFor(i);
                bool flyer = _state == State.Open && !_flyered[i];
                string extra = flyer ? " · [E] 전단지" : "";
                if (flyer && Input.GetKeyDown(KeyCode.E)) GiveFlyer(i);
                if (quest != null)
                {
                    Product wanted = ProductCatalog.All[quest.Product];
                    bool hasIt = _handProduct == quest.Product && _handCount >= quest.Quantity;
                    if (hasIt)
                    {
                        _prompt = $"[Space] {resident.Name}에게 {wanted.Name} {quest.Quantity}개 건네기 · 보상 {Won(quest.Reward)}{extra}";
                        if (pressed) Deliver(i, quest);
                        return true;
                    }
                    _prompt = $"{resident.Name}: {wanted.Name} {quest.Quantity}개 부탁해요! (손에 {HandText()})"
                        + (_talked[i] ? "" : " · [Space] 말 걸기") + extra;
                    if (pressed && !_talked[i]) Talk(i);
                    return true;
                }
                _prompt = (_talked[i] ? $"{resident.Name}: 오늘은 이야기 많이 했어요" : $"[Space] {resident.Name}에게 말 걸기") + extra;
                if (pressed && !_talked[i]) Talk(i);
                return true;
            }
            return false;
        }

        private void Talk(int index)
        {
            Resident resident = Economy.Residents[index];
            _talked[index] = true;
            int gift = Economy.ServeResident(index);
            Say(index, Dialogue(resident), 3f);
            if (gift > 0)
            {
                Float(_residentNpcs[index].Position + Vector2.up * 2.4f, $"{resident.Name}의 선물 +{Won(gift)}", Gold);
                _audio.Play(_audio.Buy);
            }
            else _audio.Play(_audio.Flyer);
        }

        private void Deliver(int index, Quest quest)
        {
            Resident resident = Economy.Residents[index];
            int earned = Economy.CompleteQuest(quest);
            _handProduct = -1;
            _handCount = 0;
            _ownerView.SetBadge(null);
            Say(index, "고마워요, 사장님! 이거 받으세요", 3f);
            Float(_residentNpcs[index].Position + Vector2.up * 2.4f, $"부탁 완료 +{Won(earned)}", Gold);
            _residentNpcs[index].GetComponent<CharacterView>().ShowBubble(Art.Heart, 3f);
            _audio.Play(_audio.DayEnd);
        }

        /// <summary>마을 사람 머리 위에 말을 띄웁니다.</summary>
        private void Say(int index, string text, float seconds)
        {
            Float(_residentNpcs[index].Position + Vector2.up * 1.7f, $"{Economy.Residents[index].Name}: {text}", Ink,
                seconds);
        }

        private static string Dialogue(Resident resident)
        {
            string favorite = ProductCatalog.All[resident.Favorite].Name;
            if (resident.Friendship >= 9) return "사장님 덕분에 동네가 살아요!";
            if (resident.Friendship >= 6) return $"{favorite} 있으면 꼭 들를게요";
            if (resident.Friendship >= 3) return "오늘도 가게 잘 되죠?";
            return "안녕하세요, 사장님!";
        }

        // ── 손에 든 물건 ───────────────────────────────────────────────────

        private string HandText() => _handProduct < 0 ? "빈손" : $"{ProductCatalog.All[_handProduct].Name} {_handCount}개";

        /// <summary>진열대에서 하나 집어 듭니다. 같은 상품만 최대 3개까지.</summary>
        private void PickUp(ShelfView shelf)
        {
            ShelfStock stock = shelf.Stock;
            int id = stock.Product.Id;
            if (_handProduct >= 0 && _handProduct != id)
            {
                Float(_owner.Position + Vector2.up * 1.6f, $"{HandText()}를 들고 있어요 · G 로 내려놓기", Bad);
                return;
            }
            if (_handCount >= HandMax || stock.Take(1) == 0) return;
            _handProduct = id;
            _handCount++;
            shelf.Refresh();
            _ownerView.SetBadge(Art.ProductIcons[id]);
            _audio.Play(_audio.Restock, 0.6f);
        }

        /// <summary>손에 든 물건을 원래 진열대에 돌려놓습니다.</summary>
        private void PutHandBack()
        {
            if (_handProduct < 0) return;
            _shelfViews[_handProduct].Stock.Add(_handCount);
            _shelfViews[_handProduct].Refresh();
            _handProduct = -1;
            _handCount = 0;
            _ownerView.SetBadge(null);
        }

        // ── 상품 관리 ─────────────────────────────────────────────────────

        /// <summary>P 로 여닫는 상품 관리 화면. ↑↓ 로 고르고 Space 로 판매/보류, Enter 로 들여오기.</summary>
        private void UpdateProductPanel()
        {
            if (Input.GetKeyDown(KeyCode.P) && !_fishing.Active && _state != State.Summary)
            {
                _showProducts = !_showProducts;
                _owner.CanMove = !_showProducts && _state != State.Summary;
            }
            if (!_showProducts) return;

            int count = Economy.Shelves.Length;
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) _productCursor = (_productCursor + count - 1) % count;
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) _productCursor = (_productCursor + 1) % count;

            ShelfStock shelf = Economy.Shelves[_productCursor];
            if (Input.GetKeyDown(KeyCode.Space) && shelf.Open)
            {
                shelf.Selling = !shelf.Selling;
                _shelfViews[_productCursor].Refresh();
                _audio.Play(_audio.Flyer);
            }
            if (Input.GetKeyDown(KeyCode.Return) && !shelf.Open)
            {
                if (Economy.TryUnlock(_productCursor))
                {
                    _shelfViews[_productCursor].Refresh();
                    _audio.Play(_audio.Buy);
                }
                else _audio.Play(_audio.Angry, 0.5f);
            }
        }

        /// <summary>주인이 직접 빨리 계산해 주면 팁을 받습니다. 알바생이 계산하면 팁은 없습니다.</summary>
        private void Checkout(Customer customer, bool byOwner)
        {
            Vector2 at = (Vector2)customer.transform.position + Vector2.up * 1.4f;
            bool regular = customer.Resident >= 0;
            bool rude = customer.Kind == CustomerKind.Rude, kind = customer.Kind == CustomerKind.Kind;
            float tipMultiplier = customer.TipMultiplier
                * (regular ? Economy.ResidentTipMultiplier(customer.Resident) : 1f);
            int tip = byOwner && !rude
                ? Economy.TipFor(customer.Product, customer.Carrying, customer.PatienceRatio, tipMultiplier, kind)
                : 0;
            string tipName = customer.Kind == CustomerKind.Vip ? "VIP 팁"
                : customer.Kind == CustomerKind.Hurried ? "바쁜 손님 팁"
                : kind ? "착한 손님의 팁"
                : regular ? "단골 팁" : "빠른 계산 팁";
            // 진상은 값을 깎고(싸게 가격) 평판도 안 오른다. 착한 손님은 평판을 두 배로 올려 준다.
            PriceTier tier = rude ? PriceTier.Cheap : customer.Shelf.Tier;
            float gain = rude ? 0f : kind ? 4f : -1f;
            int income = Economy.Sell(customer.Product, customer.Carrying, tip, tier, gain);
            if (rude) Float(at + Vector2.up * 1.8f, "진상 손님 보냈다...", Ink);
            else if (kind) Float(at + Vector2.up * 1.8f, "착한 손님! 평판 UP", Good);
            if (regular)
            {
                Resident resident = Economy.Residents[customer.Resident];
                int before = resident.Friendship;
                int gift = Economy.ServeResident(customer.Resident);
                if (resident.Friendship > before)
                {
                    Float(at + Vector2.up * 1.8f,
                        $"{resident.Name} 호감도 {resident.Friendship}/{StoreEconomy.MaxFriendship}", Bad);
                }
                if (gift > 0)
                {
                    Float(at + Vector2.up * 2.4f, $"{resident.Name}의 선물 +{Won(gift)}", Gold);
                    _audio.Play(_audio.Buy);
                }
            }
            Float(at, $"+{Won(income)}", Good);
            if (tip > 0) Float(at + Vector2.up * 0.6f, $"{tipName} +{Won(tip)}", Gold);
            if (Economy.Streak % StoreEconomy.StreakStep == 0)
            {
                Float(at + Vector2.up * 1.2f, $"연속 {Economy.Streak}명! 평판 쑥", Gold);
            }
            _audio.Play(tip > 0 ? _audio.Tip : _audio.Coin);
            _queue.Remove(customer);
            _clerkTimer = 0f;
            customer.CompleteCheckout();
        }

        private ShelfView NearestOpenShelf()
        {
            ShelfView best = null;
            float bestDist = _restockRange;
            foreach (ShelfView view in _shelfViews)
            {
                if (!view.Stock.Open) continue;
                float dist = Vector2.Distance(view.Center, _owner.Position);
                if (dist >= bestDist) continue;
                bestDist = dist;
                best = view;
            }
            return best;
        }

        // ── 손님이 호출하는 것들 ──────────────────────────────────────────

        /// <summary>줄에 섭니다. 진상 손님은 맨 앞으로 새치기합니다.</summary>
        public void JoinQueue(Customer customer, bool cutInLine = false)
        {
            if (cutInLine) _queue.Insert(0, customer);
            else _queue.Add(customer);
        }

        /// <summary>줄에 진상 손님이 있으면 다른 손님들은 두 배로 빨리 지칩니다.</summary>
        public float PatienceDrain(Customer customer)
        {
            if (customer.Kind == CustomerKind.Rude) return 1f;
            foreach (Customer other in _queue)
            {
                if (other != customer && other.Kind == CustomerKind.Rude) return 2f;
            }
            return 1f;
        }

        /// <summary>진열 알바가 채우러 갈 진열대. 재고가 1/3 아래로 떨어졌고 채울 돈이 있는 것 중 가장 빈 곳. 없으면 -1.</summary>
        public int ShelfNeedingRestock()
        {
            int best = -1, bestStock = int.MaxValue;
            for (int i = 0; i < _shelfViews.Count; i++)
            {
                ShelfStock stock = _shelfViews[i].Stock;
                if (!stock.OnSale || stock.Stock > stock.Capacity / 3 || Economy.AffordableRestock(stock) <= 0) continue;
                if (stock.Stock >= bestStock) continue;
                bestStock = stock.Stock;
                best = i;
            }
            return best;
        }

        /// <summary>진열 알바가 진열대를 채웁니다.</summary>
        public void RestockByStocker(int shelfIndex)
        {
            ShelfView shelf = _shelfViews[shelfIndex];
            int count = Economy.Restock(shelf.Stock);
            if (count <= 0) return;
            shelf.Refresh();
            if (!_inVillage) _audio.Play(_audio.Restock, 0.6f);
            Float(shelf.Center + Vector2.up, $"-{Won(count * shelf.Stock.Product.Cost)}", Bad);
        }

        public void LeaveQueue(Customer customer) => _queue.Remove(customer);

        public int QueueIndex(Customer customer) => _queue.IndexOf(customer);

        /// <summary>이 손님이 지금 서야 할 줄 칸. 줄이 칸 수보다 길면 맨 끝 칸에 겹쳐 섭니다.</summary>
        public Vector2Int QueueSpot(Customer customer)
        {
            int index = Mathf.Clamp(_queue.IndexOf(customer), 0, Map.QueueSpots.Count - 1);
            return Map.QueueSpots[index];
        }

        public void RefreshShelf(int shelfIndex) => _shelfViews[shelfIndex].Refresh();

        public void OnCustomerLost(Customer customer)
        {
            Economy.LoseCustomer();
            _audio.Play(_audio.Angry, _inVillage ? 0.4f : 1f);
        }

        public void OnCustomerGone(Customer customer)
        {
            // 가게를 나선 손님은 마을 길을 따라 집으로 돌아간다.
            WalkHome(customer.Sprites, customer.Resident, customer.HomeTile);
            _customers.Remove(customer);
            _queue.Remove(customer);
        }

        // ── 가게 만들기 ───────────────────────────────────────────────────

        private void SetupCamera()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.orthographic = true;
            cam.orthographicSize = Map.Height * 0.5f + 0.2f;
            cam.transform.position = new Vector3(Map.Width * 0.5f, Map.Height * 0.5f, -10f);
            cam.transform.rotation = Quaternion.identity;
            cam.backgroundColor = new Color(0.46f, 0.73f, 0.35f);
        }

        private void BuildStore()
        {
            const int m = StoreArt.BackgroundMargin;
            MakeSprite("Background", Art.Background, new Vector2(-m, -m), 0);

            for (int i = 0; i < Map.Shelves.Count; i++)
            {
                var view = new GameObject($"Shelf {i + 1}").AddComponent<ShelfView>();
                view.Init(Art, Map.Shelves[i]);
                _shelfViews.Add(view);
            }

            Vector2Int counter = Map.Counter[0];
            MakeSprite("Counter", Art.Counter, counter, StoreArt.SortOrder(counter.y));
            foreach (Vector2Int plant in Map.Plants)
            {
                MakeSprite("Plant", Art.Plant, plant, StoreArt.SortOrder(plant.y));
            }

            var ownerGo = new GameObject("Owner");
            _ownerView = ownerGo.AddComponent<CharacterView>();
            _ownerView.Init(Art.Owner, Art.Bubble);
            _owner = ownerGo.AddComponent<StoreOwner>();
            _owner.Init(Map, _ownerView, StoreMap.FeetPos(Map.ClerkSpot), _ownerSpeed);
            _fishing = new Fishing(this, _owner, _ownerView, _audio);
            _autoPilot = new AutoPilot(this, _owner);
#if UNITY_EDITOR
            // 개발용: 이 파일이 있으면 자동 플레이로 시작한다 (Library/ 는 Git 에 들어가지 않는다).
            _autoPilot.Enabled = System.IO.File.Exists("Library/AutoPlay/autopilot.txt");
#endif

            // 마을 사람들은 집 앞에서 어슬렁거린다. 장 보러 나가면 숨긴다.
            for (int i = 0; i < Village.Houses.Count; i++)
            {
                var npcGo = new GameObject($"Resident {i + 1}");
                var npcView = npcGo.AddComponent<CharacterView>();
                npcView.Init(_residentSprites[i], Art.Bubble);
                npcView.SetBadge(Art.Heart);
                var npc = npcGo.AddComponent<ResidentNpc>();
                npc.Init(Village, npcView, Village.HouseDoor(i) + Vector2Int.right);
                _residentNpcs.Add(npc);
            }

            // 진열 알바는 고용하기 전까지 숨겨 둔다. 가게 오른쪽 위 구석에서 대기한다.
            var stockerGo = new GameObject("Stocker");
            var stockerView = stockerGo.AddComponent<CharacterView>();
            stockerView.Init(Art.Stocker, Art.Bubble);
            _stocker = stockerGo.AddComponent<Stocker>();
            _stocker.Init(this, stockerView, new Vector2Int(Map.Width - 5, Map.Height - 6), _customerSpeed * 1.2f);

            // 알바생은 고용하기 전까지 숨겨 둔다. 계산대 뒤에서 손님 쪽(왼쪽)을 본다.
            _clerk = new GameObject("Clerk");
            var clerkView = _clerk.AddComponent<CharacterView>();
            clerkView.Init(Art.Clerk, Art.Bubble);
            clerkView.Face(Vector2.left);
            clerkView.Tick(false);
            _clerk.transform.position = StoreMap.FeetPos(Map.ClerkSpot + Vector2Int.up);

            // 시간대에 따라 화면 전체에 색을 입히는 막
            _daylight = MakeSprite("Daylight", Art.WhitePixel, new Vector2(Map.Width * 0.5f, Map.Height * 0.5f), 8000);
            _daylight.transform.localScale = new Vector3(Map.Width + m * 2, Map.Height + m * 2, 1f);
        }

        private void BuildVillage()
        {
            MakeSprite("Village", _villageArt.Background, VillageOrigin, 0);
            for (int i = 0; i < Village.Houses.Count; i++)
            {
                Vector2Int house = Village.Houses[i];
                MakeSprite($"House {i + 1}", _villageArt.Houses[i], VillageOrigin + house, StoreArt.SortOrder(house.y));
            }
            MakeSprite("Store Building", _villageArt.Store, VillageOrigin + Village.Store,
                StoreArt.SortOrder(Village.Store.y));
            foreach (Vector2Int tree in Village.Trees)
            {
                MakeSprite("Tree", _villageArt.TreeAt(tree), VillageOrigin + tree + new Vector2(0.5f, 0.1f),
                    StoreArt.SortOrder(tree.y + 0.1f));
            }
            foreach (Vector2Int lamp in Village.Lamps)
            {
                MakeSprite("Lamp", _villageArt.Lamp, VillageOrigin + lamp, StoreArt.SortOrder(lamp.y));
                // 불빛은 화면에 색을 입히는 막보다 위에 그려야 밤에 환해 보인다.
                SpriteRenderer glow = MakeSprite("Lamp Glow", _villageArt.Glow,
                    VillageOrigin + lamp + new Vector2(0.5f, 1.7f), 8500);
                glow.transform.localScale = Vector3.one * 1.6f;
                _glows.Add(glow);
            }
        }

        /// <summary>가게 안에서는 화면을 고정하고, 마을에서는 주인을 따라가되 맵 밖은 비추지 않습니다.</summary>
        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector2 center = new Vector2(Map.Width * 0.5f, Map.Height * 0.5f);
            if (_inVillage)
            {
                float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
                center.x = ClampCenter(_owner.Position.x, VillageOrigin.x, Village.Width, halfW);
                center.y = ClampCenter(_owner.Position.y + 0.5f, VillageOrigin.y, Village.Height, halfH);
            }
            cam.transform.position = new Vector3(center.x, center.y, -10f);
            _daylight.transform.position = center;
        }

        private static float ClampCenter(float value, float min, float size, float half) =>
            size <= half * 2f ? min + size * 0.5f : Mathf.Clamp(value, min + half, min + size - half);

        /// <summary>저장해 둔 가게가 있으면 이어 하고, 없거나 읽을 수 없으면 새로 시작합니다.</summary>
        private static StoreEconomy LoadEconomy()
        {
            string json = PlayerPrefs.GetString(SaveKey, "");
            if (string.IsNullOrEmpty(json)) return new StoreEconomy();
            try
            {
                return new StoreEconomy(JsonUtility.FromJson<StoreSave>(json));
            }
            catch (System.ArgumentException)
            {
                return new StoreEconomy();
            }
        }

        private void BindShelves()
        {
            for (int i = 0; i < _shelfViews.Count; i++)
            {
                _shelfViews[i].Bind(Economy.Shelves[i], Art.ProductIcons[Economy.Shelves[i].Product.Id]);
            }
        }

        private static SpriteRenderer MakeSprite(string name, Sprite sprite, Vector2 position, int sortingOrder)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = sortingOrder;
            sr.transform.position = position;
            return sr;
        }

        /// <summary>아침은 살짝 분홍빛, 저녁은 노을빛, 밤은 푸르스름하게.</summary>
        private void UpdateDaylight()
        {
            var clear = new Color(1f, 0.8f, 0.6f, 0f);
            var dawn = new Color(1f, 0.75f, 0.6f, 0.14f);
            var sunset = new Color(1f, 0.55f, 0.3f, 0.2f);
            var night = new Color(0.1f, 0.14f, 0.4f, 0.4f);
            Color c;
            if (_hour < 9.5f) c = Color.Lerp(dawn, clear, Mathf.InverseLerp(8f, 9.5f, _hour));
            else if (_hour < 16f) c = clear;
            else if (_hour < 18.5f) c = Color.Lerp(clear, sunset, Mathf.InverseLerp(16f, 18.5f, _hour));
            else c = Color.Lerp(sunset, night, Mathf.InverseLerp(18.5f, 21f, _hour));
            if (Economy.Event == DayEvent.Rainy)
            {
                // 비 오는 날은 하루 종일 흐리다.
                var cloudy = new Color(0.35f, 0.42f, 0.58f, Mathf.Max(c.a, 0.24f));
                c = Color.Lerp(c, cloudy, 0.6f);
                c.a = cloudy.a;
            }
            _daylight.color = c;

            var glowColor = new Color(1f, 1f, 1f, Mathf.InverseLerp(17.5f, 20f, _hour) * 0.8f);
            foreach (SpriteRenderer glow in _glows) glow.color = glowColor;
        }

        // ── HUD ───────────────────────────────────────────────────────────

        internal void Float(Vector2 worldPos, string text, Color color, float seconds = FloatingTextSeconds)
        {
            _texts.Add(new FloatingText { Pos = worldPos, Text = text, Color = color, Life = seconds });
        }

        internal static string Won(int amount) => $"{amount:N0}원";

        private void OnGUI()
        {
            if (Economy == null) return;
            float s = Screen.height / 720f;
            EnsureStyles(s);

            if (Economy.Event == DayEvent.Rainy && _inVillage && _state != State.Summary) DrawRain(s);
            DrawTopBar(s);
            DrawFloatingTexts(s);
            if (_autoPilot.Enabled)
            {
                string autoText = $"자동 플레이 · {_autoPilot.Status} · F1 끄기";
                float autoW = _labelStyle.CalcSize(new GUIContent(autoText)).x + 30f * s;
                var auto = new Rect(Screen.width - autoW - 12f * s, 64f * s, autoW, 40f * s);
                GUI.Box(auto, GUIContent.none, _panelStyle);
                Label(auto, autoText, Good, TextAnchor.MiddleCenter);
            }

            if (_state == State.Summary)
            {
                DrawSummary(s);
                return;
            }

            if (_showNotebook) DrawNotebook(s);
            if (_showProducts) DrawProductPanel(s);
            DrawQuestLine(s);
            if (_fishing.Active) _fishing.Draw(s, _panelStyle, _labelStyle, Ink);
            float bottom = Screen.height - 64f * s;
            // 부탁 줄 아래에 띄운다.
            if (_bannerTimer > 0f) DrawPrompt(_banner, s, Economy.Quests.Count > 0 ? 112f * s : 70f * s);
            if (_prompt != null)
            {
                DrawPrompt(_prompt, s, bottom);
            }
            else if (Economy.Day == 1 && _hour < _openHour + 3f)
            {
                DrawPrompt("방향키/WASD 이동 · Space: 계산/채우기 · P: 상품 관리 · Tab: 마을 수첩", s, bottom);
            }
        }

        /// <summary>연못가에 서면 낚시를 시작할 수 있습니다. 낚시 안내를 띄웠으면 true.</summary>
        private bool UpdateFishingPrompt(bool pressed)
        {
            if (_state != State.Open && _state != State.Closing) return false;
            Vector2Int tile = Village.WorldToTile(_owner.Position);
            bool nearWater = false;
            foreach (Vector2Int d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
            {
                if (Village.TileAt(tile.x + d.x, tile.y + d.y) == 'w') nearWater = true;
            }
            if (!nearWater) return false;
            _prompt = "[Space] 낚시하기 · 입질이 오면 Space, 초록 칸에서 다시 Space";
            if (pressed) _fishing.Start();
            return true;
        }

        /// <summary>마을 수첩: 집마다 사는 단골의 이름, 좋아하는 상품, 호감도(하트 하나가 2).</summary>
        private void DrawNotebook(float s)
        {
            Resident[] residents = Economy.Residents;
            float line = 40f * s, w = 620f * s, h = 130f * s + line * residents.Length;
            var panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            GUI.Box(panel, GUIContent.none, _panelStyle);
            _titleStyle.normal.textColor = Ink;
            GUI.Label(new Rect(panel.x, panel.y + 18f * s, w, 44f * s), "마을 수첩", _titleStyle);

            float x = panel.x + 36f * s, y = panel.y + 72f * s, icon = 21f * s;
            for (int i = 0; i < residents.Length; i++)
            {
                Resident r = residents[i];
                bool unlocked = Economy.Shelves[r.Favorite].OnSale;
                Label(new Rect(x, y, 200f * s, line), $"{i + 1}번 집  {r.Name}", Ink, TextAnchor.MiddleLeft);
                Label(new Rect(x + 200f * s, y, 220f * s, line), $"좋아하는 것: {ProductCatalog.All[r.Favorite].Name}",
                    unlocked ? Ink : InkFaded, TextAnchor.MiddleLeft);
                for (int k = 0; k < StoreEconomy.MaxFriendship / 2; k++)
                {
                    GUI.DrawTexture(new Rect(x + 424f * s + k * (icon + 3f * s), y + (line - icon) * 0.5f, icon, icon),
                        r.Friendship >= (k + 1) * 2 ? Art.HeartTexture : Art.HeartEmptyTexture);
                }
                y += line;
            }
            Label(new Rect(panel.x, panel.yMax - 50f * s, w, line),
                "계산해 주거나 말을 걸거나 부탁을 들어주면 친해져요 · Tab: 닫기", Ink, TextAnchor.MiddleCenter);
        }

        /// <summary>상단 바 아래에 오늘의 부탁과 손에 든 물건을 한 줄로 보여 줍니다.</summary>
        private void DrawQuestLine(float s)
        {
            if (_state == State.Summary || Economy.Quests.Count == 0) return;
            var parts = new List<string>();
            foreach (Quest q in Economy.Quests)
            {
                string who = Economy.Residents[q.Resident].Name, what = ProductCatalog.All[q.Product].Name;
                parts.Add(q.Done ? $"{who}: {what} {q.Quantity}개 (완료)" : $"{who}: {what} {q.Quantity}개 → {Won(q.Reward)}");
            }
            string text = "부탁  " + string.Join("  ·  ", parts) + (_handProduct >= 0 ? $"   |   손: {HandText()}" : "");
            float w = _labelStyle.CalcSize(new GUIContent(text)).x + 40f * s, h = 36f * s;
            var rect = new Rect(12f * s, 64f * s, w, h);
            GUI.Box(rect, GUIContent.none, _panelStyle);
            Label(rect, text, Ink, TextAnchor.MiddleCenter);
        }

        /// <summary>상품 관리 화면. 들여온 상품은 판매/보류를 고르고, 안 들여온 상품은 값을 치르고 들여옵니다.</summary>
        private void DrawProductPanel(float s)
        {
            ShelfStock[] shelves = Economy.Shelves;
            float line = 34f * s, w = 700f * s, h = 150f * s + line * shelves.Length;
            var panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            GUI.Box(panel, GUIContent.none, _panelStyle);
            _titleStyle.normal.textColor = Ink;
            GUI.Label(new Rect(panel.x, panel.y + 16f * s, w, 44f * s), "상품 관리", _titleStyle);

            float x = panel.x + 36f * s, y = panel.y + 70f * s, icon = 21f * s;
            for (int i = 0; i < shelves.Length; i++)
            {
                ShelfStock shelf = shelves[i];
                Product p = shelf.Product;
                bool selected = i == _productCursor;
                if (selected)
                {
                    GUI.color = new Color(1f, 0.85f, 0.4f, 0.35f);
                    GUI.DrawTexture(new Rect(panel.x + 20f * s, y, w - 40f * s, line), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }
                GUI.DrawTexture(new Rect(x, y + (line - icon) * 0.5f, icon, icon), Art.ProductIcons[i].texture);
                Color color = shelf.Open ? Ink : InkFaded;
                Label(new Rect(x + 32f * s, y, 150f * s, line), p.Name, color, TextAnchor.MiddleLeft);
                Label(new Rect(x + 180f * s, y, 220f * s, line),
                    $"매입 {Won(p.Cost)} · 판매 {Won(StoreEconomy.PriceOf(p, shelf.Tier))}", color, TextAnchor.MiddleLeft);
                string status = !shelf.Open ? $"미입고 · 들여오기 {Won(p.UnlockCost)}"
                    : shelf.Selling ? $"판매 중 · 재고 {shelf.Stock}/{shelf.Capacity}" : $"보류 · 재고 {shelf.Stock}/{shelf.Capacity}";
                Color statusColor = !shelf.Open ? (Economy.Money >= p.UnlockCost ? Ink : InkFaded)
                    : shelf.Selling ? Good : Bad;
                Label(new Rect(x + 400f * s, y, 260f * s, line), status, statusColor, TextAnchor.MiddleLeft);
                y += line;
            }
            Label(new Rect(panel.x, panel.yMax - 50f * s, w, 30f * s),
                "↑↓ 고르기 · Space 판매/보류 · Enter 들여오기 · P 닫기", Ink, TextAnchor.MiddleCenter);
        }

        /// <summary>화면 위에 빗줄기를 그립니다. 빗방울마다 정해진 자리에서 아래로 흘러내립니다.</summary>
        private void DrawRain(float s)
        {
            GUI.color = new Color(0.85f, 0.93f, 1f, 0.55f);
            for (int i = 0; i < 90; i++)
            {
                float x = Mathf.Repeat(i * 0.6180339f, 1f) * Screen.width;
                float speed = 1.1f + Mathf.Repeat(i * 0.377f, 0.6f);
                float y = Mathf.Repeat(i * 0.2718f + Time.time * speed, 1f) * (Screen.height + 30f * s) - 30f * s;
                GUI.DrawTexture(new Rect(x, y, 2f * s, 14f * s), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
        }

        /// <summary>화면 가운데 줄 y 위치에 글자 길이에 맞춘 안내 패널을 그립니다.</summary>
        private void DrawPrompt(string text, float s, float y)
        {
            float w = _labelStyle.CalcSize(new GUIContent(text)).x + 56f * s, h = 46f * s;
            var rect = new Rect((Screen.width - w) * 0.5f, y, w, h);
            GUI.Box(rect, GUIContent.none, _panelStyle);
            Label(rect, text, Ink, TextAnchor.MiddleCenter);
        }

        private void DrawTopBar(float s)
        {
            var bar = new Rect(12f * s, 10f * s, 920f * s, 48f * s);
            GUI.Box(bar, GUIContent.none, _panelStyle);

            float x = bar.x + 20f * s, y = bar.y, h = bar.height, icon = 21f * s;
            Label(new Rect(x, y, 90f * s, h), $"DAY {Economy.Day}", Ink, TextAnchor.MiddleLeft);
            x += 96f * s;

            int minutes = Mathf.FloorToInt(Mathf.Min(_hour, _closeHour) * 6f) * 10;
            string status = _state == State.Open ? "" : " 마감";
            Label(new Rect(x, y, 130f * s, h), $"{minutes / 60:00}:{minutes % 60:00}{status}",
                _state == State.Open ? Ink : Bad, TextAnchor.MiddleLeft);
            x += 136f * s;

            GUI.DrawTexture(new Rect(x, y + (h - icon) * 0.5f, icon, icon), Art.CoinTexture);
            x += icon + 8f * s;
            Label(new Rect(x, y, 150f * s, h), Won(Economy.Money), Economy.Money < 0 ? Bad : Ink,
                TextAnchor.MiddleLeft);
            x += 156f * s;

            Label(new Rect(x, y, 60f * s, h), "평판", Ink, TextAnchor.MiddleLeft);
            x += 54f * s;
            int hearts = Mathf.RoundToInt(Economy.Reputation / 20f);
            for (int i = 0; i < 5; i++)
            {
                GUI.DrawTexture(new Rect(x + i * (icon + 3f * s), y + (h - icon) * 0.5f, icon, icon),
                    i < hearts ? Art.HeartTexture : Art.HeartEmptyTexture);
            }
            x += 5f * (icon + 3f * s) + 14f * s;

            bool reached = Economy.ServedToday >= Economy.DailyGoal;
            Label(new Rect(x, y, 150f * s, h), $"목표 {Economy.ServedToday}/{Economy.DailyGoal}명", reached ? Good : Ink,
                TextAnchor.MiddleLeft);
            x += 150f * s;
            Label(new Rect(x, y, 130f * s, h), StoreEconomy.EventName(Economy.Event), Ink, TextAnchor.MiddleLeft);
        }

        private void DrawFloatingTexts(float s)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            foreach (FloatingText t in _texts)
            {
                float k = t.Age / t.Life;
                Vector3 screen = cam.WorldToScreenPoint(t.Pos + Vector2.up * (k * 0.8f));
                var rect = new Rect(screen.x - 200f * s, Screen.height - screen.y - 20f * s, 400f * s, 40f * s);
                float alpha = Mathf.Clamp01(2f - k * 2f);
                // 흰 테두리를 깔아 어떤 배경 위에서도 읽히게 한다.
                _floatStyle.normal.textColor = new Color(1f, 1f, 1f, alpha);
                foreach (Vector2 d in new[] { Vector2.left, Vector2.right, Vector2.up, Vector2.down })
                {
                    GUI.Label(new Rect(rect.x + d.x * 2f * s, rect.y + d.y * 2f * s, rect.width, rect.height), t.Text,
                        _floatStyle);
                }
                _floatStyle.normal.textColor = new Color(t.Color.r, t.Color.g, t.Color.b, alpha);
                GUI.Label(rect, t.Text, _floatStyle);
            }
        }

        private void DrawSummary(float s)
        {
            float w = 600f * s, h = 690f * s;
            var panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            GUI.Box(panel, GUIContent.none, _panelStyle);

            float x = panel.x + 40f * s, right = panel.xMax - 40f * s, y = panel.y + 22f * s, line = 30f * s;
            _titleStyle.normal.textColor = Ink;
            GUI.Label(new Rect(panel.x, y, w, 44f * s), $"DAY {_report.Day} 영업 종료!", _titleStyle);
            y += 54f * s;

            void Row(string name, string value, Color color)
            {
                Label(new Rect(x, y, right - x, line), name, color, TextAnchor.MiddleLeft);
                Label(new Rect(x, y, right - x, line), value, color, TextAnchor.MiddleRight);
                y += line;
            }

            Row("매출", $"+{Won(_report.Sales)}", Ink);
            if (_report.Tips > 0) Row("팁", $"+{Won(_report.Tips)}", Ink);
            if (_report.Gifts > 0) Row("단골의 선물", $"+{Won(_report.Gifts)}", Good);
            if (_report.Extra > 0) Row("낚시 수입", $"+{Won(_report.Extra)}", Good);
            if (_report.QuestsDone > 0) Row($"부탁 보상 ({_report.QuestsDone}건)", $"+{Won(_report.QuestReward)}", Good);
            if (_report.GoalBonus > 0) Row($"목표 달성 보너스 ({_report.Goal}명)", $"+{Won(_report.GoalBonus)}", Good);
            Row("상품 매입", $"-{Won(_report.RestockCost)}", Ink);
            Row("임대료", $"-{Won(_report.Rent)}", Ink);
            if (_report.Wage > 0) Row("알바비", $"-{Won(_report.Wage)}", Ink);
            Row("오늘 순이익", (_report.Profit >= 0 ? "+" : "-") + Won(Mathf.Abs(_report.Profit)),
                _report.Profit >= 0 ? Good : Bad);
            Row("손님", $"{_report.Served}명 (놓친 손님 {_report.Lost}명)", Ink);
            Row("잔고", Won(Economy.Money), Economy.Money < 0 ? Bad : Ink);
            y += 12f * s;

            if (_report.Bankrupt)
            {
                y += 30f * s;
                GUI.Label(new Rect(panel.x, y, w, 44f * s), "잔고가 바닥나 폐업했어요...", _titleStyle);
                Label(new Rect(panel.x, panel.yMax - 54f * s, w, line), "Space: 처음부터 다시 시작", Ink,
                    TextAnchor.MiddleCenter);
                return;
            }

            Label(new Rect(panel.x, y, w, line), "~ 가게 업그레이드 (숫자 키로 구매) ~", Ink, TextAnchor.MiddleCenter);
            y += line + 4f * s;
            for (int i = 0; i < UpgradeKeys.Length; i++)
            {
                int cost = Economy.UpgradeCost(UpgradeKeys[i]);
                Color color = cost >= 0 && Economy.Money >= cost ? Ink : InkFaded;
                Row($"[{i + 1}] {UpgradeName(UpgradeKeys[i])}", cost < 0 ? "완료" : Won(cost), color);
            }

            Label(new Rect(panel.x, panel.yMax - 54f * s, w, line), "Space: 다음 날 시작 · P: 상품 관리(영업 중) · Backspace: 새 게임", Ink,
                TextAnchor.MiddleCenter);
        }

        private string UpgradeName(Upgrade upgrade)
        {
            switch (upgrade)
            {
                case Upgrade.ShelfSize:
                    return $"진열대 확장 (지금 {Economy.ShelfCapacity}칸)";
                case Upgrade.Clerk:
                    return $"계산 알바 고용 (자동 계산, 일당 {Won(StoreEconomy.ClerkWage)})";
                case Upgrade.Stocker:
                    return $"진열 알바 고용 (빈 진열대 채움, 일당 {Won(StoreEconomy.StockerWage)})";
                default:
                    return $"전단지 홍보 Lv.{Economy.MarketingLevel} (손님 +20%)";
            }
        }

        private void Label(Rect rect, string text, Color color, TextAnchor anchor)
        {
            _labelStyle.normal.textColor = color;
            _labelStyle.alignment = anchor;
            GUI.Label(rect, text, _labelStyle);
        }

        private void EnsureStyles(float s)
        {
            if (_panelStyle == null)
            {
                _panelStyle = new GUIStyle { border = new RectOffset(12, 12, 12, 12) };
                _panelStyle.normal.background = Art.PanelTexture;
                _labelStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, wordWrap = false };
                _titleStyle = new GUIStyle(_labelStyle) { alignment = TextAnchor.MiddleCenter };
                _floatStyle = new GUIStyle(_labelStyle) { alignment = TextAnchor.MiddleCenter };
            }
            _labelStyle.fontSize = Mathf.RoundToInt(20f * s);
            _titleStyle.fontSize = Mathf.RoundToInt(32f * s);
            _floatStyle.fontSize = Mathf.RoundToInt(24f * s);
        }
    }
}
