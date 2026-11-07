using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// トゥーン表示（LifeSim/Toon）の強さと細かい値を、再生中でも変えられるようにする。2026-11-06 キャラデザのチャット
    /// ・LifeSimProto\Logs\toon_tune.txt を置くと、1秒ごとに読み直して、本番キャラの材質に当てる（書き方は Logs\toon_tune_off.txt）
    /// ・ファイルが無いとき、または値が全部ふつう（強さ1・倍率1・ずらし0）のときは、今までと同じ見た目
    /// ・Logs\toon_shots.txt を置くと、強さを変えた見比べ写真を撮る（仮モデル\Unity確認\トゥーン見比べ\）
    /// ProtoBody.ApplyToon が材質を作るたびに Register を呼ぶ。
    /// </summary>
    public static class ToonTune
    {
        // ---- 値（ファイルから読む。ふつうの値で今までと同じ見た目） ----
        public static float strength = 1f;  // トゥーンの強さ：0＝写実寄り（影の境目なめらか・線なし）、1＝今まで、2＝くっきり・線を太く
        public static float shadow = 1f;    // 影の濃さの倍率：0＝影の色なし（白）、1＝今まで、2＝もっと濃く
        public static float line = 1f;      // 輪郭線の太さの倍率（強さとかけ合わせる）
        public static float skin = 1f;     // 肌の赤み・ふちの光・ツヤの倍率
        public static float ambient = 1f;   // まわりの明るさの倍率
        public static float light = 1f;     // 光の強さの倍率
        public static float step = 0f;      // 影の境目の位置のずらし（＋で影が広く、−で狭く。−0.5〜0.5くらい）

        class Base
        {
            public Material m;
            public Color shade;
            public float soft, width, scatter, rim, gloss, amb, lig, stp;
        }
        static readonly List<Base> mats = new List<Base>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetAll()
        {
            mats.Clear();
            SetNormal();
            ToonTuneWatcher.stamp = default(System.DateTime);
            ToonTuneWatcher.hadFile = false;
        }

        public static void SetNormal() { strength = 1f; shadow = 1f; line = 1f; skin = 1f; ambient = 1f; light = 1f; step = 0f; }

        /// <summary>材質を登録して、今の値を当てる。copyOf は元にした材質（輪郭線なしの写しのとき。元の値をそちらから取る）</summary>
        public static void Register(Material m, Material copyOf = null)
        {
            if (m == null || !m.HasProperty("_ShadeStep")) return;
            Base b = null;
            if (copyOf != null)
                foreach (var x in mats) if (x.m == copyOf) { b = new Base { shade = x.shade, soft = x.soft, width = x.width, scatter = x.scatter, rim = x.rim, gloss = x.gloss, amb = x.amb, lig = x.lig, stp = x.stp }; break; }
            if (b == null)
                b = new Base
                {
                    shade = m.GetColor("_ShadeColor"), soft = m.GetFloat("_ShadeSoftness"), width = m.GetFloat("_OutlineWidth"),
                    scatter = m.GetFloat("_ScatterStrength"), rim = m.GetFloat("_RimStrength"), gloss = m.GetFloat("_GlossStrength"),
                    amb = m.GetFloat("_AmbientStrength"), lig = m.GetFloat("_LightStrength"), stp = m.GetFloat("_ShadeStep")
                };
            b.m = m;
            mats.Add(b);
            ApplyOne(b);
        }

        public static void ApplyAll()
        {
            mats.RemoveAll(x => x.m == null);
            foreach (var b in mats) ApplyOne(b);
        }

        static void ApplyOne(Base b)
        {
            float s = Mathf.Clamp(strength, 0f, 3f);
            // 影の境目のぼかし：0 で 0.5（なめらか）、1 で元の値、2 で元の 1/4（くっきり）
            float soft = s <= 1f ? Mathf.Lerp(0.5f, b.soft, s) : b.soft * Mathf.Lerp(1f, 0.25f, Mathf.Min(s - 1f, 1f));
            b.m.SetFloat("_ShadeSoftness", Mathf.Max(0.001f, soft));
            // 輪郭線：0 で線なし、1 で元の太さ、2 で 1.5 倍
            float w = b.width * Mathf.Max(0f, line) * (s <= 1f ? s : 1f + 0.5f * (s - 1f));
            b.m.SetFloat("_OutlineWidth", w);
            b.m.SetShaderPassEnabled("SRPDefaultUnlit", w > 0.00001f && !b.m.name.EndsWith("_ToonNoLine"));
            // 影の色：白と元の影の色の間（濃さの倍率）
            Color sh = Color.LerpUnclamped(Color.white, b.shade, Mathf.Max(0f, shadow));
            sh.r = Mathf.Clamp01(sh.r); sh.g = Mathf.Clamp01(sh.g); sh.b = Mathf.Clamp01(sh.b); sh.a = 1f;
            b.m.SetColor("_ShadeColor", sh);
            b.m.SetFloat("_ScatterStrength", b.scatter * Mathf.Max(0f, skin));
            b.m.SetFloat("_RimStrength", b.rim * Mathf.Max(0f, skin));
            b.m.SetFloat("_GlossStrength", b.gloss * Mathf.Max(0f, skin));
            b.m.SetFloat("_AmbientStrength", b.amb * Mathf.Max(0f, ambient));
            b.m.SetFloat("_LightStrength", b.lig * Mathf.Max(0f, light));
            b.m.SetFloat("_ShadeStep", Mathf.Clamp(b.stp + step, -1f, 1f));
        }

        public static string Now()
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            return "strength=" + strength.ToString(ci) + " shadow=" + shadow.ToString(ci) + " line=" + line.ToString(ci) + " skin=" + skin.ToString(ci)
                + " ambient=" + ambient.ToString(ci) + " light=" + light.ToString(ci) + " step=" + step.ToString(ci) + "（材質 " + mats.Count + "）";
        }
    }

    /// <summary>Logs\toon_tune.txt を1秒ごとに見て、変わっていたら読み直す。Logs\toon_shots.txt があれば見比べ写真を撮る</summary>
    public class ToonTuneWatcher : MonoBehaviour
    {
        internal static System.DateTime stamp;
        internal static bool hadFile;
        float timer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var go = new GameObject("ToonTuneWatcher");
            go.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(go);
            go.AddComponent<ToonTuneWatcher>();
        }

        static string LogsDir { get { return Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs")); } }

        void Update()
        {
            timer -= Time.unscaledDeltaTime;
            if (timer > 0f) return;
            timer = 1f;
            try
            {
                string f = Path.Combine(LogsDir, "toon_tune.txt");
                if (File.Exists(f))
                {
                    var st = File.GetLastWriteTimeUtc(f);
                    if (!hadFile || st != stamp) { stamp = st; hadFile = true; Read(f); ToonTune.ApplyAll(); Debug.Log("[ToonTune] 読みなおした：" + ToonTune.Now()); }
                }
                else if (hadFile) { hadFile = false; ToonTune.SetNormal(); ToonTune.ApplyAll(); Debug.Log("[ToonTune] ファイルが無いので、ふつうの値に戻した"); }

                // 2026-11-06：Logs\screen_shot.txt を置くと、今のゲーム画面をそのまま Logs\screen_（ファイルの中身）.png に保存（ギザギザの前後の見比べなど）
                string scr = Path.Combine(LogsDir, "screen_shot.txt");
                if (File.Exists(scr))
                {
                    string tag = File.ReadAllText(scr).Trim();
                    File.Delete(scr);
                    if (string.IsNullOrEmpty(tag)) tag = System.DateTime.Now.ToString("HHmmss");
                    ScreenCapture.CaptureScreenshot(Path.Combine(LogsDir, "screen_" + tag + ".png"));
                }

                string shots = Path.Combine(LogsDir, "toon_shots.txt");
                if (File.Exists(shots) && FindAnyObjectByType<ToonShots>() == null)
                {
                    string letters = File.ReadAllText(shots).Trim();
                    File.Delete(shots);
                    ToonShots.Letters = string.IsNullOrEmpty(letters) ? "IQGJKF" : letters;
                    new GameObject("ToonShots").AddComponent<ToonShots>();
                }
            }
            catch (System.Exception e) { Debug.LogWarning("[ToonTune] " + e.Message); }
        }

        static void Read(string f)
        {
            ToonTune.SetNormal();
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            foreach (var raw0 in File.ReadAllLines(f))
            {
                string raw = raw0;
                int h = raw.IndexOf('#'); if (h >= 0) raw = raw.Substring(0, h);
                var kv = raw.Split('=');
                if (kv.Length != 2) continue;
                float a;
                if (!float.TryParse(kv[1].Trim(), System.Globalization.NumberStyles.Float, ci, out a)) continue;
                switch (kv[0].Trim())
                {
                    case "strength": ToonTune.strength = a; break;
                    case "shadow": ToonTune.shadow = a; break;
                    case "line": ToonTune.line = a; break;
                    case "skin": ToonTune.skin = a; break;
                    case "ambient": ToonTune.ambient = a; break;
                    case "light": ToonTune.light = a; break;
                    case "step": ToonTune.step = a; break;
                }
            }
        }
    }

    /// <summary>トゥーンの強さを 0・0.5・1・1.5・2 に変えて、同じキャラを撮る（見比べ用。ゲームの進行には影響しない）</summary>
    public class ToonShots : MonoBehaviour
    {
        public static string Letters = "IQGJKF";
        public static float[] Strengths = { 0f, 0.5f, 1f, 1.5f, 2f };
        const int PhotoLayer = 31;

        IEnumerator Start()
        {
            string dir = Path.Combine(ProtoPhotoBooth.OutDir, "トゥーン見比べ");
            Directory.CreateDirectory(dir);
            var cam = new GameObject("ToonCam").AddComponent<Camera>();
            cam.enabled = false; cam.fieldOfView = 30f; cam.nearClipPlane = 0.05f;
            cam.cullingMask = 1 << PhotoLayer; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.62f, 0.66f, 0.72f);
            var rtBody = new RenderTexture(420, 680, 24) { antiAliasing = 4 };
            var rtFace = new RenderTexture(480, 480, 24) { antiAliasing = 4 };
            // 斜め前・上からの明かり（影の形が見えるように）
            var lamp = new GameObject("ToonLamp").AddComponent<Light>(); lamp.type = LightType.Point; lamp.range = 12f; lamp.intensity = 6f;

            var list = new List<KeyValuePair<string, ProtoBody>>();
            int li = 0;
            foreach (var letter in SplitLetters(Letters))
            {
                if (Resources.Load<GameObject>("Chars/" + letter + "/Char" + letter) == null) continue;
                var go = new GameObject("Toon_" + letter);
                go.transform.position = new Vector3(60f + li * 4f, 0f, 140f);
                var pb = ProtoBody.Create(go, CharLook.Get(letter, false, Color.gray), letter);
                if (pb == null) { Destroy(go); continue; }
                pb.testMode = true; pb.testLetter = letter; pb.testPose = BodyPose.Stand; pb.testOutfit = "Uniform"; pb.testFace = ""; pb.testSpeed = 0f;
                list.Add(new KeyValuePair<string, ProtoBody>(letter, pb));
                li++;
            }
            for (int k = 0; k < 20; k++) yield return null;   // 立つ姿勢に落ち着くまで待つ
            foreach (var kv in list) foreach (var r in kv.Value.GetComponentsInChildren<Renderer>(true)) r.gameObject.layer = PhotoLayer;

            float keepS = ToonTune.strength;
            var report = new StringBuilder("トゥーン見比べ " + System.DateTime.Now + "\n");
            foreach (float s in Strengths)
            {
                ToonTune.strength = s; ToonTune.ApplyAll();
                yield return null;
                string tag = s.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
                foreach (var kv in list)
                {
                    var tr = kv.Value.transform;
                    Vector3 c = tr.position + Vector3.up * 0.85f;
                    Vector3 side = tr.right * 0.8f + Vector3.up * 1.2f;
                    Shot(cam, rtBody, lamp, c + tr.forward * 3.4f + Vector3.up * 0.15f, c, side, Path.Combine(dir, kv.Key + "_強さ" + tag + "_全身.jpg"));
                    Vector3 hp = kv.Value.HeadPoint - Vector3.up * 0.05f;
                    Vector3 dirF = (tr.forward + tr.right * 0.45f).normalized;
                    Shot(cam, rtFace, lamp, hp + dirF * 0.75f, hp, side, Path.Combine(dir, kv.Key + "_強さ" + tag + "_顔.jpg"));
                }
                report.AppendLine("強さ " + tag + "：" + ToonTune.Now());
            }
            ToonTune.strength = keepS; ToonTune.ApplyAll();
            File.WriteAllText(Path.Combine(dir, "済み.txt"), report.ToString(), Encoding.UTF8);
            foreach (var kv in list) Destroy(kv.Value.gameObject);
            Destroy(cam.gameObject); Destroy(lamp.gameObject); rtBody.Release(); rtFace.Release();
            Destroy(gameObject);
        }

        static IEnumerable<string> SplitLetters(string s)
        {
            // 「IQGJKF」のような1文字の記号の並び。2文字の記号は「,」で区切る（例：I,Q,AA）
            if (s.Contains(",")) { foreach (var p in s.Split(',')) if (p.Trim().Length > 0) yield return p.Trim(); }
            else foreach (char c in s) if (!char.IsWhiteSpace(c)) yield return c.ToString();
        }

        static void Shot(Camera cam, RenderTexture rt, Light lamp, Vector3 from, Vector3 at, Vector3 lampOff, string file)
        {
            cam.targetTexture = rt;
            cam.transform.position = from; cam.transform.LookAt(at);
            lamp.transform.position = at + lampOff + (from - at).normalized * 1.2f;
            cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(file, tex.EncodeToJPG(90));
            Object.Destroy(tex);
        }
    }
}
