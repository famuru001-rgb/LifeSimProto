using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 新しい学園（作り直しの第5案。famuru 2026-10-31 決定）の試し組み。今のゲームの寮・学校とは別の場所（x＋2000）に組むので、ゲームの流れには関わらない。
    /// ・Logs\new_campus.txt があるときだけ組む（無ければ何もしない）
    /// ・写真：Logs\campus_cmd.txt に1行1枚「shot 名前 x z 高さ 向き 見下ろし 距離」（x・z は配置図の座標。正門が 0,0、北が +z）→ Logs\campus_名前.png
    /// ・部品は Resources\Furniture の建物_v2（依頼20）。模様は共通の材質だけ（材質の名前で置きかえる）
    /// 2026-11-01 家具のチャット
    /// </summary>
    public class CampusPreview : MonoBehaviour
    {
        public static readonly Vector3 Origin = new Vector3(2000f, 0f, 0f);
        static string LogsDir { get { return Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs")); } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!File.Exists(Path.Combine(LogsDir, "new_campus.txt"))) return;
            new GameObject("NewCampus").AddComponent<CampusPreview>();
        }

        Transform root;
        readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        Material copper, water, grass, bannerOurs, bannerKnight, bannerMage, bannerTower;
        int placed;

        // 配置図の形（正門が 0,0。x は東、y は北）
        static readonly Vector2[] Island =
        {
            new Vector2(-104,8), new Vector2(-60,3), new Vector2(-14,0), new Vector2(14,0), new Vector2(60,3), new Vector2(106,10), new Vector2(120,42),
            new Vector2(116,100), new Vector2(98,134), new Vector2(62,152), new Vector2(0,158), new Vector2(-66,152), new Vector2(-104,132), new Vector2(-120,92), new Vector2(-118,38)
        };
        static readonly Vector2[] WallPts =
        {
            new Vector2(-72,3), new Vector2(-8,0.6f), new Vector2(8,0.6f), new Vector2(72,3), new Vector2(94,26), new Vector2(96,86), new Vector2(80,108), new Vector2(-80,108), new Vector2(-96,86), new Vector2(-94,26)
        };
        const float WaterY = -3f;

        void Start()
        {
            root = transform;
            root.position = Origin;
            MakeMats();
            Build();
            Debug.Log("[新しい学園] 組み終わり：部品 " + placed + " 個");
        }

        // ---------------- 材質（共通） ----------------
        static Texture2D T(string n) { return Resources.Load<Texture2D>("Tiles/" + n); }

        void MakeMats()
        {
            mats["Stone_Wall"] = ProtoArt.Mat(Color.white, T("Tile_StoneWall"), null, 0.12f);
            mats["Stone_Trim"] = ProtoArt.Mat(Color.white, T("Tile_StoneWall"), null, 0.2f);
            mats["Roof_Slate"] = ProtoArt.Mat(Color.white, T("Tile_RoofSlate"), null, 0.25f);
            mats["Wood_Dark"] = ProtoArt.Mat(Color.white, T("Tile_WoodPanel"), null, 0.15f);
            mats["Plaster"] = ProtoArt.Mat(Color.white, T("Tile_Plaster"), null, 0.1f);
            mats["Ground_Path"] = ProtoArt.Mat(Color.white, T("Tile_Flagstone"), null, 0.1f);
            mats["Rock_Cliff"] = ProtoArt.Mat(Color.white, T("Tile_Cliff"), null, 0.08f);
            mats["Metal_Iron"] = ProtoArt.Mat(new Color(0.17f, 0.17f, 0.18f), null, null, 0.35f);
            mats["Glass_Window"] = ProtoArt.Mat(new Color(0.36f, 0.47f, 0.58f), null, null, 0.85f);
            var atlas = ProtoArt.MatNew(Color.white, T("Castle_Atlas"), null, 0.1f);
            atlas.SetFloat("_AlphaClip", 1f); atlas.SetFloat("_Cutoff", 0.45f); atlas.EnableKeyword("_ALPHATEST_ON");
            atlas.SetFloat("_Cull", 0f);
            mats["Atlas_Detail"] = atlas;
            bannerOurs = ProtoArt.Mat(new Color(0.18f, 0.29f, 0.62f), null, null, 0.1f);
            bannerKnight = ProtoArt.Mat(new Color(0.62f, 0.14f, 0.13f), null, null, 0.1f);
            bannerMage = ProtoArt.Mat(new Color(0.42f, 0.2f, 0.58f), null, null, 0.1f);
            bannerTower = ProtoArt.Mat(new Color(0.16f, 0.5f, 0.48f), null, null, 0.1f);
            mats["Fabric_Banner"] = bannerOurs;
            copper = ProtoArt.Mat(new Color(0.36f, 0.6f, 0.53f), null, null, 0.4f);
            water = ProtoArt.Mat(new Color(0.28f, 0.47f, 0.6f), null, null, 0.9f);
            grass = ProtoArt.Mat(Color.white, T("Tile_Grass"), null, 0.05f);
        }

        // ---------------- 部品を置く ----------------
        /// <summary>配置図の (x, 高さ, y) に部品を置く。yaw は Unity の向き（0 で部品の正面が北）。</summary>
        GameObject Put(string name, float x, float h, float y, float yaw, Vector3? scale = null, Material banner = null)
        {
            var pf = Resources.Load<GameObject>("Furniture/Furn_" + name);
            if (pf == null) { Debug.LogWarning("[新しい学園] 部品がない：" + name); return null; }
            // 入れ物（向きと大きさ：x＝部品の横、y＝高さ、z＝奥行き）の中に部品を入れる（FBX の根元の回転に左右されない）
            var holder = new GameObject(name).transform;
            holder.SetParent(root, false);
            holder.localPosition = new Vector3(x, h, y);
            holder.localRotation = Quaternion.Euler(0f, yaw, 0f);
            if (scale.HasValue) holder.localScale = scale.Value;
            var go = Instantiate(pf, holder, false);
            go.name = name + "_Model";
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var sm = r.sharedMaterials;
                for (int i = 0; i < sm.Length; i++)
                {
                    string n = sm[i] != null ? sm[i].name : "";
                    Material m = null;
                    if (n.StartsWith("Fabric_Banner")) m = banner != null ? banner : bannerOurs;
                    else if (n.StartsWith("Metal_Iron") && name == "Roof_Dome") m = copper;
                    else foreach (var kv in mats) if (n.StartsWith(kv.Key)) { m = kv.Value; break; }
                    if (m != null) sm[i] = m;
                }
                r.sharedMaterials = sm;
            }
            placed++;
            return holder.gameObject;
        }

        GameObject Box(string name, Vector3 center, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(root, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
            Destroy(go.GetComponent<Collider>());
            return go;
        }

        Material Tiled(string tile, float u, float v) { return ProtoArt.Mat(Color.white, T(tile), new Vector2(Mathf.Max(1f, u), Mathf.Max(1f, v)), 0.1f); }

        /// <summary>石の壁の箱（模様の大きさ 3m×2m に合わせる）。</summary>
        void StoneBox(string name, float x0, float y0, float x1, float y1, float h0, float h1)
        {
            float w = x1 - x0, d = y1 - y0, hh = h1 - h0;
            Box(name, new Vector3((x0 + x1) / 2f, (h0 + h1) / 2f, (y0 + y1) / 2f), new Vector3(w, hh, d), Tiled("Tile_StoneWall", Mathf.Max(w, d) / 3f, hh / 2f));
        }

        /// <summary>道（石畳の帯）を折れ線で。</summary>
        void PathLine(float width, params Vector2[] pts)
        {
            for (int i = 0; i + 1 < pts.Length; i++)
            {
                Vector2 a = pts[i], b = pts[i + 1];
                float len = Vector2.Distance(a, b);
                Vector2 c = (a + b) / 2f;
                var go = Box("Path", new Vector3(c.x, 0.03f, c.y), new Vector3(width, 0.06f, len + width * 0.6f), Tiled("Tile_Flagstone", width / 2f, len / 2f));
                go.transform.localRotation = Quaternion.Euler(0f, Mathf.Atan2(b.x - a.x, b.y - a.y) * Mathf.Rad2Deg, 0f);
            }
        }

        void Lawn(float x, float y, float r)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Lawn";
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(x, 0.04f, y);
            go.transform.localScale = new Vector3(r * 2f, 0.02f, r * 2f);
            go.GetComponent<Renderer>().sharedMaterial = Tiled("Tile_Grass", r / 2f, r / 2f);
            Destroy(go.GetComponent<Collider>());
        }

        /// <summary>辺の向きに部品を並べる（部品の長さ方向は X）。</summary>
        static float YawAlong(Vector2 d) { return Mathf.Atan2(-d.y, d.x) * Mathf.Rad2Deg; }
        /// <summary>部品の正面（+Z）を n の向きにする向き。</summary>
        static float YawFacing(Vector2 n) { return Mathf.Atan2(n.x, n.y) * Mathf.Rad2Deg; }

        // ---------------- 地形 ----------------
        /// <summary>地面の高さ：城壁の外の森は少し高く、北東に丘（famuru 2026-10-31）。</summary>
        static float H(float x, float y)
        {
            float f = Mathf.SmoothStep(0f, 1f, (y - 110f) / 12f) * 1.6f;
            float dx = (x - 40f) / 22f, dy = (y - 136f) / 15f;
            return f + 5f * Mathf.Exp(-(dx * dx + dy * dy) * 1.6f);
        }

        static bool Inside(Vector2[] poly, Vector2 p)
        {
            bool c = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if (((poly[i].y > p.y) != (poly[j].y > p.y)) && (p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)) c = !c;
            return c;
        }

        void Ground()
        {
            const float step = 2.5f;
            float x0 = -125f, x1 = 125f, y0 = -5f, y1 = 162f;
            int nx = Mathf.CeilToInt((x1 - x0) / step), ny = Mathf.CeilToInt((y1 - y0) / step);
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            for (int j = 0; j <= ny; j++)
                for (int i = 0; i <= nx; i++)
                {
                    float x = x0 + i * step, y = y0 + j * step;
                    verts.Add(new Vector3(x, H(x, y), y)); uvs.Add(new Vector2(x / 4f, y / 4f));
                }
            for (int j = 0; j < ny; j++)
                for (int i = 0; i < nx; i++)
                {
                    float cx = x0 + (i + 0.5f) * step, cy = y0 + (j + 0.5f) * step;
                    if (!Inside(Island, new Vector2(cx, cy))) continue;
                    int a = j * (nx + 1) + i, b = a + 1, c = a + nx + 1, d = c + 1;
                    tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(b); tris.Add(c); tris.Add(d);
                }
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var go = new GameObject("Island_Ground");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = grass;

            // 湖（島のまわりと街の前）
            var w = GameObject.CreatePrimitive(PrimitiveType.Plane);
            w.name = "Lake";
            w.transform.SetParent(root, false);
            w.transform.localPosition = new Vector3(20f, WaterY, 40f);
            w.transform.localScale = new Vector3(70f, 1f, 50f);
            w.GetComponent<Renderer>().sharedMaterial = water;
            Destroy(w.GetComponent<Collider>());
        }

        void Cliffs()
        {
            float area = 0f;
            for (int i = 0, j = Island.Length - 1; i < Island.Length; j = i++) area += (Island[j].x * Island[i].y - Island[i].x * Island[j].y);
            bool ccw = area > 0f;
            for (int i = 0; i < Island.Length; i++)
            {
                Vector2 a = Island[i], b = Island[(i + 1) % Island.Length];
                Vector2 d = (b - a); float L = d.magnitude; d /= L;
                Vector2 n = ccw ? new Vector2(d.y, -d.x) : new Vector2(-d.y, d.x); // 外向き
                int k = Mathf.Max(1, Mathf.CeilToInt(L / 10f));
                float seg = L / k;
                for (int s = 0; s < k; s++)
                {
                    Vector2 p = a + d * (seg * (s + 0.5f));
                    float top = H(p.x, p.y) - 0.12f; // 草の面より少し下（ちらつき防止）
                    float bottom = WaterY - 4f;
                    Vector2 c = p - n * 4f;
                    Put("Cliff", c.x, bottom, c.y, YawFacing(n), new Vector3(seg / 10f * 1.08f, (top - bottom) / 20f, 1f));
                }
            }
        }

        // ---------------- 城壁・正門・跳ね橋・街 ----------------
        void Walls()
        {
            for (int i = 0; i < WallPts.Length; i++)
            {
                if (i == 1) continue; // 正門のところ
                Vector2 a = WallPts[i], b = WallPts[(i + 1) % WallPts.Length];
                Vector2 d = b - a; float L = d.magnitude;
                int k = Mathf.Max(1, Mathf.RoundToInt(L / 6f));
                float seg = L / k;
                for (int s = 0; s < k; s++)
                {
                    Vector2 p = a + d.normalized * (seg * (s + 0.5f));
                    bool postern = (i == 7 && s == k / 2 + 1);
                    Put(postern ? "Postern" : "CastleWall", p.x, 0f, p.y, YawAlong(d), new Vector3(seg / 6f, 1f, 1f));
                }
            }
            for (int i = 0; i < WallPts.Length; i++)
            {
                if (i == 1 || i == 2) continue;
                Vector2 p = WallPts[i];
                Put("Tower_Round_Base", p.x, 0f, p.y, 0f);
                Put("Tower_Round", p.x, 3.5f, p.y, 45f);
                Put("Battlement_Top_Round", p.x, 7f, p.y, 0f);
            }
            // 正門（崖のきわ。前が南）と跳ね橋
            Put("Gatehouse", 0f, 0f, 5f, 180f);
            Put("Drawbridge", 0f, -0.8f, 5f - 15.64f, 180f);
            // 街：石の護岸と家並み（背景）
            const float qz = -24f;
            for (float x = -135f; x <= 165f; x += 10f) Put("Quay", x, -4f, qz - 1.5f, 0f);
            Box("Town_Ground", new Vector3(15f, -0.05f, -60f), new Vector3(320f, 0.1f, 70f), Tiled("Tile_Flagstone", 160f, 35f));
            int t = 0;
            for (float x = -130f; x <= 160f; x += 42f, t++)
            {
                Put("Town_Block", x, 0f, -44f, (t % 2 == 0) ? 0f : 180f);
                Put("Town_Block", x + 21f, 0f, -74f, (t % 2 == 0) ? 180f : 0f);
            }
        }

        // ---------------- 本館・体育館・大講堂・中庭の渡り廊下 ----------------
        void RoofAlongX(float x0, float x1, float yc, float span, float baseH, float roofH)
        {
            int k = Mathf.Max(1, Mathf.RoundToInt((x1 - x0) / 4f));
            float seg = (x1 - x0) / k;
            for (int s = 0; s < k; s++) Put("Roof_Gable", x0 + seg * (s + 0.5f), baseH, yc, 0f, new Vector3(seg / 4f, roofH / 6f, span / 10f));
            Put("Roof_GableEnd", x0, baseH, yc, 90f, new Vector3(span / 10f, roofH / 6f, 1f));
            Put("Roof_GableEnd", x1, baseH, yc, -90f, new Vector3(span / 10f, roofH / 6f, 1f));
        }

        void RoofAlongY(float y0, float y1, float xc, float span, float baseH, float roofH)
        {
            int k = Mathf.Max(1, Mathf.RoundToInt((y1 - y0) / 4f));
            float seg = (y1 - y0) / k;
            for (int s = 0; s < k; s++) Put("Roof_Gable", xc, baseH, y0 + seg * (s + 0.5f), 90f, new Vector3(seg / 4f, roofH / 6f, span / 10f));
            Put("Roof_GableEnd", xc, baseH, y0, 180f, new Vector3(span / 10f, roofH / 6f, 1f));
            Put("Roof_GableEnd", xc, baseH, y1, 0f, new Vector3(span / 10f, roofH / 6f, 1f));
        }

        void RoundTower(float x, float y, int floors, bool dome = false, Material banner = null)
        {
            Put("Tower_Round_Base", x, 0f, y, 0f);
            for (int f = 1; f < floors; f++) Put("Tower_Round", x, f * 3.5f, y, (f % 2) * 45f);
            if (dome) Put("Roof_Dome", x, floors * 3.5f, y, 200f);
            else Put("Roof_Cone", x, floors * 3.5f, y, 0f, null, banner);
        }

        void WindowsAlongX(float x0, float x1, float y, float facing, int floors, float every, string model = "Window_Arch")
        {
            for (int f = 0; f < floors; f++)
                for (float x = x0 + every / 2f; x < x1; x += every)
                    Put(model, x, f * 3.5f + 0.9f, y, facing);
        }

        void School()
        {
            // 本館（68×14m・3階建て）
            StoneBox("Main_Hall", -34f, 76f, 34f, 90f, 0f, 10.5f);
            RoofAlongX(-34f, 34f, 83f, 14.6f, 10.5f, 7f);
            WindowsAlongX(-30f, 30f, 75.85f, 180f, 3, 4f);
            WindowsAlongX(-30f, 30f, 90.15f, 0f, 3, 4f);
            for (int i = 0; i < 6; i++) Put("Dormer", -25f + i * 10f, 11.6f, 78.3f, 180f);
            foreach (var p in new[] { new Vector2(-34, 90), new Vector2(34, 90), new Vector2(-34, 76), new Vector2(34, 76) }) RoundTower(p.x, p.y, 4);
            RoundTower(0f, 91.5f, 5);
            for (int i = -1; i <= 1; i += 2) Put("Banner", i * 6f, 6.5f, 75.8f, 180f);
            // 体育館・大講堂（2階分の高さ）
            StoneBox("Gym", -40f, 42f, -24f, 68f, 0f, 8f);
            RoofAlongY(42f, 68f, -32f, 16.6f, 8f, 6f);
            RoundTower(-40f, 68f, 3);
            StoneBox("Auditorium", 24f, 42f, 40f, 68f, 0f, 8f);
            RoofAlongY(42f, 68f, 32f, 16.6f, 8f, 6f);
            RoundTower(40f, 68f, 3);
            for (float y = 45f; y < 66f; y += 4f) { Put("Window_Arch", -40.15f, 2.2f, y, -90f, new Vector3(1f, 1.4f, 1f)); Put("Window_Arch", 40.15f, 2.2f, y, 90f, new Vector3(1f, 1.4f, 1f)); }
            // 中庭（芝・噴水）
            Box("Court", new Vector3(0f, 0.02f, 58f), new Vector3(42f, 0.04f, 30f), Tiled("Tile_Grass", 10f, 7f));
            Put("Fountain", 0f, 0f, 58f, 0f);
            // 中庭を囲む渡り廊下：北・西・東はまっすぐ、南は外へふくらむ曲線（famuru）
            var ctr = new Vector2(0f, 58f);
            ArcadeLine(ctr, new Vector2(-22.5f, 74.5f), new Vector2(22.5f, 74.5f));
            ArcadeLine(ctr, new Vector2(-22.5f, 41.5f), new Vector2(-22.5f, 74.5f));
            ArcadeLine(ctr, new Vector2(22.5f, 41.5f), new Vector2(22.5f, 74.5f));
            var curve = new List<Vector2>();
            for (int i = 0; i <= 16; i++)
            {
                float t = i / 16f;
                Vector2 a = new Vector2(-22.5f, 41.5f), c = new Vector2(0f, 27.5f), b = new Vector2(22.5f, 41.5f);
                curve.Add((1 - t) * (1 - t) * a + 2 * (1 - t) * t * c + t * t * b);
            }
            for (int i = 0; i + 1 < curve.Count; i++)
            {
                Vector2 a = curve[i], b = curve[i + 1], m = (a + b) / 2f, d = b - a;
                Vector2 n = new Vector2(-d.y, d.x).normalized; if (Vector2.Dot(n, ctr - m) < 0f) n = -n;
                if (i == 7 || i == 8) { if (i == 7) Put("Arcade_Gate", curve[8].x, 0f, curve[8].y, YawFacing(-n)); continue; }
                Put("Arcade", m.x, 0f, m.y, YawFacing(n), new Vector3(d.magnitude / 3f * 1.04f, 1f, 1f));
            }
            Box("Court_South", new Vector3(0f, 0.02f, 38f), new Vector3(42f, 0.04f, 8f), Tiled("Tile_Grass", 10f, 2f));
        }

        void ArcadeLine(Vector2 ctr, Vector2 a, Vector2 b)
        {
            Vector2 d = b - a; float L = d.magnitude; int k = Mathf.Max(1, Mathf.RoundToInt(L / 3f)); float seg = L / k;
            for (int s = 0; s < k; s++)
            {
                Vector2 p = a + d.normalized * (seg * (s + 0.5f));
                Vector2 n = new Vector2(-d.y, d.x).normalized; if (Vector2.Dot(n, ctr - p) < 0f) n = -n;
                Put("Arcade", p.x, 0f, p.y, YawFacing(n), new Vector3(seg / 3f, 1f, 1f));
            }
        }

        /// <summary>地上の廊下（屋根つき）を折れ線で。</summary>
        void Walkway(params Vector2[] pts)
        {
            for (int i = 0; i + 1 < pts.Length; i++)
            {
                Vector2 a = pts[i], b = pts[i + 1], d = b - a; float L = d.magnitude; int k = Mathf.Max(1, Mathf.RoundToInt(L / 3f)); float seg = L / k;
                for (int s = 0; s < k; s++)
                {
                    Vector2 p = a + d.normalized * (seg * (s + 0.5f));
                    Put("Arcade", p.x, 0f, p.y, YawAlong(d) + 90f, new Vector3(seg / 3f, 1f, 1f));
                }
            }
        }

        // ---------------- 寮 ----------------
        void OurDorm()
        {
            // わたしたちの寮（31×24m・3階建て。外見だけの試し。中はあとで）
            float x0 = 46f, x1 = 77f, y0 = 64f, y1 = 88f;
            StoneBox("OurDorm", x0, y0, x1, y1, 0f, 10.5f);
            RoofAlongX(x0, x1, (y0 + y1) / 2f, 24.6f, 10.5f, 8f);
            for (int i = 0; i < 5; i++) { Put("Dormer", 52f + i * 4.2f, 12.2f, y0 + 3.2f, 180f); Put("Dormer", 52f + i * 4.2f, 12.2f, y1 - 3.2f, 0f); }
            for (int f = 0; f < 3; f++)
                for (int i = 0; i < 5; i++)
                {
                    float x = 52.5f + i * 3f;
                    bool bay = (i == 1 || i == 3);
                    Put(bay ? "Window_Bay" : "Window_Arch", x, f * 3.5f + (bay ? 0.5f : 0.9f), y0 - 0.02f, 180f);
                    Put(bay ? "Window_Bay" : "Window_Arch", x, f * 3.5f + (bay ? 0.5f : 0.9f), y1 + 0.02f, 0f);
                }
            Put("Bay_Round", x0, 0f, 74.6f, -90f);
            RoundTower(x0, y0, 4); RoundTower(x0, y1, 4); RoundTower(x1, y1, 4, true);
            Put("Banner", x0 - 0.1f, 6f, 70f, -90f); Put("Banner", x0 - 0.1f, 6f, 79f, -90f);
            // 2階の渡り廊下（本館の東のはしへ）
            for (int i = 0; i < 3; i++) Put("SkyCorridor", 44f - i * 4f, 2.7f, 81f, 0f);
            Put("SkyCorridor_Pier", 40f, 0f, 81f, 0f, new Vector3(1f, 2.7f / 3.5f, 1f));
            // 前庭と露天風呂の場所（芝と石の囲い）
            Box("Dorm_FrontLawn", new Vector3(41f, 0.03f, 71.5f), new Vector3(8f, 0.04f, 11f), Tiled("Tile_Grass", 2f, 3f));
            StoneBox("Spa_Wall", 73f, 59.5f, 81.5f, 60f, 0f, 1.4f);
            StoneBox("Spa_Wall2", 81f, 59.5f, 81.5f, 68f, 0f, 1.4f);
            Box("Spa_Water", new Vector3(78.5f, 0.25f, 62f), new Vector3(4.4f, 0.1f, 4.4f), water);
        }

        void OtherDorms()
        {
            Put("Dorm_Knight", -62f, 0f, 63f, 180f, null, bannerKnight);
            Put("Dorm_Mage", 61.5f, 0f, 25f, 180f, null, bannerMage);
            Put("Dorm_Tower", -72f, 0f, 29f, 180f, null, bannerTower);
            Put("Greenhouse", -59f, 0f, 99.5f, 180f);
            Put("Hut", -44.5f, 0f, 99f, 180f);
            Put("Training_Hall", -43f, H(-43f, 124f), 124f, 180f);
            Put("Dungeon_Gate", -67f, H(-67f, 131f), 131f, 180f);
            // 地上の廊下：騎士団の寮→本館の西のはし、塔の寮・魔術師団の寮→中庭の渡り廊下の南のすみ（famuru）
            Walkway(new Vector2(-47f, 71f), new Vector2(-40f, 71f), new Vector2(-40f, 81f), new Vector2(-35.5f, 81f));
            Walkway(new Vector2(-58f, 31f), new Vector2(-40f, 35f), new Vector2(-24f, 40f));
            Walkway(new Vector2(52f, 37f), new Vector2(40f, 35f), new Vector2(24f, 40f));
        }

        void Paths()
        {
            PathLine(4f, new Vector2(0, 0), new Vector2(0, 11));
            PathLine(3f, new Vector2(0, 18), new Vector2(0, 30));
            PathLine(3f, new Vector2(0, 18), new Vector2(14, 22), new Vector2(28, 28), new Vector2(40, 40), new Vector2(43, 52), new Vector2(41, 64));
            PathLine(3f, new Vector2(0, 18), new Vector2(-14, 22), new Vector2(-28, 28), new Vector2(-40, 40), new Vector2(-46, 48));
            PathLine(3f, new Vector2(22, 24), new Vector2(40, 28), new Vector2(46, 30));
            PathLine(2.5f, new Vector2(-34, 86), new Vector2(-52, 89), new Vector2(-72, 94), new Vector2(-87, 99));
            PathLine(2.2f, new Vector2(-87, 99), new Vector2(-80, 112), new Vector2(-70, 122));
            PathLine(2.2f, new Vector2(-80, 112), new Vector2(-50, 118));
            Lawn(0f, 18f, 7.5f); Lawn(26f, 27f, 5f); Lawn(-26f, 27f, 5f);
        }

        void Trees()
        {
            int n = 0;
            for (float y = 112f; y < 156f; y += 7f)
                for (float x = -116f; x < 116f; x += 7f)
                {
                    float jx = Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f; jx -= Mathf.Floor(jx);
                    float jy = Mathf.Sin(x * 39.346f + y * 11.135f) * 24634.6345f; jy -= Mathf.Floor(jy);
                    float px = x + (jx - 0.5f) * 5f, py = y + (jy - 0.5f) * 5f;
                    if (!Inside(Island, new Vector2(px, py))) continue;
                    if (Mathf.Abs(px + 60f) < 18f && Mathf.Abs(py - 126f) < 10f) continue; // 訓練の施設・ダンジョンの入り口
                    float d2 = (px - 40f) * (px - 40f) / 400f + (py - 136f) * (py - 136f) / 140f;
                    if (d2 < 0.25f) continue; // 丘のてっぺんはあける
                    Put("Tree", px, H(px, py), py, jx * 360f, Vector3.one * (0.9f + jy * 0.6f));
                    n++;
                }
            // 城壁の中の木（道ぞい）
            foreach (var p in new[] { new Vector2(-14, 40), new Vector2(14, 40), new Vector2(-50, 40), new Vector2(50, 50), new Vector2(-30, 95), new Vector2(30, 98), new Vector2(86, 40), new Vector2(-86, 50) })
                Put("Tree", p.x, 0f, p.y, p.x * 7f, Vector3.one * 1.1f);
        }

        void Build()
        {
            Ground();
            Cliffs();
            Walls();
            Paths();
            School();
            OurDorm();
            OtherDorms();
            Trees();
        }

        // ---------------- 写真 ----------------
        Camera shotCam;
        float nextPoll;

        void Update()
        {
            if (Time.unscaledTime < nextPoll) return;
            nextPoll = Time.unscaledTime + 0.5f;
            string cmd = Path.Combine(LogsDir, "campus_cmd.txt");
            if (!File.Exists(cmd)) return;
            string[] lines;
            try { lines = File.ReadAllLines(cmd); File.Delete(cmd); } catch { return; }
            var result = new List<string>();
            foreach (var raw in lines)
            {
                var a = raw.Trim().Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (a.Length >= 8 && a[0] == "shot")
                {
                    var ci = System.Globalization.CultureInfo.InvariantCulture;
                    float x = float.Parse(a[2], ci), z = float.Parse(a[3], ci), h = float.Parse(a[4], ci), yaw = float.Parse(a[5], ci), pitch = float.Parse(a[6], ci), dist = float.Parse(a[7], ci);
                    result.Add(Shot(a[1], new Vector3(x, h, z), yaw, pitch, dist));
                }
            }
            try { File.WriteAllLines(Path.Combine(LogsDir, "campus_result.txt"), result.ToArray()); } catch { }
        }

        string Shot(string name, Vector3 target, float yaw, float pitch, float dist)
        {
            if (shotCam == null)
            {
                var go = new GameObject("CampusShotCam");
                go.transform.SetParent(root, false);
                shotCam = go.AddComponent<Camera>();
                shotCam.enabled = false;
                shotCam.fieldOfView = 45f;
                shotCam.nearClipPlane = 0.3f;
                shotCam.farClipPlane = 2000f;
            }
            Vector3 t = root.TransformPoint(target);
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            shotCam.transform.position = t - rot * Vector3.forward * dist;
            shotCam.transform.rotation = rot;
            var rt = new RenderTexture(1600, 900, 24);
            bool fog = RenderSettings.fog;
            RenderSettings.fog = false;
            shotCam.targetTexture = rt;
            shotCam.Render();
            RenderSettings.fog = fog;
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            shotCam.targetTexture = null;
            rt.Release(); Destroy(rt);
            string path = Path.Combine(LogsDir, "campus_" + name + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Destroy(tex);
            return "shot: " + path;
        }
    }
}
