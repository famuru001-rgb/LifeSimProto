#if UNITY_EDITOR
using System;using System.Collections;using System.Collections.Generic;using System.IO;using System.Linq;using System.Reflection;using System.Text;using UnityEngine;using UnityEngine.Playables;
namespace LifeSimProto {
public class QRHemCapture:MonoBehaviour {
 [Serializable]public class Item{public string name;public Vector3 position,scale;public Quaternion rotation;}
 [Serializable]public class Pose{public Item[] items;}
 [Serializable]public class MeshDump{public string name;public Vector3[] rest,posed,posedFull,delta,c0,c1,c2,translation;public int[] triangles;}
 [Serializable]public class Dump{public MeshDump[] meshes;}
 public static string Variant="before";Camera cam;RenderTexture rt;Light sun;Color oldSun,oldAmbient;float oldIntensity,oldScale;string dir;List<GameObject> made=new List<GameObject>();List<Behaviour> frozen=new List<Behaviour>();StringBuilder log=new StringBuilder();Vector3 stage=new Vector3(100,0,100);
 IEnumerator Start(){
  dir=Path.GetFullPath(Path.Combine(Application.dataPath,"../../仮モデル/Unity確認/QR03_追記7/"+Variant));Directory.CreateDirectory(dir);var w=LoungeWorld.Instance;oldScale=Time.timeScale;Time.timeScale=1;
  if(w!=null){if(w.enabled){w.enabled=false;frozen.Add(w);}foreach(var c in w.characters)if(c.enabled){c.enabled=false;frozen.Add(c);}}
  oldAmbient=RenderSettings.ambientLight;RenderSettings.ambientLight=new Color(.5f,.47f,.42f);sun=FindObjectsByType<Light>().First(l=>l.name=="Sun"&&l.type==LightType.Directional);oldSun=sun.color;oldIntensity=sun.intensity;sun.color=new Color(1,.95f,.85f);sun.intensity=1.1f;
  cam=new GameObject("QRHemCamera").AddComponent<Camera>();made.Add(cam.gameObject);cam.CopyFrom(w.mainCamera);cam.enabled=true;cam.nearClipPlane=.02f;cam.farClipPlane=250;cam.cullingMask=1<<31;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.29f,.31f,.35f);rt=new RenderTexture(800,800,24);cam.targetTexture=rt;
  var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);made.Add(floor);floor.transform.position=stage-Vector3.up*.015f;floor.transform.localScale=Vector3.one*8;floor.layer=31;Destroy(floor.GetComponent<Collider>());var mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.SetColor("_BaseColor",new Color(.35f,.37f,.4f));floor.GetComponent<Renderer>().sharedMaterial=mat;
  foreach(string letter in new[]{"Q","R"})foreach(string clip in new[]{"Sit","SitTalk","Joy","Idle","Walk"}){
   float[] times=clip=="Sit"?new[]{1f,3f,6f}:clip=="SitTalk"?new[]{0.1f,8.8f,17.6f,26.4f,35.2f,43.8f}:clip=="Joy"?new[]{.85f}:clip=="Walk"?new[]{.25f,.65f}:new[]{0f};
   for(int sample=0;sample<times.Length;sample++){
    string id=letter+"_"+clip+"_"+sample;var go=new GameObject("HemReview_"+letter);made.Add(go);go.transform.position=stage;go.transform.rotation=Quaternion.Euler(0,180,0);cam.transform.position=stage+new Vector3(0,1,-3);cam.transform.LookAt(stage+Vector3.up*.75f);
    var pb=ProtoBody.Create(go,CharLook.Get(letter,false,Color.gray),letter);pb.testMode=true;pb.testLetter=letter;pb.testScene="";pb.testMood="";pb.testOutfit="Uniform";pb.testFace="";pb.testClip=clip;pb.testPose=clip.StartsWith("Sit")?BodyPose.Sit:BodyPose.Stand;pb.testSkirtSit=0;pb.testTalk=false;
    if(Variant=="after"||Variant.StartsWith("pair")){
     var candidate=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Proto/Editor/QRHemReviewCopies/"+(Variant.StartsWith("pair")?"Pair7/":"")+letter+"/Char"+letter+".fbx");
     foreach(var src in candidate.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.name=="Outfit_Uniform_Jacket"||r.name=="Outfit_Uniform_Skirt")){var dst=go.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r=>r.name==src.name);
     if(!src.bones.Select(b=>b.name).SequenceEqual(dst.bones.Select(b=>b.name)))throw new Exception("Jacket bone order differs");
     if(src.sharedMesh.GetBlendShapeIndex("Corrective_Skirt_Sit")!=dst.sharedMesh.GetBlendShapeIndex("Corrective_Skirt_Sit"))throw new Exception("Key order differs");
     dst.sharedMesh=src.sharedMesh;}
    }
    foreach(var r in go.GetComponentsInChildren<Renderer>(true))r.gameObject.layer=31;
    for(int f=0;f<18;f++)yield return null;
    pb.TestSetClipTime(times[sample]);var graph=(PlayableGraph)typeof(ProtoBody).GetField("graph",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(pb);graph.Stop();graph.Evaluate(0);pb.enabled=false;
    var transforms=go.GetComponentsInChildren<Transform>(true);string pf=Path.Combine(Directory.GetParent(dir).FullName,"pose_"+id+".json");
    if(Variant=="before")File.WriteAllText(pf,JsonUtility.ToJson(new Pose{items=transforms.Select(t=>new Item{name=t.name,position=t.localPosition,rotation=t.localRotation,scale=t.localScale}).ToArray()}));
    var pose=JsonUtility.FromJson<Pose>(File.ReadAllText(pf));graph.Destroy();foreach(var a in go.GetComponentsInChildren<Animator>(true))a.enabled=false;foreach(var item in pose.items){var t=transforms.First(x=>x.name==item.name);t.localPosition=item.position;t.localRotation=item.rotation;t.localScale=item.scale;}
    foreach(var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)){r.updateWhenOffscreen=true;for(int k=0;k<r.sharedMesh.blendShapeCount;k++)r.SetBlendShapeWeight(k,0);}
    yield return null;yield return new WaitForEndOfFrame();
    if(letter=="Q")SaveMesh(go,id);
    foreach(float value in clip.StartsWith("Sit")?new[]{0f,50f,100f}:new[]{0f}){
     foreach(var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)){int k=r.sharedMesh.GetBlendShapeIndex("Corrective_Skirt_Sit");if(k>=0)r.SetBlendShapeWeight(k,value);}
     yield return null;yield return new WaitForEndOfFrame();
     Vector3 hip=transforms.First(t=>t.name.EndsWith(":Hips")||t.name=="Hips").position,center=hip+Vector3.up*.06f;string stem=id+"_k"+value;
     foreach(float yaw in clip.StartsWith("Sit")?new[]{90f,180f,135f}:new[]{0f,90f}){
      var direction=Quaternion.AngleAxis(yaw,Vector3.up)*go.transform.forward;bool low=yaw==135;Snap(center+direction*.92f+Vector3.up*(low?-.25f:.18f),center,stem+"_near_"+yaw+".png",35);
      if(!low){var ray=direction*Mathf.Cos(48*Mathf.Deg2Rad)+Vector3.up*Mathf.Sin(48*Mathf.Deg2Rad);Snap(hip+ray*4f,hip,stem+"_game_"+yaw+".png",50);}
     }
    }Destroy(go);yield return null;
   }
  }
  File.WriteAllText(Path.Combine(dir,"capture.txt"),log.ToString());File.WriteAllText(Path.Combine(dir,"DONE.txt"),DateTime.Now.ToString());foreach(var o in made)if(o!=null)Destroy(o);rt.Release();Destroy(rt);Destroy(mat);Destroy(gameObject);
 }
 void SaveMesh(GameObject go,string id){var data=new List<MeshDump>();foreach(var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)){
  if(!new[]{"Outfit_Uniform_Jacket","Outfit_Uniform_Skirt","Body_QR","Outfit_Uniform_Panties"}.Contains(r.name))continue;
  var m=r.sharedMesh;var bake=new Mesh();r.BakeMesh(bake);var p=bake.vertices.Select(v=>go.transform.InverseTransformPoint(r.transform.TransformPoint(v))).ToArray();Destroy(bake);
  int n=m.vertexCount;var d=new MeshDump{name=r.name,rest=m.vertices,posed=p,delta=new Vector3[n],triangles=m.triangles};int k=m.GetBlendShapeIndex("Corrective_Skirt_Sit");if(k>=0){m.GetBlendShapeFrameVertices(k,0,d.delta,null,null);r.SetBlendShapeWeight(k,100);var full=new Mesh();r.BakeMesh(full);d.posedFull=full.vertices.Select(v=>go.transform.InverseTransformPoint(r.transform.TransformPoint(v))).ToArray();Destroy(full);r.SetBlendShapeWeight(k,0);}else d.posedFull=d.posed;
  if(r.name=="Outfit_Uniform_Jacket"||r.name=="Outfit_Uniform_Skirt"){
   d.c0=new Vector3[n];d.c1=new Vector3[n];d.c2=new Vector3[n];d.translation=new Vector3[n];var b=r.bones;var bind=m.bindposes;var weights=m.boneWeights;
   for(int i=0;i<n;i++){var z=weights[i];Matrix4x4 a=go.transform.worldToLocalMatrix*b[z.boneIndex0].localToWorldMatrix*bind[z.boneIndex0],bb=go.transform.worldToLocalMatrix*b[z.boneIndex1].localToWorldMatrix*bind[z.boneIndex1],c=go.transform.worldToLocalMatrix*b[z.boneIndex2].localToWorldMatrix*bind[z.boneIndex2],dd=go.transform.worldToLocalMatrix*b[z.boneIndex3].localToWorldMatrix*bind[z.boneIndex3];Matrix4x4 v=new Matrix4x4();for(int t=0;t<16;t++)v[t]=a[t]*z.weight0+bb[t]*z.weight1+c[t]*z.weight2+dd[t]*z.weight3;d.c0[i]=v.GetColumn(0);d.c1[i]=v.GetColumn(1);d.c2[i]=v.GetColumn(2);d.translation[i]=v.GetColumn(3);}
  }data.Add(d);
 }File.WriteAllText(Path.Combine(dir,id+"_mesh.json"),JsonUtility.ToJson(new Dump{meshes=data.ToArray()}));}
 void Snap(Vector3 from,Vector3 at,string name,float fov){cam.fieldOfView=fov;cam.transform.position=from;cam.transform.LookAt(at);cam.Render();var old=RenderTexture.active;RenderTexture.active=rt;var im=new Texture2D(800,800,TextureFormat.RGB24,false);im.ReadPixels(new Rect(0,0,800,800),0,0);im.Apply();RenderTexture.active=old;File.WriteAllBytes(Path.Combine(dir,name),im.EncodeToPNG());Destroy(im);log.AppendLine(name+" camera="+from.ToString("F6")+" at="+at.ToString("F6"));}
 void OnDestroy(){foreach(var b in frozen)if(b!=null)b.enabled=true;Time.timeScale=oldScale;RenderSettings.ambientLight=oldAmbient;if(sun!=null){sun.color=oldSun;sun.intensity=oldIntensity;}}
}}
#endif
