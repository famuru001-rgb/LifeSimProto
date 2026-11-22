using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 確認用（2026-11-22 本体 S08）：ひざの形のキー（ProtoBody.Knee.cs）を、ひざのキーを動かす（on）・止める（off）で撮りくらべる。
    /// キャラごとに 椅子に座る（Sit）・正座（SitFloor）・立つ にして、左ひざの拡大と、太ももの中ほどから足先までを、真横・斜め後ろ・斜め前から撮る。
    /// 保存先：仮モデル\Unity確認\ひざ_（Tag）\。左右の角度と、強さが 0 でないキーを 数値.txt に。命令 kneeshots 記号[,記号…] [Tag]。住人・セーブは触らない。
    /// </summary>
    public class KneeShots : MonoBehaviour
    {
        public static string Letters = "I,Q,R";
        public static string Tag = "S08";
        public static bool Busy;
        const int PhotoLayer = 31;
        Camera cam; RenderTexture rt; Light lamp; string dir;

        IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        void Shot(Vector3 from, Vector3 at, string file) { SwayShots.Shot(cam, rt, lamp, from, at, Path.Combine(dir, file + ".png")); }

        IEnumerator Start()
        {
            Busy = true;
            bool keepOn = ProtoBody.KneeOn;
            dir = Path.Combine(ProtoPhotoBooth.OutDir, "ひざ_" + Tag);
            Directory.CreateDirectory(dir);
            cam = new GameObject("KneeCam").AddComponent<Camera>();
            cam.enabled = false; cam.fieldOfView = 30f; cam.nearClipPlane = 0.02f;
            cam.cullingMask = 1 << PhotoLayer; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.3f, 0.32f, 0.36f);
            rt = new RenderTexture(600, 600, 24); cam.targetTexture = rt;
            lamp = new GameObject("KneeLamp").AddComponent<Light>(); lamp.type = LightType.Point; lamp.range = 10f; lamp.intensity = 1.4f;
            var log = new StringBuilder();
            log.AppendLine("ひざのキーの確かめ " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            int li = 0;
            foreach (var raw in Letters.Split(','))
            {
                string letter = raw.Trim().ToUpperInvariant();
                if (letter.Length == 0) continue;
                if (Resources.Load<GameObject>("Chars/" + letter + "/Char" + letter) == null) { log.AppendLine(letter + "：モデルがない"); continue; }
                Vector3 basePos = new Vector3(420f + li * 8f, 0f, 170f);
                li++;
                string[] poses = { "椅子", "正座", "立つ" };
                for (int pi = 0; pi < 3; pi++)
                {
                    Vector3 pos = basePos + new Vector3(pi * 2.6f, 0f, 0f);
                    var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    floor.name = "KneeFloor"; floor.layer = PhotoLayer;
                    floor.transform.position = pos + new Vector3(0f, -0.05f, 0f); floor.transform.localScale = new Vector3(2.2f, 0.1f, 2.2f);
                    Destroy(floor.GetComponent<Collider>());
                    floor.GetComponent<Renderer>().material.color = new Color(0.45f, 0.42f, 0.38f);
                    GameObject chairGo = null;
                    if (pi == 0)
                    {
                        var pf = Resources.Load<GameObject>("Furniture/Furn_Chair");
                        if (pf != null) { chairGo = Instantiate(pf); chairGo.transform.position = pos; chairGo.transform.rotation = pf.transform.localRotation; }
                        else { chairGo = GameObject.CreatePrimitive(PrimitiveType.Cube); chairGo.transform.position = pos + Vector3.up * 0.45f; chairGo.transform.localScale = new Vector3(0.48f, 0.06f, 0.48f); }
                        foreach (var c in chairGo.GetComponentsInChildren<Collider>(true)) Destroy(c);
                        foreach (var r in chairGo.GetComponentsInChildren<Renderer>(true)) r.gameObject.layer = PhotoLayer;
                    }
                    var go = new GameObject("Knee_" + letter + "_" + poses[pi]);
                    go.transform.position = pos;
                    var pb = ProtoBody.Create(go, CharLook.Get(letter, false, Color.gray), letter);
                    if (pb == null) { Destroy(go); if (chairGo != null) Destroy(chairGo); Destroy(floor); continue; }
                    pb.testMode = true; pb.testLetter = letter; pb.testOutfit = "Uniform"; pb.testFace = ""; pb.testSpeed = 0f;
                    if (pi == 0) { pb.testPose = BodyPose.Sit; pb.testClip = MotionBook.ChairSit(letter, false); }
                    else if (pi == 1) { pb.testPose = BodyPose.SitFloor; pb.testSitFloorClip = "SitFloor"; }
                    else pb.testPose = BodyPose.Stand;
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.gameObject.layer = PhotoLayer;
                    ProtoBody.KneeOn = true;
                    for (float w = 0f; w < 2.0f; w += Time.deltaTime) yield return null;   // 座りの補正と揺れが落ち着くまで
                    var tr = go.transform;
                    Transform up = pb.Bone("LeftUpLeg"), kn = pb.Bone("LeftLeg"), ft = pb.Bone("LeftFoot"), toe = pb.Bone("LeftToeBase");
                    if (kn == null || up == null || ft == null) { log.AppendLine(letter + " " + poses[pi] + "：脚の骨がない"); Destroy(go); if (chairGo != null) Destroy(chairGo); Destroy(floor); continue; }
                    string pre = letter + "_" + poses[pi] + "_";
                    log.AppendLine(letter + " " + poses[pi] + "：" + pb.KneeInfo);
                    foreach (var on in new[] { true, false })
                    {
                        ProtoBody.KneeOn = on;
                        yield return Frames(3);   // 形のキーは次のコマの描画で出る
                        Vector3 k = kn.position;
                        Vector3 mid = (up.position + k) * 0.5f;
                        Vector3 tip = toe != null ? toe.position : ft.position;
                        Vector3 c2 = (mid + tip) * 0.5f;
                        float span = (mid - tip).magnitude;
                        Vector3 side = -tr.right, back = -tr.forward, fwd = tr.forward;
                        var views = new[] { ("真横", side), ("斜め後ろ", (side + back).normalized), ("斜め前", (side + fwd).normalized) };
                        string tag = on ? "on" : "off";
                        foreach (var (vn, vd) in views)
                        {
                            Shot(k + vd * 0.55f + Vector3.up * 0.05f, k, pre + "ひざ_" + vn + "_" + tag);
                            Shot(c2 + vd * (span * 1.9f + 0.25f) + Vector3.up * 0.08f, c2, pre + "脚_" + vn + "_" + tag);
                        }
                        log.AppendLine("  " + tag + "：" + pb.KneeText());
                    }
                    ProtoBody.KneeOn = true;
                    Destroy(go); if (chairGo != null) Destroy(chairGo); Destroy(floor);
                    yield return null;
                }
            }
            ProtoBody.KneeOn = keepOn;
            File.WriteAllText(Path.Combine(dir, "数値.txt"), log.ToString(), Encoding.UTF8);
            File.WriteAllText(Path.Combine(dir, "済み.txt"), System.DateTime.Now.ToString());
            Destroy(cam.gameObject); Destroy(lamp.gameObject); rt.Release();
            Busy = false;
            Destroy(gameObject);
        }
    }
}
