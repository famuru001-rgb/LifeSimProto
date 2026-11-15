using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LifeSimProto.EditorTools
{
    [InitializeOnLoad]
    public static class NinaTrialCaptureTrigger
    {
        static double next;
        static readonly string Logs=Path.GetFullPath(Path.Combine(Application.dataPath,"../Logs"));
        static NinaTrialCaptureTrigger() { EditorApplication.update+=Tick; }
        static void Tick()
        {
            if(EditorApplication.timeSinceStartup<next || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            next=EditorApplication.timeSinceStartup+1;
            string req=Path.Combine(Logs,"nina_trial_request.txt");
            if(!File.Exists(req)) return;
            string command=File.ReadAllText(req).Trim(); File.Delete(req);
            var result=new StringBuilder(DateTime.Now.ToString("s")+"\n");
            try
            {
                const string path="Assets/Proto/Resources/Chars/H/CharH.fbx";
                var importer=AssetImporter.GetAtPath(path) as ModelImporter;
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                result.AppendLine("animationType="+importer.animationType+"; scale="+importer.globalScale);
                var anim=prefab.GetComponentInChildren<Animator>(true);
                result.AppendLine("avatarValid="+(anim!=null&&anim.avatar!=null&&anim.avatar.isValid)+"; human="+(anim!=null&&anim.avatar!=null&&anim.avatar.isHuman));
                int tris=0;
                foreach(var r in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    tris+=r.sharedMesh.triangles.Length/3;
                    result.AppendLine(r.name+": "+r.sharedMesh.triangles.Length/3+" triangles; "+r.sharedMesh.blendShapeCount+" keys");
                    foreach(var m in r.sharedMaterials.Where(m=>m!=null))
                        result.AppendLine("  material="+m.name+"; shader="+m.shader.name+"; texture="+(m.mainTexture==null?"none":AssetDatabase.GetAssetPath(m.mainTexture)));
                }
                result.AppendLine("triangles="+tris);
                if(command=="capture" || command=="details")
                {
                    if(!EditorApplication.isPlaying) throw new Exception("Play mode required");
                    if(UnityEngine.Object.FindAnyObjectByType<LifeSimProto.NinaTrialCapture>()!=null) throw new Exception("Capture already running");
                    new GameObject("NinaTrialCapture").AddComponent<LifeSimProto.NinaTrialCapture>().detailsOnly = command=="details";
                    result.AppendLine("capture started");
                }
            }
            catch(Exception ex) { result.AppendLine(ex.ToString()); }
            File.WriteAllText(Path.Combine(Logs,"nina_trial_result.txt"),result.ToString(),new UTF8Encoding(false));
        }
    }
}
