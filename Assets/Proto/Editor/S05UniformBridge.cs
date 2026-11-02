#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace LifeSimProto.EditorTools {
[InitializeOnLoad] public static class S05UniformBridge {
 const string Root="Assets/Proto/Editor/S05UniformReview08";
 static string Logs=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Logs"));
 [Serializable] public class Request {public string output;}
 [Serializable] class MatEntry {public string name,texture;public float[] color;}
 [Serializable] class MatMap {public MatEntry[] materials;}
 static double next;
 static S05UniformBridge(){EditorApplication.playModeStateChanged+=State;EditorApplication.update+=Tick;}
 static void Tick(){
  if(EditorApplication.timeSinceStartup<next||EditorApplication.isCompiling||EditorApplication.isUpdating)return;next=EditorApplication.timeSinceStartup+1;
  string file=Path.Combine(Logs,"s05_review_cmd.txt");if(!File.Exists(file))return;var mt=File.GetLastWriteTimeUtc(file);string cmd=File.ReadAllText(file).Trim();if(cmd!="inspect"&&cmd!="capture")return;
  try{Guard();if(File.GetLastWriteTimeUtc(file)!=mt)throw new Exception("Command changed");File.WriteAllText(file,"handled "+cmd+" "+DateTime.Now);
   if(cmd=="inspect"){var lines=EditorSceneManager.GetSceneManagerSetup().Select(x=>x.path+" loaded="+x.isLoaded+" active="+x.isActive);File.WriteAllText(Path.Combine(Logs,"s05_editor_state.txt"),"playing="+EditorApplication.isPlaying+"\n"+string.Join("\n",lines));}
   else StartReview();
  }catch(Exception e){File.WriteAllText(Path.Combine(Logs,"s05_review_error.txt"),e.ToString());Debug.LogException(e);}
 }
 static void Guard(){var p=Path.Combine(Logs,"作業中.txt");if(!File.Exists(p)||!File.ReadAllText(p).Contains("チャッピー_制服")||File.ReadAllText(p).StartsWith("終了"))throw new Exception("S05 work lock is not active");}
 public static void RunBatch(){
  try{Guard();if(!Application.isBatchMode)throw new Exception("RunBatch is only for the explicitly launched review process");if(EditorApplication.isPlaying)throw new Exception("Unexpected active play session");
   StartReview();
  }catch(Exception e){File.WriteAllText(Path.Combine(Logs,"s05_review_error.txt"),e.ToString());Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(2);}
 }
 static void StartReview(){
  Guard();if(EditorApplication.isPlaying)throw new Exception("Stop play before the isolated review");var request=JsonUtility.FromJson<Request>(File.ReadAllText(Path.Combine(Logs,"s05_review_request.json")));Directory.CreateDirectory(request.output);SessionState.SetString("S05Output",request.output);Import();
  string reviewPath=Root+"/S05Review08.unity";
  if(!File.Exists(reviewPath)){var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);EditorSceneManager.SaveScene(scene,reviewPath);EditorSceneManager.CloseScene(scene,true);}
  SessionState.SetString("S05PreviousPlayScene",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(reviewPath);SessionState.SetBool("S05BatchActive",true);EditorApplication.isPlaying=true;
 }
 static void Import(){
  string path=Root+"/UniformLia08.fbx";AssetDatabase.ImportAsset(path);
  var mi=(ModelImporter)AssetImporter.GetAtPath(path);var mt=File.GetLastWriteTimeUtc(path+".meta");mi.animationType=ModelImporterAnimationType.Human;mi.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;mi.importAnimation=false;mi.importBlendShapes=true;mi.importNormals=ModelImporterNormals.Import;mi.importBlendShapeNormals=ModelImporterNormals.Import;mi.optimizeGameObjects=false;mi.isReadable=true;if(File.GetLastWriteTimeUtc(path+".meta")!=mt)throw new Exception("Review import settings changed concurrently");mi.SaveAndReimport();mt=File.GetLastWriteTimeUtc(path+".meta");
  var map=JsonUtility.FromJson<MatMap>(File.ReadAllText(Root+"/material_map.json"));if(!AssetDatabase.IsValidFolder(Root+"/Materials"))AssetDatabase.CreateFolder(Root,"Materials");
  var remaps=new System.Collections.Generic.List<System.Tuple<string,Material>>();
  foreach(var e in map.materials){
   string dest=Root+"/Materials/"+e.name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(dest);if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,dest);}var materialMtime=File.GetLastWriteTimeUtc(dest);
   mat.SetColor("_BaseColor",new Color(e.color[0],e.color[1],e.color[2],e.color[3]));mat.SetFloat("_Metallic",0);mat.SetFloat("_Smoothness",.25f);
   if(!string.IsNullOrEmpty(e.texture)){string tp=Root+"/Textures/"+e.texture;AssetDatabase.ImportAsset(tp);var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(tp);if(tex==null)throw new Exception("Missing review texture "+tp);mat.SetTexture("_BaseMap",tex);}
   if(e.name.Contains("DoubleSided"))mat.SetFloat("_Cull",0);if(File.GetLastWriteTimeUtc(dest)!=materialMtime)throw new Exception("Review material changed concurrently");EditorUtility.SetDirty(mat);AssetDatabase.SaveAssetIfDirty(mat);remaps.Add(System.Tuple.Create(e.name,mat));
  }
  mi=(ModelImporter)AssetImporter.GetAtPath(path);mt=File.GetLastWriteTimeUtc(path+".meta");foreach(var remap in remaps)mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),remap.Item1),remap.Item2);
  if(File.GetLastWriteTimeUtc(path+".meta")!=mt)throw new Exception("Review import settings changed concurrently");mi.SaveAndReimport();var go=AssetDatabase.LoadAssetAtPath<GameObject>(path);var a=go.GetComponent<Animator>();if(a==null||a.avatar==null||!a.avatar.isValid||!a.avatar.isHuman)throw new Exception("Review model Humanoid is invalid");
  var rr=go.GetComponentsInChildren<SkinnedMeshRenderer>(true);if(rr.Length!=10)throw new Exception("Expected body plus nine uniform meshes: "+rr.Length);
  if(rr.Count(x=>x.sharedMesh.GetBlendShapeIndex("Corrective_Skirt_Sit")>=0)!=2)throw new Exception("Expected two seated corrective meshes");
  File.WriteAllText(Path.Combine(SessionState.GetString("S05Output",""),"import.txt"),"Humanoid valid=True\nModel="+path+"\nMeshes="+rr.Length+"\n"+string.Join("\n",rr.Select(x=>x.name+" vertices="+x.sharedMesh.vertexCount+" shapes="+x.sharedMesh.blendShapeCount)));
 }
 static void State(PlayModeStateChange s){
  if(!SessionState.GetBool("S05BatchActive",false))return;
  if(s==PlayModeStateChange.EnteredPlayMode){Guard();S05UniformCapture.Output=SessionState.GetString("S05Output","");new GameObject("S05UniformCapture08").AddComponent<S05UniformCapture>();}
  if(s==PlayModeStateChange.EnteredEditMode){SessionState.SetBool("S05BatchActive",false);EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString("S05PreviousPlayScene",""));if(Application.isBatchMode){bool ok=File.Exists(Path.Combine(SessionState.GetString("S05Output",""),"DONE.txt"));EditorApplication.Exit(ok?0:3);}}
 }
}}
#endif
