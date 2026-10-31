using System.Collections.Generic;
using UnityEngine;

namespace FishPond
{
    /// <summary>
    /// 에셋 없이도 돌아가도록 모든 그래픽을 코드로 생성합니다.
    /// 스프라이트는 처음 요청될 때 한 번만 만들고 캐시합니다.
    /// 스프라이트 크기 규칙: 텍스처 가로 = 1 유닛 (PPU = 텍스처 가로 픽셀).
    /// </summary>
    public static partial class Art
    {
        delegate Color PixelFn(float x, float y);

        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        static Material unlit, water;
        static Font font;
        static Mesh quad;
        static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        // ---------------------------------------------------------------- 공용 리소스
        public static Material Unlit
        {
            get
            {
                if (unlit == null)
                {
                    var sh = Shader.Find("FishPond/Unlit");
                    if (sh == null) sh = Shader.Find("Sprites/Default");
                    unlit = new Material(sh) { name = "FishPond Unlit" };
                }
                return unlit;
            }
        }

        public static Material Water
        {
            get
            {
                if (water == null)
                {
                    var sh = Shader.Find("FishPond/Water");
                    if (sh == null) { Debug.LogWarning("[FishPond] Water 셰이더를 찾지 못했습니다."); sh = Shader.Find("Sprites/Default"); }
                    water = new Material(sh) { name = "FishPond Water" };
                }
                return water;
            }
        }

        public static Font Font
        {
            get
            {
                if (font == null)
                {
                    font = Font.CreateDynamicFontFromOSFont(new[]
                    {
                        "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "AppleGothic",
                        "Noto Sans CJK KR", "Noto Sans KR", "NanumGothic", "Arial"
                    }, 96);
                }
                return font;
            }
        }

        public static Mesh Quad
        {
            get
            {
                if (quad == null)
                {
                    quad = new Mesh { name = "FishPond Quad" };
                    quad.vertices = new[]
                    {
                        new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0),
                        new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0)
                    };
                    quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
                    quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                    quad.RecalculateBounds();
                }
                return quad;
            }
        }

        // ---------------------------------------------------------------- 생성 도우미
        static Sprite Make(string key, int w, int h, Vector2 pivot, PixelFn f)
        {
            Sprite s;
            if (cache.TryGetValue(key, out s) && s != null) return s;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "FishPond_" + key,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = f(x + 0.5f, y + 0.5f);
            tex.SetPixels32(px);
            tex.Apply(false, true);
            s = Sprite.Create(tex, new Rect(0, 0, w, h), pivot, w, 0, SpriteMeshType.FullRect);
            s.name = key;
            cache[key] = s;
            return s;
        }

        static float S(float x) { return Mathf.Clamp01(x); }

        static float Hash(float a, float b)
        {
            float h = Mathf.Sin(a * 127.1f + b * 311.7f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }

        static Color Shade(Color c, float k) { return new Color(c.r * k, c.g * k, c.b * k, c.a); }

        static Color WithA(Color c, float a) { c.a = a; return c; }

        // ---------------------------------------------------------------- 기본 도형
        /// <summary>지름 1 유닛 원</summary>
        public static Sprite Circle
        {
            get
            {
                return Make("circle", 128, 128, Center, (x, y) =>
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(64, 64));
                    return new Color(1, 1, 1, S(62.5f - d));
                });
            }
        }

        /// <summary>가장자리가 부드럽게 사라지는 원 (그림자·먹물·빛)</summary>
        public static Sprite SoftCircle
        {
            get
            {
                return Make("soft", 128, 128, Center, (x, y) =>
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(64, 64)) / 63f;
                    float t = S(1f - d);
                    return new Color(1, 1, 1, t * t * (3f - 2f * t));
                });
            }
        }

        /// <summary>두께 비율 thick(0~1)인 고리</summary>
        public static Sprite Ring(float thick)
        {
            int key = Mathf.RoundToInt(thick * 100);
            return Make("ring" + key, 256, 256, Center, (x, y) =>
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(128, 128));
                float outer = 126f, inner = outer * (1f - thick);
                return new Color(1, 1, 1, S(outer + 0.5f - d) * S(d - inner + 0.5f));
            });
        }

        public static Sprite Square
        {
            get { return Make("square", 8, 8, Center, (x, y) => Color.white); }
        }

        /// <summary>뜰채 그물망 (원 안의 마름모 격자)</summary>
        public static Sprite NetMesh
        {
            get
            {
                return Make("netmesh", 256, 256, Center, (x, y) =>
                {
                    float dx = x - 128, dy = y - 128;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float inside = S(122f - d);
                    const float sp = 21f;
                    float a1 = Mathf.Abs(Mathf.Repeat(dx + dy, sp) - sp * 0.5f);
                    float a2 = Mathf.Abs(Mathf.Repeat(dx - dy, sp) - sp * 0.5f);
                    // 격자선: 주기의 경계(a가 sp/2에 가까운 곳)
                    float l1 = S(1.6f - (sp * 0.5f - a1));
                    float l2 = S(1.6f - (sp * 0.5f - a2));
                    float line = Mathf.Max(l1, l2);
                    return new Color(0.94f, 0.98f, 0.97f, inside * (0.08f + 0.5f * line));
                });
            }
        }

        // ---------------------------------------------------------------- 장식
        public static Sprite Starfish
        {
            get
            {
                return Make("star", 128, 128, Center, (x, y) =>
                {
                    float dx = (x - 64f) / 62f, dy = (y - 64f) / 62f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy), th = Mathf.Atan2(dy, dx);
                    float k = 0.5f + 0.5f * Mathf.Cos(th * 5f);
                    float f = 0.3f + 0.66f * k * k;
                    float a = S((f - r) * 62f + 0.5f);
                    var c = Color.white;
                    float gx = x / 7f, gy = y / 7f;
                    float fx = gx - Mathf.Floor(gx) - 0.5f, fy = gy - Mathf.Floor(gy) - 0.5f;
                    if (fx * fx + fy * fy < 0.07f) c = new Color(1.15f, 1.1f, 1f);
                    c = Shade(c, Mathf.Lerp(1f, 0.75f, S(r / Mathf.Max(f, 0.01f))));
                    return WithA(c, a);
                });
            }
        }
    }
}
