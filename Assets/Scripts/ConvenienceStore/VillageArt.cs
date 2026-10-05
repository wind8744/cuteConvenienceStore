using UnityEngine;

namespace ConvenienceStore
{
    /// <summary>마을 맵의 픽셀 아트: 바닥(풀밭·길·연못), 집, 편의점 외관, 나무, 가로등.</summary>
    public class VillageArt
    {
        private const int Tile = StoreArt.Tile;

        private static readonly Color32 Outline = StoreArt.Outline;
        private static readonly Color32 White = StoreArt.White;
        private static readonly Color32 Water = C(98, 178, 226);
        private static readonly Color32 WaterLight = C(156, 214, 242);
        private static readonly Color32 WaterDark = C(62, 124, 186);
        private static readonly Color32 Glass = C(160, 214, 240);
        private static readonly Color32 GlassLight = C(214, 240, 252);
        private static readonly Color32 Stone = C(168, 160, 164);

        private static readonly Color32[] RoofColors =
        {
            C(222, 96, 92), C(96, 148, 220), C(104, 178, 120), C(160, 118, 208), C(240, 152, 78), C(84, 178, 184),
        };
        private static readonly Color32[] HouseWallColors =
        {
            C(252, 240, 214), C(250, 226, 206), C(238, 240, 220), C(246, 232, 240),
        };

        public Sprite Background { get; }
        /// <summary>집마다 지붕 색이 다릅니다.</summary>
        public Sprite[] Houses { get; }
        public Sprite Store { get; }
        /// <summary>초록 나무와 벚나무.</summary>
        public Sprite[] Trees { get; }
        public Sprite Lamp { get; }
        /// <summary>밤에 가로등 둘레에 번지는 불빛.</summary>
        public Sprite Glow { get; }

        public VillageArt(VillageMap map)
        {
            Background = BuildBackground(map);
            Houses = new Sprite[map.Houses.Count];
            for (int i = 0; i < Houses.Length; i++)
            {
                Houses[i] = BuildHouse(RoofColors[i % RoofColors.Length], HouseWallColors[i % HouseWallColors.Length]);
            }
            Store = BuildStore();
            Trees = new[] { BuildTree(false), BuildTree(true) };
            Lamp = BuildLamp();
            Glow = BuildGlow();
        }

        /// <summary>자리마다 정해진 나무 종류를 고릅니다. 다섯 그루에 한 그루쯤이 벚나무.</summary>
        public Sprite TreeAt(Vector2Int tile) => Trees[StoreArt.Hash(tile.x, tile.y) % 5 == 0 ? 1 : 0];

        // ── 바닥 ──────────────────────────────────────────────────────────

        private static Sprite BuildBackground(VillageMap map)
        {
            var c = new PixelCanvas(map.Width * Tile, map.Height * Tile);
            for (int ty = 0; ty < map.Height; ty++)
            {
                for (int tx = 0; tx < map.Width; tx++)
                {
                    int px = tx * Tile, py = (map.Height - 1 - ty) * Tile;
                    switch (map.TileAt(tx, ty))
                    {
                        case 'p':
                        case 'E':
                            StoreArt.DrawPath(c, px, py);
                            DrawPathEdges(c, map, tx, ty, px, py);
                            break;
                        case 'w':
                            DrawWater(c, map, tx, ty, px, py);
                            break;
                        case 'f':
                            StoreArt.DrawGrass(c, px, py);
                            DrawFlowers(c, px, py);
                            break;
                        default:
                            StoreArt.DrawGrass(c, px, py);
                            break;
                    }
                }
            }
            return c.ToSprite(Vector2.zero);
        }

        private static bool IsPath(VillageMap map, int tx, int ty)
        {
            char t = map.TileAt(tx, ty);
            // 맵 밖으로 이어지는 길은 없으므로, 건물 아래로 들어가는 입구만 길처럼 이어 준다.
            return t == 'p' || t == 'E' || t == 'S';
        }

        /// <summary>길과 풀밭의 경계를 들쭉날쭉하게 만들어 타일 모서리가 덜 딱딱해 보이게 합니다.</summary>
        private static void DrawPathEdges(PixelCanvas c, VillageMap map, int tx, int ty, int px, int py)
        {
            bool up = !IsPath(map, tx, ty + 1), down = !IsPath(map, tx, ty - 1);
            bool left = !IsPath(map, tx - 1, ty), right = !IsPath(map, tx + 1, ty);
            for (int i = 0; i < Tile; i++)
            {
                if (up) Ragged(c, px + i, py, 0, 1);
                if (down) Ragged(c, px + i, py + Tile - 1, 0, -1);
                if (left) Ragged(c, px, py + i, 1, 0);
                if (right) Ragged(c, px + Tile - 1, py + i, -1, 0);
            }
        }

