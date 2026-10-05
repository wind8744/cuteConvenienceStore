using System.Collections.Generic;
using UnityEngine;

namespace ConvenienceStore
{
    /// <summary>
    /// 편의점 바깥의 마을 맵. 가운데에 편의점 건물이 있고 집 여섯 채가 길로 이어져 있습니다.
    /// 손님은 집에서 나와 길을 따라 편의점까지 걸어옵니다.
    /// </summary>
    public class VillageMap : TileGrid
    {
        /// <summary>
        /// 위쪽 줄부터 적습니다. g 풀밭, f 꽃밭, p 길, w 연못, t 나무, l 가로등,
        /// 1~6 집(가로 4칸 × 세로 3칸), S 편의점 건물(가로 6칸 × 세로 3칸), E 편의점 입구.
        /// 집의 문은 왼쪽에서 두 번째 칸 아래쪽에 있으므로 그 바로 아래 칸은 길이어야 합니다.
        /// </summary>
        public static readonly string[] DefaultLayout =
        {
            "tttttttttttttttttttttttttttttttttttttttt",
            "tggggggggggggggggggggggggggggggggggggggt",
            "tgggggggggggggggggggggtggggggggtgggggtgt",
            "tgtgggggtggggggggtgggggggggggggggggggggt",
            "tggggggtgg2222gggggggggggg3333gggggggtgt",
            "tggggggggg2222ggfgggtgggfg3333gtgggggggt",
            "tgg1111ggg2222gfgggggggtgg3333ggg4444ggt",
            "tgg1111ggggpggggtggggggggggpggggg4444ggt",
            "tgg1111gffgpgggggggggggggggpgggfg4444ggt",
            "tgggpggggggpggtggggggggggtgpggggfgpggggt",
            "tgggpggggggpgggggSSSSSSggggpggggggpggggt",
            "tgtgpggtgtgpfggffSSSSSSffggpfgtgggpggtgt",
            "tgggpggggggpgggffSSSSSSffggpggggggpggggt",
            "tgglpggggggpggggglgEEglggggpggggggplgggt",
            "tppppppppppppppppppppppppppppppppppppppt",
            "tppppppppppppppppppppppppppppppppppppppt",
            "tggggggggggpgggggggppgggggggpggggggggggt",
            "tggggtgggggpggglgggppggglgggpggggtgggtgt",
            "tgtggggggggpggggppppppppggggpgggggftgggt",
            "tggggfggggfpgggfggwwwwggfgtgpggggfgggggt",
            "tggggg5555gpgtgfgwwwwwwgfgggpg6666gggggt",
            "tggggg5555gpgggfgwwwwwwgfgggpg6666gggggt",
            "tggggg5555gpgggfggwwwwggfgggpg6666gggggt",
            "tggggggpppppggggggggggggggggppppgggggggt",
            "tggtggggggggggggggggggggggggggggggggtggt",
            "tgtgggggggggggtggggtgggggtgggggggggggggt",
            "tggggggggggggggggggggggggggggggggggggggt",
            "tttttttttttttttttttttttttttttttttttttttt",
        };

        public const int HouseWidth = 4;
        public const int HouseHeight = 3;
        public const int StoreWidth = 6;
        public const int StoreHeight = 3;

        private readonly List<Vector2Int> _houses = new List<Vector2Int>();
        private readonly List<Vector2Int> _trees = new List<Vector2Int>();
        private readonly List<Vector2Int> _lamps = new List<Vector2Int>();
        private readonly List<Vector2Int> _entrances = new List<Vector2Int>();

        /// <summary>집이 차지한 칸들의 왼쪽 아래. 번호 순서.</summary>
        public IReadOnlyList<Vector2Int> Houses => _houses;
        public IReadOnlyList<Vector2Int> Trees => _trees;
        public IReadOnlyList<Vector2Int> Lamps => _lamps;
        /// <summary>편의점 문 앞 칸. 왼쪽부터.</summary>
        public IReadOnlyList<Vector2Int> Entrances => _entrances;
        /// <summary>편의점 건물이 차지한 칸들의 왼쪽 아래.</summary>
        public Vector2Int Store { get; }

        public VillageMap() : this(DefaultLayout) { }

        public VillageMap(string[] layout) : base(layout)
        {
            var houseByDigit = new SortedDictionary<char, Vector2Int>();
            bool storeFound = false;
            // 아래 줄부터, 왼쪽부터 훑으므로 처음 만나는 칸이 건물의 왼쪽 아래다.
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    char t = Tiles[x, y];
                    var p = new Vector2Int(x, y);
                    if (char.IsDigit(t))
                    {
                        if (!houseByDigit.ContainsKey(t)) houseByDigit[t] = p;
                    }
                    else if (t == 't') _trees.Add(p);
                    else if (t == 'l') _lamps.Add(p);
                    else if (t == 'E') _entrances.Add(p);
                    else if (t == 'S' && !storeFound)
                    {
                        Store = p;
                        storeFound = true;
                    }
                }
            }
            _houses.AddRange(houseByDigit.Values);
        }

        public override bool IsWalkable(Vector2Int p)
        {
            if (p.x < 0 || p.x >= Width || p.y < 0 || p.y >= Height) return false;
            char t = Tiles[p.x, p.y];
            return t == 'g' || t == 'f' || t == 'p' || t == 'E';
        }

        /// <summary>마을 사람들이 다니는 길(편의점 입구 포함)인지.</summary>
        public bool IsRoad(Vector2Int p)
        {
            char t = TileAt(p.x, p.y);
            return t == 'p' || t == 'E';
        }

        /// <summary>집 문 바로 앞 칸.</summary>
        public Vector2Int HouseDoor(int houseIndex)
        {
            Vector2Int h = _houses[houseIndex];
            return new Vector2Int(h.x + 1, h.y - 1);
        }
    }
}
