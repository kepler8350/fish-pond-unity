using System.Collections.Generic;
using UnityEngine;

namespace FishPond
{
    /// <summary>
    /// 새 생물(쏠배감펭 지느러미·가오리·바다거북)과 산호초 장식 그래픽.
    /// 산호류는 흰색 명암으로 만들고 SpriteRenderer 색으로 물들입니다.
    /// </summary>
    public static partial class Art
    {
        // ---------------------------------------------------------------- 쏠배감펭 가슴지느러미
        /// <summary>피벗(왼쪽 가운데)에서 +X로 펼쳐지는 줄무늬 부채</summary>
        public static Sprite BrainCoral
        {
            get
            {
                return Make("brain", 128, 128, Center, (x, y) =>
                {
                    float dx = (x - 64f) / 62f, dy = (y - 64f) / 62f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy), th = Mathf.Atan2(dy, dx);
                    float edge = 0.9f + 0.06f * Mathf.Sin(th * 5f) + 0.03f * Mathf.Sin(th * 11f);
                    float a = S((edge - r) * 62f + 0.5f);
                    float maze = Mathf.Abs(Mathf.Sin(dx * 11f + Mathf.Sin(dy * 7f) * 2.2f) * Mathf.Cos(dy * 10f + Mathf.Sin(dx * 6f) * 2f));
                    float shade = Mathf.Lerp(0.72f, 1.05f, S(maze * 1.6f)) * Mathf.Lerp(1.08f, 0.75f, S(r / edge));
                    return new Color(shade, shade, shade, a);
                });
            }
        }

        /// <summary>사슴뿔산호: 가지가 뻗은 산호 (seed마다 모양이 다름)</summary>
        public static Sprite Staghorn(int seed)
        {
            return Make("stag" + seed, 128, 128, Center, MakeBranches(seed, 6, 0.075f));
        }

        /// <summary>부채산호: 그물처럼 얽힌 부채꼴</summary>
        public static Sprite SeaFan(int seed)
        {
            float tilt = (seed % 7) * 0.13f;
            return Make("fan" + seed, 128, 128, new Vector2(0.5f, 0.1f), (x, y) =>
            {
                float dx = (x - 64f) / 62f, dy = (y - 13f) / 112f;
                float r = Mathf.Sqrt(dx * dx + dy * dy), th = Mathf.Atan2(dx, dy) + tilt;
                if (dy < 0f || Mathf.Abs(th) > 1.1f) return new Color(1, 1, 1, 0);
                float outer = 0.92f + 0.06f * Mathf.Sin(th * 7f + seed);
                float inside = S((outer - r) * 50f);
                float radial = S(1f - Mathf.Abs(Mathf.Sin(th * 16f + r * 2f)) * 6f);
                float arcs = S(1f - Mathf.Abs(Mathf.Sin(r * 22f + th * 3f)) * 5f);
                float trunk = S(1f - Mathf.Abs(dx) * 25f) * S((0.35f - dy) * 10f);
                float a = inside * Mathf.Max(Mathf.Max(radial, arcs) * 0.9f, trunk);
                float shade = Mathf.Lerp(0.8f, 1.05f, r);
                return new Color(shade, shade, shade, a);
            });
        }

