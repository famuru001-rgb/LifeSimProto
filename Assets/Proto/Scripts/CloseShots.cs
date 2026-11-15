using System.Collections;
using System.IO;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 確認用（2026-11-14 取り込みのチャット）：取り込んだキャラの「寄り」の写真。
    /// 顔（前・斜め45度・横。ふつうと目を閉じる）・首のつなぎ目（前・斜め・後ろ）・立つ／歩くを低い前から・
    /// 歩くときの胸の下（前・左前。0.2秒ごと）・椅子に座ったときの胸の下〜おなか（前・左前）と低い前・手を振る／話すときのそで口。
    /// 保存先：仮モデル\Unity確認\寄り_（記号）\。命令 closeshots（記号）。ゲームの進行には影響しない。
    /// </summary>
    public class CloseShots : MonoBehaviour
    {
        public static string Letter = "L";
        const int PhotoLayer = 31;
        Camera cam; RenderTexture rt; Light lamp; string dir;

        ProtoBody Make(string name, int i)
        {
            var go = new GameObject("Close_" + name);
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
            dir = Path.Combine(ProtoPhotoBooth.OutDir, "寄り_" + Letter);
            Directory.CreateDirectory(dir);
            cam = new GameObject("CloseCam").AddComponent<Camera>();
            cam.enabled = false; cam.fieldOfView = 30f; cam.nearClipPlane = 0.02f;
            cam.cullingMask = 1 << PhotoLayer; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.3f, 0.32f, 0.36f);
            rt = new RenderTexture(600, 600, 24); cam.targetTexture = rt;
            lamp = new GameObject("CloseLamp").AddComponent<Light>(); lamp.type = LightType.Point; lamp.range = 10f; lamp.intensity = 1.0f;
            float keepCapture = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 30f;

            // 1. 顔と首（立つ）
            var pb = Make("立つ", 0);
            if (pb != null)
            {
                yield return Wait(30);
                var tr = pb.transform;
                foreach (var eyes in new[] { false, true })
                {
                    pb.testEyesClosed = eyes;
                    yield return Wait(3);
                    string tag = eyes ? "目を閉じる" : "ふつう";
                    Vector3 h = pb.HeadPos - tr.up * 0.02f;
                    S(h + tr.forward * 0.75f, h, "顔_" + tag + "_前");
                    S(h + (tr.forward + tr.right).normalized * 0.75f, h, "顔_" + tag + "_斜め");
                    S(h + tr.right * 0.75f, h, "顔_" + tag + "_横");
                }
                pb.testEyesClosed = false;
                yield return Wait(3);
                Transform nk = B(pb, "Neck", "Head");
                if (nk != null)
                {
                    Vector3 n = nk.position;
                    S(n + tr.forward * 0.5f + tr.up * 0.03f, n, "首_前");
                    S(n + (tr.forward + tr.right).normalized * 0.5f + tr.up * 0.03f, n, "首_斜め");
                    S(n - tr.forward * 0.5f + tr.up * 0.03f, n, "首_後ろ");
                }
                Vector3 hip = tr.position + Vector3.up * 0.75f;
                S(tr.position + tr.forward * 1.1f + Vector3.up * 0.25f, hip, "立つ_低い前");
                Transform sc = B(pb, "UpperChest", "Chest", "Spine2", "Spine1");   // 2026-11-14 14:40：立ち姿の胸の下（前・左前・右前・低い前）
                if (sc != null)
                {
                    Vector3 c = sc.position - tr.up * 0.08f;
                    S(c + tr.forward * 0.6f - tr.up * 0.15f, c, "立つ_胸の下_前");
                    S(c + (tr.forward - tr.right).normalized * 0.6f - tr.up * 0.15f, c, "立つ_胸の下_左前");
                    S(c + (tr.forward + tr.right).normalized * 0.6f - tr.up * 0.15f, c, "立つ_胸の下_右前");
                    S(c + tr.forward * 0.5f - tr.up * 0.4f, c, "立つ_胸の下_低い前");
                }
                Destroy(pb.gameObject);
            }

            // 2. 歩く：低い前と、胸の下（揺れ）
            pb = Make("歩く", 1);
            if (pb != null)
            {
                pb.testSpeed = 1.4f;
                yield return Wait(30);
                var tr = pb.transform;
                for (int f = 0; f < 8; f++)
                {
                    Vector3 hip = tr.position + Vector3.up * 0.75f;
                    S(tr.position + tr.forward * 1.1f + Vector3.up * 0.25f, hip, "歩く_低い前_" + f);
                    Transform ch = B(pb, "UpperChest", "Chest", "Spine2", "Spine1");
                    if (ch != null)
                    {
                        Vector3 c = ch.position - tr.up * 0.08f;
                        S(c + tr.forward * 0.6f - tr.up * 0.15f, c, "歩く_胸の下_前_" + f);
                        S(c + (tr.forward - tr.right).normalized * 0.6f - tr.up * 0.15f, c, "歩く_胸の下_左前_" + f);
                    }
                    yield return Wait(6);
                }
                Destroy(pb.gameObject);
            }

            // 3. 椅子に座る：胸の下〜おなか・低い前
            pb = Make("座る", 2);
            if (pb != null)
            {
                pb.testPose = BodyPose.Sit;
                yield return Wait(60);
                var tr = pb.transform;
                Transform sp = B(pb, "Spine", "Hips");
                Transform ch = B(pb, "UpperChest", "Chest", "Spine2", "Spine1");
                if (ch != null && sp != null)
                {
                    Vector3 c = (ch.position + sp.position) * 0.5f;
                    S(c + tr.forward * 0.65f, c, "座る_胸の下_前");
                    S(c + (tr.forward - tr.right).normalized * 0.65f, c, "座る_胸の下_左前");
                }
                Transform hips = B(pb, "Hips");
                if (hips != null)
                {
                    Vector3 hp = hips.position;
                    S(hp + tr.forward * 1.2f - Vector3.up * (hp.y - 0.25f), hp, "座る_低い前");
                    S(hp + tr.forward * 1.2f + tr.right * 0.8f + Vector3.up * 0.1f, hp, "座る_右前");
                    S(hp + tr.forward * 1.2f - tr.right * 0.8f + Vector3.up * 0.1f, hp, "座る_左前");
                }
                Destroy(pb.gameObject);
            }

            // 4. そで口：手を振る・話す
            foreach (var mode in new[] { "手を振る", "話す" })
            {
                pb = Make(mode, mode == "手を振る" ? 3 : 4);
                if (pb == null) continue;
                yield return Wait(20);
                if (mode == "手を振る") pb.Gesture("Wave", 9999f); else pb.testTalk = true;
                var tr = pb.transform;
                for (int f = 0; f < 8; f++)
                {
                    yield return Wait(10);
                    foreach (var side in new[] { "右", "左" })
                    {
                        Transform hd = B(pb, side == "右" ? "RightHand" : "LeftHand");
                        if (hd == null) continue;
                        Vector3 outv = side == "右" ? tr.right : -tr.right;
                        S(hd.position + tr.forward * 0.35f + outv * 0.2f, hd.position, mode + "_そで口_" + side + "_" + f);
                    }
                }
                Destroy(pb.gameObject);
            }

            Time.captureDeltaTime = keepCapture;
            File.WriteAllText(Path.Combine(dir, "済み.txt"), System.DateTime.Now.ToString());
            Destroy(cam.gameObject); Destroy(lamp.gameObject); rt.Release();
            Destroy(gameObject);
        }
    }
}
