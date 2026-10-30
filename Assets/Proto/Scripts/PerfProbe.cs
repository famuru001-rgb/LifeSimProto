using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 重さを測る（2026-10-30 軽量化のチャット）。ClaudeBridgeExtra.cs の命令 perf から使う。
    /// ・perf（秒）（名前）＝その秒数のあいだ測って Logs\perf.txt の最後に結果を足す
    /// ・perf sway off／on＝揺れの計算を止める／戻す（試し）
    /// ・perf offscreen off／on＝画面外のキャラの形の計算を止める／戻す（試し）
    /// ・perf clones（人数）＝本番モデルの見本を足して、人数が増えたときの重さを試す／perf clear＝試しを全部元に戻す
    /// ゲームの決まりは変えない。試しのスイッチは再生をやめると元に戻る。
    /// </summary>
    public class PerfProbe : MonoBehaviour
    {
        // ---- 自分のスクリプトにかかった時間（1コマごとに足していく） ----
        public const int Body = 0, Sway = 1, Sim = 2, Hud = 3, Count = 4;
        static readonly string[] Names = { "体の動き（ProtoBody 全体）", "　うち揺れ", "寮生の考え・移動（SimCharacter）", "画面の文字（SimHud）" };
        static readonly long[] acc = new long[Count];
        public static long Stamp() { return Stopwatch.GetTimestamp(); }
        public static void Add(int k, long t0) { acc[k] += Stopwatch.GetTimestamp() - t0; }

        // ---- 試しのスイッチ ----
        public static bool NoSway;
        public static bool OffscreenOff;
        public static readonly List<GameObject> Clones = new List<GameObject>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            NoSway = false; OffscreenOff = false; Clones.Clear();
            for (int i = 0; i < Count; i++) acc[i] = 0;
        }

        static string LogPath { get { return Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/perf.txt")); } }

        public static string Begin(string label, float seconds)
        {
            var go = new GameObject("PerfProbe_" + label);
            var p = go.AddComponent<PerfProbe>();
            p.label = label; p.seconds = Mathf.Clamp(seconds, 1f, 60f);
            return "perf: " + p.label + " を " + p.seconds + "秒 測っています（結果は Logs\\perf.txt）";
        }

        // ---- 画面外のキャラの形の計算 ----
        public static string SetOffscreen(bool on)
        {
            OffscreenOff = !on;
            int n = 0;
            foreach (var pb in FindObjectsByType<ProtoBody>(FindObjectsSortMode.None))
            {
                if (!pb.IsRealModel) continue;
                foreach (var smr in pb.GetComponentsInChildren<SkinnedMeshRenderer>(true)) { smr.updateWhenOffscreen = on; n++; }
            }
            return "perf offscreen " + (on ? "on" : "off") + ": " + n + "個";
        }

        // ---- 人数を増やす試し（本番モデルの見本を寮の今カメラが見ている所に並べる。半分は歩く動き） ----
        public static string AddClones(LoungeWorld w, int n)
        {
            var letters = new List<string>();
            foreach (var c in w.characters)
            {
                var pb = c.GetComponent<ProtoBody>();
                if (pb != null && pb.IsRealModel && !letters.Contains(c.letter)) letters.Add(c.letter);
            }
            if (letters.Count == 0) return "perf clones: 本番モデルがない";
            Vector3 center = Vector3.zero;
            var rig = w.mainCamera != null ? w.mainCamera.GetComponent<CameraRig>() : null;
            if (rig != null) center = rig.pivot;
            int made = 0;
            for (int i = 0; i < n; i++)
            {
                int k = Clones.Count;
                string l = letters[k % letters.Count];
                float x = (k % 6 - 2.5f) * 0.9f, z = (k / 6 - 2f) * 0.9f;
                Vector3 pos = w.NearestFree(center + new Vector3(x, 0f, z));
                var go = new GameObject("PerfClone_" + k + "_" + l);
                go.transform.position = pos;
                go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                var pb = ProtoBody.Create(go, CharLook.Get(l, false, Color.gray), l);
                if (pb == null) { Destroy(go); continue; }
                pb.testMode = true; pb.perfClone = true; pb.testPose = BodyPose.Stand; pb.testOutfit = "Uniform"; pb.testFace = "";
                pb.testSpeed = (k % 2 == 0) ? 1.2f : 0f;
                if (OffscreenOff) foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = false;
                Clones.Add(go); made++;
            }
            return "perf clones: " + made + "人足した（見本は全部で " + Clones.Count + "人。使った記号 " + string.Join(",", letters.ToArray()) + "・場所 " + center.x.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " " + center.y.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " " + center.z.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "）";
        }

        public static string Clear()
        {
            foreach (var g in Clones) if (g != null) Destroy(g);
            int n = Clones.Count;
            Clones.Clear();
            NoSway = false;
            SetOffscreen(false);   // ゲームのふだんの設定（2026-10-30 から画面外は計算しない）
            Render("reset", "");
            return "perf clear: 見本 " + n + "人を消し、揺れと画面外の計算を元に戻した";
        }

        // ---- 描き方の試し（影の段数・影の距離・SSAO・点の明かり）。設定のファイルを直接変えるので、終わったら perf render reset か perf clear で必ず戻す ----
        static UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset Urp
        {
            get
            {
                var a = QualitySettings.renderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
                return a != null ? a : UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            }
        }
        static UnityEngine.Rendering.Universal.ScriptableRendererFeature Feature(string part)
        {
            var u = Urp; if (u == null) return null;
            var f = typeof(UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset).GetField("m_RendererDataList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var list = f != null ? f.GetValue(u) as UnityEngine.Rendering.Universal.ScriptableRendererData[] : null;
            if (list == null) return null;
            foreach (var d in list) if (d != null) foreach (var rf in d.rendererFeatures) if (rf != null && rf.name.Contains(part)) return rf;
            return null;
        }
        static bool rSaved; static int oCasc; static float oDist; static bool oSsao;
        static readonly List<Light> offLights = new List<Light>();
        static void SaveRender()
        {
            if (rSaved || Urp == null) return;
            rSaved = true; oCasc = Urp.shadowCascadeCount; oDist = Urp.shadowDistance;
            var s = Feature("Ambient"); oSsao = s != null && s.isActive;
        }
        public static string RenderInfo()
        {
            var u = Urp; if (u == null) return "描き方：URP の設定が見つからない";
            var s = Feature("Ambient");
            return "描き方：影の段数 " + u.shadowCascadeCount + "・影の距離 " + u.shadowDistance + "m・SSAO " + (s == null ? "なし" : (s.isActive ? "あり" : "止め")) + "・止めた点の明かり " + offLights.Count + "個";
        }
        public static string Render(string what, string val)
        {
            var u = Urp; if (u == null) return "perf render: URP の設定が見つからない";
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            if (what == "reset")
            {
                if (rSaved) { u.shadowCascadeCount = oCasc; u.shadowDistance = oDist; var s0 = Feature("Ambient"); if (s0 != null) s0.SetActive(oSsao); rSaved = false; }
                foreach (var l in offLights) if (l != null) l.enabled = true;
                offLights.Clear();
                return "perf render reset: 元に戻した（" + RenderInfo() + "）";
            }
            SaveRender();
            if (what == "cascades") u.shadowCascadeCount = Mathf.Clamp(int.Parse(val, ci), 1, 4);
            else if (what == "shadowdist") u.shadowDistance = float.Parse(val, ci);
            else if (what == "ssao") { var s = Feature("Ambient"); if (s != null) s.SetActive(val != "off"); }
            else if (what == "lights")
            {
                if (val == "off") { foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.enabled && l.type == LightType.Point) { l.enabled = false; offLights.Add(l); } }
                else { foreach (var l in offLights) if (l != null) l.enabled = true; offLights.Clear(); }
            }
            return "perf render " + what + " " + val + ": " + RenderInfo();
        }

        // ---- 測る ----
        string label; float seconds;
        float t; int frames; bool started;
        readonly List<float> frameMs = new List<float>();
        readonly List<float> mainMs = new List<float>();
        ProfilerRecorder rMain, rRender, rBatches, rSetPass, rDraw, rTris, rVerts, rShadow, rGc, rUsed, rTex, rMesh;
        double sBatches, sSetPass, sDraw, sTris, sVerts, sShadow, sGc; long mxBatches, mxTris, mxGc;

        void OnEnable()
        {
            rMain = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
            rRender = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Render Thread", 1);
            rBatches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            rSetPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            rDraw = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            rTris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            rVerts = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count");
            rShadow = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Shadow Casters Count");
            rGc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            rUsed = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
            rTex = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Texture Memory");
            rMesh = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Mesh Memory");
        }

        void OnDisable()
        {
            rMain.Dispose(); rRender.Dispose(); rBatches.Dispose(); rSetPass.Dispose(); rDraw.Dispose(); rTris.Dispose();
            rVerts.Dispose(); rShadow.Dispose(); rGc.Dispose(); rUsed.Dispose(); rTex.Dispose(); rMesh.Dispose();
        }

        void Update()
        {
            // 始めの数コマは落ち着かないので捨てる
            if (!started)
            {
                t += Time.unscaledDeltaTime;
                if (t < 0.5f) return;
                started = true; t = 0f;
                for (int i = 0; i < Count; i++) acc[i] = 0;
                return;
            }
            float dt = Time.unscaledDeltaTime;
            t += dt; frames++;
            frameMs.Add(dt * 1000f);
            if (rMain.Valid && rMain.LastValue > 0) mainMs.Add(rMain.LastValue / 1e6f);
            Sum(rBatches, ref sBatches, ref mxBatches); long dummy = 0;
            Sum(rSetPass, ref sSetPass, ref dummy); Sum(rDraw, ref sDraw, ref dummy);
            Sum(rTris, ref sTris, ref mxTris); Sum(rVerts, ref sVerts, ref dummy); Sum(rShadow, ref sShadow, ref dummy);
            Sum(rGc, ref sGc, ref mxGc);
            if (t >= seconds) { Write(); Destroy(gameObject); }
        }

        static void Sum(ProfilerRecorder r, ref double s, ref long mx)
        {
            if (!r.Valid) return;
            long v = r.LastValue; s += v; if (v > mx) mx = v;
        }

        static float Avg(List<float> l) { if (l.Count == 0) return 0f; float s = 0f; foreach (var v in l) s += v; return s / l.Count; }
        static float Pct(List<float> l, float p)
        {
            if (l.Count == 0) return 0f;
            var c = new List<float>(l); c.Sort();
            return c[Mathf.Clamp(Mathf.RoundToInt((c.Count - 1) * p), 0, c.Count - 1)];
        }

        void Write()
        {
            var w = LoungeWorld.Instance;
            var sb = new StringBuilder();
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            sb.AppendLine("== " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "　" + label);
            if (w != null) sb.AppendLine("ゲーム：" + w.Day + "日目 " + ScheduleMaker.Clock(w.Now) + "・速さ " + w.userSpeed + "倍");
            sb.AppendLine("試し：揺れ " + (NoSway ? "止め" : "あり") + "・画面外の計算 " + (OffscreenOff ? "止め" : "あり") + "・見本 " + Clones.Count + "人");
            sb.AppendLine(RenderInfo());
            sb.AppendLine("画面：" + Screen.width + "x" + Screen.height + "・Unityが前に出ているか " + Application.isFocused + "・コマ数の上限 " + Application.targetFrameRate + "・垂直同期 " + QualitySettings.vSyncCount);

            float avg = Avg(frameMs);
            sb.AppendLine("■ コマ：" + frames + "コマ / " + t.ToString("0.0", ci) + "秒 → 1秒に " + (frames / Mathf.Max(0.01f, t)).ToString("0.0", ci) + "コマ");
            sb.AppendLine("  1コマの時間 平均 " + avg.ToString("0.0", ci) + "ms・遅い方1割 " + Pct(frameMs, 0.9f).ToString("0.0", ci) + "ms・いちばん遅い " + Pct(frameMs, 1f).ToString("0.0", ci) + "ms");
            if (mainMs.Count > 0) sb.AppendLine("  CPU（メインの処理）平均 " + Avg(mainMs).ToString("0.0", ci) + "ms・遅い方1割 " + Pct(mainMs, 0.9f).ToString("0.0", ci) + "ms");
            if (rRender.Valid && rRender.LastValue > 0) sb.AppendLine("  描く処理（Render Thread）最後のコマ " + (rRender.LastValue / 1e6f).ToString("0.0", ci) + "ms");

            sb.AppendLine("■ 自分のスクリプト（1コマあたり）");
            double tick = Stopwatch.Frequency / 1000.0;
            for (int i = 0; i < Count; i++) sb.AppendLine("  " + Names[i] + "：" + (acc[i] / tick / Mathf.Max(1, frames)).ToString("0.00", ci) + "ms");

            int f = Mathf.Max(1, frames);
            sb.AppendLine("■ 描く量（1コマ平均）");
            if (rBatches.Valid) sb.AppendLine("  まとめて描く回数（Batches）" + (long)(sBatches / f) + "（最大 " + mxBatches + "）・SetPass " + (long)(sSetPass / f) + "・Draw Calls " + (long)(sDraw / f));
            if (rTris.Valid) sb.AppendLine("  三角形 " + (long)(sTris / f) + "（最大 " + mxTris + "）・頂点 " + (long)(sVerts / f) + "・影を落とす物 " + (long)(sShadow / f));
            if (rGc.Valid) sb.AppendLine("  使い捨てのメモリ（GC）1コマ平均 " + (long)(sGc / f / 1024) + "KB（最大 " + (mxGc / 1024) + "KB）");
            if (rUsed.Valid && rUsed.LastValue > 0) sb.AppendLine("  メモリ全体 " + (rUsed.LastValue >> 20) + "MB" + (rTex.Valid && rTex.LastValue > 0 ? "・画像 " + (rTex.LastValue >> 20) + "MB" : "") + (rMesh.Valid && rMesh.LastValue > 0 ? "・形 " + (rMesh.LastValue >> 20) + "MB" : ""));

            Counts(sb, w);
            try { File.AppendAllText(LogPath, sb.ToString() + "\n", Encoding.UTF8); } catch (System.Exception e) { UnityEngine.Debug.LogWarning("perf: " + e.Message); }
        }

        // ---- 場面の中にある物の数 ----
        static void Counts(StringBuilder sb, LoungeWorld w)
        {
            int real = 0, proto = 0, springs = 0;
            foreach (var pb in FindObjectsByType<ProtoBody>(FindObjectsSortMode.None))
            {
                if (pb.IsRealModel) real++; else proto++;
                springs += pb.SpringCount;
            }
            int smr = 0, smrVis = 0, smrOff = 0, rend = 0, rendVis = 0, rendOn = 0;
            var mats = new HashSet<Material>();
            var texs = new HashSet<Texture>();
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                rend++;
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                rendOn++;
                if (r.isVisible) rendVis++;
                var s = r as SkinnedMeshRenderer;
                if (s != null) { smr++; if (s.isVisible) smrVis++; if (s.updateWhenOffscreen) smrOff++; }
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null || !mats.Add(m)) continue;
                    foreach (var id in m.GetTexturePropertyNameIDs()) { var tx = m.GetTexture(id); if (tx != null) texs.Add(tx); }
                }
            }
            long texBytes = 0; int tex2k = 0;
            foreach (var tx in texs) { texBytes += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(tx); if (tx.width >= 2048) tex2k++; }
            int lights = 0, pointL = 0, shadowL = 0;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (!l.enabled || !l.gameObject.activeInHierarchy) continue;
                lights++; if (l.type == LightType.Point) pointL++; if (l.shadows != LightShadows.None) shadowL++;
            }
            sb.AppendLine("■ 場面の中");
            sb.AppendLine("  キャラ：寮生 " + (w != null ? w.characters.Count : 0) + "人（本番モデル " + real + "体・仮の人形 " + proto + "体。見本も含む）・揺れの骨 合計 " + springs + "本");
            sb.AppendLine("  体の形（SkinnedMesh）" + smr + "個（いま映っている " + smrVis + "・画面外も計算する設定 " + smrOff + "）");
            sb.AppendLine("  描く物 全部 " + rend + "個（出している " + rendOn + "・いま映っている " + rendVis + "）・材質 " + mats.Count + "種類・画像 " + texs.Count + "枚（2048以上 " + tex2k + "枚・合計 約" + (texBytes >> 20) + "MB）");
            sb.AppendLine("  明かり " + lights + "個（点の明かり " + pointL + "・影を出す " + shadowL + "）");
        }
    }
}
