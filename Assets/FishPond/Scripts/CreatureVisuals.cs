using System.Collections.Generic;
using UnityEngine;

namespace FishPond
{
    /// <summary>
    /// 생물의 겉모습. 모든 비주얼은 "머리가 +X"를 향하도록 만들고,
    /// 길이 단위는 연못 반지름(R) 기준입니다. (생물 루트가 R배로 스케일됨)
    /// </summary>
    public abstract class CreatureVisual
    {
        protected class Part
        {
            public Renderer r;
            public SpriteRenderer sr;
            public LineRenderer lr;
            public int rel;
            public Color c0, c1;
            public float width;
        }

        public readonly Transform root;
        protected readonly List<Part> parts = new List<Part>();
        protected Transform shadow;
        protected readonly float L;
        protected float phase = Random.value * 10f;
        protected float alpha = 1f;

        protected CreatureVisual(Transform parent, float length)
        {
            root = new GameObject("visual").transform;
            root.SetParent(parent, false);
            L = length;
        }

        public static CreatureVisual Create(SpeciesDef def, Transform parent)
        {
            if (def.prefabOverride != null) return new PrefabVisual(def, parent, def.length);
            switch (def.id)
            {
                case SpeciesId.Squid: return new SquidVisual(parent, def.length);
                case SpeciesId.Octopus: return new OctopusVisual(parent, def.length);
                case SpeciesId.Jellyfish: return new JellyVisual(parent, def.length);
                case SpeciesId.Stingray: return new StingrayVisual(parent, def.length);
                case SpeciesId.SeaTurtle: return new TurtleVisual(parent, def.length);
                default: return new FishVisual(def.id, parent, def.length);
            }
        }

        // ------------------------------------------------------------ 구성 도우미
        protected Transform Node(Transform parent, string name, Vector2 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        protected Part SpritePart(Transform parent, string name, Sprite sprite, Color c, int rel, Vector2 pos, Vector2 scale, float rotDeg = 0f)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
            g.transform.localRotation = Quaternion.Euler(0, 0, rotDeg);
            g.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = c;
            var p = new Part { r = sr, sr = sr, rel = rel, c0 = c };
            parts.Add(p);
            return p;
        }

        protected LineRenderer LinePart(Transform parent, string name, int count, Color c0, Color c1, float width, AnimationCurve curve, int rel)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            var lr = g.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = count;
            lr.sharedMaterial = Art.Unlit;
            lr.widthCurve = curve;
            lr.widthMultiplier = width;
            lr.numCapVertices = 3;
            lr.numCornerVertices = 2;
            lr.alignment = LineAlignment.TransformZ;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.startColor = c0;
            lr.endColor = c1;
            parts.Add(new Part { r = lr, lr = lr, rel = rel, c0 = c0, c1 = c1, width = width });
            return lr;
        }

        protected static Vector2 Dir(float a) { return new Vector2(Mathf.Cos(a), Mathf.Sin(a)); }

