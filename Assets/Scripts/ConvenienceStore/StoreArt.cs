using System.Collections.Generic;
using UnityEngine;

namespace ConvenienceStore
{
    /// <summary>캐릭터 한 명의 방향별 걷기 프레임. 왼쪽은 Side 를 뒤집어 씁니다.</summary>
    public class CharacterSprites
    {
        public Sprite[] Down, Up, Side;
    }

    /// <summary>
    /// 게임에 쓰는 모든 픽셀 아트를 만듭니다. 16픽셀 = 1타일 = 1유닛.
    /// 타일과 가구는 사각형으로, 캐릭터와 아이콘은 문자 도트 그림으로 그립니다.
    /// </summary>
    public class StoreArt
    {
        public const int Tile = 16;
        /// <summary>화면 비율이 넓어도 빈 곳이 보이지 않도록 맵 바깥에 더 그리는 풀밭 타일 수.</summary>
        public const int BackgroundMargin = 8;
        public const int ShelfSlots = 12;

        internal static readonly Color32 Outline = C(72, 44, 40);
        internal static readonly Color32 White = C(255, 252, 244);
        internal static readonly Color32 Grass = C(118, 186, 88);
        internal static readonly Color32 GrassLight = C(146, 206, 104);
        internal static readonly Color32 GrassDark = C(92, 162, 78);
        internal static readonly Color32 Sand = C(226, 196, 142);
        internal static readonly Color32 SandDark = C(204, 170, 118);
        internal static readonly Color32 FloorA = C(246, 232, 202);
        internal static readonly Color32 FloorB = C(238, 220, 186);
        internal static readonly Color32 FloorLine = C(220, 198, 162);
        internal static readonly Color32 Wood = C(140, 94, 62);
        internal static readonly Color32 WoodLight = C(178, 126, 84);
        internal static readonly Color32 WoodDark = C(106, 68, 48);
        internal static readonly Color32 WallA = C(190, 228, 208);
        internal static readonly Color32 WallB = C(172, 216, 196);
        internal static readonly Color32 ShelfWood = C(184, 130, 84);
        internal static readonly Color32 ShelfLight = C(216, 164, 108);
        internal static readonly Color32 ShelfBack = C(112, 74, 54);
        internal static readonly Color32 Red = C(232, 76, 88);
        internal static readonly Color32 Pink = C(255, 150, 170);
        internal static readonly Color32 Yellow = C(255, 214, 92);
        internal static readonly Color32 Mint = C(96, 200, 160);

        private static readonly Color32[] HairColors =
        {
            C(74, 52, 46), C(128, 82, 52), C(226, 178, 92), C(206, 98, 72), C(60, 60, 84), C(236, 156, 178),
        };
        private static readonly Color32[] SkinColors = { C(255, 220, 186), C(244, 198, 160), C(214, 160, 122) };
        private static readonly Color32[] ShirtColors =
        {
            C(240, 110, 110), C(110, 160, 236), C(250, 200, 90), C(160, 120, 220), C(250, 150, 190), C(120, 200, 200),
            C(245, 245, 235),
        };
        private static readonly Color32[] PantsColors = { C(70, 90, 140), C(100, 80, 70), C(60, 110, 100) };