        private static void Ragged(PixelCanvas c, int x, int y, int dx, int dy)
        {
            int h = StoreArt.Hash(x, y);
            c.Set(x, y, StoreArt.GrassDark);
            if (h % 2 == 0) c.Set(x + dx, y + dy, StoreArt.Grass);
            if (h % 5 == 0) c.Set(x + dx * 2, y + dy * 2, StoreArt.Grass);
        }

        private static void DrawWater(PixelCanvas c, VillageMap map, int tx, int ty, int px, int py)
        {
            c.Rect(px, py, Tile, Tile, Water);
            for (int y = 0; y < Tile; y++)
            {
                for (int x = 0; x < Tile; x++)
                {
                    // 짧은 가로 물결
                    if (StoreArt.Hash((px + x) / 4, py + y) % 23 == 0) c.Set(px + x, py + y, WaterLight);
                }
            }

            bool up = map.TileAt(tx, ty + 1) != 'w', down = map.TileAt(tx, ty - 1) != 'w';
            bool left = map.TileAt(tx - 1, ty) != 'w', right = map.TileAt(tx + 1, ty) != 'w';
            if (up)
            {
                c.Rect(px, py, Tile, 2, WaterDark);
                c.Rect(px, py + 2, Tile, 1, WaterLight);
            }
            if (down) c.Rect(px, py + Tile - 1, Tile, 1, WaterDark);
            if (left) c.Rect(px, py, 1, Tile, WaterDark);
            if (right) c.Rect(px + Tile - 1, py, 1, Tile, WaterDark);
        }

        private static void DrawFlowers(PixelCanvas c, int px, int py)
        {
            Color32[] petals = { White, StoreArt.Pink, StoreArt.Yellow, C(190, 150, 240) };
            for (int i = 0; i < 4; i++)
            {
                int h = StoreArt.Hash(px + i * 31, py + i * 17);
                int x = px + 2 + i % 2 * 7 + h % 4, y = py + 2 + i / 2 * 7 + h / 5 % 4;
                Color32 petal = petals[h / 11 % petals.Length];
                c.Set(x, y - 1, petal);
                c.Set(x - 1, y, petal);
                c.Set(x + 1, y, petal);
                c.Set(x, y + 1, petal);
                c.Set(x, y, C(250, 170, 60));
                c.Set(x, y + 2, StoreArt.GrassDark);
            }
        }

        // ── 건물 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 집 (64×80). 아래 48픽셀이 맵에서 차지하는 3칸이고, 지붕이 그 위로 2칸만큼 솟습니다.
        /// 문은 왼쪽에서 두 번째 칸에 있습니다.
        /// </summary>
        private static Sprite BuildHouse(Color32 roof, Color32 wall)
        {
            var c = new PixelCanvas(64, 80);
            Color32 roofDark = PixelCanvas.Mul(roof, 0.78f), roofLight = PixelCanvas.Mul(roof, 1.15f);
            Color32 door = C(150, 96, 60);

            // 벽
            c.Rect(3, 44, 58, 36, Outline);
            c.Rect(4, 45, 56, 34, wall);
            c.Rect(4, 75, 56, 4, Stone);
            c.Rect(4, 75, 56, 1, PixelCanvas.Mul(Stone, 0.85f));

            // 문
            c.Rect(17, 56, 14, 23, Outline);
            c.Rect(18, 57, 12, 22, door);
            c.Rect(18, 57, 1, 22, PixelCanvas.Mul(door, 1.2f));
            c.Rect(20, 59, 8, 6, Outline);
            c.Rect(21, 60, 6, 4, GlassLight);
            c.Set(27, 69, StoreArt.Yellow);
            c.Set(27, 70, StoreArt.Yellow);

            // 창문
            DrawWindow(c, 37, 55, 16, 13);
            DrawWindow(c, 6, 57, 9, 10);
            // 창가 화분
            c.Rect(37, 69, 16, 3, Outline);
            c.Rect(38, 69, 14, 2, C(176, 122, 78));
            for (int x = 39; x < 52; x += 3)
            {
                c.Set(x, 68, x % 2 == 0 ? StoreArt.Pink : StoreArt.Yellow);
                c.Set(x + 1, 68, StoreArt.GrassDark);
            }

            // 지붕: 위로 갈수록 좁아지고, 기와 줄무늬를 넣는다.
            for (int y = 0; y < 50; y++)
            {
                int inset = y < 18 ? (18 - y) * 10 / 18 : 0;
                for (int x = inset; x < 64 - inset; x++)
                {
                    Color32 col;
                    if (x == inset || x == 63 - inset || y == 0 || y == 49) col = Outline;
                    else if (y <= 2) col = roofLight;
                    else if (y % 6 == 5 || y == 48) col = roofDark;
                    else if ((x + y / 6 % 2 * 4) % 8 == 0) col = roofDark;
                    else col = roof;
                    c.Set(x, y, col);
                }
            }
            // 처마 그림자
            c.Shade(4, 50, 56, 3, 0.84f);

            // 굴뚝
            c.Rect(44, 4, 9, 11, Outline);
            c.Rect(45, 5, 7, 9, C(186, 122, 108));
            c.Rect(45, 5, 7, 2, C(214, 150, 132));
            return c.ToSprite(Vector2.zero);
        }

