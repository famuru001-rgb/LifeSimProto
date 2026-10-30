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
                    report.AppendLine(c.letter + " " + c.displayName + "：" + (pb != null ? pb.DebugInfo : "仮モデルなし（仮の人形）") + "　今=" + c.actionText + "　姿勢=" + c.BodyPoseNow);
                    Vector3 center = c.transform.position + Vector3.up * (c.BodyPoseNow == BodyPose.Stand ? 0.9f : 0.6f);
                    Shoot(c.gameObject, center + c.transform.forward * 3f + Vector3.up * 0.5f, center, "キャラ_" + c.letter + "_前.png", true);
                    Shoot(c.gameObject, center + c.transform.right * 3f + Vector3.up * 0.5f, center, "キャラ_" + c.letter + "_横.png", true);
                    Shoot(c.gameObject, center + c.transform.forward * 1.8f + Vector3.up * 2.4f, center, "キャラ_" + c.letter + "_周り.png", false);
                }
            }

            // 見本（建物の外の離れた場所に並べる）：本番モデルがあるキャラは全員、なければ仮モデルのAとF
            var letters = new List<string>();
            if (world != null)
                foreach (var c in world.characters)
                    if (Resources.Load<GameObject>("Chars/" + c.letter + "/Char" + c.letter) != null) letters.Add(c.letter);
            if (letters.Count == 0) { letters.Add("A"); letters.Add("F"); }
            var items = new[] { "立つ", "歩く", "歩きながら手を振る", "喜ぶ", "話す", "椅子", "食べる", "正座", "居眠り", "寝る", "お風呂",
                                "服_タオル", "服_私服", "服_寝間着", "服_下着",
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
                        case "喜ぶ": pb.Gesture("Joy", 9999f); break;   // 両手を上げる（脇の確認）
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
                        case "服_下着": pb.testOutfit = "Underwear"; break;   // 2026-10-26 下着姿（Outfit_Underwear_）の色の確認。無いキャラは制服のまま
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
                    Shoot(go, hp + (Vector3.forward + Vector3.right * 0.9f).normalized * 0.75f, hp, "見本_" + n + "_斜め.png", true);   // 2026-10-27：目じり・横顔の確認
                    continue;
                }
                bool lie = n.EndsWith("寝る");
                bool stand = n.EndsWith("立つ") || n.EndsWith("歩く") || n.EndsWith("手を振る") || n.EndsWith("喜ぶ") || n.EndsWith("話す") || n.Contains("_服_");
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

    /// <summary>
    /// 確認用（2026-10-26 取り込みのチャット）：揺れの連続写真。本番モデルのキャラを建物の外に並べ、
    /// 立つ→歩き出す→歩く→止まる→その場で振り向く、を 0.1 秒ごとに横と前から撮る。
    /// 保存先：仮モデル\Unity確認\揺れ_（名前）\。ゲームの進行には影響しない。命令 sway（名前）。
    /// </summary>
    /// <summary>
    /// 確認用（2026-10-27 取り込みのチャット）：座ったときのスカート。本番モデルのキャラを座らせ、座りの補正 0・50・100 で
    /// まわり8方向（腰の高さ）と低い前・上から撮る。保存先：仮モデル\Unity確認\座り_（名前）\。命令 sitshots（名前）。
    /// </summary>
    public class SitShots : MonoBehaviour
    {
        public static string Tag = "now";
        public static string Letters = "I";
        const int PhotoLayer = 31;

        IEnumerator Start()
        {
            string dir = Path.Combine(ProtoPhotoBooth.OutDir, "座り_" + Tag);
            Directory.CreateDirectory(dir);
            var cam = new GameObject("SitCam").AddComponent<Camera>();
            cam.enabled = false; cam.fieldOfView = 30f; cam.nearClipPlane = 0.05f;
            cam.cullingMask = 1 << PhotoLayer; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.3f, 0.32f, 0.36f);
            var rt = new RenderTexture(600, 600, 24); cam.targetTexture = rt;
            var lamp = new GameObject("SitLamp").AddComponent<Light>(); lamp.type = LightType.Point; lamp.range = 10f; lamp.intensity = 1.2f;   // 近くから撮るので弱め（強いと靴や脚が白く飛ぶ）
            int li = 0;
            foreach (char ch in Letters)
            {
                string letter = ch.ToString();
                if (Resources.Load<GameObject>("Chars/" + letter + "/Char" + letter) == null) continue;
                foreach (float k in new[] { 0f, 50f, 100f })
                {
                    var go = new GameObject("Sit_" + letter + "_" + k);
                    go.transform.position = new Vector3(60f + li * 4f, 0f, 140f);
                    var pb = ProtoBody.Create(go, CharLook.Get(letter, false, Color.gray), letter);
                    if (pb == null) { Destroy(go); continue; }
                    pb.testMode = true; pb.testPose = BodyPose.Sit; pb.testOutfit = "Uniform"; pb.testFace = ""; pb.testSkirtSit = k;
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.gameObject.layer = PhotoLayer;
                    if (Tag == "i2") foreach (var sm in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) sm.updateWhenOffscreen = true;   // 試し：表示の範囲（bounds）のずれで消えていないか
                    for (float w = 0f; w < 1.2f; w += Time.deltaTime) yield return null;   // 座る動きと揺れが落ち着くまで
                    Vector3 hip = go.transform.position + Vector3.up * 0.5f;
                    var fwd = go.transform.forward; var right = go.transform.right;
                    string[] names = { "前", "右前", "右", "右後ろ", "後ろ", "左後ろ", "左", "左前" };
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 d = Quaternion.AngleAxis(i * 45f, Vector3.up) * fwd;
                        SwayShots.Shot(cam, rt, lamp, hip + d * 1.7f + Vector3.up * 0.15f, hip, Path.Combine(dir, letter + "_補正" + k + "_" + i.ToString("0") + names[i] + ".png"));
                    }
                    SwayShots.Shot(cam, rt, lamp, hip + fwd * 1.4f - Vector3.up * 0.3f, hip, Path.Combine(dir, letter + "_補正" + k + "_8低い前.png"));
                    SwayShots.Shot(cam, rt, lamp, hip - fwd * 1.2f + Vector3.up * 1.0f, hip, Path.Combine(dir, letter + "_補正" + k + "_9上の後ろ.png"));
                    Destroy(go);
                    li++;
                }
            }
            File.WriteAllText(Path.Combine(dir, "済み.txt"), System.DateTime.Now.ToString());
            Destroy(cam.gameObject); Destroy(lamp.gameObject); rt.Release();
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 確認用（2026-10-27 取り込みのチャット）：腕を上げる・振る動きの脇。喜ぶ（Joy）・手を振る（Wave）・話す（Talk）を
    /// 3.2 秒ずつ再生し、0.05 秒ごとに左右の脇の近く（斜め前の下・真横）といつもの距離（前）から撮る。
    /// 手の高さ（肩からの高さ）を 腕の高さ.csv に書く（いちばん上がる瞬間を選ぶため）。保存先：仮モデル\Unity確認\脇_（名前）\。命令 armshots。
    /// </summary>
    public class ArmShots : MonoBehaviour
    {
        public static string Tag = "i";
        public static string Letter = "I";
        const int PhotoLayer = 31;

        IEnumerator Start()
        {
            string dir = Path.Combine(ProtoPhotoBooth.OutDir, "脇_" + Tag);
            Directory.CreateDirectory(dir);
            var cam = new GameObject("ArmCam").AddComponent<Camera>();
            cam.enabled = false; cam.fieldOfView = 30f; cam.nearClipPlane = 0.02f;
            cam.cullingMask = 1 << PhotoLayer; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.3f, 0.32f, 0.36f);
            var rt = new RenderTexture(480, 480, 24); cam.targetTexture = rt;
            var lamp = new GameObject("ArmLamp").AddComponent<Light>(); lamp.type = LightType.Point; lamp.range = 10f; lamp.intensity = 1.2f;
            var csv = new StringBuilder("動き,こま,秒,右手の高さ(肩から m),左手の高さ(肩から m)\n");
            int mi = 0;
            foreach (var mode in new[] { "Joy", "Wave", "Talk" })
            {
                var go = new GameObject("Arm_" + mode);
                go.transform.position = new Vector3(60f + mi * 4f, 0f, 170f);
                var pb = ProtoBody.Create(go, CharLook.Get(Letter, false, Color.gray), Letter);
                if (pb == null) { Destroy(go); continue; }
                pb.testMode = true; pb.testPose = BodyPose.Stand; pb.testOutfit = "Uniform"; pb.testFace = "";
                foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.gameObject.layer = PhotoLayer;
                for (int w = 0; w < 5; w++) yield return null;
                if (mode == "Talk") pb.testTalk = true; else pb.Gesture(mode, 9999f);
                float t = 0f, next = 0f; int frame = 0;
                while (t < 3.2f)
                {
                    t += Time.deltaTime;
                    if (t >= next)
                    {
                        next += 0.05f;
                        var tr = go.transform;
                        Transform rs = pb.Bone("RightArm"), ls = pb.Bone("LeftArm"), rh = pb.Bone("RightHand"), lh = pb.Bone("LeftHand");
                        csv.Append(mode + "," + frame + "," + t.ToString("0.00") + "," + (rh.position.y - rs.position.y).ToString("0.000") + "," + (lh.position.y - ls.position.y).ToString("0.000") + "\n");
                        string f = frame.ToString("000");
                        // 右脇・左脇の近く：肩の少し下を、斜め前の下と真横から
                        foreach (var side in new[] { "右", "左" })
                        {
                            Transform sh = side == "右" ? rs : ls;
                            Vector3 outv = side == "右" ? tr.right : -tr.right;
                            Vector3 at = sh.position - Vector3.up * 0.08f;
                            SwayShots.Shot(cam, rt, lamp, at + outv * 0.65f + tr.forward * 0.65f - Vector3.up * 0.15f, at, Path.Combine(dir, mode + "_" + side + "脇_斜め前下_" + f + ".png"));
                            SwayShots.Shot(cam, rt, lamp, at + outv * 0.95f, at, Path.Combine(dir, mode + "_" + side + "脇_横_" + f + ".png"));
                        }
                        // いつもの距離：前から全身
                        Vector3 c = tr.position + Vector3.up * 1.0f;
                        SwayShots.Shot(cam, rt, lamp, c + tr.forward * 3.2f + Vector3.up * 0.4f, c, Path.Combine(dir, mode + "_前_" + f + ".png"));
                        if (mode == "Talk") SwayShots.Shot(cam, rt, lamp, c - tr.forward * 3.2f + Vector3.up * 0.4f, c, Path.Combine(dir, mode + "_後ろ_" + f + ".png"));
                        frame++;
                    }
                    yield return null;
                }
                Destroy(go);
                mi++;
            }
            File.WriteAllText(Path.Combine(dir, "腕の高さ.csv"), csv.ToString(), Encoding.UTF8);
            File.WriteAllText(Path.Combine(dir, "済み.txt"), System.DateTime.Now.ToString());
            Destroy(cam.gameObject); Destroy(lamp.gameObject); rt.Release();
            Destroy(gameObject);
        }
    }

    public class SwayShots : MonoBehaviour
    {
        public static string Tag = "now";
        const int PhotoLayer = 31;

        IEnumerator Start()
        {
            string dir = Path.Combine(ProtoPhotoBooth.OutDir, "揺れ_" + Tag);
            Directory.CreateDirectory(dir);
            var cam = new GameObject("SwayCam").AddComponent<Camera>();
            cam.enabled = false; cam.fieldOfView = 30f; cam.nearClipPlane = 0.05f;
            cam.cullingMask = 1 << PhotoLayer; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.3f, 0.32f, 0.36f);
            var rt = new RenderTexture(420, 620, 24); cam.targetTexture = rt;
            var lamp = new GameObject("SwayLamp").AddComponent<Light>(); lamp.type = LightType.Point; lamp.range = 10f; lamp.intensity = 4f;
            var list = new List<KeyValuePair<string, ProtoBody>>();
            int li = 0;
            foreach (var letter in new[] { "I", "K", "J", "G", "B" })
            {
                if (Resources.Load<GameObject>("Chars/" + letter + "/Char" + letter) == null) continue;
                var go = new GameObject("Sway_" + letter);
                go.transform.position = new Vector3(60f + li * 4f, 0f, 120f);
                var pb = ProtoBody.Create(go, CharLook.Get(letter, false, Color.gray), letter);
                if (pb == null) { Destroy(go); continue; }
                pb.testMode = true; pb.testPose = BodyPose.Stand; pb.testOutfit = "Uniform"; pb.testFace = ""; pb.testSpeed = 0f;
                foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.gameObject.layer = PhotoLayer;
                list.Add(new KeyValuePair<string, ProtoBody>(letter, pb));
                li++;
            }
            // 段取り（秒）：0〜0.6 立つ、0.6〜3.6 歩く（1.4m/秒）、3.6〜5.6 止まる、5.6〜6.0 その場で90度振り向く、6.0〜7.4 止まる、
            // 7.4〜8.0 おじぎ（45度まで）、8.0〜8.8 そのまま、8.8〜9.4 起き上がる、9.4〜10.4 止まる（おじぎはゲームの動きには無い確認用）
            const float End = 10.4f;
            float t = 0f, nextShot = 0f; int frame = 0;
            var csv = new StringBuilder("秒,キャラ,胸の上下(度),胸の伸び(%),髪の角度(度)\n");
            while (t < End)
            {
                foreach (var kv in list) csv.Append(t.ToString("0.000") + "," + kv.Key + "," + kv.Value.SpringDebug + "\n");
                float dt = Time.deltaTime;
                t += dt;
                bool walk = t >= 0.6f && t < 3.6f;
                foreach (var kv in list)
                {
                    var tr = kv.Value.transform;
                    kv.Value.testSpeed = walk ? 1.4f : 0f;
                    if (walk) tr.position += tr.forward * 1.4f * dt;
                    if (t >= 5.6f && t < 6.0f) tr.rotation = Quaternion.Euler(0f, Mathf.Lerp(0f, 90f, (t - 5.6f) / 0.4f), 0f);
                    else if (t >= 6.0f) tr.rotation = Quaternion.Euler(0f, 90f, 0f);
                    float bow = t < 7.4f ? 0f : t < 8.0f ? Mathf.SmoothStep(0f, 45f, (t - 7.4f) / 0.6f) : t < 8.8f ? 45f : t < 9.4f ? Mathf.SmoothStep(45f, 0f, (t - 8.8f) / 0.6f) : 0f;
                    kv.Value.testBow = bow;
                }
                if (t >= nextShot)
                {
                    nextShot += 0.1f;
                    foreach (var kv in list)
                    {
                        Vector3 c = kv.Value.transform.position + Vector3.up * 1.05f;
                        Shot(cam, rt, lamp, c + Vector3.right * 2.6f + Vector3.up * 0.1f, c, Path.Combine(dir, kv.Key + "_横_" + frame.ToString("000") + ".png"));
                        Shot(cam, rt, lamp, c + Vector3.forward * 2.6f + Vector3.up * 0.1f, c, Path.Combine(dir, kv.Key + "_前_" + frame.ToString("000") + ".png"));
                        // 後ろ（後頭部）：キャラの向きの後ろ・少し上から頭を見る
                        var trk = kv.Value.transform; Vector3 hp = kv.Value.HeadPoint;
                        Shot(cam, rt, lamp, hp - trk.forward * 1.3f + Vector3.up * 0.45f, hp - Vector3.up * 0.1f, Path.Combine(dir, kv.Key + "_後頭部_" + frame.ToString("000") + ".png"));
                    }
                    frame++;
                }
                yield return null;
            }
            File.WriteAllText(Path.Combine(dir, "揺れの数値.csv"), csv.ToString(), Encoding.UTF8);
            File.WriteAllText(Path.Combine(dir, "済み.txt"), System.DateTime.Now + " " + frame + "コマ");
            foreach (var kv in list) Destroy(kv.Value.gameObject);
            Destroy(cam.gameObject); Destroy(lamp.gameObject); rt.Release();
            Destroy(gameObject);
        }

        public static void Shot(Camera cam, RenderTexture rt, Light lamp, Vector3 from, Vector3 at, string file)
        {
            cam.transform.position = from; cam.transform.LookAt(at);
            lamp.transform.position = from + Vector3.up * 0.5f;
            cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(file, tex.EncodeToJPG(88));
            Object.Destroy(tex);
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