        // ── 캐릭터 도트 (16×18). K 외곽선, H 머리, S 피부, E 눈, B 볼터치, C 옷, P 바지, F 신발, A 앞치마 ──
        private static readonly string[] HeadDown =
        {
            "....KKKKKKKK....",
            "...KHHHHHHHHK...",
            "..KHHHHHHHHHHK..",
            "..KHHHHHHHHHHK..",
            "..KHHSSSSSSHHK..",
            "..KHSSSSSSSSHK..",
            "..KSSESSSSESSK..",
            "..KSSESSSSESSK..",
            "..KSBSSSSSSBSK..",
            "...KSSSSSSSSK...",
            "....KKKKKKKK....",
        };
        private static readonly string[] HeadUp =
        {
            "....KKKKKKKK....",
            "...KHHHHHHHHK...",
            "..KHHHHHHHHHHK..",
            "..KHHHHHHHHHHK..",
            "..KHHHHHHHHHHK..",
            "..KHHHHHHHHHHK..",
            "..KHHHHHHHHHHK..",
            "..KHHHHHHHHHHK..",
            "..KHhhhhhhhhHK..",
            "...KHHHHHHHHK...",
            "....KKKKKKKK....",
        };
        private static readonly string[] HeadSide =
        {
            "....KKKKKKKK....",
            "...KHHHHHHHHK...",
            "..KHHHHHHHHHHK..",
            "..KHHHHHHHHHHK..",
            "..KHHHHHSSSSSK..",
            "..KHHHHSSSSSSK..",
            "..KHHHSSSSESSK..",
            "..KHHHSSSSESSK..",
            "..KHHSSSSSSBSK..",
            "...KHSSSSSSSK...",
            "....KKKKKKKK....",
        };
        private static readonly string[] BodyFront =
        {
            "...KCCCCCCCCK...",
            "..KSCCCCCCCCSK..",
            "..KSCCCCCCCCSK..",
            "...KccccccccK...",
        };
        private static readonly string[] BodyApron =
        {
            "...KCAAAAAACK...",
            "..KSCAAAAAACSK..",
            "..KSCAAWWAACSK..",
            "...KcAAAAAAcK...",
        };
        private static readonly string[] BodySide =
        {
            "....KCCCCCCK....",
            "....KCCSSCCK....",
            "....KCCSSCCK....",
            "....KccccccK....",
        };
        private static readonly string[] BodySideApron =
        {
            "....KCCCAAAK....",
            "....KCCSSAAK....",
            "....KCCSSAAK....",
            "....KcccAAAK....",
        };
        private static readonly string[][] LegsFront =
        {
            new[] { "...KPPPKKPPPK...", "...KFFK..KFFK...", "....KK....KK...." },
            new[] { "...KPPPKKPPPK...", "...KFFK..KKK....", "....KK.........." },
            new[] { "...KPPPKKPPPK...", "...KFFK..KFFK...", "....KK....KK...." },
            new[] { "...KPPPKKPPPK...", "....KKK..KFFK...", "..........KK...." },
        };
        private static readonly string[][] LegsSide =
        {
            new[] { "....KPPPPPPK....", "....KFFKKFFK....", ".....KK..KK....." },
            new[] { "....KPPPPPPK....", ".....KFFFFK.....", "......KKKK......" },
            new[] { "....KPPPPPPK....", "....KFFKKFFK....", ".....KK..KK....." },
            new[] { "....KPPPPPPK....", ".....KFFFFK.....", "......KKKK......" },
        };

        // ── 상품 아이콘 (6×7). ProductCatalog 순서 ──
        private static readonly string[][] ProductMaps =
        {
            new[] { "..WW..", ".WWWW.", ".WWWW.", "WWWWWW", "WNNNNW", "WNNNNW", ".NNNN." }, // 삼각김밥
            new[] { "WWWWWW", "RRRRRR", "RYYYYR", "RYYYYR", ".RRRR.", ".RRRR.", ".RRRR." }, // 컵라면
            new[] { ".GGGG.", ".YYYY.", "YYYYYY", "YWYYYY", "YWYYYY", "YYYYYY", ".YYYY." }, // 바나나우유
            new[] { "OoOoOo", "OOOOOO", "OYYYYO", "OYRRYO", "OYYYYO", "OOOOOO", "oOoOoO" }, // 과자
            new[] { ".PPPP.", "PPPPPP", "PWPPPP", "PPPPPP", ".PPPP.", "..TT..", "..TT.." }, // 아이스크림
            new[] { "KKKKKK", "KWWKRK", "KWWKRK", "KWWKKK", "KWWKGK", "KWWKYK", "KKKKKK" }, // 도시락
            new[] { "WWWWWW", "BBBBBB", "BbBBBB", "BbBBBB", "BBBBBB", ".BBBB.", "......" }, // 커피
            new[] { "..OO..", ".OOOO.", "OOOOOO", "OWOOWO", "OOOOOO", ".OOOO.", ".OO.OO" }, // 젤리
            new[] { "..RR..", ".RRRR.", ".RRRR.", ".RRRR.", ".RRRR.", "..TT..", "..TT.." }, // 핫바
            new[] { "..YY..", ".YYYY.", "YGGGGY", "YRRRRY", "YYYYYY", "......", "......" }, // 샌드위치
            new[] { "..LL..", ".LLLL.", ".CCCC.", ".CWCC.", ".CWCC.", ".CCCC.", ".CCCC." }, // 생수
            new[] { "DDDDDD", "DdDDdD", "DDDDDD", "DdDDdD", "DDDDDD", "DdDDdD", "DDDDDD" }, // 초콜릿
        };
        private static readonly Dictionary<char, Color32> ProductPalette = new Dictionary<char, Color32>
        {
            ['W'] = White, ['N'] = C(44, 62, 56), ['R'] = Red, ['Y'] = Yellow, ['G'] = C(110, 190, 110),
            ['O'] = C(250, 150, 70), ['o'] = C(226, 120, 52), ['P'] = Pink, ['T'] = C(214, 170, 120),
            ['K'] = C(60, 50, 60), ['B'] = C(118, 78, 50), ['b'] = C(160, 112, 72), ['L'] = C(70, 130, 220),
            ['C'] = C(176, 224, 250), ['D'] = C(100, 60, 40), ['d'] = C(144, 94, 62),
        };
        private static readonly string[] StarMap =
        {
            "...Y...", "..YYY..", "YYYYYYY", ".YYYYY.", "..YYY..", ".YY.YY.", ".......",
        };