        private static void DrawWindow(PixelCanvas c, int x, int y, int w, int h)
        {
            c.Rect(x, y, w, h, Outline);
            c.Rect(x + 1, y + 1, w - 2, h - 2, White);
            c.Rect(x + 2, y + 2, w - 4, h - 4, Glass);
            c.Rect(x + w / 2, y + 1, 1, h - 2, White);
            c.Rect(x + 1, y + h / 2, w - 2, 1, White);
            c.Set(x + 3, y + 3, GlassLight);
            c.Set(x + 4, y + 3, GlassLight);
        }

        /// <summary>편의점 외관 (96×80). 아래 48픽셀이 맵에서 차지하는 3칸이고, 문은 가운데 두 칸입니다.</summary>
        private static Sprite BuildStore()
        {
            var c = new PixelCanvas(96, 80);
            Color32 stripe = C(236, 104, 112);

            // 벽
            c.Rect(2, 34, 92, 46, Outline);
            c.Rect(3, 35, 90, 44, C(252, 242, 218));
            c.Rect(3, 75, 90, 4, Stone);

            // 진열창: 안에 상품이 살짝 보인다.
            foreach (int wx in new[] { 6, 64 })
            {
                c.Rect(wx, 46, 26, 24, Outline);
                c.Rect(wx + 1, 47, 24, 22, White);
                c.Rect(wx + 2, 48, 22, 20, Glass);
                Color32[] goods = { StoreArt.Red, StoreArt.Yellow, StoreArt.Mint, StoreArt.Pink, C(250, 150, 70) };
                for (int i = 0; i < 5; i++)
                {
                    c.Rect(wx + 3 + i * 4, 62, 3, 6, goods[i]);
                    c.Rect(wx + 3 + i * 4, 53, 3, 4, goods[(i + 2) % 5]);
                }
                c.Rect(wx + 2, 57, 22, 1, White);
                for (int i = 0; i < 5; i++) c.Set(wx + 4 + i, 54 - i, GlassLight);
            }

            // 유리 자동문
            c.Rect(35, 44, 26, 35, Outline);
            c.Rect(36, 45, 24, 34, GlassLight);
            c.Rect(47, 45, 2, 34, Outline);
            c.Rect(36, 45, 24, 2, White);
            c.Rect(45, 60, 1, 6, Stone);
            c.Rect(50, 60, 1, 6, Stone);
            for (int i = 0; i < 6; i++)
            {
                c.Set(38 + i, 56 - i, White);
                c.Set(51 + i, 56 - i, White);
            }

            // 간판이 달린 지붕 앞면
            c.Rect(0, 2, 96, 24, Outline);
            c.Rect(1, 3, 94, 22, StoreArt.Mint);
            c.Rect(1, 3, 94, 2, PixelCanvas.Mul(StoreArt.Mint, 1.2f));
            c.Rect(1, 22, 94, 3, PixelCanvas.Mul(StoreArt.Mint, 0.8f));
            c.Rect(31, 6, 34, 15, Outline);
            c.Rect(32, 7, 32, 13, White);
            string[] text =
            {
                "WWW.W.W.W.W",
                "..W.W.W.W.W",
                "WWW.WWW.WWW",
                "W.....W.W.W",
                "WWW...W.W.W",
            };
            for (int j = 0; j < text.Length; j++)
                for (int i = 0; i < text[j].Length; i++)
                    if (text[j][i] == 'W') c.Rect(37 + i * 2, 9 + j * 2, 2, 2, stripe);

            // 줄무늬 차양
            for (int x = 0; x < 96; x++)
            {
                Color32 col = x / 6 % 2 == 0 ? stripe : White;
                c.Rect(x, 26, 1, 9, col);
                // 아래쪽은 물결 모양으로 끝낸다.
                if (x % 6 != 0 && x % 6 != 5) c.Rect(x, 35, 1, 2, col);
                c.Set(x, x % 6 != 0 && x % 6 != 5 ? 37 : 35, Outline);
            }
            c.Rect(0, 26, 96, 1, Outline);
            c.Rect(0, 26, 1, 10, Outline);
            c.Rect(95, 26, 1, 10, Outline);
            return c.ToSprite(Vector2.zero);
        }

