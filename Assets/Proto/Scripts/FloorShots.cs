using System.Collections;
using System.IO;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 確認用（2026-10-30 モーションのチャット）：床に座る（正座）動きの見比べ。
    /// リア（I）を床に座らせ、仮の正座（骨を直接曲げる）・SitFloor_HY1・SitFloor_HY2（HY-Motion）・SitFloor_Hand（手で付けた）を
    /// 0.5 秒ごとに 6 秒まで、横と前から撮る。最後に斜め上と低い前からも撮る。
    /// 保存先：仮モデル\Unity確認\床座り_比較\。命令 floorshots。ゲームの進行には影響しない。
    /// </summary>
    public class FloorShots : MonoBehaviour
    {
        public static string Letter = "I";
        const int PhotoLayer = 31;

        IEnumerator Start()
        {
            string dir = Path.Combine(ProtoPhotoBooth.OutDir, "床座り_比較");
            Directory.CreateDirectory(dir);
            var cam = new GameObject("FloorCam").AddComponent<Camera>();
            cam.enabled = false; cam.fieldOfView = 30f; cam.nearClipPlane = 0.05f;
            cam.cullingMask = 1 << PhotoLayer; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.3f, 0.32f, 0.36f);
            var rt = new RenderTexture(480, 480, 24); cam.targetTexture = rt;
            var lamp = new GameObject("FloorLamp").AddComponent<Light>(); lamp.type = LightType.Point; lamp.range = 10f; lamp.intensity = 1.2f;
            string[] clips = { "", "SitFloor_HY1", "SitFloor_HY2", "SitFloor_Hand", "SitFloor_Down", "SitFloor", "SitFloor_Up" };
            string[] tags = { "0仮", "1HY1", "2HY2", "3手", "4座る", "5正座", "6立つ" };
            for (int ci = 0; ci < clips.Length; ci++)
            {
                var go = new GameObject("Floor_" + tags[ci]);
                go.transform.position = new Vector3(60f + ci * 4f, 0f, 200f);
                var pb = ProtoBody.Create(go, CharLook.Get(Letter, false, Color.gray), Letter);
                if (pb == null) { Destroy(go); continue; }
                pb.testMode = true; pb.testPose = BodyPose.SitFloor; pb.testOutfit = "Uniform"; pb.testFace = ""; pb.testSitFloorClip = clips[ci];
                foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.gameObject.layer = PhotoLayer;
                var tr = go.transform;
                Vector3 c = tr.position + Vector3.up * 0.75f;
                float t = 0f, next = 0.5f;
                while (t < 6.01f)
                {
                    t += Time.deltaTime;
                    if (t >= next)
                    {
                        string f = (next * 10f).ToString("00");
                        SwayShots.Shot(cam, rt, lamp, c + tr.right * 3.0f + Vector3.up * 0.2f, c, Path.Combine(dir, tags[ci] + "_横_" + f + ".png"));
                        SwayShots.Shot(cam, rt, lamp, c + tr.forward * 3.0f + Vector3.up * 0.2f, c, Path.Combine(dir, tags[ci] + "_前_" + f + ".png"));
                        next += 0.5f;
                    }
                    yield return null;
                }
                Vector3 low = tr.position + Vector3.up * 0.35f;
                SwayShots.Shot(cam, rt, lamp, low + (tr.forward + tr.right).normalized * 1.8f + Vector3.up * 1.1f, low, Path.Combine(dir, tags[ci] + "_斜め上.png"));
                SwayShots.Shot(cam, rt, lamp, low + tr.forward * 1.6f - Vector3.up * 0.15f, low, Path.Combine(dir, tags[ci] + "_低い前.png"));
                SwayShots.Shot(cam, rt, lamp, low + tr.right * 1.6f, low, Path.Combine(dir, tags[ci] + "_近い横.png"));
                Destroy(go);
            }
            // ゲームと同じ流れ：座る→正座→話す→居眠り→立つ→立ったまま（0.5 秒ごとに横から）
            {
                var go = new GameObject("Floor_flow");
                go.transform.position = new Vector3(60f + 7 * 4f, 0f, 200f);
                var pb = ProtoBody.Create(go, CharLook.Get(Letter, false, Color.gray), Letter);
                if (pb != null)
                {
                    pb.testMode = true; pb.testPose = BodyPose.SitFloor; pb.testOutfit = "Uniform"; pb.testFace = ""; pb.testSitFloorClip = null;
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.gameObject.layer = PhotoLayer;
                    var tr = go.transform; Vector3 c = tr.position + Vector3.up * 0.75f;
                    float t = 0f, next = 0.5f;
                    while (t < 11.01f)
                    {
                        t += Time.deltaTime;
                        pb.testTalk = t >= 3.5f && t < 5.5f;
                        pb.testDoze = t >= 5.5f && t < 7f;
                        if (t >= 7f) { pb.testPose = BodyPose.Stand; pb.testStandUp = t < 10f; }
                        if (t >= next)
                        {
                            string f = (next * 10f).ToString("000");
                            SwayShots.Shot(cam, rt, lamp, c + tr.right * 3.0f + Vector3.up * 0.2f, c, Path.Combine(dir, "7流れ_横_" + f + ".png"));
                            if (next >= 3.5f && next < 7f) SwayShots.Shot(cam, rt, lamp, c + tr.forward * 3.0f + Vector3.up * 0.2f, c, Path.Combine(dir, "7流れ_前_" + f + ".png"));
                            next += 0.5f;
                        }
                        yield return null;
                    }
                    Destroy(go);
                }
            }
            // 寝る（ベッドの高さ・床）とお風呂：仮の動き（_仮）と新しい動きを並べて撮る（2026-10-30）
            string keepLie = ProtoBody.LieClip, keepSoak = ProtoBody.SoakClip;
            var poses = new[] { BodyPose.Lie, BodyPose.LieFloor, BodyPose.Soak };
            string[] pn = { "8寝る", "9床寝", "10風呂" };
            for (int pi = 0; pi < poses.Length; pi++)
                foreach (bool useClip in new[] { false, true })
                {
                    ProtoBody.LieClip = useClip ? keepLie : ""; ProtoBody.SoakClip = useClip ? keepSoak : "";
                    var go = new GameObject("Pose_" + pn[pi]);
                    go.transform.position = new Vector3(100f + pi * 6f + (useClip ? 3f : 0f), 0f, 200f);
                    var pb = ProtoBody.Create(go, CharLook.Get(Letter, false, Color.gray), Letter);
                    if (pb == null) { Destroy(go); continue; }
                    pb.testMode = true; pb.testPose = poses[pi]; pb.testOutfit = poses[pi] == BodyPose.Soak ? "Towel" : (poses[pi] == BodyPose.Lie ? "Sleep" : "Uniform");
                    pb.testFace = ""; pb.testEyesClosed = poses[pi] != BodyPose.Soak;
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.gameObject.layer = PhotoLayer;
                    for (float w = 0f; w < 1.5f; w += Time.deltaTime) yield return null;
                    var tr = go.transform;
                    float h = poses[pi] == BodyPose.Lie ? 0.75f : (poses[pi] == BodyPose.LieFloor ? 0.3f : 0.45f);
                    Vector3 c = tr.position + Vector3.up * h;
                    string tag = pn[pi] + (useClip ? "" : "_仮");
                    SwayShots.Shot(cam, rt, lamp, c + tr.right * 2.8f + Vector3.up * 0.1f, c, Path.Combine(dir, tag + "_横.png"));
                    SwayShots.Shot(cam, rt, lamp, c + Vector3.up * 2.8f + tr.forward * 0.05f, c, Path.Combine(dir, tag + "_上.png"));
                    SwayShots.Shot(cam, rt, lamp, c + (tr.forward + tr.right).normalized * 2.2f + Vector3.up * 1.2f, c, Path.Combine(dir, tag + "_斜め.png"));
                    Destroy(go);
                }
            ProtoBody.LieClip = keepLie; ProtoBody.SoakClip = keepSoak;
            File.WriteAllText(Path.Combine(dir, "済み.txt"), System.DateTime.Now.ToString());
            Destroy(cam.gameObject); Destroy(lamp.gameObject); rt.Release();
            Destroy(gameObject);
        }
    }
}
