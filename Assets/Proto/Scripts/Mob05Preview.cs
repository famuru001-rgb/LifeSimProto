#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace LifeSimProto
{
    [DefaultExecutionOrder(21000)]
    public class Mob05Preview : MonoBehaviour
    {
        public static string Output;
        public static bool Capture;
        readonly List<ProtoBody> bodies = new List<ProtoBody>();
        readonly StringBuilder samples = new StringBuilder("phase,frame,bust,hair,clip,chest_pitch,chest_stretch,hair_swing\n");
        Camera cam; Light sun; RenderTexture rt;
        int previousRate, previousVsync; float previousCapture;
        float turn; string phase = "Idle"; int frame; bool finished;
        string[] clips = { "Idle", "Walk", "BN_walk_feminine", "Sit_Normal_Down", "Sit_Normal", "Sit_Normal_Up" };
        IEnumerator Start()
        {
            try { Setup(); } catch (Exception e) { Fail(e); yield break; }
            if (!Capture) yield break;
            yield return Run();
        }
        void Setup()
        {
            if (LoungeWorld.Instance != null || FindObjectsByType<SimCharacter>(FindObjectsSortMode.None).Length > 0)
                throw new Exception("Preview requires empty scene: no residents or saves");
            // Automatic photo/hotkey/environment watchers must not execute commands in this scene.
            foreach (var b in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)) if (b != this) b.enabled = false;
            Directory.CreateDirectory(Output);
            previousRate = Application.targetFrameRate; previousVsync = QualitySettings.vSyncCount; previousCapture = Time.captureDeltaTime;
            Application.logMessageReceived += LogError;
            Application.runInBackground = true;
            Time.timeScale = 1; if (Capture) Time.captureDeltaTime = 1f / 60;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.5f, .47f, .42f); RenderSettings.skybox = null;
            sun = new GameObject("MOB05 Game Day Sun").AddComponent<Light>(); sun.type = LightType.Directional;
            sun.color = new Color(1, .95f, .85f); sun.intensity = 1.1f; sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50, -35, 0);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); floor.layer = 31; floor.transform.localScale = Vector3.one * 15;
            var fm = new Material(Shader.Find("Universal Render Pipeline/Lit")); fm.SetColor("_BaseColor", new Color(.36f, .37f, .40f)); floor.GetComponent<Renderer>().material = fm;
            cam = new GameObject("MOB05 Camera").AddComponent<Camera>(); cam.tag = "MainCamera";
            cam.nearClipPlane = .03f; cam.farClipPlane = 200; cam.fieldOfView = 30;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.30f, .32f, .36f);
            rt = new RenderTexture(1280, 720, 24); cam.targetTexture = Capture ? rt : null;
            for (int b = 1; b <= 3; b++) for (int h = 0; h < 3; h++)
            {
                var owner = new GameObject("Bust" + b + "_" + h); owner.transform.position = new Vector3((h - 1) * 1.6f, 0, -(b - 1) * 2.0f);
                var pb = ProtoBody.CreateMob05(owner, b, h, 162.9f, "mobA01");
                foreach (var r in owner.GetComponentsInChildren<Renderer>(true)) r.gameObject.layer = 10 + bodies.Count;
                if (owner.GetComponentsInChildren<SkinnedMeshRenderer>().Length != 3) throw new Exception("Variant must contain exactly 3 visible meshes");
                bodies.Add(pb);
                if (Capture) pb.enabled = false;
            }
            Overview();
            var profiles = Mob05Variants.Profiles;
            if (profiles.Length != 97) throw new Exception("Expected 97 profiles");
            var csv = new StringBuilder("symbol,bust,hair,height_cm\n");
            foreach (var p in profiles)
            {
                int b = Mob05Variants.Bust(p.letter), h = Mob05Variants.Hair(p);
                if (b != Mob05Variants.Bust(p.letter) || h != Mob05Variants.Hair(p)) throw new Exception("Non-deterministic mapping");
                csv.AppendLine(p.letter + "," + b + "," + new[] { "Short", "Semi", "Pony" }[h] + "," + p.heightCm.ToString("F4"));
            }
            File.WriteAllText(Path.Combine(Output, "variants97.csv"), csv.ToString());
            File.WriteAllText(Path.Combine(Output, "conditions.txt"), "Unity " + Application.unityVersion + "\nGPU " + SystemInfo.graphicsDeviceName + "\nActual game ProtoBody.Setup/LateUpdate/Toon/Springs. Isolated Play-mode scene; no LoungeWorld/SimCharacter.\nDay lighting: rotation(50,-35,0), color(1,.95,.85), intensity1.1, ambient(.5,.47,.42).\nHeight matches actual head surface (hair excluded).\nHead stress: direct Head rotation +/-45; game normally divides neck/head 40/60.\nHead material uses same game skin settings as body. All chest springs use current game defaults. No asset mesh/weight edits.\n" + string.Join("\n", profiles.GroupBy(p => Mob05Variants.Bust(p.letter)).Select(g => "Bust" + g.Key + "=" + g.Count())) + "\nSave folder: " + SaveSystem.Folder);
            foreach (string c in clips) if (ProtoBody.FindClip(c) == null) throw new Exception("Required motion missing: " + c);
        }
        IEnumerator Run()
        {
            yield return new WaitForEndOfFrame();
            foreach (int i in Enumerable.Range(0, 9)) foreach (string v in new[] { "front", "side", "back", "diagonal" }) Shot(i, v, false, "Tpose");
            foreach (int i in new[] { 0, 3, 6 }) foreach (string v in new[] { "front", "side", "diagonal" }) Shot(i, v, true, "Tpose_detail");
            File.WriteAllText(Path.Combine(Output,"shaders.txt"), string.Join("\n", bodies[0].GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Select(m=>m.name+" shader="+m.shader.name+" softness="+m.GetFloat("_ShadeSoftness"))));
            foreach (var pb in bodies) pb.enabled = true;
            foreach (string clip in clips.Concat(new[] { "HeadLeft45", "HeadRight45" }))
            {
                phase = clip; frame = 0; turn = clip == "HeadLeft45" ? -45 : clip == "HeadRight45" ? 45 : 0;
                string actual = clip.StartsWith("Head") ? "Idle" : clip;
                foreach (var pb in bodies) { pb.testClip = actual; pb.testSpeed = actual.Contains("alk") ? 1.2f : 0; }
                yield return null;
                foreach (var pb in bodies) pb.TestSetClipTime(0);
                float length = ProtoBody.FindClip(actual).length;
                int count = actual.EndsWith("_Down") || actual.EndsWith("_Up") ? Mathf.CeilToInt(length * 60) : 120;
                for (int f = 0; f < count; f++)
                {
                    yield return new WaitForEndOfFrame();
                    try
                    {
                        frame = f;
                        if (f % 10 == 0) for (int i = 0; i < 9; i++) samples.AppendLine(phase + "," + f + "," + (i / 3 + 1) + "," + (i % 3) + ",\"" + bodies[i].DebugClipNow() + "\"," + bodies[i].SpringDebug);
                        if (f == count - 1)
                        {
                            int[] ids = phase == "Idle" ? Enumerable.Range(0, 9).ToArray() : new[] { 0, 4, 8 };
                            foreach (int i in ids) foreach (string view in new[] { "front", "side", "back" }) Shot(i, view, false, phase + "_end");
                            if (phase == "Idle" || phase.StartsWith("Head")) foreach (int i in new[] { 0, 4, 8 }) foreach (string view in new[] { "front", "side", "back" }) Shot(i, view, true, phase + "_neck");
                        }
                        if (f % 20 == 10 && phase != "Idle") foreach (int i in new[] { 0, 4, 8 }) Shot(i, "diagonal", false, phase + "_" + f.ToString("D3"));
                        Overview();
                    }
                    catch (Exception e) { Fail(e); yield break; }
                }
            }
            turn = 0;
            try { MakeCrowd(); } catch (Exception e) { Fail(e); yield break; }
            Time.captureDeltaTime = 0;
            foreach (var pb in bodies) pb.testClip = "Idle";
            cam.cullingMask = sun.cullingMask = (1<<10)|(1<<31);
            cam.transform.position = new Vector3(0,1.8f,12); cam.transform.LookAt(new Vector3(0,.8f,0));
            yield return new WaitForSecondsRealtime(2);
            SaveCamera("lineup10.png");
            foreach (var pb in bodies) { pb.testClip = "BN_walk_feminine"; pb.testSpeed = 1.2f; }
            cam.cullingMask = sun.cullingMask = ~0;
            cam.transform.position = new Vector3(0,7,22); cam.transform.LookAt(new Vector3(0,.8f,-7));
            yield return new WaitForSecondsRealtime(3);
            // Actual perf probe used by the game command. No artificial fixed timestep here.
            if (bodies.Count != 97 || bodies.Any(b => !b.enabled || !b.DebugClipNow().Contains("BN_walk_feminine"))) { Fail(new Exception("97 animated bodies required for perf")); yield break; }
            File.WriteAllText(Path.Combine(Output,"perf_animation_check.txt"),"97 enabled ProtoBody; all BN_walk_feminine verified before measurement");
            PerfProbe.Begin("MOB05_97_preview_1280x720", 10);
            yield return new WaitForSecondsRealtime(12);
            try
            {
                SaveCamera("crowd97.png");
                File.WriteAllText(Path.Combine(Output, "motion_samples.csv"), samples.ToString());
                string perf = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/perf.txt"));
                File.WriteAllText(Path.Combine(Output, "perf_full.txt"), File.ReadAllText(perf));
                File.WriteAllText(Path.Combine(Output, "DONE.txt"), DateTime.Now.ToString("O"));
                finished = true; UnityEditor.EditorApplication.isPlaying = false;
            }
            catch (Exception e) { Fail(e); }
        }
        void LateUpdate()
        {
            if (finished) return;
            if (turn != 0) foreach (var pb in bodies) if (pb != null) pb.Mob05TurnHead(turn);
        }
        void Overview()
        {
            cam.cullingMask = sun.cullingMask = ~0;
            cam.transform.position = new Vector3(0, 3.5f, 15); cam.transform.LookAt(new Vector3(0, .8f, -2));
        }
        void Shot(int id, string view, bool detail, string label)
        {
            float yaw = view == "side" ? 90 : view == "back" ? 180 : view == "diagonal" ? 45 : 0;
            var at = bodies[id].transform.position + Vector3.up * (detail ? 1.36f : .83f);
            cam.cullingMask = sun.cullingMask = (1 << (10 + id)) | (1 << 31);
            cam.transform.position = at + Quaternion.Euler(0, yaw, 0) * Vector3.forward * (detail ? .85f : 3.8f) + Vector3.up * (detail ? .02f : .15f);
            cam.transform.LookAt(at);
            SaveCamera("B" + (id / 3 + 1) + "H" + (id % 3 + 1) + "_" + label + "_" + view + ".png");
        }
        void SaveCamera(string name)
        {
            cam.Render(); var prev = RenderTexture.active; RenderTexture.active = rt;
            var im = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            im.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); im.Apply(); RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(Output, name), im.EncodeToPNG()); Destroy(im);
        }
        void MakeCrowd()
        {
            foreach (var pb in bodies) Destroy(pb.gameObject); bodies.Clear();
            var profiles = Mob05Variants.Profiles;
            var heights = new StringBuilder("symbol,target_cm,measured_cm\n");
            for (int i = 0; i < profiles.Length; i++)
            {
                var p = profiles[i]; var go = new GameObject(p.letter + " Preview");
                go.transform.position = new Vector3((i % 10 - 4.5f) * 1.15f, 0, -(i / 10) * 1.7f);
                var pb = ProtoBody.CreateMob05(go, Mob05Variants.Bust(p.letter), Mob05Variants.Hair(p), p.heightCm, p.letter);
                foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.gameObject.layer = i < 10 ? 10 : 11;
                pb.testClip = "BN_walk_feminine"; pb.testSpeed = 1.2f; bodies.Add(pb);
                heights.AppendLine(p.letter+","+p.heightCm.ToString("F5")+","+pb.Mob05HeightAtCreation.ToString("F5"));
            }
            cam.cullingMask = sun.cullingMask = ~0;
            cam.transform.position = new Vector3(0, 7, 22); cam.transform.LookAt(new Vector3(0, .8f, -7));
            File.WriteAllText(Path.Combine(Output,"height97.csv"),heights.ToString());
            Application.targetFrameRate = -1; QualitySettings.vSyncCount = 0;
        }
        void OnGUI()
        {
            if (Capture || cam == null) return;
            GUILayout.BeginArea(new Rect(10, 10, 500, 150), GUI.skin.box);
            GUILayout.Label("MOB05 確認専用（通常の住人・セーブは変更しません）");
            GUILayout.BeginHorizontal();
            for (int i = 0; i < 9; i++) if (GUILayout.Button("胸" + (i / 3 + 1) + " 髪" + (i % 3 + 1)))
            { cam.cullingMask = sun.cullingMask = (1 << (10 + i)) | (1 << 31); var at = bodies[i].transform.position + Vector3.up * .83f; cam.transform.position = at + Vector3.forward * 3.8f; cam.transform.LookAt(at); }
            GUILayout.EndHorizontal(); GUILayout.BeginHorizontal();
            foreach (var c in clips) if (GUILayout.Button(c)) { foreach (var pb in bodies) pb.testClip = c; turn = 0; }
            GUILayout.EndHorizontal();
            if (GUILayout.Button("全9通り")) Overview();
            GUILayout.EndArea();
        }
        void LogError(string message,string stack,LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                File.AppendAllText(Path.Combine(Output,"runtime_errors.txt"), type+": "+message+"\n"+stack+"\n");
        }
        void OnDestroy()
        {
            Application.logMessageReceived -= LogError;
            Application.targetFrameRate = previousRate; QualitySettings.vSyncCount = previousVsync; Time.captureDeltaTime = previousCapture;
            if (rt != null) { rt.Release(); Destroy(rt); }
        }
        void Fail(Exception e)
        {
            finished = true; Directory.CreateDirectory(Output); File.WriteAllText(Path.Combine(Output, "ERROR.txt"), e.ToString());
            Debug.LogException(e); Time.captureDeltaTime = 0; UnityEditor.EditorApplication.isPlaying = false;
        }
    }
}
#endif
