using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 確認用：再生中に、全キャラと「姿勢の見本」の写真を撮ってファイルに保存する。
    /// 保存先：D:\ゲーム制作\ゲーム本体\仮モデル\Unity確認\
    /// メニュー「LifeSim/確認用の写真を撮る（再生中）」から使う。ゲームの進行には影響しない。
    /// </summary>
    public class ProtoPhotoBooth : MonoBehaviour
    {
        const int PhotoLayer = 31;
        public static string OutDir { get { return Path.GetFullPath(Path.Combine(Application.dataPath, "../../仮モデル/Unity確認")); } }

        Camera cam;
        RenderTexture rt;
        Light lamp;
        readonly StringBuilder report = new StringBuilder();

        IEnumerator Start()
        {
            Directory.CreateDirectory(OutDir);
            var world = FindAnyObjectByType<LoungeWorld>();
            cam = new GameObject("PhotoCam").AddComponent<Camera>();
            cam.enabled = false;
            cam.fieldOfView = 32f;
            cam.nearClipPlane = 0.05f;
            rt = new RenderTexture(600, 900, 24);
            cam.targetTexture = rt;
            lamp = new GameObject("PhotoLamp").AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.range = 10f;
            lamp.intensity = 4f;
            yield return null;

            report.AppendLine("確認の写真 " + System.DateTime.Now);
            if (world != null)
            {
                report.AppendLine("ゲーム内：" + world.Day + "日目 " + Calendar.DateText(world.Day, world.holidayWeekdays) + " " + ScheduleMaker.Clock(world.Now));
                foreach (var c in world.characters)
                {
                    var pb = c.GetComponent<ProtoBody>();
                    report.AppendLine(c.displayName + "：" + (pb != null ? pb.DebugInfo : "仮モデルなし（仮の人形）") + "　今=" + c.actionText + "　姿勢=" + c.BodyPoseNow);
                    Vector3 center = c.transform.position + Vector3.up * (c.BodyPoseNow == BodyPose.Stand ? 0.9f : 0.6f);
                    Shoot(c.gameObject, center + c.transform.forward * 3f + Vector3.up * 0.5f, center, "キャラ_" + c.displayName + "_前.png", true);
                    Shoot(c.gameObject, center + c.transform.right * 3f + Vector3.up * 0.5f, center, "キャラ_" + c.displayName + "_横.png", true);
                    Shoot(c.gameObject, center + c.transform.forward * 1.8f + Vector3.up * 2.4f, center, "キャラ_" + c.displayName + "_周り.png", false);
                }
            }

            // 姿勢の見本（建物の外の離れた場所に並べる）
            var poses = new[] { "立つ", "歩く", "話す", "椅子", "食べる", "正座", "居眠り", "寝る", "お風呂" };
            var made = new List<GameObject>();
            for (int g = 0; g < 2; g++)
            {
                bool male = g == 1;
                var look = CharLook.Get(male ? "F" : "A", male, Color.gray);
                for (int i = 0; i < poses.Length; i++)
                {
                    var go = new GameObject("PoseSample_" + poses[i]);
                    go.transform.position = new Vector3(60f + i * 3f, 0f, 60f + g * 6f);
                    go.transform.rotation = Quaternion.identity;
                    var pb = ProtoBody.Create(go, look);
                    if (pb == null) { report.AppendLine("姿勢の見本：仮モデルを作れませんでした"); Destroy(go); continue; }
                    pb.testMode = true;
                    switch (poses[i])
                    {
                        case "立つ": pb.testPose = BodyPose.Stand; break;
                        case "歩く": pb.testPose = BodyPose.Stand; pb.testSpeed = 1.2f; break;
                        case "話す": pb.testPose = BodyPose.Stand; pb.testTalk = true; break;
                        case "椅子": pb.testPose = BodyPose.Sit; break;
                        case "食べる": pb.testPose = BodyPose.Sit; pb.testEat = true; break;
                        case "正座": pb.testPose = BodyPose.SitFloor; break;
                        case "居眠り": pb.testPose = BodyPose.Sit; pb.testDoze = true; break;
                        case "寝る": pb.testPose = BodyPose.Lie; break;
                        case "お風呂": pb.testPose = BodyPose.Soak; break;
                    }
                    made.Add(go);
                }
            }
            for (int f = 0; f < 20; f++) yield return null;
            foreach (var go in made)
            {
                string n = go.name.Replace("PoseSample_", "");
                string who = go.transform.position.z > 62f ? "男性" : "女性";
                bool lie = n == "寝る";
                Vector3 center = go.transform.position + Vector3.up * (lie ? 0.6f : (n == "立つ" || n == "歩く" || n == "話す" ? 0.9f : 0.55f));
                Shoot(go, center + Vector3.forward * 3.2f + Vector3.up * 0.4f, center, "見本_" + who + "_" + n + "_前.png", true);
                Shoot(go, center + Vector3.right * 3.2f + Vector3.up * 0.4f, center, "見本_" + who + "_" + n + "_横.png", true);
                if (lie) Shoot(go, center + Vector3.up * 3.2f + Vector3.forward * 0.01f, center, "見本_" + who + "_" + n + "_上.png", true);
            }
            foreach (var go in made) Destroy(go);

            File.WriteAllText(Path.Combine(OutDir, "報告.txt"), report.ToString(), Encoding.UTF8);
            Debug.Log("確認用の写真を保存しました：" + OutDir);
            Destroy(cam.gameObject);
            Destroy(lamp.gameObject);
            rt.Release();
            Destroy(gameObject);
        }

        void Shoot(GameObject target, Vector3 from, Vector3 at, string file, bool isolated)
        {
            cam.transform.position = from;
            cam.transform.LookAt(at);
            lamp.transform.position = from + Vector3.up * 0.5f;
            var saved = new List<KeyValuePair<GameObject, int>>();
            if (isolated)
            {
                foreach (var r in target.GetComponentsInChildren<Renderer>())
                {
                    saved.Add(new KeyValuePair<GameObject, int>(r.gameObject, r.gameObject.layer));
                    r.gameObject.layer = PhotoLayer;
                }
                cam.cullingMask = 1 << PhotoLayer;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.3f, 0.32f, 0.36f);
            }
            else
            {
                cam.cullingMask = ~(1 << PhotoLayer);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
            }
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(OutDir, file), tex.EncodeToPNG());
            Destroy(tex);
            foreach (var kv in saved) kv.Key.layer = kv.Value;
        }
    }

#if UNITY_EDITOR
    /// <summary>再生中の警告・エラーをファイルにも残す（Claudeが確認するため）。保存先：LifeSimProto\Logs\play_log.txt</summary>
    static class PlayLogFile
    {
        static string path;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Init()
        {
            path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/play_log.txt"));
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, "再生開始 " + System.DateTime.Now + "\n", Encoding.UTF8);
            }
            catch { }
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
        }

        static void OnLog(string msg, string stack, LogType type)
        {
            try
            {
                string head = type == LogType.Log ? "[情報] " : (type == LogType.Warning ? "[警告] " : "[エラー] ");
                string body = head + msg + "\n" + (type == LogType.Error || type == LogType.Exception ? stack + "\n" : "");
                File.AppendAllText(path, body, Encoding.UTF8);
            }
            catch { }
        }
    }
#endif
}