        private static readonly string[] HeartMap =
        {
            ".RR.RR.", "RWRRRRR", "RRRRRRR", "RRRRRRR", ".RRRRR.", "..RRR..", "...R...",
        };
        private static readonly string[] AngerMap =
        {
            "..R.R..", "..R.R..", "RR...RR", ".......", "RR...RR", "..R.R..", "..R.R..",
        };
        private static readonly string[] DotsMap =
        {
            ".......", ".......", ".......", "K..K..K", "K..K..K", ".......", ".......",
        };
        private static readonly string[] ExclaimMap =
        {
            "..RRR..", "..RRR..", "..RRR..", "..RRR..", ".......", "..RRR..", "..RRR..",
        };
        private static readonly string[] CrownMap =
        {
            ".......", "Y..Y..Y", "YY.Y.YY", "YYYYYYY", "YWYYYRY", "OOOOOOO", ".......",
        };
        private static readonly string[] BoltMap =
        {
            "...OYO.", "..OYO..", ".OYYYO.", "..OYYO.", "..OYO..", ".OYO...", ".OO....",
        };
        private static readonly string[] CoinMap =
        {
            ".OOOOO.", "OYYYYYO", "OYWoYYO", "OYYoYYO", "OYYoYYO", "OYYYYYO", ".OOOOO.",
        };

        public Sprite Background { get; }
        public Sprite Shelf { get; }
        public Sprite Counter { get; }
        public Sprite Plant { get; }
        public Sprite Bubble { get; }
        public Sprite Heart { get; }
        public Sprite Anger { get; }
        public Sprite Dots { get; }
        public Sprite Exclaim { get; }
        public Sprite Crown { get; }
        public Sprite Bolt { get; }
        public Sprite Star { get; }
        public Sprite WhitePixel { get; }
        public Sprite[] ProductIcons { get; }
        public CharacterSprites Owner { get; }
        public CharacterSprites Clerk { get; }
        public CharacterSprites Stocker { get; }

        // HUD(IMGUI)용 텍스처
        public Texture2D PanelTexture { get; }
        public Texture2D HeartTexture { get; }
        public Texture2D HeartEmptyTexture { get; }
        public Texture2D CoinTexture { get; }

