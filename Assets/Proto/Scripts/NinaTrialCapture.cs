// ニナ仮の版1の取り込み確認専用。Editor の明示した合図でのみ動く。
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace LifeSimProto
{
    public class NinaTrialCapture : MonoBehaviour
    {
        public static readonly string Tag = "nina_v0_1_20261114";
        public bool detailsOnly;
        string dir, release; Light previousSun;
        Camera cam; RenderTexture rt; Light lamp;
        LoungeWorld world; ProgressDirector progress;
        bool worldOn, progressOn; float timeScale;
        GameObject sample;
        StringBuilder log = new StringBuilder();
        IEnumerator Start()
        {
            dir = Path.Combine(ProtoPhotoBooth.OutDir, detailsOnly ? "ニナ_仮版1_20261114_詳細" : "ニナ_仮版1_20261114_白い光");
            Directory.CreateDirectory(dir);
            release = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/nina_trial_release_white.txt"));
            world = FindAnyObjectByType<LoungeWorld>();
            progress = FindAnyObjectByType<ProgressDirector>();
            timeScale = Time.timeScale;
            if (world != null) { worldOn = world.enabled; world.enabled = false; }
            if (progress != null) { progressOn = progress.enabled; progress.enabled = false; }
            Time.timeScale = 1f;
            cam = new GameObject("NinaReviewCamera").AddComponent<Camera>();
            cam.enabled = false; cam.orthographic = true; cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 100f; cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.32f, 0.35f, 0.4f);
            rt = new RenderTexture(1000, 1100, 24); cam.targetTexture = rt;
            log.AppendLine(DateTime.Now.ToString("s"));
            log.AppendLine("Resident photos use game lighting. Comparison samples use white directional main light intensity 1.2; game toon materials unchanged. No save writes.");
            if (world != null)
            {
                var resident = world.characters.FirstOrDefault(c => c.letter == "H");
                log.AppendLine("resident_H=" + (resident != null));
                if (resident != null)
                {
                    var body = resident.GetComponent<ProtoBody>();
                    log.AppendLine("resident: " + (body == null ? "no ProtoBody" : body.DebugInfo));
                    log.AppendLine("resident_action=" + resident.actionText + "; pose=" + resident.BodyPoseNow);
                    Vector3 target = resident.transform.position + Vector3.up * 0.85f;
                    Shot("ゲーム内_H_周囲", target, resident.transform.forward, 1.1f);
                    if (body != null) Shot("ゲーム内_H_顔", body.HeadPos, resident.transform.forward, 0.23f);
                }
            }
            cam.cullingMask = 1 << 31;
            lamp = new GameObject("NinaReviewLight").AddComponent<Light>();
            lamp.type = LightType.Directional; lamp.intensity = 1.2f; lamp.shadows = LightShadows.Soft; lamp.transform.rotation = Quaternion.Euler(35f,160f,0f); previousSun = RenderSettings.sun; RenderSettings.sun = lamp;
            lamp.color = Color.white; lamp.cullingMask = 1 << 31;
            sample = new GameObject("Nina_InspectionOnly");
            sample.transform.position = new Vector3(420f, 0f, 180f);
            var pb = ProtoBody.Create(sample, CharLook.Get("H", false, Color.gray), "H");
            if (pb == null) { log.AppendLine("FAIL: ProtoBody.Create returned null"); Finish(); yield break; }
            pb.testMode = true; pb.testLetter = "H"; pb.testOutfit = "Uniform"; pb.testPose = BodyPose.Stand;
            foreach (var r in sample.GetComponentsInChildren<Renderer>(true)) r.gameObject.layer = 31;
            if(detailsOnly) pb.testClip = "Idle";
            yield return new WaitForSeconds(2f);
            if(detailsOnly) { pb.TestSetClipTime(0f); yield return null; yield return null; }
            log.AppendLine("sample: " + pb.DebugInfo);
            var origin = sample.transform.position;
            var dirs = new[] { Vector3.forward, Vector3.right, Vector3.back, new Vector3(0.7f,0,1).normalized };
            var names = new[] { "前", "横", "後ろ", "斜め" };
            for (int i=0;i<4;i++) Shot("立つ_"+names[i],origin+Vector3.up*0.8f,dirs[i],0.95f);
            for (int i=0;i<4;i++)
            {
                if(i!=2) Shot("顔_"+names[i],pb.HeadPos,dirs[i],0.22f);
                Shot("首_"+names[i],origin+Vector3.up*1.247f,dirs[i],0.155f);
            }
            Shot("靴_横_原点床なし",origin+Vector3.up*0.02f,Vector3.right,0.16f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name="NinaReviewGround"; floor.transform.SetParent(sample.transform,true);
            floor.transform.position=origin+Vector3.down*0.015f; floor.transform.localScale=new Vector3(2f,0.03f,2f);
            floor.layer=31;
            Shot("靴_横_床0",origin+Vector3.up*0.02f,Vector3.right,0.16f);
            floor.SetActive(false);
            if(detailsOnly) { Finish(); yield break; }
            var motion = new StringBuilder("state,frame,bone,qx,qy,qz,qw\n");
            foreach(var state in new[]{"歩く","手を振る","話す","首を振る"})
            {
                pb.testSpeed = state=="歩く" ? 1.2f : 0f;
                pb.testTalk = state=="話す";
                if(state=="手を振る") pb.Gesture("Wave",1.6f);
                if(state=="首を振る") pb.Gesture("HeadShake",1.6f);
                yield return new WaitForSeconds(0.5f);
                for(int k=0;k<6;k++)
                {
                    yield return new WaitForSeconds(0.2f);
                    Shot(state+"_"+k.ToString("00")+"_斜め",origin+Vector3.up*0.8f,dirs[3],0.95f);
                    Shot(state+"_"+k.ToString("00")+"_横",origin+Vector3.up*0.8f,dirs[1],0.95f);
                    if(state=="首を振る") Shot("首回転_"+k.ToString("00"),origin+Vector3.up*1.247f,dirs[0],0.16f);
                    foreach(var b in sample.GetComponentsInChildren<Transform>())
                        if(b.name=="Head" || b.name.StartsWith("Spring_Ear") || b.name=="Hips")
                        { var q=b.localRotation; motion.AppendLine(FormattableString.Invariant($"{state},{k},{b.name},{q.x},{q.y},{q.z},{q.w}")); }
                }
                yield return new WaitForSeconds(0.5f);
            }
            File.WriteAllText(Path.Combine(dir,"耳と頭の向き.csv"),motion.ToString(),new UTF8Encoding(false));
            foreach(var smr in sample.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh=new Mesh(); smr.BakeMesh(mesh);
                float ymin=float.PositiveInfinity;
                foreach(var v in mesh.vertices) ymin=Mathf.Min(ymin,smr.transform.TransformPoint(v).y-origin.y);
                log.AppendLine(smr.name+" triangles="+mesh.triangles.Length/3+" minY="+ymin.ToString("F5")+" keys="+smr.sharedMesh.blendShapeCount);
                Destroy(mesh);
            }
            Destroy(sample); sample=null;
            File.WriteAllText(Path.Combine(dir,"撮影記録.txt"),log.ToString(),new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(dir,"写真済み.txt"),DateTime.Now.ToString("s"));
            // uniformsitshots H を外から送る間も、ゲーム時計と自動保存は止めておく。
            float deadline=Time.realtimeSinceStartup+180f;
            while(!File.Exists(release) && Time.realtimeSinceStartup<deadline) yield return null;
            Finish();
        }
        void Shot(string name,Vector3 at,Vector3 direction,float halfHeight)
        {
            cam.orthographicSize=halfHeight;
            cam.transform.position=at+direction.normalized*3f;
            cam.transform.LookAt(at);
            if(lamp!=null) { lamp.transform.position=cam.transform.position+Vector3.up*0.5f; if(detailsOnly) lamp.transform.rotation=Quaternion.LookRotation(-direction.normalized+Vector3.down*0.35f); }
            cam.Render(); var previous=RenderTexture.active; RenderTexture.active=rt;
            var tex=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
            tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0); tex.Apply(); RenderTexture.active=previous;
            File.WriteAllBytes(Path.Combine(dir,name+".png"),tex.EncodeToPNG()); Destroy(tex);
        }
        void Finish() { File.WriteAllText(Path.Combine(dir,"終了.txt"),log.ToString(),new UTF8Encoding(false)); Destroy(gameObject); }
        void OnDestroy()
        {
            if(world!=null) world.enabled=worldOn;
            if(progress!=null) progress.enabled=progressOn;
            Time.timeScale=timeScale;
            if(sample!=null) Destroy(sample);
            if(cam!=null) Destroy(cam.gameObject);
            if(lamp!=null) { RenderSettings.sun=previousSun; Destroy(lamp.gameObject); }
            if(rt!=null) {rt.Release(); Destroy(rt);}
        }
    }
}
