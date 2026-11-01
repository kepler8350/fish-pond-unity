using System.Collections.Generic;
using UnityEngine;

namespace FishPond
{
    /// <summary>
    /// 산호·바위·해초 같은 배경 장식을 3D 모델로 바닥에 놓습니다 (위에서 내려다본 모습).
    /// 모델은 "Fish Pond/가져온 실사 에셋 연결하기"로 생물 설정 에셋의 decor 목록에 들어갑니다.
    /// </summary>
    public static class Decor
    {
        static List<DecorDef> all;

        static List<DecorDef> All
        {
            get
            {
                if (all == null)
                {
                    var cat = SpeciesCatalog.Load();
                    all = cat != null && cat.decor != null ? cat.decor.FindAll(d => d != null && d.prefab != null) : new List<DecorDef>();
                }
                return all;
            }
        }

        public static void Reload() { all = null; }

        public static bool Has(DecorKind kind) { return All.Exists(d => d.kind == kind); }

        public static DecorDef Pick(System.Random rnd, DecorKind kind)
        {
            var list = All.FindAll(d => d.kind == kind);
            return list.Count == 0 ? null : list[rnd.Next(list.Count)];
        }

        /// <summary>
        /// 장식 하나를 놓습니다. size = 위에서 본 가장 긴 폭, zFront = 카메라 쪽 윗면의 z (생물보다 뒤),
        /// maxDepth = 두께 상한 (물 바닥(z=1)을 뚫지 않게 납작하게 누름).
        /// </summary>
        public static Transform Place(DecorDef d, Transform parent, Vector2 pos, float size, float yawDeg, float zFront, float maxDepth)
        {
            if (d == null || d.prefab == null) return null;
            var holder = new GameObject("decor_" + d.kind).transform;
            holder.SetParent(parent, false);
            holder.localPosition = new Vector3(pos.x, pos.y, zFront);
            holder.localRotation = Quaternion.Euler(0f, 0f, yawDeg);

            var inst = Object.Instantiate(d.prefab, holder, false);
            inst.name = d.prefab.name;
            var t = inst.transform;
            // Sketchfab glb는 대부분 위가 +Z인 채로 들어오므로, 모델의 +Z가 카메라 쪽(-Z)을 보게 뒤집음
            // (위가 +Y인 모델은 회전 보정 X=90)
            t.localRotation = Quaternion.Euler(180f, 0f, 0f) * Quaternion.Euler(d.rotation);
            t.localPosition = Vector3.zero;
            t.localScale = Vector3.one;
            var rs = ModelFit.Prepare(inst, null);

            bool has = false;
            Vector3 min = Vector3.zero, max = Vector3.zero;
            foreach (var r in rs)
            {
                if (r == null || r is ParticleSystemRenderer) continue;
                var b = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                    var lp = holder.InverseTransformPoint(corner);
                    if (!has) { min = max = lp; has = true; } else { min = Vector3.Min(min, lp); max = Vector3.Max(max, lp); }
                }
            }
            if (!has) return holder;
            float extent = Mathf.Max(max.x - min.x, max.y - min.y, 0.0001f);
            float s = size / extent * d.scale;
            float depth = (max.z - min.z) * s;
            float kz = depth > maxDepth ? maxDepth / depth : 1f;
            // 가운데 정렬 + 윗면(가장 작은 z)이 holder 위치에 오게
            t.localPosition = new Vector3(-(min.x + max.x) * 0.5f, -(min.y + max.y) * 0.5f, -min.z);
            holder.localScale = new Vector3(s, s, s * kz);
            return holder;
        }
    }
}
