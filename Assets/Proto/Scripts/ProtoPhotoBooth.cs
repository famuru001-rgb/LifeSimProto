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

            // 見本（建物の外の離れた場所に並べる）：本番モデルがあるキャラは全員、なければ仮モデルのAとF
            var letters = new List<string>();
            if (world != null)
                foreach (var c in world.characters)
                    if (Resources.Load<GameObject>("Chars/" + c.displayName + "/Char" + c.displayName) != null) letters.Add(c.displayName);
            if (letters.Count == 0) { letters.Add("A"); letters.Add("F"); }
            var items = new[] { "立つ", "歩く", "歩きながら手を振る", "話す", "椅子", "食べる", "正座", "居眠り", "寝る", "お風呂",
                                "服_タオル", "服_私服", "服_寝間着",
                                "顔_ふつう", "顔_笑顔", "顔_目を閉じる", "顔_話す口", "顔_困り", "顔_怒り", "顔_驚き", "顔_照れ" };
            var made = new List<KeyValuePair<string, ProtoBody>>();
            for (int li = 0; li < letters.Count; li++)
            {
                string letter = letters[li];
                bool male = letter == "F";
                var look = CharLook.Get(letter, male, Color.gray);
                for (int i = 0; i < items.Length; i++)
                {
                    string it = items[i];
                    var go = new GameObject("Sample_" + letter + "_" + it);
                    go.transform.position = new Vector3(60f + i * 3f, 0f, 60f + li * 6f);
                    go.transform.rotation = Quaternion.identity;
                    var pb = ProtoBody.Create(go, look, letter);
                    if (pb == null) { report.AppendLine("見本：" + letter + " のモデルを作れませんでした"); Destroy(go); continue; }
                    if (i == 0) report.AppendLine("見本 " + letter + "：" + pb.DebugInfo);
                    pb.testMode = true;
                    pb.testPose = BodyPose.Stand;
                    pb.testOutfit = "Uniform";
                    pb.testFace = "";
                    switch (it)
                    {
                        case "歩く": pb.testSpeed = 1.2f; break;
                        case "歩きながら手を振る": pb.testSpeed = 1.2f; pb.Gesture("Wave", 9999f); break;
                        case "話す": pb.testTalk = true; break;
                        case "椅子": pb.testPose = BodyPose.Sit; break;
                        case "食べる": pb.testPose = BodyPose.Sit; pb.testEat = true; break;
                        case "正座": pb.testPose = BodyPose.SitFloor; break;
                        case "居眠り": pb.testPose = BodyPose.Sit; pb.testDoze = true; pb.testEyesClosed = true; break;
                        case "寝る": pb.testPose = BodyPose.Lie; pb.testOutfit = "Sleep"; pb.testEyesClosed = true; break;
                        case "お風呂": pb.testPose = BodyPose.Soak; pb.testOutfit = "Towel"; break;
                        case "服_タオル": pb.testOutfit = "Towel"; break;
                        case "服_私服": pb.testOutfit = "Casual"; break;
                        case "服_寝間着": pb.testOutfit = "Sleep"; break;
                        case "顔_笑顔": pb.testFace = "Smile"; break;
                        case "顔_目を閉じる": pb.testEyesClosed = true; break;
                        case "顔_話す口": pb.testTalk = true; break;
                        case "顔_困り": pb.testFace = "Sad"; break;
                        case "顔_怒り": pb.testFace = "Angry"; break;
                        case "顔_驚き": pb.testFace = "Surprised"; break;
                        case "顔_照れ": pb.testFace = "Shy"; break;
                    }
                    made.Add(new KeyValuePair<string, ProtoBody>(letter + "_" + it, pb));
                }
            }
            for (int f = 0; f < 30; f++) yield return null;
            foreach (var kv in made)
            {
                var go = kv.Value.gameObject;
                string n = kv.Key;
                if (n.Contains("_顔_"))
                {
                    Vector3 hp = kv.Value.HeadPoint;
                    Shoot(go, hp + Vector3.forward * 0.9f, hp, "見本_" + n + ".png", true);
                    continue;
                }
                bool lie = n.EndsWith("寝る");
                bool stand = n.EndsWith("立つ") || n.EndsWith("歩く") || n.EndsWith("手を振る") || n.EndsWith("話す") || n.Contains("_服_");
                Vector3 center = go.transform.position + Vector3.up * (lie ? 0.6f : (stand ? 0.9f : 0.55f));
                Shoot(go, center + Vector3.forward * 3.2f + Vector3.up * 0.4f, center, "見本_" + n + "_前.png", true);
                Shoot(go, center + Vector3.right * 3.2f + Vector3.up * 0.4f, center, "見本_" + n + "_横.png", true);
                if (lie) Shoot(go, center + Vector3.up * 3.2f + Vector3.forward * 0.01f, center, "見本_" + n + "_上.png", true);
            }
            foreach (var kv in made) Destroy(kv.Value.gameObject);

            // ゲームのカメラで、床の下から見上げた様子（床が透けるか）
            if (world != null && world.mainCamera != null)
            {
                var rig = world.mainCamera.GetComponent<CameraRig>();
                if (rig != null)
                {
                    var keep = new float[] { rig.pitch, rig.yaw, rig.distance };
                    Vector3 keepPivot = rig.pivot;
                    var keepFollow = rig.follow;
                    var shots = new[] { new Vector4(-35f, 0f, 14f, 1.0f), new Vector4(-25f, 90f, 12f, 3.8f), new Vector4(40f, 0f, 24f, 1.5f) };
                    for (int i = 0; i < shots.Length; i++)
                    {
                        rig.follow = null;
                        rig.pitch = shots[i].x; rig.yaw = shots[i].y; rig.distance = shots[i].z;
                        rig.pivot = new Vector3(0f, shots[i].w, -2f);
                        for (int f = 0; f < 3; f++) yield return null;
                        var mc = world.mainCamera;
                        var prevT = mc.targetTexture;
                        var wide = new RenderTexture(1280, 720, 24);
                        mc.targetTexture = wide;
                        mc.Render();
                        mc.targetTexture = prevT;
                        var prev = RenderTexture.active;
                        RenderTexture.active = wide;
                        var tex = new Texture2D(wide.width, wide.height, TextureFormat.RGB24, false);
                        tex.ReadPixels(new Rect(0, 0, wide.width, wide.height), 0, 0);
                        tex.Apply();
                        RenderTexture.active = prev;
                        File.WriteAllBytes(Path.Combine(OutDir, "カメラ_" + (i == 0 ? "床下から見上げ" : (i == 1 ? "2階の床下から" : "ふつう")) + ".png"), tex.EncodeToPNG());
                        Destroy(tex);
                        wide.Release();
                    }
                    rig.pitch = keep[0]; rig.yaw = keep[1]; rig.distance = keep[2]; rig.pivot = keepPivot; rig.follow = keepFollow;
                }
            }

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
            lamp.intensity = (from - at).magnitude < 1.5f ? 0.8f : 4f; // 顔のアップは明るすぎないように
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
    /// <summary>
    /// 確認用の写真を撮る合図：再生中に F9 キー、または LifeSimProto\Logs\take_photos.txt というファイルを置く（Claudeが外から合図するため）。
    /// </summary>
    public class PhotoTrigger : MonoBehaviour
    {
        float timer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var go = new GameObject("PhotoTrigger");
            go.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(go);
            go.AddComponent<PhotoTrigger>();
        }

        static string TriggerPath { get { return Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/take_photos.txt")); } }

        void Update()
        {
            bool go = false;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.f9Key.wasPressedThisFrame) go = true;
            timer -= Time.unscaledDeltaTime;
            if (timer <= 0f)
            {
                timer = 2f;
                try { if (File.Exists(TriggerPath)) { File.Delete(TriggerPath); go = true; } } catch { }
            }
            if (go && FindAnyObjectByType<ProtoPhotoBooth>() == null) new GameObject("PhotoBooth").AddComponent<ProtoPhotoBooth>();
        }
    }

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
