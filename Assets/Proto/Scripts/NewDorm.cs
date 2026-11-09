using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 新しい寮（学園の作り直し第5案。famuru 2026-10-31 決定）。31×24m・3階建て・1階の高さ 3.5m。
    /// ・2026-11-05 からふだんはこの寮を組む。Logs\old_campus_play.txt を置くと前の寮（前の学園）で動く
    /// ・向きは今の寮と同じ：−z が学校の側（入口）、＋z が奥（食堂・浴場）、＋x が北の棟、−x が南の棟
    /// ・2026-11-01：組み立てたあと x＝0 を軸に左右反転する（MirrorDorm。配置図＝famuru 決定と同じ向きにするため）。反転したあとは −x が北の棟、＋x が南の棟
    /// ・学園の外側（島・崖・城壁・正門・ほかの寮・中庭の渡り廊下・森）は CampusPreview.cs（配置図の座標→CampusPlan.G）
    /// ・間取り：真ん中に談話室（3階まで吹き抜け）、北と南に個室5部屋×3階＝30、手前（−z）に入口・先生の部屋・倉庫（2階：篤の部屋・小さな談話室・入口の通路、3階：トレーニング部屋・シャワー）、
    ///   奥（＋z）に浴場・脱衣所・食堂（机6）・台所（2階：娯楽室・トイレ、3階：図書室・勉強部屋）。北西の大きな塔の中に先生の個室3つ、南東のすみに露天風呂
    /// 2026-11-01 家具のチャット
    /// </summary>
    public partial class LoungeWorld
    {
        public static bool NewLayout { get; private set; }
        // 2026-11-05 famuru「マップを新しいものに入れ替えていい？」→ ふだんは新しい学園。Logs\old_campus_play.txt を置くと前の学園で動く（前の学園の組み立ては残してある）
        static string OldCampusFlag { get { return Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/old_campus_play.txt")); } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ApplyLayout()
        {
            NewLayout = !File.Exists(OldCampusFlag);
            if (NewLayout)
            {
                HalfW = 5.2f; FloorH = 3.5f;
                WingIn = 5.2f; WingCorr = 7.2f; WingOut = 12f; RoomW = 3f; RoomDepth = 4.8f;
                FrontOut = -15.5f; FrontZ = -9.5f; BackZ = 5.5f; BackOut = 15.5f;
                VoidZ0 = -7.5f; VoidZ1 = 1.7f; BandZ0 = 3.7f; BandZ1 = 5.5f; BandZ = 4.6f;
                StairA0 = 0.4f; StairA1 = 5.2f; StairB0 = -0.4f; StairB1 = -5.2f;
                KitchenX0 = 7.5f; KitchenDoorZ = 14.3f;
                PassX0 = 3f; PassX1 = 5.2f;
                Floor2Y = 3.5f; Floor3Y = 7f; RoofY = 14f; // 屋上の階＝北東の塔のてっぺんの天文台（寮の屋根より上。famuru 2026-11-08）
                LoungeCenter = new Vector3(2.9f, 0f, -1.2f);
                BirthdaySpot = new Vector3(-2.6f, 0f, 1.0f);
                FireplaceSpot = new Vector3(-2.6f, 0f, 2.9f);
                DiningCenter = new Vector3(2.0f, 0f, 10.5f);
                LoungeWanderRect = new Rect(-4.7f, -15f, 9.4f, 19f);
                // 学校（SchoolBuilder の設計図を 1m＝1 で、寮の手前（−z）に置く。設計図の北＝プログラムの＋x＝配置図の南。中庭が南に開く）
                SXN = 32f; SZ0 = -26.5f; SK = 1f;
                SchoolCorridor2F = new Vector3(32f - 35.5f, Floor2Y, -26.5f - 35f);
            }
            else
            {
                HalfW = 8.6f; FloorH = 4.5f;
                WingIn = 8.6f; WingCorr = 11f; WingOut = 17f; RoomW = 3.6f; RoomDepth = 6f;
                FrontOut = -18f; FrontZ = -9f; BackZ = 9f; BackOut = 18f;
                VoidZ0 = -7f; VoidZ1 = 5.2f; BandZ0 = 7.2f; BandZ1 = 9f; BandZ = 8.1f;
                StairA0 = 0.6f; StairA1 = 7.6f; StairB0 = -0.6f; StairB1 = -7.6f;
                KitchenX0 = 11f; KitchenDoorZ = 16.8f;
                PassX0 = 6f; PassX1 = 8.6f;
                Floor2Y = 4.5f; Floor3Y = 9f; RoofY = 13.5f;
                LoungeCenter = new Vector3(-5.5f, 0f, 3.5f);
                BirthdaySpot = new Vector3(-5.5f, 0f, 6.0f);
                FireplaceSpot = new Vector3(-5.5f, 0f, 7.0f);
                DiningCenter = new Vector3(4.3f, 0f, 13.5f);
                LoungeWanderRect = new Rect(-7.5f, -16f, 15f, 21f);
                SXN = 43.3f; SZ0 = -24.5f; SK = 1.5f;
                SchoolCorridor2F = new Vector3(SXN - SK * 35.5f, Floor2Y, SZ0 - SK * 35f);
            }
        }

        // 北西の大きな塔（先生の個室3つ）。中心は建物の角
        static readonly Vector2 TeacherTowerC = new Vector2(12f, -15.5f);
        const float TeacherTowerR = 4.5f, TowerR = 4.5f; // 3本とも半径 4.5m（famuru 2026-11-08）
        /// <summary>新しい学園の場所の注目点（DormPlaces.FocusOf が使う）。</summary>
        static readonly Dictionary<string, Vector3> NewFocus = new Dictionary<string, Vector3>();

        void BuildNew()
        {
            root = new GameObject("Dorm").transform;
            CreateMaterials();
            SetupSun();
            // 地面は学園の島（CampusPreview.cs）。寮の四角い地面は置かない
            NSlabs();
            NLounge();
            NStairs();
            BuildAtriumRailsNew();
            BuildWing(-1, 0);   // rooms[0〜4]
            NFRoom();           // rooms[5] 篤の部屋（2階）
            BuildWing(1, 0);    // rooms[6〜10]
            for (int f = 1; f <= 2; f++) { BuildWing(-1, f); BuildWing(1, f); } // rooms[11〜30]
            NTeacherTower();    // rooms[31〜33] 先生
            NBackRooms();
            NFrontRooms();
            NOuterWalls();
            NTowers();
            NTowerRooms();      // 先生の塔の2・3階・南西の塔の1〜3階を、となりの部屋とつなぐ（2026-11-08）
            NFoodTower();       // 北東の塔：食料庫と屋上の天文台への階段
            NSpa();
            NCourtyard();       // 中庭（寮の裏）
            NFrontYardAndBridge();
            NRoof();
            SkinAll();          // 寮の外壁の石の部品（依頼28。反転の前に）
            MirrorDorm();       // 配置図と同じ向きに（左右反転）
            NewFocus.Clear();   // 場所の注目点（FocusOf。反転したあとの座標）
            NewFocus[Observatory] = new Vector3(-14.2f, FloorY(3), 14.6f);
            NewFocus[Kitchen] = new Vector3(-9.75f, 0f, 10.5f);
            NewFocus[Library] = new Vector3(-4f, FloorY(2), 10.5f);
            NewFocus[Courtyard] = new Vector3(-NCGateX, 0f, 24.6f);
            NSchool();
            NSchoolOutside();   // 学校の屋根・塔
            SkinAll();          // 本館の外壁の石の部品（依頼28）
            NPlacesOutside();   // 温室と工房（本館の北の離れ）
            NShore();           // プールと浜辺の席・見どころ・場所（CampusShore.cs。2026-11-08 家具のチャット）
            FinishAreas();
            NNav();
            ApplyPlaces();
            exitPoint = new Vector3(-(PassX0 + PassX1) / 2f, FloorY(1), SchoolWestZ - 0.6f);
            entrancePoint = new Vector3(-(PassX0 + PassX1) / 2f, FloorY(1), FrontOut + 2.0f);
            Debug.Log("[新しい寮] 組み終わり：部屋 " + rooms.Count + "・席 " + seats.Count + "・見どころ " + pois.Count);
        }

        // ================= 床 =================
        void NSlabs()
        {
            Slab("Floor_Lounge", 0, Rect.MinMaxRect(-WingIn, FrontOut, WingIn, BackZ), flagstone);
            for (int s = -1; s <= 1; s += 2)
            {
                Slab("Floor_Corridor", 0, MinMax(s * WingIn, FrontZ, s * WingCorr, BackZ), woodFloor);
                Slab("Floor_Rooms", 0, MinMax(s * WingCorr, FrontZ, s * WingOut, BackZ), woodFloor);
            }
            Slab("Floor_Dining", 0, Rect.MinMaxRect(-2.5f, BackZ, KitchenX0, BackOut), woodFloor);
            Slab("Floor_Kitchen", 0, Rect.MinMaxRect(KitchenX0, BackZ, WingOut, BackOut), flagstone);
            Slab("Floor_Dressing", 0, Rect.MinMaxRect(-5f, BackZ, -2.5f, BackOut), woodFloor);
            Slab("Floor_Bath", 0, Rect.MinMaxRect(-WingOut, BackZ, -5f, BackOut), tileMat);
            Slab("Floor_Teacher", 0, Rect.MinMaxRect(WingIn, FrontOut, WingOut, FrontZ), woodFloor);
            Slab("Floor_Storage", 0, Rect.MinMaxRect(-WingOut, FrontOut, -WingIn, FrontZ), flagstone);
            for (int f = 1; f <= 2; f++)
            {
                for (int s = -1; s <= 1; s += 2) Slab("Floor_Wing", f, MinMax(s * WingIn, FrontZ, s * WingOut, BackZ), woodFloor);
                Slab("Floor_Back", f, Rect.MinMaxRect(-WingOut, BackZ, WingOut, BackOut), woodFloor);
                Slab("Floor_Front", f, Rect.MinMaxRect(-WingOut, FrontOut, WingOut, FrontZ), woodFloor);
                Slab("Floor_GalleryF", f, Rect.MinMaxRect(-WingIn, FrontZ, WingIn, VoidZ0), woodFloor);
                Slab("Floor_GalleryB", f, Rect.MinMaxRect(-WingIn, VoidZ1, WingIn, BandZ0), woodFloor);
                if (f == 1)
                {
                    Slab("Floor_Band", f, Rect.MinMaxRect(-WingIn, BandZ0, StairA0, BandZ1), woodFloor); // 1階→2階の段（x 0.4〜5.2）の上はあける
                }
                else
                {
                    Slab("Floor_Band", f, Rect.MinMaxRect(StairB0, BandZ0, WingIn, BandZ1), woodFloor); // 2階→3階の段（x −5.2〜−0.4）の上はあける
                }
            }
            {
                // 3階の天井（上に屋根）。屋上の階（天文台）を 14m にしたので、軒の高さ（10.5m）に直接置く（2026-11-08）
                var rf = Box("Roof_Floor", new Vector3(0f, EaveY - 0.075f, (FrontOut + BackOut) / 2f), new Vector3(WingOut * 2f, 0.15f, BackOut - FrontOut), mortarDark);
                slabRenderers.Add(new KeyValuePair<float, Renderer>(EaveY, rf.GetComponent<Renderer>()));
            }
        }

        // ================= 談話室 =================
        void NLounge()
        {
            var wallN = WallF(0, new Vector2(WingIn, FrontOut), new Vector2(WingIn, FrontZ));
            var wallS = WallF(0, new Vector2(-WingIn, FrontOut), new Vector2(-WingIn, FrontZ));
            float[] pz = { VoidZ0, VoidZ1, FrontZ };
            for (int s = -1; s <= 1; s += 2)
                foreach (float z in pz)
                {
                    float top = z == FrontZ ? FloorH : RoomH;
                    if (PlaceFurn("Furn_Pillar", null, new Vector3(s * WingIn, 0f, z), 0f, false, new Vector3(0.35f / 0.4f, top / 4.5f, 0.35f / 0.4f)) == null)
                        Box("Pillar", new Vector3(s * WingIn, top / 2f, z), new Vector3(0.35f, top, 0.35f), mortarDark);
                    blockers[0].Add(new Rect(s * WingIn - 0.2f, z - 0.2f, 0.4f, 0.4f));
                }
            if (PlaceFurn("Furn_LoungeRug", null, new Vector3(LoungeCenter.x, 0f, LoungeCenter.z), 0f, false, new Vector3(0.8f, 1f, 0.8f)) == null)
                Box("Rug", new Vector3(LoungeCenter.x, 0.012f, LoungeCenter.z), new Vector3(4.4f, 0.02f, 3.6f), rugMat);
            Chandelier(new Vector3(0f, 6.0f, -3.0f), 5f, 11f);
            Chandelier(new Vector3(0f, 2.4f, -12.5f), 3.5f, 8f);
            Sconce(wallN, new Vector3(WingIn - 0.15f, 2.2f, -12.5f), Vector3.left, 2.2f);
            Sconce(wallS, new Vector3(-WingIn + 0.15f, 2.2f, -12.5f), Vector3.right, 2.2f);

            // 暖炉（奥の壁ぎわ、階段の左）
            float fx = FireplaceSpot.x, fz = BackZ - 0.55f;
            bool fpModel = PlaceFurn("Furn_Fireplace", null, new Vector3(fx, 0f, fz + 0.05f), 180f, false, new Vector3(0.85f, 0.85f, 1f)) != null;
            if (!fpModel)
            {
                Box("Hearth", new Vector3(fx, 0.15f, fz), new Vector3(3.0f, 0.3f, 1.0f), mortarDark);
                Box("Fireplace_Mantel", new Vector3(fx, 2.5f, fz + 0.1f), new Vector3(3.2f, 0.3f, 0.9f), woodDark);
            }
            for (int i = 0; i < 5; i++)
            {
                var f = Box("Flame" + i, new Vector3(fx - 0.45f + i * 0.22f, 0.6f, fz + 0.1f), new Vector3(0.16f, 0.45f, 0.16f), i % 2 == 0 ? glowFire : glowEmber);
                f.AddComponent<FlameWobble>().seed = i * 1.7f;
            }
            var fire = Lamp(new Vector3(fx, 1.0f, fz - 0.55f), 8f, 10f, true, 0.2f);
            fire.color = new Color(1f, 0.6f, 0.3f);
            fire.GetComponent<Flicker>().isFire = true;
            blockers[0].Add(new Rect(fx - 1.6f, fz - 0.55f, 3.2f, 1.1f));
            Sofa("暖炉の前の椅子", "談話室", new Vector3(fx - 1.2f, 0f, fz - 1.5f), 0f, 1.0f, 1, fabricGreen);
            Sofa("暖炉の前の椅子", "談話室", new Vector3(fx + 1.2f, 0f, fz - 1.5f), 0f, 1.0f, 1, fabricGreen);
            pois.Add(MakePoi("暖炉にあたっている", new Vector3(fx, 0.8f, fz + 0.15f), new Vector3(fx - 0.5f, 0f, fz - 0.9f), new Vector3(fx + 0.5f, 0f, fz - 0.9f)));

            // ソファで囲む低いテーブル（右の吹き抜けの下）
            Vector3 lc = LoungeCenter;
            if (PlaceFurn("Furn_LowTable", null, new Vector3(lc.x, 0f, lc.z), 0f) == null)
                Box("LowTable", new Vector3(lc.x, 0.22f, lc.z), new Vector3(1.4f, 0.08f, 0.8f), woodMid);
            blockers[0].Add(new Rect(lc.x - 0.75f, lc.z - 0.45f, 1.5f, 0.9f));
            Sofa("ソファ", "談話室", new Vector3(lc.x, 0f, lc.z - 1.45f), 0f, 2.4f, 3, fabricRed);
            Sofa("ソファ", "談話室", new Vector3(lc.x - 1.75f, 0f, lc.z + 0.1f), 90f, 2.1f, 3, fabricRed);
            Sofa("ソファ", "談話室", new Vector3(lc.x + 1.75f, 0f, lc.z + 0.1f), -90f, 2.1f, 3, fabricRed);

            // 大きなテーブル（左の吹き抜けの下）
            Vector3 bt = new Vector3(-2.7f, 0f, -3.6f);
            if (PlaceFurn("Furn_BigTable", null, bt, 0f) == null)
                Box("BigTable_Top", new Vector3(bt.x, 0.75f, bt.z), new Vector3(2.4f, 0.08f, 1.0f), woodMid);
            if (PlaceFurn("Furn_CandleStand", null, new Vector3(bt.x, 0.79f, bt.z), 0f) == null)
                Box("Candle", new Vector3(bt.x, 0.94f, bt.z), new Vector3(0.06f, 0.22f, 0.06f), glowLamp);
            blockers[0].Add(new Rect(bt.x - 1.2f, bt.z - 0.5f, 2.4f, 1.0f));
            for (int i = 0; i < 3; i++)
            {
                float x = bt.x - 0.8f + i * 0.8f;
                Chair("大きなテーブル", "談話室", new Vector3(x, 0f, bt.z + 0.8f), 180f);
                Chair("大きなテーブル", "談話室", new Vector3(x, 0f, bt.z - 0.8f), 0f);
            }
            // 魔法の練習場所
            Vector3 mc = new Vector3(-2.7f, 0f, -0.6f);
            // 魔法陣の家具（直径2m の物を 1.8m に。2026-11-04 famuru「青い丸を魔法陣の家具に置きかえる」）。無ければ前の光る円
            if (PlaceFurn("Furn_MagicCircle", null, new Vector3(mc.x, 0.006f, mc.z), 0f, false, new Vector3(0.9f, 1f, 0.9f)) == null)
                Cyl("MagicCircle", new Vector3(mc.x, 0.015f, mc.z), new Vector3(1.6f, 0.004f, 1.6f), Quaternion.identity, ProtoArt.Glow(new Color(0.4f, 0.55f, 1f), 0.6f));
            var magic = MakePoi("魔法の練習をしている", new Vector3(mc.x, 1.2f, mc.z), new Vector3(mc.x - 0.5f, 0f, mc.z + 0.4f), new Vector3(mc.x + 0.5f, 0f, mc.z + 0.5f));
            magic.hobby = (int)SimCharacter.HobbyKind.Magic;
            pois.Add(magic);

            // 入口のホール（手前。天井は2階まで）：半円の出窓のベンチ・読書の椅子・本棚・おやつワゴン・掲示板
            float wz = FrontOut + 0.6f;
            Bench(new Vector3(-2.6f, 0f, wz + 0.2f), 0f);
            Bench(new Vector3(-1.0f, 0f, wz + 0.2f), 0f);
            Sofa("読書の椅子", "談話室", new Vector3(-4.2f, 0f, -12.2f), 90f, 1.0f, 1, fabricGreen);
            Sofa("読書の椅子", "談話室", new Vector3(-4.2f, 0f, -13.6f), 90f, 1.0f, 1, fabricGreen);
            Bookshelf(new Vector3(WingIn - 0.3f, 0f, -12.5f), 2.4f, 90f, 2.4f);
            if (PlaceFurn("Furn_NoticeBoard", null, new Vector3(4.0f, 1.15f, FrontOut + 0.175f), 0f) == null)
                Box("NoticeBoard", new Vector3(4.0f, 1.6f, FrontOut + 0.2f), new Vector3(1.2f, 0.9f, 0.05f), woodMid);
            pois.Add(MakePoi("本棚の本を眺めている", new Vector3(WingIn, 1.4f, -12.5f), new Vector3(WingIn - 1.2f, 0f, -13.0f), new Vector3(WingIn - 1.15f, 0f, -12.0f)));
            pois.Add(MakePoi("窓の外を眺めている", new Vector3(-1.8f, 2f, FrontOut), new Vector3(-1.8f, 0f, FrontOut + 1.6f)));
            float sx = 3.6f, sz = FrontZ - 0.6f;
            if (PlaceFurn("Furn_SnackWagon", null, new Vector3(sx, 0f, sz), 180f) == null)
                Box("SnackTrolley", new Vector3(sx, 0.4f, sz), new Vector3(0.8f, 0.8f, 0.5f), woodMid);
            blockers[0].Add(new Rect(sx - 0.4f, sz - 0.25f, 0.8f, 0.5f));
            var snackPoi = MakePoi("おやつを取っている", new Vector3(sx, 0.8f, sz), new Vector3(sx, 0f, sz - 0.65f), new Vector3(sx - 0.85f, 0f, sz - 0.05f));
            snackPoi.snack = true;
            pois.Add(snackPoi);
            Label(new Vector3(sx, 1.4f, sz), "おやつワゴン");
            Label(new Vector3(0f, 2.6f, -4.5f), "談話室");
        }

        // ================= 階段（吹き抜けの奥の帯） =================
        void NStairs()
        {
            Flight("StairA", StairA0, StairA1, 0f, FloorY(1));          // 1階 → 2階（北へ）
            Flight("StairB", StairB0, StairB1, FloorY(1), FloorY(2));    // 2階 → 3階（南へ）
            blockers[0].Add(Rect.MinMaxRect(StairA0 - 0.1f, BandZ0 - 0.1f, StairA1, BandZ1));
            blockers[1].Add(Rect.MinMaxRect(StairB1, BandZ0 - 0.1f, StairB0 + 0.1f, BandZ1));
            Vector3 lane = new Vector3(0f, 0f, 0.35f);
            stairs.Add(new Stair
            {
                lowerFloor = 0,
                bottomApproach = new Vector3(StairA0 - 0.8f, 0f, BandZ),
                bottom = new Vector3(StairA0, 0f, BandZ),
                top = new Vector3(StairA1, FloorY(1), BandZ),
                topApproach = new Vector3(StairA1 + 0.6f, FloorY(1), BandZ),
                lane = lane
            });
            stairs.Add(new Stair
            {
                lowerFloor = 1,
                bottomApproach = new Vector3(0f, FloorY(1), BandZ),
                bottom = new Vector3(StairB0, FloorY(1), BandZ),
                top = new Vector3(StairB1, FloorY(2), BandZ),
                topApproach = new Vector3(StairB1 - 0.6f, FloorY(2), BandZ),
                lane = -lane
            });
            Label(new Vector3(StairA0 - 0.3f, 1.6f, BandZ - 0.6f), "階段（2階へ）");
            Label(new Vector3(0f, FloorY(1) + 1.8f, BandZ - 0.6f), "階段（3階へ）");
        }

        void BuildAtriumRailsNew()
        {
            for (int f = 1; f <= 2; f++)
            {
                Rail(f, new Vector2(-WingIn, VoidZ0), new Vector2(-WingIn, VoidZ1));
                Rail(f, new Vector2(WingIn, VoidZ0), new Vector2(WingIn, VoidZ1));
                Rail(f, new Vector2(-WingIn, VoidZ0), new Vector2(WingIn, VoidZ0));
                Rail(f, new Vector2(-WingIn, VoidZ1), new Vector2(WingIn, VoidZ1));
            }
            Rail(1, new Vector2(StairA0, BandZ0), new Vector2(StairA1, BandZ0));
            Rail(1, new Vector2(StairA0, BandZ0), new Vector2(StairA0, BandZ1));
            Rail(2, new Vector2(StairB1, BandZ0), new Vector2(StairB0, BandZ0));
            Rail(2, new Vector2(StairB0, BandZ0), new Vector2(StairB0, BandZ1));
        }

        // ================= 個室（3.0×4.8m。2人が寝転べるベッド） =================
        void FurnishRoomNew(RoomInfo room, int side, float cz)
        {
            float s = side, y = FloorY(room.floor);
            System.Func<float, float> X = u => s * (WingCorr + u); // 廊下の壁から外壁への距離 u（0〜4.8）
            float bW = 1.6f, bD = 2.1f;
            float bu0 = RoomDepth - 0.05f - bD, bu1 = RoomDepth - 0.05f;
            float bcx = X((bu0 + bu1) / 2f), bcz = cz - 0.6f;
            if (PlaceFurn("Furn_Bed", null, new Vector3(bcx, y, bcz), -90f * s, false, new Vector3(bW / 2.2f, 1f, 1f)) == null)
            {
                Box("Bed_Frame", new Vector3(bcx, y + 0.2f, bcz), new Vector3(bD, 0.4f, bW), woodDark);
                Box("Bed_Mattress", new Vector3(bcx, y + 0.47f, bcz), new Vector3(bD - 0.1f, 0.14f, bW - 0.1f), fabricWhite);
            }
            blockers[room.floor].Add(Rect.MinMaxRect(Mathf.Min(X(bu0), X(bu1)), bcz - bW / 2f, Mathf.Max(X(bu0), X(bu1)), bcz + bW / 2f));
            room.bed = new Seat
            {
                kind = SeatKind.Bed, label = "ベッド", area = room.name, room = room,
                position = new Vector3(bcx, y, bcz), forward = new Vector3(s, 0f, 0f),
                approach = new Vector3(X(bu0 - 0.45f), y, bcz)
            };
            seats.Add(room.bed);
            room.bedEdge = new Vector3(-s, 0f, 0f);
            room.bedAcross = Vector3.forward;
            room.bedSitAlong = Vector3.forward;
            room.bedEdgeDist = bD / 2f - 0.3f;
            room.bedLieGap = 0.7f;
            room.bedLieWidth = bW - 0.1f;
            room.bedSeatOffs = new[] { -0.5f, 0f, 0.5f };
            // 来客用の布団（床。扉の左）
            Vector3 fp = new Vector3(X(1.45f), y, cz - 1.05f);
            if (PlaceFurn("Furn_GuestFuton", null, fp, s > 0f ? 0f : 180f) == null)
                Box("GuestFuton", fp + Vector3.up * 0.05f, new Vector3(1.7f, 0.1f, 0.75f), fabricWhite);
            room.futon = new Seat { kind = SeatKind.Bed, floorSeat = true, label = "来客用の布団", area = room.name, position = fp, forward = new Vector3(s, 0f, 0f), approach = new Vector3(X(1.45f), y, cz - 0.3f), room = room };
            seats.Add(room.futon);
            // 小さなテーブルと座布団2枚（扉の右）
            Vector3 tp = new Vector3(X(2.0f), y, cz + 0.95f);
            if (PlaceFurn("Furn_LowTableSmall", null, tp, 0f) == null)
                Box("LowTable_Room", tp + Vector3.up * 0.15f, new Vector3(0.5f, 0.3f, 0.5f), woodMid);
            blockers[room.floor].Add(Rect.MinMaxRect(Mathf.Min(X(1.75f), X(2.25f)), cz + 0.7f, Mathf.Max(X(1.75f), X(2.25f)), cz + 1.2f));
            for (int k = 0; k < 2; k++)
            {
                Vector3 cp = new Vector3(X(k == 0 ? 1.25f : 2.75f), y, cz + 0.95f);
                if (PlaceFurn("Furn_Cushion", null, cp, 0f) == null)
                    Box("Cushion", cp + Vector3.up * 0.04f, new Vector3(0.5f, 0.08f, 0.5f), fabricGreen);
                var cu = new Seat { kind = SeatKind.Sit, floorSeat = true, label = "空き部屋", area = room.name, position = cp, forward = new Vector3(k == 0 ? s : -s, 0f, 0f), approach = new Vector3(cp.x, y, cz + 0.25f), room = room };
                room.cushions.Add(cu);
                seats.Add(cu);
            }
            // 机（外壁ぎわ）と椅子
            float deskX = X(RoomDepth - 0.35f), dz = cz + 0.95f;
            if (PlaceFurn("Furn_Desk", null, new Vector3(deskX, y, dz), -90f * s) == null)
                Box("Desk", new Vector3(deskX, y + 0.73f, dz), new Vector3(0.6f, 0.05f, 0.8f), woodMid);
            blockers[room.floor].Add(Rect.MinMaxRect(Mathf.Min(X(RoomDepth - 0.7f), X(RoomDepth)), dz - 0.42f, Mathf.Max(X(RoomDepth - 0.7f), X(RoomDepth)), dz + 0.42f));
            Vector3 chairP = new Vector3(X(RoomDepth - 0.97f), y, dz);
            PlaceFurn("Furn_Chair", null, chairP, Quaternion.LookRotation(new Vector3(deskX - chairP.x, 0f, 0f)).eulerAngles.y);
            room.desk = new Poi { label = "机で勉強している", lookAt = new Vector3(deskX, y + 0.8f, dz), slots = new[] { chairP }, occupants = new SimCharacter[1], seated = true };
            pois.Add(room.desk);
            // タンス（廊下側の壁ぎわ。扉の右）
            if (PlaceFurn("Furn_Wardrobe", null, new Vector3(X(0.4f), y, cz + 1.0f), 90f * s) == null)
                Box("Wardrobe", new Vector3(X(0.35f), y + 0.9f, cz + 1.0f), new Vector3(0.5f, 1.8f, 0.7f), woodDark);
            blockers[room.floor].Add(Rect.MinMaxRect(Mathf.Min(X(0.1f), X(0.6f)), cz + 0.65f, Mathf.Max(X(0.1f), X(0.6f)), cz + 1.35f));
            Lamp(new Vector3(X(2.4f), y + 2.4f, cz), 1.4f, 4f, false, 0.03f);
        }

        // ================= 篤の部屋（2階の手前。x −5.2〜3） =================
        void NFRoom()
        {
            int fl = 1;
            float x0 = -WingIn, x1 = PassX0, z0 = FrontOut, z1 = FrontZ, y = FloorY(fl);
            var room = new RoomInfo { name = "篤", floor = fl };
            float bw = 3.5f, bd = 2.2f;
            float bcx = x1 - 0.3f - bw / 2f, bz = z0 + 0.2f + bd / 2f;
            if (PlaceFurn("Furn_BedLarge", null, new Vector3(bcx, y, bz), 0f) == null)
                Box("Bed_Frame", new Vector3(bcx, y + 0.2f, bz), new Vector3(bw, 0.4f, bd), woodDark);
            blockers[fl].Add(Rect.MinMaxRect(bcx - bw / 2f, z0, bcx + bw / 2f, bz + bd / 2f));
            room.bed = new Seat { kind = SeatKind.Bed, label = "ベッド", area = room.name, position = new Vector3(bcx, y, bz), forward = new Vector3(0f, 0f, -1f), approach = new Vector3(bcx - 0.4f, y, bz + bd / 2f + 0.5f), room = room };
            seats.Add(room.bed);
            room.bedEdge = new Vector3(0f, 0f, 1f);
            room.bedAcross = Vector3.right;
            room.bedSitAlong = Vector3.right;
            room.bedSpread = 1.6f;
            room.bedEdgeDist = bd / 2f - 0.3f;
            room.bedLieGap = 0.70f;
            room.bedLieWidth = bw - 0.1f;
            room.bedSeatOffs = new[] { -1.45f, -0.87f, -0.29f, 0.29f, 0.87f, 1.45f };
            foreach (float o in room.bedSeatOffs)
                seats.Add(new Seat { kind = SeatKind.Sit, label = "篤の部屋のベッド", area = room.name, room = room, position = new Vector3(bcx + o, y, bz + bd / 2f - 0.2f), forward = new Vector3(0f, 0f, 1f), approach = new Vector3(bcx + o, y, bz + bd / 2f + 0.45f) });
            // 机（西の壁ぎわ）
            float dX = x0 + 0.4f, dZ = -12.6f;
            if (PlaceFurn("Furn_Desk", null, new Vector3(dX, y, dZ), 90f) == null)
                Box("Desk", new Vector3(dX, y + 0.73f, dZ), new Vector3(0.6f, 0.05f, 1.2f), woodMid);
            blockers[fl].Add(Rect.MinMaxRect(x0, dZ - 0.45f, dX + 0.35f, dZ + 0.45f));
            Vector3 chairQ = new Vector3(dX + 0.62f, y, dZ);
            PlaceFurn("Furn_Chair", null, chairQ, -90f);
            room.desk = new Poi { label = "机で勉強している", lookAt = new Vector3(dX, y + 0.8f, dZ), slots = new[] { chairQ }, occupants = new SimCharacter[1], seated = true };
            pois.Add(room.desk);
            // ソファ（奥の壁ぎわ）と低いテーブル・座布団
            Sofa("篤の部屋のソファ", room.name, new Vector3(-1.2f, y, z1 - 0.6f), 180f, 2.4f, 3, fabricRed);
            float tx = -1.2f, tz = -11.4f;
            if (PlaceFurn("Furn_LowTableSmall", null, new Vector3(tx, y, tz), 0f, false, new Vector3(1.8f, 1f, 1.2f)) == null)
                Box("LowTable_Room", new Vector3(tx, y + 0.15f, tz), new Vector3(0.9f, 0.3f, 0.6f), woodMid);
            blockers[fl].Add(Rect.MinMaxRect(tx - 0.45f, tz - 0.3f, tx + 0.45f, tz + 0.3f));
            for (int k = 0; k < 2; k++)
            {
                float cx = k == 0 ? tx - 0.85f : tx + 0.85f;
                if (PlaceFurn("Furn_Cushion", null, new Vector3(cx, y, tz), 0f) == null)
                    Box("Cushion", new Vector3(cx, y + 0.04f, tz), new Vector3(0.5f, 0.08f, 0.5f), fabricGreen);
                var cu = new Seat { kind = SeatKind.Sit, floorSeat = true, label = "空き部屋", area = room.name, position = new Vector3(cx, y, tz), forward = new Vector3(k == 0 ? 1f : -1f, 0f, 0f), approach = new Vector3(cx, y, tz + 0.65f), room = room };
                room.cushions.Add(cu);
                seats.Add(cu);
            }
            Vector3 fp = new Vector3(-3.6f, y, -10.4f);
            if (PlaceFurn("Furn_GuestFuton", null, fp, 0f) == null)
                Box("GuestFuton", fp + Vector3.up * 0.05f, new Vector3(1.7f, 0.1f, 0.75f), fabricWhite);
            room.futon = new Seat { kind = SeatKind.Bed, floorSeat = true, label = "来客用の布団", area = room.name, position = fp, forward = new Vector3(1f, 0f, 0f), approach = fp + new Vector3(0f, 0f, -0.65f), room = room };
            seats.Add(room.futon);
            if (PlaceFurn("Furn_Wardrobe", null, new Vector3(x1 - 0.45f, y, z1 - 1.0f), -90f) == null)
                Box("Wardrobe", new Vector3(x1 - 0.45f, y + 0.9f, z1 - 1.0f), new Vector3(0.6f, 1.8f, 0.8f), woodDark);
            blockers[fl].Add(Rect.MinMaxRect(x1 - 0.8f, z1 - 1.45f, x1 - 0.1f, z1 - 0.55f));
            Lamp(new Vector3(-1.1f, y + 2.4f, -12.5f), 2.0f, 7f, false, 0.03f);
            room.labelPos = new Vector3(-1.1f, y + 2.0f, -12.5f);
            room.rect = Rect.MinMaxRect(x0, z0, x1, z1);
            AddArea(room.AreaName, room.rect, fl);
            rooms.Add(room);
        }

        // ================= 北西の塔：先生の個室3つと共用の部屋 =================
        void NTeacherTower()
        {
            Vector2 c = TeacherTowerC;
            float y = 0f;
            // 共用の部屋（手前の帯の北。x 5.2〜12）：簡単な台所・ソファ・低いテーブル・来客用の布団
            var common = Rect.MinMaxRect(WingIn, FrontOut, WingOut, FrontZ);
            Label(new Vector3(8.6f, 2.2f, -12.5f), "先生の部屋（共用）");
            AddArea("先生の部屋", common, 0);
            Lamp(new Vector3(8.6f, 2.4f, -12.5f), 1.8f, 6f);
            PlaceFurn("Furn_KitchenCounter", null, new Vector3(WingOut - 0.4f, y, -10.6f), -90f);
            blockers[0].Add(Rect.MinMaxRect(WingOut - 0.75f, -11.6f, WingOut, -9.6f));
            Sofa("先生の部屋のソファ", "先生", new Vector3(7.6f, y, FrontZ - 0.6f), 180f, 2.4f, 3, fabricGreen);
            var shared = new List<Seat>();
            Vector3 tableP = new Vector3(7.6f, y, -11.8f);
            if (PlaceFurn("Furn_LowTableSmall", null, tableP, 0f) == null)
                Box("LowTable_Room", tableP + Vector3.up * 0.15f, new Vector3(0.5f, 0.3f, 0.5f), woodMid);
            blockers[0].Add(Rect.MinMaxRect(tableP.x - 0.25f, tableP.z - 0.25f, tableP.x + 0.25f, tableP.z + 0.25f));
            for (int k = 0; k < 2; k++)
            {
                Vector3 cp = tableP + new Vector3(k == 0 ? -0.75f : 0.75f, 0f, 0f);
                if (PlaceFurn("Furn_Cushion", null, cp, 0f) == null)
                    Box("Cushion", cp + Vector3.up * 0.04f, new Vector3(0.5f, 0.08f, 0.5f), fabricGreen);
                var cu = new Seat { kind = SeatKind.Sit, floorSeat = true, label = "先生の部屋", area = "先生", position = cp, forward = new Vector3(k == 0 ? 1f : -1f, 0f, 0f), approach = cp + new Vector3(0f, 0f, -0.65f) };
                shared.Add(cu);
                seats.Add(cu);
            }
            Vector3 futonP = new Vector3(6.3f, y, -14.2f);
            if (PlaceFurn("Furn_GuestFuton", null, futonP, 90f) == null)
                Box("GuestFuton", futonP + Vector3.up * 0.05f, new Vector3(0.75f, 0.1f, 1.7f), fabricWhite);
            var futon = new Seat { kind = SeatKind.Bed, floorSeat = true, label = "来客用の布団", area = "先生", position = futonP, forward = new Vector3(0f, 0f, 1f), approach = futonP + new Vector3(0.75f, 0f, 0f) };
            seats.Add(futon);

            // 塔の中の個室3つ（円を3つに分ける形を、扱いやすい四角で）
            // R2：手前の壁の外（x 8.6〜12, z −18.9〜−15.5）、R3：北の壁の外（x 12〜15.4, z −15.5〜−12.1）、R1：角（x 12〜15.4, z −18.9〜−15.5）
            var rects = new[] { Rect.MinMaxRect(8.6f, -18.9f, 12f, -15.5f), Rect.MinMaxRect(12f, -15.5f, 15.4f, -12.1f), Rect.MinMaxRect(12f, -18.9f, 15.4f, -15.5f) };
            // ベッド：位置・向き（頭板の側）、机：位置・向き
            var beds = new[] { new Vector3(10.9f, 0f, -17.75f), new Vector3(14.3f, 0f, -14.0f), new Vector3(14.25f, 0f, -17.35f) };
            float[] bedYaw = { 0f, -90f, -90f };
            var desks = new[] { new Vector3(9.6f, 0f, -18.2f), new Vector3(14.3f, 0f, -12.6f), new Vector3(12.75f, 0f, -18.35f) }; // 塔を半径 4.5m にしたので壁から出ないよう寄せた
            float[] deskYaw = { 0f, 180f, 0f };
            for (int i = 0; i < 3; i++)
            {
                var r = rects[i];
                var room = new RoomInfo { name = "先生", floor = 0, shared = true, rect = common, labelPos = new Vector3(r.center.x, 2.0f, r.center.y) };
                Vector3 bp = beds[i];
                bool alongZ = Mathf.Abs(bedYaw[i]) < 1f;   // 頭板が −z（R2）
                if (PlaceFurn("Furn_Bed", null, bp, bedYaw[i], false, new Vector3(1.6f / 2.2f, 1f, 1f)) == null)
                    Box("Bed_Frame", bp + Vector3.up * 0.2f, alongZ ? new Vector3(1.6f, 0.4f, 2.1f) : new Vector3(2.1f, 0.4f, 1.6f), woodDark);
                blockers[0].Add(alongZ ? Rect.MinMaxRect(bp.x - 0.8f, bp.z - 1.05f, bp.x + 0.8f, bp.z + 1.05f) : Rect.MinMaxRect(bp.x - 1.05f, bp.z - 0.8f, bp.x + 1.05f, bp.z + 0.8f));
                Vector3 fwd = alongZ ? new Vector3(0f, 0f, -1f) : new Vector3(1f, 0f, 0f);
                room.bed = new Seat { kind = SeatKind.Bed, label = "ベッド", area = room.name, room = room, position = bp, forward = fwd, approach = bp - fwd * 1.5f };
                seats.Add(room.bed);
                room.bedEdge = -fwd;
                room.bedAcross = alongZ ? Vector3.right : Vector3.forward;
                room.bedSitAlong = room.bedAcross;
                room.bedEdgeDist = 0.75f;
                room.bedLieGap = 0.7f;
                room.bedLieWidth = 1.5f;
                room.bedSeatOffs = new[] { -0.5f, 0f, 0.5f };
                foreach (var cu in shared) room.cushions.Add(cu);
                room.futon = futon;
                Vector3 dp = desks[i];
                if (PlaceFurn("Furn_Desk", null, dp, deskYaw[i]) == null)
                    Box("Desk", dp + Vector3.up * 0.73f, new Vector3(0.8f, 0.05f, 0.6f), woodMid);
                blockers[0].Add(Rect.MinMaxRect(dp.x - 0.42f, dp.z - 0.32f, dp.x + 0.42f, dp.z + 0.32f));
                Vector3 dfw = Quaternion.Euler(0f, deskYaw[i], 0f) * Vector3.forward;
                Vector3 chairP = dp + dfw * 0.62f;
                PlaceFurn("Furn_Chair", null, chairP, deskYaw[i] + 180f);
                room.desk = new Poi { label = "机で勉強している", lookAt = dp + Vector3.up * 0.8f, slots = new[] { chairP }, occupants = new SimCharacter[1], seated = true };
                pois.Add(room.desk);
                Lamp(new Vector3(r.center.x, 2.3f, r.center.y), 1.2f, 3.5f, false, 0.03f);
                AddArea("先生の部屋", r, 0);
                rooms.Add(room);
            }
            foreach (var cu in shared) cu.room = rooms[rooms.Count - 3];
            futon.room = rooms[rooms.Count - 3];
            // 塔の中の仕切り（R2｜R1、R3｜R1）。R1 へは R3 から
            WallF(0, new Vector2(12f, -15.5f - TeacherTowerR), new Vector2(12f, -15.5f));
            WallF(0, new Vector2(12f, -15.5f), new Vector2(12f + TeacherTowerR, -15.5f), DoorGap(0, 0.65f, 0.9f));
            // 塔の床（1〜3階）
            for (int f = 0; f <= 2; f++)
            {
                var d = Cyl("TeacherTower_Floor", new Vector3(c.x, FloorY(f) - 0.06f, c.y), new Vector3(TeacherTowerR * 2f, 0.06f, TeacherTowerR * 2f), Quaternion.identity, woodFloor);
                slabRenderers.Add(new KeyValuePair<float, Renderer>(Mathf.Max(0.02f, FloorY(f)), d.GetComponent<Renderer>()));
            }
        }

        // ================= 奥（＋z）：浴場・脱衣所・食堂・台所／娯楽室・トイレ／図書室・勉強部屋 =================
        void NBackRooms()
        {
            float a0 = -WingOut;
            // 境の壁（z 5.5）：食堂へは北の廊下のはし（x 6.3）、2・3階も廊下のはしに扉
            WallF(0, new Vector2(-WingOut, BackZ), new Vector2(WingOut, BackZ), DoorGap(0, 6.3f - a0, 1.2f));
            WallF(1, new Vector2(-WingOut, BackZ), new Vector2(WingOut, BackZ), DoorGap(1, -6.2f - a0), DoorGap(1, 6.3f - a0));
            WallF(2, new Vector2(-WingOut, BackZ), new Vector2(WingOut, BackZ), DoorGap(2, -6.2f - a0), DoorGap(2, 6.3f - a0));
            // 仕切り：台所｜食堂（配膳の口と扉）、食堂｜脱衣所（扉）、脱衣所｜浴場（扉）
            WallF(0, new Vector2(KitchenX0, BackZ), new Vector2(KitchenX0, BackOut), DoorGap(0, 9.6f - BackZ, 6.0f, 2.3f), DoorGap(0, KitchenDoorZ - BackZ, 1.0f, 2.3f));
            WallF(0, new Vector2(-2.5f, BackZ), new Vector2(-2.5f, BackOut), DoorGap(0, 13.6f - BackZ, 0.9f));
            WallF(0, new Vector2(-5f, BackZ), new Vector2(-5f, BackOut), DoorGap(0, 8.0f - BackZ, 0.9f));
            WallF(1, new Vector2(-2f, BackZ), new Vector2(-2f, BackOut));   // 娯楽室｜トイレ
            WallF(2, new Vector2(-4f, BackZ), new Vector2(-4f, BackOut));   // 図書室｜勉強部屋

            // --- 食堂（x −2.5〜7.5。机6＝36席） ---
            Label(new Vector3(DiningCenter.x, 2.4f, BackZ + 0.6f), "食堂");
            AddArea("食堂", Rect.MinMaxRect(-2.5f, BackZ, KitchenX0, BackOut), 0);
            float cx = KitchenX0;
            PlaceFurn("Furn_ServingCounter", null, new Vector3(cx, 0f, 8.1f), -90f);
            PlaceFurn("Furn_ServingCounter", null, new Vector3(cx, 0f, 11.1f), -90f);
            blockers[0].Add(Rect.MinMaxRect(cx - 0.5f, 6.6f, cx + 0.5f, 12.6f));
            pois.Add(new Poi
            {
                label = "料理を受け取っている",
                lookAt = new Vector3(cx, 1f, 9.6f),
                slots = new[] { new Vector3(cx - 0.9f, 0f, 7.2f), new Vector3(cx - 0.9f, 0f, 8.4f), new Vector3(cx - 0.9f, 0f, 9.6f), new Vector3(cx - 0.9f, 0f, 10.8f), new Vector3(cx - 0.9f, 0f, 12.0f) },
                occupants = new SimCharacter[5],
                counter = true
            });
            foreach (float tz in new[] { 7.3f, 10.5f, 13.7f })
                foreach (float tx in new[] { -0.5f, 4.1f })
                    DiningTable(new Vector3(tx, 0f, tz), true);
            Chandelier(new Vector3(1.8f, 2.4f, 8.9f), 3.0f, 6f);
            Chandelier(new Vector3(1.8f, 2.4f, 12.1f), 3.0f, 6f);

            // --- 台所（x 7.5〜12・z 5.5〜15.5。いつでも入れる） ---
            NKitchen();

            // --- 脱衣所（x −5〜−2.5） ---
            Label(new Vector3(-3.75f, 2.4f, BackZ + 0.6f), "脱衣所");
            AddArea("脱衣所", Rect.MinMaxRect(-5f, BackZ, -2.5f, BackOut), 0);
            for (int i = 0; i < 3; i++) PlaceFurn("Furn_Locker", null, new Vector3(-4.55f + i * 0.75f, 0f, BackOut - 0.45f), 180f);
            blockers[0].Add(Rect.MinMaxRect(-4.95f, BackOut - 0.7f, -2.55f, BackOut));
            PlaceFurn("Furn_DressingBench", null, new Vector3(-3.75f, 0f, 11.0f), 90f);
            blockers[0].Add(new Rect(-3.975f, 10.2f, 0.45f, 1.6f));
            PlaceFurn("Furn_Basket", null, new Vector3(-2.95f, 0f, 6.2f), 0f);
            blockers[0].Add(new Rect(-3.2f, 6.0f, 0.5f, 0.4f));
            Lamp(new Vector3(-3.75f, 2.4f, 10.5f), 1.8f, 5f);

            // --- 浴場（x −12〜−5。湯船は6人） ---
            Label(new Vector3(-8.5f, 2.4f, BackZ + 0.6f), "浴場");
            AddArea("浴場", Rect.MinMaxRect(-WingOut, BackZ, -5f, BackOut), 0);
            float tx0 = -11.2f, tx1 = -7.2f, tz0 = 6.8f, tz1 = 11.8f;
            if (PlaceFurn("Furn_Bathtub", null, new Vector3((tx0 + tx1) / 2f, 0f, (tz0 + tz1) / 2f), 0f, false, new Vector3(0.8f, 1f, 1f)) == null)
                Box("Tub_Rim", new Vector3((tx0 + tx1) / 2f, 0.25f, (tz0 + tz1) / 2f), new Vector3(tx1 - tx0, 0.5f, tz1 - tz0), mortarDark);
            Box("Tub_Water", new Vector3((tx0 + tx1) / 2f, 0.38f, (tz0 + tz1) / 2f), new Vector3(tx1 - tx0 - 0.6f, 0.04f, tz1 - tz0 - 0.6f), waterMat);
            blockers[0].Add(Rect.MinMaxRect(tx0, tz0, tx1, tz1));
            foreach (var x in new[] { -10.3f, -8.3f })
                foreach (var z in new[] { 8.0f, 9.3f, 10.6f })
                    seats.Add(new Seat { kind = SeatKind.Bath, label = "湯船", area = "浴場", position = new Vector3(x, 0f, z), forward = Vector3.right, approach = new Vector3(tx1 + 0.6f, 0f, z) });
            for (int i = 0; i < 4; i++)
            {
                float x = -11.2f + i * 1.5f;
                PlaceFurn("Furn_WashStool", null, new Vector3(x, 0f, BackOut - 0.75f), 0f);
                PlaceFurn("Furn_WashBucket", null, new Vector3(x + 0.35f, 0f, BackOut - 0.65f), 0f);
                PlaceFurn("Furn_WashMirror", null, new Vector3(x, 0.9f, BackOut - 0.185f), 180f);
                blockers[0].Add(new Rect(x - 0.25f, BackOut - 1.0f, 0.8f, 0.85f));
            }
            Lamp(new Vector3(-8.5f, 2.4f, 10.5f), 2.4f, 7f);

            // --- 2階：娯楽室・トイレ ---
            float y1 = FloorY(1);
            Label(new Vector3(5f, y1 + 2.2f, 10.5f), "娯楽室");
            AddArea("娯楽室", Rect.MinMaxRect(-2f, BackZ, WingOut, BackOut), 1);
            Sofa("娯楽室のソファ", "娯楽室", new Vector3(5f, y1, BackOut - 0.7f), 180f, 2.4f, 3, fabricRed);
            if (PlaceFurn("Furn_LowTable", null, new Vector3(5f, y1, 12.6f), 0f) == null) Box("LowTable", new Vector3(5f, y1 + 0.22f, 12.6f), new Vector3(1.4f, 0.08f, 0.8f), woodMid);
            blockers[1].Add(new Rect(4.25f, 12.15f, 1.5f, 0.9f));
            Bookshelf(new Vector3(WingOut - 0.3f, y1, 10.5f), 2.4f, -90f, 2.2f);
            // 楽器と歌の場所（2026-11-04 famuru「入れた動きを全部使う」：R_PlayingGuitar・R_PlayingPiano・R_Singer。楽器の模型はまだ無いのでオルゴールだけ置く）
            PlaceFurn("Furn_DecorMusicBox", null, new Vector3(0.2f, y1, BackOut - 0.5f), 180f);
            pois.Add(MakePoi("楽器を弾いている", new Vector3(0.2f, y1 + 1.2f, BackOut - 0.5f), new Vector3(0.2f, y1, BackOut - 1.4f)));
            pois.Add(MakePoi("歌の練習をしている", new Vector3(1.6f, y1 + 1.4f, 13.0f), new Vector3(1.6f, y1, 12.2f), new Vector3(2.6f, y1, 12.4f)));
            Lamp(new Vector3(2f, y1 + 2.4f, 10.5f), 2.0f, 7f);
            Lamp(new Vector3(9f, y1 + 2.4f, 10.5f), 2.0f, 7f);
            Label(new Vector3(-7f, y1 + 2.2f, 10.5f), "トイレ");
            AddArea("トイレ", Rect.MinMaxRect(-WingOut, BackZ, -2f, BackOut), 1);
            Lamp(new Vector3(-7f, y1 + 2.4f, 10.5f), 1.6f, 6f);

            // --- 3階：図書室（x −4〜12）・勉強部屋（x −12〜−4） ---
            float y2 = FloorY(2);
            Label(new Vector3(4f, y2 + 2.2f, 10.5f), "図書室");
            AddArea("図書室", Rect.MinMaxRect(-4f, BackZ, WingOut, BackOut), 2);
            for (int i = 0; i < 4; i++) Bookshelf(new Vector3(-2.4f + i * 3.2f, y2, BackOut - 0.3f), 2.8f, 180f, 2.6f);
            foreach (float tx in new[] { 0.5f, 6.5f })
            {
                if (PlaceFurn("Furn_LibraryTable", null, new Vector3(tx, y2, 10.0f), 0f) == null) Box("LibTable", new Vector3(tx, y2 + 0.75f, 10.0f), new Vector3(2.4f, 0.08f, 1.0f), woodMid);
                blockers[2].Add(new Rect(tx - 1.2f, 9.5f, 2.4f, 1.0f));
                for (int k = 0; k < 2; k++) { Chair("図書室の席", "図書室", new Vector3(tx - 0.6f + k * 1.2f, y2, 10.8f), 180f); Chair("図書室の席", "図書室", new Vector3(tx - 0.6f + k * 1.2f, y2, 9.2f), 0f); }
            }
            pois.Add(MakePoi("本を探している", new Vector3(2f, y2 + 1.4f, BackOut), new Vector3(1.4f, y2, BackOut - 1.2f), new Vector3(4.6f, y2, BackOut - 1.2f), new Vector3(7.8f, y2, BackOut - 1.2f)));
            Lamp(new Vector3(1f, y2 + 2.4f, 10.5f), 2.0f, 7f);
            Lamp(new Vector3(8f, y2 + 2.4f, 10.5f), 2.0f, 7f);
            Label(new Vector3(-8f, y2 + 2.2f, 10.5f), "勉強部屋");
            AddArea("勉強部屋", Rect.MinMaxRect(-WingOut, BackZ, -4f, BackOut), 2);
            if (PlaceFurn("Furn_BigTable", null, new Vector3(-8f, y2, 10.5f), 90f) == null) Box("StudyTable", new Vector3(-8f, y2 + 0.75f, 10.5f), new Vector3(1.0f, 0.08f, 2.4f), woodMid);
            blockers[2].Add(new Rect(-8.5f, 9.3f, 1.0f, 2.4f));
            for (int k = 0; k < 3; k++) { Chair("勉強部屋の席", "勉強部屋", new Vector3(-8.8f, y2, 9.7f + k * 0.8f), 90f); Chair("勉強部屋の席", "勉強部屋", new Vector3(-7.2f, y2, 9.7f + k * 0.8f), -90f); }
            Lamp(new Vector3(-8f, y2 + 2.4f, 10.5f), 2.0f, 7f);
        }

        void NKitchen()
        {
            float x0 = KitchenX0, x1 = WingOut, z0 = BackZ, z1 = BackOut, wi = WallT / 2f;
            Label(new Vector3(9.75f, 2.4f, z0 + 0.6f), "台所");
            AddArea("台所", Rect.MinMaxRect(x0, z0, x1, z1), 0);
            float ex = x1 - wi;
            // 東（北の外壁 x 12）ぞい：調理台・流し・薪
            bool deep = Resources.Load<GameObject>("Furniture/Furn_KitchenCounterDeep") != null;
            float cd = deep ? 1.0f : 0.65f;
            PlaceFurn(deep ? "Furn_KitchenCounterDeep" : "Furn_KitchenCounter", null, new Vector3(ex - cd / 2f, 0f, 7.0f), -90f);
            PlaceFurn(deep ? "Furn_KitchenSinkDeep" : "Furn_KitchenSinkCounter", null, new Vector3(ex - cd / 2f, 0f, 9.0f), -90f);
            blockers[0].Add(Rect.MinMaxRect(ex - cd - 0.05f, 6.0f, x1, 10.0f));
            // 奥の壁（z 15.5）：大鍋の炉と窯
            float pz = z1 - wi - 0.6f;
            var pit = PlaceFurn("Furn_PotFirePit", null, new Vector3(10.6f, 0f, pz), 180f);
            if (pit != null)
            {
                PlaceFurn("Furn_StockPot", null, MarkPos(pit, "Pot", new Vector3(10.6f, 0.55f, pz)), 180f);
                var pg = Box("Pit_Fire", MarkPos(pit, "Fire", new Vector3(10.6f, 0.3f, pz)) + Vector3.up * 0.1f, new Vector3(0.6f, 0.22f, 0.5f), glowFire);
                pg.AddComponent<FlameWobble>().seed = 3.7f;
            }
            blockers[0].Add(Rect.MinMaxRect(9.75f, pz - 0.65f, 11.45f, z1));
            PlaceFurn("Furn_Firewood", null, new Vector3(ex - 0.375f, 0f, 12.6f), -90f, false, Vector3.one * 1.2f);
            blockers[0].Add(Rect.MinMaxRect(ex - 0.7f, 11.9f, x1, 13.3f));
            PlaceFurn("Furn_BarrelWater", null, new Vector3(ex - 0.35f, 0f, 11.0f), 0f);
            PlaceFurn("Furn_SpiceShelf", null, new Vector3(ex, 1.35f, 8.0f), -90f);
            // 真ん中：アイランド
            float ix = 9.7f, iz = 11.2f;
            PlaceFurn("Furn_KitchenIslandBig", null, new Vector3(ix, 0f, iz), 90f, false, new Vector3(0.8f, 1f, 0.85f));
            blockers[0].Add(new Rect(ix - 0.55f, iz - 1.2f, 1.1f, 2.4f));
            var kfire = Lamp(new Vector3(10.6f, 1.2f, 13.6f), 2.5f, 5f, false, 0.2f);
            kfire.color = new Color(1f, 0.6f, 0.3f);
            kfire.GetComponent<Flicker>().isFire = true;
            Lamp(new Vector3(9.7f, 2.4f, 9.0f), 1.8f, 5f);
            var cook = MakePoi("台所で料理をしている", new Vector3(ix, 0.9f, iz), new Vector3(ix - 0.95f, 0f, iz - 0.6f), new Vector3(ix - 0.95f, 0f, iz + 0.6f), new Vector3(ix + 0.95f, 0f, iz - 0.6f), new Vector3(ix + 0.95f, 0f, iz + 0.6f));
            cook.kitchen = true;
            pois.Add(cook);
            pois.Add(MakePoi("台所で洗い物をしている", new Vector3(ex - cd / 2f, 0.9f, 9.0f), new Vector3(ex - cd - 0.45f, 0f, 9.0f)));
            pois.Add(MakePoi("大鍋を煮込んでいる", new Vector3(10.6f, 0.9f, pz), new Vector3(10.6f, 0f, pz - 1.15f)));
            pois.Add(MakePoi("料理を配っている", new Vector3(x0, 1.0f, 9.6f), new Vector3(x0 + 0.9f, 0f, 8.1f), new Vector3(x0 + 0.9f, 0f, 11.1f)));
        }

        // ================= 手前（−z）：入口・先生の部屋・倉庫／篤の部屋・小さな談話室・入口の通路／トレーニング・シャワー =================
        void NFrontRooms()
        {
            float a0 = -WingOut;
            WallF(0, new Vector2(-WingOut, FrontZ), new Vector2(-WingIn, FrontZ), DoorGap(0, -6.2f - a0));
            WallF(0, new Vector2(WingIn, FrontZ), new Vector2(WingOut, FrontZ), DoorGap(0, 6.2f - WingIn));
            WallF(1, new Vector2(-WingOut, FrontZ), new Vector2(-WingIn, FrontZ), DoorGap(1, -6.2f - a0));
            WallF(1, new Vector2(-WingIn, FrontZ), new Vector2(PassX0, FrontZ), DoorGap(1, 1.1f + WingIn)); // 篤の部屋の扉（x 1.1。ソファの前をよける 2026-11-02）
            WallF(1, new Vector2(PassX1, FrontZ), new Vector2(WingOut, FrontZ), DoorGap(1, 6.2f - PassX1));
            WallF(2, new Vector2(-WingOut, FrontZ), new Vector2(-WingIn, FrontZ), DoorGap(2, -6.2f - a0));
            WallF(2, new Vector2(-WingIn, FrontZ), new Vector2(WingOut, FrontZ), DoorGap(2, 0f + WingIn), DoorGap(2, 6.2f + WingIn));
            WallF(1, new Vector2(-WingIn, FrontOut), new Vector2(-WingIn, FrontZ));
            WallF(1, new Vector2(PassX0, FrontOut), new Vector2(PassX0, FrontZ));
            WallF(1, new Vector2(PassX1, FrontOut), new Vector2(PassX1, FrontZ));
            WallF(2, new Vector2(-WingIn, FrontOut), new Vector2(-WingIn, FrontZ));
            float cs = -(WingIn + WingOut) / 2f, cn = -cs, cz = (FrontZ + FrontOut) / 2f;
            // 1階：ランドリー（もとの倉庫。外の物干し場へ出られる。NLaundry）
            NLaundry();
            // 2階
            float y1 = FloorY(1);
            Label(new Vector3(8.6f, y1 + 2.2f, cz), "小さな談話室（北）");
            AddArea("小さな談話室（北）", Rect.MinMaxRect(PassX1, FrontOut, WingOut, FrontZ), 1);
            Sofa("小さな談話室のソファ", "小さな談話室（北）", new Vector3(8.6f, y1, FrontOut + 0.7f), 0f, 2.4f, 3, fabricGreen);
            Lamp(new Vector3(8.6f, y1 + 2.4f, cz), 1.6f, 6f);
            Label(new Vector3(cs, y1 + 2.2f, cz), "小さな談話室（南）");
            AddArea("小さな談話室（南）", Rect.MinMaxRect(-WingOut, FrontOut, -WingIn, FrontZ), 1);
            Sofa("小さな談話室のソファ", "小さな談話室（南）", new Vector3(cs, y1, FrontOut + 0.7f), 0f, 2.4f, 3, fabricGreen);
            Lamp(new Vector3(cs, y1 + 2.4f, cz), 1.6f, 6f);
            Label(new Vector3((PassX0 + PassX1) / 2f, y1 + 2.2f, -12f), "入口の通路");
            AddArea("入口の通路", Rect.MinMaxRect(PassX0, FrontOut, PassX1, FrontZ), 1);
            Lamp(new Vector3((PassX0 + PassX1) / 2f, y1 + 2.4f, -12.5f), 1.4f, 5f);
            // 3階
            float y2 = FloorY(2);
            Label(new Vector3(3.4f, y2 + 2.2f, cz), "トレーニング部屋");
            AddArea("トレーニング部屋", Rect.MinMaxRect(-WingIn, FrontOut, WingOut, FrontZ), 2);
            for (int i = 0; i < 2; i++) { PlaceFurn("Furn_TrainingMat", null, new Vector3(-1.5f + i * 4.5f, y2, cz), 0f); }
            PlaceFurn("Furn_Dummy", null, new Vector3(9.5f, y2, cz), 0f);
            blockers[2].Add(new Rect(9.2f, cz - 0.3f, 0.6f, 0.6f));
            pois.Add(MakePoi("体を鍛えている", new Vector3(-1.5f, y2 + 1f, cz), new Vector3(-2.3f, y2, cz), new Vector3(-0.7f, y2, cz)));
            Lamp(new Vector3(0f, y2 + 2.4f, cz), 2.0f, 7f);
            Lamp(new Vector3(8f, y2 + 2.4f, cz), 2.0f, 7f);
            Label(new Vector3(cs, y2 + 2.2f, cz), "シャワールーム");
            AddArea("シャワールーム", Rect.MinMaxRect(-WingOut, FrontOut, -WingIn, FrontZ), 2);
            Lamp(new Vector3(cs, y2 + 2.4f, cz), 1.6f, 6f);
        }

        // ================= 外壁 =================
        void NOuterWalls()
        {
            float a0 = WingOut + 0.15f;
            for (int f = 0; f <= 2; f++)
            {
                float y = FloorY(f);
                // 手前（z −15.5）：1階は入口（x 1.5）・先生の個室R2への扉（x 9.4）、2階は入口の通路（渡り廊下へ）
                var gapsF = new List<Vector4>();
                if (f == 0) { gapsF.Add(UseV3 ? new Vector4(a0 + DormDoorX - 0.83f, a0 + DormDoorX + 0.83f, y, y + 2.6f) : new Vector4(a0 + 0.5f, a0 + 2.5f, y, y + 2.6f)); gapsF.Add(new Vector4(a0 + 8.95f, a0 + 9.85f, y, y + 2.2f)); } // 入口の穴は、依頼28 の入口の口（幅 1.66m）と同じに
                if (f == 1) gapsF.Add(new Vector4(a0 + PassX0 + 0.2f, a0 + PassX1 - 0.2f, y, y + 2.5f));
                // 塔の中（famuru 2026-11-08「塔の中に前の壁が残っているので削除」）：塔の円の中は外の石の部品を張らない。
                // 壁は、先生の塔は2・3階（1階は先生の個室の仕切り）、南西の塔は1〜3階とも消して、となりの部屋と塔をつなぐ
                var skinF = new List<Vector4>(gapsF);
                skinF.Add(new Vector4(a0 + WingOut - TowerR - 0.1f, a0 + WingOut + 0.3f, y, y + FloorH));
                skinF.Add(new Vector4(-0.1f, a0 - WingOut + TowerR + 0.1f, y, y + FloorH));
                if (f >= 1) gapsF.Add(new Vector4(a0 + WingOut - TowerR, a0 + WingOut + 0.15f, y, y + FloorH));
                gapsF.Add(new Vector4(0f, a0 - WingOut + TowerR, y, y + FloorH));
                var front = WallF(f, new Vector2(-WingOut - 0.15f, FrontOut), new Vector2(WingOut + 0.15f, FrontOut), gapsF.ToArray());
                Outer(front, y, y + FloorH, Vector3.back, skinF.ToArray(), f == 2);
                if (f == 0) EntranceV3(front, "Entrance_Dorm", new Vector3(DormDoorX, y, FrontOut - WallT / 2f), Vector3.back, new Vector3(0.75f, 1f, 1f), 0.6f); // 寮の入口（依頼28 追記3。扉をあけた形）。すぐ横の2階の渡り廊下にかからないよう、横だけ 0.75 倍にして 0.25m 寄せる
                float[] fwx = f == 0 ? new[] { -8.6f } : (f == 1 ? new[] { -8.6f, -1.2f } : new[] { -8.6f, -1.2f, 2.0f });
                foreach (float x in fwx) { if (x < -WingOut + TowerR + 0.6f || x > WingOut - TowerR - 0.6f) continue; Window(front, new Vector3(x, y, FrontOut), Vector3.forward, 1.0f, 1.0f, 1.3f); } // 塔の中の窓はなし
                // 奥（z 15.5）：1階は食堂から庭へ（x 2.0）、浴場から露天風呂へ（x −9.6）
                var gapsB = new List<Vector4>();
                if (f == 0) { gapsB.Add(new Vector4(a0 + 1.0f, a0 + 3.0f, y, y + 2.6f)); gapsB.Add(new Vector4(a0 - 10.1f, a0 - 9.1f, y, y + 2.2f)); }
                var back = WallF(f, new Vector2(-WingOut - 0.15f, BackOut), new Vector2(WingOut + 0.15f, BackOut), gapsB.ToArray());
                var skinB = new List<Vector4>(gapsB);
                skinB.Add(new Vector4(a0 + WingOut - NETowerR - 0.1f, a0 + WingOut + 0.3f, y, y + FloorH)); // 北東の塔の中は石の部品なし（壁は台所・図書室との仕切り）
                Outer(back, y, y + FloorH, Vector3.forward, skinB.ToArray(), f == 2);
                float[] bwx = f == 0 ? new[] { -1.2f, 5.6f, 9.8f } : new[] { -9f, -3f, 3f, 9f };
                foreach (float x in bwx) Window(back, new Vector3(x, y, BackOut), Vector3.back, 1.0f, 1.0f, 1.3f);
                // 北と南（x ±12）
                for (int s = -1; s <= 1; s += 2)
                {
                    var gs = new List<Vector4>();
                    if (f == 0 && s > 0) gs.Add(new Vector4(-12.9f - (FrontOut) - 0.45f, -12.9f - FrontOut + 0.45f, y, y + 2.2f)); // 先生の個室R3への扉（z −12.9）
                    if (s > 0) gs.Add(new Vector4(TDoorZ - FrontOut - 0.45f, TDoorZ - FrontOut + 0.45f, y, y + 2.2f)); // 北東の塔への扉（z 13.7）
                    if (f == 0 && s < 0) gs.Add(new Vector4(LaundryDoorZ - FrontOut - 0.45f, LaundryDoorZ - FrontOut + 0.45f, y, y + 2.2f)); // ランドリーから物干し場への扉（z −10.6）
                    var skinS = new List<Vector4>(gs);
                    skinS.Add(new Vector4(-0.1f, TowerR + 0.1f, y, y + FloorH));                         // 先生の塔・南西の塔の中
                    if (s > 0) skinS.Add(new Vector4(BackOut - FrontOut - NETowerR - 0.1f, BackOut - FrontOut + 0.1f, y, y + FloorH)); // 北東の塔の中
                    if (s < 0 || f >= 1) gs.Add(new Vector4(0f, TowerR, y, y + FloorH));               // 塔とつなぐ（先生の塔は2・3階、南西の塔は1〜3階）
                    var side = WallF(f, new Vector2(s * WingOut, FrontOut), new Vector2(s * WingOut, BackOut), gs.ToArray());
                    Outer(side, y, y + FloorH, new Vector3(s, 0f, 0f), skinS.ToArray(), f == 2);
                    for (int i = 0; i < 5; i++) Window(side, new Vector3(s * WingOut, y, FrontZ + RoomW * (i + 0.5f)), new Vector3(-s, 0f, 0f), 1.0f, 0.9f, 1.2f);
                    Window(side, new Vector3(s * WingOut, y, (BackZ + BackOut) / 2f), new Vector3(-s, 0f, 0f), 1.0f, 1.0f, 1.3f);
                }
            }
            Label(new Vector3(1.5f, 2.6f, FrontOut + 0.4f), "寮の入り口（前庭へ）");
        }

        // ================= 塔（四すみ。外見は依頼20の部品・壁は多角形） =================
        void NTowers()
        {
            // 北西：先生の塔（半径5）。建物の内側の4分の1を除いた外の弧に壁
            TowerRing(TeacherTowerC, TeacherTowerR, 180f, 450f, 14, 3, true);
            // 南西（ランドリーのすみ。中は閉じた物置）
            TowerRing(new Vector2(-WingOut, FrontOut), TowerR, 90f, 360f, 10, 3, false);
            TowerRing(NETowerC, NETowerR, 270f, 540f, 12, 3, false); // 北東：食料庫と屋上への階段（NFoodTower。半径 4.5）
            // 屋根
            if (Tower3Kit(TeacherTowerR) != null)
            {
                // 依頼31（famuru 2026-11-08「寮の塔の形を修正して」）：寮の屋根の上に丸い段（3m）と、城壁の塔と同じ屋根（Tower2_Roof）。北東は天文台なので胸壁
                TowerTop(TeacherTowerC, TeacherTowerR);
                TowerTop(new Vector2(-WingOut, FrontOut), TowerR);
                TowerBattlement(NETowerC, NETowerR);
                return;
            }
            PlaceKit("Roof_Cone", new Vector3(TeacherTowerC.x, EaveY, TeacherTowerC.y), 0f, new Vector3(TeacherTowerR / 3.5f, 1.1f, TeacherTowerR / 3.5f));
            PlaceKit("Roof_Cone", new Vector3(-WingOut, EaveY, FrontOut), 0f, Vector3.one);
            PlaceKit("Battlement_Top_Round", new Vector3(WingOut, FloorY(3), BackOut), 0f, new Vector3(NETowerR / 3.5f, 1f, NETowerR / 3.5f)); // 屋上の天文台（屋根なし）
        }

        // ================= 丸い塔の部品（依頼31 の寮の塔_v1。2026-11-08 家具のチャット） =================
        // 部品は塔の中心が原点の 30° の曲がった石の壁。Logs\old_towers.txt で前の多角形の塔に戻す
        static int tower3c = -1;
        /// <summary>その半径の塔の部品の名前の頭（"Tower3_R50" など）。部品が無いか Logs\old_towers.txt があれば null。</summary>
        static string Tower3Kit(float r)
        {
            if (tower3c < 0) tower3c = (UseV3 && Resources.Load<GameObject>("Furniture/Towers3/Furn_Tower3_R50_Plain") != null && !File.Exists(Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/old_towers.txt")))) ? 1 : 0;
            if (tower3c == 0) return null;
            string k = "Tower3_R" + Mathf.RoundToInt(r * 10f);
            return Resources.Load<GameObject>("Furniture/Towers3/Furn_" + k + "_Plain") != null ? k : null;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetTower3() { tower3c = -1; }
        /// <summary>部品の弧の真ん中（Blender の +Y）が向く Unity の向きのずれ（度）。</summary>
        static float Tower3YawOff = 180f;

        /// <summary>塔の中心 c に、角度 deg（0° が +x、90° が +z）の向きの部品を置く。</summary>
        GameObject Tower3Piece(string model, Vector2 c, float y, float deg, Vector3 scale)
        {
            Vector3 m = new Vector3(Mathf.Cos(deg * Mathf.Deg2Rad), 0f, Mathf.Sin(deg * Mathf.Deg2Rad));
            float yaw = Quaternion.LookRotation(m).eulerAngles.y + Tower3YawOff;
            return PlaceKit(model, new Vector3(c.x, y, c.y), yaw, scale);
        }

        /// <summary>寮の屋根の上の丸い段（360°）と屋根。</summary>
        void TowerTop(Vector2 c, float r)
        {
            string k = Tower3Kit(r);
            if (k == null) return;
            // 案1（famuru 2026-11-08）：軒（10.5m）の上に丸い壁を1段（3.5m）と上の段（3m）で 17m、その上に屋根。寮の屋根（棟 約13.4m）より上に出る
            for (int i = 0; i < 12; i++) Tower3Piece(k + (i % 3 == 1 ? "_Window" : "_Plain"), c, EaveY, i * 30f + 15f, Vector3.one);
            for (int i = 0; i < 12; i++) Tower3Piece(k + (i % 3 == 1 ? "_TopWindow" : "_Top"), c, EaveY + 3.5f, i * 30f + 15f, Vector3.one);
            // 依頼28 の Tower2_Roof（直径 8m・高さ 7m。城壁の塔 直径 7m 用）を、段の壁の外の半径（r＋0.64）に合わせる
            float sx = (r + 0.64f) * 2f * (8f / 7f) / 8f;
            PlaceKit("Tower2_Roof", new Vector3(c.x, EaveY + 6.5f, c.y), 0f, new Vector3(sx, 1.05f, sx));
        }

        /// <summary>北東の塔：屋上の天文台のまわりの胸壁（360°）。</summary>
        void TowerBattlement(Vector2 c, float r)
        {
            string k = Tower3Kit(r);
            if (k == null || Resources.Load<GameObject>("Furniture/Towers3/Furn_" + k + "_Battlement") == null)
            {
                PlaceKit("Battlement_Top_Round", new Vector3(c.x, FloorY(3), c.y), 0f, new Vector3(r / 3.5f, 1f, r / 3.5f));
                return;
            }
            // 軒（10.5m）から天文台の床（14m）まで丸い壁を1段、その上に胸壁
            for (int i = 0; i < 12; i++) Tower3Piece(k + (i % 3 == 1 ? "_Window" : "_Plain"), c, EaveY, i * 30f + 15f, Vector3.one);
            for (int i = 0; i < 12; i++) Tower3Piece(k + "_Battlement", c, FloorY(3), i * 30f + 15f, Vector3.one);
        }

        /// <summary>多角形の塔の壁（角度 a0〜a1 の弧。0° が +x、90° が +z）。floors 階分。open なら中に入れる（床は別）。</summary>
        void TowerRing(Vector2 c, float r, float a0, float a1, int segs, int floors, bool open)
        {
            string kit = Tower3Kit(r); // 依頼31 の丸い塔の部品（30° ごと）
            if (kit != null) segs = Mathf.Max(1, Mathf.RoundToInt((a1 - a0) / 30f));
            for (int f = 0; f < floors; f++)
            {
                WallSeg first = null, last = null;
                for (int i = 0; i < segs; i++)
                {
                    float t0 = Mathf.Deg2Rad * Mathf.Lerp(a0, a1, i / (float)segs), t1 = Mathf.Deg2Rad * Mathf.Lerp(a0, a1, (i + 1) / (float)segs);
                    Vector2 p0 = c + new Vector2(Mathf.Cos(t0), Mathf.Sin(t0)) * r, p1 = c + new Vector2(Mathf.Cos(t1), Mathf.Sin(t1)) * r;
                    // 丸い石の部品があるときは、内側の平らな板の壁は作らない（famuru 2026-11-08「塔の外壁にくっついている板のような壁を削除して」）。
                    // カメラの前で石の部品を消すしくみのため、形のない壁の線だけ登録する
                    WallSeg w;
                    if (kit != null) { w = new WallSeg { a = p0, b = p1 }; walls.Add(w); }
                    else w = WallF(f, p0, p1);
                    if (i == 0) first = w;
                    last = w;
                    Vector2 mo = ((p0 + p1) / 2f - c).normalized;
                    if (kit != null)
                    {
                        // 曲がった石の壁（窓の所は窓つき）。壁と一緒にカメラの前で消える
                        var go = Tower3Piece(kit + (i % 3 == 1 ? "_Window" : "_Plain"), c, FloorY(f), Mathf.Lerp(a0, a1, (i + 0.5f) / segs), Vector3.one);
                        if (go != null && w != null) foreach (var rr in go.GetComponentsInChildren<Renderer>()) w.upper.Add(rr);
                        // 外の石の厚みの所は歩けないように（1階だけ）
                        if (f == 0)
                            for (int s2 = 0; s2 < 4; s2++)
                            {
                                float tt = Mathf.Lerp(t0, t1, (s2 + 0.5f) / 4f);
                                Vector2 q = c + new Vector2(Mathf.Cos(tt), Mathf.Sin(tt)) * (r + 0.45f);
                                blockers[0].Add(new Rect(q.x - 0.3f, q.y - 0.3f, 0.6f, 0.6f));
                            }
                    }
                    else
                    Outer(w, FloorY(f), FloorY(f) + FloorH, new Vector3(mo.x, 0f, mo.y), new Vector4[0], false); // 塔の外側も石の部品（依頼28）
                    if (i % 3 == 1 && kit == null) // 丸い石の部品は窓つき（Window の部品にガラスがある）
                    {
                        Vector2 m = (p0 + p1) / 2f, n = (c - m).normalized;
                        Window(w, new Vector3(m.x, FloorY(f), m.y), new Vector3(n.x, 0f, n.y), 1.0f, 0.8f, 1.3f);
                    }
                }
                // 寮の外壁につながる所の隅石（すき間をかくす）
                if (kit != null)
                {
                    // 弧のはしは寮の壁の中心線の上なので、帯（幅 0.6m）が部屋の中に出ないよう、壁の外の石の厚みの所（0.45m 外）へずらす
                    float dEnd = Mathf.Rad2Deg * 0.45f / (r + 0.4f);
                    var e0 = Tower3Piece(kit + "_End", c, FloorY(f), a0 + dEnd, Vector3.one);
                    var e1 = Tower3Piece(kit + "_End", c, FloorY(f), a1 - dEnd, Vector3.one);
                    if (e0 != null && first != null) foreach (var rr in e0.GetComponentsInChildren<Renderer>()) first.upper.Add(rr);
                    if (e1 != null && last != null) foreach (var rr in e1.GetComponentsInChildren<Renderer>()) last.upper.Add(rr);
                }
            }
        }

        /// <summary>依頼20の部品（Furn_＊）を、入れ物で向き・大きさを付けて置く（FBX の根元の回転の影響を受けない）。模様は共通の材質。</summary>
        /// <summary>依頼28 の建物（建物_v3）の屋根・屋根窓・煙突を使うか。Logs\old_buildings.txt を置くと前の部品（見比べ用）。2026-11-07 家具のチャット</summary>
        static bool UseV3
        {
            get
            {
                if (useV3 < 0) useV3 = (Resources.Load<GameObject>("Furniture/Buildings3/Furn_Roof2_Gable") != null && !File.Exists(Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/old_buildings.txt")))) ? 1 : 0;
                return useV3 == 1;
            }
        }
        static int useV3 = -1;

        // ================= 外壁の外側の石の部品（依頼28 の建物_v3。famuru 2026-11-07「進んでよい」） =================
        // 外壁（寮の外まわり・本館の南の棟の外まわり）の外の面に、3m ごとの石の壁の部品（Wall2_Window／Wall2_Plain）を張る。
        // 窓のある所は窓の部品を窓の真ん中に、扉の穴の所はあけ、残りを窓なしの部品で埋める。いちばん上の階は軒の帯（Cornice2）も。
        // 部品は壁の上の部分と一緒に、カメラの前では消える（壁の upper に入れる）。Logs\old_buildings.txt で前の見た目。
        class SkinInfo
        {
            public Vector2 a, dir; public float len, y0, y1, depth = 0.6f; public Vector3 outward; public bool cornice, done;
            public readonly List<Vector2> gaps = new List<Vector2>();
            public readonly List<Vector2> full = new List<Vector2>(); // 床から天井までの穴（塔の中など。軒の帯も付けない）
            public readonly List<float> wins = new List<float>();
        }
        readonly Dictionary<WallSeg, SkinInfo> skins = new Dictionary<WallSeg, SkinInfo>();

        void Outer(WallSeg w, float y0, float y1, Vector3 outward, Vector4[] gaps, bool cornice, float depth = 0.6f)
        {
            if (!NewLayout || !UseV3 || w == null || skins.ContainsKey(w)) return;
            var si = new SkinInfo { a = w.a, len = Vector2.Distance(w.a, w.b), y0 = y0, y1 = y1, outward = outward.normalized, cornice = cornice, depth = depth };
            si.dir = (w.b - w.a).normalized;
            foreach (var g in gaps) if (g.z <= y0 + 0.05f) si.gaps.Add(new Vector2(g.x, g.y)); // 床からの出入口だけあける（窓の穴は窓の部品で）
            foreach (var g in gaps) if (g.z <= y0 + 0.05f && g.w >= y1 - 0.05f) si.full.Add(new Vector2(g.x, g.y));
            skins[w] = si;
        }

        void RecordSkinWindow(WallSeg w, Vector3 p)
        {
            SkinInfo si;
            if (w != null && skins.TryGetValue(w, out si)) si.wins.Add(Vector2.Dot(new Vector2(p.x, p.z) - si.a, si.dir));
        }

        void SkinAll()
        {
            foreach (var kv in skins)
            {
                var si = kv.Value;
                if (si.done) continue;
                si.done = true;
                var w = kv.Key;
                float yaw = Quaternion.LookRotation(si.outward).eulerAngles.y;
                float hs = (si.y1 - si.y0) / 3.5f;
                System.Func<float, float, bool> blocked = (s0, s1) => { foreach (var g in si.gaps) if (s1 > g.x + 0.02f && s0 < g.y - 0.02f) return true; return false; };
                var used = new List<Vector2>();
                foreach (var g in si.gaps) used.Add(new Vector2(Mathf.Max(0f, g.x), Mathf.Min(si.len, g.y)));
                // 窓の部品（窓の真ん中に。となりの窓・扉の穴・壁のはしにかからない幅に）
                si.wins.Sort();
                for (int i = 0; i < si.wins.Count; i++)
                {
                    float cpos = si.wins[i], h = 1.5f;
                    if (i > 0) h = Mathf.Min(h, (cpos - si.wins[i - 1]) / 2f);
                    if (i + 1 < si.wins.Count) h = Mathf.Min(h, (si.wins[i + 1] - cpos) / 2f);
                    h = Mathf.Min(h, cpos, si.len - cpos);
                    foreach (var g in si.gaps) { if (g.y <= cpos) h = Mathf.Min(h, cpos - g.y); else if (g.x >= cpos) h = Mathf.Min(h, g.x - cpos); }
                    if (h < 0.6f || blocked(cpos - h, cpos + h)) continue;
                    SkinPiece(w, si, "Wall2_Window", cpos, 2f * h, yaw, hs);
                    used.Add(new Vector2(cpos - h, cpos + h));
                }
                // 残りを窓なしの部品で埋める
                used.Sort((p, q) => p.x.CompareTo(q.x));
                float cur = 0f;
                foreach (var u in used) { if (u.x > cur + 0.05f) FillPlain(w, si, cur, u.x, yaw, hs); cur = Mathf.Max(cur, u.y); }
                if (si.len > cur + 0.05f) FillPlain(w, si, cur, si.len, yaw, hs);
                SkinBlockers(si);
                // 軒の帯
                if (si.cornice)
                {
                    int n = Mathf.Max(1, Mathf.RoundToInt(si.len / 3f));
                    float seg = si.len / n;
                    for (int k = 0; k < n; k++)
                    {
                        float cm = seg * (k + 0.5f);
                        bool skip = false;
                        foreach (var g in si.full) if (cm > g.x && cm < g.y) skip = true;
                        if (!skip) SkinPiece(w, si, "Cornice2", cm, seg, yaw, 1f, si.y1 - 0.6f);
                    }
                }
            }
        }

        /// <summary>
        /// 入口（依頼28 追記3：扉の板を外して口をあけた Tripo 版の入口と、別の部品の左右の扉）を、壁の扉の穴の外に置く。
        /// face：穴の真ん中の、壁の外の面の床の位置。模型の背面が石の壁の部品（厚み 0.6m）の外の面に付くように、大きさの箱で測って寄せる。扉は昼も夜も開けた形（生徒が出入りするので）。
        /// </summary>
        /// <summary>寮の入口の真ん中（反転の前の x）。依頼28 の入口を置くときは、すぐ横の2階の渡り廊下にかからないよう 0.25m 寄せた所。</summary>
        static float DormDoorX { get { return UseV3 ? 1.25f : 1.5f; } }

        void EntranceV3(WallSeg w, string model, Vector3 face, Vector3 outward, Vector3 scale, float passHalf)
        {
            if (!NewLayout || !UseV3) return;
            outward = outward.normalized;
            float yaw = Quaternion.LookRotation(outward).eulerAngles.y;
            var go = PlaceKit(model, face, yaw, scale);
            if (go == null) return;
            var dr = PlaceKit(model + "_Door", face, yaw, scale);
            // 背面（outward の向きでいちばん奥）を壁の部品の外の面へ
            Vector3 wOut = root.TransformDirection(outward);
            Vector3 wFace = root.TransformPoint(face);
            float back = float.MaxValue;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var bb = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var cpt = new Vector3((i & 1) == 0 ? bb.min.x : bb.max.x, (i & 2) == 0 ? bb.min.y : bb.max.y, (i & 4) == 0 ? bb.min.z : bb.max.z);
                    back = Mathf.Min(back, Vector3.Dot(cpt - wFace, wOut));
                }
            }
            float skinT = Resources.Load<GameObject>("Furniture/Buildings3/Furn_Wall2_Plain") != null ? 0.6f : 0f;
            Vector3 move = wOut * (skinT + 0.01f - back);
            go.transform.position += move;
            if (dr != null)
            {
                dr.transform.position += move;
                // 左右の扉を蝶番（子の原点）で外へ開く
                foreach (var t in dr.GetComponentsInChildren<Transform>())
                {
                    if (t.name.EndsWith("_Door_L")) t.RotateAround(t.position, Vector3.up, EntranceOpenDeg);
                    else if (t.name.EndsWith("_Door_R")) t.RotateAround(t.position, Vector3.up, -EntranceOpenDeg);
                }
            }
            if (w != null)
            {
                foreach (var r in go.GetComponentsInChildren<Renderer>()) w.upper.Add(r);
                if (dr != null) foreach (var r in dr.GetComponentsInChildren<Renderer>()) w.upper.Add(r);
            }
            // 入口の石の部分は歩けないように（口の真ん中の通り道 ±passHalf だけあける）。開いた扉の所も
            Vector3 along3 = Vector3.Cross(Vector3.up, outward).normalized;
            Vector2 al = new Vector2(along3.x, along3.z), ou = new Vector2(outward.x, outward.z), f2 = new Vector2(face.x, face.z);
            float aMin = float.MaxValue, aMax = float.MinValue, oMax = float.MinValue;
            foreach (var g in new[] { go, dr })
            {
                if (g == null) continue;
                foreach (var r in g.GetComponentsInChildren<Renderer>())
                {
                    var bb = r.bounds;
                    for (int i = 0; i < 8; i++)
                    {
                        var lp = root.InverseTransformPoint(new Vector3((i & 1) == 0 ? bb.min.x : bb.max.x, (i & 2) == 0 ? bb.min.y : bb.max.y, (i & 4) == 0 ? bb.min.z : bb.max.z));
                        Vector2 q = new Vector2(lp.x, lp.z) - f2;
                        aMin = Mathf.Min(aMin, Vector2.Dot(q, al)); aMax = Mathf.Max(aMax, Vector2.Dot(q, al)); oMax = Mathf.Max(oMax, Vector2.Dot(q, ou));
                    }
                }
            }
            int fl = Mathf.Clamp(Mathf.RoundToInt(face.y / FloorH), 0, 3);
            foreach (var iv in new[] { new Vector2(aMin, -passHalf), new Vector2(passHalf, aMax) })
            {
                if (iv.y <= iv.x) continue;
                Vector2 p0 = f2 + al * iv.x, p1 = f2 + al * iv.y + ou * oMax;
                blockers[fl].Add(Rect.MinMaxRect(Mathf.Min(p0.x, p1.x), Mathf.Min(p0.y, p1.y), Mathf.Max(p0.x, p1.x), Mathf.Max(p0.y, p1.y)));
            }
        }
        const float EntranceOpenDeg = 80f;

        void FillPlain(WallSeg w, SkinInfo si, float s0, float s1, float yaw, float hs)
        {
            float len = s1 - s0;
            int n = Mathf.Max(1, Mathf.RoundToInt(len / 3f));
            float seg = len / n;
            for (int k = 0; k < n; k++) SkinPiece(w, si, "Wall2_Plain", s0 + seg * (k + 0.5f), seg, yaw, hs);
        }

        void SkinPiece(WallSeg w, SkinInfo si, string model, float along, float width, float yaw, float hs, float y = float.NaN)
        {
            Vector2 p = si.a + si.dir * along;
            Vector3 pos = new Vector3(p.x, float.IsNaN(y) ? si.y0 : y, p.y) + si.outward * (WallT / 2f + 0.005f);
            var go = PlaceKit(model, pos, yaw, new Vector3(width / 3f, hs, si.depth / 0.6f));
            if (go != null) foreach (var r in go.GetComponentsInChildren<Renderer>()) w.upper.Add(r);
        }

        /// <summary>
        /// 石の壁の部品の厚みの所は歩けないようにする（famuru 2026-11-07「見た目と動きに違いがある所を探して」：外の庭や外廊下で、生徒が壁の部品にめりこんでいた）。扉の穴の所はあける。
        /// </summary>
        void SkinBlockers(SkinInfo si)
        {
            int f = Mathf.Clamp(Mathf.RoundToInt(si.y0 / FloorH), 0, 3);
            Vector2 o = new Vector2(si.outward.x, si.outward.z);
            float d0 = WallT / 2f, d1 = WallT / 2f + si.depth + 0.05f;
            bool axis = Mathf.Abs(si.dir.x) < 0.01f || Mathf.Abs(si.dir.y) < 0.01f;
            var free = new List<Vector2>();
            float cur = 0f;
            var gs = new List<Vector2>(si.gaps); gs.Sort((p, q) => p.x.CompareTo(q.x));
            foreach (var g in gs) { if (g.x > cur) free.Add(new Vector2(cur, g.x)); cur = Mathf.Max(cur, g.y); }
            if (cur < si.len) free.Add(new Vector2(cur, si.len));
            foreach (var iv in free)
            {
                if (axis)
                {
                    Vector2 p0 = si.a + si.dir * iv.x + o * d0, p1 = si.a + si.dir * iv.y + o * d1;
                    blockers[f].Add(Rect.MinMaxRect(Mathf.Min(p0.x, p1.x), Mathf.Min(p0.y, p1.y), Mathf.Max(p0.x, p1.x), Mathf.Max(p0.y, p1.y)));
                }
                else
                    for (float t = iv.x + 0.2f; t < iv.y; t += 0.4f)
                    {
                        Vector2 c2 = si.a + si.dir * t + o * ((d0 + d1) / 2f);
                        float h = Mathf.Max(0.25f, (d1 - d0) / 2f);
                        blockers[f].Add(Rect.MinMaxRect(c2.x - h, c2.y - h, c2.x + h, c2.y + h));
                    }
            }
        }

        GameObject PlaceKit(string name, Vector3 pos, float yaw, Vector3 scale)
        {
            if (UseV3)
            {
                // 依頼28：屋根は 3m 分（前は 4m）・高さ 5m（前は 6m）の部品なので、同じ大きさになるよう拡大を直す
                switch (name)
                {
                    case "Roof_Gable": name = "Roof2_Gable"; scale = new Vector3(scale.x * 4f / 3f, scale.y * 6f / 5f, scale.z); break;
                    case "Roof_GableEnd": name = "Roof2_GableEnd"; scale = new Vector3(scale.x, scale.y * 6f / 5f, scale.z); break;
                    case "Dormer": name = "Dormer2"; scale = Vector3.one * 1.5f; pos += Vector3.up * 0.4f; break; // 屋根窓は小さくて屋根に埋まるので大きく・少し上に
                    case "Chimney": name = "Chimney2"; scale = Vector3.one * 1.2f; break;
                }
            }
            var pf = Resources.Load<GameObject>("Furniture/Furn_" + name);
            if (pf == null) pf = Resources.Load<GameObject>("Furniture/Buildings3/Furn_" + name);
            if (pf == null) pf = Resources.Load<GameObject>("Furniture/Towers3/Furn_" + name); // 依頼31 の寮の塔
            if (pf == null) return null;
            var holder = new GameObject(name).transform;
            holder.SetParent(root, false);
            holder.localPosition = pos;
            holder.localRotation = Quaternion.Euler(0f, yaw, 0f);
            holder.localScale = scale;
            var go = Instantiate(pf, holder, false);
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var sm = r.sharedMaterials;
                for (int i = 0; i < sm.Length; i++)
                {
                    string n = sm[i] != null ? sm[i].name : "";
                    Material m = KitMat(n, name);
                    if (m != null) sm[i] = m;
                }
                r.sharedMaterials = sm;
            }
            return holder.gameObject;
        }

        readonly Dictionary<string, Material> kitMats = new Dictionary<string, Material>();
        /// <summary>城の部品（屋外）の模様。依頼24 の作り直し（Resources\TilesV2）を先に探す。寮の中の模様は今のまま。</summary>
        static Texture2D OutTex(string n) { var t = Resources.Load<Texture2D>("TilesV2/Tile_" + n); return t != null ? t : TileTex(n); }
        static Texture2D OutAtlas() { var t = Resources.Load<Texture2D>("TilesV2/Castle_Atlas"); return t != null ? t : Resources.Load<Texture2D>("Tiles/Castle_Atlas"); }
        Material KitMat(string matName, string model)
        {
            string key = null;
            foreach (var k in new[] { "Stone_Wall", "Stone_Trim", "Roof_Slate", "Wood_Dark", "Plaster", "Ground_Path", "Rock_Cliff", "Metal_Iron", "Glass_Window", "Fabric_Banner", "Atlas_Detail" })
                if (matName.StartsWith(k)) { key = k; break; }
            if (key == null) return null;
            if (key == "Metal_Iron" && model == "Roof_Dome") key = "Dome";
            Material m;
            if (kitMats.TryGetValue(key, out m)) return m;
            switch (key)
            {
                case "Stone_Wall": m = TilePBR.Apply(ProtoArt.Mat(Color.white, OutTex("StoneWall"), null, 0.12f), "Tile_StoneWall"); break;
                case "Stone_Trim": { bool tr = OutTex("StoneTrim") != null; m = TilePBR.Apply(ProtoArt.Mat(Color.white, OutTex(tr ? "StoneTrim" : "StoneWall"), null, 0.12f), tr ? "Tile_StoneTrim" : "Tile_StoneWall"); break; } // 依頼24
                case "Roof_Slate": m = TilePBR.Apply(ProtoArt.Mat(Color.white, OutTex("RoofSlate"), null, 0.25f), "Tile_RoofSlate"); break;
                case "Wood_Dark": m = TilePBR.Apply(ProtoArt.Mat(Color.white, OutTex("WoodPanel"), null, 0.15f), "Tile_WoodPanel"); break;
                case "Plaster": m = TilePBR.Apply(ProtoArt.Mat(Color.white, OutTex("Plaster"), null, 0.1f), "Tile_Plaster"); break;
                case "Ground_Path": m = TilePBR.Apply(ProtoArt.Mat(Color.white, OutTex("Flagstone"), null, 0.1f), "Tile_Flagstone"); break;
                case "Rock_Cliff": m = TilePBR.Apply(ProtoArt.Mat(Color.white, OutTex("Cliff"), null, 0.08f), "Tile_Cliff"); break;
                case "Metal_Iron": m = ProtoArt.Mat(new Color(0.17f, 0.17f, 0.18f), null, null, 0.35f); break;
                case "Dome": m = ProtoArt.Mat(new Color(0.36f, 0.6f, 0.53f), null, null, 0.4f); break;
                case "Glass_Window":
                    if (sharedWindowMat == null) sharedWindowMat = ProtoArt.GlassClear(); // 透明（2026-11-08）
                    m = sharedWindowMat; break;
                case "Fabric_Banner": m = ProtoArt.Mat(new Color(0.18f, 0.29f, 0.62f), null, null, 0.1f); break;
                default:
                    m = ProtoArt.MatNew(Color.white, OutAtlas(), null, 0.1f);
                    m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.45f); m.EnableKeyword("_ALPHATEST_ON"); TilePBR.Apply(m, "Castle_Atlas"); break;
            }
            kitMats[key] = m;
            return m;
        }

        // ================= 北東の塔：食料庫と屋上の天文台への階段（famuru「食料庫に階段を置いて屋上まで。階段は連続させなくてもいい」） =================
        // 反転前の座標で書く（あとで MirrorDorm）。塔の真ん中は建物の角 (12, 15.5)、半径 4.5。建物の内側の4分の1を除いた外側が塔の中。
        // 各階とも建物の北の外壁（x 12）の z 13.7 に扉（1階は台所、2階は娯楽室、3階は図書室から）。
        // 階段は奥の外壁（z 15.5）の外に2本の帯（A1：z 16.6〜17.5、A2：z 17.5〜18.4）。1階→2階は A1 を西へ、2階→3階は A2 を東へ、3階→屋上は A1 を西へ（1本目の真上）。
        static readonly Vector2 NETowerC = new Vector2(12f, 15.5f);
        const float NETowerR = 4.5f, NEInR = 4.2f;
        const float TA1 = 17.05f, TA2 = 17.95f, TBand0 = 16.6f, TBandMid = 17.5f, TBand1 = 18.4f, TXE = 14.0f, TXW = 10.2f, TDoorZ = 13.7f;

        static float TowerChord(float dz, float r) { return dz >= r ? 0f : Mathf.Sqrt(r * r - dz * dz); }

        /// <summary>塔の中の床（f 階）。holeA1・holeA2 は帯の中であけておく x の範囲（x0＞x1 ならあけない）。</summary>
        void TowerFloor(int f, Vector2 holeA1, Vector2 holeA2, Material m)
        {
            float cx = NETowerC.x, cz = NETowerC.y;
            float[] zs = { cz - NEInR + 0.25f, 12.2f, 13.0f, 13.8f, 14.6f, cz, 16.05f, TBand0, TBandMid, TBand1, 18.95f, cz + NEInR - 0.25f };
            for (int i = 0; i + 1 < zs.Length; i++)
            {
                float z0 = zs[i], z1 = zs[i + 1];
                float hc = TowerChord(Mathf.Max(Mathf.Abs(z0 - cz), Mathf.Abs(z1 - cz)), NEInR);
                if (hc < 0.3f) continue;
                float x0 = cx - hc, x1 = cx + hc, zz0 = z0;
                if (z1 <= cz + 0.01f) x0 = cx + WallT / 2f;           // 建物の中の4分の1は除く
                if (Mathf.Abs(z0 - cz) < 0.01f) zz0 = cz + WallT / 2f; // 奥の外壁の外から
                var segs = new List<Vector2> { new Vector2(x0, x1) };
                Vector2 hole = Mathf.Approximately(z0, TBand0) ? holeA1 : (Mathf.Approximately(z0, TBandMid) ? holeA2 : new Vector2(1f, 0f));
                if (hole.x < hole.y)
                {
                    segs.Clear();
                    if (hole.x > x0) segs.Add(new Vector2(x0, hole.x));
                    if (hole.y < x1) segs.Add(new Vector2(hole.y, x1));
                }
                foreach (var s in segs) if (s.y - s.x > 0.05f) Slab("Tower_Floor", f, Rect.MinMaxRect(s.x, zz0, s.y, z1), m);
            }
        }

        /// <summary>塔の中の階段（帯の中、x を xb→xt へ上がる）。</summary>
        void TowerFlight(string name, float xb, float xt, float zc, int lowerFloor)
        {
            float y0 = FloorY(lowerFloor), y1 = FloorY(lowerFloor + 1);
            const int n = 12;
            float run = xt - xb, w = TBandMid - TBand0 - 0.08f;
            for (int i = 0; i < n; i++)
            {
                float x = xb + run * (i + 0.5f) / n, top = y0 + (y1 - y0) * (i + 1) / n;
                Box(name + "_Step", new Vector3(x, top - 0.08f, zc), new Vector3(Mathf.Abs(run) / n + 0.02f, 0.16f, w), woodDark);
            }
            // 下の板（ななめ）
            float len = Mathf.Sqrt(run * run + (y1 - y0) * (y1 - y0));
            var st = Box(name + "_Stringer", new Vector3((xb + xt) / 2f, (y0 + y1) / 2f - 0.2f, zc), new Vector3(len, 0.12f, w), woodDark);
            st.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(y1 - y0, run) * Mathf.Rad2Deg);
            float dir = Mathf.Sign(run);
            stairs.Add(new Stair
            {
                lowerFloor = lowerFloor,
                bottomApproach = new Vector3(xb - dir * 0.6f, y0, zc),
                bottom = new Vector3(xb, y0, zc),
                top = new Vector3(xt, y1, zc),
                topApproach = new Vector3(xt + dir * 0.6f, y1, zc),
                lane = new Vector3(0f, 0f, 0.2f)
            });
        }

        // 3階 → 屋上（天文台 14m）の階段：塔の東寄りを南（z 12.4）から北（z 18.6）へ。屋上の床はその上をあける
        const float ObsStairX = 13.4f, ObsStairZ0 = 12.4f, ObsStairZ1 = 18.6f, ObsHoleZ0 = 12.2f, ObsHoleZ1 = 17.0f;

        /// <summary>塔の中の階段（z を zb→zt へ上がる）。2026-11-08 家具のチャット：屋上を 14m にしたので 3階からの階段を長く。</summary>
        void TowerFlightZ(string name, float x, float zb, float zt, int lowerFloor)
        {
            float y0 = FloorY(lowerFloor), y1 = FloorY(lowerFloor + 1);
            int n = Mathf.Max(12, Mathf.RoundToInt((y1 - y0) / 0.27f));
            float run = zt - zb, w = 0.8f;
            for (int i = 0; i < n; i++)
            {
                float z = zb + run * (i + 0.5f) / n, top = y0 + (y1 - y0) * (i + 1) / n;
                Box(name + "_Step", new Vector3(x, top - 0.08f, z), new Vector3(w, 0.16f, Mathf.Abs(run) / n + 0.02f), woodDark);
            }
            float len = Mathf.Sqrt(run * run + (y1 - y0) * (y1 - y0));
            var st = Box(name + "_Stringer", new Vector3(x, (y0 + y1) / 2f - 0.2f, (zb + zt) / 2f), new Vector3(w, 0.12f, len), woodDark);
            st.transform.localRotation = Quaternion.Euler(-Mathf.Atan2(y1 - y0, run) * Mathf.Rad2Deg, 0f, 0f);
            float dir = Mathf.Sign(run);
            stairs.Add(new Stair
            {
                lowerFloor = lowerFloor,
                bottomApproach = new Vector3(x, y0, zb - dir * 0.45f),
                bottom = new Vector3(x, y0, zb),
                top = new Vector3(x, y1, zt),
                topApproach = new Vector3(x, y1, zt + dir * 0.45f),
                lane = new Vector3(0.2f, 0f, 0f)
            });
        }

        /// <summary>屋上（天文台）の床：北東の塔の円ぜんぶ（寮の屋根より上なので建物の内側の4分の1も）。階段の上はあける。</summary>
        void ObservatoryFloor(Material m)
        {
            float cx = NETowerC.x, cz = NETowerC.y;
            for (float z0 = cz - NEInR + 0.2f; z0 < cz + NEInR - 0.2f - 0.01f; z0 += 0.5f)
            {
                float z1 = Mathf.Min(z0 + 0.5f, cz + NEInR - 0.2f);
                float hc = TowerChord(Mathf.Max(Mathf.Abs(z0 - cz), Mathf.Abs(z1 - cz)), NEInR);
                if (hc < 0.3f) continue;
                float x0 = cx - hc, x1 = cx + hc;
                if (z1 > ObsHoleZ0 && z0 < ObsHoleZ1)
                {
                    float h0 = ObsStairX - 0.45f, h1 = ObsStairX + 0.45f;
                    if (h0 - x0 > 0.05f) Slab("Tower_Floor", 3, Rect.MinMaxRect(x0, z0, h0, z1), m);
                    if (x1 - h1 > 0.05f) Slab("Tower_Floor", 3, Rect.MinMaxRect(h1, z0, x1, z1), m);
                }
                else Slab("Tower_Floor", 3, Rect.MinMaxRect(x0, z0, x1, z1), m);
            }
        }

        void NFoodTower()
        {
            CreatePlaceMaterials(); // 天文台の家具の色（DormPlaces.cs）
            // 床：1階は石、2・3階は木、屋上は石（階段の上はあける）
            TowerFloor(0, new Vector2(1f, 0f), new Vector2(1f, 0f), flagstone);
            TowerFloor(1, new Vector2(TXW, 12.4f), new Vector2(1f, 0f), woodFloor);
            TowerFloor(2, new Vector2(1f, 0f), new Vector2(11.8f, TXE), woodFloor);
            ObservatoryFloor(flagstone);                    // 屋上（天文台。14m。塔の円ぜんぶ）
            TowerFlight("TowerStair1", TXE, TXW, TA1, 0);   // 1階 → 2階（西へ）
            TowerFlight("TowerStair2", TXW, TXE, TA2, 1);   // 2階 → 3階（東へ）
            TowerFlightZ("TowerStair3", ObsStairX, ObsStairZ0, ObsStairZ1, 2); // 3階 → 屋上（7m 上がるので、塔の中を南から北へ長く）
            Label(new Vector3(TXE + 0.4f, 1.6f, TA1), "塔の階段（屋上へ）");
            // 1階：食料庫（たる・木箱・棚）
            PlaceFurn("Furn_BarrelFlour", null, new Vector3(14.9f, 0f, 12.5f), 0f);
            PlaceFurn("Furn_BarrelWine", null, new Vector3(15.6f, 0f, 13.6f), 0f);
            PlaceFurn("Furn_Crate", null, new Vector3(14.0f, 0f, 12.0f), 15f);
            PlaceFurn("Furn_CheeseBreadShelf", null, new Vector3(15.9f, 0f, 15.0f), -90f);
            blockers[0].Add(Rect.MinMaxRect(13.4f, 11.4f, 16.3f, 14.2f));
            blockers[0].Add(Rect.MinMaxRect(15.4f, 14.2f, 16.3f, 15.8f));
            Label(new Vector3(14.5f, 1.8f, 13.4f), "食料庫");
            AddArea("食料庫", Rect.MinMaxRect(WingOut, 11.3f, WingOut + NETowerR, BackOut), 0);
            AddArea("塔の階段", Rect.MinMaxRect(7.8f, BackOut, 16.2f, BackOut + NETowerR), 0);
            AddArea("塔の階段", Rect.MinMaxRect(WingOut, 11.3f, 16.2f, BackOut + NETowerR), 1);
            AddArea("塔の階段", Rect.MinMaxRect(7.8f, BackOut, 12f, BackOut + NETowerR), 1);
            AddArea("塔の階段", Rect.MinMaxRect(WingOut, 11.3f, 16.2f, BackOut + NETowerR), 2);
            AddArea("塔の階段", Rect.MinMaxRect(7.8f, BackOut, 12f, BackOut + NETowerR), 2);
            Lamp(new Vector3(13.5f, 2.6f, 15.8f), 1.4f, 5f);
            Lamp(new Vector3(13.5f, FloorY(1) + 2.6f, 15.8f), 1.4f, 5f);
            Lamp(new Vector3(13.5f, FloorY(2) + 2.6f, 15.8f), 1.4f, 5f);

            // 屋上の天文台（場所。開くまでは3階の階段の下の扉に鍵）
            BeginPlace();
            float y = FloorY(3);
            var tel = new GameObject("Telescope").transform;
            tel.SetParent(root, false);
            tel.localPosition = new Vector3(10.0f, y, 14.2f);
            if (PlaceFurn("Furn_Telescope", tel, Vector3.zero, 0f) == null)
                Cyl("Telescope_Tube", new Vector3(0.05f, 1.45f, 0.25f), new Vector3(0.16f, 0.75f, 0.16f), Quaternion.Euler(55f, 20f, 0f), brassMat, tel);
            blockers[3].Add(new Rect(9.55f, 13.75f, 0.9f, 0.9f));
            if (PlaceFurn("Furn_StarChartTable", null, new Vector3(10.4f, y, 17.4f), 0f) == null)
                Box("StarChart_Table", new Vector3(10.4f, y + 0.75f, 17.4f), new Vector3(1.2f, 0.06f, 0.8f), woodMid);
            blockers[3].Add(new Rect(9.8f, 17.0f, 1.2f, 0.8f));
            if (PlaceFurn("Furn_TerraceRug", null, new Vector3(15.0f, y, 15.3f), 90f, false, new Vector3(0.8f, 1f, 0.8f)) == null)
                Box("Terrace_Rug", new Vector3(15.0f, y + 0.01f, 15.3f), new Vector3(1.8f, 0.02f, 2.2f), rugMat);
            foreach (var p in new[] { new Vector3(15.4f, y, 14.4f), new Vector3(15.5f, y, 15.4f), new Vector3(15.1f, y, 16.3f) })
            {
                if (PlaceFurn("Furn_Cushion", null, p, -90f, false, new Vector3(1.1f, 1.25f, 1.1f)) == null)
                    Box("Cushion", p + Vector3.up * 0.05f, new Vector3(0.55f, 0.1f, 0.55f), fabricGreen);
                seats.Add(new Seat { kind = SeatKind.Sit, floorSeat = true, label = "天文台のクッション", area = Observatory, position = p, forward = new Vector3(-1f, 0f, 0f), approach = p + new Vector3(-0.6f, 0f, 0f) });
            }
            if (PlaceFurn("Furn_Lantern", null, new Vector3(14.6f, y + 0.03f, 16.4f), 0f) == null)
                Box("Lantern", new Vector3(14.6f, y + 0.2f, 16.4f), new Vector3(0.18f, 0.3f, 0.18f), glowLamp);
            Lamp(new Vector3(14.6f, y + 0.8f, 16.4f), 1.2f, 4f);
            var star = MakePoi("望遠鏡で星を見ている", new Vector3(10.2f, y + 1.9f, 14.7f), new Vector3(10.0f, y, 13.45f), new Vector3(10.75f, y, 14.2f));
            star.outdoor = true; star.stars = true;
            pois.Add(star);
            pois.Add(MakePoi("星図を広げている", new Vector3(10.4f, y + 0.8f, 17.4f), new Vector3(10.4f, y, 16.6f)));
            pois[pois.Count - 1].outdoor = true;
            pois.Add(MakePoi("屋上から学園を眺めている", new Vector3(11.2f, y + 1.2f, 22f), new Vector3(10.8f, y, 18.7f), new Vector3(11.8f, y, 18.9f)));
            pois[pois.Count - 1].outdoor = true;
            Label(new Vector3(14.0f, y + 2.4f, 14.6f), "屋上の天文台");
            AddArea(Observatory, Rect.MinMaxRect(7.8f, 11.3f, 16.2f, BackOut + NETowerR), 3);
            PlaceDoorAt(Observatory, 2, new Vector3(ObsStairX, 0f, ObsStairZ0 - 0.2f), true, 0.8f, 2.2f, Rect.MinMaxRect(ObsStairX - 0.4f, ObsStairZ0 - 0.4f, ObsStairX + 0.4f, ObsStairZ0 - 0.05f), woodDark); // 3階の階段の下（南のはし）
            EndPlace(Observatory);
        }

        // ================= 先生の塔・南西の塔の中を、となりの部屋とつなぐ（famuru 2026-11-08「塔の内部が1階、2階、3階と屋内とつながるように」） =================
        // 部屋割りは前に決めた間取り（第5案）のとおり：先生の塔＝1階 先生の個室（今のまま）・2階 小さな談話室（北）・3階 トレーニング部屋、
        // 南西の塔＝1階 倉庫（ランドリーとつなぐ）・2階 小さな談話室（南）・3階 シャワールーム。外壁は NOuterWalls で塔の円の中だけ消してある
        void NTowerRooms()
        {
            Vector2 tc = TeacherTowerC, sc = new Vector2(-WingOut, FrontOut);
            // 南西の塔の床（先生の塔の床は NTeacherTower）
            for (int f = 0; f <= 2; f++)
            {
                var d = Cyl("SWTower_Floor", new Vector3(sc.x, FloorY(f) - 0.07f, sc.y), new Vector3(TowerR * 2f, 0.06f, TowerR * 2f), Quaternion.identity, f == 0 ? flagstone : woodFloor);
                slabRenderers.Add(new KeyValuePair<float, Renderer>(Mathf.Max(0.02f, FloorY(f)), d.GetComponent<Renderer>()));
            }
            // 場所（となりの部屋の名前を塔まで）
            Rect tr = Rect.MinMaxRect(tc.x - TeacherTowerR, tc.y - TeacherTowerR, tc.x + TeacherTowerR, tc.y + TeacherTowerR);
            Rect sr = Rect.MinMaxRect(sc.x - TowerR, sc.y - TowerR, sc.x + TowerR, sc.y + TowerR);
            AddArea("小さな談話室（北）", tr, 1);
            AddArea("トレーニング部屋", tr, 2);
            AddArea("ランドリー", sr, 0);
            AddArea("小さな談話室（南）", sr, 1);
            AddArea("シャワールーム", sr, 2);
            // 1階：南西の塔の倉庫（ランドリーの物置）
            Label(new Vector3(sc.x - 2.2f, 2.0f, sc.y - 2.2f), "倉庫");
            if (PlaceFurn("Furn_Crate", null, new Vector3(sc.x - 2.6f, 0f, sc.y - 2.0f), 20f) == null)
                Box("Crate", new Vector3(sc.x - 2.6f, 0.35f, sc.y - 2.0f), new Vector3(0.7f, 0.7f, 0.7f), woodMid);
            blockers[0].Add(new Rect(sc.x - 3.05f, sc.y - 2.45f, 0.9f, 0.9f));
            if (PlaceFurn("Furn_BarrelWater", null, new Vector3(sc.x - 3.2f, 0f, sc.y - 0.6f), 0f) == null)
                Cyl("Barrel", new Vector3(sc.x - 3.2f, 0.45f, sc.y - 0.6f), new Vector3(0.6f, 0.45f, 0.6f), Quaternion.identity, woodMid);
            blockers[0].Add(new Rect(sc.x - 3.55f, sc.y - 0.95f, 0.7f, 0.7f));
            if (PlaceFurn("Furn_LinenShelf", null, new Vector3(sc.x - 0.6f, 0f, sc.y - 3.6f), 0f) == null)
                Box("LinenShelf", new Vector3(sc.x - 0.6f, 0.95f, sc.y - 3.6f), new Vector3(1.2f, 1.9f, 0.45f), woodDark);
            blockers[0].Add(new Rect(sc.x - 1.25f, sc.y - 3.9f, 1.3f, 0.6f));
            Lamp(new Vector3(sc.x - 1.8f, 2.4f, sc.y - 1.8f), 1.2f, 4.5f);
            // 2・3階：塔の窓辺（窓は 45°・135°・225°。先生の塔は 180° から、南西の塔は 90° から）
            for (int f = 1; f <= 2; f++)
            {
                TowerWindowPois(tc, TeacherTowerR, 180f, f);
                TowerWindowPois(sc, TowerR, 90f, f);
                Lamp(new Vector3(tc.x + 1.8f, FloorY(f) + 2.4f, tc.y - 1.8f), 1.2f, 4.5f);
                Lamp(new Vector3(sc.x - 1.8f, FloorY(f) + 2.4f, sc.y - 1.8f), 1.2f, 4.5f);
            }
        }

        void TowerWindowPois(Vector2 c, float r, float a0, int f)
        {
            float y = FloorY(f);
            foreach (float a in new[] { a0 + 45f, a0 + 135f, a0 + 225f })
            {
                Vector2 d = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
                Vector2 q = c + d * (r - 0.95f), look = c + d * (r + 3f);
                var p = MakePoi("塔の窓から外を眺めている", new Vector3(look.x, y + 1.5f, look.y), new Vector3(q.x, y, q.y));
                pois.Add(p);
            }
        }

        /// <summary>塔の円の中（壁の内側から 0.45m まで）を開ける（反転前の仮の網）。</summary>
        void OpenCornerTower(GridNav n, Vector2 c, float r)
        {
            float rr = r - 0.15f - 0.3f;
            for (float x = c.x - rr; x <= c.x + rr; x += 0.2f)
                for (float z = c.y - rr; z <= c.y + rr; z += 0.2f)
                    if ((x - c.x) * (x - c.x) + (z - c.y) * (z - c.y) <= rr * rr) n.Open(new Rect(x - 0.1f, z - 0.1f, 0.2f, 0.2f));
        }

        /// <summary>塔の壁と外の石の所（建物の外側）をふさぐ。sx は建物のある向き（先生の塔は −x、南西の塔は ＋x の側が建物 → +1／−1）。</summary>
        void ClipCornerTower(GridNav n, Vector2 c, float r, float sx)
        {
            float r0 = r - 0.15f - 0.3f, r1 = r + 0.85f;
            for (int y = 0; y < n.height; y++)
                for (int x = 0; x < n.width; x++)
                {
                    Vector3 p = n.ToWorld(x, y);
                    float dx = p.x - c.x, dz = p.z - c.y, d2 = dx * dx + dz * dz;
                    if (d2 <= r0 * r0 || d2 >= r1 * r1) continue;
                    if (sx * dx < 0f && dz > 0f) continue; // 建物の中
                    n.Block(new Rect(p.x - 0.1f, p.z - 0.1f, 0.2f, 0.2f));
                }
        }

        /// <summary>塔の中の歩ける所（反転前の仮の網に開ける）。</summary>
        void OpenTowerNav(GridNav n, int f)
        {
            float cx = NETowerC.x, cz = NETowerC.y, r = NEInR - 0.3f;
            for (int y = 0; y < n.height; y++)
                for (int x = 0; x < n.width; x++)
                {
                    Vector3 p = n.ToWorld(x, y);
                    float dx = p.x - cx, dz = p.z - cz;
                    if (dx * dx + dz * dz > r * r) continue;
                    if (f <= 2 && p.x < cx + 0.3f && p.z < cz + 0.3f) continue; // 建物の中（屋上の天文台は塔の円ぜんぶ）
                    bool a1 = p.z > TBand0 - 0.1f && p.z < TBandMid, a2 = p.z >= TBandMid && p.z < TBand1 + 0.1f;
                    bool inRun = p.x > TXW - 0.1f && p.x < TXE + 0.1f;
                    bool s3 = Mathf.Abs(p.x - ObsStairX) < 0.55f; // 3階→屋上の階段の帯
                    if (f == 0 && a1 && inRun) continue;
                    if (f == 1 && (a1 || a2) && inRun) continue;
                    if (f == 2 && a2 && inRun) continue;
                    if (f == 2 && s3 && p.z > ObsStairZ0 - 0.15f && p.z < ObsStairZ0 + 1.9f) continue; // 階段の低い所（頭がつかえる）
                    if (f == 3 && s3 && p.z > ObsHoleZ0 - 0.1f && p.z < ObsHoleZ1 + 0.1f) continue;  // 床の穴
                    n.Open(new Rect(p.x - 0.1f, p.z - 0.1f, 0.2f, 0.2f));
                }
            if (f <= 2) n.Open(DoorNav(WingOut, TDoorZ, false, 0.9f)); // 建物の北の外壁の扉
        }

        // ================= 中庭（寮の裏。famuru 2026-11-01「中庭は寮の裏に」） =================
        // 反転前の座標で書く（あとで MirrorDorm）。食堂の裏口（x 2、z 15.5）から屋根のある通路→低い塀の門（z 20.3）→中庭（x −14.4〜13.4、z 20.3〜29.2）。
        // 今の寮の中庭（DormPlaces.BuildCourtyard）と同じ物と見どころを、城壁の内に収まるよう小さくまとめた（温室と工房は本館の北の離れ）。
        const float NCY0 = 20.3f, NCY1 = 29.2f, NCX0 = -14.4f, NCX1 = 13.4f, NCGateX = 2.0f;

        void NCourtyard()
        {
            CreatePlaceMaterials();
            BeginPlace();
            float zc = (NCY0 + NCY1) / 2f;
            Box("Courtyard_Grass", new Vector3((NCX0 + NCX1) / 2f, -0.02f, zc), new Vector3(NCX1 - NCX0, 0.04f, NCY1 - NCY0), grassMat);
            // 低い石の塀（左・右・奥・前。前は門と、露天風呂の囲いのところをあける）
            var sm = StoneFor(18f, 1.4f);
            Box("Yard_WallL", new Vector3(NCX0, 0.7f, zc), new Vector3(0.4f, 1.4f, NCY1 - NCY0), sm);
            Box("Yard_WallR", new Vector3(NCX1, 0.7f, zc), new Vector3(0.4f, 1.4f, NCY1 - NCY0), sm);
            Box("Yard_WallB", new Vector3((NCX0 + NCX1) / 2f, 0.7f, NCY1), new Vector3(NCX1 - NCX0 + 0.4f, 1.4f, 0.4f), sm);
            Box("Yard_WallF1", new Vector3((-6.5f + NCGateX - 1.2f) / 2f, 0.7f, NCY0), new Vector3(NCGateX - 1.2f + 6.5f, 1.4f, 0.4f), sm);
            Box("Yard_WallF2", new Vector3((NCGateX + 1.2f + NCX1) / 2f, 0.7f, NCY0), new Vector3(NCX1 - NCGateX - 1.2f, 1.4f, 0.4f), sm);
            // 食堂の裏口から門までの屋根のある通路
            for (int k = -1; k <= 1; k += 2)
                for (int j = 0; j < 3; j++)
                {
                    float px = NCGateX + k * 1.5f, pz = 16.4f + j * 1.7f;
                    if (PlaceFurn("Furn_Pillar", null, new Vector3(px, 0f, pz), 0f, false, new Vector3(0.18f / 0.4f, 2.7f / 4.5f, 0.18f / 0.4f)) == null)
                        Box("Passage_Post", new Vector3(px, 1.35f, pz), new Vector3(0.18f, 2.7f, 0.18f), woodDark);
                    blockers[0].Add(new Rect(px - 0.15f, pz - 0.15f, 0.3f, 0.3f));
                }
            ceilingRenderers.Add(Box("Passage_Roof", new Vector3(NCGateX, 2.78f, 18.0f), new Vector3(3.4f, 0.1f, 4.4f), mortarDark).GetComponent<Renderer>());
            Box("Yard_Path", new Vector3(NCGateX, -0.005f, 18.0f), new Vector3(2.6f, 0.04f, 4.6f), flagstone);
            Lamp(new Vector3(NCGateX, 2.5f, 18.0f), 1.4f, 5f);
            // 円い広場（行事の屋台を並べられる）・若木・ベンチ・街灯
            Vector3 pc = new Vector3(NCGateX, 0f, 24.6f);
            Cyl("Plaza", pc + Vector3.up * -0.005f, new Vector3(6.4f, 0.025f, 6.4f), Quaternion.identity, flagstone);
            Cyl("Plaza_Ring", pc + Vector3.up * 0.012f, new Vector3(3.4f, 0.01f, 3.4f), Quaternion.identity, rugMat);
            Cyl("Plaza_Center", pc + Vector3.up * 0.016f, new Vector3(2.6f, 0.01f, 2.6f), Quaternion.identity, flagstone);
            Cyl("Plaza_Star", pc + Vector3.up * 0.02f, new Vector3(0.9f, 0.01f, 0.9f), Quaternion.identity, rugGoldMat);
            YardPath(new Vector2(NCGateX, NCY0 - 0.2f), new Vector2(NCGateX, 21.4f));
            SimpleBench("中庭のベンチ", Courtyard, pc + new Vector3(-4.1f, 0f, 0f), 90f, null);
            SimpleBench("中庭のベンチ", Courtyard, pc + new Vector3(4.1f, 0f, 0f), -90f, null);
            SimpleBench("中庭のベンチ", Courtyard, pc + new Vector3(0f, 0f, 3.9f), 180f, null);
            Vector2[] saplings = { new Vector2(pc.x - 2.5f, 22.0f), new Vector2(pc.x + 2.5f, 22.0f) };
            foreach (var s in saplings)
            {
                if (PlaceFurn("Furn_FlowerBed", null, new Vector3(s.x, 0f, s.y), 0f) != null)
                    PlaceFurn("Furn_Sapling", null, new Vector3(s.x, 0.25f, s.y), 0f);
                else
                {
                    Box("FlowerBed", new Vector3(s.x, 0.12f, s.y), new Vector3(1.6f, 0.24f, 1.0f), woodDark);
                    Cyl("Sapling", new Vector3(s.x, 0.7f, s.y), new Vector3(0.06f, 0.45f, 0.06f), Quaternion.identity, woodDark);
                    Leaf(new Vector3(s.x, 1.2f, s.y), 0.55f, leafLightMat);
                }
                blockers[0].Add(new Rect(s.x - 0.8f, s.y - 0.5f, 1.6f, 1.0f));
            }
            LampPost(new Vector3(pc.x - 1.9f, 0f, 22.7f));
            LampPost(new Vector3(pc.x + 1.9f, 0f, 22.7f));
            LampPost(new Vector3(-7.6f, 0f, 21.0f));
            LampPost(new Vector3(8.6f, 0f, 21.0f));
            // 飛行術（左）：浮かぶ輪・ほうき立て
            float fz = 24.2f;
            Box("FlightLine", new Vector3(-7.6f, 0.005f, fz), new Vector3(8f, 0.01f, 0.08f), fabricWhite);
            FloatRing(new Vector3(-10.9f, 2.4f, fz), 0.8f);
            FloatRing(new Vector3(-8.1f, 3.1f, fz), 0.65f);
            FloatRing(new Vector3(-5.2f, 2.2f, fz), 0.75f);
            var rack = new GameObject("BroomRack").transform;
            rack.SetParent(root, false);
            rack.localPosition = new Vector3(-13.6f, 0f, 27.0f);
            if (PlaceFurn("Furn_BroomRack", rack, Vector3.zero, 90f) == null)
                Box("Rack_Frame", new Vector3(0f, 0.9f, 0f), new Vector3(0.3f, 0.08f, 1.8f), woodDark, rack);
            blockers[0].Add(new Rect(-13.85f, 26.0f, 0.5f, 2.0f));
            // 剣術（右）：マット・打ち込み台・杭
            if (PlaceFurn("Furn_TrainingMat", null, new Vector3(9.0f, 0f, 22.4f), 0f) == null)
                Box("Mat1", new Vector3(9.0f, 0.03f, 22.4f), new Vector3(2.6f, 0.06f, 1.8f), fabricRed);
            if (PlaceFurn("Furn_TrainingMat", null, new Vector3(11.2f, 0f, 24.0f), 0f, false, new Vector3(1.6f / 2.6f, 1f, 1.2f / 1.8f)) == null)
                Box("Mat2", new Vector3(11.2f, 0.03f, 24.0f), new Vector3(1.6f, 0.06f, 1.2f), fabricRed);
            var dummy = new GameObject("TrainingDummy").transform;
            dummy.SetParent(root, false);
            dummy.localPosition = new Vector3(12.4f, 0f, 22.2f);
            if (PlaceFurn("Furn_Dummy", dummy, Vector3.zero, 0f) == null)
                Cyl("Dummy_Post", new Vector3(0f, 0.85f, 0f), new Vector3(0.3f, 0.85f, 0.3f), Quaternion.identity, woodMid, dummy);
            blockers[0].Add(new Rect(12.1f, 21.9f, 0.6f, 0.6f));
            for (int i = 0; i < 3; i++)
            {
                if (PlaceFurn("Furn_TrainingPost", null, new Vector3(10.4f + i * 0.9f, 0f, 26.6f), 0f) == null)
                    Cyl("Post", new Vector3(10.4f + i * 0.9f, 0.35f, 26.6f), new Vector3(0.18f, 0.35f, 0.18f), Quaternion.identity, woodDark);
                blockers[0].Add(new Rect(10.3f + i * 0.9f, 26.5f, 0.2f, 0.2f));
            }
            // 木（すみ）
            foreach (var p in new[] { new Vector3(-13.4f, 0f, 21.3f), new Vector3(-3.6f, 0f, 28.3f), new Vector3(7.4f, 0f, 28.3f), new Vector3(12.7f, 0f, 28.2f), new Vector3(-13.3f, 0f, 28.6f) })
                Tree(p, 3.2f + (Mathf.Abs(p.x) % 1f) * 0.3f);
            // 見どころ（だれでも）・自主練習・授業の場所
            pois.Add(MakePoi("中庭をぶらぶらしている", pc + Vector3.up, pc + new Vector3(-1.6f, 0f, -1.2f), pc + new Vector3(1.6f, 0f, 1.3f), pc + new Vector3(-1.8f, 0f, 1.4f)));
            pois[pois.Count - 1].outdoor = true;
            pois.Add(MakePoi("若木を眺めている", new Vector3(saplings[0].x, 1f, 22.0f), new Vector3(saplings[0].x, 0f, 23.0f), new Vector3(saplings[1].x, 0f, 23.0f)));
            pois[pois.Count - 1].outdoor = true;
            var fly = MakePoi("ほうきで飛ぶ練習をしている", new Vector3(-8.1f, 3f, fz), new Vector3(-12.2f, 0f, fz - 0.7f), new Vector3(-6.4f, 0f, fz - 1.0f));
            fly.subject = Academics.Flight; fly.outdoor = true;
            pois.Add(fly);
            var kick = MakePoi("打ち込み台で剣術の練習をしている", new Vector3(12.4f, 1.1f, 22.2f), new Vector3(11.6f, 0f, 22.3f), new Vector3(12.4f, 0f, 23.1f));
            kick.subject = Academics.Sword; kick.outdoor = true;
            pois.Add(kick);
            var flyClass = ClassPoi("飛行術の授業を受けている", Academics.Flight, new Vector3(-8.1f, 2.5f, fz - 0.6f), -12.4f, fz + 1.0f, 1.2f, 0f, 7, 1);
            flyClass.outdoor = true;
            pois.Add(flyClass);
            var bodyClass = ClassPoi("剣術の授業を受けている", Academics.Sword, new Vector3(9.0f, 0.5f, 22.4f), 7.6f, 21.8f, 0.85f, 2.4f, 4, 2);
            bodyClass.outdoor = true;
            pois.Add(bodyClass);
            Label(new Vector3(pc.x, 2.2f, 21.4f), "中庭");
            AddArea(Courtyard, Rect.MinMaxRect(NCX0 + 0.2f, NCY0 + 0.2f, NCX1 - 0.2f, NCY1 - 0.2f), 0);
            PlaceDoorAt(Courtyard, 0, new Vector3(NCGateX, 0f, NCY0), true, 2.4f, 2.4f, Rect.MinMaxRect(NCGateX - 1.05f, NCY0 - 0.5f, NCGateX + 1.05f, NCY0 + 0.5f), woodDark);
            EndPlace(Courtyard);
        }

        // ================= 温室と工房（本館の北の離れ。今の寮の温室・工房をそのまま −90° 回して置く。反転しない学園の座標） =================
        readonly List<Rect> outsidePlaceNav = new List<Rect>();

        /// <summary>今の寮の場所の組み立て（扉が −z 向き）を、扉が ＋x（配置図の南）を向くよう −90° 回して off だけずらす。物と席・見どころ・壁・ふさぐ所・場所・札・扉もまとめて写す。</summary>
        void PlaceRotated(System.Action build, Vector3 off, Rect navRaw)
        {
            int c0 = root.childCount, w0 = walls.Count, s0 = seats.Count, p0 = pois.Count, a0 = areaList.Count, l0 = worldLabels.Count, d0 = placeDoors.Count;
            var bl0 = new int[4]; for (int f = 0; f < 4; f++) bl0[f] = blockers[f].Count;
            var keys0 = new HashSet<string>(placeSeats.Keys);
            build();
            var holder = new GameObject("Place_Rotated").transform;
            holder.SetParent(root, false);
            holder.localPosition = off;
            holder.localRotation = Quaternion.Euler(0f, -90f, 0f);
            var kids = new List<Transform>();
            for (int i = c0; i < root.childCount; i++) { var t = root.GetChild(i); if (t != holder) kids.Add(t); }
            foreach (var t in kids) t.SetParent(holder, false);
            System.Func<Vector3, Vector3> P = v => new Vector3(-v.z + off.x, v.y, v.x + off.z);
            System.Func<Vector3, Vector3> D = v => new Vector3(-v.z, v.y, v.x);
            System.Func<Vector2, Vector2> P2 = v => new Vector2(-v.y + off.x, v.x + off.z);
            System.Func<Rect, Rect> R = r => Rect.MinMaxRect(-r.yMax + off.x, r.xMin + off.z, -r.yMin + off.x, r.xMax + off.z);
            for (int i = w0; i < walls.Count; i++) { walls[i].a = P2(walls[i].a); walls[i].b = P2(walls[i].b); }
            for (int f = 0; f < 4; f++) for (int i = bl0[f]; i < blockers[f].Count; i++) blockers[f][i] = R(blockers[f][i]);
            var seen = new HashSet<object>();
            System.Action<Seat> ms = s => { if (s == null || !seen.Add(s)) return; s.position = P(s.position); s.approach = P(s.approach); s.forward = D(s.forward); };
            System.Action<Poi> mp = p => { if (p == null || !seen.Add(p)) return; p.lookAt = P(p.lookAt); if (p.slots != null) for (int k = 0; k < p.slots.Length; k++) p.slots[k] = P(p.slots[k]); };
            for (int i = s0; i < seats.Count; i++) ms(seats[i]);
            for (int i = p0; i < pois.Count; i++) mp(pois[i]);
            foreach (var kv in placeSeats) if (!keys0.Contains(kv.Key)) foreach (var s in kv.Value) ms(s);
            foreach (var kv in placePois) if (!keys0.Contains(kv.Key)) foreach (var p in kv.Value) mp(p);
            for (int i = a0; i < areaList.Count; i++) areaList[i].rect = R(areaList[i].rect);
            for (int i = l0; i < worldLabels.Count; i++) worldLabels[i] = new KeyValuePair<Vector3, string>(P(worldLabels[i].Key), worldLabels[i].Value);
            for (int i = d0; i < placeDoors.Count; i++) placeDoors[i].nav = R(placeDoors[i].nav);
            outsidePlaceNav.Add(R(navRaw));
        }

        void NPlacesOutside()
        {
            outsidePlaceNav.Clear();
            // 温室：配置図 (−59, 99.5)、扉は南（y 96.75）。工房：配置図 (−46, 100)、扉は南（y 97.25）
            PlaceRotated(BuildGreenhouse, new Vector3(-3.75f, 0f, -113f), Rect.MinMaxRect(-10.75f, 17.25f, -4.25f, 22.25f));
            PlaceRotated(BuildWorkshop, new Vector3(-4.25f, 0f, -116f), Rect.MinMaxRect(5.25f, 17.25f, 11.75f, 22.25f));
            NewFocus[Greenhouse] = new Vector3(-19.5f - 3.75f, 0f, -7.5f - 113f);
            NewFocus[Workshop] = new Vector3(-19.8f - 4.25f, 0f, 8.5f - 116f);
        }

        // ================= ランドリーと物干し場（famuru 2026-11-02「寮にランドリーと衣服を干す場所」。1階の倉庫だった部屋＋外の南の芝生） =================
        // 反転前の座標で書く（あとで MirrorDorm）。部屋は x −12〜−5.2・z −15.5〜−9.5、外への扉は南の外壁（x −12）の z −10.6。物干し場は外の x −18.6〜−12.4・z −11.4〜−1。
        // 家具は依頼22（Furn_WashTub・LaundryBasket・IroningTable・LinenShelf・ClothesLine・ClothesLineEmpty）。届くまでは箱で組み、届けば自動で入れかわる。
        const float LaundryDoorZ = -10.0f; // 南西の塔（半径 4.5m）とぶつからないよう −10.6 から寄せた（2026-11-08）
        static readonly Rect DryYard = Rect.MinMaxRect(-18.6f, -11.4f, -12.4f, -1.0f);

        void NLaundry()
        {
            float y = 0f;
            Label(new Vector3(-8.6f, 2.2f, -12.6f), "ランドリー");
            AddArea("ランドリー", Rect.MinMaxRect(-WingOut, FrontOut, -WingIn, FrontZ), 0);
            // 洗濯桶（前の壁ぎわに2台）
            foreach (float x in new[] { -10.9f, -9.3f })
            {
                if (PlaceFurn("Furn_WashTub", null, new Vector3(x, y, -15.0f), 0f) == null)
                {
                    Box("WashTub_Stand", new Vector3(x, 0.3f, -15.0f), new Vector3(1.4f, 0.6f, 0.7f), woodDark);
                    Cyl("WashTub_A", new Vector3(x - 0.35f, 0.75f, -15.0f), new Vector3(0.6f, 0.15f, 0.6f), Quaternion.identity, woodMid);
                    Cyl("WashTub_B", new Vector3(x + 0.35f, 0.75f, -15.0f), new Vector3(0.6f, 0.15f, 0.6f), Quaternion.identity, woodMid);
                    Cyl("WashTub_Water", new Vector3(x - 0.35f, 0.9f, -15.0f), new Vector3(0.5f, 0.01f, 0.5f), Quaternion.identity, waterMat);
                }
            }
            blockers[0].Add(Rect.MinMaxRect(-11.65f, FrontOut, -8.55f, -14.6f));
            PlaceFurn("Furn_BarrelWater", null, new Vector3(-7.9f, y, -15.0f), 0f);
            blockers[0].Add(Rect.MinMaxRect(-8.3f, FrontOut, -7.5f, -14.6f));
            // リネンの棚（談話室との壁ぎわ）
            if (PlaceFurn("Furn_LinenShelf", null, new Vector3(-WingIn - 0.4f, y, -13.3f), -90f) == null)
                Box("LinenShelf", new Vector3(-WingIn - 0.4f, 0.95f, -13.3f), new Vector3(0.45f, 1.9f, 1.2f), woodDark);
            blockers[0].Add(Rect.MinMaxRect(-WingIn - 0.7f, -13.95f, -WingIn, -12.65f));
            // アイロン台
            if (PlaceFurn("Furn_IroningTable", null, new Vector3(-8.4f, y, -11.8f), 0f) == null)
                Box("IroningTable", new Vector3(-8.4f, 0.82f, -11.8f), new Vector3(1.3f, 0.06f, 0.45f), fabricWhite);
            blockers[0].Add(Rect.MinMaxRect(-9.1f, -12.1f, -7.7f, -11.5f));
            // 洗濯かご
            foreach (var p in new[] { new Vector3(-11.1f, y, -13.2f), new Vector3(-10.2f, y, -13.9f) })
            {
                if (PlaceFurn("Furn_LaundryBasket", null, p, 20f) == null && PlaceFurn("Furn_Basket", null, p, 20f) == null)
                    Box("LaundryBasket", p + Vector3.up * 0.2f, new Vector3(0.6f, 0.4f, 0.45f), woodMid);
                blockers[0].Add(new Rect(p.x - 0.35f, p.z - 0.3f, 0.7f, 0.6f));
            }
            Lamp(new Vector3(-8.6f, 2.4f, -12.5f), 1.6f, 6f);
            pois.Add(MakePoi("洗濯をしている", new Vector3(-10.1f, 0.9f, -15.0f), new Vector3(-10.9f, y, -14.25f), new Vector3(-9.3f, y, -14.25f)));
            pois.Add(MakePoi("アイロンをかけている", new Vector3(-8.4f, 0.9f, -11.8f), new Vector3(-8.4f, y, -11.05f)));
            pois.Add(MakePoi("洗濯物をたたんでいる", new Vector3(-WingIn - 0.4f, 1.0f, -13.3f), new Vector3(-6.35f, y, -13.3f), new Vector3(-6.35f, y, -12.4f)));

            // 物干し場（外の南の芝生）：3列×2本。雨の日と夜は洗濯物を取り込んだ形（LaundryLines）
            var full = new GameObject("DryYard_Full").transform; full.SetParent(root, false);
            var empty = new GameObject("DryYard_Empty").transform; empty.SetParent(root, false);
            var lineSpots = new List<Vector3>();
            foreach (float x in new[] { -13.9f, -15.7f, -17.5f })
                foreach (float z in new[] { -8.6f, -4.2f })
                {
                    var c = new Vector3(x, y, z);
                    if (PlaceFurn("Furn_ClothesLine", full, c, 90f) == null) ClothesLineBox(full, c, true);
                    if (PlaceFurn("Furn_ClothesLineEmpty", empty, c, 90f) == null) ClothesLineBox(empty, c, false);
                    blockers[0].Add(new Rect(x - 0.2f, z - 1.7f, 0.4f, 0.4f));
                    blockers[0].Add(new Rect(x - 0.2f, z + 1.3f, 0.4f, 0.4f));
                    lineSpots.Add(c);
                }
            root.gameObject.AddComponent<LaundryLines>().Set(full.gameObject, empty.gameObject);
            Box("DryYard_Path", new Vector3(-12.9f, -0.01f, LaundryDoorZ), new Vector3(1.2f, 0.04f, 1.4f), flagstone);
            var hang = MakePoi("洗濯物を干している", lineSpots[0] + Vector3.up * 1.6f, lineSpots[0] + new Vector3(0.75f, 0f, 0f), lineSpots[2] + new Vector3(0.75f, 0f, 0f), lineSpots[3] + new Vector3(0.75f, 0f, 0f), lineSpots[5] + new Vector3(0.75f, 0f, 0f));
            hang.outdoor = true;
            pois.Add(hang);
            Label(new Vector3(-15.7f, 2.2f, -6.4f), "物干し場");
            AddArea("物干し場", DryYard, 0);
        }

        /// <summary>物干しの仮の形（依頼22 が届くまで）。</summary>
        void ClothesLineBox(Transform parent, Vector3 c, bool clothes)
        {
            Box("Line_PostA", c + new Vector3(0f, 0.95f, -1.5f), new Vector3(0.08f, 1.9f, 0.08f), woodDark, parent);
            Box("Line_PostB", c + new Vector3(0f, 0.95f, 1.5f), new Vector3(0.08f, 1.9f, 0.08f), woodDark, parent);
            Box("Line_Pole", c + new Vector3(0f, 1.82f, 0f), new Vector3(0.04f, 0.04f, 3.0f), woodMid, parent);
            if (!clothes) return;
            Box("Line_Sheet", c + new Vector3(0f, 1.3f, -0.6f), new Vector3(0.02f, 1.0f, 1.3f), fabricWhite, parent);
            Box("Line_Shirt", c + new Vector3(0f, 1.5f, 0.55f), new Vector3(0.02f, 0.6f, 0.5f), fabricWhite, parent);
            Box("Line_Towel", c + new Vector3(0f, 1.55f, 1.15f), new Vector3(0.02f, 0.5f, 0.4f), rugMat, parent);
        }

        // ================= 露天風呂（南東のすみ。浴場から出る） =================
        void NSpa()
        {
            float x0 = -WingOut - 4.5f, x1 = -6.5f, z0 = BackOut, z1 = BackOut + 4.5f;
            Box("Spa_Floor", new Vector3((x0 + x1) / 2f, -0.04f, (z0 + z1) / 2f), new Vector3(x1 - x0, 0.08f, z1 - z0), flagstone);
            var sm = StoneFor(10f, 1.6f);
            Box("Spa_WallS", new Vector3((x0 + x1) / 2f, 0.8f, z1), new Vector3(x1 - x0, 1.6f, 0.3f), sm);
            Box("Spa_WallW", new Vector3(x0, 0.8f, (z0 + z1) / 2f), new Vector3(0.3f, 1.6f, z1 - z0), sm);
            Box("Spa_WallE", new Vector3(x1, 0.8f, (z0 + z1) / 2f), new Vector3(0.3f, 1.6f, z1 - z0), sm);
            Vector3 pc = new Vector3(-12.5f, 0f, BackOut + 2.4f);
            Cyl("Spa_Rim", pc + Vector3.up * 0.2f, new Vector3(3.6f, 0.2f, 3.6f), Quaternion.identity, mortarDark);
            Cyl("Spa_Water", pc + Vector3.up * 0.38f, new Vector3(3.1f, 0.02f, 3.1f), Quaternion.identity, waterMat);
            blockers[0].Add(Rect.MinMaxRect(pc.x - 1.8f, pc.z - 1.8f, pc.x + 1.8f, pc.z + 1.8f));
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI / 2f + 0.4f;
                Vector3 sp = pc + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 1.0f;
                seats.Add(new Seat { kind = SeatKind.Bath, label = "露天風呂", area = "浴場", position = sp, forward = (pc - sp).normalized, approach = pc + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 2.3f });
            }
            Label(new Vector3(pc.x, 1.6f, pc.z), "露天風呂");
            AddArea("浴場", Rect.MinMaxRect(x0, z0, x1, z1), 0);
            Lamp(new Vector3(pc.x, 2.2f, pc.z), 1.5f, 5f);
        }

        // ================= 前庭と渡り廊下（2階の入口の通路から学校の側へ） =================
        void NFrontYardAndBridge()
        {
            Box("Yard_FrontPath", new Vector3(UseV3 ? DormDoorX : 1.5f, -0.02f, FrontOut - 4f), new Vector3(2.4f, 0.04f, 8f), flagstone);
            Bench(new Vector3(-2.6f, 0f, FrontOut - 2.0f), 180f);
            Label(new Vector3(1.5f, 1.6f, FrontOut - 3f), "前庭");
            AddArea("前庭", Rect.MinMaxRect(-8f, FrontOut - 8f, 8f, FrontOut), 0);
            float y = FloorY(1), x0 = PassX0, x1 = PassX1, z0 = SchoolWestZ, z1 = FrontOut;
            Slab("Bridge_Floor", 1, Rect.MinMaxRect(x0, z0, x1, z1), woodFloor);
            Rail(1, new Vector2(x0 + 0.05f, z0), new Vector2(x0 + 0.05f, z1));
            Rail(1, new Vector2(x1 - 0.05f, z0), new Vector2(x1 - 0.05f, z1));
            for (int k = 0; k < 2; k++)
            {
                float x = k == 0 ? x0 + 0.2f : x1 - 0.2f;
                foreach (float bzp in new[] { z0 + 0.5f, FrontOut - 5.5f })
                {
                    Box("Bridge_Pillar", new Vector3(x, y / 2f, bzp), new Vector3(0.35f, y, 0.35f), mortarDark);
                    blockers[0].Add(new Rect(x - 0.2f, bzp - 0.2f, 0.4f, 0.4f));
                }
            }
            Label(new Vector3((x0 + x1) / 2f, y + 2.0f, z0 + 1.0f), "渡り廊下（学校へ）");
            AddArea("外", Rect.MinMaxRect(x0, z0, x1, z1), 1);
        }

        // ================= 屋根（高さの違う屋根の組み合わせ。屋上には出ない） =================
        void NRoof()
        {
            float y = EaveY;
            KitRoofZ(BackZ, BackOut, 0f, 24.6f, y, 6.0f);               // 奥の帯（いちばん高い。棟は x 方向…ではなく z の向きに長い）
            KitRoofZ(FrontOut, FrontZ, 0f, 24.6f, y, 4.6f);             // 手前の帯
            KitRoofX(FrontZ, BackZ, 8.6f, 7.4f, y, 4.8f);               // 北の個室の棟
            KitRoofX(FrontZ, BackZ, -8.6f, 7.4f, y, 4.8f);              // 南の個室の棟
            KitRoofX(FrontZ, BackZ, 0f, 10.4f, y, 2.6f);                // 吹き抜けの上（低い）
            for (int i = 0; i < 4; i++) { PlaceKit("Dormer", new Vector3(WingOut - 2.2f, y + 0.8f, -8f + i * 3.8f), 90f, Vector3.one); PlaceKit("Dormer", new Vector3(-WingOut + 2.2f, y + 0.8f, -8f + i * 3.8f), -90f, Vector3.one); }
            PlaceKit("Chimney", new Vector3(FireplaceSpot.x, y + 4.4f, BackZ + 0.6f), 0f, new Vector3(1.2f, 1.6f, 1.2f));
        }

        /// <summary>x の向きに棟が走る屋根（建物の z0〜z1 の区間を、中心 xc・幅 span で覆う。部品は z に並べる）。</summary>
        void KitRoofX(float z0, float z1, float xc, float span, float baseH, float roofH)
        {
            int k = Mathf.Max(1, Mathf.RoundToInt((z1 - z0) / 4f));
            float seg = (z1 - z0) / k;
            for (int s = 0; s < k; s++) PlaceKit("Roof_Gable", new Vector3(xc, baseH, z0 + seg * (s + 0.5f)), 90f, new Vector3(seg / 4f, roofH / 6f, span / 10f));
        }

        /// <summary>z の帯（z0〜z1）を、x の向きに長い屋根で覆う（棟は x）。</summary>
        void KitRoofZ(float z0, float z1, float xc, float len, float baseH, float roofH)
        {
            int k = Mathf.Max(1, Mathf.RoundToInt(len / 4f));
            float seg = len / k, span = z1 - z0 + 0.6f, zc = (z0 + z1) / 2f;
            for (int s = 0; s < k; s++) PlaceKit("Roof_Gable", new Vector3(xc - len / 2f + seg * (s + 0.5f), baseH, zc), 0f, new Vector3(seg / 4f, roofH / 6f, span / 10f));
            PlaceKit("Roof_GableEnd", new Vector3(xc - len / 2f, baseH, zc), 90f, new Vector3(span / 10f, roofH / 6f, 1f));
            PlaceKit("Roof_GableEnd", new Vector3(xc + len / 2f, baseH, zc), -90f, new Vector3(span / 10f, roofH / 6f, 1f));
        }

        // ================= 学校の本館・体育館・大講堂・中庭（SchoolBuilder をそのまま使う） =================
        // 設計図（sx 西→東 0〜70、sy 北→南 0〜46）を 1m＝1 で組む：プログラムの x＝32−sy、z＝−26.5−sx。
        // 配置図（famuru 決定）の向きでは、教室の棟が北、体育館と大講堂と中庭が南、中庭は南に開く（設計図の北＝配置図の南）。
        // 設計図の西のはし（裏口・外廊下）が寮の側（z −26.5）。寮の渡り廊下は本館の2階の大廊下の西のはしへ、前庭から1階の裏口へ。
        // 2026-11-01 寮を左右反転（MirrorDorm）にしたので、学校は反転しない。
        const float SchoolWestZ = -26.5f;
        /// <summary>正門の内側（先生が来て帰る所）。配置図の正門の内側 (0, 12)。</summary>
        public static Vector3 NewSchoolGate { get { return CampusPlan.G(0f, CampusPlan.V3 ? 14.8f : 12f); } } // 依頼28 の正門は奥行きが深い（北の面が y 13.7）ので、そのすぐ内側（2026-11-07 家具のチャット）

        void NSchool()
        {
            BuildSchool();
        }

        static Vector3 MX(Vector3 v) { return new Vector3(-v.x, v.y, v.z); }
        static Vector2 MX(Vector2 v) { return new Vector2(-v.x, v.y); }
        static Rect MX(Rect r) { return Rect.MinMaxRect(-r.xMax, r.yMin, -r.xMin, r.yMax); }

        /// <summary>
        /// 寮を x＝0 を軸に左右反転する（配置図と同じ向きにするため。2026-11-01 famuru「図のとおりに直す」）。
        /// 寮の組み立ては今の寮と同じ向き（＋x が北の棟）で書いてあるので、組み終わってから建物と、席・見どころ・場所・部屋・階段・壁などの位置をまとめて写す。
        /// </summary>
        void MirrorDorm()
        {
            var holder = new GameObject("Dorm_Mirror").transform;
            holder.SetParent(root, false);
            var kids = new List<Transform>();
            for (int i = 0; i < root.childCount; i++) { var t = root.GetChild(i); if (t != holder) kids.Add(t); }
            foreach (var t in kids) t.SetParent(holder, true);
            holder.localScale = new Vector3(-1f, 1f, 1f);

            foreach (var w in walls) { w.a = MX(w.a); w.b = MX(w.b); }
            for (int f = 0; f < 4; f++) for (int i = 0; i < blockers[f].Count; i++) blockers[f][i] = MX(blockers[f][i]);
            var seen = new HashSet<object>();
            System.Action<Seat> ms = s =>
            {
                if (s == null || !seen.Add(s)) return;
                s.position = MX(s.position); s.approach = MX(s.approach); s.forward = MX(s.forward);
            };
            System.Action<Poi> mp = p =>
            {
                if (p == null || !seen.Add(p)) return;
                p.lookAt = MX(p.lookAt);
                if (p.slots != null) for (int k = 0; k < p.slots.Length; k++) p.slots[k] = MX(p.slots[k]);
            };
            foreach (var s in seats) ms(s);
            foreach (var s in mobSeats) ms(s);
            foreach (var s in classSeats) ms(s);
            foreach (var s in teachSpots) ms(s);
            foreach (var p in pois) mp(p);
            foreach (var kv in placeSeats) foreach (var s in kv.Value) ms(s);
            foreach (var kv in placePois) foreach (var p in kv.Value) mp(p);
            foreach (var r in rooms)
            {
                ms(r.bed); mp(r.desk); ms(r.futon);
                foreach (var c in r.cushions) ms(c);
                r.labelPos = MX(r.labelPos); r.rect = MX(r.rect);
                r.bedEdge = MX(r.bedEdge); r.bedAcross = MX(r.bedAcross); r.bedSitAlong = MX(r.bedSitAlong);
            }
            foreach (var a in areaList) a.rect = MX(a.rect);
            for (int i = 0; i < worldLabels.Count; i++) worldLabels[i] = new KeyValuePair<Vector3, string>(MX(worldLabels[i].Key), worldLabels[i].Value);
            foreach (var s in stairs)
            {
                s.bottomApproach = MX(s.bottomApproach); s.bottom = MX(s.bottom); s.top = MX(s.top); s.topApproach = MX(s.topApproach); s.lane = MX(s.lane);
            }
            foreach (var d in placeDoors) d.nav = MX(d.nav);
            // 再生のたびに ApplyLayout が反転前の値を入れ直すので、ここで1回だけ反転する
            LoungeCenter = MX(LoungeCenter); BirthdaySpot = MX(BirthdaySpot); FireplaceSpot = MX(FireplaceSpot); DiningCenter = MX(DiningCenter);
            LoungeWanderRect = MX(LoungeWanderRect);
        }

        // ================= 学校の外見（屋根・塔。依頼20の部品） =================
        void NSchoolOutside()
        {
            float y3 = EaveY, y2 = FloorY(2);
            // 教室の棟（x −14〜7、z −96.5〜−26.5・3階建て）：棟が z に走る大きな屋根と、両はしの妻
            KitRoofX(-96.5f, -26.5f, -3.5f, 21.8f, y3, 7.5f);
            PlaceKit("Roof_GableEnd", new Vector3(-3.5f, y3, -96.5f), 0f, new Vector3(21.8f / 10f, 7.5f / 6f, 1f));
            PlaceKit("Roof_GableEnd", new Vector3(-3.5f, y3, -26.5f), 180f, new Vector3(21.8f / 10f, 7.5f / 6f, 1f));
            for (int i = 0; i < 6; i++)
            {
                float z = -88.5f + i * 11f;
                PlaceKit("Dormer", new Vector3(-12f, y3 + 0.9f, z), -90f, Vector3.one); // 北の屋根
                PlaceKit("Dormer", new Vector3(5f, y3 + 0.9f, z + 5.5f), 90f, Vector3.one); // 中庭の側
            }
            // 体育館（z −44.5〜−26.5）・大講堂（z −96.5〜−78.5）：x 9〜32、2階分の高さ
            KitRoofZ(-44.5f, -26.5f, 20.5f, 23.4f, y2, 6f);
            KitRoofZ(-96.5f, -78.5f, 20.5f, 23.4f, y2, 6f);
            // 丸い塔：教室の棟の北の2つのすみと、北の面の真ん中（外にくっつける）
            SchoolTower(new Vector3(-16.6f, 0f, -23.9f), 4);
            SchoolTower(new Vector3(-16.6f, 0f, -99.1f), 4);
            SchoolTower(new Vector3(-17.6f, 0f, -61.5f + (UseV3 ? 9f : 0f)), 5); // 2026-11-07 家具のチャット：真ん中の塔が本館の北の玄関の真ん前をふさいでいたので、新しい入口（依頼28）を置くときは東へ 9m ずらす
        }

        void SchoolTower(Vector3 c, int floors)
        {
            PlaceKit("Tower_Round_Base", c, 0f, Vector3.one);
            for (int f = 1; f < floors; f++) PlaceKit("Tower_Round", c + Vector3.up * (f * FloorH), (f % 2) * 45f, Vector3.one);
            PlaceKit("Roof_Cone", c + Vector3.up * (floors * FloorH), 0f, Vector3.one);
            blockers[0].Add(Rect.MinMaxRect(c.x - 3.6f, c.z - 3.6f, c.x + 3.6f, c.z + 3.6f));
        }

        // ================= 歩ける所 =================
        /// <summary>寮の部分を反転前の座標で開けた仮の網を、左右反転して本当の網に写す。</summary>
        static void CopyMirrored(GridNav src, GridNav dst)
        {
            float c = src.cell;
            for (int y = 0; y < src.height; y++)
                for (int x = 0; x < src.width; x++)
                {
                    if (!src.IsFree(x, y)) continue;
                    float wx = src.min.x + (x + 0.5f) * c, wz = src.min.y + (y + 0.5f) * c;
                    dst.Open(new Rect(-wx - c * 0.4f, wz - c * 0.4f, c * 0.8f, c * 0.8f));
                }
        }

        void NNav()
        {
            float inflate = 0.1f;
            // 本当の網：寮・学校・学園の道（配置図の正門からほかの寮まで）
            for (int f = 0; f <= 2; f++) navs[f] = new GridNav(new Vector2(-30f, -142f), new Vector2(82f, 30f), 0.25f, FloorY(f));
            navs[0] = new GridNav(new Vector2(-40f, -142f), new Vector2(82f, 78f), 0.25f, FloorY(0)); // 1階は北東の城壁の外の浜辺と桟橋まで（2026-11-08 家具のチャット：プールと浜辺で過ごせるように）
            navs[3] = new GridNav(new Vector2(-17.5f, 10f), new Vector2(-6.5f, 21f), 0.25f, FloorY(3)); // 屋上（北東の塔の天文台だけ）
            // ---- 寮（反転前の座標で仮の網に開ける） ----
            var t = new GridNav[4];
            for (int f = 0; f <= 3; f++) t[f] = new GridNav(new Vector2(-20f, -30f), new Vector2(20f, 30f), 0.25f, FloorY(f));
            var n0 = t[0];
            n0.Open(In(-WingIn, FrontOut, WingIn, BackZ));
            n0.Open(Rect.MinMaxRect(-WingCorr + 0.25f, FrontZ + 0.25f, WingCorr - 0.25f, BackZ - 0.25f));
            for (int s = -1; s <= 1; s += 2)
                for (int i = 0; i < 5; i++)
                {
                    float cz = FrontZ + RoomW * (i + 0.5f);
                    n0.Open(In(s * WingCorr, cz - RoomW / 2f, s * WingOut, cz + RoomW / 2f));
                    n0.Open(DoorNav(s * WingCorr, cz, false, 1.2f));
                }
            n0.Open(In(-2.5f, BackZ, KitchenX0, BackOut));     // 食堂
            n0.Open(In(KitchenX0, BackZ, WingOut, BackOut));    // 台所
            n0.Open(In(-5f, BackZ, -2.5f, BackOut));            // 脱衣所
            n0.Open(In(-WingOut, BackZ, -5f, BackOut));         // 浴場
            n0.Open(DoorNav(6.3f, BackZ, true, 1.2f));
            n0.Open(DoorNav(KitchenX0, KitchenDoorZ, false, 1.0f));
            n0.Open(DoorNav(-2.5f, 13.6f, false, 0.9f));
            n0.Open(DoorNav(-5f, 8.0f, false, 0.9f));
            n0.Open(In(WingIn, FrontOut, WingOut, FrontZ));     // 先生の共用の部屋
            n0.Open(In(-WingOut, FrontOut, -WingIn, FrontZ));   // ランドリー（もとの倉庫）
            n0.Open(DoorNav(-WingOut, LaundryDoorZ, false, 0.9f)); // ランドリー→物干し場
            n0.Open(In(DryYard.xMin, DryYard.yMin, DryYard.xMax, DryYard.yMax, 0.2f)); // 物干し場
            n0.Open(DoorNav(6.2f, FrontZ, true));
            n0.Open(DoorNav(-6.2f, FrontZ, true));
            // 先生の個室（塔）と扉
            n0.Open(In(8.6f, -18.9f, 12f, -15.5f));
            n0.Open(In(12f, -15.5f, 15.4f, -12.1f));
            n0.Open(In(12f, -18.9f, 15.4f, -15.5f));
            n0.Open(DoorNav(9.4f, FrontOut, true, 0.9f));
            n0.Open(DoorNav(WingOut, -12.9f, false, 0.9f));
            n0.Open(DoorNav(12.65f, -15.5f, true, 0.9f));
            // 入口・前庭・庭・露天風呂
            if (UseV3) n0.Open(Rect.MinMaxRect(DormDoorX - 0.6f, FrontOut - 0.8f, DormDoorX + 0.6f, FrontOut + 0.4f)); // 依頼28 の入口の口に合わせる
            else n0.Open(Rect.MinMaxRect(0.75f, FrontOut - 0.6f, 2.25f, FrontOut + 0.4f));
            n0.Open(Rect.MinMaxRect(-8f + 0.25f, FrontOut - 8f + 0.25f, 8f - 0.25f, FrontOut - 0.45f));
            n0.Open(Rect.MinMaxRect(1.25f, BackOut - 0.4f, 2.75f, BackOut + 0.6f));
            n0.Open(Rect.MinMaxRect(-6f, BackOut + 0.45f, 7.3f, NCY0 - 0.45f));             // 寮の裏の庭（東は北東の塔まで、奥は中庭の塀まで）
            n0.Open(Rect.MinMaxRect(NCX0 + 0.45f, NCY0 + 0.45f, NCX1 - 0.45f, NCY1 - 0.45f)); // 中庭（門は場所の扉：ApplyPlaces で開け閉め）
            n0.Open(Rect.MinMaxRect(-9.85f, BackOut - 0.4f, -9.35f, BackOut + 0.6f));
            n0.Open(In(-WingOut - 4.5f, BackOut, -6.5f, BackOut + 4.5f, 0.4f));            // 露天風呂
            n0.Open(Rect.MinMaxRect(2.5f, SchoolWestZ - 0.7f, 4.5f, FrontOut - 7.5f));     // 前庭から学校の裏口へ
            for (int f = 1; f <= 2; f++)
            {
                var n = t[f];
                n.Open(Rect.MinMaxRect(WingIn + 0.25f, FrontZ + 0.25f, WingCorr - 0.25f, BackZ - 0.25f));
                n.Open(Rect.MinMaxRect(-WingCorr + 0.25f, FrontZ + 0.25f, -WingIn - 0.25f, BackZ - 0.25f));
                n.Open(Rect.MinMaxRect(-WingCorr + 0.25f, FrontZ + 0.25f, WingCorr - 0.25f, VoidZ0 - 0.25f));
                n.Open(Rect.MinMaxRect(-WingCorr + 0.25f, VoidZ1 + 0.25f, WingCorr - 0.25f, BandZ0 - 0.2f));
                n.Open(Rect.MinMaxRect(StairB0 + 0.05f, BandZ0 - 0.25f, StairA0 - 0.05f, BandZ1 - 0.25f)); // 真ん中の踊り場
                if (f == 1)
                {
                    n.Open(Rect.MinMaxRect(StairA1 + 0.05f, BandZ0 - 0.25f, WingCorr - 0.25f, BandZ1 - 0.25f));
                    n.Open(Rect.MinMaxRect(-WingCorr + 0.25f, BandZ0 - 0.25f, StairB0 - 0.05f, BandZ1 - 0.25f));
                    n.Open(Rect.MinMaxRect(PassX0 + 0.25f, SchoolWestZ - 0.8f, PassX1 - 0.25f, FrontZ + 0.3f)); // 入口の通路と渡り廊下（学校の2階の大廊下へ）
                    n.Open(In(-WingIn, FrontOut, PassX0, FrontZ));
                    n.Open(DoorNav(1.1f, FrontZ, true)); // 篤の部屋の扉
                    n.Open(In(PassX1, FrontOut, WingOut, FrontZ));
                    n.Open(DoorNav(6.2f, FrontZ, true));
                    n.Open(In(-WingOut, FrontOut, -WingIn, FrontZ));
                    n.Open(DoorNav(-6.2f, FrontZ, true));
                    n.Open(In(-2f, BackZ, WingOut, BackOut));
                    n.Open(DoorNav(6.3f, BackZ, true));
                    n.Open(In(-WingOut, BackZ, -2f, BackOut));
                    n.Open(DoorNav(-6.2f, BackZ, true));
                }
                else
                {
                    n.Open(Rect.MinMaxRect(-WingCorr + 0.25f, BandZ0 - 0.25f, StairB1 - 0.05f, BandZ1 - 0.25f));
                    n.Open(Rect.MinMaxRect(StairA0 + 0.05f, BandZ0 - 0.25f, WingCorr - 0.25f, BandZ1 - 0.25f));
                    n.Open(In(-WingIn, FrontOut, WingOut, FrontZ));
                    n.Open(DoorNav(0f, FrontZ, true));
                    n.Open(DoorNav(6.2f, FrontZ, true));
                    n.Open(In(-WingOut, FrontOut, -WingIn, FrontZ));
                    n.Open(DoorNav(-6.2f, FrontZ, true));
                    n.Open(In(-4f, BackZ, WingOut, BackOut));
                    n.Open(DoorNav(6.3f, BackZ, true));
                    n.Open(In(-WingOut, BackZ, -4f, BackOut));
                    n.Open(DoorNav(-6.2f, BackZ, true));
                }
                for (int sd = -1; sd <= 1; sd += 2)
                    for (int i = 0; i < 5; i++)
                    {
                        float rz = FrontZ + RoomW * (i + 0.5f);
                        n.Open(In(sd * WingCorr, rz - RoomW / 2f, sd * WingOut, rz + RoomW / 2f));
                        n.Open(DoorNav(sd * WingCorr, rz, false, 1.2f));
                    }
            }
            for (int f = 0; f <= 3; f++) OpenTowerNav(t[f], f);
            // 先生の塔・南西の塔（2026-11-08 家具のチャット）：つないだ階は塔の円の中を開け、どの階も塔の壁の厚みの所はふさぐ
            for (int f = 0; f <= 2; f++)
            {
                if (f >= 1) OpenCornerTower(t[f], TeacherTowerC, TeacherTowerR);
                OpenCornerTower(t[f], new Vector2(-WingOut, FrontOut), TowerR);
                ClipCornerTower(t[f], TeacherTowerC, TeacherTowerR, 1f);
                ClipCornerTower(t[f], new Vector2(-WingOut, FrontOut), TowerR, -1f);
            }
            for (int f = 0; f <= 3; f++) CopyMirrored(t[f], navs[f]);
            // ---- 学校（設計図どおり） ----
            for (int f = 0; f <= 2; f++) OpenSchoolNav(f);
            navs[1].Open(SR(-0.7f, 34.9f, 0.5f, 36.1f));   // 2階の西のはし（寮の渡り廊下から）
            navs[0].Open(SR(69.5f, 34.9f, 70.7f, 36.1f));  // 1階の東のはし（騎士団の寮への地上の廊下）
            // ---- 学園の道（正門・中庭の南の渡り廊下・ほかの寮。CampusPreview.cs の CampusPlan） ----
            CampusPlan.OpenPaths(navs[0]);
            CampusPreview.OpenShore(navs[0]); // プールと浜辺（2026-11-08 家具のチャット）
            foreach (var r in outsidePlaceNav) navs[0].Open(r); // 温室と工房の中（扉は ApplyPlaces）
            for (int f = 0; f <= 3; f++)
                foreach (var r in blockers[f]) navs[f].Block(Rect.MinMaxRect(r.xMin - inflate, r.yMin - inflate, r.xMax + inflate, r.yMax + inflate));
        }
    }

    /// <summary>物干し場：晴れた日の 7:00〜18:00 は洗濯物を干した形、雨の日と夜は取り込んだ形（2026-11-02 家具のチャット）。</summary>
    public class LaundryLines : MonoBehaviour
    {
        GameObject full, empty;
        float next;
        int state = -1;
        public void Set(GameObject f, GameObject e) { full = f; empty = e; }
        void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 1f;
            var w = LoungeWorld.Instance;
            if (w == null || full == null || empty == null) return;
            float m = w.MinuteOfDay;
            int s = (!w.BadWeatherToday && m >= 7f * 60f && m < 18f * 60f) ? 1 : 0;
            if (s == state) return;
            state = s;
            full.SetActive(s == 1);
            empty.SetActive(s == 0);
        }
    }
}
