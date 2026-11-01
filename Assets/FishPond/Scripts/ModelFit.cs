using UnityEngine;

namespace FishPond
{
    /// <summary>
    /// 가져온 3D 모델(Asset Store 프리팹, Sketchfab glb 등)을 게임 생물 자리에 맞춰 넣습니다.
    /// 진행 방향 = 부모의 로컬 +X, 몸길이 = L.
    /// </summary>
    public static class ModelFit
    {
        public static GameObject Spawn(GameObject prefab, Transform root, float L, bool sideView, Vector3 rotation, float scale,
                                       RuntimeAnimatorController controller, out Renderer[] renderers)
        {
            return Spawn(prefab, root, L, sideView, rotation, scale, 0f, controller, out renderers);
        }

        public static GameObject Spawn(GameObject prefab, Transform root, float L, bool sideView, Vector3 rotation, float scale, float tilt,
                                       RuntimeAnimatorController controller, out Renderer[] renderers)
        {
            var instance = Object.Instantiate(prefab, root, false);
            instance.name = prefab.name;
            var t = instance.transform;
            t.localPosition = Vector3.zero;
            // 옆모습: 모델의 오른쪽 옆구리가 카메라를 봄 / 위모습: 모델의 등이 카메라를 봄
            var baseRot = sideView ? Quaternion.LookRotation(Vector3.right, Vector3.up)
                                   : Quaternion.LookRotation(Vector3.right, Vector3.back);
            t.localRotation = baseRot * Quaternion.Euler(0f, 0f, tilt) * Quaternion.Euler(rotation);
            t.localScale = Vector3.one;

            renderers = Prepare(instance, controller);

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
                float s = L / extent * scale;
                var center = (min + max) * 0.5f;
                t.localScale = Vector3.one * s;
                t.localPosition = new Vector3(-center.x * s, -center.y * s, 0f);
            }
            return instance;
        }

        /// <summary>
        /// 가져온 모델 정리: 딸린 스크립트·물리·카메라·조명은 끄고, 못 그리는 재질은 바꾸고, 애니메이션은 반복 재생.
        /// </summary>
        public static Renderer[] Prepare(GameObject instance, RuntimeAnimatorController controller)
        {
            // 에셋에 들어 있는 스크립트(데모 이동·군집 등)는 꺼서 게임의 움직임과 싸우지 않게 함
            foreach (var mb in instance.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
            foreach (var rb in instance.GetComponentsInChildren<Rigidbody>(true)) rb.isKinematic = true;
            foreach (var c in instance.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (var rb in instance.GetComponentsInChildren<Rigidbody2D>(true)) rb.simulated = false;
            foreach (var c in instance.GetComponentsInChildren<Collider2D>(true)) c.enabled = false;
            // 모델에 딸려 온 카메라·조명은 끔 (Sketchfab 장면 등)
            foreach (var cam in instance.GetComponentsInChildren<Camera>(true)) cam.enabled = false;
            foreach (var lt in instance.GetComponentsInChildren<Light>(true)) lt.enabled = false;

            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            FixMaterials(renderers);

            // 애니메이션: 메카님(Animator)과 레거시(Animation, glTF 가져오기 기본값) 모두 반복 재생
            foreach (var anim in instance.GetComponentsInChildren<Animator>(true))
            {
                if (anim.runtimeAnimatorController == null && controller != null)
                    anim.runtimeAnimatorController = controller;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                anim.applyRootMotion = false;
            }
            foreach (var anim in instance.GetComponentsInChildren<Animation>(true))
            {
                anim.cullingType = AnimationCullingType.AlwaysAnimate;
                AnimationClip pick = null, first = null;
                foreach (AnimationState st in anim)
                {
                    if (first == null) first = st.clip;
                    var n = st.name.ToLowerInvariant();
                    if (pick == null && (n.Contains("swim") || n.Contains("idle") || n.Contains("loop"))) pick = st.clip;
                }
                if (pick == null) pick = first;
                if (pick == null) continue;
                anim.wrapMode = WrapMode.Loop;
                anim[pick.name].wrapMode = WrapMode.Loop;
                anim[pick.name].time = Random.value * pick.length;
                anim.Play(pick.name);
            }

            return renderers;
        }

        static Shader standard;

        /// <summary>
        /// 이 프로젝트(Built-in)에서 못 그리는 셰이더(분홍색)를 쓰는 재질은
        /// 같은 텍스처·색으로 Standard 셰이더 재질로 바꿉니다.
        /// </summary>
        public static void FixMaterials(Renderer[] rs)
        {
            if (standard == null) standard = Shader.Find("Standard");
            if (standard == null) return;
            foreach (var r in rs)
            {
                if (r == null || r is ParticleSystemRenderer) continue;
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m != null && m.shader != null && m.shader.isSupported && m.shader.name != "Hidden/InternalErrorShader") continue;
                    var nm = new Material(standard) { name = (m != null ? m.name : "Fish") + " (Standard)" };
                    if (m != null)
                    {
                        foreach (var prop in new[] { "_MainTex", "_BaseMap", "_BaseColorMap", "_Albedo" })
                            if (m.HasProperty(prop) && m.GetTexture(prop) != null) { nm.mainTexture = m.GetTexture(prop); break; }
                        foreach (var prop in new[] { "_Color", "_BaseColor" })
                            if (m.HasProperty(prop)) { nm.color = m.GetColor(prop); break; }
                        if (m.HasProperty("_BumpMap") && m.GetTexture("_BumpMap") != null)
                        {
                            nm.SetTexture("_BumpMap", m.GetTexture("_BumpMap"));
                            nm.EnableKeyword("_NORMALMAP");
                        }
                    }
                    nm.SetFloat("_Glossiness", 0.55f);
                    mats[i] = nm;
                    changed = true;
                }
                if (changed) r.sharedMaterials = mats;
            }
        }
    }
}
