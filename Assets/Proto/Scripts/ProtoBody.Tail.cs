using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 2026-11-25 本体：しっぽ（Spring_Tail で始まる揺れの骨）を、上着の後ろの外へ押し出す当たり判定。
    /// 依頼 連携\依頼\本体_ルピナのしっぽに上着の当たり判定.md（famuru 許可。しっぽは服を貫通させ、上着とは当たり判定でずらす）
    /// ・読み込んだとき、上着（名前に Jacket のある服の部品）の面を、腰の骨から見た「高さ（1cm ごと）× 左右（4cm ごと、まん中から12cmまで）」の
    ///   いちばん後ろの位置の表にする（キャラの記号で分けない。しっぽと上着のあるキャラなら同じように効く）
    /// ・しっぽの骨の途中（1/3・2/3・先）が、その面＋余白より前（体の内）にあれば、後ろへだけ押し出す（骨の向きを回す）
    /// ・余白はしっぽの毛の太さの分。根元のすぐ近くは上着の内にあるので、根元からの距離で 0 → TailMargin に増やす
    /// ・上着が見えていない（ほかの服）ときは何もしない。スカートとはぶつけない（スカートは貫通のまま）
    /// </summary>
    public partial class ProtoBody
    {
        public static bool TailBackHit = true;          // 全員：しっぽの上着の当たり判定を使うか（確認用に切りかえられる）
        public static float TailMargin = 0.015f;        // 上着の面からの余白（m）。2026-11-25 ルピナで 3cm と 1.5cm を撮りくらべ、1.5cm（しっぽが後ろへ出すぎない）
        public static float TailMarginRamp = 0.12f;     // 余白が TailMargin になるまでの、しっぽの根元からの距離（m）
        [System.NonSerialized] public bool testNoTailHit;   // 確認用：この子だけ使わない

        const float TailBinH = 0.01f, TailBinX = 0.04f, TailH0 = -0.35f;
        const int TailNH = 70, TailNX = 3;
        GameObject tailJacket;
        float[] tailProf;                                // 高さ×左右 → いちばん後ろ（m）。NaN＝上着がない
        Vector3 tailUpL, tailBackL, tailSideL;           // 腰の骨の座標での 上・後ろ・右
        Vector3 tailO, tailUp, tailBack, tailSide;       // そのコマの値（世界の座標）
        bool tailOn;
        readonly Dictionary<SpringBone, float> tailStart = new Dictionary<SpringBone, float>();   // しっぽの骨 → 根元からの距離
        [System.NonSerialized] public int TailPushes;    // 確認用：押し出した回数（数え直しは TailStatsReset）
        [System.NonSerialized] public float TailPushMax; // 確認用：いちばん大きく押し出した長さ（m）

        public bool HasTailHit { get { return tailProf != null; } }

        void SetupTailBack()
        {
            tailProf = null; tailJacket = null; tailStart.Clear();
            var chain = new List<SpringBone>();
            foreach (var s in springs) if (BaseName(s.bone).StartsWith("Spring_Tail")) chain.Add(s);
            if (chain.Count == 0) return;
            SkinnedMeshRenderer jk = null;
            foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (r.name.Contains("Jacket") && r.sharedMesh != null) { jk = r; break; }
            if (jk == null) return;

            // 根元からの距離（springs は親から順に並んでいる）
            Transform first = chain[0].bone;
            foreach (var s in chain)
            {
                float dist = 0f;
                for (var t = s.bone; t != null && t != first.parent; t = t.parent)
                {
                    if (t == first) break;
                    dist += (t.position - t.parent.position).magnitude;
                }
                tailStart[s] = dist;
            }

            // 上着の面の点を、今の骨の位置で世界の座標にする（読み書きできないメッシュでも使えるよう BakeMesh。ProtoBody.Mob05Preview と同じ使い方）
            var baked = new Mesh();
            jk.BakeMesh(baked);
            var vs = baked.vertices;
            Destroy(baked);
            var jt = jk.transform;

            Vector3 o = hips.position, up = transform.up, side = transform.right, back = -transform.forward;
            // しっぽの根元が後ろ側になるように向きを決める
            if (Vector3.Dot(first.position - o, back) < 0f) back = -back;
            tailProf = new float[TailNH * TailNX];
            for (int i = 0; i < tailProf.Length; i++) tailProf[i] = float.NaN;
            for (int v = 0; v < vs.Length; v++)
            {
                Vector3 d = jt.TransformPoint(vs[v]) - o;
                int ih = Mathf.FloorToInt((Vector3.Dot(d, up) - TailH0) / TailBinH), ix = Mathf.FloorToInt(Mathf.Abs(Vector3.Dot(d, side)) / TailBinX);
                if (ih < 0 || ih >= TailNH || ix >= TailNX) continue;
                float b = Vector3.Dot(d, back);
                if (b <= 0f) continue;   // 体の前半分の点は見ない（点のまばらな段で、前の面の値が入らないように）
                int k = ih * TailNX + ix;
                if (float.IsNaN(tailProf[k]) || b > tailProf[k]) tailProf[k] = b;
            }
            // 上着のある高さの間の、点のない段は上下から埋める（すそより下・えりより上は埋めない）
            for (int ix = 0; ix < TailNX; ix++)
            {
                int lo = -1, hi = -1;
                for (int ih = 0; ih < TailNH; ih++) if (!float.IsNaN(tailProf[ih * TailNX + ix])) { if (lo < 0) lo = ih; hi = ih; }
                if (lo < 0) continue;
                int last = lo;
                for (int ih = lo + 1; ih <= hi; ih++)
                {
                    if (float.IsNaN(tailProf[ih * TailNX + ix])) continue;
                    for (int g = last + 1; g < ih; g++)
                        tailProf[g * TailNX + ix] = Mathf.Lerp(tailProf[last * TailNX + ix], tailProf[ih * TailNX + ix], (float)(g - last) / (ih - last));
                    last = ih;
                }
            }
            tailUpL = hips.InverseTransformDirection(up).normalized;
            tailBackL = hips.InverseTransformDirection(back).normalized;
            tailSideL = hips.InverseTransformDirection(side).normalized;
            tailJacket = jk.gameObject;
        }

        // UpdateSprings の始めに1回：そのコマで効くか・向き
        void TailFrame()
        {
            tailOn = TailBackHit && !testNoTailHit && tailProf != null && tailJacket != null && tailJacket.activeInHierarchy;
            if (tailProf == null) return;
            tailO = hips.position;
            tailUp = hips.TransformDirection(tailUpL).normalized;
            tailBack = hips.TransformDirection(tailBackL).normalized;
            tailSide = hips.TransformDirection(tailSideL).normalized;
        }

        float TailProf(Vector3 p)
        {
            Vector3 d = p - tailO;
            float fh = (Vector3.Dot(d, tailUp) - TailH0) / TailBinH - 0.5f;
            int ix = Mathf.FloorToInt(Mathf.Abs(Vector3.Dot(d, tailSide)) / TailBinX);
            if (ix >= TailNX || fh < 0f || fh > TailNH - 1) return float.NaN;
            int i0 = Mathf.FloorToInt(fh), i1 = Mathf.Min(i0 + 1, TailNH - 1);
            float a = tailProf[i0 * TailNX + ix], b = tailProf[i1 * TailNX + ix];
            if (float.IsNaN(a)) return b;
            if (float.IsNaN(b)) return a;
            return Mathf.Lerp(a, b, fh - i0);
        }

        // Constrain から：しっぽの骨の向き dir を、途中の点が上着の後ろの面＋余白より外へ出るように後ろへ回す
        Vector3 PushTail(SpringBone s, Vector3 root, Vector3 dir, float len)
        {
            float start;
            if (!tailOn || !tailStart.TryGetValue(s, out start)) return dir;
            for (int it = 0; it < 3; it++)
            {
                bool moved = false;
                for (int k = 1; k <= 3; k++)
                {
                    float t = k / 3f;
                    Vector3 p = root + dir * (len * t);
                    float need = TailProf(p);
                    if (float.IsNaN(need)) continue;
                    float m = TailMargin * Mathf.Clamp01((start + len * t) / Mathf.Max(0.001f, TailMarginRamp));
                    float gap = need + m - Vector3.Dot(p - tailO, tailBack);
                    if (gap <= 0f) continue;
                    p += tailBack * gap;
                    Vector3 nd = p - root;
                    if (nd.sqrMagnitude < 1e-10f) continue;
                    dir = nd.normalized; moved = true;
                    TailPushes++; if (gap > TailPushMax) TailPushMax = gap;
                }
                if (!moved) break;
            }
            return dir;
        }

        public void TailStatsReset() { TailPushes = 0; TailPushMax = 0f; }

        /// <summary>確認用：しっぽの骨の途中の点の、上着の後ろの面からの距離（最小、cm。負＝面より内）と、そのときの高さ</summary>
        public string TailClearance()
        {
            if (tailProf == null) return "しっぽか上着が無い";
            bool keep = tailOn; TailFrame();
            float best = float.MaxValue; string where = "";
            foreach (var kv in tailStart)
            {
                var s = kv.Key;
                Vector3 a = s.bone.position, b = s.child.position;
                for (int k = 1; k <= 6; k++)
                {
                    Vector3 p = Vector3.Lerp(a, b, k / 6f);
                    float need = TailProf(p);
                    if (float.IsNaN(need)) continue;
                    float c = Vector3.Dot(p - tailO, tailBack) - need;
                    if (c < best) { best = c; where = BaseName(s.bone) + " " + (k / 6f).ToString("0.00"); }
                }
            }
            tailOn = keep;
            return best == float.MaxValue ? "上着の高さにしっぽが無い" : (best * 100f).ToString("0.0") + "cm（" + where + "）";
        }

        /// <summary>確認用：上着の後ろの面の表（まん中の列、5cm ごと）</summary>
        public string TailProfileText()
        {
            if (tailProf == null) return "しっぽか上着が無い";
            var sb = new StringBuilder();
            sb.Append("上着の後ろ（腰の骨からの高さcm→後ろcm まん中/4cm/8cm）：");
            for (int ih = 0; ih < TailNH; ih += 5)
            {
                float a = tailProf[ih * TailNX], b = tailProf[ih * TailNX + 1], c = tailProf[ih * TailNX + 2];
                if (float.IsNaN(a) && float.IsNaN(b) && float.IsNaN(c)) continue;
                sb.Append(" ").Append(((TailH0 + (ih + 0.5f) * TailBinH) * 100f).ToString("0")).Append("→")
                  .Append(float.IsNaN(a) ? "-" : (a * 100f).ToString("0.0")).Append("/").Append(float.IsNaN(b) ? "-" : (b * 100f).ToString("0.0")).Append("/").Append(float.IsNaN(c) ? "-" : (c * 100f).ToString("0.0"));
            }
            Transform first = null; foreach (var kv in tailStart) if (kv.Value == 0f) first = kv.Key.bone;
            if (first != null)
            {
                Vector3 d = first.position - hips.position;
                sb.Append("・しっぽの根元 高さ ").Append((Vector3.Dot(d, transform.up) * 100f).ToString("0.0")).Append("cm 後ろ ").Append((Vector3.Dot(d, hips.TransformDirection(tailBackL)) * 100f).ToString("0.0")).Append("cm");
            }
            sb.Append("・余白 ").Append((TailMargin * 100f).ToString("0.0")).Append("cm（根元から ").Append((TailMarginRamp * 100f).ToString("0")).Append("cm で満ちる）");
            return sb.ToString();
        }
    }
}
