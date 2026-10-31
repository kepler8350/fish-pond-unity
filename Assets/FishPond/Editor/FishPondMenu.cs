using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishPond.EditorTools
{
    /// <summary>
    /// 상단 메뉴 "Fish Pond".
    /// 두 게임은 씬이 따로 있고, 실행파일도 따로 만들어집니다.
    ///   뜰채 바다   → Assets/FishPond/Scenes/FishPond.unity     → Builds/FishPond/FishPond.exe
    ///   상어 구조대 → Assets/FishPond/Scenes/SharkRescue.unity  → Builds/SharkRescue/SharkRescue.exe
    /// </summary>
    [InitializeOnLoad]
    public static class FishPondMenu
    {
        public const string ScenePath = "Assets/FishPond/Scenes/FishPond.unity";
        public const string SharkScenePath = "Assets/FishPond/Scenes/SharkRescue.unity";
        public const string CatalogPath = "Assets/FishPond/Resources/FishPond/SpeciesCatalog.asset";
        const string FreeAssetsUrl = "https://assetstore.unity.com/3d/characters/animals/fish?free=true&orderBy=6";

        static FishPondMenu()
        {
            EditorApplication.delayCall += AutoOpen;
        }

        static void AutoOpen()
        {
            if (SessionState.GetBool("FishPond.AutoOpened", false)) return;
            SessionState.SetBool("FishPond.AutoOpened", true);
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EnsureScene(SharkScenePath, typeof(SharkRescueGame), false);
            if (!string.IsNullOrEmpty(SceneManager.GetActiveScene().path)) return;
            if (File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath);
            else EnsureScene(ScenePath, typeof(FishPondGame), true);
        }

        /// <summary>씬이 없으면 만듭니다. open=false면 현재 씬을 건드리지 않고 따로 만듭니다.</summary>
        static void EnsureScene(string path, System.Type game, bool open)
        {
            if (File.Exists(path)) { if (open) EditorSceneManager.OpenScene(path); return; }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var mode = open ? NewSceneMode.Single : NewSceneMode.Additive;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
            var go = new GameObject(game.Name);
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent(game);
            EditorSceneManager.SaveScene(scene, path);
            if (!open) EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.Refresh();
            Debug.Log("[FishPond] 씬을 만들었습니다: " + path);
        }

        [MenuItem("Fish Pond/뜰채 바다 씬 열기", priority = 0)]
        static void OpenFishPond() { OpenScene(ScenePath, typeof(FishPondGame)); }

        [MenuItem("Fish Pond/상어 구조대 씬 열기", priority = 1)]
        static void OpenSharkRescue() { OpenScene(SharkScenePath, typeof(SharkRescueGame)); }

        static void OpenScene(string path, System.Type game)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureScene(path, game, true);
        }

        // ---------------------------------------------------------------- 실행파일
        [MenuItem("Fish Pond/실행파일 만들기 (두 게임 모두)", priority = 10)]
        public static void BuildAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var current = SceneManager.GetActiveScene().path;
            EnsureScene(ScenePath, typeof(FishPondGame), false);
            EnsureScene(SharkScenePath, typeof(SharkRescueGame), false);
            bool a = Build("뜰채 바다", ScenePath, "Builds/FishPond/FishPond.exe");
            bool b = Build("상어 구조대", SharkScenePath, "Builds/SharkRescue/SharkRescue.exe");
            if (!string.IsNullOrEmpty(current) && File.Exists(current)) EditorSceneManager.OpenScene(current);
            string msg = (a && b) ? "두 실행파일을 만들었습니다:\n" + Path.GetFullPath("Builds")
                                   : "빌드 중 오류가 있었습니다. Console 창을 확인하세요.";
            Debug.Log("[FishPond] " + msg);
            if (a && b) EditorUtility.RevealInFinder(Path.GetFullPath("Builds/FishPond/FishPond.exe"));
        }

        [MenuItem("Fish Pond/실행파일 만들기 - 뜰채 바다만", priority = 11)]
        static void BuildFishPond() { EnsureScene(ScenePath, typeof(FishPondGame), false); Build("뜰채 바다", ScenePath, "Builds/FishPond/FishPond.exe"); }

        [MenuItem("Fish Pond/실행파일 만들기 - 상어 구조대만", priority = 12)]
        static void BuildShark() { EnsureScene(SharkScenePath, typeof(SharkRescueGame), false); Build("상어 구조대", SharkScenePath, "Builds/SharkRescue/SharkRescue.exe"); }

        static bool Build(string productName, string scene, string exePath)
        {
            var oldName = PlayerSettings.productName;
            // 이전 빌드가 중간에 멈춰 게임 이름이 남아 있으면 프로젝트 이름으로 되돌림
            if (oldName == "뜰채 바다" || oldName == "상어 구조대") oldName = "fish-pond-unity";
            var oldMode = PlayerSettings.fullScreenMode;
            try
            {
                PlayerSettings.productName = productName;
                PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
                var opts = new BuildPlayerOptions
                {
                    scenes = new[] { scene },
                    locationPathName = exePath,
                    target = BuildTarget.StandaloneWindows64,
                    targetGroup = BuildTargetGroup.Standalone,
                    options = BuildOptions.None
                };
                var report = BuildPipeline.BuildPlayer(opts);
                bool ok = report.summary.result == BuildResult.Succeeded;
                Debug.Log("[FishPond] " + productName + " 빌드 " + (ok ? "성공" : "실패: " + report.summary.result) + " → " + exePath);
                // CC BY 3D 모델 출처 표기를 실행파일 옆에 함께 넣음
                if (ok && File.Exists("CREDITS.md"))
                    File.Copy("CREDITS.md", Path.Combine(Path.GetDirectoryName(exePath), "CREDITS.md"), true);
                return ok;
            }
            finally
            {
                PlayerSettings.productName = oldName;
                PlayerSettings.fullScreenMode = oldMode;
            }
        }

        // ---------------------------------------------------------------- 기타
        static SpeciesCatalog LoadOrCreateCatalog()
        {
            var cat = AssetDatabase.LoadAssetAtPath<SpeciesCatalog>(CatalogPath);
            if (cat == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath));
                cat = ScriptableObject.CreateInstance<SpeciesCatalog>();
                AssetDatabase.CreateAsset(cat, CatalogPath);
                AssetDatabase.SaveAssets();
            }
            // 새로 추가된 생물이 있으면 목록에 보충
            foreach (var d in SpeciesDefaults.Create())
                if (!cat.species.Exists(s => s.id == d.id)) cat.species.Add(d);
            return cat;
        }

        [MenuItem("Fish Pond/생물 설정 에셋 열기 (에셋 교체용)", priority = 20)]
        static void CreateCatalog()
        {
            var cat = LoadOrCreateCatalog();
            EditorUtility.SetDirty(cat);
            AssetDatabase.SaveAssets();
            Selection.activeObject = cat;
            EditorGUIUtility.PingObject(cat);
        }

        // 가져온 에셋의 파일 이름에서 찾을 단어 (앞쪽이 우선)
        static readonly Dictionary<SpeciesId, string[]> Keywords = new Dictionary<SpeciesId, string[]>
        {
            { SpeciesId.Angelfish, new[] { "EmperorAngelfish", "Emperor", "Angelfish" } },
            { SpeciesId.Sardine, new[] { "Sardine" } },
            { SpeciesId.Clownfish, new[] { "Clownfish", "Clown_fish", "Clown" } },
            { SpeciesId.BlueTang, new[] { "BlueTang", "Blue_Tang", "Blue Tang", "Dory", "Surgeonfish" } },
            { SpeciesId.YellowTang, new[] { "YellowTang", "Yellow_Tang", "Yellow Tang" } },
            { SpeciesId.Butterflyfish, new[] { "Butterflyfish", "Butterfly_fish" } },
            { SpeciesId.Pufferfish, new[] { "Pufferfish", "Puffer" } },
            { SpeciesId.Lionfish, new[] { "Lionfish", "Lion_fish" } },
            { SpeciesId.SeaTurtle, new[] { "SeaTurtle", "Sea_Turtle", "Turtle" } },
            { SpeciesId.Stingray, new[] { "Stingray", "Eagle_Ray", "Eagle Ray", "Manta" } },
            { SpeciesId.Octopus, new[] { "Octopus" } },
            { SpeciesId.Squid, new[] { "Squid" } },
            { SpeciesId.Jellyfish, new[] { "Jellyfish" } },
        };

        /// <summary>
        /// Asset Store·Sketchfab 등에서 가져온 모델을 이름으로 찾아 생물에 연결합니다.
        /// 프리팹을 먼저 찾고, 없으면 모델(FBX 등)을 씁니다. 헤엄 애니메이터 컨트롤러도 함께 찾습니다.
        /// </summary>
        [MenuItem("Fish Pond/가져온 실사 에셋 연결하기", priority = 19)]
        public static void AutoLink()
        {
            var cat = LoadOrCreateCatalog();
            var log = new StringBuilder("[FishPond] 실사 에셋 연결 결과\n");
            int linked = 0;
            foreach (var kv in Keywords)
            {
                var def = cat.species.Find(s => s.id == kv.Key);
                if (def == null) continue;
                GameObject model = null;
                string used = null;
                foreach (var kw in kv.Value)
                {
                    model = FindModel("t:Prefab", kw) ?? FindModel("t:GameObject", kw);
                    if (model != null) { used = kw; break; }
                }
                if (model == null) continue;
                if (def.prefabOverride != model)
                {
                    // 넓적한 몸(거북·가오리)은 위에서 본 모습, 나머지는 옆모습이 기본
                    def.prefabSideView = !(kv.Key == SpeciesId.SeaTurtle || kv.Key == SpeciesId.Stingray);
                    def.prefabRotation = Vector3.zero;
                }
                def.prefabOverride = model;
                ApplyKnownFix(model, ref def.prefabSideView, ref def.prefabRotation);
                var ctrl = FindController(used);
                if (ctrl != null) def.animatorController = ctrl;
                linked++;
                int fixedMats = RepairMaterials(model);
                log.AppendLine("  " + def.displayName + " ← " + AssetDatabase.GetAssetPath(model) + (ctrl != null ? "  (애니메이션: " + ctrl.name + ")" : "")
                    + (fixedMats > 0 ? "  (재질 " + fixedMats + "개를 기본 셰이더로 변환)" : ""));
            }
            // 상어 구조대의 상어
            GameObject shark = null;
            foreach (var kw in new[] { "Great_White_Shark", "GreatWhite", "White_Pointer", "Shark" })
            {
                shark = FindModel("t:Prefab", kw) ?? FindModel("t:GameObject", kw);
                if (shark != null) break;
            }
            if (shark != null)
            {
                if (cat.sharkPrefab != shark) { cat.sharkSideView = false; cat.sharkRotation = Vector3.zero; }
                cat.sharkPrefab = shark;
                ApplyKnownFix(shark, ref cat.sharkSideView, ref cat.sharkRotation);
                var sc = FindController("Shark");
                if (sc != null) cat.sharkController = sc;
                int fm = RepairMaterials(shark);
                linked++;
                log.AppendLine("  상어(상어 구조대) ← " + AssetDatabase.GetAssetPath(shark) + (fm > 0 ? "  (재질 " + fm + "개 변환)" : ""));
            }
            EditorUtility.SetDirty(cat);
            AssetDatabase.SaveAssets();
            if (linked == 0) log.AppendLine("  연결할 모델을 찾지 못했습니다. 에셋을 먼저 가져오세요 (Window > Package Management > My Assets).");
            // 진단: 가져온 에셋 폴더의 모델·프리팹 목록
            log.AppendLine("가져온 모델·프리팹 (FishPond 제외):");
            int shown = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:GameObject"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith("Assets/FishPond/", StringComparison.Ordinal) || !path.StartsWith("Assets/", StringComparison.Ordinal)) continue;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                bool r = go != null && go.GetComponentInChildren<Renderer>(true) != null;
                log.AppendLine("  " + path + (r ? "" : "  (렌더러 없음)"));
                if (++shown >= 40) { log.AppendLine("  ..."); break; }
            }
            Debug.Log(log.ToString());
            Selection.activeObject = cat;
        }

        /// <summary>
        /// URP/HDRP 전용 재질(분홍색)을 기본(Built-in) Standard 셰이더로 바꿉니다.
        /// 셰이더가 없으면 실행 중에는 텍스처 정보를 읽을 수 없으므로, 에디터에서 저장된 값을 직접 읽어 옮깁니다.
        /// </summary>
        static int RepairMaterials(GameObject model)
        {
            var standard = Shader.Find("Standard");
            if (standard == null) return 0;
            int count = 0;
            var done = new HashSet<Material>();
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null || !done.Add(m)) continue;
                    if (m.shader != null && m.shader.isSupported && m.shader.name != "Hidden/InternalErrorShader"
                        && !m.shader.name.StartsWith("Universal Render Pipeline", StringComparison.Ordinal)
                        && !m.shader.name.StartsWith("HDRP", StringComparison.Ordinal)) continue;
                    if (!AssetDatabase.GetAssetPath(m).EndsWith(".mat", StringComparison.OrdinalIgnoreCase)) continue;

                    Texture albedo = null, normal = null, mask = null;
                    Color color = Color.white;
                    bool hasColor = false;
                    float cutoff = 0.5f;
                    var so = new SerializedObject(m);
                    var texEnvs = so.FindProperty("m_SavedProperties.m_TexEnvs");
                    if (texEnvs != null)
                    {
                        for (int i = 0; i < texEnvs.arraySize; i++)
                        {
                            var e = texEnvs.GetArrayElementAtIndex(i);
                            var name = e.FindPropertyRelative("first").stringValue;
                            var tex = e.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;
                            if (tex == null) continue;
                            var n = name.ToLowerInvariant();
                            if (albedo == null && (n == "_basemap" || n == "_maintex" || n == "_basecolormap" || n.Contains("albedo") || n.Contains("diffuse"))) albedo = tex;
                            else if (normal == null && (n == "_bumpmap" || n.Contains("normal"))) normal = tex;
                            else if (mask == null && (n.Contains("metallic") || n.Contains("mask"))) mask = tex;
                        }
                    }
                    var colors = so.FindProperty("m_SavedProperties.m_Colors");
                    if (colors != null)
                        for (int i = 0; i < colors.arraySize; i++)
                        {
                            var e = colors.GetArrayElementAtIndex(i);
                            var name = e.FindPropertyRelative("first").stringValue;
                            if (name == "_BaseColor" || (name == "_Color" && !hasColor)) { color = e.FindPropertyRelative("second").colorValue; hasColor = true; }
                        }
                    var floats = so.FindProperty("m_SavedProperties.m_Floats");
                    if (floats != null)
                        for (int i = 0; i < floats.arraySize; i++)
                        {
                            var e = floats.GetArrayElementAtIndex(i);
                            if (e.FindPropertyRelative("first").stringValue == "_Cutoff") cutoff = e.FindPropertyRelative("second").floatValue;
                        }
                    // 텍스처 이름이 저장되어 있지 않으면 같은 에셋 폴더에서 이름으로 찾기
                    if (albedo == null) albedo = FindTextureNear(m, new[] { "albedo", "basecolor", "base_color", "diffuse", "color", "_d" });
                    if (normal == null) normal = FindTextureNear(m, new[] { "normal", "_n" });

                    int queue = m.renderQueue;
                    Undo.RecordObject(m, "FishPond 재질 변환");
                    m.shader = standard;
                    m.color = hasColor ? color : Color.white;
                    if (albedo != null) m.mainTexture = albedo;
                    if (normal != null) { m.SetTexture("_BumpMap", normal); m.EnableKeyword("_NORMALMAP"); }
                    m.SetFloat("_Glossiness", 0.55f);
                    m.SetFloat("_Metallic", 0f);
                    if (queue >= 2400 && queue < 3000)
                    {
                        // 지느러미 등 알파 테스트 재질 → Standard Cutout
                        m.SetFloat("_Mode", 1f);
                        m.SetFloat("_Cutoff", cutoff);
                        m.SetOverrideTag("RenderType", "TransparentCutout");
                        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                        m.SetInt("_ZWrite", 1);
                        m.EnableKeyword("_ALPHATEST_ON");
                        m.DisableKeyword("_ALPHABLEND_ON");
                        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        m.renderQueue = 2450;
                    }
                    else if (queue >= 3000)
                    {
                        m.SetFloat("_Mode", 2f);
                        m.SetOverrideTag("RenderType", "Transparent");
                        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        m.SetInt("_ZWrite", 0);
                        m.EnableKeyword("_ALPHABLEND_ON");
                        m.DisableKeyword("_ALPHATEST_ON");
                        m.renderQueue = 3000;
                    }
                    EditorUtility.SetDirty(m);
                    count++;
                }
            }
            if (count > 0) AssetDatabase.SaveAssets();
            return count;
        }

        static Texture FindTextureNear(Material m, string[] hints)
        {
            var dir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(m)).Replace('\\', '/');
            // 재질 폴더의 상위(에셋 루트 쪽)까지 올라가며 찾기
            for (int up = 0; up < 3 && !string.IsNullOrEmpty(dir); up++)
            {
                var stem = m.name.Replace("M_", "").Replace("Mat_", "").ToLowerInvariant();
                Texture best = null;
                int bestScore = 0;
                foreach (var guid in AssetDatabase.FindAssets("t:Texture", new[] { dir }))
                {
                    var p = AssetDatabase.GUIDToAssetPath(guid);
                    var fn = Path.GetFileNameWithoutExtension(p).ToLowerInvariant();
                    int score = 0;
                    for (int h = 0; h < hints.Length; h++) if (fn.Contains(hints[h])) { score += 10 - h; break; }
                    if (score == 0) continue;
                    foreach (var part in stem.Split('_', ' ')) if (part.Length > 2 && fn.Contains(part)) score += 5;
                    if (score > bestScore) { bestScore = score; best = AssetDatabase.LoadAssetAtPath<Texture>(p); }
                }
                if (best != null) return best;
                var parent = Path.GetDirectoryName(dir);
                dir = parent == null ? null : parent.Replace('\\', '/');
            }
            return null;
        }

        /// <summary>
        /// Sketchfab 모델마다 앞·위 축이 제각각이라, 확인한 모델은 회전 보정값을 미리 넣어 둡니다.
        /// (기준: 머리 = 진행 방향, 옆모습은 등이 위 / 위모습은 등이 화면 쪽)
        /// </summary>
        struct Fix { public bool side; public Vector3 rot; public Fix(bool s, float x, float y, float z) { side = s; rot = new Vector3(x, y, z); } }
        static readonly Dictionary<string, Fix> KnownFixes = new Dictionary<string, Fix>(StringComparer.OrdinalIgnoreCase)
        {
            { "clownfish",                              new Fix(true, -90f, 0f, 0f) },
            { "blue_tang_low_res",                      new Fix(true, 90f, 0f, 180f) },
            { "minna_no_ennichi_pufferfish",            new Fix(true, -90f, 0f, 0f) },
            { "oxidane_-_yellow_tang",                  new Fix(true, -90f, 0f, 90f) },
            { "lined_butterflyfish",                    new Fix(true, -90f, 0f, -90f) },
            { "lion_fish",                              new Fix(true, -90f, 0f, 180f) },
            { "model_53a_-_flatback_sea_turtle",        new Fix(false, 0f, 180f, 0f) },
            { "bullnose_eagle_ray_-_myliobatis_freminvillii", new Fix(false, -90f, 0f, 90f) },
            { "great_white_shark",                      new Fix(false, 0f, 0f, 0f) },
            { "octopus",                                new Fix(false, 0f, 0f, 0f) },
        };

        static void ApplyKnownFix(GameObject model, ref bool side, ref Vector3 rot)
        {
            Fix f;
            var stem = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(model));
            if (KnownFixes.TryGetValue(stem, out f)) { side = f.side; rot = f.rot; }
        }

        static GameObject FindModel(string filter, string keyword)
        {
            GameObject best = null;
            int bestScore = int.MinValue;
            foreach (var guid in AssetDatabase.FindAssets(filter))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith("Assets/FishPond/", StringComparison.Ordinal)) continue;
                var file = Path.GetFileNameWithoutExtension(path);
                if (file.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0) continue;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null || go.GetComponentInChildren<Renderer>(true) == null) continue;
                int score = 0;
                string lower = file.ToLowerInvariant();
                if (lower.Contains("swim")) score += 20;                                        // 헤엄 동작 우선
                if (lower.Contains("withcontroller")) score += 8;
                foreach (var bad in new[] { "attack", "turn", "eat", "idle", "dead", "bone", "rig", "skeleton", "collider",
                                            "transparent", "demo", "boids", "pold", "old" })
                    if (lower.Contains(bad)) score -= 15;
                if (go.GetComponentInChildren<SkinnedMeshRenderer>(true) != null) score += 10;   // 움직이는 모델 우선
                if (go.GetComponentInChildren<Animator>(true) != null) score += 5;
                if (path.IndexOf("demo", StringComparison.OrdinalIgnoreCase) >= 0) score -= 5;
                if (path.IndexOf("LOD", StringComparison.OrdinalIgnoreCase) >= 0) score -= 2;
                score -= file.Length / 8;                                                      // 짧은 이름 우선
                if (score > bestScore) { bestScore = score; best = go; }
            }
            return best;
        }

        static RuntimeAnimatorController FindController(string keyword)
        {
            RuntimeAnimatorController best = null;
            int bestScore = int.MinValue;
            foreach (var guid in AssetDatabase.FindAssets("t:AnimatorController"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith("Assets/FishPond/", StringComparison.Ordinal)) continue;
                var file = Path.GetFileNameWithoutExtension(path);
                if (file.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0 && path.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0) continue;
                int score = 0;
                if (file.IndexOf("swim", StringComparison.OrdinalIgnoreCase) >= 0) score += 10;
                if (file.IndexOf("long", StringComparison.OrdinalIgnoreCase) >= 0) score += 2;
                if (file.IndexOf("turn", StringComparison.OrdinalIgnoreCase) >= 0) score -= 5;
                if (score > bestScore)
                {
                    var c = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path);
                    if (c != null) { bestScore = score; best = c; }
                }
            }
            return best;
        }

        [MenuItem("Fish Pond/무료 물고기 에셋 (Asset Store) 열기", priority = 21)]
        static void OpenFreeAssets() { Application.OpenURL(FreeAssetsUrl); }

        [MenuItem("Fish Pond/설정 초기화 (PlayerPrefs)", priority = 40)]
        static void ResetSettings()
        {
            PlayerPrefs.DeleteKey("FishPond.Settings.v2");
            PlayerPrefs.DeleteKey("SharkRescue.Settings.v1");
            Debug.Log("[FishPond] 저장된 운영 설정을 지웠습니다.");
        }
    }
}
