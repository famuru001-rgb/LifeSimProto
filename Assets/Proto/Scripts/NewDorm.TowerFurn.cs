using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 寮の丸い塔の部屋の家具（依頼33・塔の部屋_v1。famuru 2026-11-13「寮の塔の部屋の家具を作る」。家具のチャット）。
    /// 2階＝小さな談話室（北＝先生の塔・南＝南西の塔）、3階＝トレーニング部屋（先生の塔）・シャワールーム（南西の塔）。
    /// 先生の塔の1階の個室3つ（扇形）のベッド・机・たんすは NTeacherTower から TowerBedroom を呼ぶ。
    /// 弧の家具（*_Arc）は原点が塔の中心・弧の真ん中が Blender の +Y（Unity では −Z）。座標は反転前（MirrorDorm の前）。
    /// </summary>
    public partial class LoungeWorld
    {
        const string TF = "TowerRooms/Furn_";
        public static bool TowerFurnOn { get { return Resources.Load<GameObject>("Furniture/" + TF + "WindowSeat_Arc") != null; } }
        readonly Dictionary<Color, Material> loungeTint = new Dictionary<Color, Material>();

        static Vector3 TDir(float a) { return new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), 0f, Mathf.Sin(a * Mathf.Deg2Rad)); }
        static float YawFacing3(Vector3 f) { return Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg; }

        /// <summary>弧の家具を、塔の中心 c・向き a（度。塔の中心から見た弧の真ん中）に置く。奥行 depth の所をふさぐ。</summary>
        GameObject PlaceArc(string name, Vector2 c, float a, int floor, float arcDeg, float depth, Color? tint = null, float widthScale = 1f)
        {
            float y = FloorY(floor);
            Vector3 u = TDir(a);
            var go = PlaceFurn(TF + name, null, new Vector3(c.x, y, c.y), YawFacing3(-u), false, Mathf.Approximately(widthScale, 1f) ? (Vector3?)null : new Vector3(widthScale, 1f, 1f));
            if (go == null) return null;
            go.name = "Furn_" + name; // 見る階を分けるとき、名前が Furn_ で始まる物は原点の高さで階を決める（DormBuilder.LevelOf）
            Tint(go, tint);
            float r = 4.35f - depth / 2f, half = depth / 2f;
            int n = Mathf.Max(2, Mathf.CeilToInt(arcDeg * Mathf.Deg2Rad * r / 0.3f));
            for (int i = 0; i <= n; i++)
            {
                Vector3 p = new Vector3(c.x, 0f, c.y) + TDir(a - arcDeg / 2f + arcDeg * i / n) * r;
                blockers[floor].Add(new Rect(p.x - half, p.z - half, depth, depth));
            }
            return go;
        }

        /// <summary>ふつうの家具（原点は床の中心・正面を f の向きへ）。</summary>
        GameObject PlaceTF(string name, Vector3 pos, Vector3 facing, int floor, Vector2 block, Color? tint = null)
        {
            var go = PlaceFurn(TF + name, null, new Vector3(pos.x, FloorY(floor), pos.z), YawFacing3(facing));
            if (go == null) return null;
            go.name = "Furn_" + name;
            Tint(go, tint);
            if (block.x > 0f) blockers[floor].Add(new Rect(pos.x - block.x / 2f, pos.z - block.y / 2f, block.x, block.y));
            return go;
        }

        void Tint(GameObject go, Color? tint)
        {
            if (go == null || !tint.HasValue) return;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var ms = r.sharedMaterials; bool ch = false;
                for (int i = 0; i < ms.Length; i++)
                {
                    if (ms[i] == null || !ms[i].name.StartsWith("Fabric_Lounge")) continue;
                    Material m;
                    if (!loungeTint.TryGetValue(tint.Value, out m)) { m = new Material(ms[i]) { name = "Fabric_Lounge_Tint" }; m.SetColor("_BaseColor", tint.Value); loungeTint[tint.Value] = m; }
                    ms[i] = m; ch = true;
                }
                if (ch) r.sharedMaterials = ms;
            }
        }

        static Transform FindDeep(Transform t, string n)
        {
            if (t.name == n) return t;
            for (int i = 0; i < t.childCount; i++) { var r = FindDeep(t.GetChild(i), n); if (r != null) return r; }
            return null;
        }
        Vector3 Mark(GameObject go, string n, Vector3 fallback)
        {
            var t = go != null ? FindDeep(go.transform, n) : null;
            return t != null ? root.InverseTransformPoint(t.position) : fallback;
        }

        /// <summary>塔の2・3階の家具（NTowerRooms のあと）。</summary>
        void NTowerFurniture()
        {
            if (!TowerFurnOn) return;
            Vector2 tc = TeacherTowerC, sc = new Vector2(-WingOut, FrontOut);
            TowerLounge(tc, 180f, "小さな談話室（北）", new Color(0.22f, 0.40f, 0.30f));
            TowerLounge(sc, 90f, "小さな談話室（南）", new Color(0.18f, 0.24f, 0.45f));
            TowerTraining(tc, 180f);
            TowerShower(sc, 90f);
        }

        /// <summary>小さな談話室（2階）。a0＝建物の外に出ている弧のはじめの角度（窓は a0+45・135・225）。</summary>
        void TowerLounge(Vector2 c, float a0, string area, Color col)
        {
            const int f = 1; float y = FloorY(f);
            Vector3 c3 = new Vector3(c.x, 0f, c.y);
            foreach (float w in new[] { a0 + 45f, a0 + 135f, a0 + 225f })
            {
                var ws = PlaceArc("WindowSeat_Arc", c, w, f, 40f, 0.55f, col);
                foreach (var sn in new[] { "Seat_1", "Seat_2" })
                {
                    Vector3 p = Mark(ws, sn, c3 + TDir(w) * 4.0f); p.y = y;
                    Vector3 fwd = (c3 - new Vector3(p.x, 0f, p.z)).normalized;
                    seats.Add(new Seat { kind = SeatKind.Sit, label = "窓辺のベンチ", area = area, position = p, forward = fwd, approach = p + fwd * 0.6f });
                }
            }
            // 本棚は窓と窓のあいだに2つずつ（famuru 2026-11-14「本棚を倍にしてソファとの間隔を詰めて」）。窓辺のベンチ（40°）のあいだの 50° を 25° ずつで埋める
            foreach (float b in new[] { a0 + 77.5f, a0 + 102.5f, a0 + 167.5f, a0 + 192.5f })
            {
                PlaceArc("Bookshelf_Arc", c, b, f, 25f, 0.35f, null, 25f / 30f);
                Vector3 q = c3 + TDir(b) * 3.45f; q.y = y;
                pois.Add(MakePoi("本棚の本を眺めている", c3 + TDir(b) * 4.2f + Vector3.up * (y + 1.2f), q)); // 読書が趣味の子も本を選びに来る（2026-11-14）
            }
            Vector3 mid = TDir(a0 + 135f), side = TDir(a0 + 225f);
            Vector3 tp = c3 + mid * 1.6f;
            var rug = PlaceTF("RoundRug", tp, mid, f, Vector2.zero, col);
            if (rug != null) foreach (var r in rug.GetComponentsInChildren<Renderer>()) slabRenderers.Add(new KeyValuePair<float, Renderer>(Mathf.Max(0.02f, y), r));
            PlaceTF("TeaTable_Round", tp, mid, f, new Vector2(0.9f, 0.9f));
            // 真ん中のひじかけいすは3つ（famuru 2026-11-14「向きは少しずらして」）。テーブルのまわりに 120° ずつ、向きは少しずつずらす
            float[] chairA = { 0f, 120f, 240f }, chairTwist = { -12f, 9f, 16f };
            for (int k = 0; k < 3; k++)
            {
                Vector3 cp = tp + Quaternion.Euler(0f, chairA[k] + 30f, 0f) * mid * 1.05f;
                Vector3 face = Quaternion.Euler(0f, chairTwist[k], 0f) * (tp - cp).normalized;
                var ch = PlaceTF("ArmChair_Round", cp, face, f, new Vector2(0.75f, 0.75f), col);
                Vector3 p = Mark(ch, "Seat_1", cp); p.y = y;
                seats.Add(new Seat { kind = SeatKind.Sit, label = "ひじかけいす", area = area, position = p, forward = face, approach = p - face * 0.7f });
            }
            Vector3 lp = c3 + TDir(a0 + 12f) * 3.6f; // 本棚が増えたので、はしの窓のない所へ（2026-11-14）
            var lamp = PlaceTF("FloorLamp", lp, -TDir(a0 + 12f), f, new Vector2(0.45f, 0.45f));
            Vector3 lq = Mark(lamp, "Light_1", lp + Vector3.up * 1.3f); if (lamp == null) lq.y = y + 1.3f;
            Lamp(lq, 1.2f, 4.5f);
        }

        /// <summary>トレーニング部屋（先生の塔の3階）。</summary>
        void TowerTraining(Vector2 c, float a0)
        {
            const int f = 2; float y = FloorY(f); const string area = "トレーニング部屋";
            Vector3 c3 = new Vector3(c.x, 0f, c.y);
            Vector3 mid = TDir(a0 + 135f);
            Vector3 mp = c3 + mid * 1.6f;
            var mat = PlaceTF("TrainingMat_Round", mp, mid, f, Vector2.zero);
            if (mat != null) foreach (var r in mat.GetComponentsInChildren<Renderer>()) slabRenderers.Add(new KeyValuePair<float, Renderer>(Mathf.Max(0.02f, y), r));
            Vector3 mw = mp; mw.y = y;
            pois.Add(MakePoi("マットの上で体をほぐしている", mw + Vector3.up * 0.3f, mw + TDir(a0 + 45f) * 0.6f, mw + TDir(a0 + 225f) * 0.6f));
            PlaceArc("WeaponRack_Arc", c, a0 + 90f, f, 30f, 0.4f);
            var bars = PlaceArc("WallBars_Arc", c, a0 + 180f, f, 30f, 0.2f);
            { Vector3 q = c3 + TDir(a0 + 180f) * 3.75f; q.y = y; pois.Add(MakePoi("肋木でのびをしている", c3 + TDir(a0 + 180f) * 4.3f + Vector3.up * (y + 1.4f), q)); }
            { Vector3 q = c3 + TDir(a0 + 90f) * 3.5f; q.y = y; pois.Add(MakePoi("木剣と杖を手入れしている", c3 + TDir(a0 + 90f) * 4.2f + Vector3.up * (y + 1.0f), q)); }
            {
                Vector3 dp = c3 + TDir(a0 + 62f) * 3.0f, face = -TDir(a0 + 62f);
                var dm = PlaceTF("TrainingDummy", dp, face, f, new Vector2(0.6f, 0.6f));
                Vector3 look = Mark(dm, "Look", dp + Vector3.up * 1.2f); if (dm == null) look.y = y + 1.2f;
                Vector3 q = dp + face * 0.95f; q.y = y;
                pois.Add(MakePoi("木人で剣の練習をしている", look, q));
                pois[pois.Count - 1].subject = Academics.Sword; // 剣術の自主練習にも使う（2026-11-14）
            }
            {
                Vector3 tp = c3 + TDir(a0 + 205f) * 3.4f, face = -TDir(a0 + 205f);
                var tg = PlaceTF("MagicTarget", tp, face, f, new Vector2(0.9f, 0.9f));
                Vector3 look = Mark(tg, "Look", tp + Vector3.up * 1.25f); if (tg == null) look.y = y + 1.25f;
                Vector3 q = tp + face * 2.4f; q.y = y;
                pois.Add(MakePoi("魔法の的に向かって練習している", look, q));
                pois[pois.Count - 1].subject = Academics.MagicArts; // 魔法術の自主練習・魔法が趣味の子の練習にも使う（2026-11-14）
            }
            {
                Vector3 bp = c3 + TDir(a0 + 135f) * 3.6f, face = -TDir(a0 + 135f);
                var bn = PlaceTF("TrainingBench", bp, face, f, Vector2.zero);
                Vector3 side = new Vector3(face.z, 0f, -face.x);
                for (int k = -2; k <= 2; k++) { Vector3 q = bp + side * (k * 0.3f); blockers[f].Add(new Rect(q.x - 0.2f, q.z - 0.2f, 0.4f, 0.4f)); }
                foreach (var sn in new[] { "Seat_1", "Seat_2" })
                {
                    Vector3 p = Mark(bn, sn, bp); p.y = y;
                    seats.Add(new Seat { kind = SeatKind.Sit, label = "トレーニング部屋の長いす", area = area, position = p, forward = face, approach = p + face * 0.6f });
                }
            }
        }

        /// <summary>シャワールーム（南西の塔の3階）。</summary>
        void TowerShower(Vector2 c, float a0)
        {
            const int f = 2; float y = FloorY(f); const string area = "シャワールーム";
            Vector3 c3 = new Vector3(c.x, 0f, c.y);
            // 1人用のシャワー4つ（窓と窓のあいだに2つずつ。仕切りは片側だけなので、並びの右はしは開いている）
            var stands = new List<Vector3>();
            foreach (float a in new[] { a0 + 70f, a0 + 110f, a0 + 160f, a0 + 200f })
            {
                var sb = PlaceArc("ShowerBooth_Arc", c, a, f, 40f, 0.25f);
                Vector3 p = Mark(sb, "Stand_1", c3 + TDir(a) * 3.8f); p.y = y;
                stands.Add(p);
            }
            pois.Add(MakePoi("シャワーを浴びている", new Vector3(c.x, y + 1.8f, c.y), stands.ToArray()));
            PlaceArc("Washbasin_Arc", c, a0 + 250f, f, 30f, 0.55f);
            { Vector3 q = c3 + TDir(a0 + 250f) * 3.4f; q.y = y; pois.Add(MakePoi("鏡の前で身なりを整えている", c3 + TDir(a0 + 250f) * 4.3f + Vector3.up * (y + 1.6f), q)); }
            PlaceArc("TowelShelf_Arc", c, a0 + 15f, f, 25f, 0.4f);
            Vector3 bp = c3 + TDir(a0 + 135f) * 1.5f;
            var bench = PlaceTF("RoundBench_Center", bp, TDir(a0 + 135f), f, new Vector2(1.3f, 1.3f));
            for (int k = 1; k <= 4; k++)
            {
                Vector3 p = Mark(bench, "Seat_" + k, bp + TDir(k * 90f) * 0.58f); p.y = y;
                Vector3 fwd = new Vector3(p.x - bp.x, 0f, p.z - bp.z).normalized;
                seats.Add(new Seat { kind = SeatKind.Sit, label = "シャワールームのベンチ", area = area, position = p, forward = fwd, approach = p + fwd * 0.6f });
            }
        }

        /// <summary>先生の塔の1階の扇形の個室（真ん中の角度 m）の家具。ベッドの真ん中と頭の向きを返す。</summary>
        bool TowerBedroom(Vector2 c, float m, out Vector3 bedPos, out Vector3 headDir, out Vector3 deskPos, out Vector3 chairPos)
        {
            bedPos = headDir = deskPos = chairPos = Vector3.zero;
            if (!TowerFurnOn) return false;
            const int f = 0;
            Vector3 c3 = new Vector3(c.x, 0f, c.y);
            // ベッド：扇形の片方のふち（m−45）の壁ぞい、頭は外の壁へ
            float ab = m - 45f; Vector3 ub = TDir(ab), inB = TDir(ab + 90f);
            bedPos = c3 + ub * 2.6f + inB * 0.62f; headDir = ub;
            PlaceTF("Bed_SingleTower", bedPos, -ub, f, Vector2.zero, new Color(0.55f, 0.33f, 0.24f));
            {
                Vector3 a = bedPos - ub * 1.05f - inB * 0.55f, b = bedPos + ub * 1.05f + inB * 0.55f;
                blockers[f].Add(Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.z, b.z), Mathf.Max(a.x, b.x), Mathf.Max(a.z, b.z)));
            }
            PlaceTF("NightStand", c3 + ub * 3.35f + inB * 1.45f, inB, f, new Vector2(0.45f, 0.45f));
            // たんす：もう片方のふち（m+45）の壁ぞい
            float aw = m + 45f; Vector3 uw = TDir(aw), inW = TDir(aw - 90f);
            PlaceTF("Wardrobe_Tower", c3 + uw * 3.1f + inW * 0.33f, inW, f, new Vector2(0.75f, 0.75f));
            // 机：窓の下の弧の机。いすは中から外を向く
            PlaceArc("Desk_Arc", c, m, f, 35f, 0.6f);
            deskPos = c3 + TDir(m) * 4.05f;
            chairPos = c3 + TDir(m) * 3.3f;
            PlaceFurn("Furn_Chair", null, chairPos, YawFacing3(TDir(m)));
            return true;
        }
    }
}
