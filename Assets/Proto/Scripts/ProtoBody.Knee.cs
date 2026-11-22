using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// ひざの形のキー（2026-11-22 本体 S08。famuru 決定 11-22 22:05）。
    /// ・キャラを作るときに Resources/Chars/（記号）/Char（記号）_knee（角度表 json）があれば読み、そのキャラのすべてのメッシュ（体・服・靴下）から
    ///   表のキーと同じ名前のシェイプキーを集める。表が無いキャラは何もしない（今までどおり）
    /// ・毎コマ（LateUpdate の動きと足の合わせのあと、揺れの前）、左右のひざの角度
    ///   ＝ acos(dot(normalize(ひざ−股), normalize(足首−ひざ)))（まっすぐ 0°、正座で約 154〜170°）を測り、
    ///   キーごとに knots を直線でつないだ強さ → weightMin〜weightMax → ×100 を入れる。表のキーは 0 も含めて全部、足し算で重ねる
    /// ・前のコマと同じ値なら入れない（軽くするため）。骨の長さが 0 の側は全部 0
    /// ・確かめ：命令 knee on／off（止めると全部 0）・knee list・kneeshots（KneeShots.cs）
    /// ・安全策（2026-11-23 S08 追記1。famuru 決定 11-22 案A）：すねの骨がひざでねじれている・曲がる軸がずれているとき（床座りの動き）は、
    ///   ひざのキーを弱める。休みの姿勢からの「太ももの骨の中で見たすねの骨の回り」を、すねの向きのまわりの「ねじれ」と「振り」に分け、
    ///   係数 ＝（ねじれ KneeTwistFull° まで 1 → KneeTwistZero° で 0）×（軸のずれ KneeAxisFull° まで 1 → KneeAxisZero° で 0。曲がり 20° 未満は見ない）
    /// </summary>
    public partial class ProtoBody
    {
        /// <summary>false：ひざのキーを動かさない（全部 0）。確かめと重さの比べ用</summary>
        public static bool KneeOn = true;
        /// <summary>確かめ用：記号 → 角度表の文字（ファイルを置かずに試す。knee test 記号 ファイル）</summary>
        public static readonly Dictionary<string, string> KneeJsonOverride = new Dictionary<string, string>();
        /// <summary>ねじれ・軸のずれで弱める（S08 追記1）。数字は目安（度）</summary>
        public static bool KneeTwistGuard = true;
        public static float KneeTwistFull = 20f, KneeTwistZero = 40f, KneeAxisFull = 20f, KneeAxisZero = 40f, KneeAxisMinBend = 20f;   // 依頼の目安は 10→25・15→30。ゲームの立つ・歩く・椅子でねじれ 18°・軸のずれ 15° まで出たので広げた（正座はねじれ約 90°）

        class KneeKey
        {
            public string name; public int side;   // 0＝L（左）・1＝R（右）
            public float min, max;
            public float[] ang, w;
            public readonly List<KeyValuePair<SkinnedMeshRenderer, int>> targets = new List<KeyValuePair<SkinnedMeshRenderer, int>>();
            public float last = -1f;
            public float Eval(float a)
            {
                float v;
                int n = ang.Length;
                if (n == 0) v = 0f;
                else if (a <= ang[0]) v = w[0];
                else if (a >= ang[n - 1]) v = w[n - 1];
                else
                {
                    v = w[n - 1];
                    for (int i = 0; i < n - 1; i++)
                        if (a <= ang[i + 1]) { float t = (ang[i + 1] - ang[i]) > 1e-6f ? (a - ang[i]) / (ang[i + 1] - ang[i]) : 1f; v = Mathf.Lerp(w[i], w[i + 1], t); break; }
                }
                return Mathf.Clamp(v, min, max) * 100f;
            }
        }

        List<KneeKey> kneeKeys;
        Transform[] kneeBonesL, kneeBonesR;
        bool kneeWasOn = true;
        Quaternion kneeRestL = Quaternion.identity, kneeRestR = Quaternion.identity;   // 休みの姿勢の「太ももの中のすねの向き」
        Vector3 kneeShinAxisL = Vector3.down, kneeShinAxisR = Vector3.down;              // 休みの姿勢のすねの骨の向き（太ももの骨の座標）
        public float KneeTwistL, KneeTwistR, KneeAxisDevL, KneeAxisDevR, KneeFacL = 1f, KneeFacR = 1f;
        public Vector3 KneeSwingAxisL, KneeSwingAxisR;
        public float KneeAngleL { get; private set; }
        public float KneeAngleR { get; private set; }
        public int KneeKeyCount { get { return kneeKeys == null ? 0 : kneeKeys.Count; } }
        public int KneeTargetCount { get { int c = 0; if (kneeKeys != null) foreach (var k in kneeKeys) c += k.targets.Count; return c; } }
        public string KneeInfo;   // 読み込みの結果（確かめ用）

        /// <summary>キャラを作るときに呼ぶ（Create から）。表が無ければ何もしない。</summary>
        void SetupKnee(string letter)
        {
            kneeKeys = null;
            if (string.IsNullOrEmpty(letter) || model == null) return;
            string text;
            if (!KneeJsonOverride.TryGetValue(letter, out text))
            {
                var ta = Resources.Load<TextAsset>("Chars/" + letter + "/Char" + letter + "_knee");
                if (ta == null) return;
                text = ta.text;
            }
            object root;
            try { root = MiniJson.Parse(text); }
            catch (System.Exception e) { KneeInfo = "角度表が読めない：" + e.Message; Debug.LogWarning("[knee] " + letter + " " + KneeInfo); return; }
            var o = root as Dictionary<string, object>;
            var keys = o != null && o.ContainsKey("keys") ? o["keys"] as Dictionary<string, object> : null;
            if (keys == null) { KneeInfo = "角度表に keys が無い"; return; }
            kneeBonesL = KneeBones(o, "leftBones", "Left");
            kneeBonesR = KneeBones(o, "rightBones", "Right");
            KneeRest(kneeBonesL, out kneeRestL, out kneeShinAxisL);
            KneeRest(kneeBonesR, out kneeRestR, out kneeShinAxisR);
            var smrs = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var list = new List<KneeKey>();
            int missing = 0;
            foreach (var kv in keys)
            {
                var d = kv.Value as Dictionary<string, object>;
                if (d == null) continue;
                var k = new KneeKey { name = kv.Key };
                k.side = (d.ContainsKey("side") && (d["side"] as string) == "R") ? 1 : 0;
                k.min = d.ContainsKey("weightMin") ? Num(d["weightMin"]) : 0f;
                k.max = d.ContainsKey("weightMax") ? Num(d["weightMax"]) : 1f;
                var kn = d.ContainsKey("knots") ? d["knots"] as List<object> : null;
                var la = new List<float>(); var lw = new List<float>();
                if (kn != null)
                    foreach (var p in kn)
                    {
                        var pl = p as List<object>;
                        if (pl == null || pl.Count < 2) continue;
                        la.Add(Num(pl[0])); lw.Add(Num(pl[1]));
                    }
                k.ang = la.ToArray(); k.w = lw.ToArray();
                foreach (var r in smrs)
                {
                    if (r.sharedMesh == null) continue;
                    int idx = r.sharedMesh.GetBlendShapeIndex(kv.Key);
                    if (idx >= 0) k.targets.Add(new KeyValuePair<SkinnedMeshRenderer, int>(r, idx));
                }
                if (k.targets.Count == 0) { missing++; continue; }   // 無い名前は飛ばす
                list.Add(k);
            }
            kneeKeys = list.Count > 0 ? list : null;
            KneeInfo = "表のキー " + keys.Count + "・見つかったキー " + list.Count + "（メッシュの数 " + KneeTargetCount + "）・無いキー " + missing;
        }

        static float Num(object v)
        {
            if (v is double) return (float)(double)v;
            if (v is long) return (long)v;
            float f; return float.TryParse(v as string, NumberStyles.Float, CultureInfo.InvariantCulture, out f) ? f : 0f;
        }

        Transform[] KneeBones(Dictionary<string, object> o, string key, string side)
        {
            // 表の名前（Unity の人型の名前 LeftUpperLeg・LeftLowerLeg・LeftFoot）を、このゲームの骨の名前に読みかえる
            var t = new Transform[3];
            string[] ours = { side + "UpLeg", side + "Leg", side + "Foot" };
            var an = model.GetComponentInChildren<Animator>();
            var names = o.ContainsKey(key) ? o[key] as List<object> : null;
            for (int i = 0; i < 3; i++)
            {
                if (an != null && an.isHuman && names != null && i < names.Count)
                {
                    try
                    {
                        var hb = (HumanBodyBones)System.Enum.Parse(typeof(HumanBodyBones), names[i] as string);
                        t[i] = an.GetBoneTransform(hb);
                    }
                    catch { }
                }
                if (t[i] == null) t[i] = B(ours[i]);
            }
            return t;
        }

        static void KneeRest(Transform[] b, out Quaternion rest, out Vector3 shinAxis)
        {
            rest = Quaternion.identity; shinAxis = Vector3.down;
            if (b == null || b[0] == null || b[1] == null || b[2] == null) return;
            rest = Quaternion.Inverse(b[0].rotation) * b[1].rotation;
            Vector3 d = Quaternion.Inverse(b[0].rotation) * (b[2].position - b[1].position);
            if (d.sqrMagnitude > 1e-10f) shinAxis = d.normalized;
        }

        /// <summary>すねの骨の回り（休みの姿勢から、太ももの骨の中で）を、ねじれ（すねの向きのまわり）と振りに分ける。</summary>
        static void KneeTwist(Transform[] b, Quaternion rest, Vector3 ax, out float twist, out float axisDev, out Vector3 swingAxis)
        {
            twist = 0f; axisDev = 0f; swingAxis = Vector3.zero;
            if (b == null || b[0] == null || b[1] == null) return;
            Quaternion d = Quaternion.Inverse(b[0].rotation) * b[1].rotation * Quaternion.Inverse(rest);
            Vector3 v = new Vector3(d.x, d.y, d.z);
            Vector3 p = Vector3.Dot(v, ax) * ax;
            var tw = new Quaternion(p.x, p.y, p.z, d.w);
            float n = Mathf.Sqrt(tw.x * tw.x + tw.y * tw.y + tw.z * tw.z + tw.w * tw.w);
            if (n < 1e-6f) { twist = 180f; tw = Quaternion.identity; }
            else
            {
                tw = new Quaternion(tw.x / n, tw.y / n, tw.z / n, tw.w / n);
                twist = 2f * Mathf.Acos(Mathf.Clamp01(Mathf.Abs(tw.w))) * Mathf.Rad2Deg;
            }
            Quaternion sw = d * Quaternion.Inverse(tw);
            float a; sw.ToAngleAxis(out a, out swingAxis);
            if (float.IsNaN(swingAxis.x) || float.IsInfinity(swingAxis.x)) swingAxis = Vector3.right;
            // ひざの横の軸（休みの姿勢の太ももの骨の座標でほぼ (1,0,0)）とのなす角。左右で向きが逆でもよいので絶対値
            axisDev = Mathf.Acos(Mathf.Clamp01(Mathf.Abs(swingAxis.normalized.x))) * Mathf.Rad2Deg;
        }

        static float Ramp(float v, float full, float zero) { return v <= full ? 1f : v >= zero ? 0f : 1f - (v - full) / (zero - full); }

        static float KneeAngle(Transform[] b)
        {
            if (b == null || b[0] == null || b[1] == null || b[2] == null) return -1f;
            Vector3 u = b[1].position - b[0].position, l = b[2].position - b[1].position;
            if (u.sqrMagnitude < 1e-10f || l.sqrMagnitude < 1e-10f) return -1f;
            return Mathf.Acos(Mathf.Clamp(Vector3.Dot(u.normalized, l.normalized), -1f, 1f)) * Mathf.Rad2Deg;
        }

        /// <summary>毎コマ（LateUpdateCore の足の合わせのあと、揺れの前）。</summary>
        void UpdateKnee()
        {
            if (kneeKeys == null) return;
            if (!KneeOn)
            {
                if (kneeWasOn) { foreach (var k in kneeKeys) Put(k, 0f); kneeWasOn = false; }
                return;
            }
            kneeWasOn = true;
            float aL = KneeAngle(kneeBonesL), aR = KneeAngle(kneeBonesR);
            KneeAngleL = aL; KneeAngleR = aR;
            KneeFacL = KneeFac(kneeBonesL, kneeRestL, kneeShinAxisL, aL, out KneeTwistL, out KneeAxisDevL, out KneeSwingAxisL);
            KneeFacR = KneeFac(kneeBonesR, kneeRestR, kneeShinAxisR, aR, out KneeTwistR, out KneeAxisDevR, out KneeSwingAxisR);
            foreach (var k in kneeKeys)
            {
                float a = k.side == 0 ? aL : aR;
                Put(k, a < 0f ? 0f : k.Eval(a) * (k.side == 0 ? KneeFacL : KneeFacR));
            }
        }

        static float KneeFac(Transform[] b, Quaternion rest, Vector3 ax, float bend, out float twist, out float axisDev, out Vector3 swingAxis)
        {
            KneeTwist(b, rest, ax, out twist, out axisDev, out swingAxis);
            if (!KneeTwistGuard) return 1f;
            float f = Ramp(twist, KneeTwistFull, KneeTwistZero);
            if (bend >= KneeAxisMinBend) f *= Ramp(axisDev, KneeAxisFull, KneeAxisZero);
            return f;
        }

        static void Put(KneeKey k, float v)
        {
            if (Mathf.Abs(v - k.last) < 0.01f) return;   // 前のコマと同じなら入れない
            k.last = v;
            foreach (var t in k.targets) if (t.Key != null) t.Key.SetBlendShapeWeight(t.Value, v);
        }

        /// <summary>確かめ用：今の左右の角度と、強さが 0 でないキー。</summary>
        public string KneeText()
        {
            if (kneeKeys == null)
            {
                string ang = kneeBonesL != null ? "角度 左 " + KneeAngle(kneeBonesL).ToString("0.0") + "°・右 " + KneeAngle(kneeBonesR).ToString("0.0") + "°・" : "";
                return ang + "動かすキーなし" + (KneeInfo != null ? "（" + KneeInfo + "）" : "");
            }
            var sb = new StringBuilder();
            sb.Append("角度 左 " + KneeAngleL.ToString("0.0") + "°・右 " + KneeAngleR.ToString("0.0") + "°（" + (KneeOn ? "動かす" : "止めている") + "）・");
            sb.Append("ねじれ 左 " + KneeTwistL.ToString("0.0") + "°・右 " + KneeTwistR.ToString("0.0") + "°・軸のずれ 左 " + KneeAxisDevL.ToString("0.0") + "°・右 " + KneeAxisDevR.ToString("0.0")
                + "°・係数 左 " + KneeFacL.ToString("0.00") + "・右 " + KneeFacR.ToString("0.00") + (KneeTwistGuard ? "" : "（弱めない）")
                + "・振りの軸 左 " + KneeSwingAxisL.ToString("F2") + "・右 " + KneeSwingAxisR.ToString("F2") + "・");
            int nz = 0;
            foreach (var k in kneeKeys)
                if (k.last > 0.05f) { sb.Append(k.name + "=" + k.last.ToString("0.0") + " "); nz++; }
            if (nz == 0) sb.Append("0 でないキーなし");
            return sb.ToString();
        }
    }

    /// <summary>小さな JSON 読み（ひざの角度表用。オブジェクト→Dictionary、配列→List、数→double、文字→string）。</summary>
    public static class MiniJson
    {
        public static object Parse(string s) { int i = 0; var v = Val(s, ref i); return v; }
        static void Ws(string s, ref int i) { while (i < s.Length && (char.IsWhiteSpace(s[i]) || s[i] == '﻿')) i++; }
        static object Val(string s, ref int i)
        {
            Ws(s, ref i);
            if (i >= s.Length) throw new System.FormatException("終わりが早い");
            char c = s[i];
            if (c == '{')
            {
                var d = new Dictionary<string, object>(); i++;
                Ws(s, ref i); if (s[i] == '}') { i++; return d; }
                while (true)
                {
                    Ws(s, ref i); string k = Str(s, ref i); Ws(s, ref i);
                    if (s[i] != ':') throw new System.FormatException(": が無い " + i); i++;
                    d[k] = Val(s, ref i); Ws(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == '}') { i++; return d; }
                    throw new System.FormatException(", か } が無い " + i);
                }
            }
            if (c == '[')
            {
                var l = new List<object>(); i++;
                Ws(s, ref i); if (s[i] == ']') { i++; return l; }
                while (true)
                {
                    l.Add(Val(s, ref i)); Ws(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == ']') { i++; return l; }
                    throw new System.FormatException(", か ] が無い " + i);
                }
            }
            if (c == '"') return Str(s, ref i);
            if (string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
            if (string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
            if (string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
            int st = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            if (i == st) throw new System.FormatException("読めない文字 " + i);
            return double.Parse(s.Substring(st, i - st), NumberStyles.Float, CultureInfo.InvariantCulture);
        }
        static string Str(string s, ref int i)
        {
            if (s[i] != '"') throw new System.FormatException("\" が無い " + i);
            i++; var sb = new StringBuilder();
            while (s[i] != '"')
            {
                if (s[i] == '\\')
                {
                    i++; char e = s[i];
                    if (e == 'u') { sb.Append((char)System.Convert.ToInt32(s.Substring(i + 1, 4), 16)); i += 4; }
                    else sb.Append(e == 'n' ? '\n' : e == 't' ? '\t' : e == 'r' ? '\r' : e == 'b' ? '\b' : e == 'f' ? '\f' : e);
                }
                else sb.Append(s[i]);
                i++;
            }
            i++;
            return sb.ToString();
        }
    }
}
