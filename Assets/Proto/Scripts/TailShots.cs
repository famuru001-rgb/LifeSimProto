using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 確認用（2026-11-25 本体）：しっぽの上着の当たり判定（ProtoBody.Tail.cs）を、使う（on）・使わない（off）で撮りくらべる。
    /// 立つ・歩く・椅子・床に座る × 前・横・後ろ・ななめ後ろ。しっぽの骨と上着の後ろの面の距離（最小）・押し出した回数を 数値.txt に。
    /// 保存先：仮モデル\Unity確認\しっぽ_（Tag）\（記号）_（姿勢）_（向き）_on／off.jpg。命令 tailhit shots 記号[,記号…] Tag。住人・セーブは触らない。
    /// </summary>
    public class TailShots : MonoBehaviour
    {
        public static string Letters = "C";
        public static string Tag = "t1";
        public static bool Busy;
        const int PhotoLayer = 31;
        Camera cam; RenderTexture rt; Light lamp; string dir;

        void Shot(Vector3 from, Vector3 at, string file) { SwayShots.Shot(cam, rt, lamp, from, at, Path.Combine(dir, file + ".jpg")); }

        IEnumerator Start()
        {
            Busy = true;
            dir = Path.Combine(ProtoPhotoBooth.OutDir, "しっぽ_" + Tag);
            Directory.CreateDirectory(dir);
            cam = new GameObject("TailCam").AddComponent<Camera>();
            cam.enabled = false; cam.fieldOfView = 30f; cam.nearClipPlane = 0.02f;
            cam.cullingMask = 1 << PhotoLayer; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.3f, 0.32f, 0.36f);
            rt = new RenderTexture(560, 560, 24); cam.targetTexture = rt;
            lamp = new GameObject("TailLamp").AddComponent<Light>(); lamp.type = LightType.Point; lamp.range = 10f; lamp.intensity = 0.8f;
            var log = new StringBuilder();
            log.AppendLine("しっぽの上着の当たり判定 " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "・余白 " + (ProtoBody.TailMargin * 100f).ToString("0.0") + "cm（根元から " + (ProtoBody.TailMarginRamp * 100f).ToString("0") + "cm で満ちる）");
            log.AppendLine("距離＝しっぽの骨の途中の点と上着の後ろの面の、いちばん近い所（cm。負＝面より内）");
            int li = 0;
            foreach (var raw in Letters.Split(','))
            {
                string letter = raw.Trim().ToUpperInvariant();
                if (letter.Length == 0) continue;
                if (Resources.Load<GameObject>("Chars/" + letter + "/Char" + letter) == null) { log.AppendLine(letter + "：モデルがない"); continue; }
                Vector3 basePos = new Vector3(460f + li * 12f, 0f, 190f);
                li++;
                string[] poses = { "立つ", "歩く", "椅子", "床" };
                for (int pi = 0; pi < poses.Length; pi++)
                {
                    Vector3 pos = basePos + new Vector3(pi * 2.8f, 0f, 0f);
                    var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    floor.name = "TailFloor"; floor.layer = PhotoLayer;
                    floor.transform.position = pos + new Vector3(0f, -0.05f, 0f); floor.transform.localScale = new Vector3(2.4f, 0.1f, 2.4f);
                    Destroy(floor.GetComponent<Collider>());
                    floor.GetComponent<Renderer>().material.color = new Color(0.45f, 0.42f, 0.38f);
                    GameObject chairGo = null;
                    if (pi == 2)
                    {
                        var pf = Resources.Load<GameObject>("Furniture/Furn_Chair");
                        if (pf != null) { chairGo = Instantiate(pf); chairGo.transform.position = pos; chairGo.transform.rotation = pf.transform.localRotation; }
                        else { chairGo = GameObject.CreatePrimitive(PrimitiveType.Cube); chairGo.transform.position = pos + Vector3.up * 0.45f; chairGo.transform.localScale = new Vector3(0.48f, 0.06f, 0.48f); }
                        foreach (var c in chairGo.GetComponentsInChildren<Collider>(true)) Destroy(c);
                        foreach (var r in chairGo.GetComponentsInChildren<Renderer>(true)) r.gameObject.layer = PhotoLayer;
                    }
                    var go = new GameObject("Tail_" + letter + "_" + poses[pi]);
                    go.transform.position = pos;
                    var pb = ProtoBody.Create(go, CharLook.Get(letter, false, Color.gray), letter);
                    if (pb == null) { Destroy(go); if (chairGo != null) Destroy(chairGo); Destroy(floor); continue; }
                    pb.testMode = true; pb.testLetter = letter; pb.testOutfit = "Uniform"; pb.testFace = ""; pb.testSpeed = pi == 1 ? 1.4f : 0f;
                    if (pi == 2) { pb.testPose = BodyPose.Sit; pb.testClip = MotionBook.ChairSit(letter, false); }
                    else if (pi == 3) { pb.testPose = BodyPose.SitFloor; pb.testSitFloorClip = "SitFloor"; }
                    else pb.testPose = BodyPose.Stand;
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.gameObject.layer = PhotoLayer;
                    if (pi == 0) log.AppendLine(letter + "：" + pb.TailProfileText());
                    var tr = go.transform;
                    Transform hp = pb.Bone("Hips");
                    if (hp == null || !pb.HasTailHit) { log.AppendLine(letter + " " + poses[pi] + "：腰の骨・しっぽ・上着のどれかが無い"); Destroy(go); if (chairGo != null) Destroy(chairGo); Destroy(floor); continue; }
                    string pre = letter + "_" + poses[pi] + "_";
                    foreach (var on in new[] { true, false })
                    {
                        pb.testNoTailHit = !on;
                        for (float w = 0f; w < 2.0f; w += Time.deltaTime) yield return null;   // 座りの補正と揺れが落ち着くまで
                        pb.TailStatsReset();
                        yield return null; yield return null;   // 揺れの計算（LateUpdate）を2コマ分数える
                        int pushes = pb.TailPushes; float pmax = pb.TailPushMax;
                        string cl = pb.TailClearance();
                        Vector3 c0 = hp.position - tr.forward * 0.15f - tr.up * (pi == 3 ? -0.02f : 0.05f);
                        string tag = on ? "on" : "off";
                        Shot(c0 + tr.forward * 1.2f + tr.up * 0.10f, c0, pre + "前_" + tag);
                        Shot(c0 - tr.right * 1.2f + tr.up * 0.05f, c0, pre + "横_" + tag);
                        Shot(c0 - tr.forward * 1.2f + tr.up * 0.20f, c0, pre + "後ろ_" + tag);
                        Shot(c0 + (-tr.forward + tr.right).normalized * 0.9f + tr.up * 0.35f, c0, pre + "ななめ後ろ_" + tag);
                        log.AppendLine(letter + " " + poses[pi] + " " + tag + "：距離 " + cl + "・押し出し " + pushes + "回（2コマ）・いちばん " + (pmax * 100f).ToString("0.0") + "cm");
                    }
                    pb.testNoTailHit = false;
                    Destroy(go); if (chairGo != null) Destroy(chairGo); Destroy(floor);
                    yield return null;
                }
            }
            File.WriteAllText(Path.Combine(dir, "数値.txt"), log.ToString(), Encoding.UTF8);
            File.WriteAllText(Path.Combine(dir, "済み.txt"), System.DateTime.Now.ToString());
            Destroy(cam.gameObject); Destroy(lamp.gameObject); rt.Release();
            Busy = false;
            Destroy(gameObject);
        }
    }
}
