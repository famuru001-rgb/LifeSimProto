using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 確認用（2026-11-25 本体）：目と口の絵の材質（名前に FaceEyes・FaceMouth）を肌の塗り方にした前と後を、同じ瞬間で撮りくらべる。
    /// 「前」は、その材質だけ服と同じ塗り方（シェーダーのふつうの値：影の色・ぼかし 0.05・赤み／ふちの光／ツヤ なし）に一時的にもどして撮る。
    /// 顔の正面・斜め・横・首の前 × 前／後。保存先：仮モデル\Unity確認\顔の肌_（Tag）\（記号）_（向き）_前／後.jpg。命令 faceskinshots 記号[,記号…] Tag。住人・セーブは触らない。
    /// </summary>
    public class FaceSkinShots : MonoBehaviour
    {
        public static string Letters = "M,L,I,F";
        public static string Tag = "f1";
        public static bool Busy;
        public static bool Talk;   // true：驚いた顔で話している（口を開けた絵）で撮る（口の中の色を見る）
        const int PhotoLayer = 31;
        Camera cam; RenderTexture rt; Light lamp; string dir;

        void Shot(Vector3 from, Vector3 at, string file) { SwayShots.Shot(cam, rt, lamp, from, at, Path.Combine(dir, file + ".jpg")); }

        class Keep { public Material m; public Color shade; public float soft, scatter, rim, gloss; }

        IEnumerator Start()
        {
            Busy = true;
            dir = Path.Combine(ProtoPhotoBooth.OutDir, "顔の肌_" + Tag);
            Directory.CreateDirectory(dir);
            cam = new GameObject("FaceSkinCam").AddComponent<Camera>();
            cam.enabled = false; cam.fieldOfView = 30f; cam.nearClipPlane = 0.02f;
            cam.cullingMask = 1 << PhotoLayer; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.3f, 0.32f, 0.36f);
            rt = new RenderTexture(560, 560, 24); cam.targetTexture = rt;
            lamp = new GameObject("FaceSkinLamp").AddComponent<Light>(); lamp.type = LightType.Point; lamp.range = 10f; lamp.intensity = 0.6f;
            var log = new StringBuilder();
            log.AppendLine("目と口の絵の材質の塗り方 " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "（前＝服と同じ塗り方・後＝肌の塗り方）");
            int li = 0;
            foreach (var raw in Letters.Split(','))
            {
                string letter = raw.Trim().ToUpperInvariant();
                if (letter.Length == 0) continue;
                if (Resources.Load<GameObject>("Chars/" + letter + "/Char" + letter) == null) { log.AppendLine(letter + "：モデルがない"); continue; }
                Vector3 pos = new Vector3(480f + li * 4f, 0f, 200f);
                li++;
                var go = new GameObject("FaceSkin_" + letter);
                go.transform.position = pos;
                var pb = ProtoBody.Create(go, CharLook.Get(letter, false, Color.gray), letter);
                if (pb == null) { Destroy(go); continue; }
                pb.testMode = true; pb.testLetter = letter; pb.testOutfit = "Uniform"; pb.testFace = Talk ? "Surprised" : ""; pb.testTalk = Talk; pb.testSpeed = 0f; pb.testPose = BodyPose.Stand;
                var rends = go.GetComponentsInChildren<Renderer>(true);
                foreach (var r in rends) r.gameObject.layer = PhotoLayer;
                for (float w = 0f; w < 2.0f; w += Time.deltaTime) yield return null;   // 揺れが落ち着くまで
                Transform head = pb.Bone("Head");
                if (head == null) { log.AppendLine(letter + "：頭の骨がない"); Destroy(go); continue; }
                var keeps = new List<Keep>();
                var names = new List<string>();
                foreach (var r in rends)
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m == null || m.shader == null || m.shader.name != "LifeSim/Toon") continue;
                        if (!m.name.Contains("FaceEyes") && !m.name.Contains("FaceMouth")) continue;
                        bool dup = false; foreach (var k in keeps) if (k.m == m) dup = true;
                        if (dup) continue;
                        keeps.Add(new Keep { m = m, shade = m.GetColor("_ShadeColor"), soft = m.GetFloat("_ShadeSoftness"), scatter = m.GetFloat("_ScatterStrength"), rim = m.GetFloat("_RimStrength"), gloss = m.GetFloat("_GlossStrength") });
                        names.Add(m.name + "（ぼかし " + m.GetFloat("_ShadeSoftness").ToString("0.00") + "・赤み " + m.GetFloat("_ScatterStrength").ToString("0.00") + "）");
                    }
                log.AppendLine(letter + "：目と口の絵の材質 " + keeps.Count + "　" + string.Join("・", names.ToArray()));
                float keepScale = Time.timeScale;
                Time.timeScale = 0f;   // 同じ瞬間で比べる
                yield return null;
                var tr = go.transform;
                foreach (var after in new[] { false, true })
                {
                    foreach (var k in keeps)
                    {
                        if (after) { k.m.SetColor("_ShadeColor", k.shade); k.m.SetFloat("_ShadeSoftness", k.soft); k.m.SetFloat("_ScatterStrength", k.scatter); k.m.SetFloat("_RimStrength", k.rim); k.m.SetFloat("_GlossStrength", k.gloss); }
                        else { k.m.SetColor("_ShadeColor", new Color(0.78f, 0.70f, 0.76f, 1f)); k.m.SetFloat("_ShadeSoftness", 0.05f); k.m.SetFloat("_ScatterStrength", 0f); k.m.SetFloat("_RimStrength", 0f); k.m.SetFloat("_GlossStrength", 0f); }
                    }
                    string tag = after ? "後" : "前";
                    Vector3 h = head.position + tr.up * 0.06f;   // 顔のまん中くらい
                    Vector3 neck = head.position - tr.up * 0.06f;
                    Shot(h + tr.forward * 0.6f, h, letter + "_正面_" + tag);
                    Shot(h + (tr.forward + tr.right).normalized * 0.6f + tr.up * 0.03f, h, letter + "_斜め_" + tag);
                    Shot(h + tr.right * 0.6f, h, letter + "_横_" + tag);
                    Shot(neck + tr.forward * 0.45f - tr.up * 0.22f, neck + tr.up * 0.03f, letter + "_首の前_" + tag);
                }
                Time.timeScale = keepScale;
                Destroy(go);
                yield return null;
            }
            File.WriteAllText(Path.Combine(dir, "記録.txt"), log.ToString(), Encoding.UTF8);
            File.WriteAllText(Path.Combine(dir, "済み.txt"), System.DateTime.Now.ToString());
            Destroy(cam.gameObject); Destroy(lamp.gameObject); rt.Release();
            Busy = false;
            Destroy(gameObject);
        }
    }
}
