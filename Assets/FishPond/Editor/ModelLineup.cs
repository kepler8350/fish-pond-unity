using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FishPond
{
    /// <summary>
    /// 연결된 모든 생물 모델을 "오른쪽으로 헤엄치는" 방향으로 한 줄씩 늘어놓는 확인용 장면.
    /// 머리가 오른쪽, 등이 위(옆모습) 또는 등이 화면 쪽(위모습)이면 정상입니다.
    /// 틀린 생물은 생물 설정의 Prefab Rotation(Y=좌우 돌리기, Z=굴리기)으로 고칩니다.
    /// </summary>
    public static class ModelLineup
    {
        class Item { public string name; public GameObject prefab; public bool side; public Vector3 rot; public float scale, tilt; public RuntimeAnimatorController ctrl; }

        [MenuItem("Fish Pond/모델 방향 확인 장면", priority = 20)]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.45f, 0.5f);
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 50f;

            var lg = new GameObject("Light");
            var light = lg.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            RenderSettings.ambientLight = new Color(0.55f, 0.6f, 0.62f);

            var cat = SpeciesCatalog.Load();
            if (cat == null) { Debug.LogWarning("[FishPond] 생물 설정 에셋이 없습니다."); return; }

            var items = new System.Collections.Generic.List<Item>();
            foreach (var s in cat.species)
                if (s != null && s.prefabOverride != null)
                    items.Add(new Item { name = s.displayName + " (" + s.id + ")", prefab = s.prefabOverride, side = s.prefabSideView, rot = s.prefabRotation, scale = s.prefabScale, tilt = s.prefabTilt, ctrl = s.animatorController });
            if (cat.sharkPrefab != null)
                items.Add(new Item { name = "상어 (Shark)", prefab = cat.sharkPrefab, side = cat.sharkSideView, rot = cat.sharkRotation, scale = cat.sharkScale, ctrl = cat.sharkController });

            int cols = 4;
            float cellW = 4.2f, cellH = 3.6f;
            int rows = Mathf.CeilToInt(items.Count / (float)cols);
            for (int i = 0; i < items.Count; i++)
            {
                int cx = i % cols, cy = i / cols;
                var pos = new Vector3((cx - (cols - 1) * 0.5f) * cellW, ((rows - 1) * 0.5f - cy) * cellH, 0f);
                var holder = new GameObject(items[i].name).transform;
                holder.position = pos;
                Renderer[] rs;
                ModelFit.Spawn(items[i].prefab, holder, 2.6f, items[i].side, items[i].rot, items[i].scale, items[i].tilt, items[i].ctrl, out rs);
                // 확인용: 가장 긴 변이 칸에 맞게 다시 맞춤 (축이 틀린 모델도 크기가 비슷하게 보이도록)
                bool has = false; Bounds b = new Bounds();
                foreach (var r in rs) { if (r == null) continue; if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds); }
                if (has)
                {
                    float m = Mathf.Max(b.size.x, b.size.y);
                    if (m > 0.0001f)
                    {
                        float k = 2.6f / m;
                        holder.localScale = Vector3.one * k;
                        holder.position = pos + (pos - b.center) * k;
                    }
                }

                var label = new GameObject("label");
                label.transform.position = pos + new Vector3(0f, -1.55f, -2f);
                var tm = label.AddComponent<TextMesh>();
                tm.text = (i + 1) + ". " + items[i].name + "  →";
                tm.characterSize = 0.09f;
                tm.fontSize = 48;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.color = Color.white;
            }
            // 배경 장식: 위에서 내려다본 모습 (아래쪽에 이어서)
            int baseRow = rows;
            var decor = cat.decor ?? new System.Collections.Generic.List<DecorDef>();
            int di = 0;
            foreach (var d in decor)
            {
                if (d == null || d.prefab == null) continue;
                int cx = di % cols, cy = baseRow + di / cols;
                var pos = new Vector3((cx - (cols - 1) * 0.5f) * cellW, ((rows - 1) * 0.5f - cy) * cellH, 0f);
                var holder = new GameObject("decor").transform;
                Decor.Place(d, holder, new Vector2(pos.x, pos.y), 2.4f, 0f, 0f, 100f);
                var label = new GameObject("label");
                label.transform.position = pos + new Vector3(0f, -1.55f, -2f);
                var tm = label.AddComponent<TextMesh>();
                tm.text = "장식 " + (di + 1) + ". " + d.kind + " / " + d.prefab.name;
                tm.characterSize = 0.07f;
                tm.fontSize = 48;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.color = Color.white;
                di++;
            }
            int totalRows = rows + Mathf.CeilToInt(di / (float)cols);
            cam.transform.position = new Vector3(0f, -(totalRows - rows) * cellH * 0.5f, -10f);
            cam.orthographicSize = Mathf.Max(5f, totalRows * cellH * 0.55f);
            Debug.Log("[FishPond] 모델 방향 확인: 모두 머리가 오른쪽(→)을 향해야 합니다. 옆모습 물고기는 등이 위, 거북·가오리·상어는 등이 보여야 합니다.");
        }
    }
}
