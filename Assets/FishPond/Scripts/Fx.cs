using System.Collections.Generic;
using UnityEngine;

namespace FishPond
{
    /// <summary>바닥에 투사되는 글자. 그림자를 깔아 밝은 바닥에서도 읽히게 합니다.</summary>
    public class FloorText
    {
        // TextMesh는 글꼴 크기 10px당 약 1 유닛으로 그려집니다.
        const float UnitsPerFontPx = 0.1f;
        const int FontSize = 96;

        public readonly GameObject go;
        readonly TextMesh main, shadow;
        Color color;
        float alpha = 1f;

        public FloorText(Transform parent, string name, int order, Color color, float height)
        {
            go = new GameObject(name);
            go.transform.SetParent(parent, false);
            shadow = MakeMesh(go.transform, "shadow", order);
            main = MakeMesh(go.transform, "text", order + 1);
            this.color = color;
            SetHeight(height);
            ApplyColor();
        }

        static TextMesh MakeMesh(Transform parent, string name, int order)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            var mr = g.AddComponent<MeshRenderer>();
            var tm = g.AddComponent<TextMesh>();
            tm.font = Art.Font;
            tm.fontSize = FontSize;
            tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            mr.sharedMaterial = Art.Font.material;
            mr.sortingOrder = order;
            return tm;
        }

        SpriteRenderer backdrop;

        /// <summary>글자 뒤에 둥근 어두운 판을 깝니다 (산호 위에서도 잘 읽히게).</summary>
        public void Backdrop(Vector2 size, Color c)
        {
            if (backdrop == null)
            {
                var g = new GameObject("backdrop");
                g.transform.SetParent(go.transform, false);
                g.transform.localPosition = new Vector3(0f, 0f, 0.002f);
                backdrop = g.AddComponent<SpriteRenderer>();
                backdrop.sprite = Art.SoftCircle;
                backdrop.sortingOrder = main.GetComponent<MeshRenderer>().sortingOrder - 2;
            }
            backdrop.transform.localScale = new Vector3(size.x, size.y, 1f);
            backdrop.color = c;
        }

        public string Text
        {
            get { return main.text; }
            set
            {
                if (main.text == value) return;
                main.text = value;
                shadow.text = value;
            }
        }

        public void SetHeight(float h)
        {
            float cs = h / (FontSize * UnitsPerFontPx);
            main.characterSize = cs;
            shadow.characterSize = cs;
            shadow.transform.localPosition = new Vector3(h * 0.05f, -h * 0.05f, 0.001f);
        }

        public Color Color { set { color = value; ApplyColor(); } }

        public float Alpha { set { alpha = value; ApplyColor(); } }

        public bool Visible { set { if (go.activeSelf != value) go.SetActive(value); } }

        void ApplyColor()
        {
            var c = color; c.a *= alpha;
            main.color = c;
            shadow.color = new Color(0.01f, 0.05f, 0.07f, 0.8f * c.a);
        }

        public void Place(Vector2 pos, float rotDeg, float z)
        {
            go.transform.localPosition = new Vector3(pos.x, pos.y, z);
            go.transform.localRotation = Quaternion.Euler(0, 0, rotDeg);
        }