        public StoreArt(StoreMap map)
        {
            Background = BuildBackground(map);
            Shelf = BuildShelf();
            Counter = BuildCounter();
            Plant = BuildPlant();
            Bubble = BuildBubble();

            var emote = new Dictionary<char, Color32> { ['R'] = Red, ['W'] = White, ['K'] = Outline };
            Heart = Icon(HeartMap, emote).ToSprite(new Vector2(0.5f, 0.5f));
            Anger = Icon(AngerMap, emote).ToSprite(new Vector2(0.5f, 0.5f));
            Dots = Icon(DotsMap, emote).ToSprite(new Vector2(0.5f, 0.5f));
            Exclaim = Icon(ExclaimMap, emote).ToSprite(new Vector2(0.5f, 0.5f));

            var badge = new Dictionary<char, Color32>
            {
                ['Y'] = Yellow, ['O'] = C(196, 130, 40), ['W'] = White, ['R'] = Red,
            };
            Crown = Icon(CrownMap, badge).ToSprite(new Vector2(0.5f, 0.5f));
            Bolt = Icon(BoltMap, badge).ToSprite(new Vector2(0.5f, 0.5f));
            Star = Icon(StarMap, badge).ToSprite(new Vector2(0.5f, 0.5f));

            ProductIcons = new Sprite[ProductMaps.Length];
            for (int i = 0; i < ProductMaps.Length; i++)
            {
                ProductIcons[i] = Icon(ProductMaps[i], ProductPalette).ToSprite(Vector2.zero);
            }

            var white = new PixelCanvas(1, 1);
            white.Set(0, 0, new Color32(255, 255, 255, 255));
            WhitePixel = white.ToSprite(new Vector2(0.5f, 0.5f), 1);

            Owner = BuildCharacter(C(96, 62, 48), SkinColors[0], White, PantsColors[0], true);
            Clerk = BuildCharacter(C(226, 178, 92), SkinColors[1], White, PantsColors[1], true);
            Stocker = BuildCharacter(C(236, 156, 178), SkinColors[2], White, PantsColors[2], true);

            PanelTexture = BuildPanel().ToTexture(3);
            HeartTexture = Icon(HeartMap, emote).ToTexture();
            var empty = new Dictionary<char, Color32> { ['R'] = C(196, 172, 148), ['W'] = C(196, 172, 148) };
            HeartEmptyTexture = Icon(HeartMap, empty).ToTexture();
            var coin = new Dictionary<char, Color32>
            {
                ['O'] = C(196, 130, 40), ['Y'] = Yellow, ['o'] = C(226, 164, 56), ['W'] = White,
            };
            CoinTexture = Icon(CoinMap, coin).ToTexture();
        }

        /// <summary>y 가 작을수록(화면 아래쪽일수록) 앞에 그려지도록 하는 정렬 순서.</summary>
        public static int SortOrder(float worldY) => 5000 - Mathf.RoundToInt(worldY * Tile);

        /// <summary>진열대 스프라이트의 왼쪽 아래를 기준으로 한 n번째 상품 칸의 위치(유닛).</summary>
        public static Vector2 ShelfSlotOffset(int slot)
        {
            int col = slot % 4, row = slot / 4;
            return new Vector2((2 + col * 7) / (float)Tile, (28 - (2 + row * 8) - 7) / (float)Tile);
        }

        /// <summary>seed 에 따라 머리·피부·옷 색이 다른 손님을 만듭니다.</summary>
        public CharacterSprites RandomCustomer(int seed)
        {
            int h = Hash(seed, 17);
            return BuildCharacter(
                HairColors[h % HairColors.Length],
                SkinColors[h / 7 % SkinColors.Length],
                ShirtColors[h / 53 % ShirtColors.Length],
                PantsColors[h / 389 % PantsColors.Length],
                false);
        }

        private static CharacterSprites BuildCharacter(Color32 hair, Color32 skin, Color32 shirt, Color32 pants,
            bool apron)
        {
            var pal = new Dictionary<char, Color32>
            {
                ['K'] = Outline, ['H'] = hair, ['h'] = PixelCanvas.Mul(hair, 0.8f), ['S'] = skin,
                ['E'] = C(52, 36, 44), ['B'] = C(248, 160, 150), ['C'] = shirt, ['c'] = PixelCanvas.Mul(shirt, 0.82f),
                ['P'] = pants, ['F'] = C(96, 64, 54), ['A'] = Mint, ['W'] = White,
            };
            return new CharacterSprites
            {
                Down = Frames(HeadDown, apron ? BodyApron : BodyFront, LegsFront, pal),
                Up = Frames(HeadUp, BodyFront, LegsFront, pal),
                Side = Frames(HeadSide, apron ? BodySideApron : BodySide, LegsSide, pal),
            };
        }

        private static Sprite[] Frames(string[] head, string[] body, string[][] legs, Dictionary<char, Color32> pal)
        {
            var frames = new Sprite[legs.Length];
            for (int i = 0; i < legs.Length; i++)
            {
                var c = new PixelCanvas(16, 18);
                c.Blit(head, pal, 0, 0);
                c.Blit(body, pal, 0, head.Length);
                c.Blit(legs[i], pal, 0, head.Length + body.Length);
                frames[i] = c.ToSprite(new Vector2(0.5f, 0f));
            }
            return frames;
        }

