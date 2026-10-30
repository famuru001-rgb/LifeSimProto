#if UNITY_EDITOR
using System;using System.Collections;using System.Collections.Generic;using System.IO;using System.Linq;using System.Reflection;using System.Text;using UnityEngine;using UnityEngine.Playables;
namespace LifeSimProto {
public class QRBalanceCapture:MonoBehaviour {
 [Serializable]public class Item{public string name;public Vector3 position,scale;public Quaternion rotation;}
 [Serializable]public class Pose{public Item[] items;}
 [Serializable]public class MeshDump{public string name;public Vector3[] rest,posed,posedHalf,posedFull,delta,c0,c1,c2,translation;public int[] triangles;public float[] frameWeights;public string[] bones;public Matrix4x4[] skin;}
 [Serializable]public class Dump{public MeshDump[] meshes;}
 public static string Variant="prior7";bool Dense=>Variant.StartsWith("dense");bool Baseline=>Variant=="prior7"||Variant=="dense7";Camera cam;RenderTexture rt;Light sun;Color oldSun,oldAmbient;float oldIntensity,oldScale;string dir;List<GameObject> made=new List<GameObject>();List<Behaviour> frozen=new List<Behaviour>();StringBuilder log=new StringBuilder();Vector3 stage=new Vector3(100,0,100);
 IEnumerator Start(){
  dir=Path.GetFullPath(Path.Combine(Application.dataPath,"../../仮モデル/Unity確認/QR03_追記8/"+Variant));Directory.CreateDirectory(dir);var w=LoungeWorld.Instance;oldScale=Time.timeScale;Time.timeScale=1;
  if(w!=null){if(w.enabled){w.enabled=false;frozen.Add(w);}foreach(var c in w.characters)if(c.enabled){c.enabled=false;frozen.Add(c);}}
  oldAmbient=RenderSettings.ambientLight;RenderSettings.ambientLight=new Color(.5f,.47f,.42f);sun=FindObjectsByType<Light>().First(l=>l.name=="Sun"&&l.type==LightType.Directional);oldSun=sun.color;oldIntensity=sun.intensity;sun.color=new Color(1,.95f,.85f);sun.intensity=1.1f;
  cam=new GameObject("QRBalanceCamera").AddComponent<Camera>();made.Add(cam.gameObject);cam.CopyFrom(w.mainCamera);cam.enabled=true;cam.nearClipPlane=.02f;cam.farClipPlane=250;cam.cullingMask=1<<31;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.29f,.31f,.35f);rt=new RenderTexture(800,800,24);cam.targetTexture=rt;
  var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);made.Add(floor);floor.transform.position=stage-Vector3.up*.015f;floor.transform.localScale=Vector3.one*8;floor.layer=31;Destroy(floor.GetComponent<Collider>());var mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.SetColor("_BaseColor",new Color(.35f,.37f,.4f));floor.GetComponent<Renderer>().sharedMaterial=mat;
  foreach(string letter in ((Dense||Variant=="weights0"||Variant.StartsWith("probe"))?new[]{"Q"}:new[]{"Q","R"}))foreach(string clip in new[]{"Sit","SitTalk","Joy","Idle","Walk","Wave","Talk","SkirtOut","SkirtIn"}){
   float[] times=clip=="Sit"?new[]{1f,3f,6f}:clip=="SitTalk"?new[]{0.1f,8.8f,17.6f,26.4f,35.2f,43.8f}:clip=="Joy"?new[]{.85f}:clip=="Walk"?new[]{.25f,.65f}:clip=="Wave"?new[]{.4f,1f,1.6f}:clip=="Talk"?new[]{.4f,1.2f,2f}:new[]{0f};
   if(Dense&&!clip.StartsWith("Skirt")){float len=clip=="Idle"?3.3333f:clip=="Walk"?.9f:clip=="Wave"?.5667f:clip=="Joy"?1.9f:clip=="Talk"?5.9667f:clip=="Sit"?6.4333f:44.1f;times=Enumerable.Range(0,16).Select(i=>(i+.25f)/16f*len).ToArray();}
   for(int sample=0;sample<times.Length;sample++){
    string id=letter+"_"+clip+"_"+sample;var go=new GameObject("BalanceReview_"+letter);made.Add(go);go.transform.position=stage;go.transform.rotation=Quaternion.Euler(0,180,0);cam.transform.position=stage+new Vector3(0,1,-3);cam.transform.LookAt(stage+Vector3.up*.75f);
    string asset=Baseline?"Assets/Proto/Editor/QRHemReviewCopies/Pair13/"+letter+"/Char"+letter+".fbx":"Assets/Proto/Editor/QRBalanceReviewCopies/"+(Dense?(Variant=="dense8"?"fit14_review":"fit"+Variant.Substring(5)+"_review"):Variant)+"/"+letter+"/Char"+letter+".fbx";
    var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(asset);if(prefab==null)throw new Exception("Missing candidate "+asset);var inst=Instantiate(prefab,go.transform,false);inst.name="Model";var pb=go.AddComponent<ProtoBody>();typeof(ProtoBody).GetField("real",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(pb,true);
    if(!(bool)typeof(ProtoBody).GetMethod("Setup",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(pb,new object[]{inst.transform,CharLook.Get(letter,false,Color.gray)}))throw new Exception("Setup failed");
    var skirtRest=go.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Spring_Skirt")).ToDictionary(t=>t.name,t=>new Item{name=t.name,position=t.localPosition,rotation=t.localRotation,scale=t.localScale});
    pb.testMode=true;pb.testLetter=letter;pb.testScene="";pb.testMood="";pb.testOutfit="Uniform";pb.testFace="";pb.testClip=clip.StartsWith("Skirt")?"Idle":clip;pb.testPose=clip.StartsWith("Sit")?BodyPose.Sit:BodyPose.Stand;pb.testSkirtSit=0;pb.testTalk=false;
    log.AppendLine(id+" avatar="+inst.GetComponent<Animator>().avatar.isHuman+" spring_count="+pb.SpringCount);
    foreach(var r in go.GetComponentsInChildren<Renderer>(true))r.gameObject.layer=31;
    for(int f=0;f<(Dense?2:18);f++)yield return null;
    pb.TestSetClipTime(times[sample]);var graph=(PlayableGraph)typeof(ProtoBody).GetField("graph",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(pb);graph.Stop();graph.Evaluate(0);pb.enabled=false;
    var transforms=go.GetComponentsInChildren<Transform>(true);string pf=Path.Combine(Directory.GetParent(dir).FullName,(Dense?"dense_pose_":"pose_")+id+".json");
    graph.Destroy();foreach(var a in go.GetComponentsInChildren<Animator>(true))a.enabled=false;
    if(Baseline){
      string old=Path.GetFullPath(Path.Combine(Application.dataPath,"../../仮モデル/Unity確認/QR03_追記7/pose_"+id+".json"));
      if(!Dense&&File.Exists(old)&&!clip.StartsWith("Skirt")){var oldPose=JsonUtility.FromJson<Pose>(File.ReadAllText(old));foreach(var item in oldPose.items){if(item.name.StartsWith("Spring_Skirt"))continue;var t=transforms.FirstOrDefault(x=>x.name==item.name);if(t!=null){t.localPosition=item.position;t.localRotation=item.rotation;t.localScale=item.scale;}}}
      File.WriteAllText(pf,JsonUtility.ToJson(new Pose{items=transforms.Select(t=>new Item{name=t.name,position=t.localPosition,rotation=t.localRotation,scale=t.localScale}).ToArray()}));
    }
    var pose=JsonUtility.FromJson<Pose>(File.ReadAllText(pf));foreach(var item in pose.items){if(item.name.StartsWith("Spring_Skirt"))continue;var t=transforms.FirstOrDefault(x=>x.name==item.name);if(t!=null){t.localPosition=item.position;t.localRotation=item.rotation;t.localScale=item.scale;}}
    foreach(var item in skirtRest.Values){var t=transforms.First(x=>x.name==item.name);t.localPosition=item.position;t.localRotation=item.rotation;t.localScale=item.scale;}
    if(clip.StartsWith("Skirt")){var hips=transforms.First(t=>t.name=="mixamorig:Hips");foreach(var t in transforms.Where(t=>t.name.StartsWith("Spring_Skirt")&&t.name.EndsWith("_1"))){var c=t.GetChild(0);var down=(c.position-t.position).normalized;var radial=t.position-hips.position;radial.y=0;var axis=Vector3.Cross(down,radial.normalized).normalized;t.rotation=Quaternion.AngleAxis(clip=="SkirtOut"?35:-10,axis)*t.rotation;}}
    foreach(var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)){r.updateWhenOffscreen=true;for(int k=0;k<r.sharedMesh.blendShapeCount;k++)r.SetBlendShapeWeight(k,0);}
    yield return null;yield return new WaitForEndOfFrame();
    if(letter=="Q")SaveMesh(go,id);
    foreach(float value in clip.StartsWith("Sit")?new[]{0f,50f,100f}:new[]{0f}){
     foreach(var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)){int k=r.sharedMesh.GetBlendShapeIndex("Corrective_Skirt_Sit");if(k>=0)r.SetBlendShapeWeight(k,value);}
     yield return null;yield return new WaitForEndOfFrame();
     Vector3 hip=transforms.First(t=>t.name.EndsWith(":Hips")||t.name=="Hips").position,center=hip+Vector3.up*.06f;string stem=id+"_k"+value;
     foreach(float yaw in clip.StartsWith("Sit")?new[]{0f,90f,180f,135f}:new[]{0f,90f,180f}){
      if(Dense&&yaw!=90&&sample%4!=0&&sample!=15)continue;var direction=Quaternion.AngleAxis(yaw,Vector3.up)*go.transform.forward;bool low=yaw==135;Snap(center+direction*.92f+Vector3.up*(low?-.25f:.18f),center,stem+"_near_"+yaw+".png",35);
      if(!low&&(!Dense||sample%4==0||sample==15)){var ray=direction*Mathf.Cos(48*Mathf.Deg2Rad)+Vector3.up*Mathf.Sin(48*Mathf.Deg2Rad);Snap(hip+ray*4f,hip,stem+"_game_"+yaw+".png",50);}
     }
    }Destroy(go);yield return null;
   }
  }
  File.WriteAllText(Path.Combine(dir,"capture.txt"),log.ToString());File.WriteAllText(Path.Combine(dir,"DONE.txt"),DateTime.Now.ToString());foreach(var o in made)if(o!=null)Destroy(o);rt.Release();Destroy(rt);Destroy(mat);Destroy(gameObject);
 }
 void SaveMesh(GameObject go,string id){var data=new List<MeshDump>();foreach(var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)){
  if(!new[]{"Outfit_Uniform_Jacket","Outfit_Uniform_Skirt","Body_QR","Outfit_Uniform_Panties"}.Contains(r.name))continue;
  var m=r.sharedMesh;var bake=new Mesh();r.BakeMesh(bake);var p=bake.vertices.Select(v=>go.transform.InverseTransformPoint(r.transform.TransformPoint(v))).ToArray();Destroy(bake);
  int n=m.vertexCount;var d=new MeshDump{name=r.name,rest=m.vertices,posed=p,delta=new Vector3[n],triangles=m.triangles};int k=m.GetBlendShapeIndex("Corrective_Skirt_Sit");if(k>=0){m.GetBlendShapeFrameVertices(k,0,d.delta,null,null);d.frameWeights=Enumerable.Range(0,m.GetBlendShapeFrameCount(k)).Select(f=>m.GetBlendShapeFrameWeight(k,f)).ToArray();r.SetBlendShapeWeight(k,50);var half=new Mesh();r.BakeMesh(half);d.posedHalf=half.vertices.Select(v=>go.transform.InverseTransformPoint(r.transform.TransformPoint(v))).ToArray();Destroy(half);r.SetBlendShapeWeight(k,100);var full=new Mesh();r.BakeMesh(full);d.posedFull=full.vertices.Select(v=>go.transform.InverseTransformPoint(r.transform.TransformPoint(v))).ToArray();Destroy(full);r.SetBlendShapeWeight(k,0);}else d.posedFull=d.posed;
  if(r.name=="Outfit_Uniform_Jacket"||r.name=="Outfit_Uniform_Skirt"){
   d.c0=new Vector3[n];d.c1=new Vector3[n];d.c2=new Vector3[n];d.translation=new Vector3[n];var b=r.bones;var bind=m.bindposes;var weights=m.boneWeights;d.bones=b.Select(x=>x.name).ToArray();d.skin=b.Select((x,i)=>go.transform.worldToLocalMatrix*x.localToWorldMatrix*bind[i]).ToArray();
   for(int i=0;i<n;i++){var z=weights[i];Matrix4x4 a=go.transform.worldToLocalMatrix*b[z.boneIndex0].localToWorldMatrix*bind[z.boneIndex0],bb=go.transform.worldToLocalMatrix*b[z.boneIndex1].localToWorldMatrix*bind[z.boneIndex1],c=go.transform.worldToLocalMatrix*b[z.boneIndex2].localToWorldMatrix*bind[z.boneIndex2],dd=go.transform.worldToLocalMatrix*b[z.boneIndex3].localToWorldMatrix*bind[z.boneIndex3];Matrix4x4 v=new Matrix4x4();for(int t=0;t<16;t++)v[t]=a[t]*z.weight0+bb[t]*z.weight1+c[t]*z.weight2+dd[t]*z.weight3;d.c0[i]=v.GetColumn(0);d.c1[i]=v.GetColumn(1);d.c2[i]=v.GetColumn(2);d.translation[i]=v.GetColumn(3);}
  }data.Add(d);
 }File.WriteAllText(Path.Combine(dir,id+"_mesh.json"),JsonUtility.ToJson(new Dump{meshes=data.ToArray()}));}
 void Snap(Vector3 from,Vector3 at,string name,float fov){cam.fieldOfView=fov;cam.transform.position=from;cam.transform.LookAt(at);cam.Render();var old=RenderTexture.active;RenderTexture.active=rt;var im=new Texture2D(800,800,TextureFormat.RGB24,false);im.ReadPixels(new Rect(0,0,800,800),0,0);im.Apply();RenderTexture.active=old;File.WriteAllBytes(Path.Combine(dir,name),im.EncodeToPNG());Destroy(im);log.AppendLine(name+" camera="+from.ToString("F6")+" at="+at.ToString("F6"));}
 void OnDestroy(){foreach(var b in frozen)if(b!=null)b.enabled=true;Time.timeScale=oldScale;RenderSettings.ambientLight=oldAmbient;if(sun!=null){sun.color=oldSun;sun.intensity=oldIntensity;}}
}}
#endif
