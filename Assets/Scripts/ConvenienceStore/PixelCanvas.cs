using System.Collections.Generic;
using UnityEngine;

namespace ConvenienceStore
{
    /// <summary>
    /// 코드로 픽셀 아트를 그리는 작은 캔버스. (0, 0) 이 왼쪽 위입니다.
    /// 아트 에셋 없이 동작하도록 모든 그림을 런타임에 만듭니다.
    /// </summary>
    public class PixelCanvas
    {
        public readonly int Width;
        public readonly int Height;
        private readonly Color32[] _px;

        public PixelCanvas(int width, int height)
        {
            Width = width;
            Height = height;
            _px = new Color32[width * height];
        }

        public void Set(int x, int y, Color32 c)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height) return;
            _px[y * Width + x] = c;
        }

        public Color32 Get(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height) return default;
            return _px[y * Width + x];
        }

        public void Rect(int x, int y, int w, int h, Color32 c)
        {
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                    Set(x + i, y + j, c);
        }

        /// <summary>문자 한 글자가 한 픽셀인 그림을 찍습니다. 팔레트에 없는 문자는 투명으로 둡니다.</summary>
        public void Blit(string[] rows, IDictionary<char, Color32> palette, int ox, int oy)
        {
            for (int j = 0; j < rows.Length; j++)
            {
                string row = rows[j];
                for (int i = 0; i < row.Length; i++)
                {
                    if (palette.TryGetValue(row[i], out Color32 c)) Set(ox + i, oy + j, c);
                }
            }
        }

        /// <summary>색을 곱해 어둡게(또는 밝게) 만듭니다. 투명 픽셀은 그대로 둡니다.</summary>
        public void Shade(int x, int y, int w, int h, float factor)
        {
            for (int j = 0; j < h; j++)
            {
                for (int i = 0; i < w; i++)
                {
                    Color32 c = Get(x + i, y + j);
                    if (c.a == 0) continue;
                    Set(x + i, y + j, Mul(c, factor));
                }
            }
        }

        public static Color32 Mul(Color32 c, float factor) => new Color32(
            (byte)Mathf.Clamp(c.r * factor, 0f, 255f),
            (byte)Mathf.Clamp(c.g * factor, 0f, 255f),
            (byte)Mathf.Clamp(c.b * factor, 0f, 255f),
            c.a);

        /// <summary>scale 배로 키운 포인트 필터 텍스처를 만듭니다.</summary>
        public Texture2D ToTexture(int scale = 1)
        {
            int w = Width * scale, h = Height * scale;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                int srcRow = (Height - 1 - y / scale) * Width;
                for (int x = 0; x < w; x++) px[y * w + x] = _px[srcRow + x / scale];
            }
            tex.SetPixels32(px);
            tex.Apply(false);
            return tex;
        }

        /// <summary>pivot 은 0~1 비율입니다. (0, 0) 이 왼쪽 아래.</summary>
        public Sprite ToSprite(Vector2 pivot, int pixelsPerUnit = StoreArt.Tile)
        {
            Texture2D tex = ToTexture();
            return Sprite.Create(tex, new Rect(0, 0, Width, Height), pivot, pixelsPerUnit);
        }
    }
}
