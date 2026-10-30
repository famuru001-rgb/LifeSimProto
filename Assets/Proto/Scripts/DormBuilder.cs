using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>個室1部屋分。</summary>
    public class RoomInfo
    {
        public string name;
        public Seat bed;
        public Poi desk;
        public Vector3 labelPos;
        public Rect rect;
        public int owner = -1;
        public int floor;                                 // 何階か（0＝1階）
        public bool vacant;                               // 空き部屋（家具なし・扉は閉まっている）
        public bool shared;                               // 何人かで使う部屋（先生の部屋。ベッドと机はひとりずつ。2026-10-30）
        public Vector3 bedEdge = new Vector3(0f, 0f, 1f); // ベッドの縁から部屋の中を向く向き（並んで座るとき）
        public float bedSpread = 1f;                      // 並んで座るときの間かく（大きいベッドほど広い）
        public float bedEdgeDist = 0.15f;                 // ベッドの真ん中から、座る縁までの距離
        // 本番の家具のベッド（頭板は奥・足元の縁に座る。家具_v1 2026-10-26）
        public Vector3 bedAcross = Vector3.zero;          // 寝る人が横に並ぶ向き（0 のときは bedEdge の向き）
        public Vector3 bedSitAlong = Vector3.zero;        // 縁に座る人が並ぶ向き（0 のときは bed.forward）
        public float bedLieGap = 0.42f;                   // 並んで寝るときの間かく
        public float bedLieWidth = 1.6f;                  // 並んで寝られる幅
        public float[] bedSeatOffs;                       // 縁に座る所（並ぶ向きの距離 m）。null のときは前の決まり
        /// <summary>n 人が縁に座るときの位置（真ん中から使う）。bedSeatOffs が無いときは null。</summary>
        public float[] SeatOffs(int n)
        {
            if (bedSeatOffs == null) return null;
            int k = Mathf.Min(n, bedSeatOffs.Length), st = (bedSeatOffs.Length - k) / 2;
            var r = new float[k];
            for (int i = 0; i < k; i++) r[i] = bedSeatOffs[st + i];
            return r;
        }
        public readonly List<Seat> cushions = new List<Seat>();
        public Seat futon;
        public string AreaName { get { return name + "の部屋"; } }
    }

    public class AreaInfo
    {
        public string name;
        public Rect rect;
        public int floor;
    }

    /// <summary>
    /// 寮の建物を組み立てる部分（2026-10-25 作り直し。設計：設計\マップ拡大計画.md、famuru 決定）。
    /// 3階建て。プログラムの向き：−z が学校の側（設計図の東）、＋z が奥・中庭の側（西）、＋x が北の棟、−x が南の棟。
    /// ・北の棟（x 8.6〜17）と南の棟（x −8.6〜−17）：廊下（幅2.4m）と個室（幅3.6m×奥行き6m）が各階5部屋ずつ＝30部屋。今の10人は1階
    /// ・真ん中（x ±8.6、z −18〜9）：1階は談話室（z −9〜9 は3階までの吹き抜け）。2・3階は吹き抜けのまわりに通路
    /// ・1階分の高さは 4.5m（2026-10-25 famuru。前は 3m）
    /// ・階段は吹き抜けの中の奥（z 7.2〜9）：1階→2階（x 0.6〜7.6・東へ上る）、2階→3階（x −0.6〜−7.6・西へ上る）、3階→屋上（x 0.6〜7.6。1階→2階の真上）。
    ///   2・3階の真ん中（x −0.6〜0.6）が踊り場。3階の踊り場の扉が屋上（天文台）への鍵
    /// ・奥（z 9〜18）：1階＝台所（x 11〜17）・食堂（x −2〜11）・脱衣所・浴場。食堂の裏口から中庭へ広い通路／2階＝娯楽室・トイレ／3階＝勉強部屋・図書室
    /// ・手前（z −18〜−9）：1階＝寮母さんの部屋・談話室の入り口・倉庫／2階＝小さな談話室・入口の通路（渡り廊下へ）・篤の部屋・小さな談話室／3階＝トレーニング部屋・シャワールーム
    /// ・屋上に天文台。寮の手前（−z）に小さな前庭、2階の入口から学校への渡り廊下
    /// </summary>
    public partial class LoungeWorld
    {
        public const float HalfW = 8.6f;   // 談話室（真ん中）の幅の半分
        public const float FloorH = 4.5f;  // 1階分の高さ（famuru 2026-10-25「1.5倍」。前は 3m）
        public const float RoomH = FloorH * 3f; // 吹き抜けの高さ
        public static float FloorY(int floor) { return floor * FloorH; }

        // 棟：真ん中の端 x 8.6 → 廊下 → x 11 → 個室（奥行き6m）→ x 17（外壁）
        const float WingIn = 8.6f, WingCorr = 11f, WingOut = 17f, RoomW = 3.6f, RoomDepth = 6f;
        // 奥と手前の帯
        const float BackZ = 9f, BackOut = 18f, FrontZ = -9f, FrontOut = -18f;
        // 2・3階の吹き抜け（z −7〜5.2）。手前の通路 z −9〜−7、奥の通路 z 5.2〜7.2、階段の帯 z 7.2〜9
        const float VoidZ0 = -7f, VoidZ1 = 5.2f, BandZ0 = 7.2f, BandZ1 = 9f, BandZ = 8.1f;
        // 階段（x）：1階→2階と3階→屋上は 0.6→7.6（東へ上る）、2階→3階は −0.6→−7.6（西へ上る）。真ん中 −0.6〜0.6 が踊り場
        // （階の高さを 4.5m にしたので、1本 7m の長い段にした。2026-10-25）
        const float StairA0 = 0.6f, StairA1 = 7.6f, StairB0 = -0.6f, StairB1 = -7.6f;
        // 奥の部屋：台所は x 11〜17（6m×9m。famuru 2026-10-25「少し小さく」。前は x 8.6〜17）、食堂は x −2〜11
        public const float KitchenX0 = 11f;
        public const float KitchenDoorZ = 16.8f; // 台所の扉の真ん中（奥の壁から0.7m・配膳の口から0.5m。famuru 2026-10-26。前は 17.0）
        // 2階の入口の通路（渡り廊下へ）
        const float PassX0 = 6f, PassX1 = 8.6f;
        const float Stub = 1.0f;
        const float WallT = 0.3f;

        // 寮の中の目印（行事の集まる所など）
        public static readonly Vector3 LoungeCenter = new Vector3(-5.5f, 0f, 3.5f);   // ソファのまわり
        public static readonly Vector3 BirthdaySpot = new Vector3(-5.5f, 0f, 6.0f);
        public static readonly Vector3 FireplaceSpot = new Vector3(-5.5f, 0f, 7.0f);  // 暖炉の前
        public static readonly Vector3 DiningCenter = new Vector3(4.3f, 0f, 13.5f);   // 食堂
        public static readonly Rect LoungeWanderRect = new Rect(-7.5f, -16f, 15f, 21f); // 談話室の中をぶらぶらする範囲

        class WallSeg
        {
            public Vector2 a, b;
            public readonly List<Renderer> upper = new List<Renderer>();
        }

        readonly List<WallSeg> walls = new List<WallSeg>();
        readonly List<Renderer> ceilingRenderers = new List<Renderer>();
        readonly List<KeyValuePair<Flicker, float>> lamps = new List<KeyValuePair<Flicker, float>>();
        readonly List<Material> windowMats = new List<Material>();
        readonly Dictionary<Material, Material> furnMatCache = new Dictionary<Material, Material>();   // 家具の材質の使い回し（PlaceFurn）
        Material sharedWindowMat;   // 窓のガラスは全部同じ材質（昼夜で色を変える）
        public readonly List<RoomInfo> rooms = new List<RoomInfo>();
        public readonly List<AreaInfo> areaList = new List<AreaInfo>();
        // 階ごとの「ふさがる所」（家具など。0＝1階 … 3＝屋上）
        readonly List<Rect>[] blockers = { new List<Rect>(), new List<Rect>(), new List<Rect>(), new List<Rect>() };
        // 床の板（下から見上げたときに透かす）
        readonly List<KeyValuePair<float, Renderer>> slabRenderers = new List<KeyValuePair<float, Renderer>>();
        Light sun;

        Transform root;
        Material flagstone, woodDark, woodMid, woodFloor, fabricRed, fabricGreen, fabricBrown, fabricWhite, iron, glowLamp, glowFire, glowEmber, ringMat, rugMat, mortarDark, groundMat, waterMat, foodMat, tileMat;

        void Build()
        {
            root = new GameObject("Dorm").transform;
            CreateMaterials();
            SetupSun();
            BuildGround();
            BuildSlabs();
            BuildLounge();
            BuildStairs();
            BuildAtriumRails();
            BuildWing(-1, 0); // 南の棟の1階：個室5部屋（rooms[0〜4]。今の左の棟の5人）
            BuildFRoom();     // 篤の部屋（rooms[5]。2階）
            BuildWing(1, 0);  // 北の棟の1階：個室5部屋（rooms[6〜10]。今の右の棟の5人）
            for (int f = 1; f <= 2; f++) { BuildWing(-1, f); BuildWing(1, f); } // 2階＝騎士課程・3階＝魔法師課程のヒロイン（rooms[11〜30]。2026-10-30 家具を置いた）
            BuildTeacherRoom(); // 先生の部屋（1階の寮母さんの部屋。rooms[31〜33]）
            BuildBackRooms();
            BuildFrontRooms();
            BuildOuterWalls();
            BuildFrontYard();
            BuildBridge();
            BuildSchool();   // 学校の本館（SchoolBuilder.cs）
            BuildRoof();
            BuildPlaces();   // 台所・図書室・中庭・温室・工房・屋上の天文台（DormPlaces.cs）
            FinishAreas();
            BuildNav();
            ApplyPlaces();
        }

        // ================= 材質と光 =================
        void CreateMaterials()
        {
            // 色は依頼11 の絵（明るい石・蜂蜜色の木・くすんだ赤と緑の絵本調）に合わせた（2026-10-21）
            flagstone = ProtoArt.Mat(new Color(0.98f, 0.95f, 0.9f), ProtoArt.Flagstone, new Vector2(5f, 4f), 0.2f);
            mortarDark = ProtoArt.Mat(new Color(0.86f, 0.8f, 0.72f), ProtoArt.StoneWall, new Vector2(1.5f, 1.5f), 0.1f);
            woodDark = ProtoArt.Mat(new Color(0.66f, 0.5f, 0.38f), ProtoArt.WoodFine, new Vector2(1f, 1f), 0.3f);
            woodMid = ProtoArt.Mat(new Color(1f, 0.9f, 0.76f), ProtoArt.WoodFine, new Vector2(1f, 1f), 0.3f);
            woodFloor = ProtoArt.Mat(new Color(0.95f, 0.82f, 0.66f), ProtoArt.WoodPlank, new Vector2(3f, 3f), 0.25f);
            fabricRed = ProtoArt.Mat(new Color(0.6f, 0.17f, 0.2f), ProtoArt.Fabric, new Vector2(3f, 3f), 0.05f);
            fabricGreen = ProtoArt.Mat(new Color(0.3f, 0.45f, 0.28f), ProtoArt.Fabric, new Vector2(3f, 3f), 0.05f);
            fabricBrown = ProtoArt.Mat(new Color(0.42f, 0.27f, 0.17f), ProtoArt.Fabric, new Vector2(2f, 2f), 0.1f);
            fabricWhite = ProtoArt.Mat(new Color(0.9f, 0.88f, 0.82f), ProtoArt.Fabric, new Vector2(2f, 2f), 0.05f);
            rugMat = ProtoArt.Mat(new Color(0.58f, 0.18f, 0.22f), ProtoArt.Fabric, new Vector2(8f, 6f), 0.02f);
            iron = ProtoArt.Mat(new Color(0.12f, 0.12f, 0.13f), null, null, 0.4f);
            groundMat = ProtoArt.Mat(new Color(0.42f, 0.56f, 0.3f), ProtoArt.Fabric, new Vector2(30f, 30f), 0.05f);
            glowLamp = ProtoArt.Glow(new Color(1f, 0.8f, 0.5f), 2.5f);
            glowFire = ProtoArt.Glow(new Color(1f, 0.55f, 0.15f), 3f);
            glowEmber = ProtoArt.Glow(new Color(1f, 0.3f, 0.05f), 2f);
            ringMat = ProtoArt.Glow(new Color(1f, 0.9f, 0.2f), 1.5f);
            waterMat = ProtoArt.Mat(new Color(0.35f, 0.6f, 0.75f), null, null, 0.9f);
            foodMat = ProtoArt.Glow(new Color(1f, 0.75f, 0.45f), 1.2f);
            tileMat = ProtoArt.Mat(new Color(0.82f, 0.86f, 0.88f), ProtoArt.Flagstone, new Vector2(4f, 4f), 0.35f);
            // 本番の床・壁の模様（建物_v1 の Tiles。あれば手描きの模様に置きかえる。家具のチャット 2026-10-26）
            var tFlag = TileTex("Flagstone"); if (tFlag != null) { flagstone = ProtoArt.Mat(Color.white, tFlag, Vector2.one, 0.15f); tileMeters[flagstone] = 2f; }
            var tWood = TileTex("WoodFloor"); if (tWood != null) { woodFloor = ProtoArt.Mat(Color.white, tWood, Vector2.one, 0.2f); tileMeters[woodFloor] = 2f; }
            var tBath = TileTex("BathTile"); if (tBath != null) { tileMat = ProtoArt.Mat(Color.white, tBath, Vector2.one, 0.35f); tileMeters[tileMat] = 1f; }
            var tGrass = TileTex("Grass"); if (tGrass != null) { groundMat = ProtoArt.Mat(Color.white, tGrass, Vector2.one, 0.05f); tileMeters[groundMat] = 4f; }
            var tPath = TileTex("GardenPath"); if (tPath != null) { pathMat = ProtoArt.Mat(Color.white, tPath, Vector2.one, 0.1f); tileMeters[pathMat] = 2f; }
            var tRoof = TileTex("Roof"); if (tRoof != null) { roofMat = ProtoArt.Mat(Color.white, tRoof, Vector2.one, 0.15f); tileMeters[roofMat] = 2f; }
            var tStoneM = TileTex("StoneWall"); if (tStoneM != null) mortarDark = ProtoArt.Mat(new Color(0.92f, 0.88f, 0.82f), tStoneM, new Vector2(1.5f, 1.5f), 0.1f);
        }

        // ---- 本番の床・壁の模様 ----
        readonly Dictionary<Material, float> tileMeters = new Dictionary<Material, float>(); // 床に貼る模様：1枚が何mか
        readonly Dictionary<string, Material> tiledCache = new Dictionary<string, Material>();
        Material pathMat, roofMat;
        static Texture2D TileTex(string n) { return Resources.Load<Texture2D>("Tiles/Tile_" + n); }

        /// <summary>平たい箱（床・道・屋根）に、大きさに合わせて模様の繰り返しを合わせた材質を返す。合わないときは null。</summary>
        Material TiledFor(string name, Vector3 size, Material m)
        {
            if (m == null) return null;
            if (pathMat != null && m == flagstone && name.Contains("Path")) m = pathMat;          // 外の小道
            if (roofMat != null && m == mortarDark && name.EndsWith("_Roof")) m = roofMat;      // 屋根
            float mm;
            if (!tileMeters.TryGetValue(m, out mm) || size.y > 0.4f) return null;
            var t = new Vector2(Mathf.Max(0.25f, Mathf.Round(size.x / mm * 4f) / 4f), Mathf.Max(0.25f, Mathf.Round(size.z / mm * 4f) / 4f));
            string key = m.GetHashCode() + "_" + t.x + "_" + t.y;
            Material c;
            if (!tiledCache.TryGetValue(key, out c))
            {
                c = new Material(m);
                if (c.HasProperty("_BaseMap")) c.SetTextureScale("_BaseMap", t);
                if (c.HasProperty("_MainTex")) c.SetTextureScale("_MainTex", t);
                tiledCache[key] = c;
            }
            return c;
        }

        void SetupSun()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.skybox = null;
            sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
        }

        Light Lamp(Vector3 pos, float intensity, float range, bool shadows = false, float flicker = 0.06f)
        {
            var l = new GameObject("Lamp").AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.72f, 0.42f);
            l.intensity = intensity;
            l.range = range;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            l.transform.position = pos;
            l.transform.SetParent(root, true);
            var f = l.gameObject.AddComponent<Flicker>();
            f.amount = flicker;
            lamps.Add(new KeyValuePair<Flicker, float>(f, intensity));
            return l;
        }

        /// <summary>時刻に合わせて、日の光・窓の明るさ・ランプを変える。</summary>
        void UpdateLighting()
        {
            float m = MinuteOfDay;
            float day;
            if (m < 330f) day = 0f;
            else if (m < 480f) day = Mathf.InverseLerp(330f, 480f, m);
            else if (m < 1020f) day = 1f;
            else if (m < 1170f) day = 1f - Mathf.InverseLerp(1020f, 1170f, m);
            else day = 0f;

            Weather w = Calendar.WeatherOf(Day);
            float wf = w == Weather.Sunny ? 1f : (w == Weather.Cloudy ? 0.7f : (w == Weather.Rain ? 0.45f : 0.6f));
            if (sun != null)
            {
                sun.intensity = Mathf.Lerp(0.12f, 1.1f * wf, day);
                sun.color = Color.Lerp(new Color(0.55f, 0.65f, 1f), new Color(1f, 0.95f, 0.85f), day);
            }
            RenderSettings.ambientLight = Color.Lerp(new Color(0.17f, 0.17f, 0.24f), new Color(0.5f, 0.47f, 0.42f), day);

            Color dayWin = w == Weather.Rain ? new Color(0.5f, 0.55f, 0.65f) : (w == Weather.Snow ? new Color(1.1f, 1.1f, 1.2f) : (w == Weather.Cloudy ? new Color(0.7f, 0.75f, 0.85f) : new Color(0.85f, 0.92f, 1.1f)));
            Color win = Color.Lerp(new Color(0.06f, 0.08f, 0.18f), dayWin, day);
            foreach (var wm in windowMats)
            {
                if (wm.HasProperty("_BaseColor")) wm.SetColor("_BaseColor", win);
                if (wm.HasProperty("_Color")) wm.SetColor("_Color", win);
            }

            bool lateNight = m < 330f;
            float lampScale = lateNight ? 0.45f : Mathf.Lerp(1f, 0.4f, day);
            bool blackout = Blackout;
            foreach (var kv in lamps) if (kv.Key != null) kv.Key.scale = (blackout && !kv.Key.isFire) ? 0.08f : lampScale;
        }

        // ================= 部品づくり =================
        GameObject Box(string name, Vector3 pos, Vector3 size, Material m, Transform parent = null, float yaw = 0f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent != null ? parent : root, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
            var tiled = TiledFor(name, size, m);
            if (tiled != null) go.GetComponent<Renderer>().sharedMaterial = tiled;
            Destroy(go.GetComponent<Collider>());
            return go;
        }

        GameObject BoxRot(string name, Vector3 pos, Vector3 size, Quaternion rot, Material m)
        {
            var go = Box(name, pos, size, m);
            go.transform.localRotation = rot;
            return go;
        }

        GameObject Cyl(string name, Vector3 pos, Vector3 scale, Quaternion rot, Material m, Transform parent = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent != null ? parent : root, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            Destroy(go.GetComponent<Collider>());
            return go;
        }

        Material StoneFor(float len, float h)
        {
            var tst = TileTex("StoneWall");
            if (tst != null) return ProtoArt.Mat(Color.white, tst, new Vector2(Mathf.Max(0.5f, len / 3f), Mathf.Max(0.5f, h / 2f)), 0.1f); // 1枚＝幅3m×高さ2m
            return ProtoArt.Mat(new Color(1f, 0.97f, 0.92f), ProtoArt.StoneWall, new Vector2(Mathf.Max(0.5f, len / 3f), Mathf.Max(0.5f, h / 2f)), 0.1f);
        }

        Material PanelFor(float len)
        {
            var tpn = TileTex("WoodPanel");
            if (tpn != null) return ProtoArt.Mat(Color.white, tpn, new Vector2(Mathf.Max(0.5f, len / 2f), 1f), 0.3f); // 1枚＝幅2m×高さ1m
            return ProtoArt.Mat(new Color(0.7f, 0.54f, 0.4f), ProtoArt.WoodPlank, new Vector2(Mathf.Max(0.5f, len / 2f), 1f), 0.3f);
        }

        /// <summary>
        /// まっすぐな壁（x 方向か z 方向）。下の腰板（木）はいつも見え、上の石の部分は視点によって消える。
        /// gaps：壁に沿った位置 (始め, 終わり, 下の高さ, 上の高さ)。出入口や扉の穴。
        /// </summary>
        WallSeg Wall(Vector2 a, Vector2 b, float baseY, float topY, float thick, params Vector4[] gaps)
        {
            var w = new WallSeg { a = a, b = b };
            walls.Add(w);
            bool alongX = Mathf.Abs(b.y - a.y) < 0.001f;
            float len = Vector2.Distance(a, b);
            Vector2 dir = (b - a).normalized;

            var gl = new List<Vector4>(gaps);
            gl.Sort((p, q) => p.x.CompareTo(q.x));
            float cursor = 0f;
            foreach (var g in gl)
            {
                if (g.x > cursor) WallPiece(w, a, dir, alongX, cursor, g.x, baseY, topY, thick);
                // 床から開いた出入口には、本番の部品の枠
                if (g.z <= baseY + 0.01f && g.y - g.x <= 2.0f)
                {
                    Vector2 gc = a + dir * ((g.x + g.y) / 2f);
                    var fr = PlaceFurn("Furn_DoorFrame", null, new Vector3(gc.x, baseY, gc.y), alongX ? 0f : 90f, false, new Vector3((g.y - g.x + 0.2f) / 1.2f, Mathf.Min(g.w - g.z + 0.15f, topY - baseY) / 2.5f, thick / 0.35f + 0.05f));
                    if (fr != null) foreach (var r in fr.GetComponentsInChildren<Renderer>()) w.upper.Add(r);
                }
                // 穴の下と上
                if (g.z > baseY + 0.01f) WallPiece(w, a, dir, alongX, g.x, g.y, baseY, g.z, thick);
                if (g.w < topY - 0.01f) WallPiece(w, a, dir, alongX, g.x, g.y, g.w, topY, thick, true);
                cursor = Mathf.Max(cursor, g.y);
            }
            if (cursor < len) WallPiece(w, a, dir, alongX, cursor, len, baseY, topY, thick);
            return w;
        }

        void WallPiece(WallSeg w, Vector2 a, Vector2 dir, bool alongX, float s0, float s1, float y0, float y1, float thick, bool upperOnly = false)
        {
            float len = s1 - s0;
            if (len < 0.01f || y1 - y0 < 0.01f) return;
            Vector2 c2 = a + dir * ((s0 + s1) / 2f);
            float stubTop = upperOnly ? y0 : Mathf.Min(y1, y0 + Stub);
            if (!upperOnly && stubTop > y0)
            {
                float h = stubTop - y0;
                Vector3 size = alongX ? new Vector3(len, h, thick) : new Vector3(thick, h, len);
                Box("WallLow", new Vector3(c2.x, y0 + h / 2f, c2.y), size, PanelFor(len));
            }
            if (y1 > stubTop + 0.01f)
            {
                float h = y1 - stubTop;
                Vector3 size = alongX ? new Vector3(len, h, thick) : new Vector3(thick, h, len);
                var go = Box("WallHigh", new Vector3(c2.x, stubTop + h / 2f, c2.y), size, StoneFor(len, h));
                w.upper.Add(go.GetComponent<Renderer>());
            }
        }

        /// <summary>1階分の高さの壁（floor：0＝1階）。</summary>
        WallSeg WallF(int floor, Vector2 a, Vector2 b, params Vector4[] gaps)
        {
            float y = FloorY(floor);
            return Wall(a, b, y, y + FloorH, WallT, gaps);
        }

        /// <summary>壁の扉の穴（壁の始めからの位置 along、幅 w）。</summary>
        static Vector4 DoorGap(int floor, float along, float w = 1.0f, float h = 2.3f)
        {
            float y = FloorY(floor);
            return new Vector4(along - w / 2f, along + w / 2f, y, y + h);
        }

        /// <summary>閉まっている扉（空き部屋など）。c は扉の真ん中の床の位置。</summary>
        void ClosedDoor(Vector3 c, bool alongX, float w = 1.0f, float h = 2.3f)
        {
            string cdName = ArchOr("Furn_Door");
            if (PlaceFurn(cdName, null, c, alongX ? 0f : 90f, false, DoorScale(cdName, w, h)) != null) return; // 本番の部品
            Vector3 size = alongX ? new Vector3(w, h, 0.08f) : new Vector3(0.08f, h, w);
            Box("Door_Closed", new Vector3(c.x, c.y + h / 2f, c.z), size, woodDark);
            Box("Door_Knob", new Vector3(c.x + (alongX ? w * 0.35f : 0f), c.y + 1.0f, c.z + (alongX ? 0f : w * 0.35f)), new Vector3(0.1f, 0.1f, 0.1f), ringMat);
        }

        /// <summary>縦長のアーチ窓。inward は部屋の内側を向く方向。</summary>
        void Window(WallSeg w, Vector3 basePos, Vector3 inward, float sill = 1.2f, float width = 1.1f, float height = 2.2f)
        {
            if (sharedWindowMat == null) { sharedWindowMat = ProtoArt.Glow(new Color(0.45f, 0.6f, 0.9f), 0.9f); windowMats.Add(sharedWindowMat); }
            var mat = sharedWindowMat;
            float yaw = Quaternion.LookRotation(inward).eulerAngles.y;
            // 本番の部品の窓（原点は窓台の上面・壁の内側の面。ガラスは昼夜で明るさが変わる材質に）
            var wm = PlaceFurn("Furn_Window", null, basePos + Vector3.up * sill + inward * (WallT / 2f), yaw, false, new Vector3(width / 1.0f, height / 1.4f, 1f));
            if (wm != null)
            {
                foreach (var r in wm.GetComponentsInChildren<Renderer>())
                {
                    var ms = r.sharedMaterials;
                    for (int i = 0; i < ms.Length; i++) if (ms[i] != null && ms[i].name.StartsWith("Glass_")) ms[i] = mat;
                    r.sharedMaterials = ms;
                    w.upper.Add(r);
                }
                return;
            }
            Vector3 c = basePos + Vector3.up * (sill + height / 2f) + inward * 0.02f;
            w.upper.Add(Box("Window", c, new Vector3(width, height, 0.04f), mat, null, yaw).GetComponent<Renderer>());
            w.upper.Add(Cyl("WindowArch", c + Vector3.up * (height / 2f), new Vector3(width, 0.02f, width), Quaternion.LookRotation(inward) * Quaternion.Euler(90f, 0f, 0f), mat).GetComponent<Renderer>());
            w.upper.Add(Box("Mullion_V", c + inward * 0.03f + Vector3.up * (width * 0.25f), new Vector3(0.06f, height + width * 0.5f, 0.05f), iron, null, yaw).GetComponent<Renderer>());
            w.upper.Add(Box("Mullion_H", c + inward * 0.03f, new Vector3(width, 0.05f, 0.05f), iron, null, yaw).GetComponent<Renderer>());
            Box("Sill", basePos + Vector3.up * (sill - 0.05f) + inward * 0.12f, new Vector3(width + 0.3f, 0.1f, 0.3f), mortarDark, null, yaw);
        }

        void Sconce(WallSeg w, Vector3 pos, Vector3 inward, float intensity = 3.5f)
        {
            float yaw = Quaternion.LookRotation(inward).eulerAngles.y;
            var scm = PlaceFurn("Furn_Sconce", null, pos - Vector3.up * 0.25f, yaw); // 壁に掛ける物
            if (scm != null) foreach (var r in scm.GetComponentsInChildren<Renderer>()) w.upper.Add(r);
            else
            {
            w.upper.Add(Box("Sconce_Bracket", pos - Vector3.up * 0.15f, new Vector3(0.12f, 0.3f, 0.2f), iron, null, yaw).GetComponent<Renderer>());
            var flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flame.name = "Sconce_Light";
            flame.transform.SetParent(root, false);
            flame.transform.position = pos + inward * 0.12f + Vector3.up * 0.08f;
            flame.transform.localScale = Vector3.one * 0.16f;
            flame.GetComponent<Renderer>().sharedMaterial = glowLamp;
            Destroy(flame.GetComponent<Collider>());
            w.upper.Add(flame.GetComponent<Renderer>());
            }
            Lamp(pos + inward * 0.4f, intensity, 7f);
        }

        void Chandelier(Vector3 pos, float intensity, float range)
        {
            var chm = PlaceFurn("Furn_Chandelier", null, pos + Vector3.up * 1.1f, 0f); // 本番の家具（原点は吊り下げる上端）
            if (chm != null) foreach (var r in chm.GetComponentsInChildren<Renderer>()) ceilingRenderers.Add(r);
            else
            {
            Cyl("Chandelier_Ring", pos, new Vector3(0.9f, 0.03f, 0.9f), Quaternion.identity, iron);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                var c = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                c.name = "Candle";
                c.transform.SetParent(root, false);
                c.transform.position = pos + new Vector3(Mathf.Cos(a) * 0.42f, 0.08f, Mathf.Sin(a) * 0.42f);
                c.transform.localScale = Vector3.one * 0.09f;
                c.GetComponent<Renderer>().sharedMaterial = glowLamp;
                Destroy(c.GetComponent<Collider>());
                ceilingRenderers.Add(c.GetComponent<Renderer>());
            }
            }
            Lamp(pos - Vector3.up * 0.3f, intensity, range);
        }

        void Label(Vector3 p, string text) { worldLabels.Add(new KeyValuePair<Vector3, string>(p, text)); }

        void AddArea(string name, Rect r, int floor) { areaList.Add(new AreaInfo { name = name, rect = r, floor = floor }); }

        /// <summary>床の板（floor の高さが上の面）。下から見上げたときに透かすので覚えておく。</summary>
        void Slab(string name, int floor, Rect r, Material m)
        {
            float y = FloorY(floor);
            float th = floor == 0 ? 0.1f : 0.15f;
            var go = Box(name, new Vector3(r.center.x, y - th / 2f, r.center.y), new Vector3(r.width, th, r.height), m);
            slabRenderers.Add(new KeyValuePair<float, Renderer>(Mathf.Max(0.02f, y), go.GetComponent<Renderer>()));
        }

        /// <summary>手すり（a〜b、floor の高さ）。</summary>
        void Rail(int floor, Vector2 a, Vector2 b)
        {
            float y = FloorY(floor);
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 0.05f) return;
            bool alongX = Mathf.Abs(d.y) < 0.001f;
            Vector3 c = new Vector3((a.x + b.x) / 2f, y, (a.y + b.y) / 2f);
            if (Resources.Load<GameObject>("Furniture/Furn_Railing") != null)
            {
                // 本番の部品の手すり（1m ずつ並べる）
                int n = Mathf.Max(1, Mathf.RoundToInt(len)); float seg = len / n;
                for (int i = 0; i < n; i++)
                {
                    Vector2 p = Vector2.Lerp(a, b, (i + 0.5f) / n);
                    PlaceFurn("Furn_Railing", null, new Vector3(p.x, y, p.y), alongX ? 0f : 90f, false, new Vector3(seg, 1f, 1f));
                }
                return;
            }
            Box("Rail_Top", c + Vector3.up * 1.0f, alongX ? new Vector3(len, 0.08f, 0.1f) : new Vector3(0.1f, 0.08f, len), woodDark);
            Box("Rail_Low", c + Vector3.up * 0.15f, alongX ? new Vector3(len, 0.1f, 0.08f) : new Vector3(0.08f, 0.1f, len), woodDark);
            int posts = Mathf.Max(1, Mathf.RoundToInt(len / 1.2f));
            for (int i = 0; i <= posts; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, i / (float)posts);
                Box("Rail_Post", new Vector3(p.x, y + 0.5f, p.y), new Vector3(0.07f, 1.0f, 0.07f), woodDark);
            }
        }

        // ================= 地面と床 =================
        void BuildGround()
        {
            Box("Ground", new Vector3(2.5f, -0.12f, -27.5f), new Vector3(85f, 0.1f, 155f), groundMat); // 寮と学校の本館（z −105〜50）
            slabRenderers.Add(new KeyValuePair<float, Renderer>(0.02f, root.Find("Ground").GetComponent<Renderer>()));
        }

        void BuildSlabs()
        {
            // 1階
            Slab("Floor_Lounge", 0, Rect.MinMaxRect(-WingIn, FrontOut, WingIn, BackZ), flagstone);
            for (int s = -1; s <= 1; s += 2)
            {
                Slab("Floor_Corridor", 0, MinMax(s * WingIn, FrontZ, s * WingCorr, BackZ), woodFloor);
                Slab("Floor_Rooms", 0, MinMax(s * WingCorr, FrontZ, s * WingOut, BackZ), woodFloor);
            }
            Slab("Floor_Dining", 0, Rect.MinMaxRect(-2f, BackZ, KitchenX0, BackOut), woodFloor);
            Slab("Floor_Dressing", 0, Rect.MinMaxRect(-6f, BackZ, -2f, BackOut), woodFloor);
            Slab("Floor_Bath", 0, Rect.MinMaxRect(-WingOut, BackZ, -6f, BackOut), tileMat);
            Slab("Floor_Matron", 0, Rect.MinMaxRect(WingIn, FrontOut, WingOut, FrontZ), woodFloor);
            Slab("Floor_Storage", 0, Rect.MinMaxRect(-WingOut, FrontOut, -WingIn, FrontZ), flagstone);
            // 台所の床は DormPlaces（BuildKitchen）

            // 2階・3階：吹き抜けと階段の穴をあける
            for (int f = 1; f <= 2; f++)
            {
                for (int s = -1; s <= 1; s += 2) Slab("Floor_Wing", f, MinMax(s * WingIn, FrontZ, s * WingOut, BackZ), woodFloor);
                Slab("Floor_Back", f, Rect.MinMaxRect(-WingOut, BackZ, WingOut, BackOut), woodFloor);
                Slab("Floor_Front", f, Rect.MinMaxRect(-WingOut, FrontOut, WingOut, FrontZ), woodFloor);
                Slab("Floor_GalleryE", f, Rect.MinMaxRect(-WingIn, FrontZ, WingIn, VoidZ0), woodFloor);
                Slab("Floor_GalleryW", f, Rect.MinMaxRect(-WingIn, VoidZ1, WingIn, BandZ0), woodFloor);
                if (f == 1)
                {
                    // 2階：1階→2階の段（x 0.6〜7.6）の上はあける。2階→3階の段は西の床の上
                    Slab("Floor_Band", f, Rect.MinMaxRect(-WingIn, BandZ0, StairB0, BandZ1), woodFloor);
                    Slab("Floor_Landing", f, Rect.MinMaxRect(StairB0, BandZ0, StairA0, BandZ1), woodDark);
                    Slab("Floor_Band", f, Rect.MinMaxRect(StairA1, BandZ0, WingIn, BandZ1), woodFloor);
                }
                else
                {
                    // 3階：2階→3階の段（x −7.6〜−0.6）の上はあける。3階→屋上の段は東の床の上
                    Slab("Floor_Band", f, Rect.MinMaxRect(-WingIn, BandZ0, StairB1, BandZ1), woodFloor);
                    Slab("Floor_Landing", f, Rect.MinMaxRect(StairB0, BandZ0, StairA0, BandZ1), woodDark);
                    Slab("Floor_Band", f, Rect.MinMaxRect(StairA0, BandZ0, WingIn, BandZ1), woodFloor);
                }
            }
            // 屋上（3階→屋上の階段の穴だけあける）
            Slab("Roof_Floor", 3, Rect.MinMaxRect(-WingOut, FrontOut, WingOut, BandZ0), mortarDark);
            Slab("Roof_Floor", 3, Rect.MinMaxRect(-WingOut, BandZ0, StairA0, BandZ1), mortarDark);
            Slab("Roof_Floor", 3, Rect.MinMaxRect(StairA1, BandZ0, WingOut, BandZ1), mortarDark);
            Slab("Roof_Floor", 3, Rect.MinMaxRect(-WingOut, BandZ1, WingOut, BackOut), mortarDark);
        }

        static Rect MinMax(float x0, float z0, float x1, float z1)
        {
            return Rect.MinMaxRect(Mathf.Min(x0, x1), Mathf.Min(z0, z1), Mathf.Max(x0, x1), Mathf.Max(z0, z1));
        }

        // ================= 談話室（1階の真ん中。奥は3階までの吹き抜け） =================
        void BuildLounge()
        {
            // 手前の外壁（z −18）は BuildOuterWalls。ここでは手前の左右の壁（寮母さんの部屋・倉庫との境）と家具
            var wallN = WallF(0, new Vector2(WingIn, FrontOut), new Vector2(WingIn, FrontZ));
            var wallS = WallF(0, new Vector2(-WingIn, FrontOut), new Vector2(-WingIn, FrontZ));

            Box("Floor_Wood", new Vector3(3.0f, 0.005f, -2.0f), new Vector3(5.0f, 0.02f, 3.8f), woodFloor);
            if (PlaceFurn("Furn_LoungeRug", null, new Vector3(-5.5f, 0f, 3.4f), 0f) == null)
            {
            Box("RugBorder", new Vector3(-5.5f, 0.008f, 3.4f), new Vector3(5.9f, 0.016f, 4.7f), fabricBrown);
            Box("Rug", new Vector3(-5.5f, 0.012f, 3.4f), new Vector3(5.6f, 0.02f, 4.4f), rugMat);
            }

            // 柱（吹き抜けの四隅と、廊下との境）
            float[] pz = { VoidZ0, -1f, VoidZ1, FrontZ };
            for (int s = -1; s <= 1; s += 2)
                foreach (float z in pz)
                {
                    float top = z == FrontZ ? FloorH : RoomH;
                    if (PlaceFurn("Furn_Pillar", null, new Vector3(s * WingIn, 0f, z), 0f, false, new Vector3(0.35f / 0.4f, top / 4.5f, 0.35f / 0.4f)) == null)
                        Box("Pillar", new Vector3(s * WingIn, top / 2f, z), new Vector3(0.35f, top, 0.35f), mortarDark);
                    blockers[0].Add(new Rect(s * WingIn - 0.2f, z - 0.2f, 0.4f, 0.4f));
                }

            // 吹き抜けのシャンデリア（高い所につるす）
            Chandelier(new Vector3(-5.5f, 6.2f, 3.4f), 5f, 11f);
            Chandelier(new Vector3(3.0f, 6.2f, -2.0f), 4f, 10f);
            Chandelier(new Vector3(0f, 2.6f, -13.5f), 3.5f, 8f);
            Sconce(wallN, new Vector3(WingIn - 0.15f, 2.4f, -16.2f), Vector3.left, 2.5f);
            Sconce(wallS, new Vector3(-WingIn + 0.15f, 2.4f, -16.2f), Vector3.right, 2.5f);

            // --- 奥の壁ぎわ：大きな暖炉（脱衣所の壁。煙突は2階の高さまで） ---
            float fx = FireplaceSpot.x, fz = BackZ - 0.55f; // 炉の手前
            bool fpModel = PlaceFurn("Furn_Fireplace", null, new Vector3(fx, 0f, fz + 0.05f), 180f) != null; // 本番の家具（家具_v1）があればそれを置く
            if (!fpModel)
            {
            Box("Hearth", new Vector3(fx, 0.15f, fz), new Vector3(3.4f, 0.3f, 1.1f), mortarDark);
            Box("Fireplace_PillarL", new Vector3(fx - 1.4f, 1.45f, fz + 0.2f), new Vector3(0.6f, 2.6f, 0.7f), mortarDark);
            Box("Fireplace_PillarR", new Vector3(fx + 1.4f, 1.45f, fz + 0.2f), new Vector3(0.6f, 2.6f, 0.7f), mortarDark);
            Box("Fireplace_Mantel", new Vector3(fx, 2.85f, fz + 0.1f), new Vector3(3.8f, 0.3f, 0.95f), woodDark);
            }
            float chH = FloorH - 3.0f; // 煙突はマントルの上から2階の床まで
            ceilingRenderers.Add(Box("Fireplace_Chimney", new Vector3(fx, 3.0f + chH / 2f, fz + 0.3f), new Vector3(2.9f, chH, 0.5f), StoneFor(2.9f, chH)).GetComponent<Renderer>());
            if (!fpModel)
            {
            Box("Fireplace_Back", new Vector3(fx, 1.3f, fz + 0.5f), new Vector3(2.3f, 2.3f, 0.1f), iron);
            Cyl("Log1", new Vector3(fx - 0.2f, 0.42f, fz + 0.1f), new Vector3(0.18f, 0.6f, 0.18f), Quaternion.Euler(0f, 0f, 90f), woodDark);
            Cyl("Log2", new Vector3(fx + 0.25f, 0.42f, fz + 0.15f), new Vector3(0.18f, 0.55f, 0.18f), Quaternion.Euler(0f, 30f, 90f), woodDark);
            }
            for (int i = 0; i < 5; i++)
            {
                var f = Box("Flame" + i, new Vector3(fx - 0.5f + i * 0.25f, 0.65f, fz + 0.1f), new Vector3(0.18f, 0.5f, 0.18f), i % 2 == 0 ? glowFire : glowEmber);
                f.AddComponent<FlameWobble>().seed = i * 1.7f;
            }
            var fire = Lamp(new Vector3(fx, 1.0f, fz - 0.55f), 9f, 11f, true, 0.2f);
            fire.color = new Color(1f, 0.6f, 0.3f);
            fire.GetComponent<Flicker>().isFire = true;
            blockers[0].Add(new Rect(fx - 1.85f, fz - 0.6f, 3.7f, 1.2f));

            Sofa("暖炉の前の椅子", "談話室", new Vector3(fx - 1.3f, 0f, fz - 1.55f), 0f, 1.0f, 1, fabricGreen);
            Sofa("暖炉の前の椅子", "談話室", new Vector3(fx + 1.3f, 0f, fz - 1.55f), 0f, 1.0f, 1, fabricGreen);

            // --- ソファで囲む低いテーブル（暖炉の手前） ---
            Vector3 lc = LoungeCenter;
            if (PlaceFurn("Furn_LowTable", null, new Vector3(lc.x, 0f, lc.z), 0f) == null)
            {
            Box("LowTable", new Vector3(lc.x, 0.22f, lc.z), new Vector3(1.4f, 0.08f, 0.8f), woodMid);
            for (int i = 0; i < 4; i++)
            {
                float x = lc.x + ((i % 2 == 0) ? -0.6f : 0.6f), z = lc.z + ((i < 2) ? -0.3f : 0.3f);
                Box("LowTable_Leg", new Vector3(x, 0.1f, z), new Vector3(0.08f, 0.2f, 0.08f), woodDark);
            }
            }
            blockers[0].Add(new Rect(lc.x - 0.75f, lc.z - 0.45f, 1.5f, 0.9f));
            Sofa("ソファ", "談話室", new Vector3(lc.x, 0f, lc.z - 1.5f), 0f, 2.4f, 3, fabricRed);
            Sofa("ソファ", "談話室", new Vector3(lc.x - 1.95f, 0f, lc.z), 90f, 2.4f, 3, fabricRed);
            Sofa("ソファ", "談話室", new Vector3(lc.x + 1.95f, 0f, lc.z), -90f, 2.4f, 3, fabricRed);

            // --- 大きなテーブル（吹き抜けの右手前） ---
            Vector3 bt = new Vector3(3.0f, 0f, -2.0f);
            if (PlaceFurn("Furn_BigTable", null, new Vector3(bt.x, 0f, bt.z), 0f) == null)
            {
            Box("BigTable_Top", new Vector3(bt.x, 0.75f, bt.z), new Vector3(2.4f, 0.08f, 1.0f), woodMid);
            for (int i = 0; i < 4; i++)
            {
                float x = bt.x + ((i % 2 == 0) ? -1.1f : 1.1f), z = bt.z + ((i < 2) ? -0.4f : 0.4f);
                Box("BigTable_Leg", new Vector3(x, 0.37f, z), new Vector3(0.1f, 0.74f, 0.1f), woodDark);
            }
            }
            if (PlaceFurn("Furn_CandleStand", null, new Vector3(bt.x, 0.79f, bt.z), 0f) == null)
            {
            Box("Candle", new Vector3(bt.x, 0.94f, bt.z), new Vector3(0.06f, 0.22f, 0.06f), glowLamp);
            }
            blockers[0].Add(new Rect(bt.x - 1.2f, bt.z - 0.5f, 2.4f, 1.0f));
            for (int i = 0; i < 3; i++)
            {
                float x = bt.x - 0.8f + i * 0.8f;
                Chair("大きなテーブル", "談話室", new Vector3(x, 0f, bt.z + 0.8f), 180f);
                Chair("大きなテーブル", "談話室", new Vector3(x, 0f, bt.z - 0.8f), 0f);
            }

            // --- 手前（天井の低い所）：窓辺のベンチ・読書の椅子・本棚・おやつワゴン・掲示板 ---
            float wz = FrontOut + 0.6f;
            Bench(new Vector3(3.6f, 0f, wz), 0f);
            Bench(new Vector3(6.4f, 0f, wz), 0f);
            Sofa("読書の椅子", "談話室", new Vector3(-6.4f, 0f, FrontOut + 1.1f), 0f, 1.0f, 1, fabricGreen);
            Sofa("読書の椅子", "談話室", new Vector3(-4.6f, 0f, FrontOut + 1.1f), 0f, 1.0f, 1, fabricGreen);
            if (PlaceFurn("Furn_ReadingTable", null, new Vector3(-5.5f, 0f, FrontOut + 1.0f), 0f) == null)
            {
            Cyl("ReadingTable", new Vector3(-5.5f, 0.5f, FrontOut + 1.0f), new Vector3(0.5f, 0.02f, 0.5f), Quaternion.identity, woodMid);
            Cyl("ReadingTable_Leg", new Vector3(-5.5f, 0.25f, FrontOut + 1.0f), new Vector3(0.08f, 0.25f, 0.08f), Quaternion.identity, woodDark);
            }
            blockers[0].Add(new Rect(-5.75f, FrontOut + 0.75f, 0.5f, 0.5f));
            Bookshelf(new Vector3(WingIn - 0.3f, 0f, -12.5f), 2.6f, 90f, 2.6f);
            Bookshelf(new Vector3(-WingIn + 0.3f, 0f, -12.5f), 2.6f, -90f, 2.6f);
            if (PlaceFurn("Furn_NoticeBoard", null, new Vector3(1.9f, 1.15f, FrontOut + 0.175f), 0f) == null) // 壁に掛ける物（原点は背面の下端）
                Box("NoticeBoard", new Vector3(1.9f, 1.6f, FrontOut + 0.2f), new Vector3(1.2f, 0.9f, 0.05f), woodMid);

            pois.Add(MakePoi("暖炉にあたっている", new Vector3(fx, 0.8f, fz + 0.15f), new Vector3(fx - 0.5f, 0f, fz - 0.9f), new Vector3(fx + 0.5f, 0f, fz - 0.9f), new Vector3(fx, 0f, fz - 1.4f)));
            pois.Add(MakePoi("本棚の本を眺めている", new Vector3(WingIn, 1.4f, -12.5f), new Vector3(WingIn - 1.2f, 0f, -13.0f), new Vector3(WingIn - 1.15f, 0f, -12.0f)));
            pois.Add(MakePoi("本棚の本を眺めている", new Vector3(-WingIn, 1.4f, -12.5f), new Vector3(-WingIn + 1.15f, 0f, -12.0f), new Vector3(-WingIn + 1.2f, 0f, -13.0f)));
            pois.Add(MakePoi("窓の外を眺めている", new Vector3(5.0f, 2f, FrontOut), new Vector3(5.0f, 0f, FrontOut + 1.4f)));
            // 魔法の練習場所（床の魔法陣）。趣味が「魔法の練習」のキャラだけが使う
            Vector3 mc = new Vector3(4.0f, 0f, 3.0f);
            Cyl("MagicCircle", new Vector3(mc.x, 0.015f, mc.z), new Vector3(1.8f, 0.004f, 1.8f), Quaternion.identity, ProtoArt.Glow(new Color(0.4f, 0.55f, 1f), 0.6f));
            var magic = MakePoi("魔法の練習をしている", new Vector3(mc.x, 1.2f, mc.z), new Vector3(mc.x - 0.5f, 0f, mc.z + 0.4f), new Vector3(mc.x + 0.5f, 0f, mc.z + 0.5f));
            magic.hobby = (int)SimCharacter.HobbyKind.Magic;
            pois.Add(magic);
            // おやつワゴン（入り口の左）。おなかが空いたらここで取って、談話室で食べる
            float sz = FrontOut + 0.65f, sx = -2.4f;
            if (PlaceFurn("Furn_SnackWagon", null, new Vector3(sx, 0f, sz), 0f) == null)
            {
            Box("SnackTrolley", new Vector3(sx, 0.4f, sz), new Vector3(0.8f, 0.8f, 0.5f), woodMid);
            for (int i = 0; i < 3; i++) Box("Snack", new Vector3(sx - 0.25f + i * 0.25f, 0.86f, sz), new Vector3(0.18f, 0.1f, 0.18f), foodMat);
            }
            blockers[0].Add(new Rect(sx - 0.4f, sz - 0.25f, 0.8f, 0.5f));
            var snackPoi = MakePoi("おやつを取っている", new Vector3(sx, 0.8f, sz), new Vector3(sx, 0f, sz + 0.65f), new Vector3(sx + 0.85f, 0f, sz + 0.05f));
            snackPoi.snack = true;
            pois.Add(snackPoi);
            Label(new Vector3(sx, 1.4f, sz), "おやつワゴン");
            Label(new Vector3(-5.5f, 2.6f, 0.2f), "談話室");
        }

        // ================= 階段（吹き抜けの奥） =================
        /// <summary>まっすぐな階段（x 方向）。x0（下）→ x1（上）、高さ y0 → y1。段は y0 から積む。</summary>
        void Flight(string name, float x0, float x1, float y0, float y1)
        {
            // 本番の部品の階段（+X へ上る・手すりは吹き抜け側＝−z）
            bool upX = x1 > x0;
            if (PlaceFurn("Furn_Stairs", null, new Vector3(x0, y0, BandZ), upX ? 0f : 180f, false, new Vector3(Mathf.Abs(x1 - x0) / 7f, (y1 - y0) / 4.5f, upX ? -1f : 1f)) != null) return;
            int steps = 24;
            float run = (x1 - x0) / steps, rise = (y1 - y0) / steps;
            float w = BandZ1 - BandZ0;
            for (int i = 0; i < steps; i++)
            {
                float top = (i + 1) * rise;
                Box(name + "_Step", new Vector3(x0 + (i + 0.5f) * run, y0 + top / 2f, BandZ), new Vector3(Mathf.Abs(run), top, w - 0.05f), i % 2 == 0 ? woodDark : woodMid);
            }
            // 手すり（斜め。吹き抜けの側）
            float len = Mathf.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
            float slope = Mathf.Atan2(y1 - y0, Mathf.Abs(x1 - x0)) * Mathf.Rad2Deg * (x1 > x0 ? -1f : 1f);
            BoxRot(name + "_Rail", new Vector3((x0 + x1) / 2f, (y0 + y1) / 2f + 0.9f, BandZ0 + 0.04f), new Vector3(len, 0.08f, 0.08f), Quaternion.Euler(0f, 0f, -slope), woodDark);
            Box(name + "_Post", new Vector3(x0, y0 + 0.5f, BandZ0 + 0.04f), new Vector3(0.12f, 1f, 0.12f), woodDark);
        }

        void BuildStairs()
        {
            Flight("StairA", StairA0, StairA1, 0f, FloorY(1));          // 1階 → 2階（東へ）
            Flight("StairB", StairB0, StairB1, FloorY(1), FloorY(2));    // 2階 → 3階（西へ。2階の床の上）
            Flight("StairC", StairA0, StairA1, FloorY(2), FloorY(3));    // 3階 → 屋上（東へ。1階→2階の真上、3階の床の上）
            Rect footA = Rect.MinMaxRect(StairA0 - 0.1f, BandZ0 - 0.1f, StairA1, BandZ1);
            Rect footB = Rect.MinMaxRect(StairB1, BandZ0 - 0.1f, StairB0 + 0.1f, BandZ1);
            blockers[0].Add(footA); // 1階→2階の段
            blockers[1].Add(footB); // 2階→3階の段
            blockers[2].Add(footA); // 3階→屋上の段
            Vector3 lane = new Vector3(0f, 0f, 0.4f); // 上りは壁側、下りは吹き抜け側
            stairs.Add(new Stair
            {
                lowerFloor = 0,
                bottomApproach = new Vector3(StairA0 - 0.8f, 0f, BandZ),
                bottom = new Vector3(StairA0, 0f, BandZ),
                top = new Vector3(StairA1, FloorY(1), BandZ),
                topApproach = new Vector3(StairA1 + 0.5f, FloorY(1), BandZ),
                lane = lane
            });
            stairs.Add(new Stair
            {
                lowerFloor = 1,
                bottomApproach = new Vector3(0f, FloorY(1), BandZ),
                bottom = new Vector3(StairB0, FloorY(1), BandZ),
                top = new Vector3(StairB1, FloorY(2), BandZ),
                topApproach = new Vector3(StairB1 - 0.5f, FloorY(2), BandZ),
                lane = -lane
            });
            stairs.Add(new Stair
            {
                lowerFloor = 2,
                bottomApproach = new Vector3(0f, FloorY(2), BandZ),
                bottom = new Vector3(StairA0, FloorY(2), BandZ),
                top = new Vector3(StairA1, FloorY(3), BandZ),
                topApproach = new Vector3(StairA1 + 0.5f, FloorY(3), BandZ),
                lane = lane
            });
            Label(new Vector3(StairA0 - 0.3f, 1.6f, BandZ - 0.6f), "階段（2階へ）");
            Label(new Vector3(0f, FloorY(1) + 1.8f, BandZ - 0.6f), "階段（3階へ）");
            Label(new Vector3(0f, FloorY(2) + 1.8f, BandZ - 0.6f), "屋上への階段");
        }

        /// <summary>2・3階の吹き抜けのまわりと、階段の穴のまわりの手すり。</summary>
        void BuildAtriumRails()
        {
            for (int f = 1; f <= 2; f++)
            {
                Rail(f, new Vector2(-WingIn, VoidZ0), new Vector2(-WingIn, VoidZ1));
                Rail(f, new Vector2(WingIn, VoidZ0), new Vector2(WingIn, VoidZ1));
                Rail(f, new Vector2(-WingIn, VoidZ0), new Vector2(WingIn, VoidZ0));
                Rail(f, new Vector2(-WingIn, VoidZ1), new Vector2(WingIn, VoidZ1));
            }
            Rail(1, new Vector2(StairA0, BandZ0), new Vector2(StairA1, BandZ0)); // 2階：1階→2階の階段の穴
            Rail(1, new Vector2(StairA0, BandZ0), new Vector2(StairA0, BandZ1));
            Rail(2, new Vector2(StairB1, BandZ0), new Vector2(StairB0, BandZ0)); // 3階：2階→3階の階段の穴
            Rail(2, new Vector2(StairB0, BandZ0), new Vector2(StairB0, BandZ1));
            Rail(3, new Vector2(StairA0, BandZ0), new Vector2(StairA1, BandZ0)); // 屋上の階段の穴
            Rail(3, new Vector2(StairA0, BandZ0), new Vector2(StairA0, BandZ1));
        }

        // ================= 棟：廊下と個室 =================
        /// <summary>
        /// side：-1 南 / 1 北。floor：0＝1階（今の10人の部屋。家具あり）、1・2＝2階・3階（空き部屋。扉は閉まっている）。
        /// 個室は廊下の外側（x 11〜17）に5つ、z の手前（−9）から奥へ 1〜5 番。
        /// </summary>
        void BuildWing(int side, int floor)
        {
            float s = side, y = FloorY(floor);
            string wingName = side < 0 ? "南" : "北";
            string prefix = floor == 0 ? "" : (floor + 1) + "階・";
            var gaps = new List<Vector4>();
            for (int i = 0; i < 5; i++)
            {
                float cz = FrontZ + RoomW * (i + 0.5f);
                gaps.Add(DoorGap(floor, cz - FrontZ, 1.2f));
            }
            WallF(floor, new Vector2(s * WingCorr, FrontZ), new Vector2(s * WingCorr, BackZ), gaps.ToArray());
            for (int i = 1; i < 5; i++)
            {
                float z = FrontZ + i * RoomW;
                WallF(floor, new Vector2(s * WingCorr, z), new Vector2(s * WingOut, z));
            }
            float cx = s * (WingIn + WingCorr) / 2f;
            Lamp(new Vector3(cx, y + 2.4f, -4.5f), 2f, 6f);
            Lamp(new Vector3(cx, y + 2.4f, 4.5f), 2f, 6f);
            string corrName = (floor + 1) + "階・" + wingName + "の廊下";
            Label(new Vector3(cx, y + 2.4f, FrontZ + 0.8f), corrName);
            AddArea(corrName, MinMax(s * WingIn, FrontZ, s * WingCorr, BackZ), floor);

            for (int i = 0; i < 5; i++)
            {
                float cz = FrontZ + RoomW * (i + 0.5f);
                var room = new RoomInfo { name = prefix + wingName + (i + 1), floor = floor, vacant = false }; // 2026-10-30：2・3階も追加のヒロインの部屋
                room.rect = MinMax(s * WingCorr, cz - RoomW / 2f, s * WingOut, cz + RoomW / 2f);
                room.labelPos = new Vector3(s * (WingCorr + RoomDepth - 2.4f), y + 2.0f, cz);
                if (room.vacant)
                {
                    ClosedDoor(new Vector3(s * WingCorr, y, cz), false, 1.2f);
                    Label(room.labelPos, "空き部屋");
                }
                else FurnishRoom(room, side, cz);
                AddArea(room.AreaName, room.rect, floor);
                rooms.Add(room);
            }
        }

        /// <summary>1階の個室の家具（ベッド・座布団・布団・机・タンス）。</summary>
        void FurnishRoom(RoomInfo room, int side, float cz)
        {
            float s = side, y = FloorY(room.floor);
            System.Func<float, float> X = u => s * (WingCorr + u); // 廊下の壁から部屋の奥への距離 u（0〜6）
            // ベッド（3人並んで寝られる・4人腰かけられる 2.2×2.1m。famuru 2026-10-26）
            // 頭板は外壁側。足元（廊下側）の縁に並んで座る。部屋の片側（−z）に寄せる
            float bz = cz - 0.95f;                                   // 来客用の布団の位置
            float bW = 2.2f, bD = 2.1f;                              // 幅（z）・奥行き（x）
            float bu0 = RoomDepth - 0.05f - bD, bu1 = RoomDepth - 0.05f;
            float bcx = X((bu0 + bu1) / 2f), bcz = cz - 0.55f;     // z は cz−1.65〜+0.55
            if (PlaceFurn("Furn_Bed", null, new Vector3(bcx, y, bcz), -90f * s) == null)
            {
                Box("Bed_Frame", new Vector3(bcx, y + 0.2f, bcz), new Vector3(bD, 0.4f, bW), woodDark);
                Box("Bed_Mattress", new Vector3(bcx, y + 0.47f, bcz), new Vector3(bD - 0.1f, 0.14f, bW - 0.1f), fabricWhite);
                Box("Bed_Blanket", new Vector3(bcx - s * 0.3f, y + 0.55f, bcz), new Vector3(bD * 0.65f, 0.04f, bW - 0.08f), fabricRed);
                Box("Bed_Head", new Vector3(X(RoomDepth - 0.1f), y + 0.55f, bcz), new Vector3(0.08f, 1.1f, bW), woodDark);
            }
            blockers[room.floor].Add(Rect.MinMaxRect(Mathf.Min(X(bu0), X(bu1)), bcz - bW / 2f, Mathf.Max(X(bu0), X(bu1)), bcz + bW / 2f));
            room.bed = new Seat
            {
                kind = SeatKind.Bed,
                label = "ベッド",
                area = room.name,
                position = new Vector3(bcx, y, bcz),
                forward = new Vector3(s, 0f, 0f),
                approach = new Vector3(X(bu0 - 0.45f), y, bcz),
                room = room
            };
            seats.Add(room.bed);
            room.bedEdge = new Vector3(-s, 0f, 0f);    // 足元の縁（部屋の中）
            room.bedAcross = Vector3.forward;            // 寝る人は z に並ぶ
            room.bedSitAlong = Vector3.forward;          // 座る人も z に並ぶ
            room.bedEdgeDist = bD / 2f - 0.3f;
            room.bedLieGap = 0.73f;
            room.bedLieWidth = bW - 0.1f;
            room.bedSeatOffs = new[] { -0.825f, -0.275f, 0.275f, 0.825f };
            // 来客用：座布団2枚と小さなテーブル、床に敷く布団
            if (PlaceFurn("Furn_LowTableSmall", null, new Vector3(X(2.3f), y, cz + 0.65f), 0f) == null)
                Box("LowTable_Room", new Vector3(X(2.3f), y + 0.15f, cz + 0.65f), new Vector3(0.5f, 0.3f, 0.5f), woodMid);
            blockers[room.floor].Add(Rect.MinMaxRect(Mathf.Min(X(2.05f), X(2.55f)), cz + 0.4f, Mathf.Max(X(2.05f), X(2.55f)), cz + 0.9f));
            for (int k = 0; k < 2; k++)
            {
                float cx = X(k == 0 ? 1.55f : 3.05f);
                if (PlaceFurn("Furn_Cushion", null, new Vector3(cx, y, cz + 0.65f), 0f) == null)
                    Box("Cushion", new Vector3(cx, y + 0.04f, cz + 0.65f), new Vector3(0.5f, 0.08f, 0.5f), fabricGreen);
                var cu = new Seat
                {
                    kind = SeatKind.Sit, floorSeat = true, label = "空き部屋", area = room.name,
                    position = new Vector3(cx, y, cz + 0.65f),
                    forward = new Vector3(k == 0 ? s : -s, 0f, 0f),
                    approach = new Vector3(cx, y, cz),
                    room = room
                };
                room.cushions.Add(cu);
                seats.Add(cu);
            }
            if (PlaceFurn("Furn_GuestFuton", null, new Vector3(X(2.05f), y, bz), s > 0f ? 0f : 180f) == null) // 枕は外壁側
                Box("GuestFuton", new Vector3(X(2.05f), y + 0.05f, bz), new Vector3(1.7f, 0.1f, 0.75f), fabricWhite);
            room.futon = new Seat
            {
                kind = SeatKind.Bed, floorSeat = true, label = "来客用の布団", area = room.name,
                position = new Vector3(X(2.05f), y, bz),
                forward = new Vector3(s, 0f, 0f),
                approach = new Vector3(X(2.05f), y, cz - 0.2f),
                room = room
            };
            seats.Add(room.futon);
            // 机（外壁側、ベッドの横。本番の家具があればそれを置く。部屋の中を向く）
            float deskX = X(RoomDepth - 0.35f), dz = cz + 1.25f;
            if (PlaceFurn("Furn_Desk", null, new Vector3(deskX, y, dz), -90f * s) == null)
            {
            Box("Desk", new Vector3(deskX, y + 0.73f, dz), new Vector3(0.6f, 0.05f, 0.8f), woodMid);
            Box("Desk_Body", new Vector3(deskX, y + 0.36f, dz), new Vector3(0.55f, 0.72f, 0.75f), woodDark);
            Box("Desk_Book", new Vector3(deskX, y + 0.8f, dz - 0.12f), new Vector3(0.25f, 0.06f, 0.18f), fabricGreen);
            var lampGlow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lampGlow.name = "DeskLamp";
            lampGlow.transform.SetParent(root, false);
            lampGlow.transform.position = new Vector3(deskX, y + 0.95f, dz + 0.28f);
            lampGlow.transform.localScale = Vector3.one * 0.12f;
            lampGlow.GetComponent<Renderer>().sharedMaterial = glowLamp;
            Destroy(lampGlow.GetComponent<Collider>());
            }
            blockers[room.floor].Add(Rect.MinMaxRect(Mathf.Min(X(RoomDepth - 0.7f), X(RoomDepth)), dz - 0.42f, Mathf.Max(X(RoomDepth - 0.7f), X(RoomDepth)), dz + 0.42f));
            // 机の椅子（2026-10-30 モーション：机で勉強は座って書く）。椅子の前を机に向ける
            Vector3 chairP = new Vector3(X(RoomDepth - 0.97f), y, dz);
            PlaceFurn("Furn_Chair", null, chairP, Quaternion.LookRotation(new Vector3(deskX - chairP.x, 0f, 0f)).eulerAngles.y);
            room.desk = new Poi
            {
                label = "机で勉強している",
                lookAt = new Vector3(deskX, y + 0.8f, dz),
                slots = new[] { chairP },
                occupants = new SimCharacter[1],
                seated = true
            };
            pois.Add(room.desk);
            // タンス（廊下側の壁ぎわ。扉の横）
            if (PlaceFurn("Furn_Wardrobe", null, new Vector3(X(0.4f), y, cz + 1.15f), 90f * s) == null) // 部屋の中を向く
                Box("Wardrobe", new Vector3(X(0.35f), y + 0.9f, cz + 1.15f), new Vector3(0.5f, 1.8f, 0.7f), woodDark);
            blockers[room.floor].Add(Rect.MinMaxRect(Mathf.Min(X(0.1f), X(0.6f)), cz + 0.8f, Mathf.Max(X(0.1f), X(0.6f)), cz + 1.5f));
            Lamp(new Vector3(X(RoomDepth - 2.5f), y + 2.3f, cz), 1.5f, 4.5f, false, 0.03f);
        }

        // ================= 先生の部屋（1階の手前・北。もとの寮母さんの部屋。x 8.6〜17、z −18〜−9） =================
        /// <summary>
        /// 先生3人（AF ウィスティ・AG ジゼル・AH クラリス）の部屋（2026-10-30 famuru「寮母の部屋を使う」）。
        /// ベッドは外壁（x 17）に頭を向けて3台、机は手前の外壁（z −18）ぞいに3つ。来客用の布団と座布団はみんなで使う。
        /// 扉は奥の壁（z −9）の x 9.8。
        /// </summary>
        void BuildTeacherRoom()
        {
            float y = 0f, cz = (FrontZ + FrontOut) / 2f;
            var rect = Rect.MinMaxRect(WingIn, FrontOut, WingOut, FrontZ);
            Label(new Vector3((WingIn + WingOut) / 2f, 2.2f, cz), "先生の部屋");
            AddArea("先生の部屋", rect, 0);
            Lamp(new Vector3((WingIn + WingOut) / 2f, 2.4f, cz), 1.8f, 7f);
            // みんなで使う物：来客用の布団・座布団2枚と小さなテーブル
            var shared = new List<Seat>();
            Vector3 futonP = new Vector3(10.6f, y, -12.6f);
            if (PlaceFurn("Furn_GuestFuton", null, futonP, 90f) == null)
                Box("GuestFuton", futonP + Vector3.up * 0.05f, new Vector3(0.75f, 0.1f, 1.7f), fabricWhite);
            var futon = new Seat { kind = SeatKind.Bed, floorSeat = true, label = "来客用の布団", area = "先生", position = futonP, forward = new Vector3(0f, 0f, 1f), approach = new Vector3(11.4f, y, -12.6f) };
            seats.Add(futon);
            Vector3 tableP = new Vector3(12.2f, y, -14.6f);
            if (PlaceFurn("Furn_LowTableSmall", null, tableP, 0f) == null)
                Box("LowTable_Room", tableP + Vector3.up * 0.15f, new Vector3(0.5f, 0.3f, 0.5f), woodMid);
            blockers[0].Add(Rect.MinMaxRect(tableP.x - 0.25f, tableP.z - 0.25f, tableP.x + 0.25f, tableP.z + 0.25f));
            for (int k = 0; k < 2; k++)
            {
                Vector3 cp = tableP + new Vector3(k == 0 ? -0.75f : 0.75f, 0f, 0f);
                if (PlaceFurn("Furn_Cushion", null, cp, 0f) == null)
                    Box("Cushion", cp + Vector3.up * 0.04f, new Vector3(0.5f, 0.08f, 0.5f), fabricGreen);
                var cu = new Seat { kind = SeatKind.Sit, floorSeat = true, label = "先生の部屋", area = "先生", position = cp, forward = new Vector3(k == 0 ? 1f : -1f, 0f, 0f), approach = cp + new Vector3(0f, 0f, 0.65f) };
                shared.Add(cu);
                seats.Add(cu);
            }
            float bD = 2.1f, bW = 2.2f;
            float bcx = WingOut - 0.05f - bD / 2f;
            float[] bzs = { -16.8f, -13.5f, -10.2f };
            float[] dxs = { 9.6f, 11.0f, 12.4f };
            for (int i = 0; i < 3; i++)
            {
                var room = new RoomInfo { name = "先生", floor = 0, shared = true, rect = rect, labelPos = new Vector3((WingIn + WingOut) / 2f, 2.0f, cz) };
                float bcz = bzs[i];
                if (PlaceFurn("Furn_Bed", null, new Vector3(bcx, y, bcz), -90f) == null)
                {
                    Box("Bed_Frame", new Vector3(bcx, y + 0.2f, bcz), new Vector3(bD, 0.4f, bW), woodDark);
                    Box("Bed_Mattress", new Vector3(bcx, y + 0.47f, bcz), new Vector3(bD - 0.1f, 0.14f, bW - 0.1f), fabricWhite);
                    Box("Bed_Blanket", new Vector3(bcx - 0.3f, y + 0.55f, bcz), new Vector3(bD * 0.65f, 0.04f, bW - 0.08f), fabricRed);
                    Box("Bed_Head", new Vector3(WingOut - 0.1f, y + 0.55f, bcz), new Vector3(0.08f, 1.1f, bW), woodDark);
                }
                blockers[0].Add(Rect.MinMaxRect(bcx - bD / 2f, bcz - bW / 2f, bcx + bD / 2f, bcz + bW / 2f));
                room.bed = new Seat
                {
                    kind = SeatKind.Bed, label = "ベッド", area = room.name, room = room,
                    position = new Vector3(bcx, y, bcz), forward = new Vector3(1f, 0f, 0f),
                    approach = new Vector3(bcx - bD / 2f - 0.45f, y, bcz),
                };
                seats.Add(room.bed);
                room.bedEdge = new Vector3(-1f, 0f, 0f);
                room.bedAcross = Vector3.forward;
                room.bedSitAlong = Vector3.forward;
                room.bedEdgeDist = bD / 2f - 0.3f;
                room.bedLieGap = 0.73f;
                room.bedLieWidth = bW - 0.1f;
                room.bedSeatOffs = new[] { -0.825f, -0.275f, 0.275f, 0.825f };
                foreach (var cu in shared) room.cushions.Add(cu);
                room.futon = futon;
                // 机（手前の外壁ぞい。部屋の中＝+z を向く）と椅子
                float dx = dxs[i], dz = FrontOut + 0.4f;
                if (PlaceFurn("Furn_Desk", null, new Vector3(dx, y, dz), 0f) == null)
                {
                    Box("Desk", new Vector3(dx, y + 0.73f, dz), new Vector3(0.8f, 0.05f, 0.6f), woodMid);
                    Box("Desk_Body", new Vector3(dx, y + 0.36f, dz), new Vector3(0.75f, 0.72f, 0.55f), woodDark);
                }
                blockers[0].Add(Rect.MinMaxRect(dx - 0.42f, FrontOut, dx + 0.42f, dz + 0.35f));
                Vector3 chairP = new Vector3(dx, y, dz + 0.62f);
                PlaceFurn("Furn_Chair", null, chairP, 180f);
                room.desk = new Poi { label = "机で勉強している", lookAt = new Vector3(dx, y + 0.8f, dz), slots = new[] { chairP }, occupants = new SimCharacter[1], seated = true };
                pois.Add(room.desk);
                rooms.Add(room);
            }
            foreach (var cu in shared) cu.room = rooms[rooms.Count - 3];
            futon.room = rooms[rooms.Count - 3];
        }

        // ================= 篤の部屋（2階・もとの玄関ホールの所） =================
        /// <summary>篤の部屋：2階の手前の真ん中（x −8.6〜6、z −18〜−9）。入口の通路の隣。吹き抜けのまわりの通路から入る（famuru 決定 2026-10-25）。</summary>
        void BuildFRoom()
        {
            int fl = 1;
            float x0 = -WingIn, x1 = PassX0, z0 = FrontOut, z1 = FrontZ, y = FloorY(fl);
            var room = new RoomInfo { name = "篤", floor = fl };
            // ベッド（5人並んで寝られる・6人腰かけられる 3.5×2.2m。famuru 2026-10-26）
            // 頭板は手前の外壁（−z）。足元（部屋の中、+z）の縁に並んで座る。東の通路側に寄せる
            float bw = 3.5f, bd = 2.2f;                       // 幅（x）・奥行き（z）
            float bcx = x1 - 0.6f - bw / 2f;                  // x 1.65〜5.15
            float bz = z0 + 0.2f + bd / 2f;
            if (PlaceFurn("Furn_BedLarge", null, new Vector3(bcx, y, bz), 0f) == null)
            {
                Box("Bed_Frame", new Vector3(bcx, y + 0.2f, bz), new Vector3(bw, 0.4f, bd), woodDark);
                Box("Bed_Mattress", new Vector3(bcx, y + 0.47f, bz), new Vector3(bw - 0.1f, 0.14f, bd - 0.1f), fabricWhite);
                Box("Bed_Blanket", new Vector3(bcx, y + 0.55f, bz + 0.35f), new Vector3(bw - 0.08f, 0.04f, bd * 0.65f), fabricGreen);
                Box("Bed_Head", new Vector3(bcx, y + 0.6f, z0 + 0.24f), new Vector3(bw, 1.2f, 0.08f), woodDark);
            }
            blockers[fl].Add(Rect.MinMaxRect(bcx - bw / 2f, z0, bcx + bw / 2f, bz + bd / 2f));
            room.bed = new Seat
            {
                kind = SeatKind.Bed, label = "ベッド", area = room.name, position = new Vector3(bcx, y, bz),
                forward = new Vector3(0f, 0f, -1f), approach = new Vector3(bcx - 0.4f, y, bz + bd / 2f + 0.5f), room = room
            };
            seats.Add(room.bed);
            room.bedEdge = new Vector3(0f, 0f, 1f); // 縁は足元（部屋の中）
            room.bedAcross = Vector3.right;
            room.bedSitAlong = Vector3.right;
            room.bedSpread = 1.6f;
            room.bedEdgeDist = bd / 2f - 0.3f;
            room.bedLieGap = 0.70f;
            room.bedLieWidth = bw - 0.1f;
            room.bedSeatOffs = new[] { -1.45f, -0.87f, -0.29f, 0.29f, 0.87f, 1.45f };
            // ベッドの縁に座る席（6人）
            foreach (float o in room.bedSeatOffs)
            {
                seats.Add(new Seat
                {
                    kind = SeatKind.Sit, label = "篤の部屋のベッド", area = room.name, room = room,
                    position = new Vector3(bcx + o, y, bz + bd / 2f - 0.2f), forward = new Vector3(0f, 0f, 1f),
                    approach = new Vector3(bcx + o, y, bz + bd / 2f + 0.45f)
                });
            }
            // 机（手前の外壁ぎわ・西。本番の家具があればそれを置く）
            float dX = -5.5f, dZ = z0 + 0.5f;
            if (PlaceFurn("Furn_Desk", null, new Vector3(dX, y, dZ), 0f) == null)
            {
            Box("Desk", new Vector3(dX, y + 0.73f, dZ), new Vector3(1.2f, 0.05f, 0.6f), woodMid);
            Box("Desk_Body", new Vector3(dX, y + 0.36f, dZ), new Vector3(1.1f, 0.72f, 0.55f), woodDark);
            Box("Desk_Book", new Vector3(dX - 0.3f, y + 0.8f, dZ), new Vector3(0.25f, 0.06f, 0.18f), fabricRed);
            var lampGlow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lampGlow.name = "DeskLamp";
            lampGlow.transform.SetParent(root, false);
            lampGlow.transform.position = new Vector3(dX + 0.4f, y + 0.95f, dZ - 0.1f);
            lampGlow.transform.localScale = Vector3.one * 0.12f;
            lampGlow.GetComponent<Renderer>().sharedMaterial = glowLamp;
            Destroy(lampGlow.GetComponent<Collider>());
            }
            blockers[fl].Add(Rect.MinMaxRect(dX - 0.6f, z0, dX + 0.6f, dZ + 0.3f));
            Vector3 chairQ = new Vector3(dX, y, dZ + 0.62f);   // 机の椅子（2026-10-30 モーション）
            PlaceFurn("Furn_Chair", null, chairQ, 180f);
            room.desk = new Poi { label = "机で勉強している", lookAt = new Vector3(dX, y + 0.8f, dZ), slots = new[] { chairQ }, occupants = new SimCharacter[1], seated = true };
            pois.Add(room.desk);
            // 本棚（西の壁ぎわ）
            ShelfRow(new Vector3(x0 + 0.4f, y, -13.5f), -90f, 2.0f, 2.0f, 4, fabricRed, woodMid, 401);
            // ソファ（奥の壁ぎわ。3人掛け。部屋の中を向く）
            Sofa("篤の部屋のソファ", room.name, new Vector3(2.6f, y, z1 - 0.65f), 180f, 2.6f, 3, fabricRed);
            // 低いテーブルと座布団2枚
            float tx = 2.6f, tz = -12.4f;
            if (PlaceFurn("Furn_LowTableSmall", null, new Vector3(tx, y, tz), 0f, false, new Vector3(1.8f, 1f, 1.2f)) == null)
                Box("LowTable_Room", new Vector3(tx, y + 0.15f, tz), new Vector3(0.9f, 0.3f, 0.6f), woodMid);
            blockers[fl].Add(Rect.MinMaxRect(tx - 0.45f, tz - 0.3f, tx + 0.45f, tz + 0.3f));
            for (int k = 0; k < 2; k++)
            {
                float cx = k == 0 ? tx - 0.85f : tx + 0.85f;
                if (PlaceFurn("Furn_Cushion", null, new Vector3(cx, y, tz), 0f) == null)
                    Box("Cushion", new Vector3(cx, y + 0.04f, tz), new Vector3(0.5f, 0.08f, 0.5f), fabricGreen);
                var cu = new Seat
                {
                    kind = SeatKind.Sit, floorSeat = true, label = "空き部屋", area = room.name,
                    position = new Vector3(cx, y, tz), forward = new Vector3(k == 0 ? 1f : -1f, 0f, 0f),
                    approach = new Vector3(cx, y, tz + 0.65f), room = room
                };
                room.cushions.Add(cu);
                seats.Add(cu);
            }
            // 来客用の布団（西寄り）
            if (PlaceFurn("Furn_GuestFuton", null, new Vector3(-5.0f, y, -11.0f), 0f) == null)
                Box("GuestFuton", new Vector3(-5.0f, y + 0.05f, -11.0f), new Vector3(1.7f, 0.1f, 0.75f), fabricWhite);
            room.futon = new Seat
            {
                kind = SeatKind.Bed, floorSeat = true, label = "来客用の布団", area = room.name,
                position = new Vector3(-5.0f, y, -11.0f), forward = new Vector3(1f, 0f, 0f), approach = new Vector3(-5.0f, y, -10.35f), room = room
            };
            seats.Add(room.futon);
            // タンス（東の壁ぎわ・奥）
            if (PlaceFurn("Furn_Wardrobe", null, new Vector3(x1 - 0.48f, y, z1 - 1.0f), -90f, false, new Vector3(1.0f / 0.7f, 1f, 1.2f)) == null) // 西を向く・横に伸ばす
                Box("Wardrobe", new Vector3(x1 - 0.45f, y + 0.9f, z1 - 1.0f), new Vector3(0.6f, 1.8f, 1.0f), woodDark);
            blockers[fl].Add(Rect.MinMaxRect(x1 - 0.8f, z1 - 1.55f, x1 - 0.1f, z1 - 0.45f));
            Lamp(new Vector3(-1.3f, y + 2.4f, -13.5f), 2.2f, 8f, false, 0.03f);
            room.labelPos = new Vector3(-1.3f, y + 2.0f, -13.5f);
            room.rect = Rect.MinMaxRect(x0, z0, x1, z1);
            AddArea(room.AreaName, room.rect, fl);
            rooms.Add(room);
        }

        // ================= 奥（1階：食堂・脱衣所・浴場／2階：娯楽室・トイレ／3階：勉強部屋・図書室） =================
        void BuildBackRooms()
        {
            // --- 談話室・廊下と奥の部屋の境の壁（z 9）。階ごとに扉 ---
            float a0 = -WingOut;
            // 1階の食堂の扉は x −0.9（1階→2階の階段が x 0.6〜7.6 に来たため。前は 6.5）。2階の娯楽室は北の廊下の扉だけ（前の x −3.5 は2階→3階の階段の所）
            WallF(0, new Vector2(-WingOut, BackZ), new Vector2(WingOut, BackZ), DoorGap(0, -2.75f - a0, 0.9f), DoorGap(0, -0.9f - a0, 1.2f));
            WallF(1, new Vector2(-WingOut, BackZ), new Vector2(WingOut, BackZ), DoorGap(1, -9.8f - a0), DoorGap(1, 9.8f - a0));
            WallF(2, new Vector2(-WingOut, BackZ), new Vector2(WingOut, BackZ), DoorGap(2, -9.8f - a0), DoorGap(2, 9.8f - a0));
            // 仕切り（1階：台所｜食堂｜脱衣所｜浴場。台所の扉は鍵つき＝DormPlaces、脱衣所→浴場の扉 z 13.5）
            WallF(0, new Vector2(KitchenX0, BackZ), new Vector2(KitchenX0, BackOut), DoorGap(0, 12.8f - BackZ, 6.0f, 2.4f), DoorGap(0, KitchenDoorZ - BackZ, 1.0f, 2.4f)); // 配膳の口（幅6m、famuru 2026-10-26）と台所の扉
            WallF(0, new Vector2(-2f, BackZ), new Vector2(-2f, BackOut));
            WallF(0, new Vector2(-6f, BackZ), new Vector2(-6f, BackOut), DoorGap(0, 13.5f - BackZ, 0.9f));
            WallF(1, new Vector2(-6f, BackZ), new Vector2(-6f, BackOut));   // 娯楽室｜トイレ
            WallF(2, new Vector2(0f, BackZ), new Vector2(0f, BackOut));     // 勉強部屋｜図書室

            // --- 食堂（x −2〜11、z 9〜18。台所を小さくした分広い） ---
            Label(new Vector3(DiningCenter.x, 2.6f, BackZ + 0.6f), "食堂");
            AddArea("食堂", Rect.MinMaxRect(-2f, BackZ, KitchenX0, BackOut), 0);
            // 配膳カウンター2つ（壁の口に並べる。トレーの棚の側＝食堂の −x。料理は台所の側から出す。famuru 2026-10-26）
            float cx = KitchenX0;
            bool sc1 = PlaceFurn("Furn_ServingCounter", null, new Vector3(cx, 0f, 11.3f), -90f) != null;
            bool sc2 = PlaceFurn("Furn_ServingCounter", null, new Vector3(cx, 0f, 14.3f), -90f) != null;
            if (!sc1 || !sc2)
            {
            Box("Counter", new Vector3(cx, 0.5f, 12.8f), new Vector3(0.8f, 1.0f, 6.0f), woodDark);
            Box("Counter_Top", new Vector3(cx, 1.02f, 12.8f), new Vector3(0.9f, 0.05f, 6.1f), woodMid);
            }
            blockers[0].Add(Rect.MinMaxRect(cx - 0.5f, 9.8f, cx + 0.5f, 15.8f));
            pois.Add(new Poi
            {
                label = "料理を受け取っている",
                lookAt = new Vector3(cx, 1f, 12.8f),
                slots = new[] { new Vector3(cx - 1.0f, 0f, 10.6f), new Vector3(cx - 1.0f, 0f, 11.7f), new Vector3(cx - 1.0f, 0f, 12.8f), new Vector3(cx - 1.0f, 0f, 13.9f), new Vector3(cx - 1.0f, 0f, 15.0f) },
                occupants = new SimCharacter[5],
                counter = true
            });
            // 縦長の机3つ（18席。机と机の間は通路）
            // 横長の机を2列×2つ（24席。famuru 2026-10-26。真ん中の x 2.6〜4.8 は中庭への通路をあける）
            foreach (float tz in new[] { 11.7f, 15.3f })
                foreach (float tx in new[] { 0.9f, 6.5f })
                {
                    DiningTable(new Vector3(tx, 0f, tz), true);
                    Chandelier(new Vector3(tx, 2.6f, tz), 3.0f, 6f);
                }

            // --- 脱衣所（x −6〜−2） ---
            Label(new Vector3(-4f, 2.6f, BackZ + 0.6f), "脱衣所");
            AddArea("脱衣所", Rect.MinMaxRect(-6f, BackZ, -2f, BackOut), 0);
            for (int i = 0; i < 5; i++)
                if (PlaceFurn("Furn_Locker", null, new Vector3(-5.5f + i * 0.75f, 0f, BackOut - 0.45f), 180f) == null)
                    Box("Locker", new Vector3(-5.5f + i * 0.75f, 0.9f, BackOut - 0.45f), new Vector3(0.65f, 1.8f, 0.4f), woodMid);
            blockers[0].Add(Rect.MinMaxRect(-5.85f, BackOut - 0.7f, -2.15f, BackOut));
            if (PlaceFurn("Furn_DressingBench", null, new Vector3(-4f, 0f, 14.0f), 90f) == null)
                Box("Dressing_Bench", new Vector3(-4f, 0.22f, 14.0f), new Vector3(0.45f, 0.44f, 1.6f), woodDark);
            blockers[0].Add(new Rect(-4.225f, 13.2f, 0.45f, 1.6f));
            if (PlaceFurn("Furn_Basket", null, new Vector3(-2.5f, 0f, 16.2f), 180f) == null)
                Box("Dressing_Basket", new Vector3(-2.5f, 0.15f, 16.2f), new Vector3(0.5f, 0.3f, 0.4f), fabricBrown);
            blockers[0].Add(new Rect(-2.75f, 16.0f, 0.5f, 0.4f));
            Lamp(new Vector3(-4f, 2.6f, 13.5f), 2.0f, 5f);

            // --- 浴場（x −17〜−6。湯船は6人、奥に洗い場） ---
            Label(new Vector3(-11.5f, 2.6f, BackZ + 0.6f), "浴場");
            AddArea("浴場", Rect.MinMaxRect(-WingOut, BackZ, -6f, BackOut), 0);
            float tx0 = -15.6f, tx1 = -10.6f, tz0 = 10.6f, tz1 = 15.6f;
            float rim = 0.3f, rimH = 0.5f;
            if (PlaceFurn("Furn_Bathtub", null, new Vector3((tx0 + tx1) / 2f, 0f, (tz0 + tz1) / 2f), 0f) == null)
            {
            Box("Tub_RimL", new Vector3(tx0 + rim / 2f, rimH / 2f, (tz0 + tz1) / 2f), new Vector3(rim, rimH, tz1 - tz0), mortarDark);
            Box("Tub_RimR", new Vector3(tx1 - rim / 2f, rimH / 2f, (tz0 + tz1) / 2f), new Vector3(rim, rimH, tz1 - tz0), mortarDark);
            Box("Tub_RimF", new Vector3((tx0 + tx1) / 2f, rimH / 2f, tz0 + rim / 2f), new Vector3(tx1 - tx0, rimH, rim), mortarDark);
            Box("Tub_RimB", new Vector3((tx0 + tx1) / 2f, rimH / 2f, tz1 - rim / 2f), new Vector3(tx1 - tx0, rimH, rim), mortarDark);
            }
            Box("Tub_Water", new Vector3((tx0 + tx1) / 2f, 0.38f, (tz0 + tz1) / 2f), new Vector3(tx1 - tx0 - rim * 2f, 0.04f, tz1 - tz0 - rim * 2f), waterMat);
            blockers[0].Add(Rect.MinMaxRect(tx0, tz0, tx1, tz1));
            float[] bx = { -14.4f, -12.0f };
            float[] bz = { 11.8f, 13.1f, 14.4f };
            foreach (var x in bx)
                foreach (var z in bz)
                    seats.Add(new Seat { kind = SeatKind.Bath, label = "湯船", area = "浴場", position = new Vector3(x, 0f, z), forward = Vector3.right, approach = new Vector3(tx1 + 0.6f, 0f, z) });
            // 洗い場（奥の壁ぎわ。小さな腰掛けと桶）
            for (int i = 0; i < 5; i++)
            {
                float x = -15.6f + i * 1.6f;
                if (PlaceFurn("Furn_WashStool", null, new Vector3(x, 0f, BackOut - 0.75f), 0f) == null)
                    Box("WashStool", new Vector3(x, 0.15f, BackOut - 0.75f), new Vector3(0.35f, 0.3f, 0.35f), woodMid);
                if (PlaceFurn("Furn_WashBucket", null, new Vector3(x + 0.35f, 0f, BackOut - 0.65f), 0f) == null)
                    Cyl("WashBucket", new Vector3(x + 0.35f, 0.12f, BackOut - 0.65f), new Vector3(0.3f, 0.12f, 0.3f), Quaternion.identity, woodDark);
                if (PlaceFurn("Furn_WashMirror", null, new Vector3(x, 0.9f, BackOut - 0.185f), 180f) == null) // 壁に掛ける物
                    Box("WashMirror", new Vector3(x, 1.2f, BackOut - 0.2f), new Vector3(0.5f, 0.6f, 0.03f), waterMat);
                blockers[0].Add(new Rect(x - 0.25f, BackOut - 1.0f, 0.8f, 0.85f));
            }
            Lamp(new Vector3(-11.5f, 2.6f, 13.5f), 2.5f, 8f);

            // --- 2階：娯楽室・トイレ（家具はこのあと） ---
            float y1 = FloorY(1);
            Label(new Vector3(5.5f, y1 + 2.4f, 13.5f), "娯楽室");
            AddArea("娯楽室", Rect.MinMaxRect(-6f, BackZ, WingOut, BackOut), 1);
            Lamp(new Vector3(0f, y1 + 2.4f, 13.5f), 2.2f, 8f);
            Lamp(new Vector3(10f, y1 + 2.4f, 13.5f), 2.2f, 8f);
            Label(new Vector3(-11.5f, y1 + 2.4f, 13.5f), "トイレ");
            AddArea("トイレ", Rect.MinMaxRect(-WingOut, BackZ, -6f, BackOut), 1);
            Lamp(new Vector3(-11.5f, y1 + 2.4f, 13.5f), 1.8f, 7f);

            // --- 3階：勉強部屋（図書室は DormPlaces） ---
            float y2 = FloorY(2);
            Label(new Vector3(8.5f, y2 + 2.4f, 13.5f), "勉強部屋");
            AddArea("勉強部屋", Rect.MinMaxRect(0f, BackZ, WingOut, BackOut), 2);
            Lamp(new Vector3(4f, y2 + 2.4f, 13.5f), 2.2f, 8f);
            Lamp(new Vector3(12f, y2 + 2.4f, 13.5f), 2.2f, 8f);
        }

        void DiningTable(Vector3 c, bool alongX = false)
        {
            if (alongX)
            {
                // 横（x）に長い机。椅子は前後に3脚ずつ
                if (PlaceFurn("Furn_DiningTable", null, new Vector3(c.x, 0f, c.z), 0f) == null)
                    Box("DiningTable", new Vector3(c.x, 0.75f, c.z), new Vector3(3.3f, 0.08f, 0.9f), woodMid);
                blockers[0].Add(new Rect(c.x - 1.65f, c.z - 0.45f, 3.3f, 0.9f));
                foreach (var dx in new[] { -1.1f, 0f, 1.1f })
                {
                    Chair("食堂の席", "食堂", new Vector3(c.x + dx, 0f, c.z - 0.8f), 0f);
                    Chair("食堂の席", "食堂", new Vector3(c.x + dx, 0f, c.z + 0.8f), 180f);
                }
                return;
            }
            // 奥行き方向（z）に長い机。椅子は左右に3脚ずつ、座る前は外側の通路に立つ
            if (PlaceFurn("Furn_DiningTable", null, new Vector3(c.x, 0f, c.z), 90f) == null) // 本番の家具（長い辺は z）
            {
            Box("DiningTable", new Vector3(c.x, 0.75f, c.z), new Vector3(0.9f, 0.08f, 3.3f), woodMid);
            for (int i = 0; i < 4; i++)
            {
                float x = c.x + ((i % 2 == 0) ? -0.35f : 0.35f), z = c.z + ((i < 2) ? -1.5f : 1.5f);
                Box("DiningTable_Leg", new Vector3(x, 0.37f, z), new Vector3(0.1f, 0.74f, 0.1f), woodDark);
            }
            Box("DiningTable_Candle", new Vector3(c.x, 0.9f, c.z), new Vector3(0.06f, 0.22f, 0.06f), glowLamp);
            }
            blockers[0].Add(new Rect(c.x - 0.45f, c.z - 1.65f, 0.9f, 3.3f));
            float[] zs = { -1.1f, 0f, 1.1f };
            foreach (var dz in zs)
            {
                Chair("食堂の席", "食堂", new Vector3(c.x - 0.8f, 0f, c.z + dz), 90f);
                Chair("食堂の席", "食堂", new Vector3(c.x + 0.8f, 0f, c.z + dz), -90f);
            }
        }

        // ================= 手前（1階：寮母さんの部屋・倉庫／2階：小さな談話室・入口の通路・篤の部屋／3階：トレーニング部屋・シャワールーム） =================
        void BuildFrontRooms()
        {
            float a0 = -WingOut;
            // 廊下・吹き抜けのまわりと手前の部屋の境の壁（z −9）
            WallF(0, new Vector2(-WingOut, FrontZ), new Vector2(-WingIn, FrontZ), DoorGap(0, -9.8f - a0));
            WallF(0, new Vector2(WingIn, FrontZ), new Vector2(WingOut, FrontZ), DoorGap(0, 9.8f - WingIn));
            WallF(1, new Vector2(-WingOut, FrontZ), new Vector2(-WingIn, FrontZ), DoorGap(1, -9.8f - a0));
            WallF(1, new Vector2(-WingIn, FrontZ), new Vector2(PassX0, FrontZ), DoorGap(1, -1.0f + WingIn));
            WallF(1, new Vector2(PassX1, FrontZ), new Vector2(WingOut, FrontZ), DoorGap(1, 9.8f - PassX1));
            WallF(2, new Vector2(-WingOut, FrontZ), new Vector2(-WingIn, FrontZ), DoorGap(2, -9.8f - a0));
            WallF(2, new Vector2(-WingIn, FrontZ), new Vector2(WingOut, FrontZ), DoorGap(2, 0f + WingIn), DoorGap(2, 9.8f + WingIn));
            // 仕切り（x）
            WallF(1, new Vector2(-WingIn, FrontOut), new Vector2(-WingIn, FrontZ));   // 小さな談話室（南）｜篤の部屋
            WallF(1, new Vector2(PassX0, FrontOut), new Vector2(PassX0, FrontZ));    // 篤の部屋｜入口の通路
            WallF(1, new Vector2(PassX1, FrontOut), new Vector2(PassX1, FrontZ));    // 入口の通路｜小さな談話室（北）
            WallF(2, new Vector2(-WingIn, FrontOut), new Vector2(-WingIn, FrontZ));   // シャワールーム｜トレーニング部屋

            float cn = (WingIn + WingOut) / 2f, cs = -cn, cz = (FrontZ + FrontOut) / 2f;
            // 1階
            // 寮母さんの部屋は、先生3人の部屋にした（2026-10-30 famuru「寮母の部屋を使う」。家具は BuildTeacherRoom）
            Lamp(new Vector3(cn, 2.4f, cz), 1.8f, 6f);
            Label(new Vector3(cs, 2.2f, cz), "倉庫");
            AddArea("倉庫", Rect.MinMaxRect(-WingOut, FrontOut, -WingIn, FrontZ), 0);
            for (int i = 0; i < 3; i++)
            {
                { if (PlaceFurn("Furn_Crate", null, new Vector3(-15.8f + i * 1.1f, 0f, FrontOut + 0.7f), 0f) == null) Box("Crate", new Vector3(-15.8f + i * 1.1f, 0.4f, FrontOut + 0.7f), new Vector3(0.9f, 0.8f, 0.9f), woodMid); }
                blockers[0].Add(new Rect(-16.3f + i * 1.1f, FrontOut + 0.2f, 1.0f, 1.0f));
            }
            Lamp(new Vector3(cs, 2.4f, cz), 1.5f, 6f);
            // 2階
            float y1 = FloorY(1);
            Label(new Vector3(cn, y1 + 2.2f, cz), "小さな談話室（北）");
            AddArea("小さな談話室（北）", Rect.MinMaxRect(PassX1, FrontOut, WingOut, FrontZ), 1);
            Lamp(new Vector3(cn, y1 + 2.4f, cz), 1.8f, 6f);
            Label(new Vector3(cs, y1 + 2.2f, cz), "小さな談話室（南）");
            AddArea("小さな談話室（南）", Rect.MinMaxRect(-WingOut, FrontOut, -WingIn, FrontZ), 1);
            Lamp(new Vector3(cs, y1 + 2.4f, cz), 1.8f, 6f);
            Label(new Vector3((PassX0 + PassX1) / 2f, y1 + 2.2f, -12f), "入口の通路");
            AddArea("入口の通路", Rect.MinMaxRect(PassX0, FrontOut, PassX1, FrontZ), 1);
            Lamp(new Vector3((PassX0 + PassX1) / 2f, y1 + 2.4f, -13.5f), 1.6f, 6f);
            // 3階
            float y2 = FloorY(2);
            Label(new Vector3(4f, y2 + 2.2f, cz), "トレーニング部屋");
            AddArea("トレーニング部屋", Rect.MinMaxRect(-WingIn, FrontOut, WingOut, FrontZ), 2);
            Lamp(new Vector3(0f, y2 + 2.4f, cz), 2.2f, 8f);
            Lamp(new Vector3(10f, y2 + 2.4f, cz), 2.2f, 8f);
            Label(new Vector3(cs, y2 + 2.2f, cz), "シャワールーム");
            AddArea("シャワールーム", Rect.MinMaxRect(-WingOut, FrontOut, -WingIn, FrontZ), 2);
            Lamp(new Vector3(cs, y2 + 2.4f, cz), 1.8f, 6f);
        }

        // ================= 外壁 =================
        void BuildOuterWalls()
        {
            float a0 = WingOut + 0.15f; // 外壁は x −17.15 から。壁に沿った位置 = x + a0
            for (int f = 0; f <= 2; f++)
            {
                float y = FloorY(f);
                // 手前（z −18）：1階は談話室の入り口、2階は入口の通路（渡り廊下へ）
                var gapsF = new List<Vector4>();
                if (f == 0) gapsF.Add(new Vector4(a0 - 1.0f, a0 + 1.0f, y, y + 2.8f));
                if (f == 1) gapsF.Add(new Vector4(a0 + PassX0 + 0.2f, a0 + PassX1 - 0.2f, y, y + 2.6f));
                var front = WallF(f, new Vector2(-WingOut - 0.15f, FrontOut), new Vector2(WingOut + 0.15f, FrontOut), gapsF.ToArray());
                float[] fwx = f == 1 ? new[] { -13f, -5f, 2f, 13f } : new[] { -13f, -5f, 5f, 13f };
                foreach (float x in fwx) Window(front, new Vector3(x, y, FrontOut), Vector3.forward, 1.0f, 1.0f, 1.4f);
                // 奥（z 18）：1階の食堂に中庭への扉（鍵つき＝DormPlaces）
                var gapsB = new List<Vector4>();
                if (f == 0) gapsB.Add(new Vector4(a0 + 1.6f, a0 + 4.0f, y, y + 2.8f)); // 中庭への広い通路（幅2.4m。famuru 2026-10-25）
                var back = WallF(f, new Vector2(-WingOut - 0.15f, BackOut), new Vector2(WingOut + 0.15f, BackOut), gapsB.ToArray());
                float[] bwx = f == 0 ? new[] { -11.5f, 0.0f, 6.0f, 13f } : new[] { -13f, -5f, 5f, 13f };
                foreach (float x in bwx) Window(back, new Vector3(x, y, BackOut), Vector3.back, f == 0 && x < -6f ? 1.8f : 1.0f, 1.0f, f == 0 && x < -6f ? 0.9f : 1.4f);
                // 北と南（x ±17）：個室ごとに窓
                for (int s = -1; s <= 1; s += 2)
                {
                    var side = WallF(f, new Vector2(s * WingOut, FrontOut), new Vector2(s * WingOut, BackOut));
                    for (int i = 0; i < 5; i++) Window(side, new Vector3(s * WingOut, y, FrontZ + RoomW * (i + 0.5f)), new Vector3(-s, 0f, 0f), 1.0f, 0.9f, 1.1f);
                    Window(side, new Vector3(s * WingOut, y, (FrontZ + FrontOut) / 2f), new Vector3(-s, 0f, 0f), 1.0f, 1.0f, 1.3f);
                    Window(side, new Vector3(s * WingOut, y, (BackZ + BackOut) / 2f), new Vector3(-s, 0f, 0f), 1.0f, 1.0f, 1.3f);
                }
            }
            // 入り口の飾り（1階の手前の真ん中）
            Box("Door_FrameL", new Vector3(-1.15f, 1.5f, FrontOut), new Vector3(0.3f, 3f, 0.5f), mortarDark);
            Box("Door_FrameR", new Vector3(1.15f, 1.5f, FrontOut), new Vector3(0.3f, 3f, 0.5f), mortarDark);
            Label(new Vector3(0f, 2.6f, FrontOut + 0.4f), "寮の入り口（前庭へ）");
        }

        // ================= 小さな前庭（1階の入り口の前、渡り廊下の下） =================
        void BuildFrontYard()
        {
            float z0 = -24.5f, z1 = FrontOut - 0.2f;
            Box("Yard_FrontPath", new Vector3(0f, -0.02f, (z0 + z1) / 2f), new Vector3(2.4f, 0.04f, z1 - z0), flagstone);
            for (int k = -1; k <= 1; k += 2)
            {
                float x = k * 3.6f;
                if (PlaceFurn("Furn_FlowerBed", null, new Vector3(x, 0f, -21.5f), 0f, false, new Vector3(2.2f / 1.6f, 1f, 1.1f)) == null)
                {
                Box("FrontBed", new Vector3(x, 0.12f, -21.5f), new Vector3(2.2f, 0.24f, 1.1f), woodDark);
                Box("FrontBed_Soil", new Vector3(x, 0.25f, -21.5f), new Vector3(2.05f, 0.04f, 0.95f), fabricBrown);
                for (int i = 0; i < 4; i++)
                {
                    var leaf = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    leaf.name = "FrontFlower";
                    leaf.transform.SetParent(root, false);
                    leaf.transform.localPosition = new Vector3(x - 0.75f + i * 0.5f, 0.42f, -21.5f);
                    leaf.transform.localScale = Vector3.one * 0.35f;
                    leaf.GetComponent<Renderer>().sharedMaterial = i % 2 == 0 ? fabricGreen : fabricRed;
                    Destroy(leaf.GetComponent<Collider>());
                }
                }
                blockers[0].Add(new Rect(x - 1.1f, -22.05f, 2.2f, 1.1f));
            }
            Bench(new Vector3(3.6f, 0f, -23.4f), 0f);
            // 学校の本館の裏口（x −10）への小道（本館を1.5倍にして裏口が南へ動いた。2026-10-25）
            Box("Yard_SidePath", new Vector3(-5.5f, -0.02f, -23.6f), new Vector3(10f, 0.04f, 1.6f), flagstone);
            Label(new Vector3(0f, 1.6f, -22.5f), "前庭");
            AddArea("前庭", Rect.MinMaxRect(-11.5f, z0, 10f, FrontOut), 0);
        }

        // ================= 渡り廊下（2階の入口から学校へ。学校の本館はこのあと作る） =================
        void BuildBridge()
        {
            float y = FloorY(1), x0 = PassX0, x1 = PassX1, z0 = -24.5f, z1 = FrontOut;
            Slab("Bridge_Floor", 1, Rect.MinMaxRect(x0, z0, x1, z1), woodFloor);
            Rail(1, new Vector2(x0 + 0.05f, z0), new Vector2(x0 + 0.05f, z1));
            Rail(1, new Vector2(x1 - 0.05f, z0), new Vector2(x1 - 0.05f, z1));
            for (int k = 0; k < 2; k++)
            {
                float x = k == 0 ? x0 + 0.2f : x1 - 0.2f;
                foreach (float bzp in new[] { z0 + 0.3f, -21.2f })
                    if (PlaceFurn("Furn_Pillar", null, new Vector3(x, 0f, bzp), 0f, false, new Vector3(0.35f / 0.4f, y / 4.5f, 0.35f / 0.4f)) == null)
                        Box("Bridge_Pillar", new Vector3(x, y / 2f, bzp), new Vector3(0.35f, y, 0.35f), mortarDark);
                blockers[0].Add(new Rect(x - 0.2f, z0 + 0.1f, 0.4f, 0.4f));
                blockers[0].Add(new Rect(x - 0.2f, -21.4f, 0.4f, 0.4f));
            }
            Lamp(new Vector3((x0 + x1) / 2f, y + 2.2f, -21.5f), 1.6f, 6f);
            Label(new Vector3((x0 + x1) / 2f, y + 2.2f, z0 + 1.0f), "渡り廊下（学校へ）");
            AddArea("外", Rect.MinMaxRect(x0, z0, x1, z1), 1);
        }

        // ================= 屋上 =================
        void BuildRoof()
        {
            float y = FloorY(3);
            var sm = StoneFor(34f, 0.9f);
            Box("Parapet_F", new Vector3(0f, y + 0.45f, FrontOut), new Vector3(WingOut * 2f + 0.3f, 0.9f, 0.3f), sm);
            Box("Parapet_B", new Vector3(0f, y + 0.45f, BackOut), new Vector3(WingOut * 2f + 0.3f, 0.9f, 0.3f), sm);
            Box("Parapet_N", new Vector3(WingOut, y + 0.45f, 0f), new Vector3(0.3f, 0.9f, BackOut * 2f), sm);
            Box("Parapet_S", new Vector3(-WingOut, y + 0.45f, 0f), new Vector3(0.3f, 0.9f, BackOut * 2f), sm);
            Lamp(new Vector3(0f, y + 2.0f, 8.0f), 1.6f, 6f);
            PlaceFurn("Furn_Chimney", null, new Vector3(FireplaceSpot.x, y, BackZ - 0.3f), 180f); // 暖炉の煙突の頭（屋上）
        }

        /// <summary>広い区画は最後に（AreaAt は最初に当たった区画を返す）。</summary>
        void FinishAreas()
        {
            AddArea("談話室", Rect.MinMaxRect(-WingIn, FrontOut, WingIn, BackZ), 0);
            for (int f = 1; f <= 2; f++)
            {
                string n = (f + 1) + "階・吹き抜けのまわり";
                AddArea(n, Rect.MinMaxRect(-WingIn, FrontZ, WingIn, VoidZ0), f);
                AddArea(n, Rect.MinMaxRect(-WingIn, VoidZ1, WingIn, BandZ1), f);
            }
            AddArea("屋上", Rect.MinMaxRect(-WingOut, FrontOut, WingOut, BackOut), 3);
        }

        // ================= 家具 =================
        Poi MakePoi(string label, Vector3 lookAt, params Vector3[] slots)
        {
            return new Poi { label = label, lookAt = lookAt, slots = slots, occupants = new SimCharacter[slots.Length] };
        }

        // ================= 本番の家具（3Dモデル） =================
        /// <summary>
        /// Resources/Furniture/Furn_（英名）.fbx があれば置いて返す（ないときは null → 今までの箱）。
        /// 決まりは D:\ゲーム制作\連携\家具モデル仕様.md（原点は床の中心、正面は Unity の +Z、頭板などの「左」は +X）。
        /// 依頼13 第1段の試し（家具のチャット 2026-10-26）。フォルダを外せば元の箱に戻る。
        /// </summary>
        static readonly string[] WalkableFurn = { "Furn_LoungeRug", "Furn_LibraryRug", "Furn_RoundRug", "Furn_TerraceRug", "Furn_DecorRug", "Furn_TrainingMat", "Furn_GymMat", "Furn_MagicCircle", "Furn_Stairs", "Furn_GuestFuton", "Furn_Cushion", "Furn_Bathtub" };

        GameObject PlaceFurn(string name, Transform parent, Vector3 localPos, float yaw, bool mirror = false, Vector3? scale = null)
        {
            var pf = Resources.Load<GameObject>("Furniture/" + name);
            if (pf == null) return null;
            var go = Instantiate(pf, parent != null ? parent : root, false);
            go.name = name;
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * pf.transform.localRotation;
            if (scale.HasValue) { var sc = go.transform.localScale; go.transform.localScale = Vector3.Scale(sc, scale.Value); }
            if (mirror) { var sc = go.transform.localScale; sc.x = -sc.x; go.transform.localScale = sc; }
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            // 2026-10-30（軽量化のチャット）：家具の材質は、同じ元の材質から作った物を全部の家具で使い回す（前は置くたびに別の材質ができていた）
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++)
                {
                    var src = ms[i];
                    if (src == null) continue;
                    Material m;
                    if (!furnMatCache.TryGetValue(src, out m))
                    {
                        m = new Material(src);
                        m.name = src.name;
                        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.12f);
                        if (m.HasProperty("_Metallic") && !m.name.StartsWith("Metal_")) m.SetFloat("_Metallic", 0f);
                        if (m.name.StartsWith("Glow_")) // ランプ・ろうそくの光る所
                        {
                            m.EnableKeyword("_EMISSION");
                            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", new Color(1f, 0.75f, 0.4f) * 2.2f);
                        }
                        furnMatCache[src] = m;
                    }
                    ms[i] = m;
                }
                r.sharedMaterials = ms;
            }
            if (System.Array.IndexOf(WalkableFurn, name) >= 0) // 上を歩く物：カメラが下にあるときは見えなくする（床と同じ）
                foreach (var r in go.GetComponentsInChildren<Renderer>()) slabRenderers.Add(new KeyValuePair<float, Renderer>(Mathf.Max(0.02f, go.transform.position.y), r));
            var seat = go.transform.Find("Seat_1");
            Debug.Log("[家具] " + name + " 置いた所 " + go.transform.position + (seat != null ? " Seat_1 " + seat.position : ""));
            return go;
        }

        void Bookshelf(Vector3 c, float w = 3f, float yaw = 0f, float h = 3.2f)
        {
            var t = new GameObject("Bookshelf").transform;
            t.SetParent(root, false);
            t.localPosition = c;
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            float d = 0.45f;
            // 本番の家具（幅2.6m・高さ2.6m）があれば、幅に合わせて並べる（本棚の正面は −z）
            if (Resources.Load<GameObject>("Furniture/Furn_Bookshelf") != null)
            {
                int n = Mathf.Max(1, Mathf.RoundToInt(w / 2.6f));
                float tw = w / n;
                for (int i = 0; i < n; i++)
                    PlaceFurn("Furn_Bookshelf", t, new Vector3(-w / 2f + tw * (i + 0.5f), 0f, 0f), 180f, false, new Vector3(tw / 2.6f, h / 2.6f, 1f));
                Vector3 qa = t.TransformPoint(new Vector3(-w / 2f - 0.1f, 0f, -0.35f));
                Vector3 qb = t.TransformPoint(new Vector3(w / 2f + 0.1f, 0f, 0.35f));
                blockers[FloorOf(c.y)].Add(Rect.MinMaxRect(Mathf.Min(qa.x, qb.x), Mathf.Min(qa.z, qb.z), Mathf.Max(qa.x, qb.x), Mathf.Max(qa.z, qb.z)));
                return;
            }
            Box("Shelf_Back", new Vector3(0f, h / 2f, 0.18f), new Vector3(w, h, 0.08f), woodDark, t);
            Box("Shelf_SideL", new Vector3(-w / 2f, h / 2f, 0f), new Vector3(0.08f, h, d), woodDark, t);
            Box("Shelf_SideR", new Vector3(w / 2f, h / 2f, 0f), new Vector3(0.08f, h, d), woodDark, t);
            Box("Shelf_Top", new Vector3(0f, h, 0f), new Vector3(w + 0.1f, 0.1f, d + 0.05f), woodDark, t);
            var bookMats = new[]
            {
                ProtoArt.Mat(new Color(0.45f, 0.1f, 0.1f)), ProtoArt.Mat(new Color(0.12f, 0.25f, 0.45f)),
                ProtoArt.Mat(new Color(0.15f, 0.35f, 0.18f)), ProtoArt.Mat(new Color(0.55f, 0.42f, 0.2f)),
                ProtoArt.Mat(new Color(0.3f, 0.18f, 0.35f)), ProtoArt.Mat(new Color(0.2f, 0.15f, 0.1f))
            };
            var rnd = new System.Random(Mathf.RoundToInt(c.x * 100 + c.z * 10));
            int tiers = Mathf.Max(3, Mathf.RoundToInt(h / 0.64f));
            for (int s = 0; s < tiers; s++)
            {
                float y = 0.1f + s * 0.62f;
                Box("Shelf_Board", new Vector3(0f, y, 0f), new Vector3(w, 0.05f, d), woodDark, t);
                float x = -w / 2f + 0.1f;
                while (x < w / 2f - 0.15f)
                {
                    float bw = 0.05f + (float)rnd.NextDouble() * 0.06f;
                    float bh = 0.3f + (float)rnd.NextDouble() * 0.2f;
                    if (rnd.NextDouble() < 0.08) { x += 0.15f; continue; }
                    Box("Book", new Vector3(x + bw / 2f, y + 0.025f + bh / 2f, -0.02f), new Vector3(bw, bh, 0.3f), bookMats[rnd.Next(bookMats.Length)], t);
                    x += bw + 0.005f;
                }
            }
            Vector3 pa = t.TransformPoint(new Vector3(-w / 2f - 0.1f, 0f, -0.35f));
            Vector3 pb = t.TransformPoint(new Vector3(w / 2f + 0.1f, 0f, 0.35f));
            blockers[FloorOf(c.y)].Add(Rect.MinMaxRect(Mathf.Min(pa.x, pb.x), Mathf.Min(pa.z, pb.z), Mathf.Max(pa.x, pb.x), Mathf.Max(pa.z, pb.z)));
        }

        /// <summary>ソファ／肘掛け椅子。yaw は座った人が向く方向。</summary>
        void Sofa(string label, string area, Vector3 c, float yaw, float width, int seatsCount, Material fabric)
        {
            var t = new GameObject(label).transform;
            t.SetParent(root, false);
            t.localPosition = c;
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            float d = 0.9f;
            // 3人掛け（幅2.4m）は本番の家具（家具_v1）があればそれを置く
            bool model = seatsCount == 3 && Mathf.Abs(width - 2.4f) < 0.31f && PlaceFurn("Furn_Sofa3", t, Vector3.zero, 0f, false, new Vector3(width / 2.4f, 1f, 1f)) != null;
            if (!model && seatsCount == 1 && Mathf.Abs(width - 1.0f) < 0.01f) model = PlaceFurn("Furn_Armchair", t, Vector3.zero, 0f) != null; // 肘掛け椅子
            if (!model)
            {
            Box("Sofa_Base", new Vector3(0f, 0.2f, 0f), new Vector3(width, 0.4f, d), woodDark, t);
            Box("Sofa_Cushion", new Vector3(0f, 0.43f, 0.03f), new Vector3(width - 0.3f, 0.12f, d - 0.15f), fabric, t);
            Box("Sofa_Back", new Vector3(0f, 0.65f, -d / 2f + 0.12f), new Vector3(width, 0.75f, 0.24f), fabric, t);
            Box("Sofa_ArmL", new Vector3(-width / 2f + 0.1f, 0.5f, 0f), new Vector3(0.2f, 0.4f, d), fabric, t);
            Box("Sofa_ArmR", new Vector3(width / 2f - 0.1f, 0.5f, 0f), new Vector3(0.2f, 0.4f, d), fabric, t);
            }
            for (int i = 0; i < seatsCount; i++)
            {
                float x = seatsCount == 1 ? 0f : -width / 3f + i * (width / 3f);
                AddSeat(t, label, area, new Vector3(x, 0f, 0.05f), new Vector3(x, 0f, d / 2f + 0.4f));
            }
            AddFootprint(t, width, d, c.y);
        }

        void Chair(string label, string area, Vector3 c, float yaw)
        {
            var t = new GameObject("椅子").transform;
            t.SetParent(root, false);
            t.localPosition = c;
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            if (PlaceFurn("Furn_Chair", t, Vector3.zero, 0f) == null)
            {
            Box("Chair_Seat", new Vector3(0f, 0.45f, 0f), new Vector3(0.48f, 0.06f, 0.48f), woodMid, t);
            Box("Chair_Back", new Vector3(0f, 0.75f, -0.22f), new Vector3(0.48f, 0.6f, 0.05f), woodMid, t);
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0) ? -0.2f : 0.2f, z = (i < 2) ? -0.2f : 0.2f;
                Box("Chair_Leg", new Vector3(x, 0.22f, z), new Vector3(0.05f, 0.44f, 0.05f), woodDark, t);
            }
            }
            AddSeat(t, label, area, Vector3.zero, new Vector3(0f, 0f, -0.7f));
            AddFootprint(t, 0.5f, 0.5f, c.y);
        }

        void Bench(Vector3 c, float yaw)
        {
            var t = new GameObject("窓辺のベンチ").transform;
            t.SetParent(root, false);
            t.localPosition = c;
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            if (PlaceFurn("Furn_Bench", t, Vector3.zero, 0f) == null)
            {
            Box("Bench_Base", new Vector3(0f, 0.22f, 0f), new Vector3(1.5f, 0.44f, 0.5f), woodDark, t);
            Box("Bench_Cushion", new Vector3(0f, 0.47f, 0f), new Vector3(1.4f, 0.08f, 0.45f), fabricGreen, t);
            }
            string area = c.z < FrontOut ? "前庭" : "談話室";
            AddSeat(t, "窓辺のベンチ", area, new Vector3(-0.35f, 0f, 0f), new Vector3(-0.35f, 0f, 0.7f));
            AddSeat(t, "窓辺のベンチ", area, new Vector3(0.35f, 0f, 0f), new Vector3(0.35f, 0f, 0.7f));
            AddFootprint(t, 1.5f, 0.5f, c.y);
        }

        void AddSeat(Transform t, string label, string area, Vector3 localPos, Vector3 localApproach)
        {
            Vector3 p = t.TransformPoint(localPos);
            Vector3 a = t.TransformPoint(localApproach);
            float y = t.position.y;
            seats.Add(new Seat
            {
                label = label,
                area = area,
                position = new Vector3(p.x, y, p.z),
                forward = t.forward,
                approach = new Vector3(a.x, y, a.z)
            });
        }

        void AddFootprint(Transform t, float w, float d, float floorY)
        {
            Vector3 a = t.TransformPoint(new Vector3(-w / 2f, 0f, -d / 2f));
            Vector3 b = t.TransformPoint(new Vector3(w / 2f, 0f, d / 2f));
            var r = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.z, b.z), Mathf.Max(a.x, b.x), Mathf.Max(a.z, b.z));
            blockers[FloorOf(floorY)].Add(r);
        }

        // ================= 歩ける場所 =================
        static Rect In(float x0, float z0, float x1, float z1, float m = 0.25f)
        {
            return Rect.MinMaxRect(Mathf.Min(x0, x1) + m, Mathf.Min(z0, z1) + m, Mathf.Max(x0, x1) - m, Mathf.Max(z0, z1) - m);
        }

        /// <summary>壁の扉の通り道（x 方向の壁なら alongX）。</summary>
        static Rect DoorNav(float x, float z, bool alongX, float w = 1.0f)
        {
            return alongX ? new Rect(x - w / 2f + 0.05f, z - 0.45f, w - 0.1f, 0.9f) : new Rect(x - 0.45f, z - w / 2f + 0.05f, 0.9f, w - 0.1f);
        }

        void BuildNav()
        {
            float inflate = 0.1f; // 家具のまわりのすき間（小さいほど通路が広い）

            // ---- 1階 ----
            var n0 = navs[0] = new GridNav(new Vector2(-36f, -131f), new Vector2(78f, 38f), 0.25f, 0f); // 学校の本館まで（1.5倍にした本館と正門への道）と、東の3つの寮（2026-10-31）
            n0.Open(In(-WingIn, FrontOut, WingIn, BackZ));                       // 談話室
            n0.Open(Rect.MinMaxRect(-WingCorr + 0.25f, FrontZ + 0.25f, WingCorr - 0.25f, BackZ - 0.25f)); // 廊下（談話室とつながる）
            for (int s = -1; s <= 1; s += 2)
                for (int i = 0; i < 5; i++)
                {
                    float cz = FrontZ + RoomW * (i + 0.5f);
                    n0.Open(In(s * WingCorr, cz - RoomW / 2f, s * WingOut, cz + RoomW / 2f));
                    n0.Open(DoorNav(s * WingCorr, cz, false, 1.2f));
                }
            n0.Open(In(-2f, BackZ, KitchenX0, BackOut));   // 食堂
            n0.Open(In(-6f, BackZ, -2f, BackOut));          // 脱衣所
            n0.Open(In(-WingOut, BackZ, -6f, BackOut));     // 浴場
            n0.Open(DoorNav(-0.9f, BackZ, true, 1.2f));     // 食堂の扉
            n0.Open(DoorNav(-2.75f, BackZ, true, 0.9f));    // 脱衣所の扉
            n0.Open(DoorNav(-6f, 13.5f, false, 0.9f));      // 浴場の扉
            n0.Open(In(WingIn, FrontOut, WingOut, FrontZ)); // 先生の部屋（もとの寮母さんの部屋）
            n0.Open(In(-WingOut, FrontOut, -WingIn, FrontZ)); // 倉庫
            n0.Open(DoorNav(9.8f, FrontZ, true));
            n0.Open(DoorNav(-9.8f, FrontZ, true));
            n0.Open(Rect.MinMaxRect(-0.75f, FrontOut - 0.6f, 0.75f, FrontOut + 0.4f)); // 入り口
            n0.Open(Rect.MinMaxRect(-11.25f, -24.25f, 9.75f, FrontOut - 0.45f));         // 前庭（西のはしは学校の本館の裏口）
            OpenPlacesNav();
            OpenSchoolNav(0);
            OpenMobDormNav(n0);
            foreach (var r in blockers[0]) n0.Block(Rect.MinMaxRect(r.xMin - inflate, r.yMin - inflate, r.xMax + inflate, r.yMax + inflate));

            // ---- 2階・3階 ----
            for (int f = 1; f <= 2; f++)
            {
                var n = navs[f] = new GridNav(new Vector2(-27f, -131f), new Vector2(44.5f, 18.5f), 0.25f, FloorY(f)); // 学校の本館まで
                n.Open(Rect.MinMaxRect(WingIn + 0.25f, FrontZ + 0.25f, WingCorr - 0.25f, BackZ - 0.25f));   // 北の廊下
                n.Open(Rect.MinMaxRect(-WingCorr + 0.25f, FrontZ + 0.25f, -WingIn - 0.25f, BackZ - 0.25f)); // 南の廊下
                n.Open(Rect.MinMaxRect(-WingCorr + 0.25f, FrontZ + 0.25f, WingCorr - 0.25f, VoidZ0 - 0.25f)); // 手前の通路
                n.Open(Rect.MinMaxRect(-WingCorr + 0.25f, VoidZ1 + 0.25f, WingCorr - 0.25f, BandZ0 - 0.2f));  // 奥の通路
                n.Open(Rect.MinMaxRect(StairB0 + 0.05f, BandZ0 - 0.25f, StairA0 - 0.05f, BandZ1 - 0.25f)); // 真ん中の踊り場
                if (f == 1)
                {
                    n.Open(Rect.MinMaxRect(StairA1 + 0.05f, BandZ0 - 0.25f, WingCorr - 0.25f, BandZ1 - 0.25f)); // 階段の帯（東。1階からの上り口）
                    n.Open(Rect.MinMaxRect(PassX0 + 0.25f, -24.25f, PassX1 - 0.25f, FrontZ + 0.3f));          // 入口の通路と渡り廊下
                    n.Open(In(-WingIn, FrontOut, PassX0, FrontZ));                                              // 篤の部屋
                    n.Open(DoorNav(-1.0f, FrontZ, true));
                    n.Open(In(PassX1, FrontOut, WingOut, FrontZ));                                              // 小さな談話室（北）
                    n.Open(DoorNav(9.8f, FrontZ, true));
                    n.Open(In(-WingOut, FrontOut, -WingIn, FrontZ));                                            // 小さな談話室（南）
                    n.Open(DoorNav(-9.8f, FrontZ, true));
                    n.Open(In(-6f, BackZ, WingOut, BackOut));                                                   // 娯楽室
                    n.Open(DoorNav(9.8f, BackZ, true));
                    n.Open(In(-WingOut, BackZ, -6f, BackOut));                                                  // トイレ
                    n.Open(DoorNav(-9.8f, BackZ, true));
                }
                else
                {
                    n.Open(Rect.MinMaxRect(-WingCorr + 0.25f, BandZ0 - 0.25f, StairB1 - 0.05f, BandZ1 - 0.25f)); // 階段の帯（西。2階からの上り口）
                    n.Open(Rect.MinMaxRect(StairA1 + 0.05f, BandZ0 - 0.25f, WingCorr - 0.25f, BandZ1 - 0.25f));  // 階段の帯（東）
                    n.Open(In(-WingIn, FrontOut, WingOut, FrontZ));                                           // トレーニング部屋
                    n.Open(DoorNav(0f, FrontZ, true));
                    n.Open(DoorNav(9.8f, FrontZ, true));
                    n.Open(In(-WingOut, FrontOut, -WingIn, FrontZ));                                          // シャワールーム
                    n.Open(DoorNav(-9.8f, FrontZ, true));
                    n.Open(In(0f, BackZ, WingOut, BackOut));                                                  // 勉強部屋
                    n.Open(DoorNav(9.8f, BackZ, true));
                }
                // 個室（2026-10-30：2・3階も住む部屋に）
                for (int sd = -1; sd <= 1; sd += 2)
                    for (int i = 0; i < 5; i++)
                    {
                        float rz = FrontZ + RoomW * (i + 0.5f);
                        n.Open(In(sd * WingCorr, rz - RoomW / 2f, sd * WingOut, rz + RoomW / 2f));
                        n.Open(DoorNav(sd * WingCorr, rz, false, 1.2f));
                    }
                if (f == 2) OpenPlacesNav2F();
                OpenSchoolNav(f);
                foreach (var r in blockers[f]) n.Block(Rect.MinMaxRect(r.xMin - inflate, r.yMin - inflate, r.xMax + inflate, r.yMax + inflate));
            }

            // ---- 屋上 ----
            var n3 = navs[3] = new GridNav(new Vector2(-17.5f, -18.5f), new Vector2(17.5f, 18.5f), 0.25f, FloorY(3));
            n3.Open(In(-WingOut, FrontOut, WingOut, BackOut, 0.4f));
            n3.Block(Rect.MinMaxRect(StairA0 - 0.1f, BandZ0 - 0.1f, StairA1 - 0.05f, BandZ1 + 0.1f)); // 階段の穴
            foreach (var r in blockers[3]) n3.Block(Rect.MinMaxRect(r.xMin - inflate, r.yMin - inflate, r.xMax + inflate, r.yMax + inflate));
        }

        // ================= 壁の表示 =================
        // 床の高さごとの床の部品（下から見上げたときに透かす）
        List<KeyValuePair<float, List<Renderer>>> floorGroups;

        void CollectFloors()
        {
            var byY = new Dictionary<float, List<Renderer>>();
            foreach (var kv in slabRenderers)
            {
                if (kv.Value == null) continue;
                List<Renderer> list;
                if (!byY.TryGetValue(kv.Key, out list)) { list = new List<Renderer>(); byY[kv.Key] = list; }
                list.Add(kv.Value);
            }
            List<Renderer> g0;
            if (!byY.TryGetValue(0.02f, out g0)) { g0 = new List<Renderer>(); byY[0.02f] = g0; }
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                if (System.Array.IndexOf(PlaceFloor0Names, r.gameObject.name) >= 0) g0.Add(r);
            floorGroups = new List<KeyValuePair<float, List<Renderer>>>();
            foreach (var kv in byY) floorGroups.Add(new KeyValuePair<float, List<Renderer>>(kv.Key, kv.Value));
        }

        void UpdateCutaway()
        {
            if (mainCamera == null) return;
            Vector3 cam = mainCamera.transform.position;
            var rig = mainCamera.GetComponent<CameraRig>();
            Vector3 focus = rig != null ? rig.pivot : cam + mainCamera.transform.forward * 10f;
            Vector2 p1 = new Vector2(cam.x, cam.z), p2 = new Vector2(focus.x, focus.z);
            foreach (var w in walls)
            {
                bool hide;
                if (wallMode == WallMode.All) hide = false;
                else if (wallMode == WallMode.Low) hide = true;
                else hide = SegmentsCross(p1, p2, w.a, w.b, 0.8f);
                var mode = hide ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
                foreach (var r in w.upper) if (r != null && r.shadowCastingMode != mode) r.shadowCastingMode = mode;
            }
            // 床：カメラが床より下にあるとき（下から見上げるとき）は透けて見えるようにする
            if (floorGroups == null) CollectFloors();
            foreach (var fg in floorGroups)
            {
                var mode = cam.y < fg.Key ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
                foreach (var r in fg.Value) if (r != null && r.shadowCastingMode != mode) r.shadowCastingMode = mode;
            }
            bool hideCeiling = cam.y > RoomH - 0.4f || wallMode == WallMode.Low;
            foreach (var r in ceilingRenderers)
            {
                var mode = hideCeiling ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
                if (r != null && r.shadowCastingMode != mode) r.shadowCastingMode = mode;
            }
        }

        // ================= 見る階（上の階を隠して、下の階を見やすくする） =================
        /// <summary>今見ている階（0＝1階 … 3＝屋上。屋上のときは全部の階が見える）。カメラの注目点の高さで決まる。</summary>
        public int ViewFloor { get; private set; } = -1;
        List<Renderer>[] levelRenderers;

        /// <summary>寮の部品を、どの階の物かで分けておく（組み立てのあと1回）。</summary>
        void CollectLevels()
        {
            levelRenderers = new[] { new List<Renderer>(), new List<Renderer>(), new List<Renderer>(), new List<Renderer>() };
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r.GetComponentInParent<SimCharacter>() != null) continue;
                levelRenderers[LevelOf(r)].Add(r);
            }
        }

        static int LevelOf(Renderer r)
        {
            string n = r.gameObject.name;
            if (n == "Chandelier_Ring" || n == "Candle" || n == "Pillar" || n == "Fireplace_Chimney") return 0; // 吹き抜けの高い所にある1階の物
            if (r.transform.position.z > BackOut + 0.3f) return 0; // 寮の奥（中庭・温室・工房）は地面の物
            float bottom = r.transform.position.y - Mathf.Abs(r.transform.lossyScale.y) * 0.5f;
            for (var tp = r.transform; tp != null; tp = tp.parent)
            {
                if (tp.name == "Furn_Chandelier") return 0;                     // 吹き抜けに吊るす本番の家具
                if (tp.name.StartsWith("Furn_")) { bottom = tp.position.y; break; } // 本番の家具・部品は原点（床）の高さで階を決める
            }
            return Mathf.Clamp(Mathf.FloorToInt((bottom + 0.2f) / FloorH), 0, 3);
        }

        /// <summary>カメラの注目点の高さで見る階を決め、その階より上の部品とキャラを隠す。</summary>
        void UpdateFloorView()
        {
            if (root == null) return;
            if (levelRenderers == null) CollectLevels();
            int vf = 3;
            if (mainCamera != null)
            {
                var rig = mainCamera.GetComponent<CameraRig>();
                if (rig != null) vf = Mathf.Clamp(FloorOf(rig.pivot.y - 0.5f), 0, 3);
            }
            if (vf != ViewFloor)
            {
                ViewFloor = vf;
                for (int lv = 0; lv < levelRenderers.Length; lv++)
                    foreach (var r in levelRenderers[lv]) if (r != null) r.enabled = lv <= vf;
            }
            foreach (var c in characters) if (c != null) c.SetFloorHidden(!FloorVisible(c.transform.position.y));
        }

        /// <summary>その高さの物が今見えている階か。</summary>
        public bool FloorVisible(float y) { return ViewFloor < 0 || FloorOf(y + 0.3f) <= ViewFloor; }

        /// <summary>カメラ→注目点の線が、壁（両端を margin だけ延ばす）と交わるか。</summary>
        static bool SegmentsCross(Vector2 p1, Vector2 p2, Vector2 a, Vector2 b, float margin)
        {
            Vector2 d = (b - a).normalized;
            a -= d * margin;
            b += d * margin;
            Vector2 r = p2 - p1, s = b - a;
            float denom = r.x * s.y - r.y * s.x;
            if (Mathf.Abs(denom) < 1e-6f) return false;
            Vector2 qp = a - p1;
            float t = (qp.x * s.y - qp.y * s.x) / denom;
            float u = (qp.x * r.y - qp.y * r.x) / denom;
            return t >= 0f && t <= 1f && u >= 0f && u <= 1f;
        }
    }

    /// <summary>火やランプのゆらぎ。scale は時刻による明るさ。</summary>
    public class Flicker : MonoBehaviour
    {
        public float amount = 0.15f;
        public float scale = 1f;
        public bool isFire;
        Light lightRef;
        float baseIntensity;
        float seed;

        void Start()
        {
            lightRef = GetComponent<Light>();
            if (lightRef != null) baseIntensity = lightRef.intensity;
            seed = Random.value * 100f;
        }

        void Update()
        {
            if (lightRef == null) return;
            float n = Mathf.PerlinNoise(seed, Time.time * 4f);
            lightRef.intensity = baseIntensity * scale * (1f - amount + 2f * amount * n);
        }
    }

    /// <summary>炎の形のゆらぎ。</summary>
    public class FlameWobble : MonoBehaviour
    {
        public float seed;
        Vector3 baseScale;
        Vector3 basePos;

        void Start()
        {
            baseScale = transform.localScale;
            basePos = transform.localPosition;
        }

        void Update()
        {
            float n = Mathf.PerlinNoise(seed, Time.time * 5f);
            float h = baseScale.y * (0.7f + 0.6f * n);
            transform.localScale = new Vector3(baseScale.x, h, baseScale.z);
            transform.localPosition = new Vector3(basePos.x, 0.32f + h / 2f, basePos.z);
        }
    }
}
