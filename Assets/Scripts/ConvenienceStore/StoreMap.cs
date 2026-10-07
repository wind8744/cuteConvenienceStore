using System.Collections.Generic;
using UnityEngine;

namespace ConvenienceStore
{
    /// <summary>
    /// 가게 타일 맵. 문자 배치도를 읽어 진열대·계산대·문 위치를 찾고, 손님이 걸어갈 길을 찾습니다.
    /// 타일 (x, y) 는 월드 좌표 [x, x+1] × [y, y+1] 을 차지하고 y 는 위쪽이 큽니다.
    /// </summary>
    public class StoreMap : TileGrid
    {
        /// <summary>
        /// 위쪽 줄부터 적습니다. g 풀밭, p 길, # 벽, w 안쪽 벽면, . 바닥, + 문,
        /// 1~9·a~c 진열대(가로 2칸, 상품 순서), K 계산대, L 화분.
        /// </summary>
        public static readonly string[] DefaultLayout =
        {
            "gggggggggggggggggggggggg",
            "ggg##################ggg",
            "ggg#wwwwwwwwwwwwwwww#ggg",
            "ggg#L..............L#ggg",
            "ggg#..11..22..33.44.#ggg",
            "ggg#................#ggg",
            "ggg#..55..66..77.88.#ggg",
            "ggg#................#ggg",
            "ggg#..99..aa..bb.cc.#ggg",
            "ggg#................#ggg",
            "ggg#............K...#ggg",
            "ggg#............K...#ggg",
            "ggg#............K...#ggg",
            "ggg#######++#########ggg",
            "ggggggggggppgggggggggggg",
        };

        public const int QueueLength = 6;

        private readonly List<Vector2Int> _shelves = new List<Vector2Int>();
        private readonly List<Vector2Int> _counter = new List<Vector2Int>();
        private readonly List<Vector2Int> _plants = new List<Vector2Int>();
        private readonly List<Vector2Int> _doors = new List<Vector2Int>();
        private readonly List<Vector2Int> _queue = new List<Vector2Int>();

        /// <summary>진열대의 왼쪽 칸. 번호 순서.</summary>
        public IReadOnlyList<Vector2Int> Shelves => _shelves;
        /// <summary>계산대 칸. 아래쪽부터.</summary>
        public IReadOnlyList<Vector2Int> Counter => _counter;
        public IReadOnlyList<Vector2Int> Plants => _plants;
        /// <summary>문 바로 바깥의 길 칸. 손님이 드나드는 곳.</summary>
        public IReadOnlyList<Vector2Int> Entrances => _doors;
        /// <summary>계산 줄. 0번이 계산대 바로 앞.</summary>
        public IReadOnlyList<Vector2Int> QueueSpots => _queue;
        /// <summary>계산대 뒤 점원이 서는 칸.</summary>
        public Vector2Int ClerkSpot { get; }

        public StoreMap() : this(DefaultLayout) { }

        public StoreMap(string[] layout) : base(layout)
        {
            var shelfByDigit = new SortedDictionary<char, Vector2Int>();
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    char t = Tiles[x, y];
                    var p = new Vector2Int(x, y);
                    bool shelf = char.IsDigit(t) || (t >= 'a' && t <= 'c');
                    if (shelf && !shelfByDigit.ContainsKey(t)) shelfByDigit[t] = p;
                    else if (t == 'K') _counter.Add(p);
                    else if (t == 'L') _plants.Add(p);
                    else if (t == 'p') _doors.Add(p);
                }
            }
            _shelves.AddRange(shelfByDigit.Values);

            if (_counter.Count > 0)
            {
                Vector2Int mid = _counter[_counter.Count / 2];
                ClerkSpot = new Vector2Int(mid.x + 1, mid.y);
                for (int i = 0; i < QueueLength; i++) _queue.Add(new Vector2Int(mid.x - 1 - i, mid.y));
            }
        }

        /// <summary>맵 밖은 풀밭이고, 문 앞 길만 아래로 계속 이어집니다.</summary>
        public override char TileAt(int x, int y)
        {
            if (x < 0 || x >= Width || y >= Height) return 'g';
            if (y < 0) return Tiles[x, 0] == 'p' ? 'p' : 'g';
            return Tiles[x, y];
        }

        public override bool IsWalkable(Vector2Int p)
        {
            if (p.x < 0 || p.x >= Width || p.y < 0 || p.y >= Height) return false;
            char t = Tiles[p.x, p.y];
            return t == '.' || t == '+' || t == 'p';
        }

        /// <summary>진열대 앞(아래쪽)에서 물건을 고르는 두 칸.</summary>
        public Vector2Int[] ShopSpots(int shelfIndex)
        {
            Vector2Int s = _shelves[shelfIndex];
            return new[] { new Vector2Int(s.x, s.y - 1), new Vector2Int(s.x + 1, s.y - 1) };
        }

        /// <summary>계산대 뒤쪽, 주인이 서서 계산할 수 있는 구역인지.</summary>
        public bool IsClerkZone(Vector2 worldPos)
        {
            if (_counter.Count == 0) return false;
            int x = Mathf.FloorToInt(worldPos.x), y = Mathf.FloorToInt(worldPos.y);
            return x > _counter[0].x && x <= _counter[0].x + 3
                && y >= _counter[0].y && y <= _counter[_counter.Count - 1].y;
        }

        /// <summary>칸 안에서 캐릭터 발이 놓이는 월드 좌표.</summary>
        public static Vector2 FeetPos(Vector2Int tile) => new Vector2(tile.x + 0.5f, tile.y + 0.3f);

        public static Vector2Int TileOf(Vector2 worldPos) =>
            new Vector2Int(Mathf.FloorToInt(worldPos.x), Mathf.FloorToInt(worldPos.y));
    }
}