        private static PixelCanvas Icon(string[] rows, Dictionary<char, Color32> pal)
        {
            var c = new PixelCanvas(rows[0].Length, rows.Length);
            c.Blit(rows, pal, 0, 0);
            return c;
        }

        // ── 배경 ──────────────────────────────────────────────────────────

        private static Sprite BuildBackground(StoreMap map)
        {
            const int m = BackgroundMargin;
            var c = new PixelCanvas((map.Width + m * 2) * Tile, (map.Height + m * 2) * Tile);
            for (int ty = -m; ty < map.Height + m; ty++)
            {
                for (int tx = -m; tx < map.Width + m; tx++)
                {
                    int px = (tx + m) * Tile, py = (map.Height - 1 - ty + m) * Tile;
                    switch (map.TileAt(tx, ty))
                    {
                        case 'g': DrawGrass(c, px, py); break;
                        case 'p': DrawPath(c, px, py); break;
                        case '#': DrawWall(c, px, py); break;
                        case 'w': DrawWallFace(c, px, py); break;
                        case '+': DrawDoor(c, px, py, map.TileAt(tx - 1, ty) != '+'); break;
                        default: DrawFloor(c, px, py, (tx + ty) % 2 == 0); break;
                    }
                    // 벽면 바로 아래 바닥에는 그림자를 드리운다.
                    if (map.TileAt(tx, ty) != 'w' && map.TileAt(tx, ty + 1) == 'w') c.Shade(px, py, Tile, 3, 0.86f);
                }
            }
            DrawWallDecor(c, map);
            return c.ToSprite(Vector2.zero);
        }

        internal static void DrawGrass(PixelCanvas c, int px, int py)
        {
            for (int y = 0; y < Tile; y++)
            {
                for (int x = 0; x < Tile; x++)
                {
                    int h = Hash(px + x, py + y);
                    c.Set(px + x, py + y, h % 19 == 0 ? GrassLight : h % 29 == 0 ? GrassDark : Grass);
                }
            }

            int t = Hash(px / Tile + 7, py / Tile + 3);
            if (t % 3 == 0)
            {
                int x = px + 2 + t / 3 % 10, y = py + 3 + t / 31 % 9;
                c.Set(x, y + 1, GrassDark);
                c.Set(x + 1, y, GrassDark);
                c.Set(x + 2, y + 1, GrassDark);
            }
            if (t % 11 == 0)
            {
                int x = px + 3 + t / 11 % 9, y = py + 3 + t / 97 % 9;
                Color32 petal = t / 5 % 3 == 0 ? White : t / 5 % 3 == 1 ? Pink : Yellow;
                c.Set(x, y - 1, petal);
                c.Set(x - 1, y, petal);
                c.Set(x + 1, y, petal);
                c.Set(x, y + 1, petal);
                c.Set(x, y, C(250, 170, 60));
            }
        }

        internal static void DrawPath(PixelCanvas c, int px, int py)
        {
            for (int y = 0; y < Tile; y++)
            {
                for (int x = 0; x < Tile; x++)
                {
                    int h = Hash(px + x, py + y);
                    c.Set(px + x, py + y, h % 13 == 0 ? SandDark : h % 23 == 0 ? White : Sand);
                }
            }
        }

        private static void DrawFloor(PixelCanvas c, int px, int py, bool alt)
        {
            c.Rect(px, py, Tile, Tile, alt ? FloorA : FloorB);
            c.Rect(px, py + Tile - 1, Tile, 1, FloorLine);
            c.Rect(px + Tile - 1, py, 1, Tile, FloorLine);
        }

        private static void DrawWall(PixelCanvas c, int px, int py)
        {
            c.Rect(px, py, Tile, Tile, Wood);
            c.Rect(px, py, 1, Tile, WoodDark);
            c.Rect(px + 8, py, 1, Tile, WoodDark);
            c.Rect(px, py, Tile, 1, WoodLight);
            c.Rect(px, py + Tile - 1, Tile, 1, Outline);
        }