        public void Destroy() { Object.Destroy(go); }
    }

    /// <summary>물결, 점수 팝업, 문어 먹물</summary>
    public class Fx
    {
        enum Kind { Ripple, Pop, Ink, Bubble }

        class Item
        {
            public Kind kind;
            public GameObject go;
            public SpriteRenderer sr;
            public FloorText text;
            public Vector2 pos, dir;
            public Color color;
            public float life, max, rate;
        }

        readonly Transform parent;
        readonly List<Item> items = new List<Item>();

        public Fx(Transform parent) { this.parent = parent; }

        SpriteRenderer MakeSprite(string name, Sprite sprite, int order, Vector2 pos, float z)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = new Vector3(pos.x, pos.y, z);
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        public void Ripple(Vector2 p, float maxRadius, Color c)
        {
            var sr = MakeSprite("ripple", Art.Ring(0.06f), 9, p, 0.5f);
            items.Add(new Item { kind = Kind.Ripple, go = sr.gameObject, sr = sr, pos = p, color = c, life = 1f, max = maxRadius, rate = 1.4f });
            sr.transform.localScale = Vector3.one * maxRadius * 0.3f;
        }

        public void Pop(Vector2 p, string text, Color c, float rotDeg, float height)
        {
            var t = new FloorText(parent, "pop", 85, c, height);
            t.Text = text;
            t.Place(p, rotDeg, -3f);
            float r = rotDeg * Mathf.Deg2Rad;
            items.Add(new Item { kind = Kind.Pop, go = t.go, text = t, pos = p, dir = new Vector2(-Mathf.Sin(r), Mathf.Cos(r)) * height * 1.2f, life = 1f, rate = 0.9f });
        }

        public void Ink(Vector2 p, float size)
        {
            for (int i = 0; i < 3; i++)
            {
                var off = Random.insideUnitCircle * size * 0.35f;
                var sr = MakeSprite("ink", Art.SoftCircle, 25, p + off, -0.2f);
                sr.color = new Color(0.06f, 0.03f, 0.1f, 0f);
                items.Add(new Item { kind = Kind.Ink, go = sr.gameObject, sr = sr, pos = p + off, life = 1f, max = size * Random.Range(0.8f, 1.25f), rate = 1f / Random.Range(3f, 4f) });
            }
        }

        /// <summary>바닥에서 올라오는 공기 방울: 커지면서 옅어짐</summary>
        public void Bubble(Vector2 p, float size)
        {
            var sr = MakeSprite("bubble", Art.Ring(0.18f), 34, p, -0.25f);
            sr.color = new Color(0.9f, 1f, 1f, 0f);
            items.Add(new Item { kind = Kind.Bubble, go = sr.gameObject, sr = sr, pos = p, dir = Random.insideUnitCircle * size * 2f, life = 1f, max = size, rate = 1f / Random.Range(1.2f, 2.2f) });
        }

        public void Update(float dt)
        {
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var it = items[i];
                it.life -= dt * it.rate;
                if (it.life <= 0f)
                {
                    Object.Destroy(it.go);
                    items.RemoveAt(i);
                    continue;
                }
                float age = 1f - it.life;
                switch (it.kind)
                {
                    case Kind.Ripple:
                    {
                        float r = Mathf.Lerp(it.max * 0.15f, it.max, 1f - Mathf.Pow(1f - age, 2.5f));
                        it.sr.transform.localScale = Vector3.one * r * 2f;
                        var c = it.color; c.a *= it.life * 0.5f; it.sr.color = c;
                        break;
                    }
                    case Kind.Pop:
                    {
                        var p = it.pos + it.dir * age;
                        it.go.transform.localPosition = new Vector3(p.x, p.y, -3f);
                        it.text.Alpha = Mathf.Min(1f, it.life * 2f);
                        break;
                    }
                    case Kind.Bubble:
                    {
                        var p = it.pos + it.dir * age;
                        it.go.transform.localPosition = new Vector3(p.x, p.y, -0.25f);
                        it.sr.transform.localScale = Vector3.one * it.max * (1f + age * 1.5f);
                        it.sr.color = new Color(0.9f, 1f, 1f, 0.55f * Mathf.Min(1f, age * 5f) * it.life);
                        break;
                    }
                    case Kind.Ink:
                    {
                        float grow = Mathf.Min(1f, age * 6f);
                        it.sr.transform.localScale = Vector3.one * it.max * (0.3f + 0.7f * grow) * (1f + age * 0.3f);
                        it.sr.color = new Color(0.06f, 0.03f, 0.1f, 0.9f * Mathf.Min(1f, it.life * 1.6f) * grow);
                        break;
                    }
                }
            }
        }

        public void Clear()
        {
            foreach (var it in items) Object.Destroy(it.go);
            items.Clear();
        }
    }

    /// <summary>효과음을 코드로 합성합니다 (오디오 파일 불필요).</summary>
    public class Sfx
    {
        struct Seg
        {
            public float f1, f2, dur, start, vol;
            public int wave; // 0 사인, 1 삼각, 2 사각
            public Seg(float f1, float f2, float dur, int wave, float vol, float start = 0f)
            { this.f1 = f1; this.f2 = f2; this.dur = dur; this.wave = wave; this.vol = vol; this.start = start; }
        }

        const int Rate = 44100;
        readonly AudioSource src;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        public float volume = 0.8f;

        public Sfx(GameObject host)
        {
            src = host.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;

            Add("catch", new Seg(520, 920, 0.12f, 0, 0.5f));
            Add("deposit", new Seg(660, 990, 0.14f, 0, 0.5f), new Seg(990, 1320, 0.2f, 0, 0.45f, 0.11f));
            Add("drop", new Seg(420, 140, 0.3f, 1, 0.5f));
            Add("tick", new Seg(880, 880, 0.07f, 2, 0.18f));
            Add("count", new Seg(660, 660, 0.1f, 2, 0.18f));
            Add("start", new Seg(523, 1046, 0.35f, 0, 0.55f));
            Add("end", new Seg(523, 784, 0.2f, 0, 0.5f), new Seg(784, 1046, 0.4f, 0, 0.5f, 0.18f));
            Add("puff", new Seg(260, 520, 0.22f, 1, 0.45f));
            Add("ink", new Seg(220, 70, 0.35f, 1, 0.5f));
            Add("sting", new Seg(900, 300, 0.25f, 2, 0.22f), new Seg(700, 250, 0.2f, 2, 0.18f, 0.08f));
            Add("jet", new Seg(300, 600, 0.12f, 1, 0.3f));
            Add("chomp", new Seg(190, 60, 0.16f, 2, 0.3f), new Seg(150, 50, 0.16f, 2, 0.26f, 0.13f));
        }

        void Add(string name, params Seg[] segs)
        {
            float total = 0f;
            foreach (var s in segs) total = Mathf.Max(total, s.start + s.dur);
            int n = Mathf.CeilToInt((total + 0.02f) * Rate);
            var data = new float[n];
            foreach (var s in segs)
            {
                int start = Mathf.FloorToInt(s.start * Rate), len = Mathf.FloorToInt(s.dur * Rate);
                double phase = 0;
                for (int i = 0; i < len && start + i < n; i++)
                {
                    float t = i / (float)Rate, k = t / s.dur;
                    float f = s.f1 * Mathf.Pow(s.f2 / s.f1, k);
                    phase += f / Rate;
                    float p = (float)(phase - System.Math.Floor(phase));
                    float w;
                    if (s.wave == 0) w = Mathf.Sin(p * Mathf.PI * 2f);
                    else if (s.wave == 1) w = 1f - 4f * Mathf.Abs(p - 0.5f);
                    else w = p < 0.5f ? 1f : -1f;
                    float env = Mathf.Min(1f, t / 0.005f) * (1f - k) * (1f - k);
                    data[start + i] += w * env * s.vol;
                }
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            clips[name] = clip;
        }

        public void Play(string name, float vol = 1f)
        {
            AudioClip c;
            if (clips.TryGetValue(name, out c)) src.PlayOneShot(c, volume * vol);
        }
    }
}
