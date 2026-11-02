#if UNITY_EDITOR
using System;using System.Collections.Generic;using System.IO;using System.Linq;using System.Reflection;using System.Text;using UnityEngine;using UnityEngine.Playables;
namespace LifeSimProto {
[DefaultExecutionOrder(20000)] public class S05UniformCapture:MonoBehaviour {
 public static string Output;const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;const string Model="Assets/Proto/Editor/S05UniformReview08/UniformLia08.fbx";
 class Phase{public string name,clip;public int frames;public float speed;public bool sit;public Phase(string n,string c,int f,float v=0,bool s=false){name=n;clip=c;frames=f;speed=v;sit=s;}}
 Phase[] phases={new Phase("Warmup","Idle",60),new Phase("Idle","Idle",60),new Phase("Walk","Walk",150,1.2f),new Phase("Wave","Wave",60),new Phase("Joy","Joy",150),new Phase("Talk","Talk",90),new Phase("SitEnter","Sit",30,0,true),new Phase("Sit","Sit",120,0,true),new Phase("SitTalk","SitTalk",120,0,true),new Phase("StandEnter","Idle",30),new Phase("GameWalk",null,150,1.2f)};
 ProtoBody[] bodies=new ProtoBody[4];Camera cam;RenderTexture rt;int phase=-1,frame;bool ready,ended;StringBuilder csv=new StringBuilder("phase,frame,role,clip,leg_hit_strength,corrective,skirt_springs,leg_capsules,corrective_meshes,penetration_cm,sum_cm,mean_angle,angle_difference_from_off\n");StringBuilder shots=new StringBuilder();
 static object Field(object o,string name)=>o.GetType().GetField(name,F).GetValue(o);
 void Start(){try{
  if(LoungeWorld.Instance!=null||FindObjectsByType<SimCharacter>(FindObjectsSortMode.None).Length>0)throw new Exception("This capture must run in the empty inspection scene, with no residents");
  foreach(var b in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))if(b!=this)b.enabled=false;
  Directory.CreateDirectory(Output);Directory.CreateDirectory(Path.Combine(Output,"individual"));Directory.CreateDirectory(Path.Combine(Output,"waist"));Directory.CreateDirectory(Path.Combine(Output,"pair"));Directory.CreateDirectory(Path.Combine(Output,"detail"));Time.timeScale=1;Time.captureDeltaTime=1f/60;Time.fixedDeltaTime=1f/60;Application.runInBackground=true;
  RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.5f,.47f,.42f);RenderSettings.skybox=null;
  var sun=new GameObject("S05_GameDaySun").AddComponent<Light>();sun.type=LightType.Directional;sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(50,-35,0);sun.color=new Color(1,.95f,.85f);sun.intensity=1.1f;sun.cullingMask=(1<<29)|(1<<30)|(1<<31);
  var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.layer=31;floor.transform.localScale=Vector3.one*8;var fm=new Material(Shader.Find("Universal Render Pipeline/Lit"));fm.SetColor("_BaseColor",new Color(.36f,.37f,.40f));floor.GetComponent<Renderer>().material=fm;
  cam=new GameObject("S05_ReviewCamera").AddComponent<Camera>();cam.enabled=false;cam.nearClipPlane=.03f;cam.farClipPlane=100;cam.fieldOfView=30;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.30f,.32f,.36f);rt=new RenderTexture(640,800,24);cam.targetTexture=rt;
  for(int i=0;i<4;i++){
   var owner=new GameObject("S05_"+(i%2==0?"Tia":"Lia08")+(i>=2?"_ColliderOff":"_ColliderOn"));owner.transform.position=new Vector3(i%2==0?-1:1,0,0);
   ProtoBody pb;
   if(i%2==0)pb=ProtoBody.Create(owner,CharLook.Get("R",false,Color.gray),"R");
   else {var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(Model);var model=Instantiate(prefab,owner.transform,false);model.name="Model";pb=owner.AddComponent<ProtoBody>();typeof(ProtoBody).GetField("real",F).SetValue(pb,true);if(!(bool)typeof(ProtoBody).GetMethod("Setup",F).Invoke(pb,new object[]{model.transform,CharLook.Get("I",false,Color.gray)}))throw new Exception("Review Setup failed");}
   if(pb==null)throw new Exception("Model missing");pb.testMode=true;pb.testLetter="R";pb.testScene="";pb.testMood="";pb.testOutfit="Uniform";pb.testFace="";pb.testSkirtSit=-1;pb.testNoLegHit=i>=2;
   foreach(var r in owner.GetComponentsInChildren<Renderer>(true)){r.gameObject.layer=i>=2?28:(i==0?29:30);if(i>=2)r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;}
   bodies[i]=pb;
  }
  if(!ProtoBody.SkirtLegHit)throw new Exception("Global skirt-leg collision is disabled; not changing game settings");
  File.WriteAllText(Path.Combine(Output,"conditions.txt"),"Unity "+Application.unityVersion+"\nGame ProtoBody actual setup, Humanoid graph, UpdateCorrectives and UpdateSprings; 60fps fixed capture delta.\nDay light: DormBuilder sunny noon values, sun Euler(50,-35,0), color(1,.95,.85), intensity1.1, ambient(.5,.47,.42).\nNo LoungeWorld or SimCharacter; no SaveSystem writes.\nOriginal registered R instantiated without asset changes. Lia08 loaded from Editor-only review asset.\nTwo invisible collider-off copies provide per-frame control, without changing the global setting.\n");
  ready=true;NextPhase();
 }catch(Exception e){Fail(e);}}
 void NextPhase(){phase++;frame=0;if(phase>=phases.Length){Finish();return;}var p=phases[phase];foreach(var pb in bodies){pb.testClip=p.clip;pb.testPose=p.sit?BodyPose.Sit:BodyPose.Stand;pb.testSpeed=p.speed;pb.testTalk=false;}}
 void Update(){if(!ready||ended)return;try{var p=phases[phase];foreach(var pb in bodies){if(p.speed>0)pb.transform.position+=pb.transform.forward*p.speed/60f;if(frame>0)pb.TestSetClipTime((frame-1)/60f);}}catch(Exception e){Fail(e);}}
 void LateUpdate(){if(!ready||ended)return;try{
  var p=phases[phase];frame++;if(p.name!="Warmup"){
   for(int i=0;i<4;i++){
    var pb=bodies[i];var a=pb.SkirtLegDebug.Split(',');float diff=0;
    if(i<2){foreach(var t in pb.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Spring_Skirt")&&!t.name.EndsWith("_End"))){var q=bodies[i+2].GetComponentsInChildren<Transform>(true).First(x=>x.name==t.name);diff=Mathf.Max(diff,Quaternion.Angle(t.localRotation,q.localRotation));}}
    csv.AppendLine(p.name+","+frame+","+(i%2==0?"Tia":"Lia08")+(i>=2?"_off":"_on")+",\""+pb.DebugClipNow()+"\","+Convert.ToSingle(Field(pb,"legK")).ToString("F6")+","+Convert.ToSingle(Field(pb,"skirtSitNow")).ToString("F4")+","+a[3]+","+a[4]+","+a[5]+","+a[0]+","+a[1]+","+a[2]+","+diff.ToString("F5"));
   }
   bool transition=p.name.EndsWith("Enter"),series=p.name=="Walk"||p.name=="Joy"||p.name=="GameWalk";
   bool capture=transition?frame%3==0:series?frame%15==0:(frame==30||frame==p.frames);
   if(capture){foreach(string view in new[]{"front","side","back","diagonal"})PairShot(p.name,frame,view);}
   if((p.name=="Idle"||p.name=="Walk"||p.name=="Wave"||p.name=="SitTalk")&&(frame==30||frame==60||frame==120))for(int i=0;i<2;i++)DetailShot(i,p.name,frame);
   for(int i=0;i<2;i++)chest.AppendLine(p.name+","+frame+","+(i==0?"Tia":"Lia08")+","+bodies[i].SpringDebug);
  }
  if(frame>=p.frames)NextPhase();
 }catch(Exception e){Fail(e);}}
 void Shot(int who,string phaseName,int f,string view,bool waist,bool ids){
  var pb=bodies[who];var root=pb.transform;float yaw=view=="front"?0:view=="side"?90:view=="back"?180:view=="backdiagonal"?135:45;Vector3 di=Quaternion.AngleAxis(yaw,Vector3.up)*root.forward;
  Vector3 at=root.position+Vector3.up*(phases[phase].sit?.62f:.78f);float distance=3.8f,up=.2f;
  if(waist){at=pb.Bone("Hips").position+Vector3.up*.08f+root.right*.10f;distance=.90f;up=.10f;}
  cam.cullingMask=(1<<(who==0?29:30))|(1<<31);cam.transform.position=at+di*distance+Vector3.up*up;cam.transform.LookAt(at);cam.fieldOfView=30;
  var old=new Dictionary<Renderer,Material[]>();var temp=new List<Material>();
  if(ids)foreach(var r in pb.GetComponentsInChildren<Renderer>(true)){old[r]=r.sharedMaterials;Color c=r.name.Contains("Jacket")?Color.blue:r.name.Contains("Skirt")?Color.green:r.name.StartsWith("Body")?Color.red:r.name.Contains("Hand")?new Color(1,.5f,0):r.name.Contains("Hair")?Color.yellow:r.name.Contains("Panties")?Color.magenta:Color.cyan;var m=new Material(Shader.Find("Universal Render Pipeline/Unlit"));m.SetColor("_BaseColor",c);m.SetFloat("_Cull",0);temp.Add(m);r.sharedMaterials=Enumerable.Repeat(m,r.sharedMaterials.Length).ToArray();}
  cam.Render();var previous=RenderTexture.active;RenderTexture.active=rt;var im=new Texture2D(640,800,TextureFormat.RGB24,false);im.ReadPixels(new Rect(0,0,640,800),0,0);im.Apply();RenderTexture.active=previous;
  string name=(who==0?"Tia":"Lia08")+"_"+phaseName+"_"+f.ToString("D3")+"_"+view+(ids?"_partIDs":"")+".png";File.WriteAllBytes(Path.Combine(Output,waist?"waist":"individual",name),im.EncodeToPNG());Destroy(im);
  foreach(var kv in old)kv.Key.sharedMaterials=kv.Value;foreach(var m in temp)Destroy(m);
  shots.AppendLine(name+" from="+cam.transform.position.ToString("F6")+" target="+at.ToString("F6")+" relativeFrom="+(cam.transform.position-root.position).ToString("F6")+" clip="+pb.DebugClipNow()+" corrective="+Field(pb,"skirtSitNow"));
 }
 StringBuilder chest=new StringBuilder("phase,frame,role,chest_pitch_degrees,chest_stretch_percent,hair_swing_degrees\n");
 void SaveCamera(string path){cam.Render();var prev=RenderTexture.active;RenderTexture.active=cam.targetTexture;var im=new Texture2D(cam.targetTexture.width,cam.targetTexture.height,TextureFormat.RGB24,false);im.ReadPixels(new Rect(0,0,im.width,im.height),0,0);im.Apply();RenderTexture.active=prev;File.WriteAllBytes(path,im.EncodeToPNG());Destroy(im);}
 void PairShot(string name,int f,string view){float yaw=view=="front"?0:view=="side"?90:view=="back"?180:45;Vector3 dir=Quaternion.AngleAxis(yaw,Vector3.up)*Vector3.forward;Vector3 right=Vector3.Cross(dir,Vector3.up);var a=bodies[0].transform.position;var b=bodies[1].transform.position;var center=(a+b)*.5f;bodies[0].transform.position=center-right*.75f;bodies[1].transform.position=center+right*.75f;var at=center+Vector3.up*(phases[phase].sit?.67f:.85f);cam.cullingMask=(1<<29)|(1<<30)|(1<<31);var paired=new RenderTexture(1200,800,24);cam.targetTexture=paired;cam.transform.position=at+dir*4.5f+Vector3.up*.2f;cam.transform.LookAt(at);SaveCamera(Path.Combine(Output,"pair",name+"_"+f.ToString("D3")+"_"+view+".png"));cam.targetTexture=rt;paired.Release();Destroy(paired);bodies[0].transform.position=a;bodies[1].transform.position=b;shots.AppendLine("PAIR "+name+" "+f+" "+view+" Tia left / Lia right; same actual camera, display translation only, restored immediately");}
 void DetailShot(int who,string name,int f){var pb=bodies[who];var at=pb.Bone("LeftUpperArm");if(at==null)at=pb.Bone("LeftArm");var target=at!=null?at.position-Vector3.up*.1f:pb.transform.position+Vector3.up*1.15f;var dir=Quaternion.AngleAxis(145,Vector3.up)*Vector3.forward;cam.cullingMask=(1<<(who==0?29:30))|(1<<31);cam.transform.position=target+dir*1.1f+Vector3.up*.12f;cam.transform.LookAt(target);SaveCamera(Path.Combine(Output,"detail",(who==0?"Tia":"Lia08")+"_"+name+"_"+f.ToString("D3")+"_armpit.png"));}
 void Finish(){if(ended)return;ended=true;File.WriteAllText(Path.Combine(Output,"physics.csv"),csv.ToString(),Encoding.UTF8);File.WriteAllText(Path.Combine(Output,"chest.csv"),chest.ToString(),Encoding.UTF8);File.WriteAllText(Path.Combine(Output,"shots.txt"),shots.ToString(),Encoding.UTF8);File.WriteAllText(Path.Combine(Output,"DONE.txt"),DateTime.Now.ToString());Time.captureDeltaTime=0;UnityEditor.EditorApplication.isPlaying=false;}
 void Fail(Exception e){ended=true;Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,"ERROR.txt"),e.ToString());Debug.LogException(e);UnityEditor.EditorApplication.isPlaying=false;}
}}
#endif
