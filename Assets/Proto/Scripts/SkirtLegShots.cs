using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 確認用（2026-11-01 本体のチャット）：スカートと脚の当たり判定の、足す前・足した後の見比べ。
    /// 1人につき2体（前＝当たり判定なし／後＝あり）を並べて同じ動きを同時に流し、0.25秒ごとに横と斜め前から（腰より少し上から見下ろして）撮る。
    /// 動き：歩く（1.2m/秒で前へ進む）・喜ぶ（Joy）・話す（Talk）・座る（Sit）・立ち上がる（座った姿勢から立つ）。
    /// 保存先：仮モデル\Unity確認\スカート脚_（Tag）\（記号）_（動き）_（横/斜め）_（前/後）_（番号）.jpg と 数値.csv。命令 skirtlegshots [記号,記号…] [Tag]。
    /// ゲームの進行には影響しない。
    /// </summary>
    public class SkirtLegShots : MonoBehaviour
    {
        public static string Letters = "R,I";
        public static string Tag = "now";
        const int PhotoLayer = 31;
        const float ShotEvery = 0.25f;

        class Motion { public string name; public float dur; public BodyPose pose; public float speed; public string clip; }

        IEnumerator Start()
        {
            string dir = Path.Combine(ProtoPhotoBooth.OutDir, "スカート脚_" + Tag);
            Directory.CreateDirectory(dir);
            var cam = new GameObject("SkirtLegCam").AddComponent<Camera>();
            cam.enabled = false; cam.fieldOfView = 30f; cam.nearClipPlane = 0.05f;
            cam.cullingMask = 1 << PhotoLayer; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.3f, 0.32f, 0.36f);
            var rt = new RenderTexture(360, 480, 24); cam.targetTexture = rt;
            var lamp = new GameObject("SkirtLegLamp").AddComponent<Light>(); lamp.type = LightType.Point; lamp.range = 10f; lamp.intensity = 3f;
            var motions = new[]
            {
                new Motion { name = "歩く", dur = 2.5f, pose = BodyPose.Stand, speed = 1.2f },
                new Motion { name = "喜ぶ", dur = 2.5f, pose = BodyPose.Stand, clip = "Joy" },
                new Motion { name = "話す", dur = 2.5f, pose = BodyPose.Stand, clip = "Talk" },
                new Motion { name = "座る", dur = 2.0f, pose = BodyPose.Sit },
                new Motion { name = "立ち上がる", dur = 1.5f, pose = BodyPose.Stand },
            };
            var csv = new StringBuilder("キャラ,動き,秒,前・はみ出し最大(cm),前・はみ出し合計(cm),前・スカートの角度(度),後・はみ出し最大(cm),後・はみ出し合計(cm),後・スカートの角度(度),スカートの骨,脚の筒,座りの補正\n");
            var sum = new StringBuilder("キャラ,動き,前・はみ出し最大の平均(cm),後・はみ出し最大の平均(cm),前・合計の平均(cm),後・合計の平均(cm),前・角度の平均(度),後・角度の平均(度)\n");
            int li = 0;
            foreach (var raw in Letters.Split(','))
            {
                string letter = raw.Trim().ToUpperInvariant();
                if (letter.Length == 0) continue;
                if (Resources.Load<GameObject>("Chars/" + letter + "/Char" + letter) == null) { sum.Append(letter + ",モデルがない\n"); continue; }
                var pbs = new ProtoBody[2];
                for (int k = 0; k < 2; k++)
                {
                    var go = new GameObject("SkirtLeg_" + letter + (k == 0 ? "_前" : "_後"));
                    go.transform.position = new Vector3(240f + li * 8f + k * 4f, 0f, 120f);
                    var pb = ProtoBody.Create(go, CharLook.Get(letter, false, Color.gray), letter);
                    if (pb == null) { Destroy(go); continue; }
                    pb.testMode = true; pb.testPose = BodyPose.Stand; pb.testOutfit = "Uniform"; pb.testFace = ""; pb.testSpeed = 0f; pb.testLetter = letter;
                    pb.testNoLegHit = k == 0;
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.gameObject.layer = PhotoLayer;
                    pbs[k] = pb;
                }
                li++;
                if (pbs[0] == null || pbs[1] == null) { foreach (var pb in pbs) if (pb != null) Destroy(pb.gameObject); continue; }
                for (int w = 0; w < 10; w++) yield return null;
                foreach (var m in motions)
                {
                    foreach (var pb in pbs)
                    {
                        pb.testPose = m.pose; pb.testSpeed = m.speed; pb.testClip = m.clip;
                        if (m.name == "歩く") pb.transform.position = new Vector3(pb.transform.position.x, 0f, 120f);
                    }
                    yield return null; yield return null;
                    if (m.name != "立ち上がる") foreach (var pb in pbs) pb.TestSetClipTime(0f);   // 2体の動きをそろえる
                    float t = 0f, next = 0f; int shot = 0;
                    float[] mxSum = new float[2], totSum = new float[2], angSum = new float[2]; int nRow = 0;
                    while (t < m.dur)
                    {
                        float dt = Time.deltaTime;
                        t += dt;
                        if (m.speed > 0f) foreach (var pb in pbs) pb.transform.position += pb.transform.forward * m.speed * dt;
                        var row = new StringBuilder(letter + "," + m.name + "," + t.ToString("0.000"));
                        string extra = "";
                        for (int k = 0; k < 2; k++)
                        {
                            var a = pbs[k].SkirtLegDebug.Split(',');
                            row.Append("," + a[0] + "," + a[1] + "," + a[2]);
                            float f;
                            if (float.TryParse(a[0], out f)) mxSum[k] += f;
                            if (float.TryParse(a[1], out f)) totSum[k] += f;
                            if (float.TryParse(a[2], out f)) angSum[k] += f;
                            extra = "," + a[3] + "," + a[4] + "," + a[5];
                        }
                        csv.Append(row).Append(extra).Append("\n");
                        nRow++;
                        if (t >= next)
                        {
                            next += ShotEvery;
                            for (int k = 0; k < 2; k++)
                            {
                                var tr = pbs[k].transform;
                                var hp = pbs[k].Bone("Hips");
                                Vector3 c = (hp != null ? hp.position : tr.position + Vector3.up * 0.8f) - Vector3.up * 0.25f;
                                string pre = Path.Combine(dir, letter + "_" + m.name + "_");
                                string ba = k == 0 ? "前" : "後";
                                SwayShots.Shot(cam, rt, lamp, c + tr.right * 2.2f + Vector3.up * 0.55f, c, pre + "横_" + ba + "_" + shot.ToString("00") + ".jpg");
                                SwayShots.Shot(cam, rt, lamp, c + (tr.forward * 0.9f - tr.right * 0.45f).normalized * 2.2f + Vector3.up * 0.55f, c, pre + "斜め_" + ba + "_" + shot.ToString("00") + ".jpg");
                            }
                            shot++;
                        }
                        yield return null;
                    }
                    if (nRow > 0)
                        sum.Append(letter + "," + m.name + "," + (mxSum[0] / nRow).ToString("0.00") + "," + (mxSum[1] / nRow).ToString("0.00") + "," + (totSum[0] / nRow).ToString("0.00") + "," + (totSum[1] / nRow).ToString("0.00") + "," + (angSum[0] / nRow).ToString("0.0") + "," + (angSum[1] / nRow).ToString("0.0") + "\n");
                }
                foreach (var pb in pbs) Destroy(pb.gameObject);
            }
            File.WriteAllText(Path.Combine(dir, "数値.csv"), csv.ToString(), Encoding.UTF8);
            File.WriteAllText(Path.Combine(dir, "まとめ.csv"), sum.ToString(), Encoding.UTF8);
            File.WriteAllText(Path.Combine(dir, "済み.txt"), System.DateTime.Now.ToString());
            Destroy(cam.gameObject); Destroy(lamp.gameObject); rt.Release();
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 確認用（2026-11-01 本体）：重さを測るための見本。スカートのキャラを12人、カメラに関係なく揺れを計算する形で歩かせ、Sec 秒で消える。
    /// 命令 skirtlegbench。この間に perf と skirtleg on/off で、脚の当たり判定の重さを比べる。
    /// </summary>
    public class SkirtLegBench : MonoBehaviour
    {
        public static float Sec = 60f;
        IEnumerator Start()
        {
            var list = new List<GameObject>();
            string[] letters = { "R", "I", "G", "J", "Q", "K", "B" };
            int n = 0;
            for (int i = 0; i < 12; i++)
            {
                string l = letters[i % letters.Length];
                if (Resources.Load<GameObject>("Chars/" + l + "/Char" + l) == null) continue;
                var go = new GameObject("SkirtLegBench_" + i + "_" + l);
                go.transform.position = new Vector3(300f + (i % 4) * 2f, 0f, 120f + (i / 4) * 2f);
                var pb = ProtoBody.Create(go, CharLook.Get(l, false, Color.gray), l);
                if (pb == null) { Destroy(go); continue; }
                pb.testMode = true; pb.testPose = BodyPose.Stand; pb.testOutfit = "Uniform"; pb.testFace = ""; pb.testSpeed = (i % 3 == 2) ? 0f : 1.2f; pb.testLetter = l;
                list.Add(go); n++;
            }
            float t = 0f;
            while (t < Sec) { t += Time.deltaTime; yield return null; }
            foreach (var go in list) if (go != null) Destroy(go);
            Destroy(gameObject);
        }
    }
}
