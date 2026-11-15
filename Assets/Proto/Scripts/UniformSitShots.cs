using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 確認用（2026-11-13 共通制服のチャット）：座ったときに服（上着・スカート・シャツ）が椅子の座面や床を突き抜けていないか。
    /// キャラごとに、ゲームと同じ椅子（Furn_Chair）に、そのキャラの椅子の座り方（MotionBook.ChairSit）で座らせて撮り、
    /// 次に床に、そのキャラの床の座り方（MotionBook.FloorSit）で座らせて撮る。座面・床より下に出た服の点の数と深さを 数値.txt に書く。
    /// 保存先：仮モデル\Unity確認\制服座り_（Tag）\。命令 uniformsitshots [記号,記号…] [Tag]。ゲームの進行には影響しない。
    /// </summary>
    public class UniformSitShots : MonoBehaviour
    {
        public static string Letters = "I,Q,R";
        public static string Tag = "now";
        const int PhotoLayer = 31;

        IEnumerator Start()
        {
            string dir = Path.Combine(ProtoPhotoBooth.OutDir, "制服座り_" + Tag);
            Directory.CreateDirectory(dir);
            var cam = new GameObject("UniSitCam").AddComponent<Camera>();
            cam.enabled = false; cam.fieldOfView = 30f; cam.nearClipPlane = 0.03f;
            cam.cullingMask = 1 << PhotoLayer; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.3f, 0.32f, 0.36f);
            var rt = new RenderTexture(480, 480, 24); cam.targetTexture = rt;
            var lamp = new GameObject("UniSitLamp").AddComponent<Light>(); lamp.type = LightType.Point; lamp.range = 10f; lamp.intensity = 1.5f;
            var log = new StringBuilder();
            int li = 0;
            foreach (var raw in Letters.Split(','))
            {
                string letter = raw.Trim().ToUpperInvariant();
                if (letter.Length == 0) continue;
                if (Resources.Load<GameObject>("Chars/" + letter + "/Char" + letter) == null) { log.AppendLine(letter + "：モデルがない"); continue; }
                Vector3 basePos = new Vector3(400f + li * 6f, 0f, 160f);
                li++;
                for (int mode = 0; mode < 2; mode++)
                {
                    bool chair = mode == 0;
                    string clipName = chair ? MotionBook.ChairSit(letter, false) : MotionBook.FloorSit(letter, false);
                    // 床（どちらも置く。床に座るときは床の上を見る）
                    var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    floor.name = "UniSitFloor"; floor.layer = PhotoLayer;
                    floor.transform.position = basePos + new Vector3(chair ? 0f : 3f, -0.05f, 0f); floor.transform.localScale = new Vector3(2.4f, 0.1f, 2.4f);
                    Destroy(floor.GetComponent<Collider>());
                    floor.GetComponent<Renderer>().material.color = new Color(0.45f, 0.42f, 0.38f);
                    Vector3 pos = basePos + new Vector3(chair ? 0f : 3f, 0f, 0f);
                    GameObject chairGo = null; float seatY = 0f;
                    if (chair)
                    {
                        var pf = Resources.Load<GameObject>("Furniture/Furn_Chair");
                        if (pf != null)
                        {
                            chairGo = Instantiate(pf); chairGo.transform.position = pos; chairGo.transform.rotation = pf.transform.localRotation;
                        }
                        else
                        {
                            chairGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                            chairGo.transform.position = pos + Vector3.up * 0.45f; chairGo.transform.localScale = new Vector3(0.48f, 0.06f, 0.48f);
                        }
                        foreach (var c in chairGo.GetComponentsInChildren<Collider>(true)) Destroy(c);
                        foreach (var mf in chairGo.GetComponentsInChildren<MeshFilter>(true)) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                        foreach (var r in chairGo.GetComponentsInChildren<Renderer>(true)) r.gameObject.layer = PhotoLayer;
                        yield return new WaitForFixedUpdate();
                        Physics.SyncTransforms();
                        RaycastHit hit;
                        seatY = Physics.Raycast(pos + Vector3.up * 1.0f, Vector3.down, out hit, 1.0f) ? hit.point.y - pos.y : 0.48f;
                    }
                    var go = new GameObject("UniSit_" + letter + (chair ? "_椅子" : "_床"));
                    go.transform.position = pos;
                    var pb = ProtoBody.Create(go, CharLook.Get(letter, false, Color.gray), letter);
                    if (pb == null) { Destroy(go); if (chairGo != null) Destroy(chairGo); Destroy(floor); continue; }
                    pb.testMode = true; pb.testLetter = letter; pb.testOutfit = "Uniform"; pb.testFace = ""; pb.testSpeed = 0f;
                    pb.testPose = chair ? BodyPose.Sit : BodyPose.SitFloor;
                    if (chair) pb.testClip = clipName; else pb.testSitFloorClip = clipName;
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.gameObject.layer = PhotoLayer;
                    for (float w = 0f; w < 2.0f; w += Time.deltaTime) yield return null;   // 座りの補正と揺れが落ち着くまで
                    var tr = go.transform;
                    var hp = pb.Bone("Hips");
                    float hipY = hp != null ? hp.position.y - pos.y : 0f;
                    // 服の点が座面・床より下に出ていないか（椅子は座面の広さの中だけ）
                    float plane = chair ? seatY : 0f;
                    log.AppendLine(letter + " " + (chair ? "椅子" : "床") + "（" + clipName + "）：腰の高さ " + (hipY * 100f).ToString("0.0") + "cm" + (chair ? "・座面 " + (seatY * 100f).ToString("0.0") + "cm" : ""));
                    foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        string n = smr.name;
                        if (!(n.Contains("Jacket") || n.Contains("Skirt") || n.Contains("Shirt") || n.StartsWith("Body"))) continue;
                        var m = new Mesh(); smr.BakeMesh(m, true);
                        var M = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                        var vs = m.vertices; int below = 0; float minY = 99f;
                        foreach (var v in vs)
                        {
                            var p = M.MultiplyPoint3x4(v) - pos;
                            if (chair) { var lp = Quaternion.Inverse(tr.rotation) * p; if (Mathf.Abs(lp.x) > 0.22f || Mathf.Abs(lp.z) > 0.22f) continue; }
                            if (p.y < minY) minY = p.y;
                            if (p.y < plane - 0.003f) below++;
                        }
                        Destroy(m);
                        log.AppendLine("  " + n + "：いちばん低い所 " + (minY < 98f ? ((minY - plane) * 100f).ToString("0.0") + "cm" : "なし") + "・下に出た点 " + below + "／" + vs.Length + "・補正 " + KeyInfo(smr));
                    }
                    DumpPose(Path.Combine(dir, "姿勢_" + letter + "_" + (chair ? "椅子" : "床") + ".json"), go, clipName);
                    Vector3 c0 = (hp != null ? hp.position : pos + Vector3.up * 0.5f);
                    string pre = Path.Combine(dir, letter + "_" + (chair ? "椅子" : "床") + "_");
                    string[] names = { "前", "右", "後ろ", "左" };
                    for (int i = 0; i < 4; i++)
                    {
                        Vector3 d = Quaternion.AngleAxis(i * 90f, Vector3.up) * tr.forward;
                        SwayShots.Shot(cam, rt, lamp, c0 + d * 1.6f + Vector3.up * 0.25f, c0 - Vector3.up * 0.1f, pre + i + names[i] + ".png");
                    }
                    if (chair)
                    {
                        // 座面の下から（服が座面を突き抜けていれば見える）。椅子の脚の間・後ろ下から見上げる
                        Vector3 seatC = pos + Vector3.up * seatY;
                        SwayShots.Shot(cam, rt, lamp, seatC - tr.forward * 0.9f - Vector3.up * 0.35f, seatC, pre + "4後ろ下.png");
                        SwayShots.Shot(cam, rt, lamp, seatC + tr.right * 0.9f - Vector3.up * 0.3f, seatC, pre + "5横下.png");
                        SwayShots.Shot(cam, rt, lamp, seatC - tr.forward * 0.7f + tr.right * 0.5f + Vector3.up * 0.05f, seatC, pre + "6後ろ斜め.png");
                    }
                    else
                    {
                        SwayShots.Shot(cam, rt, lamp, c0 - tr.forward * 1.0f + tr.right * 0.6f + Vector3.up * 0.0f, c0 - Vector3.up * 0.15f, pre + "4後ろ低い.png");
                        SwayShots.Shot(cam, rt, lamp, c0 + tr.right * 1.1f - Vector3.up * 0.1f, c0 - Vector3.up * 0.15f, pre + "5横低い.png");
                        SwayShots.Shot(cam, rt, lamp, c0 + tr.forward * 1.0f - tr.right * 0.5f + Vector3.up * 0.0f, c0 - Vector3.up * 0.15f, pre + "6前低い.png");
                    }
                    Destroy(go); if (chairGo != null) Destroy(chairGo); Destroy(floor);
                    yield return null;
                }
            }
            File.WriteAllText(Path.Combine(dir, "数値.txt"), log.ToString(), Encoding.UTF8);
            File.WriteAllText(Path.Combine(dir, "済み.txt"), System.DateTime.Now.ToString());
            Destroy(cam.gameObject); Destroy(lamp.gameObject); rt.Release();
            Destroy(gameObject);
        }

        // その時の骨の向き・位置と、骨の休みの向き（bindposes から）、体と服の点の位置（体の根もとから見た世界の向き）を書き出す。
        // Blender で同じ姿勢にして服の座る形を直すのに使う（2026-11-13 共通制服）
        static string F(float v) { return v.ToString("0.#####", System.Globalization.CultureInfo.InvariantCulture); }
        static string V3(Vector3 v) { return "[" + F(v.x) + "," + F(v.y) + "," + F(v.z) + "]"; }
        static string Q4(Quaternion q) { return "[" + F(q.x) + "," + F(q.y) + "," + F(q.z) + "," + F(q.w) + "]"; }
        static void DumpPose(string path, GameObject go, string clipName)
        {
            var root = go.transform;
            var sb = new StringBuilder("{\"clip\":\"" + clipName + "\",\"root\":" + V3(root.position) + ",\"rootq\":" + Q4(root.rotation) + ",\"bones\":{");
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
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                string n = smr.name;
                if (!(n.Contains("Jacket") || n.Contains("Skirt") || n.Contains("Shirt") || (n.StartsWith("Body") && !n.Contains("Neck")))) continue;
                var m = new Mesh(); smr.BakeMesh(m, true);
                var M = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                var vs = m.vertices; Destroy(m);
                sb.Append((first ? "" : ",") + "\"" + n + "\":[");
                for (int i = 0; i < vs.Length; i++) { var p = M.MultiplyPoint3x4(vs[i]) - root.position; sb.Append((i > 0 ? "," : "") + p.x.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) + "," + p.y.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) + "," + p.z.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)); }
                sb.Append("]"); first = false;
            }
            sb.Append("}}");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        static string KeyInfo(SkinnedMeshRenderer smr)
        {
            var mesh = smr.sharedMesh; if (mesh == null) return "";
            var sb = new StringBuilder();
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                string k = mesh.GetBlendShapeName(i);
                if (!k.StartsWith("Corrective_Skirt")) continue;
                sb.Append(k + "=" + smr.GetBlendShapeWeight(i).ToString("0") + " ");
            }
            return sb.ToString();
        }
    }
}
