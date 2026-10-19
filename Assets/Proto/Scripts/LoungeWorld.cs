using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>座れる場所1つ分。</summary>
    public class Seat
    {
        public Vector3 position;  // 座ったときの立ち位置（床の高さ）
        public Vector3 forward;   // 座ったときに向く方向
        public Vector3 approach;  // 座る前に立つ場所
        public string label;
        public SimCharacter occupant;
    }

    /// <summary>暇つぶしの場所（暖炉・本棚・窓）。</summary>
    public class Poi
    {
        public string label;
        public Vector3 lookAt;
        public Vector3[] slots;
        public SimCharacter[] occupants;

        public int FreeSlot()
        {
            for (int i = 0; i < occupants.Length; i++) if (occupants[i] == null) return i;
            return -1;
        }
    }

    /// <summary>
    /// 談話室を丸ごと組み立てて、試作を動かす中心の部品。
    /// 部屋：横12m × 奥行き10m × 天井5m。手前(z=-5)に出入口、奥(z=+5)に暖炉と本棚、左右に階段の予定地。
    /// </summary>
    public class LoungeWorld : MonoBehaviour
    {
        public static LoungeWorld Instance { get; private set; }

        public const float HalfW = 6f, HalfD = 5f, RoomH = 5f;

        public GridNav nav;
        public Relationships rel;
        public readonly List<SimCharacter> characters = new List<SimCharacter>();
        public readonly List<Seat> seats = new List<Seat>();
        public readonly List<Poi> pois = new List<Poi>();
        public readonly List<KeyValuePair<Vector3, string>> worldLabels = new List<KeyValuePair<Vector3, string>>();
        public readonly List<string> log = new List<string>();
        public float simTime;
        public Camera mainCamera;

        class WallGroup
        {
            public List<Renderer> renderers = new List<Renderer>();
            public Vector3 point;
            public Vector3 inward;
            public bool isCeiling;
        }
        readonly List<WallGroup> wallGroups = new List<WallGroup>();
        readonly List<Rect> blockers = new List<Rect>();

        Transform root;
        Material stoneWall, flagstone, woodDark, woodMid, woodFloor, fabricRed, fabricGreen, fabricBrown, iron, glowWindow, glowLamp, glowFire, glowEmber, ringMat, rugMat, mortarDark;

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            root = new GameObject("Lounge").transform;
            CreateMaterials();
            SetupLighting();
            BuildRoom();
            BuildFurniture();
            BuildNav();
            SpawnCharacters();
            SetupCamera();
            gameObject.AddComponent<SimHud>();
            Log("談話室の試作を開始しました");
        }

        void Update()
        {
            simTime += Time.deltaTime;
            UpdateCutaway();
        }

        public void Log(string message)
        {
            int t = Mathf.FloorToInt(simTime);
            log.Add(string.Format("[{0:00}:{1:00}] {2}", t / 60, t % 60, message));
            if (log.Count > 60) log.RemoveAt(0);
        }

        public void ReportTalk(SimCharacter a, SimCharacter b, TalkResult result)
        {
            Vector2Int stage = rel.Apply(a.id, b.id, result, simTime);
            string word = result == TalkResult.Fun ? "楽しく話した ♪" : (result == TalkResult.Normal ? "ふつうに話した" : "少し気まずかった");
            Log(a.displayName + "と" + b.displayName + "：" + word + "（好感度 " + Mathf.RoundToInt(rel.Get(a.id, b.id)) + "）");
            if (stage.y > stage.x)
                Log("★ " + a.displayName + "と" + b.displayName + "が「" + Relationships.StageName(stage.y) + "」になった！");
            else if (stage.y < stage.x)
                Log(a.displayName + "と" + b.displayName + "の関係が「" + Relationships.StageName(stage.y) + "」に戻った");
        }

        // ================= 材質と光 =================
        void CreateMaterials()
        {
            stoneWall = ProtoArt.Mat(new Color(0.95f, 0.92f, 0.88f), ProtoArt.StoneWall, new Vector2(4f, 2.5f), 0.1f);
            flagstone = ProtoArt.Mat(new Color(0.9f, 0.88f, 0.85f), ProtoArt.Flagstone, new Vector2(5f, 4f), 0.2f);
            mortarDark = ProtoArt.Mat(new Color(0.55f, 0.52f, 0.5f), ProtoArt.StoneWall, new Vector2(1.5f, 1.5f), 0.1f);
            woodDark = ProtoArt.Mat(new Color(0.55f, 0.42f, 0.33f), ProtoArt.WoodFine, new Vector2(1f, 1f), 0.3f);
            woodMid = ProtoArt.Mat(new Color(0.85f, 0.68f, 0.52f), ProtoArt.WoodFine, new Vector2(1f, 1f), 0.3f);
            woodFloor = ProtoArt.Mat(new Color(0.8f, 0.65f, 0.5f), ProtoArt.WoodPlank, new Vector2(3f, 3f), 0.25f);
            fabricRed = ProtoArt.Mat(new Color(0.55f, 0.12f, 0.12f), ProtoArt.Fabric, new Vector2(3f, 3f), 0.05f);
            fabricGreen = ProtoArt.Mat(new Color(0.2f, 0.33f, 0.22f), ProtoArt.Fabric, new Vector2(3f, 3f), 0.05f);
            fabricBrown = ProtoArt.Mat(new Color(0.42f, 0.27f, 0.17f), ProtoArt.Fabric, new Vector2(2f, 2f), 0.1f);
            rugMat = ProtoArt.Mat(new Color(0.45f, 0.1f, 0.12f), ProtoArt.Fabric, new Vector2(8f, 6f), 0.02f);
            iron = ProtoArt.Mat(new Color(0.12f, 0.12f, 0.13f), null, null, 0.4f);
            glowWindow = ProtoArt.Glow(new Color(0.45f, 0.6f, 0.9f), 0.9f);
            glowLamp = ProtoArt.Glow(new Color(1f, 0.8f, 0.5f), 2.5f);
            glowFire = ProtoArt.Glow(new Color(1f, 0.55f, 0.15f), 3f);
            glowEmber = ProtoArt.Glow(new Color(1f, 0.3f, 0.05f), 2f);
            ringMat = ProtoArt.Glow(new Color(1f, 0.9f, 0.2f), 1.5f);
        }

        void SetupLighting()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.30f, 0.27f, 0.25f);
            RenderSettings.skybox = null;

            var moon = new GameObject("Moonlight").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.6f, 0.7f, 1f);
            moon.intensity = 0.35f;
            moon.shadows = LightShadows.None;
            moon.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            moon.transform.SetParent(root, true);
        }

        Light PointLight(Vector3 pos, Color c, float intensity, float range, bool shadows)
        {
            var l = new GameObject("Light").AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.intensity = intensity;
            l.range = range;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            l.transform.position = pos;
            l.transform.SetParent(root, true);
            return l;
        }

        // ================= 部屋 =================
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

        WallGroup NewGroup(Vector3 point, Vector3 inward)
        {
            var g = new WallGroup { point = point, inward = inward };
            wallGroups.Add(g);
            return g;
        }

        void Add(WallGroup g, GameObject go) { g.renderers.Add(go.GetComponent<Renderer>()); }

        void BuildRoom()
        {
            float t = 0.4f;
            // 床（石畳）
            var floor = Box("Floor_Stone", new Vector3(0f, -0.05f, 0f), new Vector3(HalfW * 2f, 0.1f, HalfD * 2f), flagstone);
            floor.GetComponent<Renderer>().sharedMaterial = flagstone;
            // 大テーブルのまわりは木の床
            Box("Floor_Wood", new Vector3(3.3f, 0.005f, -3.1f), new Vector3(4.6f, 0.02f, 3.6f), woodFloor);
            // ソファまわりの大きなじゅうたん
            Box("Rug", new Vector3(0f, 0.012f, 0.4f), new Vector3(5.6f, 0.02f, 4.4f), rugMat);
            Box("RugBorder", new Vector3(0f, 0.008f, 0.4f), new Vector3(5.9f, 0.016f, 4.7f), fabricBrown);

            // 壁（石積み）＋腰板（木）
            var back = NewGroup(new Vector3(0f, 0f, HalfD), Vector3.back);
            Add(back, Box("Wall_Back", new Vector3(0f, RoomH / 2f, HalfD + t / 2f), new Vector3(HalfW * 2f + t * 2f, RoomH, t), stoneWall));
            Add(back, Box("Wainscot_Back", new Vector3(0f, 0.6f, HalfD - 0.03f), new Vector3(HalfW * 2f, 1.2f, 0.06f), woodDark));

            var left = NewGroup(new Vector3(-HalfW, 0f, 0f), Vector3.right);
            Add(left, Box("Wall_Left", new Vector3(-HalfW - t / 2f, RoomH / 2f, 0f), new Vector3(t, RoomH, HalfD * 2f + t * 2f), stoneWall));
            Add(left, Box("Wainscot_Left", new Vector3(-HalfW + 0.03f, 0.6f, 0f), new Vector3(0.06f, 1.2f, HalfD * 2f), woodDark));

            var right = NewGroup(new Vector3(HalfW, 0f, 0f), Vector3.left);
            Add(right, Box("Wall_Right", new Vector3(HalfW + t / 2f, RoomH / 2f, 0f), new Vector3(t, RoomH, HalfD * 2f + t * 2f), stoneWall));
            Add(right, Box("Wainscot_Right", new Vector3(HalfW - 0.03f, 0.6f, 0f), new Vector3(0.06f, 1.2f, HalfD * 2f), woodDark));

            // 手前の壁（中央に出入口）
            var front = NewGroup(new Vector3(0f, 0f, -HalfD), Vector3.forward);
            float doorHalf = 1.0f, doorH = 3.0f;
            float segW = HalfW + t - doorHalf;
            Add(front, Box("Wall_Front_L", new Vector3(-(doorHalf + segW / 2f), RoomH / 2f, -HalfD - t / 2f), new Vector3(segW, RoomH, t), stoneWall));
            Add(front, Box("Wall_Front_R", new Vector3(doorHalf + segW / 2f, RoomH / 2f, -HalfD - t / 2f), new Vector3(segW, RoomH, t), stoneWall));
            Add(front, Box("Wall_Front_Top", new Vector3(0f, doorH + (RoomH - doorH) / 2f, -HalfD - t / 2f), new Vector3(doorHalf * 2f, RoomH - doorH, t), stoneWall));
            Add(front, Box("Wainscot_Front_L", new Vector3(-(doorHalf + (HalfW - doorHalf) / 2f), 0.6f, -HalfD + 0.03f), new Vector3(HalfW - doorHalf, 1.2f, 0.06f), woodDark));
            Add(front, Box("Wainscot_Front_R", new Vector3(doorHalf + (HalfW - doorHalf) / 2f, 0.6f, -HalfD + 0.03f), new Vector3(HalfW - doorHalf, 1.2f, 0.06f), woodDark));
            // 出入口のアーチ（石の枠）と、奥の暗い廊下
            Add(front, Box("Door_FrameL", new Vector3(-doorHalf - 0.15f, doorH / 2f, -HalfD + 0.05f), new Vector3(0.3f, doorH, 0.5f), mortarDark));
            Add(front, Box("Door_FrameR", new Vector3(doorHalf + 0.15f, doorH / 2f, -HalfD + 0.05f), new Vector3(0.3f, doorH, 0.5f), mortarDark));
            Add(front, Box("Door_Lintel", new Vector3(0f, doorH + 0.2f, -HalfD + 0.05f), new Vector3(doorHalf * 2f + 0.6f, 0.4f, 0.5f), mortarDark));
            Box("Corridor_Floor", new Vector3(0f, -0.05f, -HalfD - 1.5f), new Vector3(2f, 0.1f, 2.6f), flagstone);
            Add(front, Box("Corridor_Back", new Vector3(0f, 1.5f, -HalfD - 2.8f), new Vector3(2.4f, 3f, 0.2f), stoneWall));
            worldLabels.Add(new KeyValuePair<Vector3, string>(new Vector3(0f, 3.6f, -HalfD), "出入口"));

            // 窓（縦長のアーチ窓）
            Window(left, new Vector3(-HalfW + 0.02f, 0f, -3.1f), Vector3.right);
            Window(left, new Vector3(-HalfW + 0.02f, 0f, 3.4f), Vector3.right);
            Window(right, new Vector3(HalfW - 0.02f, 0f, 3.4f), Vector3.left);
            Window(right, new Vector3(HalfW - 0.02f, 0f, -3.4f), Vector3.left);

            // 天井の太い木の梁
            var ceiling = NewGroup(new Vector3(0f, RoomH, 0f), Vector3.down);
            ceiling.isCeiling = true;
            for (int i = -2; i <= 2; i++)
                Add(ceiling, Box("Beam_" + i, new Vector3(0f, RoomH - 0.2f, i * 2.1f), new Vector3(HalfW * 2f, 0.35f, 0.35f), woodDark));
            Add(ceiling, Box("Beam_L", new Vector3(-HalfW + 0.2f, RoomH - 0.45f, 0f), new Vector3(0.3f, 0.3f, HalfD * 2f), woodDark));
            Add(ceiling, Box("Beam_R", new Vector3(HalfW - 0.2f, RoomH - 0.45f, 0f), new Vector3(0.3f, 0.3f, HalfD * 2f), woodDark));

            // 壁のランプ
            Sconce(left, new Vector3(-HalfW + 0.15f, 2.7f, -0.5f), Vector3.right);
            Sconce(right, new Vector3(HalfW - 0.15f, 2.7f, -0.5f), Vector3.left);
            Sconce(back, new Vector3(-2.2f, 3.2f, HalfD - 0.15f), Vector3.back);
            Sconce(back, new Vector3(2.2f, 3.2f, HalfD - 0.15f), Vector3.back);
            Sconce(front, new Vector3(-3.5f, 2.8f, -HalfD + 0.15f), Vector3.forward);
            Sconce(front, new Vector3(3.5f, 2.8f, -HalfD + 0.15f), Vector3.forward);

            // 左右の階段の予定地（後で作る）
            StairPlaceholder(new Vector3(-5.3f, 0f, 0.5f));
            StairPlaceholder(new Vector3(5.3f, 0f, 0.5f));
        }

        void Window(WallGroup g, Vector3 basePos, Vector3 inward)
        {
            float yaw = Quaternion.LookRotation(inward).eulerAngles.y;
            Vector3 c = basePos + Vector3.up * 2.3f;
            var pane = Box("Window", c, new Vector3(1.1f, 2.2f, 0.04f), glowWindow, null, yaw);
            Add(g, pane);
            var arch = Cyl("WindowArch", c + Vector3.up * 1.1f, new Vector3(1.1f, 0.02f, 1.1f), Quaternion.LookRotation(inward) * Quaternion.Euler(90f, 0f, 0f), glowWindow);
            Add(g, arch);
            // 窓の桟（鉄）
            Add(g, Box("Mullion_V", c + inward * 0.03f + Vector3.up * 0.3f, new Vector3(0.06f, 2.8f, 0.05f), iron, null, yaw));
            Add(g, Box("Mullion_H1", c + inward * 0.03f + Vector3.up * 0.3f, new Vector3(1.1f, 0.05f, 0.05f), iron, null, yaw));
            Add(g, Box("Mullion_H2", c + inward * 0.03f - Vector3.up * 0.5f, new Vector3(1.1f, 0.05f, 0.05f), iron, null, yaw));
            Add(g, Box("Sill", basePos + Vector3.up * 1.15f + inward * 0.12f, new Vector3(1.4f, 0.1f, 0.3f), mortarDark, null, yaw));
        }

        void Sconce(WallGroup g, Vector3 pos, Vector3 inward)
        {
            float yaw = Quaternion.LookRotation(inward).eulerAngles.y;
            Add(g, Box("Sconce_Bracket", pos - Vector3.up * 0.15f, new Vector3(0.12f, 0.3f, 0.2f), iron, null, yaw));
            var flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flame.name = "Sconce_Light";
            flame.transform.SetParent(root, false);
            flame.transform.position = pos + inward * 0.12f + Vector3.up * 0.08f;
            flame.transform.localScale = Vector3.one * 0.16f;
            flame.GetComponent<Renderer>().sharedMaterial = glowLamp;
            Destroy(flame.GetComponent<Collider>());
            Add(g, flame);
            var l = PointLight(pos + inward * 0.4f, new Color(1f, 0.72f, 0.42f), 3.5f, 7f, false);
            l.gameObject.AddComponent<Flicker>().amount = 0.08f;
        }

        void StairPlaceholder(Vector3 center)
        {
            Vector3 size = new Vector3(1.4f, 0.02f, 4f);
            Box("Stair_Area", center + Vector3.up * 0.01f, size, woodDark);
            for (int i = 0; i < 2; i++)
            {
                float z = center.z + (i == 0 ? -2f : 2f);
                float x = center.x < 0 ? center.x + 0.7f : center.x - 0.7f;
                Box("Stair_Post", new Vector3(x, 0.5f, z), new Vector3(0.12f, 1f, 0.12f), woodDark);
            }
            float rx = center.x < 0 ? center.x + 0.7f : center.x - 0.7f;
            Box("Stair_Rail", new Vector3(rx, 0.95f, center.z), new Vector3(0.08f, 0.08f, 4f), woodDark);
            worldLabels.Add(new KeyValuePair<Vector3, string>(center + Vector3.up * 1.3f, "個室への階段（後で作る）"));
            blockers.Add(new Rect(center.x - 0.75f, center.z - 2f, 1.5f, 4f));
        }

        // ================= 家具 =================
        void BuildFurniture()
        {
            // --- 奥：大きな暖炉 ---
            Box("Hearth", new Vector3(0f, 0.15f, 4.45f), new Vector3(3.4f, 0.3f, 1.1f), mortarDark);
            Box("Fireplace_PillarL", new Vector3(-1.4f, 1.45f, 4.65f), new Vector3(0.6f, 2.6f, 0.7f), mortarDark);
            Box("Fireplace_PillarR", new Vector3(1.4f, 1.45f, 4.65f), new Vector3(0.6f, 2.6f, 0.7f), mortarDark);
            Box("Fireplace_Mantel", new Vector3(0f, 2.85f, 4.55f), new Vector3(3.8f, 0.3f, 0.95f), woodDark);
            Box("Fireplace_Chimney", new Vector3(0f, 3.95f, 4.75f), new Vector3(2.9f, 2.1f, 0.5f), stoneWall);
            Box("Fireplace_Back", new Vector3(0f, 1.3f, 4.95f), new Vector3(2.3f, 2.3f, 0.1f), iron);
            Cyl("Log1", new Vector3(-0.2f, 0.42f, 4.55f), new Vector3(0.18f, 0.6f, 0.18f), Quaternion.Euler(0f, 0f, 90f), woodDark);
            Cyl("Log2", new Vector3(0.25f, 0.42f, 4.6f), new Vector3(0.18f, 0.55f, 0.18f), Quaternion.Euler(0f, 30f, 90f), woodDark);
            for (int i = 0; i < 5; i++)
            {
                var f = Box("Flame" + i, new Vector3(-0.5f + i * 0.25f, 0.65f, 4.55f), new Vector3(0.18f, 0.5f, 0.18f), i % 2 == 0 ? glowFire : glowEmber);
                var fl = f.AddComponent<FlameWobble>();
                fl.seed = i * 1.7f;
            }
            var fire = PointLight(new Vector3(0f, 1.0f, 3.9f), new Color(1f, 0.6f, 0.3f), 9f, 10f, true);
            fire.gameObject.AddComponent<Flicker>().amount = 0.2f;
            blockers.Add(new Rect(-1.85f, 3.85f, 3.7f, 1.2f));

            // --- 奥の左右：本棚 ---
            Bookshelf(new Vector3(-4.1f, 0f, 4.75f));
            Bookshelf(new Vector3(4.1f, 0f, 4.75f));

            // --- 暖炉の前：肘掛け椅子2脚（暖炉を向く）---
            Sofa("暖炉の前の椅子", new Vector3(-1.3f, 0f, 2.9f), 0f, 1.0f, 1, fabricGreen);
            Sofa("暖炉の前の椅子", new Vector3(1.3f, 0f, 2.9f), 0f, 1.0f, 1, fabricGreen);

            // --- 中央：低いテーブルを囲むソファ3つ（暖炉側が開いたコの字）---
            Box("LowTable", new Vector3(0f, 0.22f, 0.5f), new Vector3(1.4f, 0.08f, 0.8f), woodMid);
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0) ? -0.6f : 0.6f, z = (i < 2) ? 0.2f : 0.8f;
                Box("LowTable_Leg", new Vector3(x, 0.1f, z), new Vector3(0.08f, 0.2f, 0.08f), woodDark);
            }
            blockers.Add(new Rect(-0.75f, 0.05f, 1.5f, 0.9f));
            Sofa("ソファ", new Vector3(0f, 0f, -1.0f), 0f, 2.4f, 3, fabricRed);
            Sofa("ソファ", new Vector3(-1.95f, 0f, 0.5f), 90f, 2.4f, 3, fabricRed);
            Sofa("ソファ", new Vector3(1.95f, 0f, 0.5f), -90f, 2.4f, 3, fabricRed);

            // --- 右手前：大きなテーブル＋椅子6脚 ---
            Box("BigTable_Top", new Vector3(3.2f, 0.75f, -3.0f), new Vector3(2.4f, 0.08f, 1.0f), woodMid);
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0) ? 2.1f : 4.3f, z = (i < 2) ? -3.4f : -2.6f;
                Box("BigTable_Leg", new Vector3(x, 0.37f, z), new Vector3(0.1f, 0.74f, 0.1f), woodDark);
            }
            Box("Candle", new Vector3(3.2f, 0.9f, -3.0f), new Vector3(0.06f, 0.22f, 0.06f), glowLamp);
            blockers.Add(new Rect(2.0f, -3.5f, 2.4f, 1.0f));
            for (int i = 0; i < 3; i++)
            {
                float x = 2.4f + i * 0.8f;
                Chair(new Vector3(x, 0f, -2.2f), 180f);
                Chair(new Vector3(x, 0f, -3.8f), 0f);
            }

            // --- 左手前：窓辺のベンチ ---
            Bench(new Vector3(-5.6f, 0f, -3.1f), 90f);

            // --- 暇つぶしの場所 ---
            pois.Add(MakePoi("暖炉にあたっている", new Vector3(0f, 0.8f, 4.6f), new Vector3(-0.5f, 0f, 3.55f), new Vector3(0.5f, 0f, 3.55f), new Vector3(0f, 0f, 3.05f)));
            pois.Add(MakePoi("本棚の本を眺めている", new Vector3(-4.1f, 1.5f, 5f), new Vector3(-4.6f, 0f, 3.9f), new Vector3(-3.6f, 0f, 3.9f)));
            pois.Add(MakePoi("本棚の本を眺めている", new Vector3(4.1f, 1.5f, 5f), new Vector3(3.6f, 0f, 3.9f), new Vector3(4.6f, 0f, 3.9f)));
            pois.Add(MakePoi("窓の外を眺めている", new Vector3(6f, 2f, 3.4f), new Vector3(5.2f, 0f, 3.4f)));
        }

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
            blockers.Add(new Rect(c.x - w / 2f - 0.1f, c.z - 0.35f, w + 0.2f, 0.7f));
        }

        /// <summary>ソファ／肘掛け椅子。yaw は座った人が向く方向。</summary>
        void Sofa(string label, Vector3 c, float yaw, float width, int seatsCount, Material fabric)
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
                AddSeat(t, label, new Vector3(x, 0f, 0.05f), new Vector3(x, 0f, d / 2f + 0.4f));
            }
            AddFootprint(t, width, d);
        }

        void Chair(Vector3 c, float yaw)
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
            AddSeat(t, "大きなテーブル", Vector3.zero, new Vector3(0f, 0f, -0.7f));
            AddFootprint(t, 0.5f, 0.5f);
        }

        void Bench(Vector3 c, float yaw)
        {
            var t = new GameObject("窓辺のベンチ").transform;
            t.SetParent(root, false);
            t.localPosition = c;
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Box("Bench_Base", new Vector3(0f, 0.22f, 0f), new Vector3(1.5f, 0.44f, 0.5f), woodDark, t);
            Box("Bench_Cushion", new Vector3(0f, 0.47f, 0f), new Vector3(1.4f, 0.08f, 0.45f), fabricGreen, t);
            AddSeat(t, "窓辺のベンチ", new Vector3(-0.35f, 0f, 0f), new Vector3(-0.35f, 0f, 0.7f));
            AddSeat(t, "窓辺のベンチ", new Vector3(0.35f, 0f, 0f), new Vector3(0.35f, 0f, 0.7f));
            AddFootprint(t, 1.5f, 0.5f);
        }

        void AddSeat(Transform t, string label, Vector3 localPos, Vector3 localApproach)
        {
            seats.Add(new Seat
            {
                label = label,
                position = Flat(t.TransformPoint(localPos)),
                forward = t.forward,
                approach = Flat(t.TransformPoint(localApproach))
            });
        }

        void AddFootprint(Transform t, float w, float d)
        {
            Vector3 a = t.TransformPoint(new Vector3(-w / 2f, 0f, -d / 2f));
            Vector3 b = t.TransformPoint(new Vector3(w / 2f, 0f, d / 2f));
            float xMin = Mathf.Min(a.x, b.x), xMax = Mathf.Max(a.x, b.x);
            float zMin = Mathf.Min(a.z, b.z), zMax = Mathf.Max(a.z, b.z);
            blockers.Add(Rect.MinMaxRect(xMin, zMin, xMax, zMax));
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        // ================= 歩ける場所 =================
        void BuildNav()
        {
            nav = new GridNav(new Vector2(-HalfW, -HalfD), new Vector2(HalfW, HalfD), 0.25f);
            float m = 0.3f; // 壁ぎわ
            nav.Block(Rect.MinMaxRect(-HalfW - 1f, -HalfD - 1f, -HalfW + m, HalfD + 1f));
            nav.Block(Rect.MinMaxRect(HalfW - m, -HalfD - 1f, HalfW + 1f, HalfD + 1f));
            nav.Block(Rect.MinMaxRect(-HalfW - 1f, HalfD - m, HalfW + 1f, HalfD + 1f));
            nav.Block(Rect.MinMaxRect(-HalfW - 1f, -HalfD - 1f, HalfW + 1f, -HalfD + m));
            float inflate = 0.15f;
            foreach (var r in blockers)
                nav.Block(Rect.MinMaxRect(r.xMin - inflate, r.yMin - inflate, r.xMax + inflate, r.yMax + inflate));
        }

        // ================= キャラ =================
        void SpawnCharacters()
        {
            rel = new Relationships(6, 12345);
            AddCharacter(0, "A", "青髪・角", new Color(0.55f, 0.8f, 1f), 0.5f, 0.15f, false);
            AddCharacter(1, "B", "エルフ", new Color(0.35f, 0.7f, 0.35f), 0.2f, 0.15f, false);
            AddCharacter(2, "C", "猫耳", new Color(0.85f, 0.2f, 0.2f), 0.85f, 0.85f, false);
            AddCharacter(3, "D", "黒髪ロング", new Color(0.13f, 0.13f, 0.15f), 0.25f, 0.5f, false);
            AddCharacter(4, "E", "金髪ツインテール", new Color(0.95f, 0.8f, 0.35f), 0.8f, 0.5f, false);
            AddCharacter(5, "F", "男性", new Color(0.55f, 0.55f, 0.58f), 0.5f, 0.5f, true);
        }

        void AddCharacter(int id, string name, string note, Color color, float social, float whim, bool male)
        {
            var go = new GameObject("Char_" + name);
            go.transform.SetParent(root, false);
            Vector3 p = nav.RandomFreePoint();
            go.transform.position = p;
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(go.transform, false);
            body.GetComponent<Renderer>().sharedMaterial = ProtoArt.Mat(color, ProtoArt.Fabric, new Vector2(2f, 2f), 0.3f);

            // 顔の向きがわかる目印
            var face = GameObject.CreatePrimitive(PrimitiveType.Cube);
            face.name = "FaceMark";
            face.transform.SetParent(body.transform, false);
            face.transform.localPosition = new Vector3(0f, 0.62f, 0.42f);
            face.transform.localScale = new Vector3(0.6f, 0.08f, 0.25f);
            face.GetComponent<Renderer>().sharedMaterial = ProtoArt.Mat(new Color(0.95f, 0.85f, 0.75f));
            Destroy(face.GetComponent<Collider>());

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "SelectRing";
            ring.transform.SetParent(go.transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            ring.transform.localScale = new Vector3(0.9f, 0.01f, 0.9f);
            ring.GetComponent<Renderer>().sharedMaterial = ringMat;
            Destroy(ring.GetComponent<Collider>());
            ring.SetActive(false);

            var c = go.AddComponent<SimCharacter>();
            c.id = id;
            c.displayName = name;
            c.note = note;
            c.color = color;
            c.sociability = social;
            c.whim = whim;
            c.male = male;
            c.Init(this, body.transform, ring);
            characters.Add(c);
        }

        // ================= カメラ =================
        void SetupCamera()
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                mainCamera = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = new Color(0.04f, 0.04f, 0.06f);
            mainCamera.nearClipPlane = 0.1f;
            mainCamera.farClipPlane = 200f;
            mainCamera.fieldOfView = 50f;
            if (mainCamera.GetComponent<CameraRig>() == null) mainCamera.gameObject.AddComponent<CameraRig>();
        }

        // カメラが壁の外側にあるとき、その壁を消して中が見えるようにする
        void UpdateCutaway()
        {
            if (mainCamera == null) return;
            Vector3 cam = mainCamera.transform.position;
            foreach (var g in wallGroups)
            {
                bool hide = Vector3.Dot(cam - g.point, g.inward) < 0f;
                foreach (var r in g.renderers)
                {
                    if (r == null) continue;
                    r.shadowCastingMode = hide ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
                }
            }
        }
    }

    /// <summary>火やランプのゆらぎ。</summary>
    public class Flicker : MonoBehaviour
    {
        public float amount = 0.15f;
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
            lightRef.intensity = baseIntensity * (1f - amount + 2f * amount * n);
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
