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
            // 모든 생물은 가져온 3D 에셋으로만 그립니다 (에셋이 없는 종은 등장하지 않음)
            return new PrefabVisual(def, parent, def.length);
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
    // ================================================================ Asset Store 프리팹
    public class PrefabVisual : CreatureVisual
    {
        readonly GameObject instance;
        readonly Renderer[] renderers;
        readonly Transform pivot;      // 옆모습 물고기를 진행 방향 축으로 뒤집는 받침
        readonly bool sideView;
        float roll, rollTarget;

        public PrefabVisual(SpeciesDef def, Transform parent, float length) : base(parent, length)
        {
            pivot = Node(root, "pivot", Vector2.zero);
            sideView = def.prefabSideView;
            instance = ModelFit.Spawn(def.prefabOverride, pivot, L, def.prefabSideView, def.prefabRotation, def.prefabScale, def.prefabTilt, def.animatorController, out renderers);

            // 움직임이 없는 물고기 모델은 헤엄 셰이더로 꼬리·지느러미를 흔듦
            bool fish = !(def.id == SpeciesId.SeaTurtle || def.id == SpeciesId.Stingray || def.id == SpeciesId.Octopus
                          || def.id == SpeciesId.Jellyfish || def.id == SpeciesId.Squid);
            if (fish && !ModelFit.HasAnimation(instance))
                swimRenderers = ModelFit.ApplySwim(renderers, root.TransformDirection(Vector3.right), root.TransformDirection(Vector3.up), 0.09f);
        }

        Renderer[] swimRenderers;
        MaterialPropertyBlock mpb;
        float swimPhase = Random.value * 10f;

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

        /// <summary>
        /// 옆모습 물고기가 왼쪽으로 헤엄치면 배가 위로 가므로(뒤집힘),
        /// 진행 방향 축으로 반 바퀴 돌려 항상 등이 화면 위쪽을 향하게 합니다.
        /// </summary>
        public override void Animate(float dt, float swim, float worldScale)
        {
            if (swimRenderers != null && swimRenderers.Length > 0)
            {
                // 빨리 헤엄칠수록 꼬리를 빠르게
                swimPhase += dt * (5f + swim * 9f);
                if (mpb == null) mpb = new MaterialPropertyBlock();
                foreach (var r in swimRenderers)
                {
                    if (r == null) continue;
                    r.GetPropertyBlock(mpb);
                    mpb.SetFloat("_SwimPhase", swimPhase);
                    r.SetPropertyBlock(mpb);
                }
            }
            if (!sideView) return;
            float upY = root.up.y;
            if (upY < -0.15f) rollTarget = 180f;
            else if (upY > 0.15f) rollTarget = 0f;
            if (roll == rollTarget && dt > 0f) return;
            roll = dt <= 0f ? rollTarget : Mathf.MoveTowards(roll, rollTarget, 600f * dt);
            pivot.localRotation = Quaternion.Euler(roll, 0f, 0f);
        }
    }
}
