#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace LifeSimProto
{
    // 検品だけの見本。住人・部屋・関係・保存データには登録しない。
    public class QRReviewCapture : MonoBehaviour
    {
        public static bool FacesOnly;
        Camera cam;
        RenderTexture rt;
        GameObject floor;
        readonly List<GameObject> made = new List<GameObject>();
        readonly StringBuilder report = new StringBuilder();
        readonly StringBuilder csv = new StringBuilder("letter,action,frame,seconds,rightHandY,leftHandY,chestPitch,chestStretch,hairAngle\n");
        string dir;
        Vector3 stage = new Vector3(100,0,100);
        float overviewPitch = 48, overviewDistance = 24, gameFov=50;
        Light daySun; Color savedSunColor, savedAmbient; float savedSunIntensity;
        LoungeWorld frozenWorld;
        readonly List<Behaviour> frozen = new List<Behaviour>();
        float savedTimeScale;
        static string F(float f) => f.ToString("0.000", CultureInfo.InvariantCulture);
        IEnumerator Start()
        {
            dir = Path.GetFullPath(Path.Combine(Application.dataPath, FacesOnly?"../../仮モデル/Unity確認/QR_20261030_顔":"../../仮モデル/Unity確認/QR_20261030_昼"));
            Directory.CreateDirectory(dir);
            var world = LoungeWorld.Instance;
            frozenWorld=world; savedTimeScale=Time.timeScale;
            if(world!=null) {
                if(world.enabled) { frozen.Add(world); world.enabled=false; }
                foreach(var c in world.characters) if(c.enabled) { frozen.Add(c); c.enabled=false; }
            }
            Time.timeScale=1;
            savedAmbient=RenderSettings.ambientLight;
            daySun=FindObjectsByType<Light>().FirstOrDefault(l=>l.name=="Sun"&&l.type==LightType.Directional);
            if(daySun!=null) { savedSunColor=daySun.color; savedSunIntensity=daySun.intensity; daySun.color=new Color(1,.95f,.85f); daySun.intensity=1.1f; }
            RenderSettings.ambientLight=new Color(.5f,.47f,.42f);
            report.AppendLine("QR Unity review " + DateTime.Now + "\nConfirmation samples only; no residents or saves modified.");
            report.AppendLine("Lighting: game sunny daytime preset (DormBuilder.UpdateLighting day=1/weather=Sunny). Original Sun rotation 50,-35,0; intensity 1.1, RGB 1,.95,.85; ambient .5,.47,.42. Restored after capture; no world time change. Runtime ProtoBody springs; NoSway="+PerfProbe.NoSway);
            if(world != null) report.AppendLine("Residents: " + string.Join(",", world.characters.Select(c=>c.letter)));
            cam = new GameObject("QR_ReviewCamera").AddComponent<Camera>(); made.Add(cam.gameObject);
            if(world != null && world.mainCamera != null)
            {
                cam.CopyFrom(world.mainCamera);
                gameFov=cam.fieldOfView;
                var rig = world.mainCamera.GetComponent<CameraRig>();
                if(rig != null) { overviewPitch = rig.pitch; overviewDistance = rig.distance; }
            }
            cam.enabled = true; cam.nearClipPlane=.02f; cam.farClipPlane=250f;
            cam.clearFlags=CameraClearFlags.SolidColor; cam.backgroundColor=new Color(.29f,.31f,.35f); cam.cullingMask=1<<31;
            rt=new RenderTexture(960,960,24); cam.targetTexture=rt;
            cam.transform.position=stage+new Vector3(0,1,3); cam.transform.LookAt(stage+Vector3.up*.75f);
            report.AppendLine("Default overview pitch="+F(overviewPitch)+" distance="+F(overviewDistance)+" FOV="+F(cam.fieldOfView));
            foreach(var light in FindObjectsByType<Light>())
                if(light.enabled) report.AppendLine("Light: "+light.name+" "+light.type+" "+light.color+" intensity="+F(light.intensity));
            floor=GameObject.CreatePrimitive(PrimitiveType.Plane); made.Add(floor); floor.name="QR_ReviewFloor";
            floor.transform.position=stage+Vector3.down*.015f; floor.transform.localScale=Vector3.one*8; floor.layer=31;
            Destroy(floor.GetComponent<Collider>());
            var shader=Shader.Find("Universal Render Pipeline/Lit");
            var floorMat=new Material(shader); floorMat.SetColor("_BaseColor",new Color(.35f,.37f,.40f)); floor.GetComponent<Renderer>().sharedMaterial=floorMat;
            foreach(string letter in new[]{"Q","R"})
            {
                var pb=Make(letter,stage); report.AppendLine(letter+": "+pb.DebugInfo);
                if(!pb.IsRealModel || pb.SpringCount==0) throw new Exception("Missing real model/springs "+letter);
                foreach(string clip in (FacesOnly?new string[0]:new[]{"Idle","Walk","Wave","Joy","Talk","Sit","SitTalk"}))
                {
                    var asset=pb.TestFindClip(clip); if(asset==null) throw new Exception("Missing clip "+clip);
                    report.AppendLine(letter+" clip="+clip+" length="+F(asset.length));
                    pb.testClip=clip; pb.testPose=clip.StartsWith("Sit")?BodyPose.Sit:BodyPose.Stand;
                    pb.testTalk=clip=="Talk"||clip=="SitTalk"; pb.testSpeed=clip=="Walk"?1.2f:0;
                    pb.testFace=clip=="Joy"?"Smile":"";
                    for(int f=0;f<12;f++) yield return null;
                    pb.TestSetClipTime(0); yield return null;
                    int count=clip=="Joy"?12:clip.StartsWith("Sit")?6:4;
                    float duration=asset.length, next=0, elapsed=0;
                    for(int k=0;k<count;k++)
                    {
                        next=duration*k/Mathf.Max(1,count-1);
                        while(elapsed<next) { elapsed+=Time.deltaTime; yield return null; }
                        yield return new WaitForEndOfFrame();
                        string stem=letter+"_"+clip+"_"+k.ToString("D2");
                        CaptureBody(pb,stem,"front",0,false);
                        if(clip=="Joy") { CaptureBody(pb,stem,"waist_front",0,true); CaptureOverview(pb,stem,0); }
                        if(clip.StartsWith("Sit"))
                        {
                            CaptureBody(pb,stem,"side",90,false); CaptureBody(pb,stem,"back",180,false);
                            CaptureBody(pb,stem,"waist_side",90,true); CaptureBody(pb,stem,"waist_back",180,true);
                            CaptureOverview(pb,stem,90); CaptureOverview(pb,stem,180);
                        }
                        var bones=pb.GetComponentsInChildren<Transform>();
                        var rh=bones.First(t=>t.name.EndsWith("RightHand")); var lh=bones.First(t=>t.name.EndsWith("LeftHand"));
                        csv.AppendLine(letter+","+clip+","+k+","+F(elapsed)+","+F(rh.position.y)+","+F(lh.position.y)+","+pb.SpringDebug);
                    }
                }
                // 顔：ゲームのシェーダー・光のまま、8キーをそれぞれ最大にする。
                Destroy(pb.gameObject); yield return null; pb=Make(letter,stage);
                pb.testClip="Idle"; pb.testPose=BodyPose.Stand; pb.testTalk=false; pb.testSpeed=0; pb.testFace="";
                for(float settle=0;settle<1.5f;settle+=Time.deltaTime) yield return null;
                pb.enabled=false;
                foreach(string key in new[]{"Basis","Blink","Blink_L","Blink_R","Smile","MouthOpen","Sad","Angry","Surprised","Surprised_MouthOpen"})
                {
                    foreach(var r in pb.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                        for(int i=0;i<r.sharedMesh.blendShapeCount;i++)
                        {
                            string n=r.sharedMesh.GetBlendShapeName(i);
                            bool on=n==key||(key=="Surprised_MouthOpen"&&(n=="Surprised"||n=="MouthOpen"));
                            r.SetBlendShapeWeight(i,on?100:0);
                        }
                    yield return null; yield return new WaitForEndOfFrame();
                    CaptureFace(pb,letter+"_face_"+key,0); CaptureFace(pb,letter+"_face_"+key,45);
                    foreach(var r in pb.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if(r.name.StartsWith("Face_"))
                    { report.Append(letter+" "+key+" "+r.name+": "); for(int i=0;i<r.sharedMesh.blendShapeCount;i++) report.Append(r.sharedMesh.GetBlendShapeName(i)+"="+r.GetBlendShapeWeight(i)+";"); report.AppendLine(); }
                }
                if(FacesOnly) { Destroy(pb.gameObject); yield return null; continue; }
                pb.enabled=true; pb.testClip="Walk";
                for(int f=0;f<15;f++) yield return null;
                // 歩き出し、通常歩行、停止：実際の揺れ計算を連続で進める。
                float time=0, shotAt=0; int frame=0;
                while(time<5f)
                {
                    bool walking=time>=.6f&&time<3.1f;
                    pb.testClip=walking?"Walk":"Idle"; pb.testSpeed=walking?1.2f:0;
                    if(walking) pb.transform.position+=pb.transform.forward*1.2f*Time.deltaTime;
                    yield return new WaitForEndOfFrame();
                    if(time>=shotAt)
                    {
                        string stem=letter+"_sway_"+frame.ToString("D3");
                        CaptureBody(pb,stem,"front",0,false); CaptureBody(pb,stem,"side",90,false); CaptureBody(pb,stem,"back",180,false);
                        csv.AppendLine(letter+",sway,"+frame+","+F(time)+",,,"+pb.SpringDebug);
                        frame++; shotAt+=.2f;
                    }
                    time+=Time.deltaTime; yield return null;
                }
                Destroy(pb.gameObject); yield return null;
            }
            var q=Make("Q",stage+Vector3.left*.65f); var r2=Make("R",stage+Vector3.right*.65f);
            q.testClip=r2.testClip="Idle";
            for(int f=0;f<30;f++) yield return null;
            Snap(stage+new Vector3(0,1.0f,-4.1f),stage+Vector3.up*.78f,"QR_pair_front.png",35);
            Snap(stage+new Vector3(2.1f,1.2f,-4.2f),stage+Vector3.up*.78f,"QR_pair_45.png",35);
            File.WriteAllText(Path.Combine(dir,"runtime_inspection.txt"),report.ToString(),Encoding.UTF8);
            File.WriteAllText(Path.Combine(dir,"motion_samples.csv"),csv.ToString(),Encoding.UTF8);
            File.WriteAllText(Path.Combine(dir,"DONE.txt"),DateTime.Now.ToString());
            foreach(var go in made) if(go!=null) Destroy(go);
            rt.Release(); Destroy(rt); Destroy(floorMat); Destroy(gameObject);
        }
        void OnDestroy() { foreach(var b in frozen) if(b!=null) b.enabled=true; Time.timeScale=savedTimeScale; RenderSettings.ambientLight=savedAmbient; if(daySun!=null) { daySun.color=savedSunColor; daySun.intensity=savedSunIntensity; } }
        ProtoBody Make(string letter,Vector3 pos)
        {
            var go=new GameObject("Review_"+letter+(letter=="Q"?"_ティナ":"_ティア")); made.Add(go); go.transform.position=pos;
            go.transform.rotation=Quaternion.Euler(0,180,0);
            cam.transform.position=pos+go.transform.forward*3+Vector3.up*.85f; cam.transform.LookAt(pos+Vector3.up*.75f);
            var pb=ProtoBody.Create(go,CharLook.Get(letter,false,Color.gray),letter);
            if(pb==null) throw new Exception("Could not create "+letter);
            pb.testMode=true; pb.testLetter=letter; pb.testScene=""; pb.testMood=""; pb.testPose=BodyPose.Stand; pb.testOutfit="Uniform"; pb.testFace="";
            foreach(var renderer in go.GetComponentsInChildren<Renderer>(true)) renderer.gameObject.layer=31;
            return pb;
        }
        Vector3 Hip(ProtoBody pb) => pb.GetComponentsInChildren<Transform>().First(t=>t.name.EndsWith(":Hips")||t.name=="Hips").position;
        void CaptureBody(ProtoBody pb,string stem,string label,float yaw,bool waist)
        {
            var tr=pb.transform; var center=waist?Hip(pb)+Vector3.up*.06f:tr.position+Vector3.up*(pb.testPose==BodyPose.Sit?.57f:.75f);
            var direction=Quaternion.AngleAxis(yaw,Vector3.up)*tr.forward;
            Snap(center+direction*(waist?.92f:2.8f)+Vector3.up*(waist?.18f:.22f),center,stem+"_"+label+".png",35);
        }
        void CaptureOverview(ProtoBody pb,string stem,float yaw)
        {
            Vector3 center=Hip(pb), direction=Quaternion.AngleAxis(yaw,Vector3.up)*pb.transform.forward;
            float rad=overviewPitch*Mathf.Deg2Rad;
            Vector3 ray=direction*Mathf.Cos(rad)+Vector3.up*Mathf.Sin(rad);
            // 通常の見下ろし角度。全体距離と、対象を追う距離の両方を記録。
            Snap(center+ray*overviewDistance,center,stem+"_game_default_"+yaw+".png",gameFov);
            Snap(center+ray*4f,center,stem+"_game_near_"+yaw+".png",gameFov);
        }
        void CaptureFace(ProtoBody pb,string stem,float yaw)
        {
            var at=pb.HeadPoint-Vector3.up*.035f;
            var direction=Quaternion.AngleAxis(yaw,Vector3.up)*pb.transform.forward;
            Snap(at+direction*.6f,at,stem+"_"+yaw+".png",35);
        }
        void Snap(Vector3 from,Vector3 at,string file,float fov)
        {
            cam.fieldOfView=fov; cam.transform.position=from; cam.transform.LookAt(at); cam.Render();
            var prev=RenderTexture.active; RenderTexture.active=rt;
            var tex=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
            tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0); tex.Apply(); RenderTexture.active=prev;
            File.WriteAllBytes(Path.Combine(dir,file),tex.EncodeToPNG()); Destroy(tex);
        }
    }
}
#endif
