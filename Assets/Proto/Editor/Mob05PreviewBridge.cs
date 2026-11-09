#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LifeSimProto.EditorTools
{
    [InitializeOnLoad]
    public static class Mob05PreviewBridge
    {
        const string Model = "Assets/Proto/Resources/Chars/Mob/CharMob.fbx";
        const string Review = "Assets/Proto/Editor/Mob05Review";
        public static string Output => SessionState.GetString("Mob05Output", Path.GetFullPath(Path.Combine(Application.dataPath, "../../../連携/確認/モブ/MOB05_20261109")));
        static double next;
        static Mob05PreviewBridge() { EditorApplication.playModeStateChanged += State; EditorApplication.update += Tick; }
        static void Tick()
        {
            if (EditorApplication.timeSinceStartup < next || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            next = EditorApplication.timeSinceStartup + 1;
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/mob05_command.txt"));
            if (!File.Exists(path)) return;
            var stamp = File.GetLastWriteTimeUtc(path); string cmd = File.ReadAllText(path).Trim();
            if (cmd != "capture") return;
            Guard();
            if (File.GetLastWriteTimeUtc(path) != stamp) throw new Exception("MOB05 command changed");
            File.WriteAllText(path, "handled capture " + DateTime.Now);
            Start(true);
        }
        static void Guard()
        {
            string file = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/作業中.txt"));
            if (!File.Exists(file) || !File.ReadAllText(file).StartsWith("チャット：") || !File.ReadAllText(file).Contains("MOB05"))
                throw new Exception("先にUnityの作業札をMOB05の担当チャットで確保してください");
        }
        [MenuItem("LifeSim/MOB05/確認画面を開く（通常モブは変更しない）")]
        public static void Open() { Start(false); }
        [MenuItem("LifeSim/MOB05/自動検査と撮影（終了後に停止）")]
        public static void CaptureAll() { Start(true); }
        static void Start(bool capture)
        {
            try
            {
                Guard();
                if (EditorApplication.isPlaying) throw new Exception("MOB05は編集モードから開始してください");
                SessionState.SetString("Mob05Output", Path.GetFullPath(Path.Combine(Application.dataPath, "../../../連携/確認/モブ/MOB05_20261109/run_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"))));
                Directory.CreateDirectory(Output);
                Import();
                if (!AssetDatabase.IsValidFolder(Review)) AssetDatabase.CreateFolder("Assets/Proto/Editor", "Mob05Review");
                string path = Review + "/Mob05Review.unity";
                if (!File.Exists(path))
                {
                    var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                    EditorSceneManager.SaveScene(scene, path); EditorSceneManager.CloseScene(scene, true);
                }
                SessionState.SetString("Mob05PreviousScene", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
                SessionState.SetBool("Mob05Active", true); SessionState.SetBool("Mob05Capture", capture);
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                EditorApplication.isPlaying = true;
            }
            catch (Exception e) { Directory.CreateDirectory(Output); File.WriteAllText(Path.Combine(Output, "ERROR.txt"), e.ToString()); Debug.LogException(e); if (Application.isBatchMode) EditorApplication.Exit(2); }
        }
        static void Import()
        {
            AssetDatabase.ImportAsset(Model);
            var mi = (ModelImporter)AssetImporter.GetAtPath(Model);
            var stamp = File.GetLastWriteTimeUtc(Model + ".meta");
            mi.animationType = ModelImporterAnimationType.Human; mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = false; mi.importBlendShapes = true; mi.importNormals = ModelImporterNormals.Import;
            mi.importBlendShapeNormals = ModelImporterNormals.Import; mi.optimizeGameObjects = false; mi.isReadable = true;
            string dir = Path.GetDirectoryName(Model).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(dir + "/Materials")) AssetDatabase.CreateFolder(dir, "Materials");
            foreach (string name in new[] { "Mob_Body", "Mob_Head", "Mob_Hair_Short", "Mob_Hair_Semi", "Mob_Hair_Pony" })
            {
                string dest = dir + "/Materials/" + name + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(dest);
                if (mat == null)
                {
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/Textures/" + name + ".png");
                    if (tex == null) throw new Exception("Missing texture " + name);
                    mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); mat.name = name;
                    mat.SetColor("_BaseColor", Color.white); mat.SetTexture("_BaseMap", tex);
                    mat.SetFloat("_Metallic", 0); mat.SetFloat("_Smoothness", .25f);
                    AssetDatabase.CreateAsset(mat, dest);
                }
                mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), mat);
            }
            if (File.GetLastWriteTimeUtc(Model + ".meta") != stamp) throw new Exception("Import metadata changed concurrently");
            mi.SaveAndReimport();
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(Model); var animator = go.GetComponent<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman) throw new Exception("Invalid Humanoid avatar");
            var meshes = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (meshes.Length != 7) throw new Exception("Expected 7 meshes; got " + meshes.Length);
            File.WriteAllText(Path.Combine(Output, "import.txt"), "Unity=" + Application.unityVersion + "\nHumanoid valid=True\n" + string.Join("\n", meshes.Select(r => r.name + " tris=" + r.sharedMesh.triangles.Length / 3 + " vertices=" + r.sharedMesh.vertexCount + " keys=" + r.sharedMesh.blendShapeCount + " mats=" + string.Join("/", r.sharedMaterials.Select(m => m.name)))));
        }
        static void State(PlayModeStateChange s)
        {
            if (!SessionState.GetBool("Mob05Active", false)) return;
            if (s == PlayModeStateChange.EnteredPlayMode)
            {
                Guard(); Mob05Preview.Output = Output; Mob05Preview.Capture = SessionState.GetBool("Mob05Capture", false);
                new GameObject("MOB05 Preview Only").AddComponent<Mob05Preview>();
            }
            if (s == PlayModeStateChange.EnteredEditMode)
            {
                File.WriteAllText(Path.Combine(Output, "editor_finished.txt"), "Playing=false\n" + DateTime.Now);
                SessionState.SetBool("Mob05Active", false);
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString("Mob05PreviousScene", ""));
                if (Application.isBatchMode) EditorApplication.Exit(File.Exists(Path.Combine(Output, "DONE.txt")) ? 0 : 3);
            }
        }
    }
}
#endif
