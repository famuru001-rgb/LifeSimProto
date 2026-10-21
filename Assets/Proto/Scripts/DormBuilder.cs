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
    /// 寮の建物を組み立てる部分。
    /// 大きさは famuru 決定（2026-10-21）：個室 幅3m×奥行き5.4m、2階の廊下 幅2.4m、談話室 14m×15m、食堂 10m×7m、浴場 6m×7m＋脱衣所 3m×7m、
    /// 篤の部屋は個室の倍（もとは物置だった離れ。2階・右の廊下の突き当たりの先）。
    /// 1階：談話室（中央 x-7〜7, z-7.5〜7.5, 天井6m）／手前に 食堂（x-12〜-2）・廊下（-2〜2）・脱衣所（2〜5）・浴場（5〜11）（z-14.5〜-7.5）
    /// 2階（高さ3m）：談話室の左右に廊下と個室（左5部屋・右4部屋と物置）。談話室の左右の階段から上がる。
    /// </summary>
    public partial class LoungeWorld
    {
        public const float HalfW = 7f, HalfD = 7.5f, RoomH = 6f;
        const float FrontZ = -14.5f, BandH = 3.5f, WingTop = 5.8f, Stub = 1.0f;
        // 2階の個室の並び：談話室の壁の外側から 廊下（2.4m）→ 個室（奥行き5.4m）。個室は幅3m で5つ並ぶ（5×3m＝談話室の奥行き15m）
        const float WingInner = HalfW + 0.4f, CorrW = 2.4f, RoomDepth = 5.4f, RoomW = 3f;
        // 談話室の階段：幅1.8m（2026-10-21 に1.2mから広げた）。上りと下りで左右に分かれて通る
        const float StairW = 1.8f, StairX = HalfW - 1.0f;

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
        readonly List<Rect> blockers0 = new List<Rect>();
        readonly List<Rect> blockers1 = new List<Rect>();
        readonly List<Rect> blockers2 = new List<Rect>(); // 屋上（天文台）
        Light sun;

        Transform root;
        Material flagstone, woodDark, woodMid, woodFloor, fabricRed, fabricGreen, fabricBrown, fabricWhite, iron, glowLamp, glowFire, glowEmber, ringMat, rugMat, mortarDark, groundMat, waterMat, foodMat;

        void Build()
        {
            root = new GameObject("Dorm").transform;
            CreateMaterials();
            SetupSun();
            BuildGround();
            BuildLounge();
            BuildFrontBand();
            BuildWing(-1);   // 左：個室5部屋（rooms[0〜4]）
            BuildFRoom();    // 篤の部屋（rooms[5]）
            BuildWing(1);    // 右：個室4部屋（rooms[6〜9]）と物置
            BuildPlaces();   // 新しい場所（DormPlaces.cs）
            BuildNav();
            ApplyPlaces();
            float sx = StairX;
            for (int side = -1; side <= 1; side += 2)
            {
                stairs.Add(new Stair
                {
                    bottomApproach = new Vector3(side * sx, 0f, -2.1f),
                    bottom = new Vector3(side * sx, 0f, -1.5f),
                    top = new Vector3(side * sx, Floor2Y, 2.4f),
                    topApproach = new Vector3(side * sx, Floor2Y, 3.1f),
                    lane = new Vector3(side * 0.45f, 0f, 0f) // 上りは壁側、下りは談話室側
                });
            }
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
            groundMat = ProtoArt.Mat(new Color(0.42f, 0.56f, 0.3f), ProtoArt.Fabric, new Vector2(20f, 20f), 0.05f);
            glowLamp = ProtoArt.Glow(new Color(1f, 0.8f, 0.5f), 2.5f);
            glowFire = ProtoArt.Glow(new Color(1f, 0.55f, 0.15f), 3f);
            glowEmber = ProtoArt.Glow(new Color(1f, 0.3f, 0.05f), 2f);
            ringMat = ProtoArt.Glow(new Color(1f, 0.9f, 0.2f), 1.5f);
            waterMat = ProtoArt.Mat(new Color(0.35f, 0.6f, 0.75f), null, null, 0.9f);
            foodMat = ProtoArt.Glow(new Color(1f, 0.75f, 0.45f), 1.2f);
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

        // ================= 地面 =================
        void BuildGround()
        {
            Box("Ground", new Vector3(-2f, -0.08f, 4f), new Vector3(68f, 0.1f, 66f), groundMat);
            Box("Path_Outside", new Vector3(0f, -0.02f, -17.6f), new Vector3(3f, 0.04f, 6f), flagstone);
            Label(new Vector3(0f, 1.2f, -16.9f), "外（授業へ）");
        }

        // ================= 談話室 =================
        void BuildLounge()
        {
            float t = 0.4f;
            Box("Lounge_Floor", new Vector3(0f, -0.05f, 0f), new Vector3(HalfW * 2f, 0.1f, HalfD * 2f), flagstone);
            Box("Floor_Wood", new Vector3(3.4f, 0.005f, -4.0f), new Vector3(5.0f, 0.02f, 3.8f), woodFloor);
            Box("RugBorder", new Vector3(0f, 0.008f, 1.9f), new Vector3(5.9f, 0.016f, 4.7f), fabricBrown);
            Box("Rug", new Vector3(0f, 0.012f, 1.9f), new Vector3(5.6f, 0.02f, 4.4f), rugMat);

            float wx = HalfW + t / 2f, wz = HalfD + t / 2f;
            // 奥の壁：左は図書室の扉（x -3.9〜-2.9）、右は中庭への裏口（x 2.3〜3.3）。どちらも開くまで鍵がかかっている
            float b0 = -wx - t / 2f;
            var back = Wall(new Vector2(b0, wz), new Vector2(wx + t / 2f, wz), 0f, RoomH, t,
                new Vector4(-3.9f - b0, -2.9f - b0, 0f, 2.6f), new Vector4(2.3f - b0, 3.3f - b0, 0f, 2.6f));
            back.upper.Add(Box("BackDoor_LintelL", new Vector3(-3.4f, 2.75f, wz), new Vector3(1.3f, 0.3f, t + 0.1f), mortarDark).GetComponent<Renderer>());
            back.upper.Add(Box("BackDoor_LintelR", new Vector3(2.8f, 2.75f, wz), new Vector3(1.3f, 0.3f, t + 0.1f), mortarDark).GetComponent<Renderer>());
            // 左右の壁：2階の廊下へ通じる扉（z 2.6〜3.6、高さ3m〜5.3m）
            var left = Wall(new Vector2(-wx, -wz), new Vector2(-wx, wz), 0f, RoomH, t, new Vector4(wz + 2.6f, wz + 3.6f, Floor2Y, Floor2Y + 2.3f));
            var right = Wall(new Vector2(wx, -wz), new Vector2(wx, wz), 0f, RoomH, t, new Vector4(wz + 2.6f, wz + 3.6f, Floor2Y, Floor2Y + 2.3f));
            // 手前の壁：中央に出入口
            var front = Wall(new Vector2(-wx - t / 2f, -wz), new Vector2(wx + t / 2f, -wz), 0f, RoomH, t, new Vector4(wx + t / 2f - 1f, wx + t / 2f + 1f, 0f, 3f));
            Box("Door_FrameL", new Vector3(-1.15f, 1.5f, -HalfD + 0.05f), new Vector3(0.3f, 3f, 0.5f), mortarDark);
            Box("Door_FrameR", new Vector3(1.15f, 1.5f, -HalfD + 0.05f), new Vector3(0.3f, 3f, 0.5f), mortarDark);
            front.upper.Add(Box("Door_Lintel", new Vector3(0f, 3.2f, -HalfD + 0.05f), new Vector3(2.6f, 0.4f, 0.5f), mortarDark).GetComponent<Renderer>());
            Label(new Vector3(0f, 3.7f, -HalfD), "談話室の出入口");

            Window(left, new Vector3(-HalfW, 0f, -3.6f), Vector3.right);
            Window(left, new Vector3(-HalfW, 0f, 5.4f), Vector3.right);
            Window(right, new Vector3(HalfW, 0f, -6.0f), Vector3.left);
            Window(right, new Vector3(HalfW, 0f, 5.4f), Vector3.left);
            Window(back, new Vector3(-2.2f, 0f, HalfD), Vector3.back, 3.6f, 0.8f, 1.4f);
            Window(back, new Vector3(2.2f, 0f, HalfD), Vector3.back, 3.6f, 0.8f, 1.4f);

            // 天井の梁（上から見るときは消える）
            for (int i = -3; i <= 3; i++)
                ceilingRenderers.Add(Box("Beam", new Vector3(0f, RoomH - 0.2f, i * 2.1f), new Vector3(HalfW * 2f, 0.35f, 0.35f), woodDark).GetComponent<Renderer>());
            Chandelier(new Vector3(0f, 4.6f, 1.9f), 5f, 10f);
            Chandelier(new Vector3(0f, 4.6f, -4.2f), 4f, 9f);

            Sconce(left, new Vector3(-HalfW + 0.15f, 3.9f, -0.5f), Vector3.right);
            Sconce(right, new Vector3(HalfW - 0.15f, 3.9f, -0.5f), Vector3.left);
            Sconce(front, new Vector3(-3.5f, 2.8f, -HalfD + 0.15f), Vector3.forward);
            Sconce(front, new Vector3(3.5f, 2.8f, -HalfD + 0.15f), Vector3.forward);

            // --- 奥：大きな暖炉 ---
            float fz = HalfD - 0.55f; // 炉の手前
            Box("Hearth", new Vector3(0f, 0.15f, fz), new Vector3(3.4f, 0.3f, 1.1f), mortarDark);
            Box("Fireplace_PillarL", new Vector3(-1.4f, 1.45f, fz + 0.2f), new Vector3(0.6f, 2.6f, 0.7f), mortarDark);
            Box("Fireplace_PillarR", new Vector3(1.4f, 1.45f, fz + 0.2f), new Vector3(0.6f, 2.6f, 0.7f), mortarDark);
            Box("Fireplace_Mantel", new Vector3(0f, 2.85f, fz + 0.1f), new Vector3(3.8f, 0.3f, 0.95f), woodDark);
            back.upper.Add(Box("Fireplace_Chimney", new Vector3(0f, 4.45f, fz + 0.3f), new Vector3(2.9f, 3.1f, 0.5f), StoneFor(2.9f, 3.1f)).GetComponent<Renderer>());
            Box("Fireplace_Back", new Vector3(0f, 1.3f, fz + 0.5f), new Vector3(2.3f, 2.3f, 0.1f), iron);
            Cyl("Log1", new Vector3(-0.2f, 0.42f, fz + 0.1f), new Vector3(0.18f, 0.6f, 0.18f), Quaternion.Euler(0f, 0f, 90f), woodDark);
            Cyl("Log2", new Vector3(0.25f, 0.42f, fz + 0.15f), new Vector3(0.18f, 0.55f, 0.18f), Quaternion.Euler(0f, 30f, 90f), woodDark);
            for (int i = 0; i < 5; i++)
            {
                var f = Box("Flame" + i, new Vector3(-0.5f + i * 0.25f, 0.65f, fz + 0.1f), new Vector3(0.18f, 0.5f, 0.18f), i % 2 == 0 ? glowFire : glowEmber);
                f.AddComponent<FlameWobble>().seed = i * 1.7f;
            }
            var fire = Lamp(new Vector3(0f, 1.0f, fz - 0.55f), 9f, 11f, true, 0.2f);
            fire.color = new Color(1f, 0.6f, 0.3f);
            fire.GetComponent<Flicker>().isFire = true;
            blockers0.Add(new Rect(-1.85f, fz - 0.6f, 3.7f, 1.2f));

            Bookshelf(new Vector3(-5.8f, 0f, HalfD - 0.25f), 2f);
            Bookshelf(new Vector3(5.8f, 0f, HalfD - 0.25f), 2f);

            Sofa("暖炉の前の椅子", "談話室", new Vector3(-1.3f, 0f, 5.4f), 0f, 1.0f, 1, fabricGreen);
            Sofa("暖炉の前の椅子", "談話室", new Vector3(1.3f, 0f, 5.4f), 0f, 1.0f, 1, fabricGreen);

            // --- ソファで囲む低いテーブル ---
            Box("LowTable", new Vector3(0f, 0.22f, 2.0f), new Vector3(1.4f, 0.08f, 0.8f), woodMid);
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0) ? -0.6f : 0.6f, z = (i < 2) ? 1.7f : 2.3f;
                Box("LowTable_Leg", new Vector3(x, 0.1f, z), new Vector3(0.08f, 0.2f, 0.08f), woodDark);
            }
            blockers0.Add(new Rect(-0.75f, 1.55f, 1.5f, 0.9f));
            Sofa("ソファ", "談話室", new Vector3(0f, 0f, 0.5f), 0f, 2.4f, 3, fabricRed);
            Sofa("ソファ", "談話室", new Vector3(-1.95f, 0f, 2.0f), 90f, 2.4f, 3, fabricRed);
            Sofa("ソファ", "談話室", new Vector3(1.95f, 0f, 2.0f), -90f, 2.4f, 3, fabricRed);

            // --- 大きなテーブル（右手前） ---
            Box("BigTable_Top", new Vector3(3.4f, 0.75f, -4.0f), new Vector3(2.4f, 0.08f, 1.0f), woodMid);
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0) ? 2.3f : 4.5f, z = (i < 2) ? -4.4f : -3.6f;
                Box("BigTable_Leg", new Vector3(x, 0.37f, z), new Vector3(0.1f, 0.74f, 0.1f), woodDark);
            }
            Box("Candle", new Vector3(3.4f, 0.9f, -4.0f), new Vector3(0.06f, 0.22f, 0.06f), glowLamp);
            blockers0.Add(new Rect(2.2f, -4.5f, 2.4f, 1.0f));
            for (int i = 0; i < 3; i++)
            {
                float x = 2.6f + i * 0.8f;
                Chair("大きなテーブル", "談話室", new Vector3(x, 0f, -3.2f), 180f);
                Chair("大きなテーブル", "談話室", new Vector3(x, 0f, -4.8f), 0f);
            }
            // --- 窓辺のベンチ（左右）と、読書の椅子（左手前。2026-10-21 追加） ---
            Bench(new Vector3(-HalfW + 0.4f, 0f, -3.6f), 90f);
            Bench(new Vector3(HalfW - 0.4f, 0f, -4.4f), -90f);
            Sofa("読書の椅子", "談話室", new Vector3(-5.9f, 0f, -6.4f), 0f, 1.0f, 1, fabricGreen);
            Sofa("読書の椅子", "談話室", new Vector3(-4.1f, 0f, -6.4f), 0f, 1.0f, 1, fabricGreen);
            Cyl("ReadingTable", new Vector3(-5.0f, 0.5f, -6.5f), new Vector3(0.5f, 0.02f, 0.5f), Quaternion.identity, woodMid);
            Cyl("ReadingTable_Leg", new Vector3(-5.0f, 0.25f, -6.5f), new Vector3(0.08f, 0.25f, 0.08f), Quaternion.identity, woodDark);
            blockers0.Add(new Rect(-5.25f, -6.75f, 0.5f, 0.5f));

            BuildStairs(-1);
            BuildStairs(1);

            pois.Add(MakePoi("暖炉にあたっている", new Vector3(0f, 0.8f, fz + 0.15f), new Vector3(-0.5f, 0f, fz - 0.9f), new Vector3(0.5f, 0f, fz - 0.9f), new Vector3(0f, 0f, fz - 1.4f)));
            pois.Add(MakePoi("本棚の本を眺めている", new Vector3(-5.8f, 1.5f, HalfD), new Vector3(-6.2f, 0f, HalfD - 1.1f), new Vector3(-5.35f, 0f, HalfD - 1.05f)));
            pois.Add(MakePoi("本棚の本を眺めている", new Vector3(5.8f, 1.5f, HalfD), new Vector3(5.35f, 0f, HalfD - 1.05f), new Vector3(6.2f, 0f, HalfD - 1.1f)));
            pois.Add(MakePoi("窓の外を眺めている", new Vector3(HalfW, 2f, -6.0f), new Vector3(HalfW - 0.8f, 0f, -6.0f)));
            // 魔法の練習場所（床の魔法陣）。趣味が「魔法の練習」のキャラだけが使う
            Cyl("MagicCircle", new Vector3(-3.6f, 0.015f, -4.3f), new Vector3(1.8f, 0.004f, 1.8f), Quaternion.identity, ProtoArt.Glow(new Color(0.4f, 0.55f, 1f), 0.6f));
            var magic = MakePoi("魔法の練習をしている", new Vector3(-3.6f, 1.2f, -4.3f), new Vector3(-4.1f, 0f, -3.9f), new Vector3(-3.1f, 0f, -3.8f));
            magic.hobby = (int)SimCharacter.HobbyKind.Magic;
            pois.Add(magic);
            // おやつワゴン（出入口の左）。おなかが空いたらここで取って、談話室で食べる
            float sz = -HalfD + 0.65f;
            Box("SnackTrolley", new Vector3(-2.2f, 0.4f, sz), new Vector3(0.8f, 0.8f, 0.5f), woodMid);
            for (int i = 0; i < 3; i++) Box("Snack", new Vector3(-2.45f + i * 0.25f, 0.86f, sz), new Vector3(0.18f, 0.1f, 0.18f), foodMat);
            blockers0.Add(new Rect(-2.6f, sz - 0.25f, 0.8f, 0.5f));
            var snackPoi = MakePoi("おやつを取っている", new Vector3(-2.2f, 0.8f, sz), new Vector3(-2.2f, 0f, sz + 0.65f), new Vector3(-1.35f, 0f, sz + 0.05f));
            snackPoi.snack = true;
            pois.Add(snackPoi);
            Label(new Vector3(-2.2f, 1.4f, sz), "おやつワゴン");

            AddArea("談話室", new Rect(-HalfW, -HalfD, HalfW * 2f, HalfD * 2f), 0);
        }

        /// <summary>談話室の左右の階段（2階の廊下へ）と、上の踊り場。side：-1 左 / 1 右</summary>
        void BuildStairs(int side)
        {
            float x = StairX * side;
            int steps = 15;
            float z0 = -1.5f, z1 = 2.4f;
            float run = (z1 - z0) / steps, rise = Floor2Y / steps;
            for (int i = 0; i < steps; i++)
            {
                float top = (i + 1) * rise;
                Box("Stair_Step", new Vector3(x, top / 2f, z0 + (i + 0.5f) * run), new Vector3(StairW, top, run), i % 2 == 0 ? woodDark : woodMid);
            }
            // 手すり（斜め。談話室側）
            float railX = x - (StairW / 2f + 0.02f) * side;
            float slope = Mathf.Atan2(Floor2Y, z1 - z0) * Mathf.Rad2Deg;
            float lenRail = Mathf.Sqrt(Floor2Y * Floor2Y + (z1 - z0) * (z1 - z0));
            BoxRot("Stair_Rail", new Vector3(railX, Floor2Y / 2f + 0.9f, (z0 + z1) / 2f), new Vector3(0.08f, 0.08f, lenRail), Quaternion.Euler(-slope, 0f, 0f), woodDark);
            Box("Stair_Post", new Vector3(railX, 0.5f, z0), new Vector3(0.12f, 1f, 0.12f), woodDark);
            // 踊り場（高さ3m）
            float lx = x, lw = StairW + 0.2f;
            Box("Landing", new Vector3(lx, Floor2Y - 0.1f, 3.1f), new Vector3(lw, 0.2f, 1.4f), woodDark);
            Box("Landing_RailSide", new Vector3(lx - lw / 2f * side, Floor2Y + 0.5f, 3.1f), new Vector3(0.08f, 1f, 1.4f), woodDark);
            Box("Landing_RailBack", new Vector3(lx, Floor2Y + 0.5f, 3.8f), new Vector3(lw, 1f, 0.08f), woodDark);
            Box("Landing_PostA", new Vector3(lx - (lw / 2f - 0.05f) * side, Floor2Y / 2f, 2.45f), new Vector3(0.15f, Floor2Y, 0.15f), woodDark);
            Box("Landing_PostB", new Vector3(lx - (lw / 2f - 0.05f) * side, Floor2Y / 2f, 3.75f), new Vector3(0.15f, Floor2Y, 0.15f), woodDark);
            blockers0.Add(new Rect(x - StairW / 2f, z0, StairW, z1 - z0));
            blockers0.Add(new Rect(lx - (lw / 2f - 0.05f) * side - 0.1f, 2.35f, 0.2f, 0.2f));
            blockers0.Add(new Rect(lx - (lw / 2f - 0.05f) * side - 0.1f, 3.65f, 0.2f, 0.2f));
            Label(new Vector3(x, 1.4f, z0 - 0.3f), side < 0 ? "階段（2階・左の廊下へ）" : "階段（2階・右の廊下へ）");
        }

        // ================= 1階手前：食堂・廊下・脱衣所・浴場 =================
        void BuildFrontBand()
        {
            float zb = -HalfD - 0.2f;                 // 談話室の手前の壁（-7.7）
            float zc = (FrontZ + (-HalfD)) / 2f;      // -11
            float depth = -HalfD - FrontZ;            // 7m
            Box("Floor_Dining", new Vector3(-7f, -0.05f, zc), new Vector3(10f, 0.1f, depth), woodFloor);
            Box("Floor_Corridor", new Vector3(0f, -0.05f, zc), new Vector3(4f, 0.1f, depth), flagstone);
            Box("Floor_Dressing", new Vector3(3.5f, -0.05f, zc), new Vector3(3f, 0.1f, depth), woodFloor);
            Box("Floor_Bath", new Vector3(8f, -0.05f, zc), new Vector3(6f, 0.1f, depth), mortarDark);

            float t = 0.4f, it = 0.2f;
            float fzw = FrontZ - 0.2f; // 手前の外壁（-14.7）
            // 後ろの壁（談話室の左右にはみ出す部分）
            Wall(new Vector2(-12.4f, zb), new Vector2(-WingInner, zb), 0f, BandH, t);
            Wall(new Vector2(WingInner, zb), new Vector2(11.4f, zb), 0f, BandH, t);
            // 外壁（左は台所への扉 z -13.3〜-12.3）
            var dl = Wall(new Vector2(-12.2f, fzw - 0.2f), new Vector2(-12.2f, zb + 0.2f), 0f, BandH, t, new Vector4(1.6f, 2.6f, 0f, 2.4f));
            var br = Wall(new Vector2(11.2f, fzw - 0.2f), new Vector2(11.2f, zb + 0.2f), 0f, BandH, t);
            var fw = Wall(new Vector2(-12.4f, fzw), new Vector2(11.4f, fzw), 0f, BandH, t, new Vector4(11.4f, 13.4f, 0f, 2.8f));
            // 食堂｜廊下｜脱衣所｜浴場 の仕切り（扉は z -11.6〜-10.4）
            var pd = new Vector4(2.9f, 4.1f, 0f, 2.4f);
            Wall(new Vector2(-2f, FrontZ), new Vector2(-2f, zb - 0.2f), 0f, BandH, it, pd);
            Wall(new Vector2(2f, FrontZ), new Vector2(2f, zb - 0.2f), 0f, BandH, it, pd);
            Wall(new Vector2(5f, FrontZ), new Vector2(5f, zb - 0.2f), 0f, BandH, it, pd);

            Window(fw, new Vector3(-9.5f, 0f, FrontZ), Vector3.forward, 1.0f, 1.0f, 1.6f);
            Window(fw, new Vector3(-5f, 0f, FrontZ), Vector3.forward, 1.0f, 1.0f, 1.6f);
            Window(dl, new Vector3(-12f, 0f, -10.0f), Vector3.right, 1.0f, 1.0f, 1.6f);
            Window(br, new Vector3(11f, 0f, -11f), Vector3.left, 1.8f, 1.6f, 1.0f);
            Window(fw, new Vector3(8f, 0f, FrontZ), Vector3.forward, 1.8f, 1.6f, 1.0f);

            // --- 廊下 ---
            Box("Corridor_Bench", new Vector3(1.6f, 0.22f, -9.0f), new Vector3(0.45f, 0.44f, 1.4f), woodDark);
            blockers0.Add(new Rect(1.35f, -9.7f, 0.5f, 1.4f));
            Box("NoticeBoard", new Vector3(-1.85f, 1.6f, -8.8f), new Vector3(0.05f, 0.9f, 1.2f), woodMid);
            Lamp(new Vector3(0f, 2.9f, -11f), 2.5f, 7f);
            Label(new Vector3(0f, 2.9f, -12.6f), "廊下");
            AddArea("廊下", new Rect(-2f, FrontZ, 4f, depth), 0);
            AddArea("外", new Rect(-1.5f, -18.2f, 3f, 3.5f), 0);

            // --- 食堂（10m×7m） ---
            Label(new Vector3(-7f, 3.0f, -8.1f), "食堂");
            AddArea("食堂", new Rect(-12f, FrontZ, 10f, depth), 0);
            Box("Counter", new Vector3(-11.5f, 0.5f, -9.3f), new Vector3(0.8f, 1.0f, 3.0f), woodDark);
            Box("Counter_Top", new Vector3(-11.5f, 1.02f, -9.3f), new Vector3(0.9f, 0.05f, 3.1f), woodMid);
            for (int i = 0; i < 3; i++) Box("Food", new Vector3(-11.45f, 1.12f, -10.3f + i * 0.95f), new Vector3(0.35f, 0.12f, 0.35f), foodMat);
            blockers0.Add(new Rect(-11.95f, -10.85f, 0.9f, 3.1f));
            pois.Add(new Poi
            {
                label = "料理を受け取っている",
                lookAt = new Vector3(-11.5f, 1f, -9.3f),
                slots = new[] { new Vector3(-10.6f, 0f, -10.3f), new Vector3(-10.6f, 0f, -9.3f), new Vector3(-10.6f, 0f, -8.3f) },
                occupants = new SimCharacter[3],
                counter = true
            });
            // 縦長の机2つ（12席。机と机の間は通路）
            DiningTable(new Vector3(-7.6f, 0f, -11.3f));
            DiningTable(new Vector3(-4.3f, 0f, -11.3f));
            Chandelier(new Vector3(-9.3f, 2.9f, -11.3f), 3.5f, 7f);
            Chandelier(new Vector3(-5.0f, 2.9f, -11.3f), 3.5f, 7f);

            // --- 脱衣所（3m×7m） ---
            Label(new Vector3(3.5f, 3.0f, -8.1f), "脱衣所");
            AddArea("脱衣所", new Rect(2f, FrontZ, 3f, depth), 0);
            for (int i = 0; i < 4; i++) Box("Locker", new Vector3(2.45f + i * 0.7f, 0.9f, FrontZ + 0.45f), new Vector3(0.6f, 1.8f, 0.4f), woodMid);
            blockers0.Add(new Rect(2.15f, FrontZ + 0.2f, 2.8f, 0.5f));
            Box("Dressing_Bench", new Vector3(3.5f, 0.22f, -9.2f), new Vector3(0.45f, 0.44f, 1.4f), woodDark);
            blockers0.Add(new Rect(3.275f, -9.9f, 0.45f, 1.4f));
            Box("Dressing_Basket", new Vector3(3.5f, 0.15f, -13.0f), new Vector3(0.5f, 0.3f, 0.4f), fabricBrown);
            blockers0.Add(new Rect(3.25f, -13.2f, 0.5f, 0.4f));
            Lamp(new Vector3(3.5f, 2.9f, -11f), 2.0f, 5f);

            // --- 浴場（6m×7m。湯船は6人、手前に洗い場） ---
            Label(new Vector3(8f, 3.0f, -8.1f), "浴場");
            AddArea("浴場", new Rect(5f, FrontZ, 6f, depth), 0);
            float tx0 = 7.1f, tx1 = 10.9f, tz0 = -14.1f, tz1 = -9.3f;
            float rim = 0.3f, rimH = 0.5f;
            Box("Tub_RimL", new Vector3(tx0 + rim / 2f, rimH / 2f, (tz0 + tz1) / 2f), new Vector3(rim, rimH, tz1 - tz0), mortarDark);
            Box("Tub_RimR", new Vector3(tx1 - rim / 2f, rimH / 2f, (tz0 + tz1) / 2f), new Vector3(rim, rimH, tz1 - tz0), mortarDark);
            Box("Tub_RimF", new Vector3((tx0 + tx1) / 2f, rimH / 2f, tz0 + rim / 2f), new Vector3(tx1 - tx0, rimH, rim), mortarDark);
            Box("Tub_RimB", new Vector3((tx0 + tx1) / 2f, rimH / 2f, tz1 - rim / 2f), new Vector3(tx1 - tx0, rimH, rim), mortarDark);
            Box("Tub_Water", new Vector3((tx0 + tx1) / 2f, 0.38f, (tz0 + tz1) / 2f), new Vector3(tx1 - tx0 - rim * 2f, 0.04f, tz1 - tz0 - rim * 2f), waterMat);
            blockers0.Add(Rect.MinMaxRect(tx0, tz0, tx1, tz1));
            float[] bx = { 8.2f, 9.8f };
            float[] bz = { -13.0f, -11.7f, -10.4f };
            foreach (var x in bx)
                foreach (var z in bz)
                    seats.Add(new Seat { kind = SeatKind.Bath, label = "湯船", area = "浴場", position = new Vector3(x, 0f, z), forward = Vector3.left, approach = new Vector3(6.6f, 0f, z) });
            // 洗い場（奥の壁ぎわ。小さな腰掛けと桶）
            for (int i = 0; i < 4; i++)
            {
                float x = 5.9f + i * 1.4f;
                Box("WashStool", new Vector3(x, 0.15f, zb - 0.55f), new Vector3(0.35f, 0.3f, 0.35f), woodMid);
                Cyl("WashBucket", new Vector3(x + 0.35f, 0.12f, zb - 0.45f), new Vector3(0.3f, 0.12f, 0.3f), Quaternion.identity, woodDark);
                Box("WashMirror", new Vector3(x, 1.2f, zb - 0.22f), new Vector3(0.5f, 0.6f, 0.03f), waterMat);
                blockers0.Add(new Rect(x - 0.25f, zb - 0.8f, 0.8f, 0.6f));
            }
            Lamp(new Vector3(8f, 2.9f, -11f), 2.5f, 7f);
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
            blockers0.Add(new Rect(c.x - 0.45f, c.z - 1.65f, 0.9f, 3.3f));
            float[] zs = { -1.1f, 0f, 1.1f };
            foreach (var dz in zs)
            {
                Chair("食堂の席", "食堂", new Vector3(c.x - 0.8f, 0f, c.z + dz), 90f);
                Chair("食堂の席", "食堂", new Vector3(c.x + 0.8f, 0f, c.z + dz), -90f);
            }
        }

        // ================= 2階：個室 =================
        /// <summary>side：-1 左 / 1 右。談話室の外側に廊下（幅2.4m）、その外に個室（幅3m×奥行き5.4m）が5つ。右の奥の1つは物置。</summary>
        void BuildWing(int side)
        {
            float s = side;
            float inner = WingInner, corr = inner + CorrW, outer = corr + RoomDepth;
            // 土台（1階の部分）と2階の床
            Box("Wing_Base", new Vector3(s * (inner + outer) / 2f, (Floor2Y - 0.05f) / 2f, 0f), new Vector3(outer - inner, Floor2Y - 0.05f, HalfD * 2f), mortarDark);
            Box("Wing_Floor", new Vector3(s * (inner + outer) / 2f, Floor2Y - 0.025f, 0f), new Vector3(outer - inner, 0.05f, HalfD * 2f), woodFloor);

            float t = 0.4f, it = 0.2f;
            var outerW = Wall(new Vector2(s * (outer + 0.2f), -HalfD - 0.4f), new Vector2(s * (outer + 0.2f), HalfD + 0.4f), Floor2Y, WingTop, t);
            // 奥の壁：左の廊下の突き当たりは屋上の天文台への扉（x -7.75〜-8.65）、右は篤の部屋への扉（x 8.0〜9.0）
            var endDoor = side < 0 ? new Vector4(0.35f, 1.25f, Floor2Y, Floor2Y + 2.2f) : new Vector4(0.6f, 1.6f, Floor2Y, Floor2Y + 2.2f);
            Wall(new Vector2(s * inner, HalfD + 0.2f), new Vector2(s * (outer + 0.4f), HalfD + 0.2f), Floor2Y, WingTop, t, endDoor);
            Wall(new Vector2(s * inner, -HalfD - 0.2f), new Vector2(s * (outer + 0.4f), -HalfD - 0.2f), Floor2Y, WingTop, t);

            // 廊下の壁（各部屋の扉つき。物置には扉がない）
            var gaps = new List<Vector4>();
            for (int i = 0; i < 5; i++)
            {
                if (side > 0 && i == 4) continue;
                float cz = -HalfD + RoomW * (i + 0.5f);
                float along = cz + HalfD; // a.z = -HalfD から測る
                gaps.Add(new Vector4(along - 0.6f, along + 0.6f, Floor2Y, Floor2Y + 2.2f));
            }
            Wall(new Vector2(s * corr, -HalfD), new Vector2(s * corr, HalfD), Floor2Y, WingTop, it, gaps.ToArray());
            // 部屋の仕切り
            for (int i = 1; i < 5; i++)
            {
                float z = -HalfD + i * RoomW;
                Wall(new Vector2(s * corr, z), new Vector2(s * outer, z), Floor2Y, WingTop, it);
            }
            Lamp(new Vector3(s * (inner + corr) / 2f, Floor2Y + 2.2f, -3.75f), 2f, 6f);
            Lamp(new Vector3(s * (inner + corr) / 2f, Floor2Y + 2.2f, 3.75f), 2f, 6f);
            Label(new Vector3(s * (inner + corr) / 2f, Floor2Y + 2.4f, -HalfD + 0.6f), side < 0 ? "2階・左の廊下" : "2階・右の廊下");
            AddArea(side < 0 ? "2階・左の廊下" : "2階・右の廊下", Rect.MinMaxRect(Mathf.Min(s * inner, s * corr), -HalfD, Mathf.Max(s * inner, s * corr), HalfD), 1);

            for (int i = 0; i < 5; i++)
            {
                float cz = -HalfD + RoomW * (i + 0.5f);
                if (side > 0 && i == 4)
                {
                    // 物置（使っていない。扉はない）
                    Box("Storage_Crate", new Vector3(s * (corr + 1.4f), Floor2Y + 0.4f, cz - 0.6f), new Vector3(0.8f, 0.8f, 0.8f), woodDark);
                    Box("Storage_Crate", new Vector3(s * (corr + 2.5f), Floor2Y + 0.3f, cz + 0.4f), new Vector3(0.6f, 0.6f, 0.6f), woodMid);
                    Box("Storage_Sack", new Vector3(s * (corr + 4.3f), Floor2Y + 0.3f, cz - 0.2f), new Vector3(0.7f, 0.6f, 0.6f), fabricBrown);
                    Label(new Vector3(s * (corr + 2.7f), Floor2Y + 2.0f, cz), "物置");
                    AddArea("物置", Rect.MinMaxRect(Mathf.Min(s * corr, s * outer), cz - RoomW / 2f, Mathf.Max(s * corr, s * outer), cz + RoomW / 2f), 1);
                    continue;
                }
                string name = (side < 0 ? "左" : "右") + (i + 1);
                var room = new RoomInfo { name = name };
                System.Func<float, float> X = u => s * (corr + u); // 廊下の壁から部屋の奥への距離 u（0〜5.4）
                Window(outerW, new Vector3(s * outer, Floor2Y, cz), new Vector3(-s, 0f, 0f), 1.0f, 0.9f, 1.1f);
                // ベッド（頭は外壁側、部屋の手前寄り）
                float bedX0 = X(RoomDepth - 0.1f), bedX1 = X(RoomDepth - 2.1f), bz = cz - 0.95f;
                float bcx = (bedX0 + bedX1) / 2f;
                Box("Bed_Frame", new Vector3(bcx, Floor2Y + 0.2f, bz), new Vector3(2.0f, 0.4f, 0.85f), woodDark);
                Box("Bed_Mattress", new Vector3(bcx, Floor2Y + 0.47f, bz), new Vector3(1.9f, 0.14f, 0.8f), fabricWhite);
                Box("Bed_Blanket", new Vector3(bcx - s * 0.3f, Floor2Y + 0.55f, bz), new Vector3(1.3f, 0.04f, 0.82f), fabricRed);
                Box("Bed_Pillow", new Vector3(X(RoomDepth - 0.4f), Floor2Y + 0.58f, bz), new Vector3(0.4f, 0.1f, 0.55f), fabricWhite);
                Box("Bed_Head", new Vector3(X(RoomDepth - 0.1f), Floor2Y + 0.55f, bz), new Vector3(0.08f, 1.1f, 0.85f), woodDark);
                blockers1.Add(Rect.MinMaxRect(Mathf.Min(bedX0, bedX1), bz - 0.43f, Mathf.Max(bedX0, bedX1), bz + 0.43f));
                room.bed = new Seat
                {
                    kind = SeatKind.Bed,
                    label = "ベッド",
                    area = name,
                    position = new Vector3(bcx, Floor2Y, bz),
                    forward = new Vector3(s, 0f, 0f),
                    approach = new Vector3(X(RoomDepth - 2.5f), Floor2Y, cz - 0.2f),
                    room = room
                };
                seats.Add(room.bed);
                // 来客用：座布団2枚と小さなテーブル、床に敷く布団
                Box("LowTable_Room", new Vector3(X(2.3f), Floor2Y + 0.15f, cz + 0.65f), new Vector3(0.5f, 0.3f, 0.5f), woodMid);
                blockers1.Add(Rect.MinMaxRect(Mathf.Min(X(2.05f), X(2.55f)), cz + 0.4f, Mathf.Max(X(2.05f), X(2.55f)), cz + 0.9f));
                for (int k = 0; k < 2; k++)
                {
                    float cx = X(k == 0 ? 1.55f : 3.05f);
                    Box("Cushion", new Vector3(cx, Floor2Y + 0.04f, cz + 0.65f), new Vector3(0.5f, 0.08f, 0.5f), fabricGreen);
                    var cu = new Seat
                    {
                        kind = SeatKind.Sit, floorSeat = true, label = "空き部屋", area = name,
                        position = new Vector3(cx, Floor2Y, cz + 0.65f),
                        forward = new Vector3(k == 0 ? s : -s, 0f, 0f),
                        approach = new Vector3(cx, Floor2Y, cz),
                        room = room
                    };
                    room.cushions.Add(cu);
                    seats.Add(cu);
                }
                Box("GuestFuton", new Vector3(X(2.05f), Floor2Y + 0.05f, bz), new Vector3(1.7f, 0.1f, 0.75f), fabricWhite);
                room.futon = new Seat
                {
                    kind = SeatKind.Bed, floorSeat = true, label = "来客用の布団", area = name,
                    position = new Vector3(X(2.05f), Floor2Y, bz),
                    forward = new Vector3(s, 0f, 0f),
                    approach = new Vector3(X(2.05f), Floor2Y, cz - 0.2f),
                    room = room
                };
                seats.Add(room.futon);
                // 机（外壁側、部屋の奥寄り）
                float deskX = X(RoomDepth - 0.4f), dz = cz + 0.9f;
                Box("Desk", new Vector3(deskX, Floor2Y + 0.73f, dz), new Vector3(0.6f, 0.05f, 0.8f), woodMid);
                Box("Desk_Body", new Vector3(deskX, Floor2Y + 0.36f, dz), new Vector3(0.55f, 0.72f, 0.75f), woodDark);
                Box("Desk_Book", new Vector3(deskX, Floor2Y + 0.8f, dz - 0.12f), new Vector3(0.25f, 0.06f, 0.18f), fabricGreen);
                var lampGlow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                lampGlow.transform.SetParent(root, false);
                lampGlow.transform.position = new Vector3(deskX, Floor2Y + 0.95f, dz + 0.28f);
                lampGlow.transform.localScale = Vector3.one * 0.12f;
                lampGlow.GetComponent<Renderer>().sharedMaterial = glowLamp;
                Destroy(lampGlow.GetComponent<Collider>());
                blockers1.Add(Rect.MinMaxRect(Mathf.Min(X(RoomDepth - 0.7f), X(RoomDepth)), dz - 0.4f, Mathf.Max(X(RoomDepth - 0.7f), X(RoomDepth)), dz + 0.4f));
                room.desk = new Poi
                {
                    label = "机で勉強している",
                    lookAt = new Vector3(deskX, Floor2Y + 0.8f, dz),
                    slots = new[] { new Vector3(X(RoomDepth - 1.15f), Floor2Y, dz) },
                    occupants = new SimCharacter[1]
                };
                pois.Add(room.desk);
                // タンス（廊下側の壁ぎわ。扉の横）
                Box("Wardrobe", new Vector3(X(0.35f), Floor2Y + 0.9f, cz + 1.05f), new Vector3(0.5f, 1.8f, 0.7f), woodDark);
                blockers1.Add(Rect.MinMaxRect(Mathf.Min(X(0.1f), X(0.6f)), cz + 0.7f, Mathf.Max(X(0.1f), X(0.6f)), cz + 1.4f));
                Lamp(new Vector3(X(RoomDepth - 2.5f), Floor2Y + 2.3f, cz), 1.5f, 4.5f, false, 0.03f);
                room.labelPos = new Vector3(X(RoomDepth - 2.4f), Floor2Y + 2.0f, cz);
                room.rect = Rect.MinMaxRect(Mathf.Min(s * corr, s * outer), cz - RoomW / 2f, Mathf.Max(s * corr, s * outer), cz + RoomW / 2f);
                AddArea(name + "の部屋", room.rect, 1);
                rooms.Add(room);
            }
        }

        // ================= 篤の部屋 =================
        /// <summary>
        /// 篤の部屋：2階・右の廊下の突き当たりの先にある離れ（もとは物置だった所を部屋にした設定。famuru 決定 2026-10-21）。
        /// 広さは個室の倍（7.8m×4.2m）。下は石の土台。
        /// </summary>
        void BuildFRoom()
        {
            float x0 = WingInner, x1 = WingInner + CorrW + RoomDepth; // 7.4 〜 15.2（内側）
            float z0 = HalfD + 0.4f, z1 = z0 + 4.0f;                   // 7.9 〜 11.9（内側）
            float y = Floor2Y, t = 0.4f;
            Box("Annex_Base", new Vector3((x0 + x1) / 2f + 0.1f, (y - 0.05f) / 2f, (z0 + z1) / 2f + 0.1f), new Vector3(x1 - x0 + 0.6f, y - 0.05f, z1 - z0 + 0.6f), mortarDark);
            Box("Annex_Floor", new Vector3((x0 + x1) / 2f, y - 0.025f, (z0 + z1) / 2f), new Vector3(x1 - x0, 0.05f, z1 - z0), woodFloor);
            var outerW = Wall(new Vector2(x1 + 0.2f, z0 - 0.2f), new Vector2(x1 + 0.2f, z1 + 0.4f), y, WingTop, t);
            var backW = Wall(new Vector2(x0 - 0.4f, z1 + 0.2f), new Vector2(x1 + 0.4f, z1 + 0.2f), y, WingTop, t);
            var leftW = Wall(new Vector2(x0 - 0.2f, z0 - 0.2f), new Vector2(x0 - 0.2f, z1 + 0.2f), y, WingTop, t);
            Window(outerW, new Vector3(x1, y, (z0 + z1) / 2f), Vector3.left, 1.0f, 1.0f, 1.2f);
            Window(backW, new Vector3(11.6f, y, z1), Vector3.back, 1.0f, 1.2f, 1.2f);
            Window(leftW, new Vector3(x0, y, 8.7f), Vector3.right, 1.0f, 0.8f, 1.2f);
            var room = new RoomInfo { name = "篤" };
            // ベッド（個室のベッドの1.5倍。奥の壁・外壁から少し離し、縁に何人か並んで座れる。famuru 決定 2026-10-21）
            float bw = 3.1f, bd = 2.1f;                       // 長さ（x）・幅（z）
            float bx0 = x1 - 0.3f, bx1 = bx0 - bw;            // 頭は外壁側
            float bz = z1 - 0.3f - bd / 2f;                   // 奥の壁から 0.3m
            float bcx = (bx0 + bx1) / 2f;
            Box("Bed_Frame", new Vector3(bcx, y + 0.2f, bz), new Vector3(bw, 0.4f, bd), woodDark);
            Box("Bed_Mattress", new Vector3(bcx, y + 0.47f, bz), new Vector3(bw - 0.1f, 0.14f, bd - 0.1f), fabricWhite);
            Box("Bed_Blanket", new Vector3(bcx - 0.45f, y + 0.55f, bz), new Vector3(bw * 0.65f, 0.04f, bd - 0.08f), fabricGreen);
            for (int k = -1; k <= 1; k += 2) Box("Bed_Pillow", new Vector3(bx0 - 0.3f, y + 0.58f, bz + k * 0.5f), new Vector3(0.4f, 0.1f, 0.8f), fabricWhite);
            Box("Bed_Head", new Vector3(bx0 + 0.04f, y + 0.6f, bz), new Vector3(0.08f, 1.2f, bd), woodDark);
            blockers1.Add(Rect.MinMaxRect(bx1, bz - bd / 2f, bx0 + 0.1f, bz + bd / 2f));
            room.bed = new Seat
            {
                kind = SeatKind.Bed, label = "ベッド", area = room.name, position = new Vector3(bcx, y, bz),
                forward = new Vector3(1f, 0f, 0f), approach = new Vector3(bcx - 0.4f, y, bz - bd / 2f - 0.5f), room = room
            };
            seats.Add(room.bed);
            room.bedEdge = new Vector3(0f, 0f, -1f); // 縁は手前（部屋の中）側
            room.bedSpread = 1.6f;
            room.bedEdgeDist = bd / 2f - 0.3f;
            // ベッドの縁に座る席（3人）
            for (int k = 0; k < 3; k++)
            {
                float sx = bx1 + 0.55f + k * 0.95f;
                seats.Add(new Seat
                {
                    kind = SeatKind.Sit, label = "篤の部屋のベッド", area = room.name, room = room,
                    position = new Vector3(sx, y, bz - bd / 2f + 0.15f), forward = new Vector3(0f, 0f, -1f),
                    approach = new Vector3(sx, y, bz - bd / 2f - 0.45f)
                });
            }
            // 机（奥の壁ぎわ）
            float dX = 9.9f, dZ = z1 - 0.35f;
            Box("Desk", new Vector3(dX, y + 0.73f, dZ), new Vector3(1.2f, 0.05f, 0.6f), woodMid);
            Box("Desk_Body", new Vector3(dX, y + 0.36f, dZ), new Vector3(1.1f, 0.72f, 0.55f), woodDark);
            Box("Desk_Book", new Vector3(dX - 0.3f, y + 0.8f, dZ), new Vector3(0.25f, 0.06f, 0.18f), fabricRed);
            var lampGlow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lampGlow.transform.SetParent(root, false);
            lampGlow.transform.position = new Vector3(dX + 0.4f, y + 0.95f, dZ + 0.1f);
            lampGlow.transform.localScale = Vector3.one * 0.12f;
            lampGlow.GetComponent<Renderer>().sharedMaterial = glowLamp;
            Destroy(lampGlow.GetComponent<Collider>());
            blockers1.Add(Rect.MinMaxRect(dX - 0.6f, dZ - 0.3f, dX + 0.6f, z1));
            room.desk = new Poi { label = "机で勉強している", lookAt = new Vector3(dX, y + 0.8f, dZ), slots = new[] { new Vector3(dX, y, dZ - 0.75f) }, occupants = new SimCharacter[1] };
            pois.Add(room.desk);
            // 本棚（左の壁ぎわ）
            ShelfRow(new Vector3(x0 + 0.25f, y, 10.7f), -90f, 2.0f, 2.0f, 4, fabricRed, woodMid, 401, true);
            // ソファ（手前の壁ぎわ。3人掛け）
            Sofa("篤の部屋のソファ", room.name, new Vector3(10.45f, y, z0 + 0.5f), 0f, 2.6f, 3, fabricRed);
            // 低いテーブルと座布団2枚
            Box("LowTable_Room", new Vector3(10.6f, y + 0.15f, 10.15f), new Vector3(0.9f, 0.3f, 0.6f), woodMid);
            blockers1.Add(Rect.MinMaxRect(10.15f, 9.85f, 11.05f, 10.45f));
            for (int k = 0; k < 2; k++)
            {
                float cx = k == 0 ? 9.75f : 11.45f;
                Box("Cushion", new Vector3(cx, y + 0.04f, 10.15f), new Vector3(0.5f, 0.08f, 0.5f), fabricGreen);
                var cu = new Seat
                {
                    kind = SeatKind.Sit, floorSeat = true, label = "空き部屋", area = room.name,
                    position = new Vector3(cx, y, 10.15f), forward = new Vector3(k == 0 ? 1f : -1f, 0f, 0f),
                    approach = new Vector3(cx, y, 9.5f), room = room
                };
                room.cushions.Add(cu);
                seats.Add(cu);
            }
            // 来客用の布団（手前の壁ぎわ）
            Box("GuestFuton", new Vector3(13.0f, y + 0.05f, z0 + 0.5f), new Vector3(1.7f, 0.1f, 0.75f), fabricWhite);
            room.futon = new Seat
            {
                kind = SeatKind.Bed, floorSeat = true, label = "来客用の布団", area = room.name,
                position = new Vector3(13.0f, y, z0 + 0.5f), forward = new Vector3(1f, 0f, 0f), approach = new Vector3(13.0f, y, z0 + 1.15f), room = room
            };
            seats.Add(room.futon);
            // タンス（外壁側の手前）
            Box("Wardrobe", new Vector3(x1 - 0.3f, y + 0.9f, z0 + 0.55f), new Vector3(0.6f, 1.8f, 1.0f), woodDark);
            blockers1.Add(Rect.MinMaxRect(x1 - 0.6f, z0, x1, z0 + 1.05f));
            Lamp(new Vector3(11.3f, y + 2.3f, 9.9f), 2.0f, 6f, false, 0.03f);
            room.labelPos = new Vector3(11.3f, y + 2.1f, 9.9f);
            room.rect = Rect.MinMaxRect(x0, z0 - 0.2f, x1, z1);
            AddArea(room.AreaName, room.rect, 1);
            rooms.Add(room);
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
            var rnd = new System.Random(Mathf.RoundToInt(c.x * 100));
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
            blockers0.Add(Rect.MinMaxRect(Mathf.Min(pa.x, pb.x), Mathf.Min(pa.z, pb.z), Mathf.Max(pa.x, pb.x), Mathf.Max(pa.z, pb.z)));
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
            AddSeat(t, "窓辺のベンチ", "談話室", new Vector3(-0.35f, 0f, 0f), new Vector3(-0.35f, 0f, 0.7f));
            AddSeat(t, "窓辺のベンチ", "談話室", new Vector3(0.35f, 0f, 0f), new Vector3(0.35f, 0f, 0.7f));
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
            int fl = FloorOf(floorY);
            if (fl == 0) blockers0.Add(r); else if (fl == 1) blockers1.Add(r); else blockers2.Add(r);
        }

        // ================= 歩ける場所 =================
        void BuildNav()
        {
            nav0 = new GridNav(new Vector2(-21f, -19f), new Vector2(17f, 28f), 0.25f, 0f);
            // 部屋の中（壁から少し離す）
            nav0.Open(Rect.MinMaxRect(-HalfW + 0.3f, -HalfD + 0.3f, HalfW - 0.3f, HalfD - 0.3f)); // 談話室
            nav0.Open(Rect.MinMaxRect(-1.65f, -14.25f, 1.65f, -7.95f));   // 廊下
            nav0.Open(Rect.MinMaxRect(-11.75f, -14.25f, -2.35f, -7.95f)); // 食堂
            nav0.Open(Rect.MinMaxRect(2.35f, -14.25f, 4.65f, -7.95f));    // 脱衣所
            nav0.Open(Rect.MinMaxRect(5.35f, -14.25f, 10.75f, -7.95f));   // 浴場
            nav0.Open(Rect.MinMaxRect(-1.2f, -18.2f, 1.2f, -14.95f));     // 外の小道
            // 出入口
            nav0.Open(Rect.MinMaxRect(-0.75f, -8.1f, 0.75f, -7.1f));      // 談話室の出入口
            nav0.Open(Rect.MinMaxRect(-0.75f, -15.05f, 0.75f, -14.1f));   // 寮の玄関
            nav0.Open(Rect.MinMaxRect(-2.45f, -11.5f, -1.55f, -10.5f));   // 食堂の扉
            nav0.Open(Rect.MinMaxRect(1.55f, -11.5f, 2.45f, -10.5f));     // 脱衣所の扉
            nav0.Open(Rect.MinMaxRect(4.55f, -11.5f, 5.45f, -10.5f));     // 浴場の扉
            OpenPlacesNav();
            float inflate = 0.1f; // 家具のまわりのすき間（小さいほど通路が広い）
            foreach (var r in blockers0)
                nav0.Block(Rect.MinMaxRect(r.xMin - inflate, r.yMin - inflate, r.xMax + inflate, r.yMax + inflate));

            nav1 = new GridNav(new Vector2(-16.5f, -9f), new Vector2(16.5f, 15f), 0.25f, Floor2Y);
            float inner = WingInner, corr = WingInner + CorrW, outer = corr + RoomDepth;
            for (int side = -1; side <= 1; side += 2)
            {
                float s = side;
                nav1.Open(Rect.MinMaxRect(Mathf.Min(s * (inner + 0.15f), s * (corr - 0.15f)), -HalfD + 0.25f, Mathf.Max(s * (inner + 0.15f), s * (corr - 0.15f)), HalfD - 0.25f)); // 廊下
                for (int i = 0; i < 5; i++)
                {
                    if (side > 0 && i == 4) continue; // 物置
                    float cz = -HalfD + RoomW * (i + 0.5f);
                    nav1.Open(Rect.MinMaxRect(Mathf.Min(s * (corr + 0.25f), s * (outer - 0.25f)), cz - 1.25f, Mathf.Max(s * (corr + 0.25f), s * (outer - 0.25f)), cz + 1.25f)); // 部屋
                    nav1.Open(Rect.MinMaxRect(Mathf.Min(s * (corr - 0.25f), s * (corr + 0.45f)), cz - 0.45f, Mathf.Max(s * (corr - 0.25f), s * (corr + 0.45f)), cz + 0.45f)); // 部屋の扉
                }
                float lx = StairX;
                nav1.Open(Rect.MinMaxRect(Mathf.Min(s * (lx - 0.85f), s * (lx + 0.85f)), 2.6f, Mathf.Max(s * (lx - 0.85f), s * (lx + 0.85f)), 3.6f));   // 踊り場
                nav1.Open(Rect.MinMaxRect(Mathf.Min(s * (lx + 0.3f), s * (inner + 0.3f)), 2.65f, Mathf.Max(s * (lx + 0.3f), s * (inner + 0.3f)), 3.55f)); // 踊り場→廊下の扉
            }
            nav1.Open(Rect.MinMaxRect(inner + 0.2f, HalfD + 0.6f, outer - 0.2f, HalfD + 4.2f)); // 篤の部屋
            nav1.Open(Rect.MinMaxRect(8.05f, HalfD - 0.4f, 8.95f, HalfD + 0.8f));            // 篤の部屋の扉
            OpenPlacesNav1();
            foreach (var r in blockers1)
                nav1.Block(Rect.MinMaxRect(r.xMin - inflate, r.yMin - inflate, r.xMax + inflate, r.yMax + inflate));

            // 屋上（天文台。高さ6m）
            nav2 = new GridNav(new Vector2(-16f, 9f), new Vector2(-6f, 17f), 0.25f, Floor3Y);
            OpenPlacesNav2();
            foreach (var r in blockers2)
                nav2.Block(Rect.MinMaxRect(r.xMin - inflate, r.yMin - inflate, r.xMax + inflate, r.yMax + inflate));
        }

        // ================= 壁の表示 =================
        // 床の高さごとの床の部品（下から見上げたときに透かす）
        List<KeyValuePair<float, List<Renderer>>> floorGroups;
        static readonly string[] Floor0Names = { "Ground", "Path_Outside", "Lounge_Floor", "Floor_Wood", "RugBorder", "Rug", "Floor_Dining", "Floor_Corridor", "Floor_Dressing", "Floor_Bath" };
        static readonly string[] Floor1Names = { "Wing_Base", "Wing_Floor", "Landing", "Annex_Base", "Annex_Floor" };

        void CollectFloors()
        {
            var f0 = new List<Renderer>();
            var f1 = new List<Renderer>();
            var f2 = new List<Renderer>();
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                string n = r.gameObject.name;
                if (System.Array.IndexOf(Floor0Names, n) >= 0 || System.Array.IndexOf(PlaceFloor0Names, n) >= 0) f0.Add(r);
                else if (System.Array.IndexOf(Floor1Names, n) >= 0 || System.Array.IndexOf(PlaceHallFloorNames, n) >= 0) f1.Add(r);
                else if (System.Array.IndexOf(PlaceFloor1Names, n) >= 0) f2.Add(r); // 屋上の天文台
            }
            floorGroups = new List<KeyValuePair<float, List<Renderer>>>
            {
                new KeyValuePair<float, List<Renderer>>(0.02f, f0),
                new KeyValuePair<float, List<Renderer>>(Floor2Y, f1),
                new KeyValuePair<float, List<Renderer>>(Floor3Y, f2),
            };
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
