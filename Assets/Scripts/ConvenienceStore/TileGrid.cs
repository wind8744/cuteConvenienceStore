using System;
using System.Collections.Generic;
using UnityEngine;

namespace ConvenienceStore
{
    /// <summary>
    /// 문자 배치도로 만든 타일 맵의 공통 부분: 타일 조회, 월드 좌표 변환, 길찾기.
    /// 타일 (x, y) 는 Origin 을 기준으로 [x, x+1] × [y, y+1] 을 차지하고 y 는 위쪽이 큽니다.
    /// </summary>
    public abstract class TileGrid
    {
        private static readonly Vector2Int[] Dirs4 =
        {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
        };

        protected readonly char[,] Tiles;

        public int Width { get; }
        public int Height { get; }
        /// <summary>맵의 왼쪽 아래가 놓이는 월드 좌표.</summary>
        public Vector2 Origin { get; set; }

        /// <summary>layout 은 위쪽 줄부터 적습니다.</summary>
        protected TileGrid(string[] layout)
        {
            Height = layout.Length;
            Width = layout[0].Length;
            Tiles = new char[Width, Height];
            for (int y = 0; y < Height; y++)
            {
                string row = layout[Height - 1 - y];
                for (int x = 0; x < Width; x++) Tiles[x, y] = row[x];
            }
        }

        /// <summary>맵 밖은 풀밭으로 칩니다.</summary>
        public virtual char TileAt(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height) return 'g';
            return Tiles[x, y];
        }

        public abstract bool IsWalkable(Vector2Int p);

        public Vector2Int WorldToTile(Vector2 worldPos) =>
            new Vector2Int(Mathf.FloorToInt(worldPos.x - Origin.x), Mathf.FloorToInt(worldPos.y - Origin.y));

        /// <summary>칸 안에서 캐릭터 발이 놓이는 월드 좌표.</summary>
        public Vector2 TileToWorld(Vector2Int tile) => Origin + new Vector2(tile.x + 0.5f, tile.y + 0.3f);

        /// <summary>
        /// 너비 우선 탐색으로 최단 경로를 찾습니다. 출발 칸은 빼고 도착 칸까지 담아 돌려주며,
        /// 갈 수 없으면 null 입니다. passable 을 주면 IsWalkable 대신 그 조건으로 지나갈 칸을 고릅니다.
        /// </summary>
        public List<Vector2Int> FindPath(Vector2Int from, Vector2Int to, Func<Vector2Int, bool> passable = null)
        {
            if (passable == null) passable = IsWalkable;
            var path = new List<Vector2Int>();
            if (from == to) return path;
            if (!passable(to)) return null;

            var cameFrom = new Dictionary<Vector2Int, Vector2Int> { [from] = from };
            var open = new Queue<Vector2Int>();
            open.Enqueue(from);
            while (open.Count > 0)
            {
                Vector2Int cur = open.Dequeue();
                if (cur == to) break;
                foreach (Vector2Int d in Dirs4)
                {
                    Vector2Int next = cur + d;
                    if (cameFrom.ContainsKey(next) || !passable(next)) continue;
                    cameFrom[next] = cur;
                    open.Enqueue(next);
                }
            }
            if (!cameFrom.ContainsKey(to)) return null;

            for (Vector2Int p = to; p != from; p = cameFrom[p]) path.Add(p);
            path.Reverse();
            return path;
        }
    }
}
