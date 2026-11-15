using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 設置の仕組み 段2：視界の道具（2026-11-16 famuru「次に進んで」。決定：道具は全部・どこでも置けるが、物と場所によって撤去されることがある・続く時間は種類ごと・手に入れ方は購買部とダンジョンの宝箱）。
    /// 設置_品物.csv の「道具の効きめ」の列（「／」区切り）：
    ///   置く道具（種類＝道具）
    ///   ・見える半径8 … 置いた所のまわりが、篤から見えなくても見える（夜も）。置きランタン
    ///   ・区画が見える … 置いた区画（部屋）がまるごと見える。使い魔の見張り台
    ///   ・聞こえる半径10 … まわりの子の吹き出しだけ、見えなくても出る。盗聴の魔導具
    ///   ・影が見える半径8 … まわりの壁ごしの子を黒い影で見せる。透視の香
    ///   ・続く時間6 … 6時間で消える（書かなければ、しまうまでずっと。周もまたぐ）
    ///   ・見つかりやすさ0.05 … 1時間に見つかって撤去される見こみ（場所の表の「見つかりやすさ」とまわりの人の数を掛ける。篤の部屋は0）
    ///   持つ道具（種類＝持つ道具。篤の「持ち物」の欄で「使う」）
    ///   ・夜の扇形90度18m … 持っているあいだ（「使う」で入り切り）、夜の扇形が広く遠くなる。ランタン
    ///   ・飲むと影が見える半径20・1時間 … 1つ使うと、しばらく篤のまわりの壁ごしの子が黒い影で見える。透視薬
    /// 見え方は視界の制限（LoungeWorld.Fog.cs・LifeSimFog.shader）につながる。視界の制限が切れていれば、見える・聞こえるは関係ない（撤去と時間切れはある）。
    /// </summary>
    public static partial class PlaceBook
    {
        public class ToolFx
        {
            public float seeRadius, hearRadius, ghostRadius, hours, findPerHour;
            public bool seeArea;
            public float coneDeg, coneDist;          // 持つランタン
            public float potionRadius, potionHours;  // 透視薬
            public bool Any { get { return seeRadius > 0 || hearRadius > 0 || ghostRadius > 0 || seeArea || coneDeg > 0 || potionRadius > 0 || hours > 0 || findPerHour > 0; } }
        }

        static readonly Regex NumRe = new Regex(@"([0-9.]+)");

        public static ToolFx ParseTool(string name, string text, List<string> errors)
        {
            var f = new ToolFx();
            if (string.IsNullOrEmpty(text)) return f;
            foreach (var raw in text.Split('／', '/'))
            {
                string t = raw.Trim().Replace("　", "").Replace(" ", "");
                if (t.Length == 0) continue;
                var nums = new List<float>();
                foreach (Match m in NumRe.Matches(t))
                {
                    float v;
                    if (float.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) nums.Add(v);
                }
                float n0 = nums.Count > 0 ? nums[0] : 0f, n1 = nums.Count > 1 ? nums[1] : 0f;
                if (t == "区画が見える") f.seeArea = true;
                else if (t.StartsWith("飲むと")) { f.potionRadius = n0; f.potionHours = n1 > 0 ? n1 : 1f; }
                else if (t.StartsWith("見える半径")) f.seeRadius = n0;
                else if (t.StartsWith("聞こえる半径")) f.hearRadius = n0;
                else if (t.StartsWith("影が見える半径")) f.ghostRadius = n0;
                else if (t.StartsWith("続く時間")) f.hours = n0;
                else if (t.StartsWith("見つかりやすさ")) f.findPerHour = n0;
                else if (t.StartsWith("夜の扇形")) { f.coneDeg = n0; f.coneDist = n1; }
                else if (errors != null) errors.Add(name + "：道具の効きめ「" + t + "」が読めない");
            }
            return f;
        }

        // 持つ道具の状態（篤だけ）
        public static string LanternOn;         // 使っている持つランタンの名前（無ければ null）
        public static double PotionUntil = -1;  // 透視薬が効いているのはこの時刻（ゲームの分）まで
        public static float PotionRadius;
    }

    public partial class LoungeWorld
    {
        double toolLastNow = -1;
        float toolTimer;
        public int ToolFoundCount, ToolExpiredCount;   // 試験用

        /// <summary>毎コマ（Update）：0.5 秒ごとに、時間切れと「見つかって撤去」を調べる。</summary>
        void UpdateTools()
        {
            toolTimer -= Time.unscaledDeltaTime;
            if (toolTimer > 0f) return;
            toolTimer = 0.5f;
            double now = Now;
            if (toolLastNow < 0 || now < toolLastNow) { toolLastNow = now; return; }
            float dtMin = Mathf.Min(60f, (float)(now - toolLastNow));   // 時間を飛ばしても1回に1時間分まで
            toolLastNow = now;
            if (dtMin <= 0f) return;
            ToolTick(dtMin);
        }

        /// <summary>試験用（place tick 分）：その分だけ、見つかるかどうかを調べる。</summary>
        public void DebugToolTick(float minutes) { while (minutes > 0f) { ToolTick(Mathf.Min(60f, minutes)); minutes -= 60f; } }

        void ToolTick(float dtMin)
        {
            double now = Now;
            var f = RomanceTarget;
            for (int i = PlaceBook.Placed.Count - 1; i >= 0; i--)
            {
                var p = PlaceBook.Placed[i];
                var d = PlaceBook.Get(p.name);
                if (d == null || !d.IsTool) continue;
                var tf = d.tool;
                // 時間切れ（周が変わって時刻が戻ったときも）
                if (tf.hours > 0f && (now >= p.t + tf.hours * 60.0 || now < p.t))
                {
                    Unplace(p, false);
                    ToolExpiredCount++;
                    Log("「" + p.name + "」の" + (p.name.Contains("使い魔") ? "使い魔が帰っていった" : "効きめが切れて消えた"));
                    continue;
                }
                // 見つかって撤去
                if (tf.findPerHour <= 0f) continue;
                float mul = PlaceFindMul(p.area);
                if (mul <= 0f) continue;
                Vector3 pos = new Vector3(p.x, p.y, p.z);
                SimCharacter finder = null; int near = 0;
                foreach (var c in characters)
                {
                    if (c == null || !c.Present || c.IsTarget) continue;
                    if (c.state == SimCharacter.State.Sleeping) continue;
                    Vector3 v = c.transform.position - pos;
                    if (Mathf.Abs(v.y) > 2.5f || v.x * v.x + v.z * v.z > 8f * 8f) continue;
                    near++;
                    if (finder == null || Random.value < 0.3f) finder = c;
                }
                if (near == 0) continue;
                float guard = 1f;
                if (f != null && (f.transform.position - pos).sqrMagnitude < 6f * 6f) guard = 0.3f;   // 篤がそばにいると見つかりにくい
                float chance = tf.findPerHour * mul * (1f + 0.15f * (near - 1)) * guard * dtMin / 60f;
                if (Random.value < chance)
                {
                    Unplace(p, false);
                    ToolFoundCount++;
                    Log("⚠ " + finder.displayName + "が「" + p.name + "」を見つけて持っていった（" + (p.area ?? "外") + "）");
                }
            }
            if (PlaceBook.PotionUntil > 0 && (now >= PlaceBook.PotionUntil || now < PlaceBook.PotionUntil - PlaceBook.PotionHoursMax * 60.0))
            {
                PlaceBook.PotionUntil = -1;
                Log("透視薬の効きめが切れた");
            }
        }

        /// <summary>場所の見つかりやすさ（設置_場所.csv。表に無い所は、外 0.5・建物の中 1.0）。</summary>
        public float PlaceFindMul(string area)
        {
            var s = area != null ? PlaceBook.SpotOf(area) : null;
            if (s != null && s.findMul >= 0f) return s.findMul;
            if (area == null) return 0.5f;
            foreach (var a in areaList) if (a.name == area) return IsOutdoorArea(a) ? 0.5f : 1f;
            return 1f;
        }

        // ---- 視界の制限とのつなぎ（LoungeWorld.Fog.cs から呼ぶ） ----

        /// <summary>置いた道具で見える所（丸：x,z,半径,階 ／ 四角：区画）を集める。</summary>
        void CollectToolSeen(List<Vector4> circles, List<Vector4> rects, List<float> rectFloors)
        {
            circles.Clear(); rects.Clear(); rectFloors.Clear();
            foreach (var p in PlaceBook.Placed)
            {
                var d = PlaceBook.Get(p.name);
                if (d == null || !d.IsTool) continue;
                int fl = PixFloor(p.y + 0.3f);
                if (d.tool.seeRadius > 0f && circles.Count < FogSeenMax) circles.Add(new Vector4(p.x, p.z, d.tool.seeRadius, fl));
                if (d.tool.seeArea && rects.Count < FogSeenMax)
                {
                    AreaInfo a = null;
                    foreach (var ar in areaList) if (ar.name == p.area) { a = ar; break; }
                    if (a != null) { rects.Add(R4(a.rect, 0.3f)); rectFloors.Add(a.floor); }
                    else if (circles.Count < FogSeenMax) circles.Add(new Vector4(p.x, p.z, 12f, fl));   // 外：まわり 12m
                }
            }
        }

        /// <summary>この子は、置いた盗聴の魔導具で吹き出しだけ聞こえるか。</summary>
        public bool FogHears(SimCharacter c)
        {
            if (!fogWasOn || c == null) return false;
            Vector3 cp = c.transform.position;
            foreach (var p in PlaceBook.Placed)
            {
                var d = PlaceBook.Get(p.name);
                if (d == null || d.tool.hearRadius <= 0f) continue;
                if (Mathf.Abs(cp.y - p.y) > 2.5f) continue;
                float dx = cp.x - p.x, dz = cp.z - p.z;
                if (dx * dx + dz * dz <= d.tool.hearRadius * d.tool.hearRadius) return true;
            }
            return false;
        }

        /// <summary>この場所の見えない子を、黒い影で見せるか（透視の香・透視薬）。</summary>
        bool ToolGhostAt(Vector3 cp)
        {
            foreach (var p in PlaceBook.Placed)
            {
                var d = PlaceBook.Get(p.name);
                if (d == null || d.tool.ghostRadius <= 0f) continue;
                if (Mathf.Abs(cp.y - p.y) > 2.5f) continue;
                float dx = cp.x - p.x, dz = cp.z - p.z;
                if (dx * dx + dz * dz <= d.tool.ghostRadius * d.tool.ghostRadius) return true;
            }
            var f = RomanceTarget;
            if (PlaceBook.PotionUntil > Now && f != null)
            {
                Vector3 v = cp - f.transform.position;
                if (Mathf.Abs(v.y) < 4f && v.x * v.x + v.z * v.z <= PlaceBook.PotionRadius * PlaceBook.PotionRadius) return true;
            }
            return false;
        }

        /// <summary>夜の扇形（持つランタンを使っていれば、その角度・遠さ）。</summary>
        void NightCone(out float halfDeg, out float dist)
        {
            halfDeg = FogNightHalfAngle; dist = FogNightRadius;
            var d = PlaceBook.LanternOn != null ? PlaceBook.Get(PlaceBook.LanternOn) : null;
            if (d == null || PlaceBook.Count(d.name) <= 0) { PlaceBook.LanternOn = null; return; }
            if (d.tool.coneDeg > 0f) halfDeg = Mathf.Max(halfDeg, d.tool.coneDeg);
            if (d.tool.coneDist > 0f) dist = Mathf.Max(dist, d.tool.coneDist);
        }

        /// <summary>持つ道具を使う（篤の持ち物の欄）。</summary>
        public void UseCarry(PlaceBook.Def d)
        {
            if (d == null || !d.IsCarry || PlaceBook.Count(d.name) <= 0) return;
            if (d.tool.coneDeg > 0f)
            {
                PlaceBook.LanternOn = PlaceBook.LanternOn == d.name ? null : d.name;
                Log(PlaceBook.LanternOn != null ? "「" + d.name + "」をともした（夜の見える所が広くなる）" : "「" + d.name + "」を消した");
                return;
            }
            if (d.tool.potionRadius > 0f)
            {
                PlaceBook.Take(d.name);
                PlaceBook.PotionRadius = d.tool.potionRadius;
                PlaceBook.PotionUntil = Now + d.tool.potionHours * 60.0;
                PlaceBook.PotionHoursMax = Mathf.Max(1f, d.tool.potionHours);
                Log("「" + d.name + "」を飲んだ（" + d.tool.potionHours + "時間、まわり " + d.tool.potionRadius + "m の壁ごしの子が影で見える）");
            }
        }
    }

    public static partial class PlaceBook
    {
        public static float PotionHoursMax = 1f;
    }
}
