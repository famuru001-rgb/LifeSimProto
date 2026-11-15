using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 確認用（2026-11-14 取り込みのチャット）：立ち姿・歩く姿勢の書き出し。撮影用の体だけを作って動かす（住人・セーブは触らない）。
    /// CloseShots（closeshots）と同じ順・同じコマ数で動かし、「立つ_胸の下」「歩く_胸の下_前_0」を撮ったのと同じ時点で、
    /// 写真（同じ向き）と 姿勢_（記号）_立つ.json・姿勢_（記号）_歩く.json（UniformSitShots の DumpPose と同じ形式＋形のキーの強さ・動きの名前）を書く。
    /// 合図：Logs\posedump_request.txt に「（記号） （保存先：Unity確認からの相対）」（Editor\PoseDumpTrigger.cs）。
    /// </summary>
    public class PoseDumpShots : MonoBehaviour
    {
        public static string Letter = "H";
        public static string SubDir = "姿勢_H";
        const int PhotoLayer = 31;
        Camera cam; RenderTexture rt; Light lamp; string dir;
        readonly StringBuilder log = new StringBuilder();

        ProtoBody Make(string name, int i)
        {
            var go = new GameObject("PoseDump_" + name);
            go.transform.position = new Vector3(60f + i * 4f, 0f, 230f);
            var pb = ProtoBody.Create(go, CharLook.Get(Letter, Letter == "F", Color.gray), Letter);
            if (pb == null) { Destroy(go); return null; }
            pb.testMode = true; pb.testLetter = Letter; pb.testPose = BodyPose.Stand; pb.testOutfit = "Uniform"; pb.testFace = "";
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.gameObject.layer = PhotoLayer;
            return pb;
        }

        Transform B(ProtoBody pb, params string[] names)
        {
            foreach (var n in names) { var t = pb.Bone(n); if (t != null) return t; }
            return null;
        }

        void S(Vector3 from, Vector3 at, string file) { SwayShots.Shot(cam, rt, lamp, from, at, Path.Combine(dir, file + ".png")); }
        IEnumerator Wait(int frames) { for (int i = 0; i < frames; i++) yield return null; }

        IEnumerator Start()
        {
            dir = Path.Combine(ProtoPhotoBooth.OutDir, SubDir);
            Directory.CreateDirectory(dir);
            cam = new GameObject("PoseDumpCam").AddComponent<Camera>();
            cam.enabled = false; cam.fieldOfView = 30f; cam.nearClipPlane = 0.02f;
            cam.cullingMask = 1 << PhotoLayer; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.3f, 0.32f, 0.36f);
            rt = new RenderTexture(600, 600, 24); cam.targetTexture = rt;
            lamp = new GameObject("PoseDumpLamp").AddComponent<Light>(); lamp.type = LightType.Point; lamp.range = 10f; lamp.intensity = 1.0f;
            float keepCapture = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 30f;
            log.AppendLine(System.DateTime.Now + " " + Letter + "（撮影用の体だけ。住人・セーブは触らない。1コマ 1/30 秒）");

            // 立つ：CloseShots と同じ（30コマ待つ→目3コマ×2→3コマ）のあとの時点
            var pb = Make("立つ", 0);
            if (pb != null)
            {
                yield return Wait(30);
                pb.testEyesClosed = false; yield return Wait(3);
                pb.testEyesClosed = true; yield return Wait(3);
                pb.testEyesClosed = false; yield return Wait(3);
                var tr = pb.transform;
                Transform sc = B(pb, "UpperChest", "Chest", "Spine2", "Spine1");
                if (sc != null)
                {
                    Vector3 c = sc.position - tr.up * 0.08f;
                    S(c + tr.forward * 0.6f - tr.up * 0.15f, c, "立つ_胸の下_前");
                    S(c + (tr.forward - tr.right).normalized * 0.6f - tr.up * 0.15f, c, "立つ_胸の下_左前");
                    S(c + (tr.forward + tr.right).normalized * 0.6f - tr.up * 0.15f, c, "立つ_胸の下_右前");
                    S(c + tr.forward * 0.5f - tr.up * 0.4f, c, "立つ_胸の下_低い前");
                }
                Dump(pb, "立つ");
                Destroy(pb.gameObject);
            }

            // 歩く：CloseShots と同じ（速さ 1.4・30コマ待つ）の最初のこま（歩く_胸の下_前_0）
            pb = Make("歩く", 1);
            if (pb != null)
            {
                pb.testSpeed = 1.4f;
                yield return Wait(30);
                var tr = pb.transform;
                Transform ch = B(pb, "UpperChest", "Chest", "Spine2", "Spine1");
                if (ch != null)
                {
                    Vector3 c = ch.position - tr.up * 0.08f;
                    S(c + tr.forward * 0.6f - tr.up * 0.15f, c, "歩く_胸の下_前_0");
                    S(c + (tr.forward - tr.right).normalized * 0.6f - tr.up * 0.15f, c, "歩く_胸の下_左前_0");
                }
                Dump(pb, "歩く");
                Destroy(pb.gameObject);
            }

            // 待機の別のコマ（2026-11-14 17:03）：立つを 8 回、1 秒ごとに胸の下（前・左前・低い前）。動きの名前と時間も数値.txt に
            pb = Make("待機", 2);
            if (pb != null)
            {
                // 2026-11-15：胸の揺れの骨（Spring_ChestL_1・R_1）が休みの向き（作った直後の向き）から何度回ったかを毎コマ測る
                var chestBones = new System.Collections.Generic.List<Transform>();
                var chestRest = new System.Collections.Generic.List<Quaternion>();
                foreach (var t in pb.GetComponentsInChildren<Transform>(true))
                    if (t.name == "Spring_ChestL_1" || t.name == "Spring_ChestR_1") { chestBones.Add(t); chestRest.Add(t.localRotation); }
                var chestMax = new float[chestBones.Count]; var chestMaxAt = new string[chestBones.Count];
                float shotMax = 0f; int frameNo = 0;
                System.Func<int, float> ang = (i) => Quaternion.Angle(chestBones[i].localRotation, chestRest[i]);
                yield return Wait(30);
                var tr = pb.transform;
                for (int f = 0; f < 8; f++)
                {
                    string now = "";
                    for (int i = 0; i < chestBones.Count; i++) now += chestBones[i].name + " " + ang(i).ToString("0.0") + "度 ";
                    log.AppendLine("待機 " + f + " の胸の揺れ：" + now);
                    Transform sc = B(pb, "UpperChest", "Chest", "Spine2", "Spine1");
                    if (sc != null)
                    {
                        Vector3 c = sc.position - tr.up * 0.08f;
                        S(c + tr.forward * 0.6f - tr.up * 0.15f, c, "待機_" + f + "_前");
                        S(c + (tr.forward - tr.right).normalized * 0.6f - tr.up * 0.15f, c, "待機_" + f + "_左前");
                        S(c + tr.forward * 0.5f - tr.up * 0.4f, c, "待機_" + f + "_低い前");
                    }
                    log.AppendLine("待機 " + f + "：" + pb.DebugClipNow());
                    for (int w = 0; w < 30; w++)
                    {
                        yield return new WaitForEndOfFrame(); frameNo++;   // 揺れの計算（LateUpdate）が終わったあとで測る
                        float big0 = 0f;
                        for (int i = 0; i < chestBones.Count; i++) { float a = ang(i); big0 = Mathf.Max(big0, a); if (a > chestMax[i]) { chestMax[i] = a; chestMaxAt[i] = "待機の " + (frameNo / 30f).ToString("0.00") + "秒・" + pb.DebugClipNow(); } }
                        if (big0 > shotMax + 0.5f)
                        {
                            shotMax = big0;
                            Transform sc0 = B(pb, "UpperChest", "Chest", "Spine2", "Spine1");
                            if (sc0 != null)
                            {
                                Vector3 c0 = sc0.position - tr.up * 0.08f;
                                S(c0 + tr.forward * 0.6f - tr.up * 0.15f, c0, "待機_最大_前");
                                S(c0 + (tr.forward - tr.right).normalized * 0.6f - tr.up * 0.15f, c0, "待機_最大_左前");
                                S(c0 + tr.forward * 0.5f - tr.up * 0.4f, c0, "待機_最大_低い前");
                            }
                        }
                    }
                }
                // そのあと 10 秒：いちばん大きく回ったコマを撮る（待機_最大_…。前より大きくなるたびに撮り直す）
                for (int w = 0; w < 1700; w++)   // 待機の動き（R_HeadTilts 55秒）が1回まわる長さ（くり返しのつなぎ目も入る）
                {
                    yield return new WaitForEndOfFrame(); frameNo++;   // 揺れの計算（LateUpdate）が終わったあとで測る
                    float big = 0f;
                    for (int i = 0; i < chestBones.Count; i++) { float a = ang(i); big = Mathf.Max(big, a); if (a > chestMax[i]) { chestMax[i] = a; chestMaxAt[i] = "待機の " + (frameNo / 30f).ToString("0.00") + "秒・" + pb.DebugClipNow(); } }
                    if (big > shotMax)
                    {
                        shotMax = big;
                        Transform sc = B(pb, "UpperChest", "Chest", "Spine2", "Spine1");
                        if (sc != null)
                        {
                            Vector3 c = sc.position - tr.up * 0.08f;
                            S(c + tr.forward * 0.6f - tr.up * 0.15f, c, "待機_最大_前");
                            S(c + (tr.forward - tr.right).normalized * 0.6f - tr.up * 0.15f, c, "待機_最大_左前");
                            S(c + tr.forward * 0.5f - tr.up * 0.4f, c, "待機_最大_低い前");
                        }
                    }
                }
                for (int i = 0; i < chestBones.Count; i++) log.AppendLine("胸の揺れの最大：" + chestBones[i].name + " " + chestMax[i].ToString("0.0") + "度（" + chestMaxAt[i] + "）");
                log.AppendLine("待機_最大_… の写真は、待機の約 65 秒の中で左右の大きいほうがいちばん大きかったコマ（" + shotMax.ToString("0.0") + "度）");
                Destroy(pb.gameObject);
            }
            // 床に座る・椅子に座る（いすなし）：胸の下〜おなか（前・左前）
            foreach (var floorSit in new[] { false, true })
            {
                pb = Make(floorSit ? "床" : "椅子", floorSit ? 4 : 3);
                if (pb == null) continue;
                pb.testPose = floorSit ? BodyPose.SitFloor : BodyPose.Sit;
                yield return Wait(150);   // 床は座りこむ動き（約3秒）が終わるまで
                var tr = pb.transform;
                Transform sp = B(pb, "Spine", "Hips");
                Transform ch = B(pb, "UpperChest", "Chest", "Spine2", "Spine1");
                if (ch != null && sp != null)
                {
                    Vector3 c = (ch.position + sp.position) * 0.5f;
                    string n = floorSit ? "床" : "椅子";
                    S(c + tr.forward * 0.65f, c, n + "_胸の下_前");
                    S(c + (tr.forward - tr.right).normalized * 0.65f, c, n + "_胸の下_左前");
                    S(c + tr.forward * 0.6f - tr.up * 0.3f, c, n + "_胸の下_低い前");
                }
                log.AppendLine((floorSit ? "床" : "椅子（いすなし）") + "：" + pb.DebugClipNow());
                Destroy(pb.gameObject);
            }

            Time.captureDeltaTime = keepCapture;
            log.AppendLine("揺れの骨（Spring_…）は止めていない。書き出しは、そのコマの揺れの計算が終わったあとの向き（写真と同じ時点）");
            File.WriteAllText(Path.Combine(dir, "数値.txt"), log.ToString(), new UTF8Encoding(true));
            File.WriteAllText(Path.Combine(dir, "済み.txt"), System.DateTime.Now.ToString());
            Destroy(cam.gameObject); Destroy(lamp.gameObject); rt.Release();
            Destroy(gameObject);
        }

        static string F(float v) { return v.ToString("0.#####", System.Globalization.CultureInfo.InvariantCulture); }
        static string V3(Vector3 v) { return "[" + F(v.x) + "," + F(v.y) + "," + F(v.z) + "]"; }
        static string Q4(Quaternion q) { return "[" + F(q.x) + "," + F(q.y) + "," + F(q.z) + "," + F(q.w) + "]"; }

        // UniformSitShots.DumpPose と同じ中身（clip・root・rootq・bones{p,q,bp,bq}・verts）に、keys（形のキーの強さ）と spring（揺れを止めていないか）を足した
        void Dump(ProtoBody pb, string name)
        {
            var go = pb.gameObject; var root = go.transform;
            string clip = pb.DebugClipNow();
            var sb = new StringBuilder("{\"clip\":\"" + clip.Replace("\"", "'") + "\",\"root\":" + V3(root.position) + ",\"rootq\":" + Q4(root.rotation) + ",\"bones\":{");
            var done = new HashSet<string>(); bool first = true;
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = smr.sharedMesh; if (mesh == null) continue;
                var bp = mesh.bindposes; var bs = smr.bones;
                for (int i = 0; i < bs.Length && i < bp.Length; i++)
                {
                    var b = bs[i]; if (b == null || done.Contains(b.name)) continue;
                    done.Add(b.name);
                    var bind = smr.transform.localToWorldMatrix * bp[i].inverse;
                    sb.Append((first ? "" : ",") + "\"" + b.name + "\":{\"p\":" + V3(b.position - root.position) + ",\"q\":" + Q4(b.rotation) + ",\"bp\":" + V3((Vector3)bind.GetColumn(3) - root.position) + ",\"bq\":" + Q4(bind.rotation) + "}");
                    first = false;
                }
            }
            sb.Append("},\"verts\":{");
            first = true;
            var keys = new StringBuilder(); bool kf = true;
            log.AppendLine(name + "：" + clip + "｜骨 " + done.Count + "本");
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                string n = smr.name;
                var mesh = smr.sharedMesh;
                if (mesh != null)
                    for (int i = 0; i < mesh.blendShapeCount; i++)
                    {
                        string k = mesh.GetBlendShapeName(i); float w = smr.GetBlendShapeWeight(i);
                        keys.Append((kf ? "" : ",") + "\"" + n + "/" + k + "\":" + F(w)); kf = false;
                        if (w != 0f || k == "Corrective_Arm_Down") log.AppendLine("  形のキー " + n + " / " + k + " = " + w.ToString("0.##") + "％" + (smr.gameObject.activeInHierarchy ? "" : "（非表示）"));
                    }
                if (!(n.Contains("Jacket") || n.Contains("Skirt") || n.Contains("Shirt") || (n.StartsWith("Body") && !n.Contains("Neck")))) continue;
                if (!smr.gameObject.activeInHierarchy) continue;
                var m = new Mesh(); smr.BakeMesh(m, true);
                var M = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                var vs = m.vertices; Destroy(m);
                sb.Append((first ? "" : ",") + "\"" + n + "\":[");
                for (int i = 0; i < vs.Length; i++) { var p = M.MultiplyPoint3x4(vs[i]) - root.position; sb.Append((i > 0 ? "," : "") + p.x.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) + "," + p.y.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) + "," + p.z.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)); }
                sb.Append("]"); first = false;
                log.AppendLine("  点 " + n + "：" + vs.Length);
            }
            sb.Append("},\"keys\":{" + keys + "},\"spring\":\"揺れの骨は止めていない（そのコマの揺れの計算のあと）\"}");
            File.WriteAllText(Path.Combine(dir, "姿勢_" + Letter + "_" + name + ".json"), sb.ToString(), new UTF8Encoding(false));
        }
    }
}
