using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LifeSimProto.EditorTools
{
    /// <summary>
    /// 談話室の試作シーン（Assets/Proto/Scenes/Lounge.unity）がなければ自動で作って開く。
    /// メニュー「LifeSim」からも作り直し・開き直しができる。
    /// </summary>
    [InitializeOnLoad]
    public static class LoungeSceneSetup
    {
        const string SceneDir = "Assets/Proto/Scenes";
        const string ScenePath = "Assets/Proto/Scenes/Lounge.unity";

        static LoungeSceneSetup()
        {
            EditorApplication.delayCall += EnsureScene;
        }

        static void EnsureScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (File.Exists(ScenePath)) return;
            CreateScene();
        }

        [MenuItem("LifeSim/談話室シーンを開く")]
        static void OpenScene()
        {
            if (!File.Exists(ScenePath)) { CreateScene(); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        [MenuItem("LifeSim/談話室シーンを作り直す")]
        static void CreateScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(SceneDir);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("LoungeWorld");
            go.AddComponent<LoungeWorld>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("[LifeSim] 談話室シーンを作成しました。上の再生ボタン（▶）を押すと試作が始まります。");
        }
    }
}
