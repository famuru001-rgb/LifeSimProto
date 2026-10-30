using System.Collections;
using System.IO;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 確認用（2026-10-30 モーションのチャット）：動きの候補の見比べ。
    /// Logs\motionshots_list.txt の1行（カテゴリ|動きの名前）ごとに、リア（I）にその動きを流し、
    /// 長さの 0〜100% を6か所（6秒より長い動きは最初の6秒）で斜め前から撮る。
    /// 保存先：仮モデル\Unity確認\動き候補\（カテゴリ）_（動き）_（番号）.png。命令 motionshots。ゲームの進行には影響しない。
    /// </summary>
    public class MotionShots : MonoBehaviour
    {
        public static string Letter = "I";
        const int PhotoLayer = 31;

        IEnumerator Start()
        {
            string dir = Path.Combine(ProtoPhotoBooth.OutDir, "動き候補");
            Directory.CreateDirectory(dir);
            string listPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/motionshots_list.txt"));
            string[] lines = File.Exists(listPath) ? File.ReadAllLines(listPath) : new string[0];
            var cam = new GameObject("MotionCam").AddComponent<Camera>();
            cam.enabled = false; cam.fieldOfView = 30f; cam.nearClipPlane = 0.05f;
            cam.cullingMask = 1 << PhotoLayer; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.3f, 0.32f, 0.36f);
            var rt = new RenderTexture(360, 420, 24); cam.targetTexture = rt;
            var lamp = new GameObject("MotionLamp").AddComponent<Light>(); lamp.type = LightType.Point; lamp.range = 10f; lamp.intensity = 1.2f;
            var info = new System.Text.StringBuilder("カテゴリ,動き,長さ(秒)\n");
            int n = 0;
            foreach (var raw in lines)
            {
                var a = raw.Trim().Split('|');
                if (a.Length < 2) continue;
                string cat = a[0], clipName = a[1];
                var go = new GameObject("Motion_" + clipName);
                go.transform.position = new Vector3(140f + (n % 10) * 3f, 0f, 200f + (n / 10) * 3f);
                var pb = ProtoBody.Create(go, CharLook.Get(Letter, false, Color.gray), Letter);
                if (pb == null) { Destroy(go); continue; }
                var clip = pb.TestFindClip(clipName);
                if (clip == null) { info.Append(cat + "," + clipName + ",見つからない\n"); Destroy(go); continue; }
                pb.testMode = true; pb.testPose = BodyPose.Stand; pb.testOutfit = "Uniform"; pb.testFace = ""; pb.testClip = clipName;
                foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.gameObject.layer = PhotoLayer;
                for (int w = 0; w < 4; w++) yield return null;
                float len = Mathf.Min(clip.length, 6f);
                info.Append(cat + "," + clipName + "," + clip.length.ToString("0.0") + "\n");
                var tr = go.transform;
                Vector3 c = tr.position + Vector3.up * 0.85f;
                Vector3 from = c + (tr.forward * 0.85f + tr.right * 0.5f).normalized * 3.3f + Vector3.up * 0.25f;
                for (int k = 0; k < 6; k++)
                {
                    pb.TestSetClipTime(len * k / 5f);
                    yield return null; yield return null;
                    SwayShots.Shot(cam, rt, lamp, from, c, Path.Combine(dir, cat + "_" + clipName + "_" + k + ".png"));
                }
                Destroy(go);
                n++;
            }
            File.WriteAllText(Path.Combine(dir, "一覧.csv"), info.ToString(), System.Text.Encoding.UTF8);
            File.WriteAllText(Path.Combine(dir, "済み.txt"), System.DateTime.Now.ToString());
            Destroy(cam.gameObject); Destroy(lamp.gameObject); rt.Release();
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 確認用（2026-10-30 モーションのチャット）：気持ちの身ぶりを、立つ・椅子・正座で流して 0.5 秒ごとに撮る。
    /// 保存先：仮モデル\Unity確認\身ぶり\（身ぶり）_（姿勢）_（番号）.png。命令 gestureshots。
    /// </summary>
    public class GestureShots : MonoBehaviour
    {
        public static string Letter = "I";
        const int PhotoLayer = 31;
        IEnumerator Start()
        {
            string dir = Path.Combine(ProtoPhotoBooth.OutDir, "身ぶり");
            Directory.CreateDirectory(dir);
            var cam = new GameObject("GestCam").AddComponent<Camera>();
            cam.enabled = false; cam.fieldOfView = 30f; cam.nearClipPlane = 0.05f;
            cam.cullingMask = 1 << PhotoLayer; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.3f, 0.32f, 0.36f);
            var rt = new RenderTexture(360, 420, 24); cam.targetTexture = rt;
            var lamp = new GameObject("GestLamp").AddComponent<Light>(); lamp.type = LightType.Point; lamp.range = 10f; lamp.intensity = 1.2f;
            string[] gestures = { "Nod", "HeadShake", "Surprised", "Surprised2", "Troubled", "Think", "Shy", "Cry", "Angry", "Thanks" };
            var poses = new[] { BodyPose.Stand, BodyPose.Sit, BodyPose.SitFloor };
            string[] pn = { "立つ", "椅子", "正座" };
            int n = 0;
            foreach (var g in gestures)
                for (int pi = 0; pi < poses.Length; pi++)
                {
                    var go = new GameObject("Gest_" + g);
                    go.transform.position = new Vector3(180f + (n % 10) * 3f, 0f, 200f + (n / 10) * 3f); n++;
                    var pb = ProtoBody.Create(go, CharLook.Get(Letter, false, Color.gray), Letter);
                    if (pb == null) { Destroy(go); continue; }
                    pb.testMode = true; pb.testPose = poses[pi]; pb.testOutfit = "Uniform"; pb.testFace = ""; pb.testSitFloorClip = null;
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.gameObject.layer = PhotoLayer;
                    float wait = poses[pi] == BodyPose.SitFloor ? 3.3f : 0.4f;
                    for (float w = 0f; w < wait; w += Time.deltaTime) yield return null;
                    pb.Gesture(g, 3.0f);
                    var tr = go.transform;
                    Vector3 c = tr.position + Vector3.up * (poses[pi] == BodyPose.Stand ? 0.85f : 0.6f);
                    Vector3 from = c + (tr.forward * 0.85f + tr.right * 0.5f).normalized * 3.0f + Vector3.up * 0.25f;
                    float t = 0f, next = 0.5f; int k = 0;
                    while (t < 3.01f)
                    {
                        t += Time.deltaTime;
                        if (t >= next) { SwayShots.Shot(cam, rt, lamp, from, c, Path.Combine(dir, g + "_" + pn[pi] + "_" + k + ".png")); k++; next += 0.5f; }
                        yield return null;
                    }
                    Destroy(go);
                }
            File.WriteAllText(Path.Combine(dir, "済み.txt"), System.DateTime.Now.ToString());
            Destroy(cam.gameObject); Destroy(lamp.gameObject); rt.Release();
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 確認用（2026-10-30 モーションのチャット）：動きの候補を動画で見比べる。
    /// Logs\motionshots_list.txt のカテゴリごとに、リア（I）を横に並べて（6人ずつ段にする）同時に動かし、
    /// ゲームの時間を 1/20 秒ずつ進めながら正面から撮る（最長 8 秒）。
    /// 保存先：仮モデル\Unity確認\動き候補_動画\（カテゴリ）\f_0000.jpg と 並び.txt。命令 motionvideo。
    /// </summary>
    public class MotionVideo : MonoBehaviour
    {
        public static string Letter = "I";
        const int PhotoLayer = 31;
        const float Fps = 20f, PxPerM = 140f, ColW = 1.5f, RowH = 2.2f;

        IEnumerator Start()
        {
            string root = Path.Combine(ProtoPhotoBooth.OutDir, "動き候補_動画");
            string listPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/motionshots_list.txt"));
            var cats = new System.Collections.Generic.List<string>();
            var byCat = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>();
            foreach (var raw in (File.Exists(listPath) ? File.ReadAllLines(listPath) : new string[0]))
            {
                var a = raw.Trim().Split('|'); if (a.Length < 2) continue;
                if (!byCat.ContainsKey(a[0])) { byCat[a[0]] = new System.Collections.Generic.List<string>(); cats.Add(a[0]); }
                byCat[a[0]].Add(a[1]);
            }
            var cam = new GameObject("VidCam").AddComponent<Camera>();
            cam.enabled = false; cam.orthographic = true; cam.nearClipPlane = 0.1f; cam.farClipPlane = 60f;
            cam.cullingMask = 1 << PhotoLayer; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.3f, 0.32f, 0.36f);
            var lamp = new GameObject("VidLamp").AddComponent<Light>(); lamp.type = LightType.Directional; lamp.intensity = 0.9f; lamp.cullingMask = 1 << PhotoLayer;
            lamp.transform.rotation = Quaternion.Euler(35f, 160f, 0f);
            Vector3 origin = new Vector3(-200f, 0f, 260f);
            foreach (var cat in cats)
            {
                var names = byCat[cat];
                int cols = Mathf.Min(names.Count, 6), rows = (names.Count + 5) / 6;
                int W = Mathf.RoundToInt(cols * ColW * PxPerM / 2f) * 2, H = Mathf.RoundToInt(rows * RowH * PxPerM / 2f) * 2;
                var rt = new RenderTexture(W, H, 24); cam.targetTexture = rt;
                cam.orthographicSize = rows * RowH / 2f;
                Vector3 center = origin + new Vector3((cols - 1) * ColW / 2f, -(rows - 1) * RowH / 2f + 0.8f, 0f);
                cam.transform.position = center + new Vector3(0f, 0f, 20f);
                cam.transform.rotation = Quaternion.LookRotation(Vector3.back);
                string dir = Path.Combine(root, cat); Directory.CreateDirectory(dir);
                var bodies = new System.Collections.Generic.List<GameObject>();
                var layout = new System.Text.StringBuilder();
                float maxLen = 0f;
                for (int i = 0; i < names.Count; i++)
                {
                    var go = new GameObject("Vid_" + names[i]);
                    go.transform.position = origin + new Vector3((i % 6) * ColW, -(i / 6) * RowH, 0f);
                    var pb = ProtoBody.Create(go, CharLook.Get(Letter, false, Color.gray), Letter);
                    if (pb == null) { Destroy(go); continue; }
                    var clip = pb.TestFindClip(names[i]);
                    pb.testMode = true; pb.testPose = BodyPose.Stand; pb.testOutfit = "Uniform"; pb.testFace = ""; pb.testClip = names[i];
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.gameObject.layer = PhotoLayer;
                    bodies.Add(go);
                    if (clip != null) maxLen = Mathf.Max(maxLen, clip.length);
                    Vector3 sp = cam.WorldToViewportPoint(go.transform.position + Vector3.up * 1.85f);
                    layout.Append(names[i] + "|" + Mathf.RoundToInt(sp.x * W) + "|" + Mathf.RoundToInt((1f - sp.y) * H) + "|" + (clip != null ? clip.length.ToString("0.0") : "-") + "\n");
                }
                File.WriteAllText(Path.Combine(dir, "並び.txt"), W + "|" + H + "\n" + layout);
                for (int w = 0; w < 3; w++) yield return null;
                foreach (var go in bodies) { var pb = go.GetComponent<ProtoBody>(); if (pb != null) pb.TestSetClipTime(0f); }
                Time.captureFramerate = (int)Fps;
                int frames = Mathf.CeilToInt(Mathf.Min(Mathf.Max(maxLen, 2f), 8f) * Fps);
                var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
                for (int f = 0; f < frames; f++)
                {
                    yield return new WaitForEndOfFrame();
                    cam.Render();
                    var prev = RenderTexture.active; RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
                    RenderTexture.active = prev;
                    File.WriteAllBytes(Path.Combine(dir, "f_" + f.ToString("0000") + ".jpg"), tex.EncodeToJPG(85));
                }
                Time.captureFramerate = 0;
                Destroy(tex);
                foreach (var go in bodies) Destroy(go);
                cam.targetTexture = null; rt.Release();
                yield return null;
            }
            File.WriteAllText(Path.Combine(root, "済み.txt"), System.DateTime.Now.ToString());
            Destroy(cam.gameObject); Destroy(lamp.gameObject);
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 確認用（2026-10-30 モーションのチャット）：動きの使い分け（MotionBook）を動画で確かめる。
    /// Logs\\usagevideo_list.txt の1行（グループ|表示名|キャラの記号|姿勢|場面|身ぶり|速さ|気分）ごとに並べて同時に動かす。
    /// 保存先：仮モデル\\Unity確認\\使い分け_動画\\（グループ）\\。命令 usagevideo。
    /// </summary>
    public class UsageVideo : MonoBehaviour
    {
        const int PhotoLayer = 31;
        const float Fps = 20f, PxPerM = 120f, ColW = 1.5f, RowH = 2.2f;
        IEnumerator Start()
        {
            string root = Path.Combine(ProtoPhotoBooth.OutDir, "使い分け_動画");
            string listPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/usagevideo_list.txt"));
            var groups = new System.Collections.Generic.List<string>();
            var byG = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string[]>>();
            foreach (var raw in (File.Exists(listPath) ? File.ReadAllLines(listPath) : new string[0]))
            {
                var a = raw.Trim().Split('|'); if (a.Length < 8) continue;
                if (!byG.ContainsKey(a[0])) { byG[a[0]] = new System.Collections.Generic.List<string[]>(); groups.Add(a[0]); }
                byG[a[0]].Add(a);
            }
            var cam = new GameObject("UseCam").AddComponent<Camera>();
            cam.enabled = false; cam.orthographic = true; cam.nearClipPlane = 0.1f; cam.farClipPlane = 60f;
            cam.cullingMask = 1 << PhotoLayer; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.3f, 0.32f, 0.36f);
            var lamp = new GameObject("UseLamp").AddComponent<Light>(); lamp.type = LightType.Directional; lamp.intensity = 0.9f; lamp.cullingMask = 1 << PhotoLayer;
            lamp.transform.rotation = Quaternion.Euler(35f, 160f, 0f);
            Vector3 origin = new Vector3(-260f, 0f, 300f);
            foreach (var g in groups)
            {
                var items = byG[g];
                int cols = Mathf.Min(items.Count, 6), rows = (items.Count + 5) / 6;
                int W = Mathf.RoundToInt(cols * ColW * PxPerM / 2f) * 2, H = Mathf.RoundToInt(rows * RowH * PxPerM / 2f) * 2;
                var rt = new RenderTexture(W, H, 24); cam.targetTexture = rt;
                cam.orthographicSize = rows * RowH / 2f;
                Vector3 center = origin + new Vector3((cols - 1) * ColW / 2f, -(rows - 1) * RowH / 2f + 0.8f, 0f);
                cam.transform.position = center + new Vector3(0f, 0f, 20f);
                cam.transform.rotation = Quaternion.LookRotation(Vector3.back);
                string dir = Path.Combine(root, g); Directory.CreateDirectory(dir);
                var bodies = new System.Collections.Generic.List<ProtoBody>();
                var layout = new System.Text.StringBuilder();
                for (int i = 0; i < items.Count; i++)
                {
                    var a = items[i];
                    string letter = a[2];
                    var go = new GameObject("Use_" + a[1]);
                    go.transform.position = origin + new Vector3((i % 6) * ColW, -(i / 6) * RowH, 0f);
                    var pb = ProtoBody.Create(go, CharLook.Get(letter, false, Color.gray), letter);
                    if (pb == null) { Destroy(go); continue; }
                    pb.testMode = true; pb.testOutfit = "Uniform"; pb.testFace = ""; pb.testLetter = letter;
                    pb.testPose = (BodyPose)System.Enum.Parse(typeof(BodyPose), a[3]);
                    pb.testScene = a[4] == "-" ? null : a[4];
                    pb.testSpeed = float.Parse(a[6], System.Globalization.CultureInfo.InvariantCulture);
                    pb.testMood = a[7] == "-" ? null : a[7];
                    pb.testSitFloorClip = null;
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.gameObject.layer = PhotoLayer;
                    bodies.Add(pb);
                    Vector3 sp = cam.WorldToViewportPoint(go.transform.position + Vector3.up * 1.85f);
                    layout.Append(a[1] + "|" + Mathf.RoundToInt(sp.x * W) + "|" + Mathf.RoundToInt((1f - sp.y) * H) + "|" + a[5] + "\n");
                }
                File.WriteAllText(Path.Combine(dir, "並び.txt"), W + "|" + H + "\n" + layout);
                Time.captureFramerate = (int)Fps;
                for (int w = 0; w < 4; w++) yield return null;
                int frames = Mathf.RoundToInt(6f * Fps);
                var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
                for (int f = 0; f < frames; f++)
                {
                    if (f == 20)   // 1秒目に身ぶりを始める
                        for (int i = 0; i < bodies.Count && i < items.Count; i++)
                            if (items[i][5] != "-") bodies[i].Gesture(items[i][5], 3.5f);
                    yield return new WaitForEndOfFrame();
                    cam.Render();
                    var prev = RenderTexture.active; RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
                    RenderTexture.active = prev;
                    File.WriteAllBytes(Path.Combine(dir, "f_" + f.ToString("0000") + ".jpg"), tex.EncodeToJPG(85));
                }
                Time.captureFramerate = 0;
                Destroy(tex);
                foreach (var pb in bodies) if (pb != null) Destroy(pb.gameObject);
                cam.targetTexture = null; rt.Release();
                yield return null;
            }
            File.WriteAllText(Path.Combine(root, "済み.txt"), System.DateTime.Now.ToString());
            Destroy(cam.gameObject); Destroy(lamp.gameObject);
            Destroy(gameObject);
        }
    }
}
