using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishPond.EditorTools
{
    /// <summary>
    /// 상단 메뉴 "Fish Pond".
    /// 프로젝트를 처음 열면 게임 씬을 자동으로 만들고 엽니다.
    /// </summary>
    [InitializeOnLoad]
    public static class FishPondMenu
    {
        public const string ScenePath = "Assets/FishPond/Scenes/FishPond.unity";
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
            if (!string.IsNullOrEmpty(SceneManager.GetActiveScene().path)) return;
            if (File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath);
            else CreateScene();
        }

        [MenuItem("Fish Pond/게임 씬 열기", priority = 0)]
        static void OpenScene()
        {
            if (!File.Exists(ScenePath)) { CreateScene(); return; }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Fish Pond/게임 씬 다시 만들기", priority = 1)]
        public static void CreateScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("FishPond");
            go.AddComponent<FishPondGame>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!list.Exists(s => s.path == ScenePath))
            {
                list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = list.ToArray();
            }
            Debug.Log("[FishPond] 게임 씬을 만들었습니다: " + ScenePath + "  ▶ 재생 버튼을 누르세요.");
        }

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
        static void OpenFreeAssets()
        {
            Application.OpenURL(FreeAssetsUrl);
        }

        [MenuItem("Fish Pond/설정 초기화 (PlayerPrefs)", priority = 40)]
        static void ResetSettings()
        {
            PlayerPrefs.DeleteKey("FishPond.Settings.v2");
            Debug.Log("[FishPond] 저장된 운영 설정을 지웠습니다.");
        }
    }
}
