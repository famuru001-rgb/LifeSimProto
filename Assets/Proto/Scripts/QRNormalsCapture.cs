#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Playables;
namespace LifeSimProto {
public class QRNormalsCapture:MonoBehaviour {
 [Serializable] public class PoseItem {public string name;public Vector3 position,scale;public Quaternion rotation;}
 [Serializable] public class PoseData {public PoseItem[] items;}
 public static string Variant="before";
 Camera cam; RenderTexture rt; Light sun; Color sunColor,ambient; float intensity,scale;
 readonly List<GameObject> made=new List<GameObject>(); readonly List<Behaviour> frozen=new List<Behaviour>();
 string dir; readonly StringBuilder log=new StringBuilder();
 IEnumerator Start(){
  dir=Path.GetFullPath(Path.Combine(Application.dataPath,"../../仮モデル/Unity確認/QR03_追記6/"+Variant));Directory.CreateDirectory(dir);
  var w=LoungeWorld.Instance; scale=Time.timeScale; Time.timeScale=1;
  if(w!=null){if(w.enabled){frozen.Add(w);w.enabled=false;}foreach(var c in w.characters)if(c.enabled){frozen.Add(c);c.enabled=false;}}
  ambient=RenderSettings.ambientLight;RenderSettings.ambientLight=new Color(.5f,.47f,.42f);
  sun=FindObjectsByType<Light>().First(l=>l.name=="Sun"&&l.type==LightType.Directional);sunColor=sun.color;intensity=sun.intensity;sun.color=new Color(1,.95f,.85f);sun.intensity=1.1f;
  cam=new GameObject("QRNormalsCam").AddComponent<Camera>();made.Add(cam.gameObject);if(w!=null)cam.CopyFrom(w.mainCamera);
  cam.enabled=true;cam.nearClipPlane=.02f;cam.farClipPlane=250;cam.cullingMask=1<<31;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.29f,.31f,.35f);
  rt=new RenderTexture(960,960,24);cam.targetTexture=rt;
  Vector3 stage=new Vector3(100,0,100);var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);made.Add(floor);floor.transform.position=stage-Vector3.up*.015f;floor.transform.localScale=Vector3.one*8;floor.layer=31;Destroy(floor.GetComponent<Collider>());
  var mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.SetColor("_BaseColor",new Color(.35f,.37f,.4f));floor.GetComponent<Renderer>().sharedMaterial=mat;
  log.AppendLine("Variant="+Variant+" game sunny day Sun=1.1 RGB=1,.95,.85 ambient=.5,.47,.42 sunRotation="+sun.transform.eulerAngles+" residents="+string.Join(",",w.characters.Select(c=>c.letter)));
  foreach(var letter in new[]{"Q","R","I","K"}){
   var go=new GameObject("NormalsReview_"+letter);made.Add(go);go.transform.position=stage;go.transform.rotation=Quaternion.Euler(0,180,0);
   var pb=ProtoBody.Create(go,CharLook.Get(letter,false,Color.gray),letter);if(pb==null)throw new Exception("No model "+letter);
   pb.testMode=true;pb.testLetter=letter;pb.testScene="";pb.testMood="";pb.testPose=BodyPose.Stand;pb.testOutfit="Uniform";pb.testFace="";pb.testClip="Idle";
   foreach(var r in go.GetComponentsInChildren<Renderer>(true))r.gameObject.layer=31;
   var springs=go.GetComponentsInChildren<Transform>().Where(t=>t.name.Contains("Spring_")).ToArray();var rots=springs.Select(t=>t.localRotation).ToArray();var scales=springs.Select(t=>t.localScale).ToArray();
   cam.transform.position=stage+new Vector3(0,1,-3);cam.transform.LookAt(stage+Vector3.up*.75f);
   for(int f=0;f<10;f++)yield return null;
   pb.TestSetClipTime(0);var graph=(PlayableGraph)typeof(ProtoBody).GetField("graph",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(pb);graph.Stop();graph.Evaluate(0);pb.enabled=false;
   for(int i=0;i<springs.Length;i++){springs[i].localRotation=rots[i];springs[i].localScale=scales[i];}
   var transforms=go.GetComponentsInChildren<Transform>(true);
   string poseFile=Path.Combine(Directory.GetParent(dir).FullName,"fixed_pose_"+letter+".json");
   if(Variant.StartsWith("before"))File.WriteAllText(poseFile,JsonUtility.ToJson(new PoseData{items=transforms.Select(t=>new PoseItem{name=t.name,position=t.localPosition,rotation=t.localRotation,scale=t.localScale}).ToArray()},true));
   var pose=JsonUtility.FromJson<PoseData>(File.ReadAllText(poseFile));
   graph.Destroy();foreach(var animator in go.GetComponentsInChildren<Animator>(true))animator.enabled=false;
   foreach(var item in pose.items){var t=transforms.First(x=>x.name==item.name);t.localPosition=item.position;t.localRotation=item.rotation;t.localScale=item.scale;}
   foreach(var m in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))m.updateWhenOffscreen=true;
   yield return null;yield return new WaitForEndOfFrame();
   string[] states=letter=="Q"||letter=="R"?new[]{"Basis","Blink","Smile","MouthOpen","Sad","Angry","Surprised","Blink_L","Blink_R","Surprised_MouthOpen"}:new[]{"Basis"};
   foreach(var key in states){
    foreach(var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))for(int i=0;i<r.sharedMesh.blendShapeCount;i++){var n=r.sharedMesh.GetBlendShapeName(i);r.SetBlendShapeWeight(i,(n==key||(key=="Surprised_MouthOpen"&&(n=="Surprised"||n=="MouthOpen")))?100:0);}
    yield return null;yield return new WaitForEndOfFrame();
    Vector3 at=pb.HeadPoint-Vector3.up*.035f;
    foreach(float yaw in key=="Basis"?new[]{0f,45f,90f}:new[]{0f,45f}){
     Vector3 from=at+Quaternion.AngleAxis(yaw,Vector3.up)*go.transform.forward*.6f;
     Snap(from,at,letter+"_"+key+"_"+yaw+".png",35);log.AppendLine(letter+" "+key+" "+yaw+" camera="+from.ToString("F6")+" target="+at.ToString("F6"));
    }
    if(key=="Basis"&&(letter=="Q"||letter=="R")){
     Vector3 c=stage+Vector3.up*.75f;Snap(c+go.transform.forward*2.8f+Vector3.up*.22f,c,letter+"_full_near.png",35);
     Vector3 v=go.transform.forward*Mathf.Cos(48*Mathf.Deg2Rad)+Vector3.up*Mathf.Sin(48*Mathf.Deg2Rad);
     foreach(float dist in new[]{4f,24f})Snap(c+v*dist,c,letter+"_full_game_"+dist+".png",50);
    }
   }
   Destroy(go);yield return null;
  }
  File.WriteAllText(Path.Combine(dir,"capture.txt"),log.ToString());File.WriteAllText(Path.Combine(dir,"DONE.txt"),DateTime.Now.ToString());
  foreach(var go in made)if(go!=null)Destroy(go);rt.Release();Destroy(rt);Destroy(mat);Destroy(gameObject);
 }
 void Snap(Vector3 from,Vector3 at,string name,float fov){cam.fieldOfView=fov;cam.transform.position=from;cam.transform.LookAt(at);cam.Render();var old=RenderTexture.active;RenderTexture.active=rt;var im=new Texture2D(960,960,TextureFormat.RGB24,false);im.ReadPixels(new Rect(0,0,960,960),0,0);im.Apply();RenderTexture.active=old;File.WriteAllBytes(Path.Combine(dir,name),im.EncodeToPNG());Destroy(im);}
 void OnDestroy(){foreach(var b in frozen)if(b!=null)b.enabled=true;Time.timeScale=scale;RenderSettings.ambientLight=ambient;if(sun!=null){sun.color=sunColor;sun.intensity=intensity;}}
}}
#endif
