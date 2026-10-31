using System.Collections.Generic;
using System.IO;
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
                return ok;
            }
            finally
            {
                PlayerSettings.productName = oldName;
                PlayerSettings.fullScreenMode = oldMode;
            }
        }

        // ---------------------------------------------------------------- 기타
        [MenuItem("Fish Pond/생물 설정 에셋 만들기 (에셋 교체용)", priority = 20)]
        static void CreateCatalog()
        {
            var existing = AssetDatabase.LoadAssetAtPath<SpeciesCatalog>(CatalogPath);
            if (existing == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath));
                existing = ScriptableObject.CreateInstance<SpeciesCatalog>();
                AssetDatabase.CreateAsset(existing, CatalogPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[FishPond] 생물 설정 에셋을 만들었습니다. 각 생물의 Prefab Override에 모델을 끌어다 놓으세요.");
            }
            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
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