        /// <summary>말미잘: 가운데 입 둘레로 촉수가 퍼진 모양</summary>
        public static Sprite Anemone
        {
            get
            {
                return Make("anemone", 128, 128, Center, (x, y) =>
                {
                    float dx = (x - 64f) / 62f, dy = (y - 64f) / 62f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy), th = Mathf.Atan2(dy, dx);
                    float k = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(th * 22f + r * 4f), 2f);
                    float reach = 0.42f + 0.52f * k;
                    float a = S((reach - r) * 40f);
                    float tip = S((r - reach + 0.12f) * 12f);
                    float shade = Mathf.Lerp(0.78f, 1.1f, tip);
                    if (r < 0.2f) shade = Mathf.Lerp(0.55f, 0.8f, r / 0.2f);
                    return new Color(shade, shade, shade, a);
                });
            }
        }

        /// <summary>관해면: 구멍 뚫린 원통 여러 개</summary>
        public static Sprite TubeSponge(int seed)
        {
            var rnd = new System.Random(seed);
            int n = 4 + rnd.Next(3);
            var tubes = new List<Vector3>();
            for (int i = 0; i < n; i++)
            {
                float a = (float)rnd.NextDouble() * 6.28f, d = (float)rnd.NextDouble() * 0.45f;
                tubes.Add(new Vector3(Mathf.Cos(a) * d, Mathf.Sin(a) * d, 0.2f + (float)rnd.NextDouble() * 0.14f));
            }
            return Make("tube" + seed, 128, 128, Center, (x, y) =>
            {
                float dx = (x - 64f) / 62f, dy = (y - 64f) / 62f;
                float alpha = 0f, shade = 1f;
                foreach (var t in tubes)
                {
                    float d = Mathf.Sqrt((dx - t.x) * (dx - t.x) + (dy - t.y) * (dy - t.y));
                    float inside = S((t.z - d) * 62f + 0.5f);
                    if (inside <= 0f) continue;
                    alpha = Mathf.Max(alpha, inside);
                    float rel = d / t.z;
                    shade = rel < 0.55f ? Mathf.Lerp(0.3f, 0.45f, rel / 0.55f) : Mathf.Lerp(1.05f, 0.75f, (rel - 0.55f) / 0.45f);
                }
                return new Color(shade, shade, shade, alpha);
            });
        }

        /// <summary>판산호: 물결 모양으로 겹친 접시</summary>
        public static Sprite PlateCoral
        {
            get
            {
                return Make("plate", 128, 128, Center, (x, y) =>
                {
                    float dx = (x - 64f) / 62f, dy = (y - 64f) / 62f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy), th = Mathf.Atan2(dy, dx);
                    float edge = 0.88f + 0.08f * Mathf.Sin(th * 6f) + 0.04f * Mathf.Sin(th * 13f);
                    float a = S((edge - r) * 50f + 0.5f);
                    float rings = 0.5f + 0.5f * Mathf.Sin(r * 26f + Mathf.Sin(th * 6f) * 1.5f);
                    float shade = Mathf.Lerp(0.78f, 1.05f, rings) * Mathf.Lerp(0.9f, 1.08f, r);
                    return new Color(shade, shade, shade, a);
                });
            }
        }

        /// <summary>성게: 가시가 촘촘한 검은 공</summary>
        public static Sprite Urchin
        {
            get
            {
                return Make("urchin", 128, 128, Center, (x, y) =>
                {
                    float dx = (x - 64f) / 62f, dy = (y - 64f) / 62f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy), th = Mathf.Atan2(dy, dx);
                    float spike = Mathf.Pow(Mathf.Abs(Mathf.Cos(th * 18f)), 10f);
                    float reach = 0.42f + 0.55f * spike;
                    float a = S((reach - r) * 50f);
                    var c = r < 0.42f ? new Color(0.16f, 0.08f, 0.22f) : new Color(0.1f, 0.06f, 0.14f);
                    return WithA(c, a);
                });
            }
        }

        /// <summary>가리비 껍데기</summary>
        public static Sprite Scallop
        {
            get
            {
                return Make("scallop", 64, 64, new Vector2(0.5f, 0.15f), (x, y) =>
                {
                    float dx = (x - 32f) / 30f, dy = (y - 10f) / 50f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy), th = Mathf.Atan2(dx, dy);
                    float a = (dy >= 0f && Mathf.Abs(th) < 0.95f) ? S((0.95f + 0.03f * Mathf.Cos(th * 22f) - r) * 40f) : 0f;
                    if (dy < 0.12f && Mathf.Abs(dx) < 0.28f && dy > -0.1f) a = 1f; // 경첩
                    float rib = 0.85f + 0.2f * Mathf.Abs(Mathf.Cos(th * 11f));
                    return new Color(rib, rib, rib, a);
                });
            }
        }

        /// <summary>원 가장자리를 어둡게 덮는 고리 (원 밖 검정과 자연스럽게 이어짐)</summary>
        public static Sprite EdgeShade
        {
            get
            {
                return Make("edgeshade", 256, 256, Center, (x, y) =>
                {
                    float r = Vector2.Distance(new Vector2(x, y), new Vector2(128, 128)) / 127f;
                    float t = S((r - 0.93f) / 0.07f);
                    return new Color(0f, 0.02f, 0.04f, t * t * (3f - 2f * t) * (r > 1f ? 0f : 0.85f));
                });
            }
        }

        static PixelFn MakeBranches(int seed, int arms, float thick)
        {
            var rnd = new System.Random(seed);
            var segs = new List<Vector4>(); // x1,y1,x2,y2
            var widths = new List<float>();
            for (int i = 0; i < arms; i++)
            {
                float a = i / (float)arms * 6.28f + (float)rnd.NextDouble() * 0.6f;
                float len = 0.45f + (float)rnd.NextDouble() * 0.4f;
                var p0 = Vector2.zero;
                var p1 = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * len;
                segs.Add(new Vector4(p0.x, p0.y, p1.x, p1.y)); widths.Add(thick);
                for (int b = 0; b < 2; b++)
                {
                    float t = 0.4f + (float)rnd.NextDouble() * 0.4f;
                    var q0 = Vector2.Lerp(p0, p1, t);
                    float ba = a + (b == 0 ? 1f : -1f) * (0.5f + (float)rnd.NextDouble() * 0.5f);
                    var q1 = q0 + new Vector2(Mathf.Cos(ba), Mathf.Sin(ba)) * (len * (0.35f + (float)rnd.NextDouble() * 0.3f));
                    segs.Add(new Vector4(q0.x, q0.y, q1.x, q1.y)); widths.Add(thick * 0.75f);
                }
            }
            return (x, y) =>
            {
                var p = new Vector2((x - 64f) / 62f, (y - 64f) / 62f);
                float best = 9f, along = 0f;
                for (int i = 0; i < segs.Count; i++)
                {
                    var s = segs[i];
                    var a = new Vector2(s.x, s.y); var b = new Vector2(s.z, s.w);
                    var ab = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
                    float w = widths[i] * (1f - 0.45f * t);
                    float d = Vector2.Distance(p, a + ab * t) - w;
                    if (d < best) { best = d; along = t; }
                }
                float alpha = S(-best * 62f + 0.5f);
                float shade = Mathf.Lerp(0.82f, 1.12f, along);
                return new Color(shade, shade, shade, alpha);
            };
        }
    }
}
