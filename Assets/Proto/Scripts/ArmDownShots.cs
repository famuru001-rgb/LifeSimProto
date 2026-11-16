using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 確認用（2026-11-17 本体 AA02）：腕を下ろす補正（Corrective_Arm_Down）を、ヒロインで「動かさない（前と同じ）」と「動かす」で同じ姿勢・同じ向きから撮る。
    /// 服ごと（制服・下着・水着など、その子が持つ服ぜんぶ）に、立つ（7方向）と歩く（3コマ×4方向）。わき・肩の後ろが見える高さ。
    /// 保存先：仮モデル\Unity確認\腕下げ_（記号）\前\・後\（同じファイル名）と 記録.txt（キーの強さ）。命令 armdown shots（記号）。住人・セーブは触らない。
    /// </summary>
    public class ArmDownShots : MonoBehaviour
    {
        public static string Letter = "H";
        public static bool Busy;
        const int PhotoLayer = 31;
        Camera cam; RenderTexture rt; Light lamp; string dir;
        readonly StringBuilder log = new StringBuilder();

        ProtoBody Make(string name, int i, string outfit)
        {
            var go = new GameObject("ArmDown_" + name);
            go.transform.position = new Vector3(60f + i * 4f, 0f, 236f);
            var pb = ProtoBody.Create(go, CharLook.Get(Letter, Letter == "F", Color.gray), Letter);
            if (pb == null) { Destroy(go); return null; }
            pb.testMode = true; pb.testLetter = Letter; pb.testPose = BodyPose.Stand; pb.testOutfit = outfit; pb.testFace = "";
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.gameObject.layer = PhotoLayer;
            return pb;
        }

        Transform B(ProtoBody pb, params string[] names)
        {
            foreach (var n in names) { var t = pb.Bone(n); if (t != null) return t; }
            return null;
        }

        void S(string sub, Vector3 from, Vector3 at, string file) { SwayShots.Shot(cam, rt, lamp, from, at, Path.Combine(dir, sub, file + ".png")); }

        IEnumerator Wait(int frames) { for (int i = 0; i < frames; i++) yield return null; }

        static readonly string[] StandViews = { "前", "左前", "右前", "後ろ", "左後ろ", "右後ろ", "左横" };
        static readonly string[] WalkViews = { "左前", "後ろ", "右後ろ", "左横" };

        Vector3 Dir(Transform tr, string v)
        {
            Vector3 f = tr.forward, r = tr.right;
            switch (v)
            {
                case "前": return f;
                case "左前": return (f - r).normalized;
                case "右前": return (f + r).normalized;
                case "後ろ": return -f;
                case "左後ろ": return (-f - r).normalized;
                case "右後ろ": return (-f + r).normalized;
                default: return -r;   // 左横
            }
        }

        // 同じ体・同じ瞬間で、補正を「0（前と同じ）」と「動かす」に切りかえて撮る（待機の姿勢はくじで変わるため）。
        // 補正の強さは次のコマの描画で形に出るので、時間を止めて（timeScale 0）2コマ待ってから撮る
        IEnumerator Both(ProtoBody pb, string[] views, string name)
        {
            var tr = pb.transform;
            float keepScale = Time.timeScale;
            Time.timeScale = 0f;
            Transform ch = B(pb, "UpperChest", "Chest", "Spine2", "Spine1");
            Vector3 c = ch != null ? ch.position : tr.position + Vector3.up * 1.25f;
            foreach (var on in new[] { false, true })
            {
                ProtoBody.HeroArmDown = on;
                yield return Wait(2);
                foreach (var v in views) S(on ? "後" : "前", c + Dir(tr, v) * 0.95f + Vector3.up * 0.12f, c, name + "_" + v);
                log.AppendLine((on ? "後 " : "前 ") + name + "：" + pb.ArmDownText());
            }
            ProtoBody.HeroArmDown = true;
            Time.timeScale = keepScale;
        }

        IEnumerator Pass(string outfit, int idx)
        {
            // 立つ
            var pb = Make("立つ_" + outfit, idx * 2, outfit);
            if (pb != null)
            {
                yield return Wait(40);
                yield return Both(pb, StandViews, outfit + "_立つ");
                Destroy(pb.gameObject);
                yield return null;
            }
            // 歩く
            pb = Make("歩く_" + outfit, idx * 2 + 1, outfit);
            if (pb != null)
            {
                pb.testSpeed = 1.4f;
                yield return Wait(30);
                for (int f = 0; f < 3; f++)
                {
                    yield return Both(pb, WalkViews, outfit + "_歩く_" + f);
                    yield return Wait(9);
                }
                Destroy(pb.gameObject);
                yield return null;
            }
        }

        IEnumerator Start()
        {
            Busy = true;
            bool keepFlag = ProtoBody.HeroArmDown;
            dir = Path.Combine(ProtoPhotoBooth.OutDir, "腕下げ_" + Letter);
            Directory.CreateDirectory(Path.Combine(dir, "前"));
            Directory.CreateDirectory(Path.Combine(dir, "後"));
            cam = new GameObject("ArmDownCam").AddComponent<Camera>();
            cam.enabled = false; cam.fieldOfView = 30f; cam.nearClipPlane = 0.02f;
            cam.cullingMask = 1 << PhotoLayer; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.3f, 0.32f, 0.36f);
            rt = new RenderTexture(600, 600, 24); cam.targetTexture = rt;
            lamp = new GameObject("ArmDownLamp").AddComponent<Light>(); lamp.type = LightType.Point; lamp.range = 10f; lamp.intensity = 1.0f;
            float keepCapture = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 30f;

            // 服の一覧（その子が持つ服ぜんぶ）
            var outfits = new List<string>();
            var probe = Make("服を調べる", 0, "Uniform");
            if (probe != null)
            {
                yield return Wait(3);
                outfits = probe.OutfitNames();
                outfits.Sort(System.StringComparer.Ordinal);
                log.AppendLine("服：" + string.Join("・", outfits.ToArray()));
                log.AppendLine("キーを持つメッシュ（Uniform を着た時点）：" + probe.ArmDownText());
                Destroy(probe.gameObject);
                yield return null;
            }
            for (int i = 0; i < outfits.Count; i++)
            {
                yield return Pass(outfits[i], i);
            }

            Time.captureDeltaTime = keepCapture;
            ProtoBody.HeroArmDown = keepFlag;
            File.WriteAllText(Path.Combine(dir, "記録.txt"), log.ToString(), Encoding.UTF8);
            File.WriteAllText(Path.Combine(dir, "済み.txt"), System.DateTime.Now.ToString());
            Destroy(cam.gameObject); Destroy(lamp.gameObject); rt.Release();
            Busy = false;
            Destroy(gameObject);
        }
    }
}