        protected static AnimationCurve Taper(float end)
        {
            return new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, end));
        }

        // ------------------------------------------------------------ 공통 조작
        public virtual void SetSorting(int baseOrder)
        {
            foreach (var p in parts) p.r.sortingOrder = baseOrder + p.rel;
        }

        public virtual void SetAlpha(float a)
        {
            alpha = a;
            foreach (var p in parts)
            {
                if (p.sr != null) { var c = p.c0; c.a *= a; p.sr.color = c; }
                else if (p.lr != null)
                {
                    var c0 = p.c0; c0.a *= a; p.lr.startColor = c0;
                    var c1 = p.c1; c1.a *= a; p.lr.endColor = c1;
                }
            }
        }

        /// <summary>그림자는 회전과 상관없이 항상 같은 방향(오른쪽 아래)으로 떨어집니다.</summary>
        public void SetShadow(Vector2 stageOffset, float headingRad, float worldScale)
        {
            if (shadow == null || worldScale <= 0f) return;
            float c = Mathf.Cos(-headingRad), s = Mathf.Sin(-headingRad);
            var o = new Vector2(stageOffset.x * c - stageOffset.y * s, stageOffset.x * s + stageOffset.y * c) / worldScale;
            shadow.localPosition = new Vector3(o.x, o.y, 0.01f);
        }

        public void SetShadowVisible(bool on)
        {
            if (shadow != null && shadow.gameObject.activeSelf != on) shadow.gameObject.SetActive(on);
        }

        /// <summary>LineRenderer 두께는 트랜스폼 스케일을 따르지 않아 직접 맞춥니다.</summary>
        protected void UpdateWidths(float worldScale)
        {
            foreach (var p in parts)
                if (p.lr != null) p.lr.widthMultiplier = p.width * worldScale;
        }

        public abstract void Animate(float dt, float swim, float worldScale);
        public virtual void SetPuff(float k) { }
        public virtual void SetSquish(float k) { }
        public virtual void SetDash(float k) { }

        public void Destroy() { Object.Destroy(root.gameObject); }
    }

    // ================================================================ 흰동가리 · 블루탱 · 복어
    public class FishVisual : CreatureVisual
    {
        readonly Transform tail, finL, finR, body, fanL, fanR;
        readonly Part spikes;
        readonly Vector2 finBase;

        static Vector2 TailSize(SpeciesId id)
        {
            switch (id)
            {
                case SpeciesId.BlueTang: case SpeciesId.YellowTang: return new Vector2(0.3f, 0.42f);
                case SpeciesId.Pufferfish: return new Vector2(0.2f, 0.26f);
                case SpeciesId.Lionfish: return new Vector2(0.28f, 0.34f);
                case SpeciesId.Angelfish: return new Vector2(0.28f, 0.4f);
                case SpeciesId.Butterflyfish: return new Vector2(0.26f, 0.34f);
                default: return new Vector2(0.32f, 0.36f);
            }
        }

        public FishVisual(SpeciesId id, Transform parent, float length) : base(parent, length)
        {
            var bodySprite = Art.FishBody(id);
            shadow = SpritePart(root, "shadow", bodySprite, new Color(0f, 0.01f, 0.02f, 0.28f), -5, Vector2.zero, new Vector2(L, L)).r.transform;

            tail = Node(root, "tailPivot", new Vector2(-L * 0.47f, 0f));
            SpritePart(tail, "tail", Art.FishTail(id), Color.white, 1, Vector2.zero, TailSize(id) * L);

            float hw = Art.FishHalfWidth(id, 0.66f) * 0.25f * L * 0.85f;
            finBase = new Vector2(L * 0.16f, hw);
            finL = Node(root, "finL", finBase);
            finR = Node(root, "finR", new Vector2(finBase.x, -finBase.y));
            var fc = Art.FinColor(id);
            SpritePart(finL, "fin", Art.Circle, fc, 1, new Vector2(-L * 0.05f, L * 0.03f), new Vector2(L * 0.16f, L * 0.07f), 25f);
            SpritePart(finR, "fin", Art.Circle, fc, 1, new Vector2(-L * 0.05f, -L * 0.03f), new Vector2(L * 0.16f, L * 0.07f), -25f);

            // 쏠배감펭: 줄무늬 부채 같은 가슴지느러미
            if (id == SpeciesId.Lionfish)
            {
                fanL = Node(root, "fanL", new Vector2(L * 0.12f, finBase.y * 0.8f));
                fanR = Node(root, "fanR", new Vector2(L * 0.12f, -finBase.y * 0.8f));
                SpritePart(fanL, "fan", Art.LionFan, Color.white, 0, Vector2.zero, new Vector2(L * 0.55f, L * 0.55f), 125f);
                SpritePart(fanR, "fan", Art.LionFan, Color.white, 0, Vector2.zero, new Vector2(L * 0.55f, L * 0.55f), -125f);
            }

            body = SpritePart(root, "body", bodySprite, Color.white, 2, Vector2.zero, new Vector2(L, L)).r.transform;

            if (id == SpeciesId.Pufferfish)
                spikes = SpritePart(root, "spikes", Art.PufferSpikes, new Color(1, 1, 1, 0), 3, Vector2.zero, new Vector2(L * 1.05f, L * 0.62f));
        }

        public override void Animate(float dt, float swim, float worldScale)
        {
            phase += dt * (6f + swim * 14f);
            tail.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(phase) * (20f + swim * 12f));
            body.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(phase + Mathf.PI) * 3f);
            float f = Mathf.Sin(phase * 0.7f) * 20f;
            finL.localRotation = Quaternion.Euler(0, 0, f);
            finR.localRotation = Quaternion.Euler(0, 0, -f);
            if (fanL != null)
            {
                float g = Mathf.Sin(phase * 0.35f) * 12f;
                fanL.localRotation = Quaternion.Euler(0, 0, g);
                fanR.localRotation = Quaternion.Euler(0, 0, -g);
            }
        }

        public override void SetPuff(float k)
        {
            if (spikes == null) return;
            float sx = 1f + 0.2f * k, sy = 1f + 0.75f * k;
            body.localScale = new Vector3(L * sx, L * sy, 1f);
            shadow.localScale = body.localScale;
            spikes.r.transform.localScale = new Vector3(L * 1.05f * sx, L * 0.62f * sy, 1f);
            finL.localPosition = new Vector2(finBase.x, finBase.y * sy);
            finR.localPosition = new Vector2(finBase.x, -finBase.y * sy);
            spikes.c0 = new Color(1, 1, 1, k);
            var c = spikes.c0; c.a *= alpha; spikes.sr.color = c;
        }
    }

    // ================================================================ 오징어
    public class SquidVisual : CreatureVisual
    {
        readonly LineRenderer[] arms = new LineRenderer[10];
        readonly Vector3[] buf = new Vector3[10];
        readonly Transform body;
        float squish;

        public SquidVisual(Transform parent, float length) : base(parent, length)
        {
            shadow = SpritePart(root, "shadow", Art.SquidBody, new Color(0f, 0.01f, 0.02f, 0.25f), -5, Vector2.zero, new Vector2(L, L)).r.transform;
            var club = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.7f, 0.55f), new Keyframe(0.86f, 1.7f), new Keyframe(1f, 0.3f));
            for (int i = 0; i < 10; i++)
            {
                bool tentacle = i >= 8;
                arms[i] = LinePart(root, tentacle ? "tentacle" : "arm", buf.Length,
                    new Color(0.97f, 0.8f, 0.78f, 0.95f), new Color(0.95f, 0.68f, 0.66f, 0.85f),
                    (tentacle ? 0.022f : 0.036f) * L, tentacle ? club : Taper(0.25f), 0);
            }
            body = SpritePart(root, "body", Art.SquidBody, Color.white, 2, Vector2.zero, new Vector2(L, L)).r.transform;
        }

        public override void Animate(float dt, float swim, float worldScale)
        {
            phase += dt * (3f + swim * 6f);
            UpdateWidths(worldScale);
            float spread = 1f - 0.65f * squish;
            for (int i = 0; i < 10; i++)
            {
                bool tentacle = i >= 8;
                float off = tentacle ? (i == 8 ? -0.25f : 0.25f) : (i / 7f - 0.5f) * 2f;
                var p = new Vector2(-0.47f * L, off * 0.07f * L);
                float baseAng = Mathf.PI + off * 0.45f * spread;
                float len = (tentacle ? 0.55f : 0.3f) * L * (1f + 0.15f * squish);
                float seg = len / (buf.Length - 1);
                for (int k = 0; k < buf.Length; k++)
                {
                    float s = k / (buf.Length - 1f);
                    float ang = baseAng + Mathf.Sin(phase + i * 0.8f + s * 3f) * 0.25f * s;
                    buf[k] = p;
                    p += Dir(ang) * seg;
                }
                arms[i].SetPositions(buf);
            }
        }

        public override void SetSquish(float k)
        {
            squish = k;
            body.localScale = new Vector3(L * (1f - 0.12f * k), L * (1f + 0.12f * k), 1f);
        }
    }

    // ================================================================ 문어
    public class OctopusVisual : CreatureVisual
    {
        static readonly float[] BaseDeg = { 25f, -25f, 68f, -68f, 112f, -112f, 155f, -155f };
        readonly LineRenderer[] arms = new LineRenderer[8];
        readonly Vector3[] buf = new Vector3[12];
        readonly Vector2 origin;
        float dash;

        public OctopusVisual(Transform parent, float length) : base(parent, length)
        {
            origin = new Vector2(0.04f * L, 0f);
            var mantlePos = new Vector2(-0.16f * L, 0f);
            var mantleScale = new Vector2(0.56f * L, 0.44f * L);
            shadow = SpritePart(root, "shadow", Art.SoftCircle, new Color(0f, 0.01f, 0.02f, 0.35f), -5, mantlePos, mantleScale * 1.6f).r.transform;
            for (int i = 0; i < 8; i++)
                arms[i] = LinePart(root, "arm", buf.Length, Art.OctopusColor, new Color(0.98f, 0.66f, 0.55f, 1f), 0.075f * L, Taper(0.12f), 0);
            SpritePart(root, "mantle", Art.OctopusMantle, Color.white, 2, mantlePos, mantleScale);
            SpritePart(root, "eyeL", Art.OctopusEye, Color.white, 3, new Vector2(0.05f * L, 0.1f * L), new Vector2(0.09f * L, 0.09f * L));
            SpritePart(root, "eyeR", Art.OctopusEye, Color.white, 3, new Vector2(0.05f * L, -0.1f * L), new Vector2(0.09f * L, 0.09f * L));
        }

        public override void SetDash(float k) { dash = k; }

        public override void Animate(float dt, float swim, float worldScale)
        {
            phase += dt * (1.4f + swim * 6f);
            UpdateWidths(worldScale);
            for (int i = 0; i < 8; i++)
            {
                float a0 = BaseDeg[i] * Mathf.Deg2Rad;
                float sign = a0 > 0f ? 1f : -1f;
                float a = Mathf.Lerp(a0, sign * Mathf.PI * 0.94f, dash);
                float len = 0.5f * L * (1f - 0.15f * dash);
                float seg = len / (buf.Length - 1);
                var p = origin + Dir(a) * 0.06f * L;
                for (int k = 0; k < buf.Length; k++)
                {
                    float s = k / (buf.Length - 1f);
                    float curl = Mathf.Sin(phase + i * 1.7f + s * 3.2f) * (0.55f + swim * 0.8f) * s * (1f - dash * 0.7f)
                               + sign * 0.6f * s * s * (1f - dash);
                    buf[k] = p;
                    p += Dir(a + curl) * seg;
                }
                arms[i].SetPositions(buf);
            }
        }
    }

    // ================================================================ 해파리
    public class JellyVisual : CreatureVisual
    {
        readonly LineRenderer[] tentacles = new LineRenderer[8];
        readonly LineRenderer[] oral = new LineRenderer[4];
        readonly Vector3[] buf = new Vector3[12];
        readonly Transform bell;

        public JellyVisual(Transform parent, float length) : base(parent, length)
        {
            SpritePart(root, "glow", Art.SoftCircle, new Color(0.75f, 0.6f, 1f, 0.22f), -2, Vector2.zero, new Vector2(1.9f * L, 1.9f * L));
            for (int i = 0; i < tentacles.Length; i++)
                tentacles[i] = LinePart(root, "tentacle", buf.Length, new Color(0.88f, 0.78f, 1f, 0.6f), new Color(0.8f, 0.7f, 1f, 0.05f), 0.012f * L, Taper(0.4f), 0);
            for (int i = 0; i < oral.Length; i++)
                oral[i] = LinePart(root, "oralArm", buf.Length, new Color(1f, 0.72f, 0.86f, 0.6f), new Color(1f, 0.8f, 0.9f, 0.15f), 0.06f * L, Taper(0.2f), 1);
            bell = SpritePart(root, "bell", Art.JellyBell, Color.white, 2, Vector2.zero, new Vector2(L, L)).r.transform;
        }

        public override void Animate(float dt, float swim, float worldScale)
        {
            phase += dt;
            UpdateWidths(worldScale);
            float pulse = Mathf.Sin(phase * Mathf.PI * 2f / 1.8f);
            float k = 1f - 0.1f * Mathf.Max(0f, pulse);
            bell.localScale = new Vector3(L * k, L * k, 1f);

            for (int i = 0; i < tentacles.Length; i++)
            {
                float th = Mathf.PI + (i / (tentacles.Length - 1f) - 0.5f) * 2.2f;
                var p = Dir(th) * 0.4f * L * k;
                float len = 0.95f * L, seg = len / (buf.Length - 1);
                for (int j = 0; j < buf.Length; j++)
                {
                    float s = j / (buf.Length - 1f);
                    float ang = Mathf.PI + (th - Mathf.PI) * 0.3f + Mathf.Sin(phase * 2.2f + i + s * 4f) * 0.35f * s;
                    buf[j] = p;
                    p += Dir(ang) * seg;
                }
                tentacles[i].SetPositions(buf);
            }
            for (int i = 0; i < oral.Length; i++)
            {
                var p = new Vector2(0f, (i - 1.5f) * 0.05f * L);
                float len = 0.55f * L, seg = len / (buf.Length - 1);
                for (int j = 0; j < buf.Length; j++)
                {
                    float s = j / (buf.Length - 1f);
                    float ang = Mathf.PI + (i - 1.5f) * 0.12f + Mathf.Sin(phase * 1.6f + i * 1.3f + s * 6f) * 0.5f * s;
                    buf[j] = p;
                    p += Dir(ang) * seg;
                }
                oral[i].SetPositions(buf);
            }
        }
    }

    // ================================================================ Asset Store 프리팹
    public class PrefabVisual : CreatureVisual
    {
        readonly GameObject instance;
        readonly Renderer[] renderers;

        public PrefabVisual(SpeciesDef def, Transform parent, float length) : base(parent, length)
        {
            instance = Object.Instantiate(def.prefabOverride, root, false);
            instance.name = def.prefabOverride.name;
            var t = instance.transform;
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.LookRotation(Vector3.right, Vector3.back) * Quaternion.Euler(def.prefabRotation);
            t.localScale = Vector3.one;

            // 물리 컴포넌트는 꺼서 떨어지거나 부딪히지 않게 함
            foreach (var rb in instance.GetComponentsInChildren<Rigidbody>(true)) rb.isKinematic = true;
            foreach (var c in instance.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (var rb in instance.GetComponentsInChildren<Rigidbody2D>(true)) rb.simulated = false;
            foreach (var c in instance.GetComponentsInChildren<Collider2D>(true)) c.enabled = false;

            renderers = instance.GetComponentsInChildren<Renderer>(true);

            // 길이(진행 방향 = 로컬 X)가 L이 되도록 맞추고 가운데 정렬
            bool has = false;
            Vector3 min = Vector3.zero, max = Vector3.zero;
            foreach (var r in renderers)
            {
                if (r is ParticleSystemRenderer) continue;
                var b = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3(
                        (i & 1) == 0 ? b.min.x : b.max.x,
                        (i & 2) == 0 ? b.min.y : b.max.y,
                        (i & 4) == 0 ? b.min.z : b.max.z);
                    var lp = root.InverseTransformPoint(corner);
                    if (!has) { min = max = lp; has = true; }
                    else { min = Vector3.Min(min, lp); max = Vector3.Max(max, lp); }
                }
            }
            if (has)
            {
                float extent = Mathf.Max(max.x - min.x, 0.0001f);
                float s = L / extent * def.prefabScale;
                var center = (min + max) * 0.5f;
                t.localScale = Vector3.one * s;
                t.localPosition = new Vector3(-center.x * s, -center.y * s, 0f);
            }
        }

        public override void SetSorting(int baseOrder)
        {
            foreach (var r in renderers) if (r != null) r.sortingOrder = baseOrder + 2;
        }

        public override void SetAlpha(float a)
        {
            alpha = a;
            bool on = a > 0.05f;
            if (instance.activeSelf != on) instance.SetActive(on);
        }

        public override void Animate(float dt, float swim, float worldScale) { }
    }
}
