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
    }

    public class AreaInfo
    {
        public string name;
        public Rect rect;
        public int floor;
    }

    /// <summary>
    /// 寮の建物を組み立てる部分。
    /// 1階：談話室（中央 x-6〜6, z-5〜5, 天井6m）／手前に廊下・左に食堂・右に浴場（z-11〜-5）
    /// 2階（高さ3m）：談話室の左右に個室5部屋ずつ。談話室の左右の階段から上がる。
    /// </summary>
    public partial class LoungeWorld
    {
        public const float HalfW = 6f, HalfD = 5f, RoomH = 6f;
        const float FrontZ = -11f, BandH = 3.5f, WingTop = 5.8f, Stub = 1.0f;

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
            BuildWing(-1);
            BuildWing(1);
            BuildNav();
            stairs.Add(new Stair
            {
                bottomApproach = new Vector3(-5.3f, 0f, -2.0f),
                bottom = new Vector3(-5.3f, 0f, -1.5f),
                top = new Vector3(-5.3f, Floor2Y, 2.4f),
                topApproach = new Vector3(-5.3f, Floor2Y, 3.1f)
            });
            stairs.Add(new Stair
            {
                bottomApproach = new Vector3(5.3f, 0f, -2.0f),
                bottom = new Vector3(5.3f, 0f, -1.5f),
                top = new Vector3(5.3f, Floor2Y, 2.4f),
                topApproach = new Vector3(5.3f, Floor2Y, 3.1f)
            });
        }

        // ================= 材質と光 =================
        void CreateMaterials()
        {
            flagstone = ProtoArt.Mat(new Color(0.9f, 0.88f, 0.85f), ProtoArt.Flagstone, new Vector2(5f, 4f), 0.2f);
            mortarDark = ProtoArt.Mat(new Color(0.55f, 0.52f, 0.5f), ProtoArt.StoneWall, new Vector2(1.5f, 1.5f), 0.1f);
            woodDark = ProtoArt.Mat(new Color(0.55f, 0.42f, 0.33f), ProtoArt.WoodFine, new Vector2(1f, 1f), 0.3f);
            woodMid = ProtoArt.Mat(new Color(0.85f, 0.68f, 0.52f), ProtoArt.WoodFine, new Vector2(1f, 1f), 0.3f);
            woodFloor = ProtoArt.Mat(new Color(0.8f, 0.65f, 0.5f), ProtoArt.WoodPlank, new Vector2(3f, 3f), 0.25f);
            fabricRed = ProtoArt.Mat(new Color(0.55f, 0.12f, 0.12f), ProtoArt.Fabric, new Vector2(3f, 3f), 0.05f);
            fabricGreen = ProtoArt.Mat(new Color(0.2f, 0.33f, 0.22f), ProtoArt.Fabric, new Vector2(3f, 3f), 0.05f);
            fabricBrown = ProtoArt.Mat(new Color(0.42f, 0.27f, 0.17f), ProtoArt.Fabric, new Vector2(2f, 2f), 0.1f);
            fabricWhite = ProtoArt.Mat(new Color(0.9f, 0.88f, 0.82f), ProtoArt.Fabric, new Vector2(2f, 2f), 0.05f);
            rugMat = ProtoArt.Mat(new Color(0.45f, 0.1f, 0.12f), ProtoArt.Fabric, new Vector2(8f, 6f), 0.02f);
            iron = ProtoArt.Mat(new Color(0.12f, 0.12f, 0.13f), null, null, 0.4f);
            groundMat = ProtoArt.Mat(new Color(0.18f, 0.22f, 0.16f), ProtoArt.Fabric, new Vector2(20f, 20f), 0.05f);
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

            if (sun != null)
            {
                sun.intensity = Mathf.Lerp(0.12f, 1.1f, day);
                sun.color = Color.Lerp(new Color(0.55f, 0.65f, 1f), new Color(1f, 0.95f, 0.85f), day);
            }
            RenderSettings.ambientLight = Color.Lerp(new Color(0.16f, 0.15f, 0.2f), new Color(0.42f, 0.4f, 0.37f), day);

            Color win = Color.Lerp(new Color(0.06f, 0.08f, 0.18f), new Color(0.85f, 0.92f, 1.1f), day);
            foreach (var wm in windowMats)
            {
                if (wm.HasProperty("_BaseColor")) wm.SetColor("_BaseColor", win);
                if (wm.HasProperty("_Color")) wm.SetColor("_Color", win);
            }

            bool lateNight = m < 330f;
            float lampScale = lateNight ? 0.45f : Mathf.Lerp(1f, 0.4f, day);
            foreach (var kv in lamps) if (kv.Key != null) kv.Key.scale = lampScale;
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
            return ProtoArt.Mat(new Color(0.95f, 0.92f, 0.88f), ProtoArt.StoneWall, new Vector2(Mathf.Max(0.5f, len / 3f), Mathf.Max(0.5f, h / 2f)), 0.1f);
        }

        Material PanelFor(float len)
        {
            return ProtoArt.Mat(new Color(0.55f, 0.42f, 0.33f), ProtoArt.WoodPlank, new Vector2(Mathf.Max(0.5f, len / 2f), 1f), 0.3f);
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
            Box("Ground", new Vector3(0f, -0.08f, -4f), new Vector3(50f, 0.1f, 40f), groundMat);
            Box("Path_Outside", new Vector3(0f, -0.02f, -14.5f), new Vector3(3f, 0.04f, 7f), flagstone);
            Label(new Vector3(0f, 1.2f, -13.4f), "外（授業へ）");
        }

        // ================= 談話室 =================
        void BuildLounge()
        {
            float t = 0.4f;
            Box("Lounge_Floor", new Vector3(0f, -0.05f, 0f), new Vector3(HalfW * 2f, 0.1f, HalfD * 2f), flagstone);
            Box("Floor_Wood", new Vector3(3.3f, 0.005f, -3.1f), new Vector3(4.6f, 0.02f, 3.6f), woodFloor);
            Box("RugBorder", new Vector3(0f, 0.008f, 0.4f), new Vector3(5.9f, 0.016f, 4.7f), fabricBrown);
            Box("Rug", new Vector3(0f, 0.012f, 0.4f), new Vector3(5.6f, 0.02f, 4.4f), rugMat);

            float wx = HalfW + t / 2f, wz = HalfD + t / 2f;
            var back = Wall(new Vector2(-wx - t / 2f, wz), new Vector2(wx + t / 2f, wz), 0f, RoomH, t);
            // 左右の壁：2階の個室へ通じる扉（高さ3m〜5.3m）
            var left = Wall(new Vector2(-wx, -wz), new Vector2(-wx, wz), 0f, RoomH, t, new Vector4(wz + 2.6f, wz + 3.6f, Floor2Y, Floor2Y + 2.3f));
            var right = Wall(new Vector2(wx, -wz), new Vector2(wx, wz), 0f, RoomH, t, new Vector4(wz + 2.6f, wz + 3.6f, Floor2Y, Floor2Y + 2.3f));
            // 手前の壁：中央に出入口
            var front = Wall(new Vector2(-wx - t / 2f, -wz), new Vector2(wx + t / 2f, -wz), 0f, RoomH, t, new Vector4(wx + t / 2f - 1f, wx + t / 2f + 1f, 0f, 3f));
            Box("Door_FrameL", new Vector3(-1.15f, 1.5f, -HalfD + 0.05f), new Vector3(0.3f, 3f, 0.5f), mortarDark);
            Box("Door_FrameR", new Vector3(1.15f, 1.5f, -HalfD + 0.05f), new Vector3(0.3f, 3f, 0.5f), mortarDark);
            front.upper.Add(Box("Door_Lintel", new Vector3(0f, 3.2f, -HalfD + 0.05f), new Vector3(2.6f, 0.4f, 0.5f), mortarDark).GetComponent<Renderer>());
            Label(new Vector3(0f, 3.7f, -HalfD), "談話室の出入口");

            Window(left, new Vector3(-HalfW, 0f, -3.1f), Vector3.right);
            Window(right, new Vector3(HalfW, 0f, -3.4f), Vector3.left);
            Window(back, new Vector3(-2.2f, 0f, HalfD), Vector3.back, 3.6f, 0.8f, 1.4f);
            Window(back, new Vector3(2.2f, 0f, HalfD), Vector3.back, 3.6f, 0.8f, 1.4f);

            // 天井の梁（上から見るときは消える）
            for (int i = -2; i <= 2; i++)
                ceilingRenderers.Add(Box("Beam", new Vector3(0f, RoomH - 0.2f, i * 2.1f), new Vector3(HalfW * 2f, 0.35f, 0.35f), woodDark).GetComponent<Renderer>());
            Chandelier(new Vector3(0f, 4.6f, 0.4f), 5f, 10f);

            Sconce(left, new Vector3(-HalfW + 0.15f, 3.9f, -0.5f), Vector3.right);
            Sconce(right, new Vector3(HalfW - 0.15f, 3.9f, -0.5f), Vector3.left);
            Sconce(front, new Vector3(-3.5f, 2.8f, -HalfD + 0.15f), Vector3.forward);
            Sconce(front, new Vector3(3.5f, 2.8f, -HalfD + 0.15f), Vector3.forward);

            // --- 奥：大きな暖炉 ---
            Box("Hearth", new Vector3(0f, 0.15f, 4.45f), new Vector3(3.4f, 0.3f, 1.1f), mortarDark);
            Box("Fireplace_PillarL", new Vector3(-1.4f, 1.45f, 4.65f), new Vector3(0.6f, 2.6f, 0.7f), mortarDark);
            Box("Fireplace_PillarR", new Vector3(1.4f, 1.45f, 4.65f), new Vector3(0.6f, 2.6f, 0.7f), mortarDark);
            Box("Fireplace_Mantel", new Vector3(0f, 2.85f, 4.55f), new Vector3(3.8f, 0.3f, 0.95f), woodDark);
            back.upper.Add(Box("Fireplace_Chimney", new Vector3(0f, 4.45f, 4.75f), new Vector3(2.9f, 3.1f, 0.5f), StoneFor(2.9f, 3.1f)).GetComponent<Renderer>());
            Box("Fireplace_Back", new Vector3(0f, 1.3f, 4.95f), new Vector3(2.3f, 2.3f, 0.1f), iron);
            Cyl("Log1", new Vector3(-0.2f, 0.42f, 4.55f), new Vector3(0.18f, 0.6f, 0.18f), Quaternion.Euler(0f, 0f, 90f), woodDark);
            Cyl("Log2", new Vector3(0.25f, 0.42f, 4.6f), new Vector3(0.18f, 0.55f, 0.18f), Quaternion.Euler(0f, 30f, 90f), woodDark);
            for (int i = 0; i < 5; i++)
            {
                var f = Box("Flame" + i, new Vector3(-0.5f + i * 0.25f, 0.65f, 4.55f), new Vector3(0.18f, 0.5f, 0.18f), i % 2 == 0 ? glowFire : glowEmber);
                f.AddComponent<FlameWobble>().seed = i * 1.7f;
            }
            var fire = Lamp(new Vector3(0f, 1.0f, 3.9f), 9f, 10f, true, 0.2f);
            fire.color = new Color(1f, 0.6f, 0.3f);
            blockers0.Add(new Rect(-1.85f, 3.85f, 3.7f, 1.2f));

            Bookshelf(new Vector3(-4.1f, 0f, 4.75f));
            Bookshelf(new Vector3(4.1f, 0f, 4.75f));

            Sofa("暖炉の前の椅子", "談話室", new Vector3(-1.3f, 0f, 2.9f), 0f, 1.0f, 1, fabricGreen);
            Sofa("暖炉の前の椅子", "談話室", new Vector3(1.3f, 0f, 2.9f), 0f, 1.0f, 1, fabricGreen);

            Box("LowTable", new Vector3(0f, 0.22f, 0.5f), new Vector3(1.4f, 0.08f, 0.8f), woodMid);
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0) ? -0.6f : 0.6f, z = (i < 2) ? 0.2f : 0.8f;
                Box("LowTable_Leg", new Vector3(x, 0.1f, z), new Vector3(0.08f, 0.2f, 0.08f), woodDark);
            }
            blockers0.Add(new Rect(-0.75f, 0.05f, 1.5f, 0.9f));
            Sofa("ソファ", "談話室", new Vector3(0f, 0f, -1.0f), 0f, 2.4f, 3, fabricRed);
            Sofa("ソファ", "談話室", new Vector3(-1.95f, 0f, 0.5f), 90f, 2.4f, 3, fabricRed);
            Sofa("ソファ", "談話室", new Vector3(1.95f, 0f, 0.5f), -90f, 2.4f, 3, fabricRed);

            Box("BigTable_Top", new Vector3(3.2f, 0.75f, -3.0f), new Vector3(2.4f, 0.08f, 1.0f), woodMid);
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0) ? 2.1f : 4.3f, z = (i < 2) ? -3.4f : -2.6f;
                Box("BigTable_Leg", new Vector3(x, 0.37f, z), new Vector3(0.1f, 0.74f, 0.1f), woodDark);
            }
            Box("Candle", new Vector3(3.2f, 0.9f, -3.0f), new Vector3(0.06f, 0.22f, 0.06f), glowLamp);
            blockers0.Add(new Rect(2.0f, -3.5f, 2.4f, 1.0f));
            for (int i = 0; i < 3; i++)
            {
                float x = 2.4f + i * 0.8f;
                Chair("大きなテーブル", "談話室", new Vector3(x, 0f, -2.2f), 180f);
                Chair("大きなテーブル", "談話室", new Vector3(x, 0f, -3.8f), 0f);
            }
            Bench(new Vector3(-5.6f, 0f, -3.1f), 90f);

            BuildStairs(-1);
            BuildStairs(1);

            pois.Add(MakePoi("暖炉にあたっている", new Vector3(0f, 0.8f, 4.6f), new Vector3(-0.5f, 0f, 3.55f), new Vector3(0.5f, 0f, 3.55f), new Vector3(0f, 0f, 3.05f)));
            pois.Add(MakePoi("本棚の本を眺めている", new Vector3(-4.1f, 1.5f, 5f), new Vector3(-4.6f, 0f, 3.9f), new Vector3(-3.6f, 0f, 3.9f)));
            pois.Add(MakePoi("本棚の本を眺めている", new Vector3(4.1f, 1.5f, 5f), new Vector3(3.6f, 0f, 3.9f), new Vector3(4.6f, 0f, 3.9f)));
            pois.Add(MakePoi("窓の外を眺めている", new Vector3(6f, 2f, -3.4f), new Vector3(5.2f, 0f, -3.4f)));
            // 魔法の練習場所（床の魔法陣）。趣味が「魔法の練習」のキャラだけが使う
            Cyl("MagicCircle", new Vector3(-3.0f, 0.015f, -3.6f), new Vector3(1.8f, 0.004f, 1.8f), Quaternion.identity, ProtoArt.Glow(new Color(0.4f, 0.55f, 1f), 0.6f));
            var magic = MakePoi("魔法の練習をしている", new Vector3(-3.0f, 1.2f, -3.6f), new Vector3(-3.6f, 0f, -3.2f), new Vector3(-2.4f, 0f, -4.0f));
            magic.hobby = (int)SimCharacter.HobbyKind.Magic;
            pois.Add(magic);

            AddArea("談話室", new Rect(-HalfW, -HalfD, HalfW * 2f, HalfD * 2f), 0);
        }

        /// <summary>談話室の左右の階段（2階の個室へ）と、上の踊り場。side：-1 左 / 1 右</summary>
        void BuildStairs(int side)
        {
            float x = 5.3f * side;
            int steps = 15;
            float z0 = -1.5f, z1 = 2.4f;
            float run = (z1 - z0) / steps, rise = Floor2Y / steps;
            for (int i = 0; i < steps; i++)
            {
                float top = (i + 1) * rise;
                Box("Stair_Step", new Vector3(x, top / 2f, z0 + (i + 0.5f) * run), new Vector3(1.2f, top, run), i % 2 == 0 ? woodDark : woodMid);
            }
            // 手すり（斜め）
            float railX = x - 0.62f * side;
            float slope = Mathf.Atan2(Floor2Y, z1 - z0) * Mathf.Rad2Deg;
            float lenRail = Mathf.Sqrt(Floor2Y * Floor2Y + (z1 - z0) * (z1 - z0));
            BoxRot("Stair_Rail", new Vector3(railX, Floor2Y / 2f + 0.9f, (z0 + z1) / 2f), new Vector3(0.08f, 0.08f, lenRail), Quaternion.Euler(-slope, 0f, 0f), woodDark);
            Box("Stair_Post", new Vector3(railX, 0.5f, z0), new Vector3(0.12f, 1f, 0.12f), woodDark);
            // 踊り場（高さ3m）
            float lx = 5.3f * side;
            Box("Landing", new Vector3(lx, Floor2Y - 0.1f, 3.1f), new Vector3(1.4f, 0.2f, 1.4f), woodDark);
            Box("Landing_RailSide", new Vector3(lx - 0.7f * side, Floor2Y + 0.5f, 3.1f), new Vector3(0.08f, 1f, 1.4f), woodDark);
            Box("Landing_RailBack", new Vector3(lx, Floor2Y + 0.5f, 3.8f), new Vector3(1.4f, 1f, 0.08f), woodDark);
            Box("Landing_PostA", new Vector3(lx - 0.65f * side, Floor2Y / 2f, 2.45f), new Vector3(0.15f, Floor2Y, 0.15f), woodDark);
            Box("Landing_PostB", new Vector3(lx - 0.65f * side, Floor2Y / 2f, 3.75f), new Vector3(0.15f, Floor2Y, 0.15f), woodDark);
            blockers0.Add(new Rect(x - 0.6f, z0, 1.2f, z1 - z0));
            blockers0.Add(new Rect(lx - 0.65f * side - 0.1f, 2.35f, 0.2f, 0.2f));
            blockers0.Add(new Rect(lx - 0.65f * side - 0.1f, 3.65f, 0.2f, 0.2f));
            Label(new Vector3(x, 1.4f, z0 - 0.3f), side < 0 ? "階段（2階・左の個室へ）" : "階段（2階・右の個室へ）");
        }

        // ================= 1階手前：食堂・廊下・浴場 =================
        void BuildFrontBand()
        {
            float zc = (FrontZ + (-HalfD)) / 2f;
            float depth = -HalfD - FrontZ; // 6m
            Box("Floor_Dining", new Vector3(-7.5f, -0.05f, zc), new Vector3(11f, 0.1f, depth), woodFloor);
            Box("Floor_Corridor", new Vector3(0f, -0.05f, zc), new Vector3(4f, 0.1f, depth), flagstone);
            Box("Floor_Bath", new Vector3(7.5f, -0.05f, zc), new Vector3(11f, 0.1f, depth), mortarDark);

            float t = 0.4f, it = 0.2f;
            // 後ろの壁（談話室の左右にはみ出す部分）
            Wall(new Vector2(-13.4f, -5.2f), new Vector2(-6.4f, -5.2f), 0f, BandH, t);
            Wall(new Vector2(6.4f, -5.2f), new Vector2(13.4f, -5.2f), 0f, BandH, t);
            // 外壁
            var dl = Wall(new Vector2(-13.2f, -11.4f), new Vector2(-13.2f, -5f), 0f, BandH, t);
            var br = Wall(new Vector2(13.2f, -11.4f), new Vector2(13.2f, -5f), 0f, BandH, t);
            var fw = Wall(new Vector2(-13.4f, -11.2f), new Vector2(13.4f, -11.2f), 0f, BandH, t, new Vector4(12.4f, 14.4f, 0f, 2.8f));
            // 食堂｜廊下｜浴場 の仕切り（扉つき）
            Wall(new Vector2(-2f, -11f), new Vector2(-2f, -5.4f), 0f, BandH, it, new Vector4(2.4f, 3.6f, 0f, 2.4f));
            Wall(new Vector2(2f, -11f), new Vector2(2f, -5.4f), 0f, BandH, it, new Vector4(2.4f, 3.6f, 0f, 2.4f));

            Window(fw, new Vector3(-9.5f, 0f, FrontZ), Vector3.forward, 1.0f, 1.0f, 1.6f);
            Window(fw, new Vector3(-5f, 0f, FrontZ), Vector3.forward, 1.0f, 1.0f, 1.6f);
            Window(dl, new Vector3(-13f, 0f, -8f), Vector3.right, 1.0f, 1.0f, 1.6f);
            Window(br, new Vector3(13f, 0f, -8f), Vector3.left, 1.8f, 1.6f, 1.0f);
            Window(fw, new Vector3(7.5f, 0f, FrontZ), Vector3.forward, 1.8f, 1.6f, 1.0f);

            // --- 廊下 ---
            Box("Corridor_Bench", new Vector3(1.6f, 0.22f, -6.8f), new Vector3(0.45f, 0.44f, 1.4f), woodDark);
            blockers0.Add(new Rect(1.35f, -7.5f, 0.5f, 1.4f));
            Box("NoticeBoard", new Vector3(-1.85f, 1.6f, -6.3f), new Vector3(0.05f, 0.9f, 1.2f), woodMid);
            Lamp(new Vector3(0f, 2.9f, -8f), 2.5f, 6f);
            Label(new Vector3(0f, 2.9f, -9.2f), "廊下");
            AddArea("廊下", new Rect(-2f, FrontZ, 4f, depth), 0);
            AddArea("外", new Rect(-1.5f, -14f, 3f, 3f), 0);

            // --- 食堂 ---
            Label(new Vector3(-7.5f, 3.0f, -5.6f), "食堂");
            AddArea("食堂", new Rect(-13f, FrontZ, 11f, depth), 0);
            Box("Counter", new Vector3(-12.5f, 0.5f, -8.25f), new Vector3(0.8f, 1.0f, 3.5f), woodDark);
            Box("Counter_Top", new Vector3(-12.5f, 1.02f, -8.25f), new Vector3(0.9f, 0.05f, 3.6f), woodMid);
            for (int i = 0; i < 4; i++) Box("Food", new Vector3(-12.45f, 1.12f, -9.6f + i * 0.9f), new Vector3(0.35f, 0.12f, 0.35f), foodMat);
            blockers0.Add(new Rect(-12.95f, -10.05f, 0.9f, 3.6f));
            pois.Add(new Poi
            {
                label = "料理を受け取っている",
                lookAt = new Vector3(-12.5f, 1f, -8.25f),
                slots = new[] { new Vector3(-11.6f, 0f, -9.4f), new Vector3(-11.6f, 0f, -8.25f), new Vector3(-11.6f, 0f, -7.1f) },
                occupants = new SimCharacter[3],
                counter = true
            });
            // 縦長の机3つ（机と机の間は通路）
            DiningTable(new Vector3(-10.05f, 0f, -8f));
            DiningTable(new Vector3(-7.15f, 0f, -8f));
            DiningTable(new Vector3(-4.25f, 0f, -8f));
            Chandelier(new Vector3(-9.5f, 2.9f, -8f), 3.5f, 7f);
            Chandelier(new Vector3(-5f, 2.9f, -8f), 3.5f, 7f);

            // --- 浴場（1つ） ---
            Label(new Vector3(7.5f, 3.0f, -5.6f), "浴場");
            AddArea("浴場", new Rect(2f, FrontZ, 11f, depth), 0);
            float tx0 = 6f, tx1 = 12.5f, tz0 = -10.3f, tz1 = -5.7f;
            float rim = 0.3f, rimH = 0.5f;
            Box("Tub_RimL", new Vector3(tx0 + rim / 2f, rimH / 2f, (tz0 + tz1) / 2f), new Vector3(rim, rimH, tz1 - tz0), mortarDark);
            Box("Tub_RimR", new Vector3(tx1 - rim / 2f, rimH / 2f, (tz0 + tz1) / 2f), new Vector3(rim, rimH, tz1 - tz0), mortarDark);
            Box("Tub_RimF", new Vector3((tx0 + tx1) / 2f, rimH / 2f, tz0 + rim / 2f), new Vector3(tx1 - tx0, rimH, rim), mortarDark);
            Box("Tub_RimB", new Vector3((tx0 + tx1) / 2f, rimH / 2f, tz1 - rim / 2f), new Vector3(tx1 - tx0, rimH, rim), mortarDark);
            Box("Tub_Water", new Vector3((tx0 + tx1) / 2f, 0.38f, (tz0 + tz1) / 2f), new Vector3(tx1 - tx0 - rim * 2f, 0.04f, tz1 - tz0 - rim * 2f), waterMat);
            blockers0.Add(Rect.MinMaxRect(tx0, tz0, tx1, tz1));
            float[] bx = { 7.2f, 9.2f, 11.2f };
            float[] bz = { -9.2f, -8f, -6.8f };
            foreach (var x in bx)
                foreach (var z in bz)
                    seats.Add(new Seat { kind = SeatKind.Bath, label = "湯船", area = "浴場", position = new Vector3(x, 0f, z), forward = Vector3.left, approach = new Vector3(5.6f, 0f, z) });
            for (int i = 0; i < 4; i++) Box("Locker", new Vector3(3.0f + i * 0.7f, 0.9f, -5.6f), new Vector3(0.6f, 1.8f, 0.4f), woodMid);
            blockers0.Add(new Rect(2.65f, -5.85f, 2.8f, 0.5f));
            Lamp(new Vector3(5f, 2.9f, -8f), 2.5f, 7f);
            Lamp(new Vector3(9.5f, 2.9f, -8f), 2.5f, 7f);
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
        /// <summary>side：-1 左 / 1 右。個室は談話室の外側に5部屋。</summary>
        void BuildWing(int side)
        {
            float s = side;
            float inner = 6.4f, corr = 8.0f, outer = 13f;
            // 土台（1階の部分）と2階の床
            Box("Wing_Base", new Vector3(s * (inner + outer) / 2f, (Floor2Y - 0.05f) / 2f, 0f), new Vector3(outer - inner, Floor2Y - 0.05f, HalfD * 2f), mortarDark);
            Box("Wing_Floor", new Vector3(s * (inner + outer) / 2f, Floor2Y - 0.025f, 0f), new Vector3(outer - inner, 0.05f, HalfD * 2f), woodFloor);

            float t = 0.4f, it = 0.2f;
            var outerW = Wall(new Vector2(s * (outer + 0.2f), -HalfD - 0.4f), new Vector2(s * (outer + 0.2f), HalfD + 0.4f), Floor2Y, WingTop, t);
            Wall(new Vector2(s * inner, HalfD + 0.2f), new Vector2(s * (outer + 0.4f), HalfD + 0.2f), Floor2Y, WingTop, t);
            Wall(new Vector2(s * inner, -HalfD - 0.2f), new Vector2(s * (outer + 0.4f), -HalfD - 0.2f), Floor2Y, WingTop, t);

            // 廊下の壁（各部屋の扉つき）
            var gaps = new List<Vector4>();
            for (int i = 0; i < 5; i++)
            {
                float cz = -4f + i * 2f;
                float along = cz + HalfD; // a.z = -HalfD から測る
                gaps.Add(new Vector4(along - 0.5f, along + 0.5f, Floor2Y, Floor2Y + 2.2f));
            }
            Wall(new Vector2(s * corr, -HalfD), new Vector2(s * corr, HalfD), Floor2Y, WingTop, it, gaps.ToArray());
            // 部屋の仕切り
            for (int i = 1; i < 5; i++)
            {
                float z = -HalfD + i * 2f;
                Wall(new Vector2(s * corr, z), new Vector2(s * outer, z), Floor2Y, WingTop, it);
            }
            Lamp(new Vector3(s * (inner + corr) / 2f, Floor2Y + 2.2f, 0f), 2f, 6f);
            Label(new Vector3(s * (inner + corr) / 2f, Floor2Y + 2.4f, -4.4f), side < 0 ? "2階・左の廊下" : "2階・右の廊下");
            AddArea(side < 0 ? "2階・左の廊下" : "2階・右の廊下", Rect.MinMaxRect(Mathf.Min(s * inner, s * corr), -HalfD, Mathf.Max(s * inner, s * corr), HalfD), 1);

            for (int i = 0; i < 5; i++)
            {
                float cz = -4f + i * 2f;
                string name = (side < 0 ? "左" : "右") + (i + 1);
                var room = new RoomInfo { name = name };
                Window(outerW, new Vector3(s * outer, Floor2Y, cz), new Vector3(-s, 0f, 0f), 1.0f, 0.7f, 1.1f);
                // ベッド（頭は外壁側）
                float bedX0 = s * (outer - 0.1f), bedX1 = s * (outer - 2.1f);
                float bcx = (bedX0 + bedX1) / 2f;
                Box("Bed_Frame", new Vector3(bcx, Floor2Y + 0.2f, cz - 0.52f), new Vector3(2.0f, 0.4f, 0.85f), woodDark);
                Box("Bed_Mattress", new Vector3(bcx, Floor2Y + 0.47f, cz - 0.52f), new Vector3(1.9f, 0.14f, 0.8f), fabricWhite);
                Box("Bed_Blanket", new Vector3(bcx - s * 0.3f, Floor2Y + 0.55f, cz - 0.52f), new Vector3(1.3f, 0.04f, 0.82f), fabricRed);
                Box("Bed_Pillow", new Vector3(s * (outer - 0.4f), Floor2Y + 0.58f, cz - 0.52f), new Vector3(0.4f, 0.1f, 0.55f), fabricWhite);
                Box("Bed_Head", new Vector3(s * (outer - 0.1f), Floor2Y + 0.55f, cz - 0.52f), new Vector3(0.08f, 1.1f, 0.85f), woodDark);
                blockers1.Add(Rect.MinMaxRect(Mathf.Min(bedX0, bedX1), cz - 0.95f, Mathf.Max(bedX0, bedX1), cz - 0.1f));
                room.bed = new Seat
                {
                    kind = SeatKind.Bed,
                    label = "ベッド",
                    area = name,
                    position = new Vector3(bcx, Floor2Y, cz - 0.52f),
                    forward = new Vector3(s, 0f, 0f),
                    approach = new Vector3(s * (outer - 2.6f), Floor2Y, cz - 0.5f)
                };
                seats.Add(room.bed);
                // 机
                float deskX = s * (outer - 0.4f);
                Box("Desk", new Vector3(deskX, Floor2Y + 0.73f, cz + 0.52f), new Vector3(0.6f, 0.05f, 0.8f), woodMid);
                Box("Desk_Body", new Vector3(deskX, Floor2Y + 0.36f, cz + 0.52f), new Vector3(0.55f, 0.72f, 0.75f), woodDark);
                Box("Desk_Book", new Vector3(deskX, Floor2Y + 0.8f, cz + 0.4f), new Vector3(0.25f, 0.06f, 0.18f), fabricGreen);
                var lampGlow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                lampGlow.transform.SetParent(root, false);
                lampGlow.transform.position = new Vector3(deskX, Floor2Y + 0.95f, cz + 0.8f);
                lampGlow.transform.localScale = Vector3.one * 0.12f;
                lampGlow.GetComponent<Renderer>().sharedMaterial = glowLamp;
                Destroy(lampGlow.GetComponent<Collider>());
                blockers1.Add(Rect.MinMaxRect(Mathf.Min(s * (outer - 0.7f), s * outer), cz + 0.12f, Mathf.Max(s * (outer - 0.7f), s * outer), cz + 0.92f));
                room.desk = new Poi
                {
                    label = "机で勉強している",
                    lookAt = new Vector3(deskX, Floor2Y + 0.8f, cz + 0.52f),
                    slots = new[] { new Vector3(s * (outer - 1.15f), Floor2Y, cz + 0.52f) },
                    occupants = new SimCharacter[1]
                };
                pois.Add(room.desk);
                Lamp(new Vector3(s * (outer - 2.5f), Floor2Y + 2.3f, cz), 1.5f, 4f, false, 0.03f);
                room.labelPos = new Vector3(s * (outer - 2.4f), Floor2Y + 2.0f, cz);
                room.rect = Rect.MinMaxRect(Mathf.Min(s * corr, s * outer), cz - 1f, Mathf.Max(s * corr, s * outer), cz + 1f);
                AddArea(name + "の部屋", room.rect, 1);
                rooms.Add(room);
            }
        }

        // ================= 家具 =================
        Poi MakePoi(string label, Vector3 lookAt, params Vector3[] slots)
        {
            return new Poi { label = label, lookAt = lookAt, slots = slots, occupants = new SimCharacter[slots.Length] };
        }

        void Bookshelf(Vector3 c)
        {
            var t = new GameObject("Bookshelf").transform;
            t.SetParent(root, false);
            t.localPosition = c;
            float w = 3f, h = 3.2f, d = 0.45f;
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
            for (int s = 0; s < 5; s++)
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
            blockers0.Add(new Rect(c.x - w / 2f - 0.1f, c.z - 0.35f, w + 0.2f, 0.7f));
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
            if (FloorOf(floorY) == 0) blockers0.Add(r); else blockers1.Add(r);
        }

        // ================= 歩ける場所 =================
        void BuildNav()
        {
            nav0 = new GridNav(new Vector2(-14f, -14f), new Vector2(14f, 6f), 0.25f, 0f);
            // 部屋の中（壁から少し離す）
            nav0.Open(Rect.MinMaxRect(-5.7f, -4.7f, 5.7f, 4.7f));        // 談話室
            nav0.Open(Rect.MinMaxRect(-1.65f, -10.75f, 1.65f, -5.65f));  // 廊下
            nav0.Open(Rect.MinMaxRect(-12.75f, -10.75f, -2.35f, -5.65f)); // 食堂
            nav0.Open(Rect.MinMaxRect(2.35f, -10.75f, 12.75f, -5.65f));  // 浴場
            nav0.Open(Rect.MinMaxRect(-1.2f, -13.8f, 1.2f, -11.45f));    // 外の小道
            // 出入口
            nav0.Open(Rect.MinMaxRect(-0.75f, -5.75f, 0.75f, -4.6f));
            nav0.Open(Rect.MinMaxRect(-0.75f, -11.55f, 0.75f, -10.6f));
            nav0.Open(Rect.MinMaxRect(-2.45f, -8.35f, -1.55f, -7.65f));
            nav0.Open(Rect.MinMaxRect(1.55f, -8.35f, 2.45f, -7.65f));
            float inflate = 0.15f;
            foreach (var r in blockers0)
                nav0.Block(Rect.MinMaxRect(r.xMin - inflate, r.yMin - inflate, r.xMax + inflate, r.yMax + inflate));

            nav1 = new GridNav(new Vector2(-14f, -6f), new Vector2(14f, 6f), 0.25f, Floor2Y);
            for (int side = -1; side <= 1; side += 2)
            {
                float s = side;
                nav1.Open(Rect.MinMaxRect(Mathf.Min(s * 6.55f, s * 7.85f), -4.75f, Mathf.Max(s * 6.55f, s * 7.85f), 4.75f)); // 廊下
                for (int i = 0; i < 5; i++)
                {
                    float cz = -4f + i * 2f;
                    nav1.Open(Rect.MinMaxRect(Mathf.Min(s * 8.25f, s * 12.75f), cz - 0.75f, Mathf.Max(s * 8.25f, s * 12.75f), cz + 0.75f)); // 部屋
                    nav1.Open(Rect.MinMaxRect(Mathf.Min(s * 7.7f, s * 8.4f), cz - 0.35f, Mathf.Max(s * 7.7f, s * 8.4f), cz + 0.35f));    // 部屋の扉
                }
                nav1.Open(Rect.MinMaxRect(Mathf.Min(s * 4.85f, s * 5.75f), 2.6f, Mathf.Max(s * 4.85f, s * 5.75f), 3.6f));  // 踊り場
                nav1.Open(Rect.MinMaxRect(Mathf.Min(s * 5.6f, s * 6.7f), 2.75f, Mathf.Max(s * 5.6f, s * 6.7f), 3.45f));   // 踊り場→廊下の扉
            }
            foreach (var r in blockers1)
                nav1.Block(Rect.MinMaxRect(r.xMin - inflate, r.yMin - inflate, r.xMax + inflate, r.yMax + inflate));
        }

        // ================= 壁の表示 =================
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