        private static void DrawWallFace(PixelCanvas c, int px, int py)
        {
            for (int x = 0; x < Tile; x++) c.Rect(px + x, py, 1, Tile, x % 8 < 4 ? WallA : WallB);
            c.Rect(px, py, Tile, 3, WoodDark);
            c.Rect(px, py + 2, Tile, 1, Outline);
            c.Rect(px, py + 13, Tile, 3, Wood);
            c.Rect(px, py + 13, Tile, 1, WoodLight);
        }

        private static void DrawDoor(PixelCanvas c, int px, int py, bool leftHalf)
        {
            c.Rect(px, py, Tile, Tile, FloorB);
            // 두 칸에 걸친 현관 매트
            int x0 = leftHalf ? px + 3 : px, w = Tile - 3;
            c.Rect(x0, py + 4, w, 9, C(170, 66, 66));
            c.Rect(leftHalf ? x0 + 1 : x0, py + 5, w - 1, 7, C(222, 104, 96));
            for (int x = 2; x < w - 2; x += 3) c.Rect(x0 + x, py + 7, 1, 3, C(250, 220, 200));
            // 문틀
            c.Rect(leftHalf ? px : px + Tile - 1, py, 1, Tile, Outline);
        }

        private static void DrawWallDecor(PixelCanvas c, StoreMap map)
        {
            // 벽면 줄을 찾는다.
            for (int ty = 0; ty < map.Height; ty++)
            {
                int first = -1, last = -1;
                for (int tx = 0; tx < map.Width; tx++)
                {
                    if (map.TileAt(tx, ty) != 'w') continue;
                    if (first < 0) first = tx;
                    last = tx;
                }
                if (first < 0 || last - first < 13) continue;

                int py = (map.Height - 1 - ty + BackgroundMargin) * Tile;
                int Px(int tx) => (tx + BackgroundMargin) * Tile;
                int mid = (first + last) / 2;

                DrawWindow(c, Px(first + 2) + 2, py + 3);
                DrawWindow(c, Px(last - 3) + 2, py + 3);

                // 가운데 간판 "24H"
                int sx = Px(mid) + 2, sy = py + 3;
                c.Rect(sx, sy, 28, 10, Outline);
                c.Rect(sx + 1, sy + 1, 26, 8, C(236, 104, 112));
                c.Rect(sx + 1, sy + 1, 26, 1, C(250, 150, 150));
                string[] text =
                {
                    "WWW.W.W.W.W",
                    "..W.W.W.W.W",
                    "WWW.WWW.WWW",
                    "W.....W.W.W",
                    "WWW...W.W.W",
                };
                c.Blit(text, new Dictionary<char, Color32> { ['W'] = White }, sx + 9, sy + 3);

                // 시계
                int cx = Px(first + 5) + 4, cy = py + 4;
                c.Rect(cx + 1, cy, 6, 8, Outline);
                c.Rect(cx, cy + 1, 8, 6, Outline);
                c.Rect(cx + 1, cy + 1, 6, 6, White);
                c.Rect(cx + 4, cy + 2, 1, 3, Outline);
                c.Rect(cx + 4, cy + 4, 2, 1, Red);

                // 포스터
                int ox = Px(mid + 3) + 3, oy = py + 3;
                c.Rect(ox, oy, 10, 10, Outline);
                c.Rect(ox + 1, oy + 1, 8, 8, C(255, 236, 170));
                c.Blit(new[] { ".R.R.", "RRRRR", ".RRR.", "..R.." },
                    new Dictionary<char, Color32> { ['R'] = Red }, ox + 2, oy + 3);
            }
        }

        private static void DrawWindow(PixelCanvas c, int x, int y)
        {
            c.Rect(x, y, 28, 10, Outline);
            c.Rect(x + 1, y + 1, 26, 8, White);
            c.Rect(x + 2, y + 2, 24, 6, C(150, 208, 238));
            c.Rect(x + 13, y + 1, 2, 8, White);
            for (int i = 0; i < 4; i++)
            {
                c.Set(x + 4 + i, y + 6 - i, C(214, 240, 252));
                c.Set(x + 17 + i, y + 6 - i, C(214, 240, 252));
            }
        }

        // ── 가구 ──────────────────────────────────────────────────────────

