using System;
using System.IO;
using System.Text;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LifeSimProto.EditorTools
{
    // QR の取り込みと確認だけ。明示した合図がある時だけ動く。
    [InitializeOnLoad]
    public static class QRImportReview
    {
        static string Logs => Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs"));
        static double next;
        static QRImportReview() { EditorApplication.update += Tick; }
        static void Tick()
        {
            if (EditorApplication.timeSinceStartup < next || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            next = EditorApplication.timeSinceStartup + 1;
            string p = Path.Combine(Logs, "qr_review_cmd.txt");
            if (!File.Exists(p)) return;
            string taskLock = Path.Combine(Logs, "作業中.txt");
            if (!File.Exists(taskLock) || !File.ReadAllText(taskLock).Contains("チャッピー_ティナ・ティア")) return;
            string cmd = File.ReadAllText(p).Trim(); File.Delete(p);
            try {
                if (cmd == "import" && !EditorApplication.isPlaying) Import();
                else if (cmd == "inspect") Inspect();
                else if ((cmd == "capture" || cmd=="faces") && EditorApplication.isPlaying && UnityEngine.Object.FindAnyObjectByType<QRReviewCapture>() == null)
                { QRReviewCapture.FacesOnly=cmd=="faces"; new GameObject("QRReviewCapture").AddComponent<QRReviewCapture>(); }
                else throw new Exception("Command not valid in current state: " + cmd);
                File.WriteAllText(Path.Combine(Logs, "qr_review_result.txt"), DateTime.Now + " " + cmd + " OK");
            } catch (Exception e) { File.WriteAllText(Path.Combine(Logs, "qr_review_result.txt"), e.ToString()); Debug.LogException(e); }
        }
        static string TextureName(string mat, string letter)
        {
            if (mat == "QR_Face_DoubleSided") return "Shared_Face_BaseColor";
            if (mat == "QR_Hair_DoubleSided") return letter + "_Hair_BaseColor";
            if (mat == "QR_Uniform_DoubleSided") return "Shared_Uniform_BaseColor";
            if (mat == "QR_Underwear_DoubleSided") return "Shared_Underwear_BaseColor";
            if (mat == "QR_Shoes") return "Shared_Shoes_BaseColor";
            if (mat == "QR_SharedPalette_DoubleSided") return "Shared_ColorPalette_v1";
            throw new Exception("Unexpected material " + mat);
        }
        static void Import()
        {
            foreach (string l in new[] { "Q", "R" })
            {
                string dir = "Assets/Proto/Resources/Chars/" + l;
                string path = dir + "/Char" + l + ".fbx";
                foreach (string tex in Directory.GetFiles(dir + "/Textures", "*.png")) AssetDatabase.ImportAsset(tex.Replace('\\','/'), ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var mi = (ModelImporter)AssetImporter.GetAtPath(path);
                mi.importNormals = ModelImporterNormals.Import;
                mi.importBlendShapeNormals = ModelImporterNormals.Import;
                var sources = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>().ToArray();
                if (sources.Length != 6 && mi.GetExternalObjectMap().Count == 0) throw new Exception("Expected six materials: " + l);
                if (!AssetDatabase.IsValidFolder(dir + "/Materials")) AssetDatabase.CreateFolder(dir, "Materials");
                foreach (var src in sources)
                {
                    string mp = dir + "/Materials/" + src.name + ".mat";
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(mp);
                    if (mat == null) { mat = new Material(src); AssetDatabase.CreateAsset(mat, mp); }
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/Textures/" + TextureName(src.name, l) + ".png");
                    if (tex == null) throw new Exception("Texture missing " + src.name);
                    if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
                    if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                    if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
                    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0);
                    if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", .25f);
                    EditorUtility.SetDirty(mat);
                    mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), src.name), mat);
                }
                mi.SaveAndReimport();
            }
            AssetDatabase.SaveAssets(); Inspect();
        }
        static void Inspect()
        {
            var b = new StringBuilder("QR Unity import " + DateTime.Now + "\n");
            foreach (string l in new[] { "Q", "R" })
            {
                string p = "Assets/Proto/Resources/Chars/" + l + "/Char" + l + ".fbx";
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (go == null) throw new Exception("Missing model " + p);
                var a = go.GetComponent<Animator>();
                if (a == null || a.avatar == null || !a.avatar.isValid || !a.avatar.isHuman) throw new Exception("Invalid Humanoid " + l);
                var rr = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                b.AppendLine(l + " humanoid=True meshes=" + rr.Length);
                foreach (var r in rr)
                {
                    b.Append(r.name + " shapes=");
                    for (int i=0;i<r.sharedMesh.blendShapeCount;i++) b.Append(r.sharedMesh.GetBlendShapeName(i) + ";");
                    b.AppendLine();
                    foreach (var m in r.sharedMaterials)
                    {
                        var t = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m.mainTexture;
                        if (t == null) throw new Exception("Unbound texture " + l + " " + m.name);
                        string tp = AssetDatabase.GetAssetPath(t);
                        if (!tp.StartsWith("Assets/Proto/Resources/Chars/" + l + "/Textures/")) throw new Exception("Unexpected texture path " + tp);
                        b.AppendLine("  " + m.name + " -> " + tp);
                    }
                }
            }
            File.WriteAllText(Path.Combine(Logs, "qr_import_inspection.txt"), b.ToString());
        }
    }
}
