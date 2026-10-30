using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 学校の本館（2026-10-25。設計\マップ拡大計画.md、間取りの図は famuru 確認ずみの「案A：コの字」）。
    /// 設計図の向き（sx：西→東 0〜70、sy：北→南 0〜46）で書き、プログラムの座標に直して置く（x = 43.3 − 1.5×sy、z = −24.5 − 1.5×sx）。
    /// 2026-10-25 famuru「学園をもっと大きく」：設計図の数字はそのままで、置くときに全体を1.5倍（東西105m×南北69m）。外廊下の西のはしが寮の渡り廊下に合うように x の基準を 31.3→43.3 にした。
    /// ・南の長い棟（sy 25〜46・3階建て）：東西の大廊下（sy 34〜37）の両側に部屋。東西のはしに階段の部屋
    /// ・西に体育館、東に大講堂（sy 0〜23・2階建ての高さ。2階に見下ろし席）。そのあいだが本館の中庭（北に開く）
    /// ・教室の棟の北側に外廊下（sy 23〜25。1階と2階。寮の前庭と渡り廊下は外廊下の西のはしにつながる）
    /// ・家具と授業はこのあと（建物と歩ける所だけ）
    /// ・2026-10-31：空き教室も扉を開けて入れるようにした（二人きりの時間の場所）
    /// </summary>
    public partial class LoungeWorld
    {
        const float SXN = 43.3f, SZ0 = -24.5f;     // 設計図の (0,0) の、プログラムの x と z
        const float SK = 1.5f;                      // 設計図の1を何mにするか（全体の大きさ。famuru 2026-10-25「1.5倍」）
        const float SLen = 70f;                     // 東西の長さ
        const float SVer0 = 23f, SVer1 = 25f;       // 外廊下
        const float SCor0 = 34f, SCor1 = 37f;       // 大廊下
        const float SBack = 46f;                    // 南の外壁
        const float SBand0 = 43.6f, SBand1 = 45.6f, SBand = 44.6f; // 階段の帯

        /// <summary>本館の真ん中（授業へ出かけるときの行き先：2階の大廊下）。</summary>
        public static readonly Vector3 SchoolCorridor2F = new Vector3(SXN - SK * 35.5f, Floor2Y, SZ0 - SK * 35f);
        /// <summary>正門（本館の南。寮生以外の学生・先生はここから来て、ここから自分の寮へ帰る。2026-10-30）。</summary>
        public static Vector3 SchoolGate { get { return S3(35f, 51f, 0f); } }

        static Vector2 SP(float sx, float sy) { return new Vector2(SXN - SK * sy, SZ0 - SK * sx); }
        static Vector3 S3(float sx, float sy, float y) { return new Vector3(SXN - SK * sy, y, SZ0 - SK * sx); }
        static Rect SR(float sx0, float sy0, float sx1, float sy1)
        {
            return Rect.MinMaxRect(SXN - SK * Mathf.Max(sy0, sy1), SZ0 - SK * Mathf.Max(sx0, sx1), SXN - SK * Mathf.Min(sy0, sy1), SZ0 - SK * Mathf.Min(sx0, sx1));
        }
        /// <summary>壁の穴（設計図の長さで書いた位置）を、m に直す。</summary>
        static Vector4 SG(Vector4 g) { return new Vector4(g.x * SK, g.y * SK, g.z, g.w); }
        static Vector4[] SG(Vector4[] gs) { var r = new Vector4[gs.Length]; for (int i = 0; i < gs.Length; i++) r[i] = SG(gs[i]); return r; }
        static Rect SIn(float sx0, float sy0, float sx1, float sy1, float m = 0.25f)
        {
            var r = SR(sx0, sy0, sx1, sy1);
            return Rect.MinMaxRect(r.xMin + m, r.yMin + m, r.xMax - m, r.yMax - m);
        }
        /// <summary>設計図の向き（東・南）を、プログラムの向きに直す。</summary>
        static Vector3 SDir(float east, float south) { return new Vector3(-south, 0f, -east); }
        WallSeg SWall(float y0, float y1, float sx0, float sy0, float sx1, float sy1, params Vector4[] gaps)
        {
            return Wall(SP(sx0, sy0), SP(sx1, sy1), y0, y1, WallT, SG(gaps));
        }
        /// <summary>扉の通り道。horizontal：設計図で東西にのびる壁（sy が一定）。</summary>
        static Rect SDoorNav(float sx, float sy, bool horizontal, float w = 1.2f)
        {
            return DoorNav(SXN - SK * sy, SZ0 - SK * sx, !horizontal, w);
        }

        enum SKind { Room, Vacant, Pass, Stair, Hall }
        class SRoom
        {
            public float x0, x1; public string name, sub; public SKind kind;
            public SRoom(float a, float b, string n, SKind k, string s = "") { x0 = a; x1 = b; name = n; kind = k; sub = s; }
            public float Mid { get { return (x0 + x1) / 2f; } }
        }

        // ---- 階ごとの部屋（北の列 sy 25〜34、南の列 sy 37〜46） ----
        static SRoom R(float a, float b, string n, string s = "") { return new SRoom(a, b, n, SKind.Room, s); }
        static SRoom V(float a, float b, string n = "空き教室") { return new SRoom(a, b, n, SKind.Vacant); }
        static SRoom P(float a, float b, string n) { return new SRoom(a, b, n, SKind.Pass); }

        static readonly SRoom[][] SNorth =
        {
            new[] { R(0, 7.75f, "用具室"), P(7.75f, 10.25f, "体育館への通路"), R(10.25f, 20.5f, "更衣室"), R(20.5f, 33.5f, "シャワー"), P(33.5f, 36.5f, "中庭への通路"),
                    R(36.5f, 59.75f, "学食・購買部"), P(59.75f, 62.25f, "大講堂への通路"), R(62.25f, 70f, "ダンジョン実習の準備室") },
            new[] { V(0, 7.75f), P(7.75f, 10.25f, "体育館の2階席への通路"), R(10.25f, 21.9f, "薬草学の教室"), R(21.9f, 33.5f, "魔物学の教室"), P(33.5f, 36.5f, "外廊下への通路"),
                    R(36.5f, 48f, "魔法薬の調合室"), R(48f, 59.75f, "魔法実習室"), P(59.75f, 62.25f, "大講堂の2階席への通路"), V(62.25f, 70f) },
            new[] { R(0, 11.7f, "生徒会室"), R(11.7f, 23.3f, "ほうき部の部室"), R(23.3f, 35f, "剣術部の部室"), R(35f, 46.7f, "魔法術部の部室"), V(46.7f, 58.3f, "部室（あとで決める）"), V(58.3f, 70f, "部室（あとで決める）") },
        };
        static readonly SRoom[][] SSouth =
        {
            new[] { new SRoom(0, 10, "階段", SKind.Stair), R(10, 16, "宿直室"), R(16, 23, "医学実習室"), R(23, 29, "保健室"), new SRoom(29, 41, "玄関ホール", SKind.Hall),
                    R(41, 55, "職員室"), R(55, 60, "トイレ"), new SRoom(60, 70, "階段", SKind.Stair) },
            new[] { new SRoom(0, 10, "階段", SKind.Stair), R(10, 21.25f, "魔法理論の教室"), R(21.25f, 32.5f, "古代語の教室"), R(32.5f, 43.75f, "魔道具学の教室"), R(43.75f, 55, "王国史の教室"),
                    R(55, 60, "トイレ"), new SRoom(60, 70, "階段", SKind.Stair) },
            new[] { new SRoom(0, 10, "階段", SKind.Stair), V(10, 21), V(21, 32), V(32, 43, "倉庫"), V(43, 55), R(55, 60, "トイレ"), new SRoom(60, 70, "階段", SKind.Stair) },
        };

        static string SFloorName(int f) { return "本館" + (f + 1) + "階・"; }

        void BuildSchool()
        {
            CreatePlaceMaterials(); // 木の葉の色など（DormPlaces.cs）
            BuildSchoolSlabs();
            for (int f = 0; f <= 2; f++) BuildSchoolFloor(f);
            BuildSchoolStairs();
            BuildSchoolVeranda();
            BuildSchoolHall(0f, 18f, "体育館", "西");
            BuildSchoolHall(52f, 70f, "大講堂", "東");
            BuildSchoolCourtyard();
            BuildMobDorms(); // 寮生以外の学生の寮（仮の建物。SchoolDorms.cs）
            // 正門への道（南）
            Box("School_FrontPath", S3(35f, 50f, -0.02f), new Vector3(8f * SK, 0.04f, 4f * SK), flagstone);
            Label(S3(35f, 49f, 1.6f), "正門へ");
        }

        // ================= 床 =================
        void BuildSchoolSlabs()
        {
            Slab("School_Floor", 0, SR(0, 25, SLen, SBack), flagstone);
            // 2階：1階→2階の階段の上はあける
            Slab("School_Floor2", 1, SR(0, 25, SLen, SBand0), woodFloor);
            Slab("School_Floor2", 1, SR(0, SBand0, 5.4f, SBack), woodFloor);
            Slab("School_Floor2", 1, SR(9.2f, SBand0, 60.8f, SBack), woodFloor);
            Slab("School_Floor2", 1, SR(64.6f, SBand0, SLen, SBack), woodFloor);
            // 3階：2階→3階の階段の上はあける
            Slab("School_Floor3", 2, SR(0, 25, SLen, SBand0), woodFloor);
            Slab("School_Floor3", 2, SR(0, SBand0, 1.0f, SBack), woodFloor);
            Slab("School_Floor3", 2, SR(4.4f, SBand0, 65.6f, SBack), woodFloor);
            Slab("School_Floor3", 2, SR(69.0f, SBand0, SLen, SBack), woodFloor);
            Slab("School_Roof", 3, SR(0, 25, SLen, SBack), mortarDark);
        }

        // ================= 南の棟の1つの階 =================
        void BuildSchoolFloor(int f)
        {
            float y = FloorY(f);
            var north = SNorth[f];
            var south = SSouth[f];
            string fn = SFloorName(f);

            // 北の外壁（外廊下の側）：1・2階は通路の所をあける
            var gapsN = new List<Vector4>();
            foreach (var r in north) if (r.kind == SKind.Pass) gapsN.Add(new Vector4(r.x0 + 0.1f, r.x1 - 0.1f, y, y + 2.6f));
            var wN = SWall(y, y + FloorH, 0, 25, SLen, 25, gapsN.ToArray());
            // 大廊下の北の壁：部屋の扉（通路はそのまま開ける）
            var gapsCN = new List<Vector4>();
            foreach (var r in north)
            {
                if (r.kind == SKind.Pass) gapsCN.Add(new Vector4(r.x0 + 0.1f, r.x1 - 0.1f, y, y + 2.6f));
                else gapsCN.Add(DoorGap(f, r.Mid, 1.2f));
            }
            SWall(y, y + FloorH, 0, SCor0, SLen, SCor0, gapsCN.ToArray());
            // 大廊下の南の壁（階段の部屋の所は壁なし）
            var gapsCS = new List<Vector4>();
            foreach (var r in south)
            {
                if (r.kind == SKind.Stair) continue;
                if (r.kind == SKind.Hall) gapsCS.Add(new Vector4(r.Mid - 2f - 10f, r.Mid + 2f - 10f, y, y + 2.8f));
                else gapsCS.Add(DoorGap(f, r.Mid - 10f, 1.2f));
            }
            SWall(y, y + FloorH, 10, SCor1, 60, SCor1, gapsCS.ToArray());
            // 南の外壁：1階は玄関
            var gapsS = new List<Vector4>();
            if (f == 0) gapsS.Add(new Vector4(35f - 1.2f, 35f + 1.2f, y, y + 2.8f));
            var wS = SWall(y, y + FloorH, 0, SBack, SLen, SBack, gapsS.ToArray());
            // 西と東の外壁（1階の西は裏口：寮の前庭へ）
            var wW = f == 0 ? SWall(y, y + FloorH, 0, 25, 0, SBack, new Vector4(35.5f - 25f - 0.8f, 35.5f - 25f + 0.8f, y, y + 2.5f)) : SWall(y, y + FloorH, 0, 25, 0, SBack);
            var wE = SWall(y, y + FloorH, SLen, 25, SLen, SBack);
            // 仕切り
            for (int i = 1; i < north.Length; i++) SWall(y, y + FloorH, north[i].x0, 25, north[i].x0, SCor0);
            for (int i = 1; i < south.Length; i++) SWall(y, y + FloorH, south[i].x0, SCor1, south[i].x0, SBack);

            // 窓
            foreach (var r in north) if (r.kind != SKind.Pass) Window(wN, S3(r.Mid, 25, y), SDir(0, 1), 1.0f, 1.2f, 1.4f);
            foreach (var r in south) if (r.kind != SKind.Hall || f > 0) Window(wS, S3(r.Mid, SBack, y), SDir(0, -1), 1.0f, 1.2f, 1.4f);
            Window(wW, S3(0, 29.5f, y), SDir(1, 0), 1.0f, 1.0f, 1.4f);
            Window(wE, S3(SLen, 29.5f, y), SDir(-1, 0), 1.0f, 1.0f, 1.4f);
            Window(wE, S3(SLen, 35.5f, y), SDir(-1, 0), 1.0f, 1.0f, 1.4f);

            // 大廊下
            for (int i = 0; i < 7; i++) Lamp(S3(5f + i * 10f, 35.5f, y + 2.5f), 2.2f, 7f);
            Label(S3(20f, 35.5f, y + 2.2f), fn + "大廊下");
            AddArea(fn + "大廊下", SR(0, SCor0, SLen, SCor1), f);

            // 部屋
            foreach (var r in north) SchoolRoom(f, r, 25, SCor0, false);
            foreach (var r in south) SchoolRoom(f, r, SCor1, SBack, true);
            // 内装と什器（SchoolInterior.cs）
            foreach (var r in north) FurnishSchoolRoom(f, r, 25, SCor0, false);
            foreach (var r in south) FurnishSchoolRoom(f, r, SCor1, SBack, true);
        }

        void SchoolRoom(int f, SRoom r, float sy0, float sy1, bool southSide)
        {
            float y = FloorY(f);
            float cy = (sy0 + sy1) / 2f;
            string fn = SFloorName(f);
            switch (r.kind)
            {
                case SKind.Vacant:
                    // 空き教室も入れるようにした（2026-10-31 famuru：二人きりの時間に使う）。前は扉が閉まっていた
                    Label(S3(r.Mid, cy, y + 2.0f), r.name);
                    AddArea(fn + r.name, SR(r.x0, sy0, r.x1, sy1), f);
                    break;
                case SKind.Stair:
                    Label(S3(r.Mid, cy - 2f, y + 2.0f), "階段");
                    AddArea(fn + "階段", SR(r.x0, sy0, r.x1, sy1), f);
                    break;
                case SKind.Pass:
                    Lamp(S3(r.Mid, cy, y + 2.4f), 1.4f, 5f);
                    AddArea(fn + r.name, SR(r.x0, sy0, r.x1, sy1), f);
                    break;
                default:
                    Lamp(S3(r.Mid, cy, y + 2.5f), r.x1 - r.x0 > 12f ? 3f : 2.2f, r.x1 - r.x0 > 12f ? 10f : 7f);
                    Label(S3(r.Mid, cy, y + 2.2f), r.name);
                    AddArea(r.kind == SKind.Hall ? "玄関ホール" : r.name, SR(r.x0, sy0, r.x1, sy1), f);
                    break;
            }
        }

        // ================= 階段（東西のはし） =================
        /// <summary>まっすぐな階段（設計図の東西の向き）。sxFrom（下）→ sxTo（上）、高さ y0 → y1。</summary>
        void SchoolFlight(string name, float sxFrom, float sxTo, float y0, float y1)
        {
            int steps = 24;
            float run = (sxTo - sxFrom) / steps, rise = (y1 - y0) / steps;
            float w = (SBand1 - SBand0) * SK;
            for (int i = 0; i < steps; i++)
            {
                float top = (i + 1) * rise;
                var p = S3(sxFrom + (i + 0.5f) * run, SBand, y0 + top / 2f);
                Box(name + "_Step", p, new Vector3(w - 0.05f, top, Mathf.Abs(run) * SK), i % 2 == 0 ? woodDark : woodMid);
            }
        }

        void BuildSchoolStairs()
        {
            SchoolFlight("SchoolStairWA", 9.2f, 5.4f, 0f, FloorY(1));
            SchoolFlight("SchoolStairWB", 4.4f, 1.0f, FloorY(1), FloorY(2));
            SchoolFlight("SchoolStairEA", 60.8f, 64.6f, 0f, FloorY(1));
            SchoolFlight("SchoolStairEB", 65.6f, 69.0f, FloorY(1), FloorY(2));
            // ふさがる所（1階：1階→2階の段の下／2階：2階→3階の段と、1階→2階の段の穴／3階：2階→3階の段の穴）
            Rect aW = SR(5.3f, SBand0 - 0.1f, 9.3f, SBack), aE = SR(60.7f, SBand0 - 0.1f, 64.7f, SBack);
            Rect bW = SR(0f, SBand0 - 0.1f, 4.5f, SBack), bE = SR(65.5f, SBand0 - 0.1f, SLen, SBack);
            blockers[0].Add(aW); blockers[0].Add(aE);
            blockers[1].Add(aW); blockers[1].Add(aE); blockers[1].Add(bW); blockers[1].Add(bE);
            blockers[2].Add(bW); blockers[2].Add(bE);
            // 穴のまわりの手すり
            Rail(1, SP(5.4f, SBand0), SP(9.2f, SBand0));
            Rail(1, SP(60.8f, SBand0), SP(64.6f, SBand0));
            Rail(1, SP(9.2f, SBand0), SP(9.2f, SBack - 0.15f));
            Rail(1, SP(60.8f, SBand0), SP(60.8f, SBack - 0.15f));
            Rail(2, SP(1.0f, SBand0), SP(4.4f, SBand0));
            Rail(2, SP(65.6f, SBand0), SP(69.0f, SBand0));
            Rail(2, SP(4.4f, SBand0), SP(4.4f, SBack - 0.15f));
            Rail(2, SP(65.6f, SBand0), SP(65.6f, SBack - 0.15f));
            // 上り下りの点（上りきった所・下りきった所は、穴の上を通らないように床のある側に置く）
            Vector3 lane = new Vector3(-0.4f, 0f, 0f); // 上りは南（壁）側
            stairs.Add(new Stair { lowerFloor = 0, bottomApproach = S3(9.5f, 42.4f, 0f), bottom = S3(9.2f, SBand, 0f), top = S3(5.4f, SBand, FloorY(1)), topApproach = S3(4.9f, 42.4f, FloorY(1)), lane = lane });
            stairs.Add(new Stair { lowerFloor = 1, bottomApproach = S3(4.9f, 42.4f, FloorY(1)), bottom = S3(4.4f, SBand, FloorY(1)), top = S3(1.0f, SBand, FloorY(2)), topApproach = S3(0.7f, 42.4f, FloorY(2)), lane = lane });
            stairs.Add(new Stair { lowerFloor = 0, bottomApproach = S3(60.5f, 42.4f, 0f), bottom = S3(60.8f, SBand, 0f), top = S3(64.6f, SBand, FloorY(1)), topApproach = S3(65.1f, 42.4f, FloorY(1)), lane = lane });
            stairs.Add(new Stair { lowerFloor = 1, bottomApproach = S3(65.1f, 42.4f, FloorY(1)), bottom = S3(65.6f, SBand, FloorY(1)), top = S3(69.0f, SBand, FloorY(2)), topApproach = S3(69.3f, 42.4f, FloorY(2)), lane = lane });
        }

        // ================= 外廊下（教室の棟の北側。1階と2階。屋根あり・壁なし） =================
        void BuildSchoolVeranda()
        {
            Box("Veranda_Path", S3(SLen / 2f, (SVer0 + SVer1) / 2f, -0.02f), new Vector3((SVer1 - SVer0) * SK, 0.04f, SLen * SK), flagstone);
            Slab("Veranda_Floor2", 1, SR(0, SVer0, SLen, SVer1), woodFloor);
            Box("Veranda_Roof", S3(SLen / 2f, (SVer0 + SVer1) / 2f, FloorY(2) + 0.05f), new Vector3((SVer1 - SVer0) * SK + 0.3f, 0.1f, SLen * SK), mortarDark);
            if (Resources.Load<GameObject>("Furniture/Furn_RoofEdge") != null) // 本番の部品の軒（外廊下の屋根の外側）
            {
                int ne = Mathf.RoundToInt(SLen * SK / 2f); float le = SLen * SK / ne;
                Vector3 e0 = S3(0f, SVer0, FloorY(2) - 0.1f) + new Vector3(0.15f - 0.3f, 0f, 0f);
                for (int i = 0; i < ne; i++) PlaceFurn("Furn_RoofEdge", null, e0 + new Vector3(0f, 0f, -(i + 0.5f) * le), 90f, false, new Vector3(le / 2f, 1f, 1f));
            }
            // 柱（中庭の側）と2階の手すり
            for (float sx = 18f; sx <= 52.01f; sx += 4.25f)
            {
                if (PlaceFurn("Furn_Pillar", null, S3(sx, SVer0 + 0.15f, 0f), 0f, false, new Vector3(0.25f / 0.4f, FloorH * 2f / 4.5f, 0.25f / 0.4f)) == null)
                    Box("Veranda_Post", S3(sx, SVer0 + 0.15f, FloorH), new Vector3(0.25f, FloorH * 2f, 0.25f), woodDark);
                blockers[0].Add(SR(sx - 0.2f, SVer0, sx + 0.2f, SVer0 + 0.35f));
                blockers[1].Add(SR(sx - 0.2f, SVer0, sx + 0.2f, SVer0 + 0.35f));
            }
            Rail(1, SP(18f, SVer0 + 0.05f), SP(52f, SVer0 + 0.05f));
            Rail(1, SP(SLen - 0.05f, SVer0), SP(SLen - 0.05f, SVer1)); // 2階の東のはし
            for (int i = 0; i < 4; i++) Lamp(S3(9f + i * 17f, 24f, FloorY(1) + 2.4f), 1.4f, 6f);
            Label(S3(26f, 24f, 2.0f), "外廊下");
            Label(S3(26f, 24f, FloorY(1) + 2.0f), "外廊下（寮の渡り廊下から）");
            AddArea("外廊下", SR(0, SVer0, SLen, SVer1), 0);
            AddArea("外廊下", SR(0, SVer0, SLen, SVer1), 1);
        }

        // ================= 体育館・大講堂（2階建ての高さ。2階に見下ろし席） =================
        void BuildSchoolHall(float sx0, float sx1, string name, string side)
        {
            float H = FloorY(2);
            float mid = (sx0 + sx1) / 2f;
            bool west = side == "西";
            Slab(name == "体育館" ? "Gym_Floor" : "Hall_Floor", 0, SR(sx0, 0, sx1, SVer0), woodFloor);
            var wNorth = Wall(SP(sx0, 0), SP(sx1, 0), 0f, H, WallT);
            var wOuter = west ? Wall(SP(sx0, 0), SP(sx0, SVer0), 0f, H, WallT) : Wall(SP(sx1, 0), SP(sx1, SVer0), 0f, H, WallT);
            // 中庭の側の壁：真ん中に扉
            if (west) Wall(SP(sx1, 0), SP(sx1, SVer0), 0f, H, WallT, SG(new Vector4(11.5f - 1f, 11.5f + 1f, 0f, 2.6f)));
            else Wall(SP(sx0, 0), SP(sx0, SVer0), 0f, H, WallT, SG(new Vector4(11.5f - 1f, 11.5f + 1f, 0f, 2.6f)));
            // 外廊下の側の壁：1階と2階の真ん中に入口（穴が上下に重なるので、1階分ずつ作る）
            Wall(SP(sx0, SVer0), SP(sx1, SVer0), 0f, FloorH, WallT, SG(new Vector4(mid - sx0 - 1.2f, mid - sx0 + 1.2f, 0f, 2.6f)));
            Wall(SP(sx0, SVer0), SP(sx1, SVer0), FloorH, H, WallT, SG(new Vector4(mid - sx0 - 0.8f, mid - sx0 + 0.8f, FloorY(1), FloorY(1) + 2.3f)));
            for (int i = 0; i < 3; i++)
            {
                Window(wNorth, S3(sx0 + 4f + i * 5f, 0, 0f), SDir(0, 1), 2.6f, 1.4f, 2.4f);
                Window(wOuter, S3(west ? sx0 : sx1, 5f + i * 6.5f, 0f), SDir(west ? 1 : -1, 0), 2.6f, 1.4f, 2.4f);
            }
            Box(name + "_Roof", S3(mid, SVer0 / 2f, H + 0.05f), new Vector3(SVer0 * SK + 0.3f, 0.12f, (sx1 - sx0) * SK + 0.3f), mortarDark);
            // 2階の見下ろし席（外廊下の側）
            Slab(name + "_Balcony", 1, SR(sx0 + 0.3f, 20.5f, sx1 - 0.3f, SVer0), woodDark);
            Rail(1, SP(sx0 + 0.3f, 20.5f), SP(sx1 - 0.3f, 20.5f));
            if (name == "体育館")
            {
                // 床の線（剣術の試合場）
                Box("Gym_Line", S3(mid, 9f, 0.006f), new Vector3(10f * SK, 0.01f, 0.08f), fabricWhite);
                Box("Gym_Line", S3(mid, 9f, 0.006f), new Vector3(0.08f, 0.01f, 10f * SK), fabricWhite);
            }
            else
            {
                // 壇（北の奥）
                Box("Hall_Stage", S3(mid, 2.2f, 0.3f), new Vector3(3.6f * SK, 0.6f, (sx1 - sx0 - 4f) * SK), woodMid);
                blockers[0].Add(SR(sx0 + 2f, 0.4f, sx1 - 2f, 4.0f));
            }
            for (int i = 0; i < 2; i++) Lamp(S3(mid + (i == 0 ? -4f : 4f), 10f, H - 0.8f), 4f, 13f);
            Label(S3(mid, 10f, 2.6f), name);
            AddArea(name, SR(sx0, 0, sx1, SVer0), 0);
            AddArea(name + "の2階席", SR(sx0, 20.5f, sx1, SVer0), 1);
            FurnishSchoolHall(sx0, sx1, name);
        }

        // ================= 本館の中庭（北に開く。噴水・石畳の小道・丸い休憩所） =================
        void BuildSchoolCourtyard()
        {
            float fx = 35f, fy = 11.5f;
            SPath(new Vector2(35f, SVer0), new Vector2(fx, fy), 1.6f);
            SPath(new Vector2(18f, fy), new Vector2(52f, fy), 1.6f);
            SPath(new Vector2(fx, fy), new Vector2(44.3f, 5.2f), 1.2f);
            // 噴水
            var c = S3(fx, fy, 0f);
            bool fnt = PlaceFurn("Furn_Fountain", null, c, 0f) != null; // 本番の家具（水はこのまま）
            if (!fnt) Cyl("Fountain_Rim", c + Vector3.up * 0.25f, new Vector3(7.2f, 0.25f, 7.2f), Quaternion.identity, mortarDark);
            Cyl("Fountain_Water", c + Vector3.up * 0.48f, new Vector3(6.5f, 0.02f, 6.5f), Quaternion.identity, waterMat);
            if (!fnt) Cyl("Fountain_Pillar", c + Vector3.up * 0.9f, new Vector3(0.5f, 0.9f, 0.5f), Quaternion.identity, mortarDark);
            if (!fnt) Cyl("Fountain_Bowl", c + Vector3.up * 1.8f, new Vector3(1.6f, 0.12f, 1.6f), Quaternion.identity, mortarDark);
            blockers[0].Add(new Rect(c.x - 3.7f, c.z - 3.7f, 7.4f, 7.4f));
            // 噴水や休憩所でくつろぐ場所は、授業のしくみを作るときに足す（今は寮から遠くへ行きすぎないように）
            // 丸い休憩所（壁なし。柱6本と丸い屋根、まわりにベンチ）
            var g = S3(46f, 3.6f, 0f);
            bool gz = PlaceFurn("Furn_Gazebo", null, g, 0f) != null; // 本番の家具（柱・屋根・机も入っている）
            if (!gz) Cyl("Gazebo_Floor", g + Vector3.up * 0.05f, new Vector3(5.4f, 0.05f, 5.4f), Quaternion.identity, flagstone);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                Vector3 p = g + new Vector3(Mathf.Cos(a) * 2.5f, 0f, Mathf.Sin(a) * 2.5f);
                if (!gz) Cyl("Gazebo_Post", p + Vector3.up * 1.3f, new Vector3(0.18f, 1.3f, 0.18f), Quaternion.identity, woodDark);
                blockers[0].Add(new Rect(p.x - 0.15f, p.z - 0.15f, 0.3f, 0.3f));
            }
            if (!gz) ceilingRenderers.Add(Cyl("Gazebo_Roof", g + Vector3.up * 2.7f, new Vector3(6.0f, 0.1f, 6.0f), Quaternion.identity, fabricRed).GetComponent<Renderer>());
            if (!gz) Cyl("Gazebo_Table", g + Vector3.up * 0.4f, new Vector3(1.0f, 0.4f, 1.0f), Quaternion.identity, woodMid);
            blockers[0].Add(new Rect(g.x - 0.55f, g.z - 0.55f, 1.1f, 1.1f));
            Lamp(g + Vector3.up * 2.3f, 1.4f, 5f);
            // 木（すみ）
            Tree(S3(21.5f, 3f, 0f), 3.6f);
            Tree(S3(48.5f, 20f, 0f), 3.4f);
            Tree(S3(21.5f, 20f, 0f), 3.2f);
            Label(S3(26f, 17f, 2.2f), "本館の中庭");
            Label(g + Vector3.up * 2.2f, "休憩所");
            AddArea("本館の中庭", SR(18f, 0, 52f, SVer0), 0);
        }

        void SPath(Vector2 a, Vector2 b, float w)
        {
            Vector3 pa = S3(a.x, a.y, 0f), pb = S3(b.x, b.y, 0f);
            Vector3 d = pb - pa;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            Box("School_YardPath", (pa + pb) / 2f + Vector3.up * -0.004f, new Vector3(w * SK, 0.04f, d.magnitude), flagstone, null, yaw);
        }

        // ================= 歩ける所 =================
        void OpenSchoolNav(int f)
        {
            var n = navs[f];
            n.Open(SIn(0, SCor0, SLen, SCor1));                       // 大廊下
            foreach (var r in SNorth[f])
            {
                if (r.kind == SKind.Pass) { n.Open(SR(r.x0 + 0.25f, SVer1 - 0.6f, r.x1 - 0.25f, SCor0 + 0.5f)); continue; }
                n.Open(SIn(r.x0, 25, r.x1, SCor0));
                n.Open(SDoorNav(r.Mid, SCor0, true));
            }
            foreach (var r in SSouth[f])
            {
                if (r.kind == SKind.Stair) { n.Open(SR(r.x0 + 0.25f, SCor0 + 0.5f, r.x1 - 0.25f, SBack - 0.25f)); continue; }
                n.Open(SIn(r.x0, SCor1, r.x1, SBack));
                if (r.kind == SKind.Hall) n.Open(SR(r.Mid - 1.75f, SCor1 - 0.5f, r.Mid + 1.75f, SCor1 + 0.5f));
                else n.Open(SDoorNav(r.Mid, SCor1, true));
            }
            if (f <= 1)
            {
                n.Open(SIn(0, SVer0, SLen, SVer1));                    // 外廊下
                n.Open(SR(-0.6f, SVer0 + 0.25f, 0.5f, SVer1 - 0.25f));  // 外廊下の西のはし（寮の前庭・渡り廊下へ）
            }
            if (f == 0)
            {
                n.Open(SR(-0.7f, 34.9f, 0.5f, 36.1f));                 // 裏口（寮の前庭へ）
                n.Open(SR(33.9f, SBack - 0.5f, 36.1f, SBack + 0.6f));   // 玄関
                n.Open(SR(31.25f, SBack + 0.25f, 38.75f, SBack + 6f));  // 正門への道
                n.Open(SIn(0, 0, 18f, SVer0));                         // 体育館
                n.Open(SIn(52f, 0, SLen, SVer0));                      // 大講堂
                n.Open(SIn(18f, 0, 52f, SVer0));                       // 中庭
                n.Open(SR(18.25f, SVer0 - 0.5f, 51.75f, SVer0 + 0.5f)); // 中庭と外廊下のあいだ
                n.Open(SDoorNav(9f, SVer0, true, 2.4f));                // 体育館の入口
                n.Open(SDoorNav(61f, SVer0, true, 2.4f));               // 大講堂の入口
                n.Open(SDoorNav(18f, 11.5f, false, 2f));                // 体育館→中庭
                n.Open(SDoorNav(52f, 11.5f, false, 2f));                // 大講堂→中庭
            }
            if (f == 1)
            {
                n.Open(SIn(0.3f, 20.5f, 17.7f, SVer0));                // 体育館の2階席
                n.Open(SIn(52.3f, 20.5f, 69.7f, SVer0));               // 大講堂の2階席
                n.Open(SDoorNav(9f, SVer0, true, 1.6f));
                n.Open(SDoorNav(61f, SVer0, true, 1.6f));
            }
        }
    }
}
