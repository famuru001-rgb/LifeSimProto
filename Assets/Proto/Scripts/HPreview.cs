using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LifeSimProto
{
    /// <summary>
    /// 二人きりの時間・自慰の動きを確かめる場（ゲームの流れには関わらない）。F8 で開く・閉じる。
    /// 動きは Resources/Motions/H（名前 H_＜形＞_＜部品＞_＜役＞）。役：M＝篤、F＝相手、P・S・H＝4人のときのほかの子。
    /// 2026-10-31 モーションのチャット
    /// </summary>
    public class HPreview : MonoBehaviour
    {
        public static HPreview Instance;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { var go = new GameObject("HPreviewKey"); DontDestroyOnLoad(go); go.AddComponent<HPreviewKey>(); }
        public static HPreview Open()
        {
            if (Instance != null) return Instance;
            return new GameObject("HPreview").AddComponent<HPreview>();
        }

        // ---------- 形と場面の表 ----------
        class Step { public string part; public float speed; public int count; public Step(string p, float s, int c) { part = p; speed = s; count = c; } }
        class Form
        {
            public string key, label, family; public string[] rigs;
            public Form(string k, string l, string f, params string[] r) { key = k; label = l; family = f; rigs = r; }
        }
        static readonly Form[] Forms =
        {
            new Form("MisA", "正常位A", "mis", "M", "F"),
            new Form("MisB", "正常位B", "mis", "M", "F"),
            new Form("Ride", "騎乗位", "ride", "M", "F"),
            new Form("Seat", "対面座位", "seat", "M", "F"),
            new Form("Doggy", "後背位", "mis", "M", "F"),
            new Form("MisB3", "4人：正常位B", "mis", "M", "F", "P", "S", "H"),
            new Form("Ride4", "4人：騎乗位", "ride", "M", "F", "P", "S", "H"),
            new Form("Doggy4", "4人：後背位", "mis", "M", "F", "P", "S", "H"),
            new Form("Seat4", "4人：対面座位", "seat", "M", "F", "P", "S", "H"),
            new Form("SoloBack", "自慰：あおむけ", "solo", "F"),
            new Form("SoloSide", "自慰：横向き", "solo", "F"),
            new Form("SoloSit", "自慰：もたれて座る", "solo", "F"),
        };
        static readonly string[] PhaseNames = { "前半", "中盤", "後半", "フィニッシュ" };
        static List<Step>[] Phases(string family)
        {
            var r = new List<Step>[4];
            switch (family)
            {
                case "ride":
                    r[0] = new List<Step> { new Step("Grind", 0.6f, 3), new Step("BounceSmall", 0.7f, 2) };
                    r[1] = new List<Step> { new Step("BounceSmall", 1.1f, 1), new Step("Bounce", 1.1f, 1), new Step("BounceSmall", 1.1f, 1), new Step("Bounce", 1.1f, 2), new Step("Circle", 0.9f, 1) };
                    r[2] = new List<Step> { new Step("Bounce", 1.8f, 6), new Step("Grind", 1.7f, 3) };
                    break;
                case "seat":
                    r[0] = new List<Step> { new Step("Rock", 0.6f, 3), new Step("Circle", 0.7f, 1) };
                    r[1] = new List<Step> { new Step("RockSmall", 1.1f, 1), new Step("Rock", 1.1f, 1), new Step("RockSmall", 1.1f, 1), new Step("Rock", 1.1f, 2), new Step("Bounce", 0.9f, 2) };
                    r[2] = new List<Step> { new Step("Rock", 1.9f, 6), new Step("Bounce", 1.5f, 2) };
                    break;
                case "solo":
                    r[0] = new List<Step> { new Step("Slow", 0.9f, 3), new Step("Slow", 1.3f, 2) };
                    r[1] = new List<Step> { new Step("Fast", 1.0f, 5) };
                    r[2] = new List<Step> { new Step("Fast", 1.4f, 4) };
                    break;
                default:
                    r[0] = new List<Step> { new Step("Deep", 0.7f, 3), new Step("Grind", 0.8f, 1) };
                    r[1] = new List<Step> { new Step("Deep", 1.2f, 2), new Step("Shallow", 1.2f, 1), new Step("Deep", 1.2f, 2) };
                    r[2] = new List<Step> { new Step("Shallow", 2.1f, 6), new Step("Deep", 1.5f, 2) };
                    break;
            }
            r[3] = new List<Step> { new Step("Finish", 1f, 1) };
            return r;
        }
        static readonly Dictionary<string, string> PartJa = new Dictionary<string, string>
        {
            { "Deep", "深く" }, { "Shallow", "浅く" }, { "Grind", "押しつけ・回す" }, { "Finish", "フィニッシュ" },
            { "Bounce", "上下（大）" }, { "BounceSmall", "上下（小）" }, { "Circle", "腰を回す" },
            { "Rock", "前後（大）" }, { "RockSmall", "前後（小）" }, { "Slow", "ゆっくり" }, { "Fast", "速く" },
        };
        static readonly string[] Letters = { "I", "B", "G", "J", "K", "Q", "R" };

        // ---------- 状態 ----------
        int formIx, letterIx, phase = -1;   // phase -1＝自動（前半→中盤→後半→フィニッシュ）
        int phaseNow, stepIx, rep;
        float t, speedMul = 1f, holdUntil = -1f;
        bool paused, underwear;
        List<Step>[] phases;
        class Body { public string rig; public ProtoBody pb; public GameObject go; }
        readonly List<Body> bodies = new List<Body>();
        Transform stage, anchor, headboard;
        Camera cam; Light lamp;
        float yaw = 210f, pitch = 25f, dist = 3.2f;
        float sitFixAt = -1f;
        public static readonly Vector3 StagePos = new Vector3(600f, 0f, 600f);
        const float BedTop = 0.45f;

        void Awake()
        {
            Instance = this;
            stage = new GameObject("HPreviewStage").transform; stage.SetParent(transform); stage.position = StagePos;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); floor.transform.SetParent(stage); floor.transform.localPosition = Vector3.zero; floor.transform.localScale = Vector3.one * 1.2f;
            Tint(floor, new Color(0.42f, 0.4f, 0.38f));
            var bed = GameObject.CreatePrimitive(PrimitiveType.Cube); bed.name = "Bed"; bed.transform.SetParent(stage);
            bed.transform.localScale = new Vector3(2.8f, BedTop, 2.8f); bed.transform.localPosition = new Vector3(0f, BedTop / 2f, 0f);
            Tint(bed, new Color(0.86f, 0.84f, 0.8f));
            headboard = GameObject.CreatePrimitive(PrimitiveType.Cube).transform; headboard.name = "Headboard"; headboard.SetParent(stage);
            headboard.localScale = new Vector3(1.2f, 1.0f, 0.08f); Tint(headboard.gameObject, new Color(0.55f, 0.42f, 0.3f));
            headboard.gameObject.SetActive(false);
            foreach (var c in stage.GetComponentsInChildren<Collider>()) Destroy(c);
            anchor = new GameObject("Anchor").transform; anchor.SetParent(stage); anchor.localPosition = new Vector3(0f, BedTop, 0f); anchor.localRotation = Quaternion.identity;
            cam = new GameObject("HPreviewCam").AddComponent<Camera>(); cam.transform.SetParent(transform);
            cam.depth = 50; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.3f, 0.32f, 0.36f); cam.nearClipPlane = 0.05f; cam.fieldOfView = 40f;
            lamp = new GameObject("HPreviewLamp").AddComponent<Light>(); lamp.transform.SetParent(transform);
            lamp.type = LightType.Point; lamp.range = 8f; lamp.intensity = 2.2f; lamp.transform.position = StagePos + new Vector3(0.8f, 2.6f, -1.2f);
            var lamp2 = new GameObject("HPreviewLamp2").AddComponent<Light>(); lamp2.transform.SetParent(transform);
            lamp2.type = LightType.Point; lamp2.range = 8f; lamp2.intensity = 1.2f; lamp2.transform.position = StagePos + new Vector3(-1.5f, 2.0f, 1.5f);
            Build();
        }
        void OnDestroy() { if (Instance == this) Instance = null; }
        static void Tint(GameObject g, Color c)
        {
            var r = g.GetComponent<Renderer>(); if (r == null) return;
            var m = new Material(r.sharedMaterial); m.color = c; r.material = m;
        }

        public void SetForm(string key)
        {
            for (int i = 0; i < Forms.Length; i++) if (string.Equals(Forms[i].key, key, System.StringComparison.OrdinalIgnoreCase)) { formIx = i; Build(); return; }
        }
        public void SetLetter(string l)
        {
            for (int i = 0; i < Letters.Length; i++) if (string.Equals(Letters[i], l, System.StringComparison.OrdinalIgnoreCase)) { letterIx = i; Build(); return; }
        }
        public void SetPhase(int p) { phase = p; phaseNow = p < 0 ? 0 : p; stepIx = 0; rep = 0; t = 0f; holdUntil = -1f; }

        void Build()
        {
            foreach (var b in bodies) if (b.go != null) Destroy(b.go);
            bodies.Clear();
            var f = Forms[formIx];
            phases = Phases(f.family);
            foreach (var rig in f.rigs)
            {
                bool male = rig == "M";
                string letter = male ? "F" : rig == "F" ? Letters[letterIx] : rig == "P" ? "J" : rig == "S" ? "K" : "B";
                var go = new GameObject("HPrev_" + rig + "_" + letter);
                go.transform.SetParent(anchor, false); go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity;
                var pb = ProtoBody.Create(go, CharLook.Get(letter, male, Color.gray), letter);
                if (pb == null) { Destroy(go); continue; }
                pb.testMode = true; pb.testOutfit = underwear ? "Underwear" : "Uniform"; pb.testFace = ""; pb.testLetter = letter;
                pb.testPose = BodyPose.Stand; pb.testSpeed = 0f; pb.testSitFloorClip = null;
                bodies.Add(new Body { rig = rig, pb = pb, go = go });
            }
            headboard.gameObject.SetActive(false);
            sitFixAt = f.key == "SoloSit" ? Time.time + 0.5f : -1f;
            SetPhase(phase);
        }

        Step Cur { get { var l = phases[phaseNow]; return l[Mathf.Clamp(stepIx, 0, l.Count - 1)]; } }
        string ClipName(string part, string rig) { return "H_" + Forms[formIx].key + "_" + part + "_" + rig; }
        float CurLen()
        {
            if (bodies.Count == 0) return 1f;
            var c = bodies[0].pb.TestFindClip(ClipName(Cur.part, bodies[0].rig));
            return c != null ? Mathf.Max(0.1f, c.length) : 1f;
        }
        float FpsFix()   // 動きは 30コマ/秒 で作ったが、書き出しの記録が 24コマ/秒 のものがある。コマ数どおりの長さで流す
        {
            if (bodies.Count == 0) return 1f;
            var c = bodies[0].pb.TestFindClip(ClipName(Cur.part, bodies[0].rig));
            return c != null && c.frameRate > 1f ? 30f / c.frameRate : 1f;
        }
        void Advance()
        {
            var l = phases[phaseNow];
            rep++;
            if (rep >= l[stepIx].count) { rep = 0; stepIx++; }
            if (stepIx >= l.Count)
            {
                stepIx = 0;
                if (phase < 0) { phaseNow = (phaseNow + 1) % 4; }
            }
            t = 0f;
        }

        void Update()
        {
            if (bodies.Count > 0 && !paused)
            {
                if (holdUntil > 0f) { if (Time.time >= holdUntil) { holdUntil = -1f; Advance(); } }
                else
                {
                    t += Time.deltaTime * Cur.speed * speedMul * FpsFix();
                    float len = CurLen();
                    if (t >= len)
                    {
                        if (Cur.part == "Finish") { t = len; holdUntil = Time.time + 1.5f; }
                        else Advance();
                    }
                }
            }
            foreach (var b in bodies) if (b.pb != null) { b.pb.testClip = ClipName(Cur.part, b.rig); b.pb.testOutfit = underwear ? "Underwear" : "Uniform"; }
            // カメラ：右ボタンで回す・ホイールで近づく
            var ms = Mouse.current;
            if (ms != null)
            {
                bool overUi = ms.position.ReadValue().x < 330f;
                if (ms.rightButton.isPressed && !overUi) { var d = ms.delta.ReadValue(); yaw += d.x * 0.3f; pitch = Mathf.Clamp(pitch - d.y * 0.3f, -5f, 85f); }
                float sc = ms.scroll.ReadValue().y; if (Mathf.Abs(sc) > 0.01f && !overUi) dist = Mathf.Clamp(dist * (sc > 0 ? 0.9f : 1.1f), 0.8f, 8f);
            }
            if (sitFixAt > 0f && Time.time >= sitFixAt) { sitFixAt = -1f; PlaceHeadboard(); }
        }
        void LateUpdate()
        {
            foreach (var b in bodies) if (b.pb != null) b.pb.TestSetClipTime(t);
            PlaceCam();
        }
        void PlaceCam()
        {
            Vector3 target = anchor.position + Vector3.up * 0.35f;
            cam.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            cam.transform.position = target - cam.transform.forward * dist;
        }
        void PlaceHeadboard()   // もたれて座る：背中の後ろに板を置く
        {
            if (bodies.Count == 0) return;
            var an = bodies[0].go.GetComponentInChildren<Animator>();
            if (an == null || !an.isHuman) return;
            var hips = an.GetBoneTransform(HumanBodyBones.Hips); var head = an.GetBoneTransform(HumanBodyBones.Head);
            if (hips == null || head == null) return;
            Vector3 back = head.position - hips.position; back.y = 0f;
            if (back.sqrMagnitude < 1e-4f) return;
            back.Normalize();
            headboard.position = hips.position + back * 0.42f; headboard.position = new Vector3(headboard.position.x, BedTop + 0.5f, headboard.position.z);
            headboard.rotation = Quaternion.LookRotation(back);
            headboard.gameObject.SetActive(true);
        }

        public bool Shot(string path, int w = 960, int h = 720)
        {
            PlaceCam();
            var rt = new RenderTexture(w, h, 24); var prev = cam.targetTexture; cam.targetTexture = rt; cam.Render();
            var act = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            RenderTexture.active = act; cam.targetTexture = prev; Destroy(rt);
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG()); Destroy(tex);
            return true;
        }
        public string Status() { return Forms[formIx].label + " / " + Letters[letterIx] + " / " + PhaseNames[phaseNow] + " / " + Cur.part + " x" + Cur.speed + " t=" + t.ToString("0.00") + "/" + CurLen().ToString("0.00") + " bodies=" + bodies.Count; }
        public void SetView(float y, float p, float d) { yaw = y; pitch = p; dist = d; }

        // ---------- 画面 ----------
        GUIStyle bigLabel;
        void OnGUI()
        {
            if (bigLabel == null) { bigLabel = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold }; }
            GUILayout.BeginArea(new Rect(10, 10, 310, Screen.height - 20), GUI.skin.box);
            GUILayout.Label("動きの確認（F8 で閉じる）", bigLabel);
            GUILayout.Label("形");
            for (int i = 0; i < Forms.Length; i++)
                if (GUILayout.Toggle(formIx == i, Forms[i].label, GUI.skin.button) && formIx != i) { formIx = i; Build(); }
            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Label("相手：" + Letters[letterIx], GUILayout.Width(90));
            if (GUILayout.Button("◀")) { letterIx = (letterIx + Letters.Length - 1) % Letters.Length; Build(); }
            if (GUILayout.Button("▶")) { letterIx = (letterIx + 1) % Letters.Length; Build(); }
            GUILayout.EndHorizontal();
            underwear = GUILayout.Toggle(underwear, "下着にする");
            GUILayout.Space(6);
            GUILayout.Label("場面");
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(phase < 0, "自動", GUI.skin.button) && phase >= 0) SetPhase(-1);
            for (int p = 0; p < 4; p++)
                if (GUILayout.Toggle(phase == p, PhaseNames[p], GUI.skin.button) && phase != p) SetPhase(p);
            GUILayout.EndHorizontal();
            GUILayout.Label("速さ ×" + speedMul.ToString("0.0"));
            speedMul = GUILayout.HorizontalSlider(speedMul, 0.3f, 2.5f);
            paused = GUILayout.Toggle(paused, "止める");
            GUILayout.Space(6);
            string pj; PartJa.TryGetValue(Cur.part, out pj);
            GUILayout.Label("いま：" + PhaseNames[phaseNow] + "　" + (pj ?? Cur.part) + "　速さ " + (Cur.speed * speedMul).ToString("0.0"), bigLabel);
            GUILayout.Label("右ボタンを押しながら動かす：回る／ホイール：近づく");
            GUILayout.EndArea();
        }
    }

    /// <summary>F8 で確認の場を開く・閉じる（どの場面でも使えるよう、いつも置いておく）</summary>
    public class HPreviewKey : MonoBehaviour
    {
        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || !kb.f8Key.wasPressedThisFrame) return;
            if (HPreview.Instance != null) Destroy(HPreview.Instance.gameObject); else HPreview.Open();
        }
    }
}
