using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LifeSimProto.EditorTools
{
    /// <summary>
    /// 確認用（2026-11-14 モーションのチャット）：ゲームの中の動きを数える。ゲームの動きは変えない。
    /// Logs\motion_check_cmd.txt に「start 秒数」と書くと、その秒数のあいだ、見えている全員について
    ///  ・歩いているとき：進む向き・体（transform）の向き・見た目の腰の向きのずれ
    ///  ・座っているとき：上半身の傾き（横・前後）
    ///  ・立って話しているとき：腰の高さ（低ければ座ったような形）
    /// を数え、Logs\motion_check.txt に書く。
    /// </summary>
    [InitializeOnLoad]
    public static class MotionCheck
    {
        static readonly string Logs = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs"));
        static double nextPoll, until = -1;
        static float lastT;
        class Acc
        {
            public string name;
            public Vector3 lastPos; public bool has;
            public int walkN, walkBad, walkBadBody; public float walkSum, bodySum;
            public Dictionary<string, int> walkBadClips = new Dictionary<string, int>();
            public Dictionary<string, float[]> sit = new Dictionary<string, float[]>();   // clip → [n, side, fb, maxSide, maxFb, thigh, knee, ankle]
            public int talkN, talkLow; public Dictionary<string, int> talkLowClips = new Dictionary<string, int>(); public float talkMinHip = 9f;
            public Dictionary<string, int> talkClips = new Dictionary<string, int>();
            public Quaternion[] kRef = new Quaternion[2]; public bool[] kHas = new bool[2];
            public Dictionary<string, float[]> knee = new Dictionary<string, float[]>();   // 姿勢:動き:左右 → [n, 曲がり, |ねじれ|の和, ねじれ最大, 軸のずれの和]
            public int lookN, lookOver; public float lookMax; public Dictionary<string, int> lookOverWhy = new Dictionary<string, int>();
        }
        static Dictionary<SimCharacter, Acc> acc;
        static string traceName; static int traceN; static StringBuilder trace; static int lastFrame = -1; static Vector3 tracePrev;
        static void Trace(LoungeWorld w)
        {
            if (trace == null || Time.frameCount == lastFrame) return; lastFrame = Time.frameCount;
            foreach (var c in w.characters)
            {
                if (c == null || c.displayName != traceName) continue;
                var an = c.GetComponentInChildren<Animator>();
                var lu = an.GetBoneTransform(HumanBodyBones.LeftUpperLeg); var ru = an.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                Vector3 r = ru.position - lu.position; r.y = 0; Vector3 vf = Vector3.Cross(r, Vector3.up).normalized;
                Vector3 p = c.transform.position; Vector3 mv = p - tracePrev; tracePrev = p;
                trace.AppendLine(Time.frameCount + " t" + Time.time.ToString("0.000") + " pos(" + p.x.ToString("0.00") + "," + p.z.ToString("0.00") + ") mv(" + mv.x.ToString("0.000") + "," + mv.z.ToString("0.000") + ") fwd(" + c.transform.forward.x.ToString("0.00") + "," + c.transform.forward.z.ToString("0.00") + ") vis(" + vf.x.ToString("0.00") + "," + vf.z.ToString("0.00") + ") " + c.StateName() + " " + ClipOf(c.GetComponent<ProtoBody>()));
            }
            if (--traceN <= 0) { File.AppendAllText(Path.Combine(Logs, "motion_trace.txt"), trace.ToString(), new UTF8Encoding(false)); trace = null; }
        }

        static MotionCheck() { EditorApplication.update += Tick; }

        static void Tick()
        {
            if (EditorApplication.timeSinceStartup >= nextPoll)
            {
                nextPoll = EditorApplication.timeSinceStartup + 0.5;
                string req = Path.Combine(Logs, "motion_check_cmd.txt");
                if (File.Exists(req))
                {
                    string c = File.ReadAllText(req).Trim(); File.Delete(req);
                    var a = c.Split(' ');
                    if (a[0] == "start" && Application.isPlaying)
                    {
                        float sec = a.Length > 1 ? float.Parse(a[1], System.Globalization.CultureInfo.InvariantCulture) : 30f;
                        acc = new Dictionary<SimCharacter, Acc>(); until = EditorApplication.timeSinceStartup + sec; lastT = Time.time;
                    }
                    else if (a[0] == "looklimit" && a.Length > 1) { ProtoBody.LookTotalMax = float.Parse(a[1], System.Globalization.CultureInfo.InvariantCulture); File.AppendAllText(Path.Combine(Logs, "motion_check.txt"), "looklimit " + ProtoBody.LookTotalMax + "\n", new UTF8Encoding(false)); }
                    else if (a[0] == "muscles" && a.Length > 1 && Application.isPlaying) Muscles(a[1]);
                    else if (a[0] == "clipmuscles" && a.Length > 1) ClipMuscles(a[1]);
                    else if (a[0] == "trace" && Application.isPlaying) { traceName = a[1]; traceN = a.Length > 2 ? int.Parse(a[2]) : 60; trace = new StringBuilder(); }
                    else File.WriteAllText(Path.Combine(Logs, "motion_check.txt"), "再生していないか、命令がちがう：" + c, new UTF8Encoding(false));
                }
            }
            if (!Application.isPlaying) return;
            var w = LoungeWorld.Instance; if (w == null) return;
            Trace(w);
            if (until < 0) return;
            float dt = Time.time - lastT; lastT = Time.time;
            if (dt > 0f) Sample(w, dt);
            if (EditorApplication.timeSinceStartup >= until) { until = -1; Write(); }
        }

        static string ClipOf(ProtoBody b)
        {
            if (b == null) return "?";
            var s = b.DebugClipNow(); var p = s.Split('｜'); if (p.Length < 2) return s;
            var q = p[1]; int i = q.IndexOf(' '); return i > 0 ? q.Substring(0, i) : q;
        }

        /// <summary>その子の脚の筋肉（人型の動かし方）の値と、体の型の範囲（Logs\motion_check.txt）</summary>
        static void Muscles(string letter)
        {
            var sb = new StringBuilder("== 筋肉 " + letter + " " + DateTime.Now.ToString("HH:mm:ss") + "\n");
            var w = LoungeWorld.Instance;
            foreach (var c in w.characters)
            {
                if (c == null || c.letter != letter.ToUpperInvariant()) continue;
                foreach (var an in c.GetComponentsInChildren<Animator>(true))
                {
                    if (an.avatar == null || !an.avatar.isHuman) continue;
                    var h = new HumanPoseHandler(an.avatar, an.transform); var hp = new HumanPose(); h.GetHumanPose(ref hp);
                    sb.AppendLine(an.name + "（" + an.avatar.name + "）姿勢 " + c.BodyPoseNow);
                    for (int i = 0; i < HumanTrait.MuscleCount; i++)
                    {
                        string n = HumanTrait.MuscleName[i];
                        if (!n.Contains("Leg") && !n.Contains("Foot")) continue;
                        sb.AppendLine("  " + n + " = " + hp.muscles[i].ToString("0.000") + "（範囲 " + HumanTrait.GetMuscleDefaultMin(i) + "〜" + HumanTrait.GetMuscleDefaultMax(i) + "）");
                    }
                    var hd = an.avatar.humanDescription;
                    foreach (var hb in hd.human) if (hb.humanName.Contains("LowerLeg") || hb.humanName.Contains("UpperLeg")) sb.AppendLine("  " + hb.humanName + " 範囲 既定=" + hb.limit.useDefaultValues + " min" + hb.limit.min + " max" + hb.limit.max);
                }
            }
            File.AppendAllText(Path.Combine(Logs, "motion_check.txt"), sb.ToString(), new UTF8Encoding(false));
        }

        /// <summary>動き（FBX の中の人型の動き）の脚の筋肉の値（始め・真ん中・終わり）</summary>
        static void ClipMuscles(string name)
        {
            var sb = new StringBuilder("== 動きの筋肉 " + name + " " + DateTime.Now.ToString("HH:mm:ss") + "\n");
            foreach (var g in AssetDatabase.FindAssets(name + " t:AnimationClip"))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(p))
                {
                    var clip = o as AnimationClip; if (clip == null || clip.name != name) continue;
                    sb.AppendLine(p + "（長さ " + clip.length.ToString("0.00") + "）");
                    foreach (var b in AnimationUtility.GetCurveBindings(clip))
                    {
                        if (!(b.propertyName.Contains("Leg") || b.propertyName.Contains("Foot"))) continue;
                        var cv = AnimationUtility.GetEditorCurve(clip, b);
                        sb.AppendLine("  " + b.propertyName + "：" + cv.Evaluate(0f).ToString("0.000") + " ／ " + cv.Evaluate(clip.length * 0.5f).ToString("0.000") + " ／ " + cv.Evaluate(clip.length).ToString("0.000"));
                    }
                }
            }
            File.AppendAllText(Path.Combine(Logs, "motion_check.txt"), sb.ToString(), new UTF8Encoding(false));
        }

        static void Sample(LoungeWorld w, float dt)
        {
            foreach (var c in w.characters)
            {
                if (c == null || !c.Present) continue;
                Acc A; if (!acc.TryGetValue(c, out A)) { A = new Acc { name = c.displayName }; acc[c] = A; }
                Vector3 pos = c.transform.position;
                Vector3 mv = pos - A.lastPos; mv.y = 0f; bool had = A.has; A.lastPos = pos; A.has = true;
                var an = c.GetComponentInChildren<Animator>();
                if (an == null || an.avatar == null || !an.avatar.isHuman) continue;
                var hips = an.GetBoneTransform(HumanBodyBones.Hips); var lu = an.GetBoneTransform(HumanBodyBones.LeftUpperLeg); var ru = an.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                var head = an.GetBoneTransform(HumanBodyBones.Head);
                if (hips == null || lu == null || ru == null || head == null) continue;
                Vector3 right = ru.position - lu.position; right.y = 0f;
                Vector3 vf = Vector3.Cross(right, Vector3.up).normalized;   // 見た目の腰の正面
                Vector3 tf = c.transform.forward; tf.y = 0f; tf.Normalize();
                var pb = c.GetComponent<ProtoBody>(); string clip = ClipOf(pb);
                var pose = c.BodyPoseNow;
                // 歩いている
                if (had && pose == BodyPose.Stand && mv.magnitude / dt > 0.6f && mv.magnitude < 0.5f)
                {
                    Vector3 md = mv.normalized;
                    float a1 = Vector3.Angle(md, vf), a2 = Vector3.Angle(tf, vf);
                    A.walkN++; A.walkSum += a1; A.bodySum += Vector3.Angle(md, tf);
                    if (a1 > 35f) { A.walkBad++; int n; A.walkBadClips.TryGetValue(clip, out n); A.walkBadClips[clip] = n + 1; }
                    if (Vector3.Angle(md, tf) > 35f) A.walkBadBody++;
                }
                // ひざ：立ってひざがのびているときの「太ももから見たすね」を基準に、座ったときの回り方（曲がり・ねじれ・軸）を測る（2026-11-23 モーション）
                for (int sd = 0; sd < 2; sd++)
                {
                    var th = an.GetBoneTransform(sd == 0 ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg);
                    var sh = an.GetBoneTransform(sd == 0 ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg);
                    var fo = an.GetBoneTransform(sd == 0 ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
                    if (th == null || sh == null || fo == null) continue;
                    Quaternion rel = Quaternion.Inverse(th.rotation) * sh.rotation;
                    float bendDir = Vector3.Angle(sh.position - th.position, fo.position - sh.position);
                    if (pose == BodyPose.Stand && bendDir < 12f && (!had || mv.magnitude / dt < 0.05f)) { A.kRef[sd] = rel; A.kHas[sd] = true; }
                    if ((pose == BodyPose.SitFloor || pose == BodyPose.Sit) && A.kHas[sd])
                    {
                        Quaternion d = Quaternion.Inverse(A.kRef[sd]) * rel;
                        if (d.w < 0f) { d.x = -d.x; d.y = -d.y; d.z = -d.z; d.w = -d.w; }
                        Vector3 axLoc = (Quaternion.Inverse(sh.rotation) * (fo.position - sh.position)).normalized;   // すねの長さの向き（すねの骨の中）
                        Vector3 v = new Vector3(d.x, d.y, d.z);
                        float tw = 2f * Mathf.Atan2(Vector3.Dot(v, axLoc), d.w) * Mathf.Rad2Deg;
                        if (tw > 180f) tw -= 360f; if (tw < -180f) tw += 360f;
                        // 軸のずれ：回る軸と「太ももの骨の横の軸（基準の姿勢で曲げたときの軸）」のなす角 ＝ ねじれ以外の回り方の軸と、すねの長さに直角な面の中での、ひざの横の軸とのずれ
                        float ang; Vector3 ax; d.ToAngleAxis(out ang, out ax);
                        Vector3 sideLoc = Vector3.Cross(axLoc, Quaternion.Inverse(sh.rotation) * (sh.position - th.position)).normalized;
                        float axErr = sideLoc.sqrMagnitude > 0.5f ? Mathf.Min(Vector3.Angle(ax, sideLoc), Vector3.Angle(ax, -sideLoc)) : 0f;
                        string kk = pose + ":" + clip + ":" + (sd == 0 ? "左" : "右");
                        float[] s2; if (!A.knee.TryGetValue(kk, out s2)) { s2 = new float[5]; A.knee[kk] = s2; }
                        s2[0]++; s2[1] += bendDir; s2[2] += Mathf.Abs(tw); if (Mathf.Abs(tw) > Mathf.Abs(s2[3])) s2[3] = tw; s2[4] += axErr;
                    }
                }
                Vector3 up = head.position - hips.position;
                if (pose == BodyPose.Sit || pose == BodyPose.SitFloor)
                {
                    float side = Mathf.Atan2(Vector3.Dot(up, right.normalized), up.y) * Mathf.Rad2Deg;
                    float fb = Mathf.Atan2(Vector3.Dot(up, vf), up.y) * Mathf.Rad2Deg;   // ＋＝前かがみ
                    string k = pose + ":" + clip; float[] s; if (!A.sit.TryGetValue(k, out s)) { s = new float[8]; A.sit[k] = s; }
                    var kn = an.GetBoneTransform(HumanBodyBones.LeftLowerLeg); var ft = an.GetBoneTransform(HumanBodyBones.LeftFoot);
                    if (kn != null && ft != null) { Vector3 th = kn.position - lu.position, shn = ft.position - kn.position; s[5] += Mathf.Atan2(-th.y, new Vector2(th.x, th.z).magnitude) * Mathf.Rad2Deg; s[6] += Vector3.Angle(th, shn); s[7] += ft.position.y - pos.y; }
                    s[0]++; s[1] += side; s[2] += fb; if (Mathf.Abs(side) > Mathf.Abs(s[3])) s[3] = side; if (Mathf.Abs(fb) > Mathf.Abs(s[4])) s[4] = fb;
                }
                if (pb != null && pb.LookWhy != null)
                {
                    float ly = pb.LookHeadYaw; A.lookN++; if (Mathf.Abs(ly) > Mathf.Abs(A.lookMax)) A.lookMax = ly;
                    if (Mathf.Abs(ly) > 72f) { A.lookOver++; string kk = pose + ":" + clip; int n2; A.lookOverWhy.TryGetValue(kk, out n2); A.lookOverWhy[kk] = n2 + 1; }
                }
                if (pose == BodyPose.Stand && c.IsTalkingNow)
                {
                    float hh = hips.position.y - pos.y;
                    A.talkN++; if (hh < A.talkMinHip) A.talkMinHip = hh;
                    int n; A.talkClips.TryGetValue(clip, out n); A.talkClips[clip] = n + 1;
                    if (hh < 0.65f) { A.talkLow++; A.talkLowClips.TryGetValue(clip + "(" + c.StateName() + ")", out n); A.talkLowClips[clip + "(" + c.StateName() + ")"] = n + 1; }
                }
            }
        }

        static string Top(Dictionary<string, int> d, int k)
        {
            var l = new List<KeyValuePair<string, int>>(d); l.Sort((x, y) => y.Value.CompareTo(x.Value));
            var sb = new StringBuilder(); for (int i = 0; i < l.Count && i < k; i++) sb.Append(l[i].Key + "×" + l[i].Value + " ");
            return sb.ToString();
        }

        static void Write()
        {
            var sb = new StringBuilder();
            sb.AppendLine("== " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " 動きの確認（MotionCheck）");
            sb.AppendLine("【歩く】見た目の腰の正面と進む向きのずれ（平均・35度より大きいコマの割合）／体（transform）の向きと進む向きのずれ");
            foreach (var A in acc.Values) if (A.walkN > 5)
                    sb.AppendLine(A.name + "｜コマ " + A.walkN + "｜腰 平均" + (A.walkSum / A.walkN).ToString("0") + "度・ずれ " + (100f * A.walkBad / A.walkN).ToString("0") + "%｜体 平均" + (A.bodySum / A.walkN).ToString("0") + "度・ずれ " + (100f * A.walkBadBody / A.walkN).ToString("0") + "%｜" + Top(A.walkBadClips, 3));
            sb.AppendLine("【座る】上半身（腰→頭）の傾き 横（＋＝右）・前後（＋＝前かがみ）の平均と最大");
            foreach (var A in acc.Values) foreach (var kv in A.sit)
                {
                    var s = kv.Value; if (s[0] < 5) continue;
                    sb.AppendLine(A.name + "｜" + kv.Key + "｜コマ " + s[0] + "｜横 " + (s[1] / s[0]).ToString("0") + "度（最大 " + s[3].ToString("0") + "）・前後 " + (s[2] / s[0]).ToString("0") + "度（最大 " + s[4].ToString("0") + "）｜もも 下向き " + (s[5] / s[0]).ToString("0") + "度・ひざ " + (s[6] / s[0]).ToString("0") + "度・足首の高さ " + (s[7] / s[0]).ToString("0.00") + "m");
                }
            sb.AppendLine("【ひざ】立った姿勢を基準にした、すねの回り方：曲がり（太もも→すねの向きのなす角）・ねじれ（すねの長さのまわり）・軸のずれ");
            foreach (var A in acc.Values) foreach (var kv in A.knee)
                {
                    var s2 = kv.Value; if (s2[0] < 5) continue;
                    sb.AppendLine(A.name + "｜" + kv.Key + "｜コマ " + s2[0] + "｜曲がり " + (s2[1] / s2[0]).ToString("0") + "度・ねじれ 平均 " + (s2[2] / s2[0]).ToString("0") + "度（最大 " + s2[3].ToString("0") + "）・軸のずれ " + (s2[4] / s2[0]).ToString("0") + "度");
                }
            sb.AppendLine("【立って話す】腰の高さ（床から）の最低と、0.65m より低いコマ");
            foreach (var A in acc.Values) if (A.talkN > 0)
                    sb.AppendLine(A.name + "｜コマ " + A.talkN + "｜最低 " + A.talkMinHip.ToString("0.00") + "m｜低い " + A.talkLow + "｜" + Top(A.talkLowClips, 3) + "｜動き " + Top(A.talkClips, 3));
            sb.AppendLine("【見る】頭の左右の向き（胸の正面から）の最大と、72度より大きいコマ（上限 " + ProtoBody.LookTotalMax + "）");
            int tn = 0, to = 0; float tm = 0f;
            foreach (var A in acc.Values) if (A.lookN > 0)
                {
                    tn += A.lookN; to += A.lookOver; if (Mathf.Abs(A.lookMax) > Mathf.Abs(tm)) tm = A.lookMax;
                    if (A.lookOver > 0 || Mathf.Abs(A.lookMax) > 60f) sb.AppendLine(A.name + "｜コマ " + A.lookN + "｜最大 " + A.lookMax.ToString("0") + "度｜72度より大きい " + A.lookOver + "｜" + Top(A.lookOverWhy, 3));
                }
            sb.AppendLine("全員｜見ているコマ " + tn + "｜最大 " + tm.ToString("0") + "度｜72度より大きい " + to);
            File.AppendAllText(Path.Combine(Logs, "motion_check.txt"), sb.ToString(), new UTF8Encoding(false));
        }
    }
}