        // ── 나무·가로등 ───────────────────────────────────────────────────

        /// <summary>나무 (32×40). 기준점은 밑동 가운데입니다.</summary>
        private static Sprite BuildTree(bool blossom)
        {
            var c = new PixelCanvas(32, 40);
            Color32 leaf = blossom ? C(255, 176, 196) : C(84, 166, 92);
            Color32 leafLight = blossom ? C(255, 214, 224) : C(128, 200, 112);
            Color32 leafDark = blossom ? C(226, 130, 160) : C(54, 122, 84);
            Color32 edge = blossom ? C(170, 84, 120) : C(38, 88, 70);

            // 바닥 그림자와 줄기
            for (int x = 8; x < 24; x++)
            {
                int half = x < 10 || x > 21 ? 1 : 2;
                c.Rect(x, 37 - half, 1, half * 2, StoreArt.GrassDark);
            }
            c.Rect(12, 24, 8, 14, Outline);
            c.Rect(13, 24, 6, 13, C(136, 92, 62));
            c.Rect(13, 24, 2, 13, C(168, 118, 80));

            // 수관
            for (int y = 0; y < 30; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    float dx = x - 15.5f, dy = (y - 14f) * 1.08f, d2 = dx * dx + dy * dy;
                    if (d2 > 14.5f * 14.5f) continue;
                    int h = StoreArt.Hash(x, y + (blossom ? 50 : 0));
                    Color32 col;
                    if (d2 > 13f * 13f) col = edge;
                    else if (dx + dy > 9f) col = h % 3 == 0 ? leaf : leafDark;
                    else if (dx + dy < -9f) col = h % 3 == 0 ? leaf : leafLight;
                    else col = h % 7 == 0 ? leafLight : h % 9 == 0 ? leafDark : leaf;
                    c.Set(x, y, col);
                }
            }
            return c.ToSprite(new Vector2(0.5f, 0f));
        }

        private static Sprite BuildLamp()
        {
            var c = new PixelCanvas(16, 34);
            Color32 metal = C(74, 78, 96), metalLight = C(118, 124, 146);
            c.Rect(5, 31, 6, 3, Outline);
            c.Rect(6, 31, 4, 2, metal);
            c.Rect(7, 11, 2, 20, metal);
            c.Rect(7, 11, 1, 20, metalLight);
            c.Rect(3, 2, 10, 10, Outline);
            c.Rect(4, 3, 8, 8, C(255, 232, 150));
            c.Rect(5, 4, 3, 3, White);
            c.Rect(2, 1, 12, 2, metal);
            c.Rect(6, 0, 4, 1, metal);
            return c.ToSprite(Vector2.zero);
        }

        /// <summary>가운데가 밝고 바깥으로 갈수록 계단처럼 옅어지는 둥근 불빛 (64×64).</summary>
        private static Sprite BuildGlow()
        {
            var c = new PixelCanvas(64, 64);
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    float dx = x - 31.5f, dy = y - 31.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / 32f;
                    if (d >= 1f) continue;
                    int band = Mathf.CeilToInt((1f - d) * 5f);
                    c.Set(x, y, new Color32(255, 226, 140, (byte)(band * 30)));
                }
            }
            return c.ToSprite(new Vector2(0.5f, 0.5f));
        }

        private static Color32 C(int r, int g, int b) => StoreArt.C(r, g, b);
    }
}
