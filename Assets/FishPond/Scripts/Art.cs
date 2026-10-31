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

        // ---------------------------------------------------------------- 물고기 (흰동가리·블루탱·복어)
        /// <summary>몸통 반폭 (u: 0=꼬리 쪽, 1=주둥이 / 반환: 텍스처 반높이 대비)</summary>
        public static float FishHalfWidth(SpeciesId id, float u)
        {
            if (u <= 0f || u >= 1f) return 0f;
            float s = Mathf.Max(0f, Mathf.Sin(Mathf.PI * (u * 0.94f + 0.06f)));
            switch (id)
            {
                case SpeciesId.Pufferfish:
                    return 0.95f * Mathf.Pow(s, 0.45f) * Mathf.Lerp(0.6f, 1f, S(u * 2f));
                case SpeciesId.YellowTang:
                    return 0.62f * Mathf.Pow(s, 0.6f) * Mathf.Lerp(0.5f, 1f, Mathf.SmoothStep(0f, 1f, u * 1.6f));
                case SpeciesId.Butterflyfish:
                case SpeciesId.Angelfish:
                    return 0.66f * Mathf.Pow(s, 0.55f) * Mathf.Lerp(0.5f, 1f, Mathf.SmoothStep(0f, 1f, u * 1.6f));
                case SpeciesId.Lionfish:
                    return 0.55f * Mathf.Pow(s, 0.7f) * Mathf.Lerp(0.55f, 1f, Mathf.SmoothStep(0f, 1f, u * 1.6f));
                case SpeciesId.BlueTang:
                    return 0.56f * Mathf.Pow(s, 0.7f) * Mathf.Lerp(0.5f, 1f, Mathf.SmoothStep(0f, 1f, u * 1.6f));
                default:
                    return 0.64f * Mathf.Pow(s, 0.7f) * Mathf.Lerp(0.55f, 1f, Mathf.SmoothStep(0f, 1f, u * 1.6f));
            }
        }

        public static Color FinColor(SpeciesId id)
        {
            switch (id)
            {
                case SpeciesId.Clownfish: return new Color(1f, 0.55f, 0.2f, 0.9f);
                case SpeciesId.BlueTang: return new Color(0.2f, 0.42f, 0.95f, 0.85f);
                case SpeciesId.YellowTang: return new Color(1f, 0.88f, 0.25f, 0.9f);
                case SpeciesId.Butterflyfish: return new Color(1f, 0.85f, 0.3f, 0.85f);
                case SpeciesId.Angelfish: return new Color(0.25f, 0.4f, 0.95f, 0.85f);
                case SpeciesId.Lionfish: return new Color(0.98f, 0.9f, 0.85f, 0.7f);
                case SpeciesId.Sardine: return new Color(0.8f, 0.85f, 0.9f, 0.7f);
                default: return new Color(0.95f, 0.85f, 0.55f, 0.85f);
            }
        }

        static Color FishPaint(SpeciesId id, float u, float v, float hw)
        {
            switch (id)
            {
                case SpeciesId.Clownfish:
                {
                    var orange = new Color(1f, 0.45f, 0.08f);
                    var white = new Color(0.98f, 0.97f, 0.95f);
                    var black = new Color(0.08f, 0.05f, 0.03f);
                    float[] centers = { 0.17f, 0.48f, 0.78f };
                    float[] widths = { 0.035f, 0.055f, 0.045f };
                    for (int i = 0; i < 3; i++)
                    {
                        float d = Mathf.Abs(u - centers[i] - v * v * 0.05f) - widths[i];
                        if (d < 0f) return white;
                        if (d < 0.013f) return black;
                    }
                    return orange;
                }
                case SpeciesId.BlueTang:
                {
                    var blue = new Color(0.12f, 0.35f, 0.9f);
                    var navy = new Color(0.03f, 0.06f, 0.2f);
                    var yellow = new Color(1f, 0.85f, 0.15f);
                    if (u < 0.09f) return yellow;
                    float rel = Mathf.Abs(v) / Mathf.Max(hw, 0.001f);
                    if (u > 0.18f && u < 0.86f && rel < 0.3f + 0.14f * Mathf.Sin(u * 9f)) return navy;
                    if (u > 0.3f && u < 0.62f && rel > 0.55f && rel < 0.8f) return navy;
                    return blue;
                }
                case SpeciesId.YellowTang:
                {
                    if (u > 0.07f && u < 0.12f && Mathf.Abs(v) < hw * 0.35f) return new Color(0.98f, 0.98f, 0.95f);
                    return new Color(1f, 0.84f, 0.08f);
                }
                case SpeciesId.Butterflyfish:
                {
                    if (Mathf.Abs(u - 0.8f) < 0.035f) return new Color(0.07f, 0.05f, 0.05f);
                    if (u < 0.42f) return new Color(1f, 0.8f, 0.15f);
                    float rel = Mathf.Abs(v) / Mathf.Max(hw, 0.001f);
                    if (u < 0.7f && rel > 0.45f && Mathf.Repeat(u * 14f, 1f) < 0.25f) return new Color(0.75f, 0.72f, 0.65f);
                    return new Color(0.98f, 0.97f, 0.9f);
                }
                case SpeciesId.Angelfish:
                {
                    if (Mathf.Abs(u - 0.78f) < 0.04f) return new Color(0.05f, 0.07f, 0.2f);
                    if (u > 0.83f) return new Color(0.2f, 0.35f, 0.85f);
                    return Mathf.Repeat(u * 9f + v * 1.2f, 1f) < 0.38f ? new Color(1f, 0.85f, 0.2f) : new Color(0.15f, 0.3f, 0.85f);
                }
                case SpeciesId.Lionfish:
                    return Mathf.Repeat(u * 9f - v * 0.6f, 1f) < 0.5f ? new Color(0.68f, 0.16f, 0.12f) : new Color(0.97f, 0.9f, 0.85f);
                case SpeciesId.Sardine:
                {
                    // 등은 짙은 청록, 옆은 은빛, 점 한 줄
                    float rel = Mathf.Abs(v) / Mathf.Max(hw, 0.001f);
                    var c = Color.Lerp(new Color(0.18f, 0.35f, 0.45f), new Color(0.85f, 0.88f, 0.92f), Mathf.SmoothStep(0f, 1f, (rel - 0.2f) * 1.6f));
                    if (rel > 0.45f && rel < 0.62f && u > 0.25f && u < 0.75f && Mathf.Repeat(u * 18f, 1f) < 0.35f) c = new Color(0.12f, 0.18f, 0.25f);
                    return c;
                }
                default: // Pufferfish
                {
                    var tan = new Color(0.9f, 0.78f, 0.45f);
                    var brown = new Color(0.45f, 0.3f, 0.15f);
                    if ((Mathf.Abs(u - 0.38f) < 0.05f || Mathf.Abs(u - 0.6f) < 0.05f) && Mathf.Abs(v) < hw * 0.7f)
                        return Color.Lerp(tan, brown, 0.6f);
                    float gx = u * 22f, gy = v * 5.5f;
                    float fx = gx - Mathf.Floor(gx) - 0.5f, fy = gy - Mathf.Floor(gy) - 0.5f;
                    if (Hash(Mathf.Floor(gx), Mathf.Floor(gy)) > 0.5f && fx * fx + fy * fy < 0.06f) return brown;
                    return tan;
                }
            }
        }

        /// <summary>몸통 스프라이트: 1 × 0.5 유닛, 주둥이가 +X</summary>
        public static Sprite FishBody(SpeciesId id)
        {
            return Make("fbody_" + id, 256, 128, Center, (x, y) =>
            {
                const float W = 256f, HH = 64f;
                float u = x / W, v = (y - HH) / HH;
                float hw = FishHalfWidth(id, u);
                float av = Mathf.Abs(v);
                float inside = (hw - av) * HH;
                float a = hw <= 0.001f ? 0f : S(inside + 0.5f);

                var c = FishPaint(id, u, v, hw);
                float rel = hw > 0.001f ? S(av / hw) : 1f;
                c = Shade(c, Mathf.Lerp(1.06f, 0.78f, rel * rel));
                var outline = id == SpeciesId.Clownfish ? new Color(0.1f, 0.05f, 0.02f) : Shade(c, 0.45f);
                c = Color.Lerp(c, outline, S(1f - inside / 2.5f) * 0.7f);

                // 눈 (위에서 본 모습이라 양쪽에 하나씩)
                float eu = id == SpeciesId.Pufferfish ? 0.8f : 0.82f;
                float ehw = FishHalfWidth(id, eu);
                float er = id == SpeciesId.Pufferfish ? 7.5f : 6f;
                for (int s = -1; s <= 1; s += 2)
                {
                    float ex = eu * W, ey = HH + s * ehw * HH * 0.62f;
                    float d = Mathf.Sqrt((x - ex) * (x - ex) + (y - ey) * (y - ey));
                    c = Color.Lerp(c, new Color(0.05f, 0.05f, 0.07f), S(er + 0.5f - d));
                    float hx = x - ex - er * 0.25f, hy = y - ey - er * 0.25f;
                    c = Color.Lerp(c, Color.white, S(er * 0.35f + 0.5f - Mathf.Sqrt(hx * hx + hy * hy)));
                }
                return WithA(c, a);
            });
        }

        /// <summary>꼬리 스프라이트: 1 × 1 유닛, 피벗 = 오른쪽 가운데(몸통에 붙는 곳), -X 방향으로 펼쳐짐</summary>
        public static Sprite FishTail(SpeciesId id)
        {
            return Make("ftail_" + id, 64, 64, new Vector2(1f, 0.5f), (x, y) =>
            {
                float dx = (64f - x) / 64f, dy = (y - 32f) / 32f;
                float spread = 0.2f + 0.8f * dx;
                float ny = Mathf.Abs(dy) / (spread * 0.92f);
                float limit;
                if (id == SpeciesId.BlueTang || id == SpeciesId.YellowTang || id == SpeciesId.Sardine) limit = 0.72f + 0.28f * ny * ny; // 가위 꼬리
                else if (id == SpeciesId.Pufferfish) limit = 0.75f - 0.2f * ny * ny; // 작고 둥근
                else limit = 0.95f - 0.22f * ny * ny;                                // 둥근 부채
                float a = S(Mathf.Min((1f - ny) * spread * 30f, (limit - dx) * 64f) + 0.5f);

                Color c;
                if (id == SpeciesId.Clownfish)
                {
                    c = new Color(1f, 0.47f, 0.1f);
                    if (dx > limit - 0.07f) c = new Color(0.97f, 0.96f, 0.94f);
                    else if (dx > limit - 0.2f) c = new Color(0.08f, 0.05f, 0.03f);
                }
                else if (id == SpeciesId.BlueTang)
                {
                    c = new Color(1f, 0.84f, 0.15f);
                    if (dx > limit - 0.06f || ny > 0.88f) c = new Color(0.04f, 0.07f, 0.22f);
                }
                else if (id == SpeciesId.YellowTang) c = new Color(1f, 0.84f, 0.1f);
                else if (id == SpeciesId.Butterflyfish)
                {
                    c = new Color(1f, 0.82f, 0.2f);
                    if (dx > limit - 0.1f) c = new Color(0.1f, 0.08f, 0.06f);
                }
                else if (id == SpeciesId.Angelfish) c = new Color(1f, 0.82f, 0.18f);
                else if (id == SpeciesId.Sardine) c = new Color(0.62f, 0.7f, 0.78f);
                else if (id == SpeciesId.Lionfish)
                {
                    c = Mathf.Repeat(dx * 6f, 1f) < 0.5f ? new Color(0.98f, 0.92f, 0.88f) : new Color(0.7f, 0.25f, 0.2f);
                    a *= 0.75f;
                }
                else c = new Color(0.82f, 0.7f, 0.42f);

                float ang = Mathf.Atan2(dy, dx + 0.15f);
                c = Shade(c, 0.9f + 0.1f * Mathf.Cos(ang * 40f));
                return WithA(c, a * 0.95f);
            });
        }

        /// <summary>복어 가시 (부풀 때 보임)</summary>
        public static Sprite PufferSpikes
        {
            get
            {
                return Make("spikes", 128, 128, Center, (x, y) =>
                {
                    float dx = (x - 64f) / 64f, dy = (y - 64f) / 64f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy), th = Mathf.Atan2(dy, dx);
                    float k = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(th * 22f), 6f);
                    float outer = 0.66f + 0.3f * k;
                    float a = S((outer - r) * 64f + 0.5f) * S((r - 0.6f) * 64f + 0.5f) * S(k * 3f);
                    return new Color(0.55f, 0.42f, 0.22f, a);
                });
            }
        }

        // ---------------------------------------------------------------- 오징어
        /// <summary>오징어 몸통: 1 × 0.5 유닛. +X = 외투막 끝(지느러미), -X = 머리(다리가 붙는 곳)</summary>
        public static Sprite SquidBody
        {
            get
            {
                return Make("squid", 256, 128, Center, (x, y) =>
                {
                    const float HH = 64f;
                    float u = x / 256f, v = (y - HH) / HH, av = Mathf.Abs(v);
                    float hw;
                    if (u < 0.2f) hw = 0.30f + 0.07f * Mathf.Sin(u / 0.2f * Mathf.PI);
                    else if (u < 0.26f) hw = 0.27f;
                    else { float t = (u - 0.26f) / 0.74f; hw = 0.36f * Mathf.Sqrt(Mathf.Max(0f, 1f - t * t * t * t)); }
                    if (u < 0.02f) hw *= u / 0.02f;

                    float fin = 0f;
                    if (u > 0.68f)
                    {
                        float t = (u - 0.68f) / 0.32f;
                        fin = 0.92f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, t * 1.05f)) * (1f - t * 0.3f);
                    }
                    bool inBody = av < hw;
                    float a = S((Mathf.Max(hw, fin) - av) * HH + 0.5f);

                    var body = new Color(0.98f, 0.86f, 0.84f);
                    float gx = u * 40f, gy = v * 20f;
                    float fx = gx - Mathf.Floor(gx) - 0.5f, fy = gy - Mathf.Floor(gy) - 0.5f;
                    float h = Hash(Mathf.Floor(gx), Mathf.Floor(gy));
                    if (h > 0.45f && fx * fx + fy * fy < 0.05f + 0.1f * h)
                        body = Color.Lerp(body, new Color(0.78f, 0.28f, 0.22f), 0.7f * h);
                    float rel = hw > 0.001f ? S(av / hw) : 1f;
                    body = Shade(body, Mathf.Lerp(1.04f, 0.84f, rel * rel));

                    Color c = body;
                    if (!inBody) { c = new Color(0.97f, 0.8f, 0.8f); a *= 0.7f; }

                    for (int s = -1; s <= 1; s += 2)
                    {
                        float ex = 0.11f * 256f, ey = HH + s * 0.34f * HH;
                        float d = Mathf.Sqrt((x - ex) * (x - ex) + (y - ey) * (y - ey));
                        c = Color.Lerp(c, new Color(0.9f, 0.95f, 0.95f), S(10.5f - d));
                        c = Color.Lerp(c, new Color(0.04f, 0.04f, 0.06f), S(7.5f - d));
                        if (d < 10.5f) a = Mathf.Max(a, S(10.5f - d));
                    }
                    return WithA(c, a);
                });
            }
        }

        // ---------------------------------------------------------------- 문어
        public static readonly Color OctopusColor = new Color(0.85f, 0.36f, 0.27f);

        /// <summary>문어 외투막(머리): 지름 1 유닛 타원용 원</summary>
        public static Sprite OctopusMantle
        {
            get
            {
                return Make("octo", 128, 128, Center, (x, y) =>
                {
                    float dx = (x - 64f) / 62f, dy = (y - 64f) / 62f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = S((1f - r) * 62f + 0.5f);
                    var c = OctopusColor;
                    float gx = x / 9f, gy = y / 9f;
                    float fx = gx - Mathf.Floor(gx) - 0.5f, fy = gy - Mathf.Floor(gy) - 0.5f;
                    float h = Hash(Mathf.Floor(gx), Mathf.Floor(gy));
                    if (h > 0.4f && fx * fx + fy * fy < 0.08f) c = Color.Lerp(c, new Color(1f, 0.72f, 0.6f), 0.55f);
                    c = Shade(c, Mathf.Lerp(1.12f, 0.72f, r * r));
                    return WithA(c, a);
                });
            }
        }

        /// <summary>문어 눈: 흰자 + 가로 눈동자</summary>
        public static Sprite OctopusEye
        {
            get
            {
                return Make("octoeye", 32, 32, Center, (x, y) =>
                {
                    float dx = (x - 16f) / 15f, dy = (y - 16f) / 15f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = S((1f - r) * 15f + 0.5f);
                    var c = new Color(0.98f, 0.92f, 0.7f);
                    if (Mathf.Abs(dy) < 0.22f && Mathf.Abs(dx) < 0.62f) c = new Color(0.05f, 0.04f, 0.04f);
                    return WithA(c, a);
                });
            }
        }

        // ---------------------------------------------------------------- 해파리
        public static Sprite JellyBell
        {
            get
            {
                return Make("jelly", 128, 128, Center, (x, y) =>
                {
                    float dx = (x - 64f) / 62f, dy = (y - 64f) / 62f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy), th = Mathf.Atan2(dy, dx);
                    float edge = 0.93f + 0.05f * Mathf.Cos(th * 8f);
                    float inside = S((edge - r) * 62f + 0.5f);
                    float rr = r / edge;
                    var c = Color.Lerp(new Color(0.95f, 0.9f, 1f), new Color(0.72f, 0.6f, 1f), rr);
                    float alpha = 0.42f + 0.38f * rr * rr * rr;
                    float gon = Mathf.Abs(r - 0.32f - 0.08f * Mathf.Cos(th * 4f));
                    if (gon < 0.06f) { c = Color.Lerp(c, new Color(1f, 0.55f, 0.8f), 0.8f); alpha = Mathf.Max(alpha, 0.75f); }
                    if (rr > 0.9f) { c = Color.Lerp(c, Color.white, 0.5f); alpha = Mathf.Max(alpha, 0.8f); }
                    return WithA(c, inside * alpha);
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
