using UnityEngine;

namespace FishPond
{
    public static partial class Art
    {
        /// <summary>상어 몸통(위에서 본 모습, 가슴지느러미 포함): 1 × 0.5 유닛, 주둥이가 +X</summary>
        public static Sprite SharkBody
        {
            get
            {
                return Make("shark", 256, 128, Center, (x, y) =>
                {
                    const float HH = 64f;
                    float u = x / 256f, v = (y - HH) / HH, av = Mathf.Abs(v);
                    float s = Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Clamp01(u * 0.92f + 0.08f)));
                    float hw = 0.3f * Mathf.Pow(s, 0.8f) * Mathf.Lerp(0.4f, 1f, Mathf.SmoothStep(0f, 1f, u * 1.8f));
                    if (u >= 0.999f) hw = 0f;
                    bool body = av < hw;
                    // 뒤로 젖혀진 가슴지느러미
                    float sweep = (av - 0.3f);
                    bool fin = av >= hw && av < 0.95f && u > 0.47f - sweep * 0.35f && u < 0.63f - sweep * 0.5f;
                    float edgeBody = (hw - av) * HH;
                    float a = body ? S(edgeBody + 0.5f) : (fin ? 1f : 0f);
                    if (!body && !fin) a = Mathf.Max(a, S(edgeBody + 0.5f));

                    var top = new Color(0.42f, 0.48f, 0.54f);
                    var c = top;
                    if (body)
                    {
                        float rel = hw > 0.001f ? av / hw : 1f;
                        c = Color.Lerp(top, new Color(0.78f, 0.8f, 0.82f), S((rel - 0.7f) * 3f)); // 옆으로 보이는 흰 배
                        c = Shade(c, Mathf.Lerp(0.92f, 1.05f, rel));
                        // 등지느러미 (가운데 짙은 삼각형)
                        if (u > 0.45f && u < 0.63f && av < 0.07f * (1f - (u - 0.45f) / 0.18f)) c = new Color(0.25f, 0.28f, 0.32f);
                        // 아가미 줄
                        if (u > 0.7f && u < 0.76f && rel > 0.65f && Mathf.Repeat(u * 80f, 1f) < 0.3f) c = Shade(c, 0.7f);
                    }
                    else if (fin)
                    {
                        float tip = Mathf.InverseLerp(0.3f, 0.95f, av);
                        c = Color.Lerp(top, new Color(0.28f, 0.32f, 0.37f), tip);
                    }
                    // 눈
                    float ehw = 0.3f * Mathf.Pow(Mathf.Sin(Mathf.PI * (0.86f * 0.92f + 0.08f)), 0.8f);
                    for (int k = -1; k <= 1; k += 2)
                    {
                        float ex = 0.86f * 256f, ey = HH + k * ehw * HH * 0.62f;
                        float d = Mathf.Sqrt((x - ex) * (x - ex) + (y - ey) * (y - ey));
                        c = Color.Lerp(c, new Color(0.03f, 0.03f, 0.04f), S(4.5f - d));
                    }
                    return WithA(c, a);
                });
            }
        }

        /// <summary>상어 꼬리: 초승달 모양, 피벗 = 오른쪽 가운데</summary>
        public static Sprite SharkTail
        {
            get
            {
                return Make("sharktail", 64, 64, new Vector2(1f, 0.5f), (x, y) =>
                {
                    float dx = (64f - x) / 64f, dy = (y - 32f) / 32f;
                    float spread = 0.18f + 0.82f * dx;
                    float ny = Mathf.Abs(dy) / (spread * 0.95f);
                    float limit = 0.5f + 0.5f * Mathf.Pow(ny, 1.4f);
                    float a = S(Mathf.Min((1f - ny) * spread * 30f, (limit - dx) * 64f) + 0.5f);
                    var c = Color.Lerp(new Color(0.42f, 0.48f, 0.54f), new Color(0.3f, 0.34f, 0.38f), dx);
                    return WithA(c, a);
                });
            }
        }

        /// <summary>원의 왼쪽 절반 (가운데 선 쪽은 부드럽게)</summary>
        public static Sprite HalfDiscLeft
        {
            get
            {
                return Make("halfleft", 256, 256, Center, (x, y) =>
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(128, 128));
                    float a = S(126.5f - d) * S((128f - x) / 10f);
                    return new Color(1, 1, 1, a);
                });
            }
        }
    }
}
