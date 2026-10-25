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
        public Vector3 bedEdge = new Vector3(0f, 0f, 1f); // ベッドの縁から部屋の中を向く向き（並んで座るとき）
        public float bedSpread = 1f;                      // 並んで座るときの間かく（大きいベッドほど広い）
        public float bedEdgeDist = 0.15f;                 // ベッドの真ん中から、座る縁までの距離
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
    /// ・階段は吹き抜けの中の奥（z 7.2〜9）：1階→2階（x 0.2〜4）、踊り場（x 4〜5）、2階→3階（x 5〜8.6）、3階→屋上（x 4〜0.2）
    /// ・奥（z 9〜18）：1階＝台所・食堂・脱衣所・浴場／2階＝娯楽室・トイレ／3階＝勉強部屋・図書室
    /// ・手前（z −18〜−9）：1階＝寮母さんの部屋・談話室の入り口・倉庫／2階＝小さな談話室・入口の通路（渡り廊下へ）・篤の部屋・小さな談話室／3階＝トレーニング部屋・シャワールーム
    /// ・屋上に天文台。寮の手前（−z）に小さな前庭、2階の入口から学校への渡り廊下
    /// </summary>
    public partial class LoungeWorld
    {
        public const float HalfW = 8.6f;   // 談話室（真ん中）の幅の半分
        public const float RoomH = 9f;     // 吹き抜けの高さ
        public const float FloorH = 3f;    // 1階分の高さ
        public static float FloorY(int floor) { return floor * FloorH; }

        // 棟：真ん中の端 x 8.6 → 廊下 → x 11 → 個室（奥行き6m）→ x 17（外壁）
        const float WingIn = 8.6f, WingCorr = 11f, WingOut = 17f, RoomW = 3.6f, RoomDepth = 6f;
        // 奥と手前の帯
        const float BackZ = 9f, BackOut = 18f, FrontZ = -9f, FrontOut = -18f;
        // 2・3階の吹き抜け（z −7〜5.2）。手前の通路 z −9〜−7、奥の通路 z 5.2〜7.2、階段の帯 z 7.2〜9
        const float VoidZ0 = -7f, VoidZ1 = 5.2f, BandZ0 = 7.2f, BandZ1 = 9f, BandZ = 8.1f;
        // 階段（x）：1階→2階 0.2〜4、踊り場 4〜5、2階→3階 5〜8.6、3階→屋上 4〜0.2
        const float StairA0 = 0.2f, StairA1 = 4f, Land1 = 5f, StairB1 = 8.6f;
        // 2階の入口の通路（渡り廊下へ）
        const float PassX0 = 6f, PassX1 = 8.6f;
        const float Stub = 1.0f;
        const float WallT = 0.3f;

        // 寮の中の目印（行事の集まる所など）
        public static readonly Vector3 LoungeCenter = new Vector3(-5.5f, 0f, 3.5f);   // ソファのまわり
        public static readonly Vector3 BirthdaySpot = new Vector3(-5.5f, 0f, 6.0f);
        public static readonly Vector3 FireplaceSpot = new Vector3(-5.5f, 0f, 7.0f);  // 暖炉の前
        public static readonly Vector3 DiningCenter = new Vector3(2.8f, 0f, 13.5f);   // 食堂
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
            for (int f = 1; f <= 2; f++) { BuildWing(-1, f); BuildWing(1, f); } // 2・3階は空き部屋（rooms[11〜30]）
            BuildBackRooms();
            BuildFrontRooms();
            BuildOuterWalls();
            BuildFrontYard();
            BuildBridge();
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
            return ProtoArt.Mat(new Color(1f, 0.97f, 0.92f), ProtoArt.StoneWall, new Vector2(Mathf.Max(0.5f, len / 3f), Mathf.Max(0.5f, h / 2f)), 0.1f);
        }

        Material PanelFor(float len)
        {
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
            Vector3 size = alongX ? new Vector3(w, h, 0.08f) : new Vector3(0.08f, h, w);
            Box("Door_Closed", new Vector3(c.x, c.y + h / 2f, c.z), size, woodDark);
            Box("Door_Knob", new Vector3(c.x + (alongX ? w * 0.35f : 0f), c.y + 1.0f, c.z + (alongX ? 0f : w * 0.35f)), new Vector3(0.1f, 0.1f, 0.1f), ringMat);
        }

        /// <summary>縦長のアーチ窓。inward は部屋の内側を向く方向。</summary>
        void Window(WallSeg w, Vector3 basePos, Vector3 inward, float sill = 1.2f, float width = 1.1f, float height = 2.2f)
        {
            var mat = ProtoArt.Glow(new Color(0.45f, 0.6f, 0.9f), 0.9f);
            windowMats.Add(mat);
            float yaw = Quaternion.LookRotation(inward).eulerAngles.y;
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
            w.upper.Add(Box("Sconce_Bracket", pos - Vector3.up * 0.15f, new Vector3(0.12f, 0.3f, 0.2f), iron, null, yaw).GetComponent<Renderer>());
            var flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flame.name = "Sconce_Light";
            flame.transform.SetParent(root, false);
            flame.transform.position = pos + inward * 0.12f + Vector3.up * 0.08f;
            flame.transform.localScale = Vector3.one * 0.16f;
            flame.GetComponent<Renderer>().sharedMaterial = glowLamp;
            Destroy(flame.GetComponent<Collider>());
            w.upper.Add(flame.GetComponent<Renderer>());
            Lamp(pos + inward * 0.4f, intensity, 7f);
        }

        void Chandelier(Vector3 pos, float intensity, float range)
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
            Box("Ground", new Vector3(0f, -0.12f, 5f), new Vector3(80f, 0.1f, 90f), groundMat);
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
            Slab("Floor_Dining", 0, Rect.MinMaxRect(-2f, BackZ, WingIn, BackOut), woodFloor);
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
                    Slab("Floor_Band", f, Rect.MinMaxRect(-WingIn, BandZ0, StairA0, BandZ1), woodFloor);
                    Slab("Floor_Landing", f, Rect.MinMaxRect(StairA1, BandZ0, Land1, BandZ1), woodDark);
                }
                else Slab("Floor_Band", f, Rect.MinMaxRect(-WingIn, BandZ0, Land1, BandZ1), woodFloor);
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
            Box("RugBorder", new Vector3(-5.5f, 0.008f, 3.4f), new Vector3(5.9f, 0.016f, 4.7f), fabricBrown);
            Box("Rug", new Vector3(-5.5f, 0.012f, 3.4f), new Vector3(5.6f, 0.02f, 4.4f), rugMat);

            // 柱（吹き抜けの四隅と、廊下との境）
            float[] pz = { VoidZ0, -1f, VoidZ1, FrontZ };
            for (int s = -1; s <= 1; s += 2)
                foreach (float z in pz)
                {
                    float top = z == FrontZ ? FloorH : RoomH;
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
            Box("Hearth", new Vector3(fx, 0.15f, fz), new Vector3(3.4f, 0.3f, 1.1f), mortarDark);
            Box("Fireplace_PillarL", new Vector3(fx - 1.4f, 1.45f, fz + 0.2f), new Vector3(0.6f, 2.6f, 0.7f), mortarDark);
            Box("Fireplace_PillarR", new Vector3(fx + 1.4f, 1.45f, fz + 0.2f), new Vector3(0.6f, 2.6f, 0.7f), mortarDark);
            Box("Fireplace_Mantel", new Vector3(fx, 2.85f, fz + 0.1f), new Vector3(3.8f, 0.3f, 0.95f), woodDark);
            ceilingRenderers.Add(Box("Fireplace_Chimney", new Vector3(fx, 4.0f, fz + 0.3f), new Vector3(2.9f, 2.2f, 0.5f), StoneFor(2.9f, 2.2f)).GetComponent<Renderer>());
            Box("Fireplace_Back", new Vector3(fx, 1.3f, fz + 0.5f), new Vector3(2.3f, 2.3f, 0.1f), iron);
            Cyl("Log1", new Vector3(fx - 0.2f, 0.42f, fz + 0.1f), new Vector3(0.18f, 0.6f, 0.18f), Quaternion.Euler(0f, 0f, 90f), woodDark);
            Cyl("Log2", new Vector3(fx + 0.25f, 0.42f, fz + 0.15f), new Vector3(0.18f, 0.55f, 0.18f), Quaternion.Euler(0f, 30f, 90f), woodDark);
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
            Box("LowTable", new Vector3(lc.x, 0.22f, lc.z), new Vector3(1.4f, 0.08f, 0.8f), woodMid);
            for (int i = 0; i < 4; i++)
            {
                float x = lc.x + ((i % 2 == 0) ? -0.6f : 0.6f), z = lc.z + ((i < 2) ? -0.3f : 0.3f);
                Box("LowTable_Leg", new Vector3(x, 0.1f, z), new Vector3(0.08f, 0.2f, 0.08f), woodDark);
            }
            blockers[0].Add(new Rect(lc.x - 0.75f, lc.z - 0.45f, 1.5f, 0.9f));
            Sofa("ソファ", "談話室", new Vector3(lc.x, 0f, lc.z - 1.5f), 0f, 2.4f, 3, fabricRed);
            Sofa("ソファ", "談話室", new Vector3(lc.x - 1.95f, 0f, lc.z), 90f, 2.4f, 3, fabricRed);
            Sofa("ソファ", "談話室", new Vector3(lc.x + 1.95f, 0f, lc.z), -90f, 2.4f, 3, fabricRed);

            // --- 大きなテーブル（吹き抜けの右手前） ---
            Vector3 bt = new Vector3(3.0f, 0f, -2.0f);
            Box("BigTable_Top", new Vector3(bt.x, 0.75f, bt.z), new Vector3(2.4f, 0.08f, 1.0f), woodMid);
            for (int i = 0; i < 4; i++)
            {
                float x = bt.x + ((i % 2 == 0) ? -1.1f : 1.1f), z = bt.z + ((i < 2) ? -0.4f : 0.4f);
                Box("BigTable_Leg", new Vector3(x, 0.37f, z), new Vector3(0.1f, 0.74f, 0.1f), woodDark);
            }
            Box("Candle", new Vector3(bt.x, 0.9f, bt.z), new Vector3(0.06f, 0.22f, 0.06f), glowLamp);
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
            Cyl("ReadingTable", new Vector3(-5.5f, 0.5f, FrontOut + 1.0f), new Vector3(0.5f, 0.02f, 0.5f), Quaternion.identity, woodMid);
            Cyl("ReadingTable_Leg", new Vector3(-5.5f, 0.25f, FrontOut + 1.0f), new Vector3(0.08f, 0.25f, 0.08f), Quaternion.identity, woodDark);
            blockers[0].Add(new Rect(-5.75f, FrontOut + 0.75f, 0.5f, 0.5f));
            Bookshelf(new Vector3(WingIn - 0.3f, 0f, -12.5f), 2.6f, 90f, 2.6f);
            Bookshelf(new Vector3(-WingIn + 0.3f, 0f, -12.5f), 2.6f, -90f, 2.6f);
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
            Box("SnackTrolley", new Vector3(sx, 0.4f, sz), new Vector3(0.8f, 0.8f, 0.5f), woodMid);
            for (int i = 0; i < 3; i++) Box("Snack", new Vector3(sx - 0.25f + i * 0.25f, 0.86f, sz), new Vector3(0.18f, 0.1f, 0.18f), foodMat);
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
            int steps = 15;
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
            Flight("StairA", StairA0, StairA1, 0f, FloorY(1));          // 1階 → 2階
            Flight("StairB", Land1, StairB1, FloorY(1), FloorY(2));      // 2階 → 3階（3階の北の廊下に出る）
            Flight("StairC", StairA1, StairA0, FloorY(2), FloorY(3));    // 3階 → 屋上
            blockers[0].Add(Rect.MinMaxRect(StairA0 - 0.1f, BandZ0 - 0.1f, StairA1, BandZ1)); // 1階→2階の段の下は通れない
            blockers[2].Add(Rect.MinMaxRect(StairA0 - 0.1f, BandZ0 - 0.1f, StairA1, BandZ1)); // 3階→屋上の段
            Vector3 lane = new Vector3(0f, 0f, 0.4f); // 上りは壁側、下りは吹き抜け側
            stairs.Add(new Stair
            {
                lowerFloor = 0,
                bottomApproach = new Vector3(StairA0 - 0.8f, 0f, BandZ),
                bottom = new Vector3(StairA0, 0f, BandZ),
                top = new Vector3(StairA1, FloorY(1), BandZ),
                topApproach = new Vector3(4.5f, FloorY(1), 6.3f),
                lane = lane
            });
            stairs.Add(new Stair
            {
                lowerFloor = 1,
                bottomApproach = new Vector3(4.5f, FloorY(1), 6.3f),
                bottom = new Vector3(Land1, FloorY(1), BandZ),
                top = new Vector3(StairB1, FloorY(2), BandZ),
                topApproach = new Vector3(9.6f, FloorY(2), BandZ),
                lane = lane
            });
            stairs.Add(new Stair
            {
                lowerFloor = 2,
                bottomApproach = new Vector3(4.5f, FloorY(2), BandZ),
                bottom = new Vector3(StairA1, FloorY(2), BandZ),
                top = new Vector3(StairA0, FloorY(3), BandZ),
                topApproach = new Vector3(StairA0 - 0.8f, FloorY(3), BandZ),
                lane = -lane
            });
            Label(new Vector3(StairA0 - 0.3f, 1.6f, BandZ - 0.6f), "階段（2階・3階へ）");
            Label(new Vector3(4.5f, FloorY(2) + 1.8f, BandZ - 0.6f), "屋上への階段");
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
                Rail(f, new Vector2(Land1, BandZ0), new Vector2(StairB1, BandZ0)); // 2階→3階の階段の穴
            }
            Rail(1, new Vector2(StairA0, BandZ0), new Vector2(StairA1, BandZ0)); // 1階→2階の階段の穴
            Rail(1, new Vector2(StairA0, BandZ0), new Vector2(StairA0, BandZ1));
            Rail(3, new Vector2(StairA0, BandZ0), new Vector2(StairA1, BandZ0)); // 屋上の階段の穴
            Rail(3, new Vector2(StairA1, BandZ0), new Vector2(StairA1, BandZ1));
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
                var room = new RoomInfo { name = prefix + wingName + (i + 1), floor = floor, vacant = floor > 0 };
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
            // ベッド（頭は外壁側、部屋の手前寄り）
            float bedX0 = X(RoomDepth - 0.1f), bedX1 = X(RoomDepth - 2.1f), bz = cz - 0.95f;
            float bcx = (bedX0 + bedX1) / 2f;
            Box("Bed_Frame", new Vector3(bcx, y + 0.2f, bz), new Vector3(2.0f, 0.4f, 0.85f), woodDark);
            Box("Bed_Mattress", new Vector3(bcx, y + 0.47f, bz), new Vector3(1.9f, 0.14f, 0.8f), fabricWhite);
            Box("Bed_Blanket", new Vector3(bcx - s * 0.3f, y + 0.55f, bz), new Vector3(1.3f, 0.04f, 0.82f), fabricRed);
            Box("Bed_Pillow", new Vector3(X(RoomDepth - 0.4f), y + 0.58f, bz), new Vector3(0.4f, 0.1f, 0.55f), fabricWhite);
            Box("Bed_Head", new Vector3(X(RoomDepth - 0.1f), y + 0.55f, bz), new Vector3(0.08f, 1.1f, 0.85f), woodDark);
            blockers[room.floor].Add(Rect.MinMaxRect(Mathf.Min(bedX0, bedX1), bz - 0.43f, Mathf.Max(bedX0, bedX1), bz + 0.43f));
            room.bed = new Seat
            {
                kind = SeatKind.Bed,
                label = "ベッド",
                area = room.name,
                position = new Vector3(bcx, y, bz),
                forward = new Vector3(s, 0f, 0f),
                approach = new Vector3(X(RoomDepth - 2.5f), y, cz - 0.2f),
                room = room
            };
            seats.Add(room.bed);
            // 来客用：座布団2枚と小さなテーブル、床に敷く布団
            Box("LowTable_Room", new Vector3(X(2.3f), y + 0.15f, cz + 0.65f), new Vector3(0.5f, 0.3f, 0.5f), woodMid);
            blockers[room.floor].Add(Rect.MinMaxRect(Mathf.Min(X(2.05f), X(2.55f)), cz + 0.4f, Mathf.Max(X(2.05f), X(2.55f)), cz + 0.9f));
            for (int k = 0; k < 2; k++)
            {
                float cx = X(k == 0 ? 1.55f : 3.05f);
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
            // 机（外壁側、部屋の奥寄り）
            float deskX = X(RoomDepth - 0.4f), dz = cz + 0.9f;
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
            blockers[room.floor].Add(Rect.MinMaxRect(Mathf.Min(X(RoomDepth - 0.7f), X(RoomDepth)), dz - 0.4f, Mathf.Max(X(RoomDepth - 0.7f), X(RoomDepth)), dz + 0.4f));
            room.desk = new Poi
            {
                label = "机で勉強している",
                lookAt = new Vector3(deskX, y + 0.8f, dz),
                slots = new[] { new Vector3(X(RoomDepth - 1.15f), y, dz) },
                occupants = new SimCharacter[1]
            };
            pois.Add(room.desk);
            // タンス（廊下側の壁ぎわ。扉の横）
            Box("Wardrobe", new Vector3(X(0.35f), y + 0.9f, cz + 1.15f), new Vector3(0.5f, 1.8f, 0.7f), woodDark);
            blockers[room.floor].Add(Rect.MinMaxRect(Mathf.Min(X(0.1f), X(0.6f)), cz + 0.8f, Mathf.Max(X(0.1f), X(0.6f)), cz + 1.5f));
            Lamp(new Vector3(X(RoomDepth - 2.5f), y + 2.3f, cz), 1.5f, 4.5f, false, 0.03f);
        }

        // ================= 篤の部屋（2階・もとの玄関ホールの所） =================
        /// <summary>篤の部屋：2階の手前の真ん中（x −8.6〜6、z −18〜−9）。入口の通路の隣。吹き抜けのまわりの通路から入る（famuru 決定 2026-10-25）。</summary>
        void BuildFRoom()
        {
            int fl = 1;
            float x0 = -WingIn, x1 = PassX0, z0 = FrontOut, z1 = FrontZ, y = FloorY(fl);
            var room = new RoomInfo { name = "篤", floor = fl };
            // ベッド（個室のベッドの1.5倍。手前の外壁ぎわ。縁に何人か並んで座れる。famuru 決定 2026-10-21）
            float bw = 3.1f, bd = 2.1f;                       // 長さ（x）・幅（z）
            float bx0 = x1 - 0.45f, bx1 = bx0 - bw;           // 頭は東（入口の通路）側
            float bz = z0 + 0.45f + bd / 2f;                  // 外壁から 0.3m
            float bcx = (bx0 + bx1) / 2f;
            Box("Bed_Frame", new Vector3(bcx, y + 0.2f, bz), new Vector3(bw, 0.4f, bd), woodDark);
            Box("Bed_Mattress", new Vector3(bcx, y + 0.47f, bz), new Vector3(bw - 0.1f, 0.14f, bd - 0.1f), fabricWhite);
            Box("Bed_Blanket", new Vector3(bcx - 0.45f, y + 0.55f, bz), new Vector3(bw * 0.65f, 0.04f, bd - 0.08f), fabricGreen);
            for (int k = -1; k <= 1; k += 2) Box("Bed_Pillow", new Vector3(bx0 - 0.3f, y + 0.58f, bz + k * 0.5f), new Vector3(0.4f, 0.1f, 0.8f), fabricWhite);
            Box("Bed_Head", new Vector3(bx0 + 0.04f, y + 0.6f, bz), new Vector3(0.08f, 1.2f, bd), woodDark);
            blockers[fl].Add(Rect.MinMaxRect(bx1, bz - bd / 2f, bx0 + 0.1f, bz + bd / 2f));
            room.bed = new Seat
            {
                kind = SeatKind.Bed, label = "ベッド", area = room.name, position = new Vector3(bcx, y, bz),
                forward = new Vector3(1f, 0f, 0f), approach = new Vector3(bcx - 0.4f, y, bz + bd / 2f + 0.5f), room = room
            };
            seats.Add(room.bed);
            room.bedEdge = new Vector3(0f, 0f, 1f); // 縁は部屋の中（奥）側
            room.bedSpread = 1.6f;
            room.bedEdgeDist = bd / 2f - 0.3f;
            // ベッドの縁に座る席（3人）
            for (int k = 0; k < 3; k++)
            {
                float sx = bx1 + 0.55f + k * 0.95f;
                seats.Add(new Seat
                {
                    kind = SeatKind.Sit, label = "篤の部屋のベッド", area = room.name, room = room,
                    position = new Vector3(sx, y, bz + bd / 2f - 0.15f), forward = new Vector3(0f, 0f, 1f),
                    approach = new Vector3(sx, y, bz + bd / 2f + 0.45f)
                });
            }
            // 机（手前の外壁ぎわ・西）
            float dX = -5.5f, dZ = z0 + 0.5f;
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
            blockers[fl].Add(Rect.MinMaxRect(dX - 0.6f, z0, dX + 0.6f, dZ + 0.3f));
            room.desk = new Poi { label = "机で勉強している", lookAt = new Vector3(dX, y + 0.8f, dZ), slots = new[] { new Vector3(dX, y, dZ + 0.75f) }, occupants = new SimCharacter[1] };
            pois.Add(room.desk);
            // 本棚（西の壁ぎわ）
            ShelfRow(new Vector3(x0 + 0.4f, y, -13.5f), -90f, 2.0f, 2.0f, 4, fabricRed, woodMid, 401);
            // ソファ（奥の壁ぎわ。3人掛け。部屋の中を向く）
            Sofa("篤の部屋のソファ", room.name, new Vector3(2.6f, y, z1 - 0.65f), 180f, 2.6f, 3, fabricRed);
            // 低いテーブルと座布団2枚
            float tx = 2.6f, tz = -12.4f;
            Box("LowTable_Room", new Vector3(tx, y + 0.15f, tz), new Vector3(0.9f, 0.3f, 0.6f), woodMid);
            blockers[fl].Add(Rect.MinMaxRect(tx - 0.45f, tz - 0.3f, tx + 0.45f, tz + 0.3f));
            for (int k = 0; k < 2; k++)
            {
                float cx = k == 0 ? tx - 0.85f : tx + 0.85f;
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
            Box("GuestFuton", new Vector3(-5.0f, y + 0.05f, -11.0f), new Vector3(1.7f, 0.1f, 0.75f), fabricWhite);
            room.futon = new Seat
            {
                kind = SeatKind.Bed, floorSeat = true, label = "来客用の布団", area = room.name,
                position = new Vector3(-5.0f, y, -11.0f), forward = new Vector3(1f, 0f, 0f), approach = new Vector3(-5.0f, y, -10.35f), room = room
            };
            seats.Add(room.futon);
            // タンス（東の壁ぎわ・奥）
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
            WallF(0, new Vector2(-WingOut, BackZ), new Vector2(WingOut, BackZ), DoorGap(0, -2.75f - a0, 0.9f), DoorGap(0, 6.5f - a0, 1.2f));
            WallF(1, new Vector2(-WingOut, BackZ), new Vector2(WingOut, BackZ), DoorGap(1, -9.8f - a0), DoorGap(1, -3.5f - a0), DoorGap(1, 9.8f - a0));
            WallF(2, new Vector2(-WingOut, BackZ), new Vector2(WingOut, BackZ), DoorGap(2, -9.8f - a0), DoorGap(2, 9.8f - a0));
            // 仕切り（1階：台所｜食堂｜脱衣所｜浴場。台所の扉は鍵つき＝DormPlaces、脱衣所→浴場の扉 z 13.5）
            WallF(0, new Vector2(WingIn, BackZ), new Vector2(WingIn, BackOut), DoorGap(0, 17.0f - BackZ, 1.0f, 2.4f));
            WallF(0, new Vector2(-2f, BackZ), new Vector2(-2f, BackOut));
            WallF(0, new Vector2(-6f, BackZ), new Vector2(-6f, BackOut), DoorGap(0, 13.5f - BackZ, 0.9f));
            WallF(1, new Vector2(-6f, BackZ), new Vector2(-6f, BackOut));   // 娯楽室｜トイレ
            WallF(2, new Vector2(0f, BackZ), new Vector2(0f, BackOut));     // 勉強部屋｜図書室

            // --- 食堂（x −2〜8.6、z 9〜18） ---
            Label(new Vector3(DiningCenter.x, 2.6f, BackZ + 0.6f), "食堂");
            AddArea("食堂", Rect.MinMaxRect(-2f, BackZ, WingIn, BackOut), 0);
            float cx = WingIn - 0.5f; // 受け取り口（台所の壁ぎわ）
            Box("Counter", new Vector3(cx, 0.5f, 12.0f), new Vector3(0.8f, 1.0f, 3.0f), woodDark);
            Box("Counter_Top", new Vector3(cx, 1.02f, 12.0f), new Vector3(0.9f, 0.05f, 3.1f), woodMid);
            for (int i = 0; i < 3; i++) Box("Food", new Vector3(cx + 0.05f, 1.12f, 11.0f + i * 0.95f), new Vector3(0.35f, 0.12f, 0.35f), foodMat);
            blockers[0].Add(new Rect(cx - 0.45f, 10.45f, 0.9f, 3.1f));
            pois.Add(new Poi
            {
                label = "料理を受け取っている",
                lookAt = new Vector3(cx, 1f, 12.0f),
                slots = new[] { new Vector3(cx - 0.9f, 0f, 11.0f), new Vector3(cx - 0.9f, 0f, 12.0f), new Vector3(cx - 0.9f, 0f, 13.0f) },
                occupants = new SimCharacter[3],
                counter = true
            });
            // 縦長の机2つ（12席。机と机の間は通路）
            DiningTable(new Vector3(0.9f, 0f, DiningCenter.z));
            DiningTable(new Vector3(4.3f, 0f, DiningCenter.z));
            Chandelier(new Vector3(0.9f, 2.6f, DiningCenter.z), 3.5f, 7f);
            Chandelier(new Vector3(4.3f, 2.6f, DiningCenter.z), 3.5f, 7f);

            // --- 脱衣所（x −6〜−2） ---
            Label(new Vector3(-4f, 2.6f, BackZ + 0.6f), "脱衣所");
            AddArea("脱衣所", Rect.MinMaxRect(-6f, BackZ, -2f, BackOut), 0);
            for (int i = 0; i < 5; i++) Box("Locker", new Vector3(-5.5f + i * 0.75f, 0.9f, BackOut - 0.45f), new Vector3(0.65f, 1.8f, 0.4f), woodMid);
            blockers[0].Add(Rect.MinMaxRect(-5.85f, BackOut - 0.7f, -2.15f, BackOut));
            Box("Dressing_Bench", new Vector3(-4f, 0.22f, 14.0f), new Vector3(0.45f, 0.44f, 1.6f), woodDark);
            blockers[0].Add(new Rect(-4.225f, 13.2f, 0.45f, 1.6f));
            Box("Dressing_Basket", new Vector3(-2.5f, 0.15f, 16.2f), new Vector3(0.5f, 0.3f, 0.4f), fabricBrown);
            blockers[0].Add(new Rect(-2.75f, 16.0f, 0.5f, 0.4f));
            Lamp(new Vector3(-4f, 2.6f, 13.5f), 2.0f, 5f);

            // --- 浴場（x −17〜−6。湯船は6人、奥に洗い場） ---
            Label(new Vector3(-11.5f, 2.6f, BackZ + 0.6f), "浴場");
            AddArea("浴場", Rect.MinMaxRect(-WingOut, BackZ, -6f, BackOut), 0);
            float tx0 = -15.6f, tx1 = -10.6f, tz0 = 10.6f, tz1 = 15.6f;
            float rim = 0.3f, rimH = 0.5f;
            Box("Tub_RimL", new Vector3(tx0 + rim / 2f, rimH / 2f, (tz0 + tz1) / 2f), new Vector3(rim, rimH, tz1 - tz0), mortarDark);
            Box("Tub_RimR", new Vector3(tx1 - rim / 2f, rimH / 2f, (tz0 + tz1) / 2f), new Vector3(rim, rimH, tz1 - tz0), mortarDark);
            Box("Tub_RimF", new Vector3((tx0 + tx1) / 2f, rimH / 2f, tz0 + rim / 2f), new Vector3(tx1 - tx0, rimH, rim), mortarDark);
            Box("Tub_RimB", new Vector3((tx0 + tx1) / 2f, rimH / 2f, tz1 - rim / 2f), new Vector3(tx1 - tx0, rimH, rim), mortarDark);
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
                Box("WashStool", new Vector3(x, 0.15f, BackOut - 0.75f), new Vector3(0.35f, 0.3f, 0.35f), woodMid);
                Cyl("WashBucket", new Vector3(x + 0.35f, 0.12f, BackOut - 0.65f), new Vector3(0.3f, 0.12f, 0.3f), Quaternion.identity, woodDark);
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

        void DiningTable(Vector3 c)
        {
            // 奥行き方向（z）に長い机。椅子は左右に3脚ずつ、座る前は外側の通路に立つ
            Box("DiningTable", new Vector3(c.x, 0.75f, c.z), new Vector3(0.9f, 0.08f, 3.3f), woodMid);
            for (int i = 0; i < 4; i++)
            {
                float x = c.x + ((i % 2 == 0) ? -0.35f : 0.35f), z = c.z + ((i < 2) ? -1.5f : 1.5f);
                Box("DiningTable_Leg", new Vector3(x, 0.37f, z), new Vector3(0.1f, 0.74f, 0.1f), woodDark);
            }
            Box("DiningTable_Candle", new Vector3(c.x, 0.9f, c.z), new Vector3(0.06f, 0.22f, 0.06f), glowLamp);
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
            Label(new Vector3(cn, 2.2f, cz), "寮母さんの部屋");
            AddArea("寮母さんの部屋", Rect.MinMaxRect(WingIn, FrontOut, WingOut, FrontZ), 0);
            Lamp(new Vector3(cn, 2.4f, cz), 1.8f, 6f);
            Label(new Vector3(cs, 2.2f, cz), "倉庫");
            AddArea("倉庫", Rect.MinMaxRect(-WingOut, FrontOut, -WingIn, FrontZ), 0);
            for (int i = 0; i < 3; i++)
            {
                Box("Crate", new Vector3(-15.8f + i * 1.1f, 0.4f, FrontOut + 0.7f), new Vector3(0.9f, 0.8f, 0.9f), woodMid);
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
                if (f == 0) gapsB.Add(new Vector4(a0 + 2.3f, a0 + 3.3f, y, y + 2.6f));
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
                blockers[0].Add(new Rect(x - 1.1f, -22.05f, 2.2f, 1.1f));
            }
            Bench(new Vector3(-3.6f, 0f, -23.4f), 0f);
            Label(new Vector3(0f, 1.6f, -22.5f), "前庭");
            AddArea("前庭", Rect.MinMaxRect(-6f, z0, 10f, FrontOut), 0);
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
                Box("Bridge_Pillar", new Vector3(x, y / 2f, z0 + 0.3f), new Vector3(0.35f, y, 0.35f), mortarDark);
                Box("Bridge_Pillar", new Vector3(x, y / 2f, -21.2f), new Vector3(0.35f, y, 0.35f), mortarDark);
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

        void Bookshelf(Vector3 c, float w = 3f, float yaw = 0f, float h = 3.2f)
        {
            var t = new GameObject("Bookshelf").transform;
            t.SetParent(root, false);
            t.localPosition = c;
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            float d = 0.45f;
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
            Box("Sofa_Base", new Vector3(0f, 0.2f, 0f), new Vector3(width, 0.4f, d), woodDark, t);
            Box("Sofa_Cushion", new Vector3(0f, 0.43f, 0.03f), new Vector3(width - 0.3f, 0.12f, d - 0.15f), fabric, t);
            Box("Sofa_Back", new Vector3(0f, 0.65f, -d / 2f + 0.12f), new Vector3(width, 0.75f, 0.24f), fabric, t);
            Box("Sofa_ArmL", new Vector3(-width / 2f + 0.1f, 0.5f, 0f), new Vector3(0.2f, 0.4f, d), fabric, t);
            Box("Sofa_ArmR", new Vector3(width / 2f - 0.1f, 0.5f, 0f), new Vector3(0.2f, 0.4f, d), fabric, t);
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
            Box("Chair_Seat", new Vector3(0f, 0.45f, 0f), new Vector3(0.48f, 0.06f, 0.48f), woodMid, t);
            Box("Chair_Back", new Vector3(0f, 0.75f, -0.22f), new Vector3(0.48f, 0.6f, 0.05f), woodMid, t);
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0) ? -0.2f : 0.2f, z = (i < 2) ? -0.2f : 0.2f;
                Box("Chair_Leg", new Vector3(x, 0.22f, z), new Vector3(0.05f, 0.44f, 0.05f), woodDark, t);
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
            Box("Bench_Base", new Vector3(0f, 0.22f, 0f), new Vector3(1.5f, 0.44f, 0.5f), woodDark, t);
            Box("Bench_Cushion", new Vector3(0f, 0.47f, 0f), new Vector3(1.4f, 0.08f, 0.45f), fabricGreen, t);
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
            var n0 = navs[0] = new GridNav(new Vector2(-22f, -27f), new Vector2(22f, 38f), 0.25f, 0f);
            n0.Open(In(-WingIn, FrontOut, WingIn, BackZ));                       // 談話室
            n0.Open(Rect.MinMaxRect(-WingCorr + 0.25f, FrontZ + 0.25f, WingCorr - 0.25f, BackZ - 0.25f)); // 廊下（談話室とつながる）
            for (int s = -1; s <= 1; s += 2)
                for (int i = 0; i < 5; i++)
                {
                    float cz = FrontZ + RoomW * (i + 0.5f);
                    n0.Open(In(s * WingCorr, cz - RoomW / 2f, s * WingOut, cz + RoomW / 2f));
                    n0.Open(DoorNav(s * WingCorr, cz, false, 1.2f));
                }
            n0.Open(In(-2f, BackZ, WingIn, BackOut));      // 食堂
            n0.Open(In(-6f, BackZ, -2f, BackOut));          // 脱衣所
            n0.Open(In(-WingOut, BackZ, -6f, BackOut));     // 浴場
            n0.Open(DoorNav(6.5f, BackZ, true, 1.2f));      // 食堂の扉
            n0.Open(DoorNav(-2.75f, BackZ, true, 0.9f));    // 脱衣所の扉
            n0.Open(DoorNav(-6f, 13.5f, false, 0.9f));      // 浴場の扉
            n0.Open(In(WingIn, FrontOut, WingOut, FrontZ)); // 寮母さんの部屋
            n0.Open(In(-WingOut, FrontOut, -WingIn, FrontZ)); // 倉庫
            n0.Open(DoorNav(9.8f, FrontZ, true));
            n0.Open(DoorNav(-9.8f, FrontZ, true));
            n0.Open(Rect.MinMaxRect(-0.75f, FrontOut - 0.6f, 0.75f, FrontOut + 0.4f)); // 入り口
            n0.Open(Rect.MinMaxRect(-5.75f, -24.25f, 9.75f, FrontOut - 0.45f));          // 前庭
            OpenPlacesNav();
            foreach (var r in blockers[0]) n0.Block(Rect.MinMaxRect(r.xMin - inflate, r.yMin - inflate, r.xMax + inflate, r.yMax + inflate));

            // ---- 2階・3階 ----
            for (int f = 1; f <= 2; f++)
            {
                var n = navs[f] = new GridNav(new Vector2(-17.5f, -25f), new Vector2(17.5f, 18.5f), 0.25f, FloorY(f));
                n.Open(Rect.MinMaxRect(WingIn + 0.25f, FrontZ + 0.25f, WingCorr - 0.25f, BackZ - 0.25f));   // 北の廊下
                n.Open(Rect.MinMaxRect(-WingCorr + 0.25f, FrontZ + 0.25f, -WingIn - 0.25f, BackZ - 0.25f)); // 南の廊下
                n.Open(Rect.MinMaxRect(-WingCorr + 0.25f, FrontZ + 0.25f, WingCorr - 0.25f, VoidZ0 - 0.25f)); // 手前の通路
                n.Open(Rect.MinMaxRect(-WingCorr + 0.25f, VoidZ1 + 0.25f, WingCorr - 0.25f, BandZ0 - 0.2f));  // 奥の通路
                if (f == 1)
                {
                    n.Open(Rect.MinMaxRect(-WingIn + 0.25f, BandZ0 - 0.25f, StairA0 - 0.25f, BandZ1 - 0.25f)); // 階段の帯（西）
                    n.Open(Rect.MinMaxRect(StairA1 + 0.05f, BandZ0 - 0.25f, Land1 - 0.05f, BandZ1 - 0.25f));  // 踊り場
                    n.Open(Rect.MinMaxRect(PassX0 + 0.25f, -24.25f, PassX1 - 0.25f, FrontZ + 0.3f));          // 入口の通路と渡り廊下
                    n.Open(In(-WingIn, FrontOut, PassX0, FrontZ));                                              // 篤の部屋
                    n.Open(DoorNav(-1.0f, FrontZ, true));
                    n.Open(In(PassX1, FrontOut, WingOut, FrontZ));                                              // 小さな談話室（北）
                    n.Open(DoorNav(9.8f, FrontZ, true));
                    n.Open(In(-WingOut, FrontOut, -WingIn, FrontZ));                                            // 小さな談話室（南）
                    n.Open(DoorNav(-9.8f, FrontZ, true));
                    n.Open(In(-6f, BackZ, WingOut, BackOut));                                                   // 娯楽室
                    n.Open(DoorNav(9.8f, BackZ, true));
                    n.Open(DoorNav(-3.5f, BackZ, true));
                    n.Open(In(-WingOut, BackZ, -6f, BackOut));                                                  // トイレ
                    n.Open(DoorNav(-9.8f, BackZ, true));
                }
                else
                {
                    n.Open(Rect.MinMaxRect(-WingIn + 0.25f, BandZ0 - 0.25f, Land1 - 0.05f, BandZ1 - 0.25f)); // 階段の帯（屋上への段は下でふさぐ）
                    n.Open(In(-WingIn, FrontOut, WingOut, FrontZ));                                           // トレーニング部屋
                    n.Open(DoorNav(0f, FrontZ, true));
                    n.Open(DoorNav(9.8f, FrontZ, true));
                    n.Open(In(-WingOut, FrontOut, -WingIn, FrontZ));                                          // シャワールーム
                    n.Open(DoorNav(-9.8f, FrontZ, true));
                    n.Open(In(0f, BackZ, WingOut, BackOut));                                                  // 勉強部屋
                    n.Open(DoorNav(9.8f, BackZ, true));
                }
                if (f == 2) OpenPlacesNav2F();
                foreach (var r in blockers[f]) n.Block(Rect.MinMaxRect(r.xMin - inflate, r.yMin - inflate, r.xMax + inflate, r.yMax + inflate));
            }

            // ---- 屋上 ----
            var n3 = navs[3] = new GridNav(new Vector2(-17.5f, -18.5f), new Vector2(17.5f, 18.5f), 0.25f, FloorY(3));
            n3.Open(In(-WingOut, FrontOut, WingOut, BackOut, 0.4f));
            n3.Block(Rect.MinMaxRect(StairA0 - 0.05f, BandZ0 - 0.1f, StairA1 + 0.1f, BandZ1 + 0.1f)); // 階段の穴
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
