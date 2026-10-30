using System;using System.IO;using System.Linq;using System.Text;using UnityEditor;using UnityEngine;
namespace LifeSimProto.EditorTools {
[InitializeOnLoad]public static class QRHemBridge {
 static string Logs=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Logs"));static double next;
 static QRHemBridge(){EditorApplication.update+=Tick;}
 static void Tick(){
  if(EditorApplication.timeSinceStartup<next||EditorApplication.isCompiling||EditorApplication.isUpdating)return;next=EditorApplication.timeSinceStartup+1;
  string p=Path.Combine(Logs,"qr_hem_cmd.txt");if(!File.Exists(p))return;
  string lk=Path.Combine(Logs,"作業中.txt");if(!File.Exists(lk)||!File.ReadAllText(lk).Contains("チャッピー_ティナ・ティア"))return;
  string cmd=File.ReadAllText(p).Trim();File.Delete(p);
  try{
   if((cmd=="before"||cmd=="after"||cmd=="trial"||cmd=="before_fixed"||cmd=="after_fixed"||cmd.StartsWith("pair")||cmd=="baseline")&&EditorApplication.isPlaying){QRHemCapture.Variant=cmd;new GameObject("QRHemCapture").AddComponent<QRHemCapture>();}
   else if(cmd=="inspect"||cmd=="reimport"){
    var s=new StringBuilder();foreach(var l in new[]{"Q","R"}){
     string path="Assets/Proto/Resources/Chars/"+l+"/Char"+l+".fbx";var mi=(ModelImporter)AssetImporter.GetAtPath(path);
     if(cmd=="reimport"){mi.importNormals=ModelImporterNormals.Import;mi.importBlendShapeNormals=ModelImporterNormals.Import;mi.SaveAndReimport();}
     var o=AssetDatabase.LoadAssetAtPath<GameObject>(path);var a=o.GetComponent<Animator>();s.AppendLine(l+" Normals="+mi.importNormals+" BlendShapeNormals="+mi.importBlendShapeNormals+" humanoid="+(a.avatar.isValid&&a.avatar.isHuman));
     foreach(var r in o.GetComponentsInChildren<SkinnedMeshRenderer>(true)){s.AppendLine(r.name+" vertices="+r.sharedMesh.vertexCount+" shapes="+r.sharedMesh.blendShapeCount);foreach(var m in r.sharedMaterials){var t=m.HasProperty("_BaseMap")?m.GetTexture("_BaseMap"):m.mainTexture;if(t==null)throw new Exception("Missing texture "+m.name);}}
    }File.WriteAllText(Path.Combine(Logs,"qr_hem_import.txt"),s.ToString());
   }else throw new Exception("Invalid state "+cmd);
   File.WriteAllText(Path.Combine(Logs,"qr_hem_result.txt"),DateTime.Now+" "+cmd+" OK");
  }catch(Exception e){File.WriteAllText(Path.Combine(Logs,"qr_hem_result.txt"),e.ToString());Debug.LogException(e);}
 }
}}