        private static Sprite BuildShelf()
        {
            var c = new PixelCanvas(32, 28);
            c.Rect(0, 0, 32, 28, Outline);
            c.Rect(1, 1, 30, 26, ShelfWood);
            c.Rect(1, 1, 30, 1, ShelfLight);
            c.Rect(2, 2, 28, 24, ShelfBack);
            for (int row = 0; row < 3; row++) c.Rect(2, 2 + row * 8 + 7, 28, 1, ShelfLight);
            return c.ToSprite(Vector2.zero);
        }

        private static Sprite BuildCounter()
        {
            var c = new PixelCanvas(16, 56);
            c.Rect(0, 0, 16, 56, Outline);
            c.Rect(1, 1, 14, 42, C(222, 172, 116));
            c.Rect(1, 1, 14, 1, C(244, 204, 150));
            c.Rect(1, 44, 14, 11, Wood);
            c.Rect(1, 44, 14, 1, WoodLight);
            c.Rect(8, 45, 1, 10, WoodDark);
            // 계산기
            c.Rect(3, 15, 10, 13, C(52, 56, 72));
            c.Rect(4, 16, 8, 11, C(112, 120, 142));
            c.Rect(5, 17, 6, 3, C(150, 232, 170));
            for (int by = 0; by < 2; by++)
                for (int bx = 0; bx < 3; bx++)
                    c.Rect(5 + bx * 2, 22 + by * 2, 1, 1, White);
            // 사탕 통
            c.Rect(5, 33, 6, 6, Outline);
            c.Rect(6, 34, 4, 4, Pink);
            c.Set(7, 35, White);
            // 손님 쪽 작은 화분
            c.Rect(6, 5, 4, 4, C(196, 110, 72));
            c.Rect(5, 3, 6, 3, GrassDark);
            c.Set(7, 2, Grass);
            return c.ToSprite(Vector2.zero);
        }

        private static Sprite BuildPlant()
        {
            var c = new PixelCanvas(16, 24);
            Color32 leaf = C(84, 170, 96), leafLight = C(128, 204, 116), leafDark = C(52, 122, 82);
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    float dx = x - 7.5f, dy = y - 7.5f, d2 = dx * dx + dy * dy;
                    if (d2 > 52f) continue;
                    int h = Hash(x, y);
                    c.Set(x, y, d2 > 38f ? leafDark : h % 5 == 0 ? leafLight : h % 7 == 0 ? leafDark : leaf);
                }
            }
            c.Rect(3, 14, 10, 3, Outline);
            c.Rect(4, 15, 8, 1, C(232, 146, 98));
            c.Rect(4, 17, 8, 7, Outline);
            c.Rect(5, 17, 6, 6, C(196, 110, 72));
            c.Rect(5, 17, 1, 6, C(222, 134, 90));
            return c.ToSprite(Vector2.zero);
        }

        private static Sprite BuildBubble()
        {
            var c = new PixelCanvas(14, 14);
            for (int y = 0; y < 12; y++)
            {
                for (int x = 0; x < 14; x++)
                {
                    bool edgeX = x == 0 || x == 13, edgeY = y == 0 || y == 11;
                    if (edgeX && edgeY) continue;
                    c.Set(x, y, edgeX || edgeY ? Outline : White);
                }
            }
            c.Set(6, 11, White);
            c.Set(7, 11, White);
            c.Set(5, 12, Outline);
            c.Set(6, 12, White);
            c.Set(7, 12, White);
            c.Set(8, 12, Outline);
            c.Set(6, 13, Outline);
            c.Set(7, 13, Outline);
            return c.ToSprite(new Vector2(0.5f, 0f));
        }

        private static PixelCanvas BuildPanel()
        {
            var c = new PixelCanvas(12, 12);
            c.Rect(1, 0, 10, 12, Outline);
            c.Rect(0, 1, 12, 10, Outline);
            c.Rect(1, 1, 10, 10, WoodLight);
            c.Rect(2, 2, 8, 8, C(252, 238, 204));
            return c;
        }

        internal static Color32 C(int r, int g, int b) => new Color32((byte)r, (byte)g, (byte)b, 255);

        internal static int Hash(int x, int y)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263;
                h = (h ^ (h >> 13)) * 1274126177;
                return (h ^ (h >> 16)) & 0x7fffffff;
            }
        }
    }
}
