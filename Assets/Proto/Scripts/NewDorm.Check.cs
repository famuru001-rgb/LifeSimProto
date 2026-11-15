using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 設置ルールの自動チェック（2026-11-15 家具のチャット。famuru「重なるところの家具や壁等の設置ルールを作って重ならないように」）。
    /// 試験命令 overlapcheck で呼ぶ。ルールの文書は 連携\共有\設置ルール_重ならない・すき間なし.md。
    /// ①ドア枠をつらぬく仕切り壁　②壁にめりこむ家具　③家具どうしの重なり　④床のすき間（細いすき間だけ。階段の穴などは広いので出ない）
    /// </summary>
    public partial class LoungeWorld
    {
        static readonly string[] OvlSkipWords = { "DoorFrame", "Window", "Railing", "Rug", "Banner", "Pillar", "Door", "Chandelier", "Lantern", "Lamp", "Cushion", "Curtain", "Carpet", "Flag", "Tile", "Floor", "Vine", "Candle", "Mat", "Skylight", "Roof", "Tower3", "Arch", "Fence", "Sconce", "Bunting", "Garland", "Stair", "Banister", "Chain" };

        static bool OvlSkip(string n)
        {
            foreach (var w in OvlSkipWords) if (n.IndexOf(w, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        static float OvlAmt(float aMin, float aMax, float bMin, float bMax) { return Mathf.Min(aMax, bMax) - Mathf.Max(aMin, bMin); }

        /// <summary>点 (x,z) が、床の板のどれかの上にあるか。</summary>
        static bool OvlCovered(List<Bounds> l, float x, float z)
        {
            foreach (var b in l) if (x > b.min.x + 0.001f && x < b.max.x - 0.001f && z > b.min.z + 0.001f && z < b.max.z - 0.001f) return true;
            return false;
        }

        /// <summary>a(i)・b(j) のあいだのすき間に、ほかの板がかかっているか（かかっていれば、2枚はとなりどうしではない）。xGap＝x 方向のすき間（x が g0〜g1、もう一方は o0〜o1 が重なり範囲）。</summary>
        static bool OvlBetween(List<Bounds> l, int i, int j, bool xGap, float g0, float g1, float o0, float o1)
        {
            float gLo = Mathf.Min(g0, g1), gHi = Mathf.Max(g0, g1), oLo = Mathf.Min(o0, o1), oHi = Mathf.Max(o0, o1);
            for (int k = 0; k < l.Count; k++)
            {
                if (k == i || k == j) continue;
                var c = l[k];
                float cg0 = xGap ? c.min.x : c.min.z, cg1 = xGap ? c.max.x : c.max.z, co0 = xGap ? c.min.z : c.min.x, co1 = xGap ? c.max.z : c.max.x;
                if (cg0 < gHi - 0.001f && cg1 > gLo + 0.001f && Mathf.Min(co1, oHi) - Mathf.Max(co0, oLo) > 0.05f) return true;
            }
            return false;
        }

        public List<string> CheckOverlaps()
        {
            var res = new List<string>();
            if (root == null) { res.Add("overlapcheck: root なし"); return res; }
            var wallB = new List<KeyValuePair<string, Bounds>>();
            var frames = new List<KeyValuePair<string, Bounds>>();
            var furns = new List<KeyValuePair<string, Bounds>>();
            foreach (Transform t in root)
            {
                string n = t.name;
                if (n == "WallLow" || n == "WallHigh")
                {
                    var r = t.GetComponent<Renderer>();
                    if (r != null) wallB.Add(new KeyValuePair<string, Bounds>(n, r.bounds));
                }
                else if (n.StartsWith("Furn_"))
                {
                    var rs = t.GetComponentsInChildren<Renderer>(true);
                    if (rs.Length == 0) continue;
                    Bounds b = rs[0].bounds;
                    for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                    if (n.Contains("DoorFrame")) frames.Add(new KeyValuePair<string, Bounds>(n, b));
                    else if (!OvlSkip(n)) furns.Add(new KeyValuePair<string, Bounds>(n, b));
                }
            }
            int n1 = 0, n2 = 0, n3 = 0, n4 = 0;
            // ① ドア枠をつらぬく仕切り壁（枠と直角の向きの壁）
            foreach (var f in frames)
            {
                bool fAlongX = f.Value.size.x > f.Value.size.z;
                foreach (var w in wallB)
                {
                    bool wAlongX = w.Value.size.x > w.Value.size.z;
                    if (wAlongX == fAlongX) continue;
                    float ox = OvlAmt(f.Value.min.x, f.Value.max.x, w.Value.min.x, w.Value.max.x), oz = OvlAmt(f.Value.min.z, f.Value.max.z, w.Value.min.z, w.Value.max.z), oy = OvlAmt(f.Value.min.y, f.Value.max.y, w.Value.min.y, w.Value.max.y);
                    if (ox > 0.05f && oz > 0.05f && oy > 0.2f) { n1++; if (n1 <= 40) res.Add("①枠×仕切り " + f.Value.center.ToString("F2") + " 枠の幅" + f.Value.size.x.ToString("F2") + "×" + f.Value.size.z.ToString("F2") + " ← 壁 " + w.Value.center.ToString("F2")); }
                }
            }
            // ①b ドア枠のすぐ横（0.25m 以内）にある仕切り壁（枠と直角）
            foreach (var f in frames)
            {
                bool fAlongX = f.Value.size.x > f.Value.size.z;
                foreach (var w in wallB)
                {
                    bool wAlongX = w.Value.size.x > w.Value.size.z;
                    if (wAlongX == fAlongX) continue;
                    float oy = OvlAmt(f.Value.min.y, f.Value.max.y, w.Value.min.y, w.Value.max.y);
                    if (oy < 0.5f) continue;
                    float gap = fAlongX ? OvlAmt(f.Value.min.x, f.Value.max.x, w.Value.min.x, w.Value.max.x) : OvlAmt(f.Value.min.z, f.Value.max.z, w.Value.min.z, w.Value.max.z);
                    float across = fAlongX ? OvlAmt(f.Value.min.z - 0.4f, f.Value.max.z + 0.4f, w.Value.min.z, w.Value.max.z) : OvlAmt(f.Value.min.x - 0.4f, f.Value.max.x + 0.4f, w.Value.min.x, w.Value.max.x);
                    if (across > 0f && gap > -0.25f) { n1++; if (n1 <= 40) res.Add("①b枠のよこの仕切り " + f.Value.center.ToString("F2") + " 枠" + f.Value.size.ToString("F2") + " ← 壁 " + w.Value.center.ToString("F2") + " " + w.Value.size.ToString("F2") + " 重なり/すき間 " + gap.ToString("F2")); }
                }
            }
            // ② 壁にめりこむ家具
            foreach (var f in furns)
                foreach (var w in wallB)
                {
                    float ox = OvlAmt(f.Value.min.x, f.Value.max.x, w.Value.min.x, w.Value.max.x), oz = OvlAmt(f.Value.min.z, f.Value.max.z, w.Value.min.z, w.Value.max.z), oy = OvlAmt(f.Value.min.y, f.Value.max.y, w.Value.min.y, w.Value.max.y);
                    if (ox > 0.12f && oz > 0.12f && oy > 0.3f) { n2++; if (n2 <= 60) res.Add("②家具×壁 " + f.Key + " " + f.Value.center.ToString("F2") + " ← 壁 " + w.Value.center.ToString("F2") + " 重なり " + ox.ToString("F2") + "×" + oz.ToString("F2")); }
                }
            // ③ 家具どうし
            for (int i = 0; i < furns.Count; i++)
                for (int j = i + 1; j < furns.Count; j++)
                {
                    var a = furns[i].Value; var b = furns[j].Value;
                    float ox = OvlAmt(a.min.x, a.max.x, b.min.x, b.max.x), oz = OvlAmt(a.min.z, a.max.z, b.min.z, b.max.z), oy = OvlAmt(a.min.y, a.max.y, b.min.y, b.max.y);
                    if (ox > 0.25f && oz > 0.25f && oy > 0.4f) { n3++; if (n3 <= 60) res.Add("③家具×家具 " + furns[i].Key + " " + a.center.ToString("F2") + " と " + furns[j].Key + " " + b.center.ToString("F2") + " 重なり " + ox.ToString("F2") + "×" + oz.ToString("F2")); }
                }
            // ④ 床のすき間（同じ高さの床の板が、細い幅（0.003〜0.5m）をあけて向かい合っている所）
            var byY = new Dictionary<int, List<Bounds>>();
            foreach (var kv in slabRenderers)
            {
                if (kv.Value == null) continue;
                string n = kv.Value.gameObject.name;
                if (!(n.StartsWith("Floor_") || n.StartsWith("Tower_Floor") || n.StartsWith("Bridge_Floor"))) continue;
                int k = Mathf.RoundToInt(kv.Key * 10f);
                List<Bounds> l; if (!byY.TryGetValue(k, out l)) { l = new List<Bounds>(); byY[k] = l; }
                l.Add(kv.Value.bounds);
            }
            foreach (var kv in byY)
            {
                var l = kv.Value;
                for (int i = 0; i < l.Count; i++)
                    for (int j = i + 1; j < l.Count; j++)
                    {
                        var a = l[i]; var b = l[j];
                        float ox = OvlAmt(a.min.x, a.max.x, b.min.x, b.max.x), oz = OvlAmt(a.min.z, a.max.z, b.min.z, b.max.z);
                        float gx = -ox, gz = -oz;
                        if (oz > 0.1f && gx > 0.003f && gx < 0.5f)
                        {
                            float xm = (Mathf.Min(a.max.x, b.max.x) + Mathf.Max(a.min.x, b.min.x)) / 2f, zm = (Mathf.Max(a.min.z, b.min.z) + Mathf.Min(a.max.z, b.max.z)) / 2f;
                            if (!OvlBetween(l, i, j, true, Mathf.Min(a.max.x, b.max.x), Mathf.Max(a.min.x, b.min.x), Mathf.Max(a.min.z, b.min.z), Mathf.Min(a.max.z, b.max.z))) { n4++; if (n4 <= 60) res.Add("④床のすき間 高さ" + (kv.Key / 10f).ToString("F1") + " x " + Mathf.Min(a.max.x, b.max.x).ToString("F2") + "〜" + Mathf.Max(a.min.x, b.min.x).ToString("F2") + " z " + Mathf.Max(a.min.z, b.min.z).ToString("F2") + "〜" + Mathf.Min(a.max.z, b.max.z).ToString("F2")); }
                        }
                        else if (ox > 0.1f && gz > 0.003f && gz < 0.5f)
                        {
                            float zm = (Mathf.Min(a.max.z, b.max.z) + Mathf.Max(a.min.z, b.min.z)) / 2f, xm = (Mathf.Max(a.min.x, b.min.x) + Mathf.Min(a.max.x, b.max.x)) / 2f;
                            if (!OvlBetween(l, i, j, false, Mathf.Min(a.max.z, b.max.z), Mathf.Max(a.min.z, b.min.z), Mathf.Max(a.min.x, b.min.x), Mathf.Min(a.max.x, b.max.x))) { n4++; if (n4 <= 60) res.Add("④床のすき間 高さ" + (kv.Key / 10f).ToString("F1") + " z " + Mathf.Min(a.max.z, b.max.z).ToString("F2") + "〜" + Mathf.Max(a.min.z, b.min.z).ToString("F2") + " x " + Mathf.Max(a.min.x, b.min.x).ToString("F2") + "〜" + Mathf.Min(a.max.x, b.max.x).ToString("F2")); }
                        }
                    }
            }
            res.Add("overlapcheck: 調べた 枠" + frames.Count + "・壁" + wallB.Count + "・家具" + furns.Count + " / ①枠×仕切り " + n1 + "・②家具×壁 " + n2 + "・③家具×家具 " + n3 + "・④床のすき間 " + n4);
            try { File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/overlap_report.txt")), string.Join("\n", res.ToArray()), new UTF8Encoding(true)); } catch { }
            return res;
        }
    }
}
