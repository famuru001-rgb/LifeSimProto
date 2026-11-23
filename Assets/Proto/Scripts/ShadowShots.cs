using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LifeSimProto
{
    /// <summary>
    /// 確認用（2026-11-23 本体 S08 追記2）：キャラの肌に出る影のまだら（ぎざぎざの島）の原因を、同じ姿勢・同じ瞬間で、影の出し方だけを変えて撮りくらべる。
    /// 正座・椅子・立つ × ひざ・太もも・顔・胸の下 × 6通り（今のまま／影を受けない／影を落とさない／ずらし大／浮かせ1.5cm／浮かせ3cm）。
    /// 保存先：仮モデル\Unity確認\影_（Tag）\（記号）_（姿勢）_（向き）_（番号と名前）.png。命令 toonshadow shots 記号[,記号…] Tag。住人・セーブは触らない。
    /// </summary>
    public class ShadowShots : MonoBehaviour
    {
        public static string Letters = "I,Q,R";
        public static string Tag = "s08";
        public static bool Busy;
        public static string Set = "a";   // a：影の地図の比べ・b：2段の影の境目の比べ
        // 名前, 影を見ない, 浮かせ(m), 影を落とさない, ずらし大, ぼかし足し, 境目足し
        static readonly object[][] SetA = {
            new object[] { "0今のまま", 0f, 0f, false, false, 0f, 0f }, new object[] { "1影を受けない", 1f, 0f, false, false, 0f, 0f },
            new object[] { "2影を落とさない", 0f, 0f, true, false, 0f, 0f }, new object[] { "3ずらし大", 0f, 0f, false, true, 0f, 0f },
            new object[] { "4浮かせ1.5cm", 0f, 0.015f, false, false, 0f, 0f }, new object[] { "5浮かせ3cm", 0f, 0.03f, false, false, 0f, 0f } };
        static readonly object[][] SetB = {
            new object[] { "0今のまま", 0f, 0f, false, false, 0f, 0f }, new object[] { "1影を受けない", 1f, 0f, false, false, 0f, 0f },
            new object[] { "2ぼかし+0.15", 0f, 0f, false, false, 0.15f, 0f }, new object[] { "3ぼかし+0.30", 0f, 0f, false, false, 0.30f, 0f },
            new object[] { "4境目-0.25", 0f, 0f, false, false, 0f, -0.25f }, new object[] { "5浮かせ2cm_ぼかし+0.15", 0f, 0.02f, false, false, 0.15f, 0f } };
        // c：肌の影の境目のぼかし（材質の _ShadeSoftness）の前と後。8つめ＝肌のぼかし（負なら変えない）。椅子は撮らない
        static readonly object[][] SetC = {
            new object[] { "0前_肌0.16", 0f, 0f, false, false, 0f, 0f, 0.16f }, new object[] { "1後_肌0.35", 0f, 0f, false, false, 0f, 0f, 0.35f },
            new object[] { "2後_肌0.45", 0f, 0f, false, false, 0f, 0f, 0.45f }, new object[] { "3後0.45_影を受けない", 1f, 0f, false, false, 0f, 0f, 0.45f } };
        // c に色の表の材質（ティナ・ティアの体）も入れた（2026-11-23）
        const int PhotoLayer = 31;
        Camera cam; RenderTexture rt; Light lamp; string dir;

        IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        void Shot(Vector3 from, Vector3 at, string file) { SwayShots.Shot(cam, rt, lamp, from, at, Path.Combine(dir, file + ".png")); }

        static Light Sun()
        {
            if (RenderSettings.sun != null) return RenderSettings.sun;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional && l.isActiveAndEnabled && l.shadows != LightShadows.None) return l;
            return null;
        }

        IEnumerator Start()
        {
            Busy = true;
            float keepOffset = Shader.GetGlobalFloat("_LSShadowNormalOffset"), keepIgnore = Shader.GetGlobalFloat("_LSShadowIgnore");
            dir = Path.Combine(ProtoPhotoBooth.OutDir, "影_" + Tag);
            Directory.CreateDirectory(dir);
            cam = new GameObject("ShadowCam").AddComponent<Camera>();
            cam.enabled = false; cam.fieldOfView = 30f; cam.nearClipPlane = 0.02f;
            cam.cullingMask = 1 << PhotoLayer; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.3f, 0.32f, 0.36f);
            rt = new RenderTexture(520, 520, 24); cam.targetTexture = rt;
            lamp = new GameObject("ShadowLamp").AddComponent<Light>(); lamp.type = LightType.Point; lamp.range = 10f; lamp.intensity = 0.6f;
            var sun = Sun();
            var sunData = sun != null ? sun.GetComponent<UniversalAdditionalLightData>() : null;
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            var log = new StringBuilder();
            log.AppendLine("影の出し方の比べ " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "・太陽 " + (sun != null ? sun.name + " 向き " + sun.transform.forward.ToString("F2") + " 強さ " + sun.intensity.ToString("0.00") : "なし")
                + "・影のずらし（URP）深さ " + (urp != null ? urp.shadowDepthBias.ToString("0.00") : "?") + "・面 " + (urp != null ? urp.shadowNormalBias.ToString("0.00") : "?"));
            var vs = Set == "c" ? SetC : Set == "b" ? SetB : SetA;
            string[] vn = new string[vs.Length];
            for (int q = 0; q < vs.Length; q++) vn[q] = (string)vs[q][0];
            float keepSoft = Shader.GetGlobalFloat("_LSShadeSoftAdd"), keepStep = Shader.GetGlobalFloat("_LSShadeStepAdd");
            int li = 0;
            foreach (var raw in Letters.Split(','))
            {
                string letter = raw.Trim().ToUpperInvariant();
                if (letter.Length == 0) continue;
                if (Resources.Load<GameObject>("Chars/" + letter + "/Char" + letter) == null) { log.AppendLine(letter + "：モデルがない"); continue; }
                Vector3 basePos = new Vector3(440f + li * 8f, 0f, 180f);
                li++;
                string[] poses = { "正座", "椅子", "立つ" };
                for (int pi = 0; pi < 3; pi++)
                {
                    if (Set == "c" && pi == 1) continue;
                    Vector3 pos = basePos + new Vector3(pi * 2.6f, 0f, 0f);
                    var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    floor.name = "ShadowFloor"; floor.layer = PhotoLayer;
                    floor.transform.position = pos + new Vector3(0f, -0.05f, 0f); floor.transform.localScale = new Vector3(2.2f, 0.1f, 2.2f);
                    Destroy(floor.GetComponent<Collider>());
                    floor.GetComponent<Renderer>().material.color = new Color(0.45f, 0.42f, 0.38f);
                    GameObject chairGo = null;
                    if (pi == 1)
                    {
                        var pf = Resources.Load<GameObject>("Furniture/Furn_Chair");
                        if (pf != null) { chairGo = Instantiate(pf); chairGo.transform.position = pos; chairGo.transform.rotation = pf.transform.localRotation; }
                        else { chairGo = GameObject.CreatePrimitive(PrimitiveType.Cube); chairGo.transform.position = pos + Vector3.up * 0.45f; chairGo.transform.localScale = new Vector3(0.48f, 0.06f, 0.48f); }
                        foreach (var c in chairGo.GetComponentsInChildren<Collider>(true)) Destroy(c);
                        foreach (var r in chairGo.GetComponentsInChildren<Renderer>(true)) r.gameObject.layer = PhotoLayer;
                    }
                    var go = new GameObject("Shadow_" + letter + "_" + poses[pi]);
                    go.transform.position = pos;
                    var pb = ProtoBody.Create(go, CharLook.Get(letter, false, Color.gray), letter);
                    if (pb == null) { Destroy(go); if (chairGo != null) Destroy(chairGo); Destroy(floor); continue; }
                    pb.testMode = true; pb.testLetter = letter; pb.testOutfit = "Uniform"; pb.testFace = ""; pb.testSpeed = 0f;
                    if (pi == 0) { pb.testPose = BodyPose.SitFloor; pb.testSitFloorClip = "SitFloor"; }
                    else if (pi == 1) { pb.testPose = BodyPose.Sit; pb.testClip = MotionBook.ChairSit(letter, false); }
                    else pb.testPose = BodyPose.Stand;
                    var rends = go.GetComponentsInChildren<Renderer>(true);
                    foreach (var r in rends) r.gameObject.layer = PhotoLayer;
                    for (float w = 0f; w < 2.0f; w += Time.deltaTime) yield return null;   // 座りの補正と揺れが落ち着くまで
                    var tr = go.transform;
                    Transform kn = pb.Bone("LeftLeg"), up = pb.Bone("LeftUpLeg"), head = pb.Bone("Head"), ch = pb.Bone("Spine2") ?? pb.Bone("Spine1");
                    if (kn == null || up == null || head == null) { log.AppendLine(letter + " " + poses[pi] + "：骨がない"); Destroy(go); if (chairGo != null) Destroy(chairGo); Destroy(floor); continue; }
                    float keepScale = Time.timeScale;
                    Time.timeScale = 0f;   // 同じ瞬間で比べる（体の動きは止まる。影の地図は毎コマ描き直される）
                    var keepMode = new ShadowCastingMode[rends.Length];
                    for (int k = 0; k < rends.Length; k++) keepMode[k] = rends[k].shadowCastingMode;
                    bool keepUse = sunData != null && sunData.usePipelineSettings;
                    float keepB = sun != null ? sun.shadowBias : 0f, keepN = sun != null ? sun.shadowNormalBias : 0f;
                    string pre = letter + "_" + poses[pi] + "_";
                    var skinMats = new System.Collections.Generic.List<Material>();
                    foreach (var r in rends) foreach (var m in r.sharedMaterials)
                        if (m != null && m.shader != null && m.shader.name == "LifeSim/Toon" && ((m.HasProperty("_ScatterStrength") && m.GetFloat("_ScatterStrength") > 0f) || m.name.Contains("SharedPalette")) && !skinMats.Contains(m)) skinMats.Add(m);
                    var keepSkin = new float[skinMats.Count];
                    for (int k = 0; k < skinMats.Count; k++) keepSkin[k] = skinMats[k].GetFloat("_ShadeSoftness");
                    for (int v = 0; v < vn.Length; v++)
                    {
                        var sp = vs[v];
                        Shader.SetGlobalFloat("_LSShadowIgnore", (float)sp[1]);
                        Shader.SetGlobalFloat("_LSShadowNormalOffset", (float)sp[2]);
                        Shader.SetGlobalFloat("_LSShadeSoftAdd", (float)sp[5]);
                        Shader.SetGlobalFloat("_LSShadeStepAdd", (float)sp[6]);
                        for (int k = 0; k < skinMats.Count; k++) skinMats[k].SetFloat("_ShadeSoftness", sp.Length > 7 && (float)sp[7] >= 0f ? (float)sp[7] : keepSkin[k]);
                        for (int k = 0; k < rends.Length; k++) rends[k].shadowCastingMode = (bool)sp[3] ? ShadowCastingMode.Off : keepMode[k];
                        if (sun != null)
                        {
                            if ((bool)sp[4]) { if (sunData != null) sunData.usePipelineSettings = false; sun.shadowBias = 1.0f; sun.shadowNormalBias = 1.5f; }
                            else { if (sunData != null) sunData.usePipelineSettings = keepUse; sun.shadowBias = keepB; sun.shadowNormalBias = keepN; }
                        }
                        yield return Frames(2);
                        Vector3 k0 = kn.position, thigh = (up.position + k0) * 0.5f, h = head.position, c0 = ch != null ? ch.position - tr.up * 0.12f : h - Vector3.up * 0.4f;
                        Shot(k0 + (tr.forward - tr.right).normalized * 0.5f + Vector3.up * 0.12f, k0, pre + "ひざ_" + vn[v]);
                        Shot(thigh - tr.right * 0.75f + Vector3.up * 0.15f, thigh, pre + "太もも_" + vn[v]);
                        Shot(h + tr.forward * 0.55f + Vector3.up * 0.03f, h, pre + "顔_" + vn[v]);
                        Shot(c0 + tr.forward * 0.6f - Vector3.up * 0.2f, c0, pre + "胸の下_" + vn[v]);
                    }
                    for (int k = 0; k < skinMats.Count; k++) skinMats[k].SetFloat("_ShadeSoftness", keepSkin[k]);
                    log.AppendLine(letter + " " + poses[pi] + "：肌の材質 " + skinMats.Count);
                    Shader.SetGlobalFloat("_LSShadowIgnore", keepIgnore);
                    Shader.SetGlobalFloat("_LSShadowNormalOffset", keepOffset);
                    Shader.SetGlobalFloat("_LSShadeSoftAdd", keepSoft);
                    Shader.SetGlobalFloat("_LSShadeStepAdd", keepStep);
                    if (sun != null) { if (sunData != null) sunData.usePipelineSettings = keepUse; sun.shadowBias = keepB; sun.shadowNormalBias = keepN; }
                    Time.timeScale = keepScale;
                    log.AppendLine(letter + " " + poses[pi] + "：撮った");
                    Destroy(go); if (chairGo != null) Destroy(chairGo); Destroy(floor);
                    yield return null;
                }
            }
            File.WriteAllText(Path.Combine(dir, "記録.txt"), log.ToString(), Encoding.UTF8);
            File.WriteAllText(Path.Combine(dir, "済み.txt"), System.DateTime.Now.ToString());
            Destroy(cam.gameObject); Destroy(lamp.gameObject); rt.Release();
            Busy = false;
            Destroy(gameObject);
        }
    }
}
