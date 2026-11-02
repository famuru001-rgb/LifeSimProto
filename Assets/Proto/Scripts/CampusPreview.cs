using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 新しい学園（作り直しの第5案。famuru 2026-10-31 決定）の外側。
    /// ・新しい学園で遊ぶとき（Logs\new_campus_play.txt があるとき＝LoungeWorld.NewLayout）は、寮と学校の本当の場所に合わせて組む（配置図の座標→CampusPlan.G）。
    ///   寮と学校の中（NewDorm.cs・SchoolBuilder）は組まず、島・崖・城壁・正門・街・中庭を囲む渡り廊下・地上の廊下・ほかの寮・離れ・森・道だけ
    /// ・そうでなく Logs\new_campus.txt があるときは、今までどおり別の場所（x＋2000）に全部を試しに組む（ゲームの流れには関わらない）
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
            if (!LoungeWorld.NewLayout && !File.Exists(Path.Combine(LogsDir, "new_campus.txt"))) return;
            new GameObject("NewCampus").AddComponent<CampusPreview>();
        }

        Transform root;
        readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        Material copper, water, grass, bannerOurs, bannerKnight, bannerMage, bannerTower, curbMat, lampGlass;
        int placed;
        bool game; // 新しい学園で遊ぶとき（本当の場所に組む）

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
        public static bool GateOpen = true; // 組んだときの形。新しい学園で遊ぶときは時刻で開け閉めする（6:00〜21:00 は開ける）
        GameObject gateL, gateR;
        float gateK;
        CampusSky sky;

        void Start()
        {
            root = transform;
            game = LoungeWorld.NewLayout;
            if (game) { root.position = CampusPlan.Origin; root.rotation = CampusPlan.Rotation; }
            else root.position = Origin;
            MakeMats();
            if (game) { sky = new GameObject("CampusSky").AddComponent<CampusSky>(); sky.SetWater(water); } // 空・雲・山・湖・灯り（CampusSky.cs）
            Build();
            Debug.Log("[新しい学園] 組み終わり：部品 " + placed + " 個");
        }

        // ---------------- 材質（共通） ----------------
        /// <summary>模様の画像。新しい学園では依頼24 の作り直し（Resources\TilesV2）を先に探す。</summary>
        static Texture2D T(string n)
        {
            Texture2D t = LoungeWorld.NewLayout ? Resources.Load<Texture2D>("TilesV2/" + n) : null;
            return t != null ? t : Resources.Load<Texture2D>("Tiles/" + n);
        }

        void MakeMats()
        {
            mats["Stone_Wall"] = TilePBR.Apply(ProtoArt.Mat(Color.white, T("Tile_StoneWall"), null, 0.12f), "Tile_StoneWall");
            bool trim = T("Tile_StoneTrim") != null; // 依頼24 の角の石（無ければ石壁）
            mats["Stone_Trim"] = TilePBR.Apply(ProtoArt.Mat(Color.white, T(trim ? "Tile_StoneTrim" : "Tile_StoneWall"), null, 0.2f), trim ? "Tile_StoneTrim" : "Tile_StoneWall");
            mats["Roof_Slate"] = TilePBR.Apply(ProtoArt.Mat(Color.white, T("Tile_RoofSlate"), null, 0.25f), "Tile_RoofSlate");
            mats["Wood_Dark"] = TilePBR.Apply(ProtoArt.Mat(Color.white, T("Tile_WoodPanel"), null, 0.15f), "Tile_WoodPanel");
            mats["Plaster"] = TilePBR.Apply(ProtoArt.Mat(Color.white, T("Tile_Plaster"), null, 0.1f), "Tile_Plaster");
            mats["Ground_Path"] = TilePBR.Apply(ProtoArt.Mat(Color.white, T("Tile_Flagstone"), null, 0.1f), "Tile_Flagstone");
            mats["Rock_Cliff"] = TilePBR.Apply(ProtoArt.Mat(Color.white, T("Tile_Cliff"), null, 0.08f), "Tile_Cliff");
            mats["Metal_Iron"] = ProtoArt.Mat(new Color(0.17f, 0.17f, 0.18f), null, null, 0.35f);
            mats["Glass_Window"] = ProtoArt.Mat(new Color(0.36f, 0.47f, 0.58f), null, null, 0.85f);
            var atlas = ProtoArt.MatNew(Color.white, T("Castle_Atlas"), null, 0.1f);
            atlas.SetFloat("_AlphaClip", 1f); atlas.SetFloat("_Cutoff", 0.45f); atlas.EnableKeyword("_ALPHATEST_ON");
            atlas.SetFloat("_Cull", 0f);
            mats["Atlas_Detail"] = TilePBR.Apply(atlas, "Castle_Atlas");
            // 依頼23 の植物・街の家の材質（依頼24 の作り直しの画像）
            foreach (var k in new[] { "Leaf_Green", "Leaf_Dark", "Bark", "Roof_Red" })
                if (T(k) != null) mats[k] = TilePBR.Apply(ProtoArt.MatNew(Color.white, T(k), null, 0.12f), k);
            foreach (var k in new[] { "Flower", "Grass_Blade" })
            {
                var tx = Resources.Load<Texture2D>("Furniture/Textures/" + k);
                if (tx != null) mats[k] = ProtoArt.MatNew(Color.white, tx, null, 0.08f);
            }
            if (game)
            {
                // 葉・草・花は風でゆれる材質に（LifeSim/Foliage。2026-11-02）
                var fol = Shader.Find("LifeSim/Foliage");
                if (fol != null)
                {
                    foreach (var k in new[] { "Leaf_Green", "Leaf_Dark", "Flower", "Grass_Blade" })
                    {
                        Texture2D tx = k.StartsWith("Leaf") ? T(k) : Resources.Load<Texture2D>("Furniture/Textures/" + k);
                        if (tx == null) continue;
                        var fm = new Material(fol) { name = "Foliage_" + k, enableInstancing = true };
                        fm.SetTexture("_BaseMap", tx);
                        bool leaf = k.StartsWith("Leaf");
                        var nm = leaf ? T(k + "_Normal") : null;
                        if (nm != null) { fm.SetTexture("_BumpMap", nm); fm.SetFloat("_UseBump", 1f); }
                        fm.SetFloat("_WindStrength", leaf ? 0.07f : 0.1f);
                        fm.SetFloat("_WindSpeed", leaf ? 1.1f : 1.8f);
                        fm.SetFloat("_WindYScale", leaf ? 0.16f : 2.2f);
                        fm.SetFloat("_Wrap", leaf ? 0.4f : 0.55f);
                        fm.SetFloat("_UpNormal", leaf ? 0f : 0.5f);
                        fm.SetFloat("_Translucency", leaf ? 0.9f : 0.5f);
                        fm.SetFloat("_Cull", leaf ? 2f : 0f);
                        mats[k] = fm;
                    }
                }
            }
            mats["Glow_Fire"] = ProtoArt.Glow(new Color(1f, 0.55f, 0.2f), 2.6f);
            lampGlass = ProtoArt.Glow(new Color(1f, 0.75f, 0.45f), 1.5f);
            bannerOurs = ProtoArt.Mat(new Color(0.18f, 0.29f, 0.62f), null, null, 0.1f);
            bannerKnight = ProtoArt.Mat(new Color(0.62f, 0.14f, 0.13f), null, null, 0.1f);
            bannerMage = ProtoArt.Mat(new Color(0.42f, 0.2f, 0.58f), null, null, 0.1f);
            bannerTower = ProtoArt.Mat(new Color(0.16f, 0.5f, 0.48f), null, null, 0.1f);
            mats["Fabric_Banner"] = bannerOurs;
            copper = ProtoArt.Mat(new Color(0.36f, 0.6f, 0.53f), null, null, 0.4f);
            water = ProtoArt.Mat(new Color(0.28f, 0.47f, 0.6f), null, null, 0.9f);
            grass = ProtoArt.Mat(Color.white, T("Tile_Grass"), null, 0.05f);
            curbMat = ProtoArt.Mat(new Color(0.78f, 0.76f, 0.72f), T("Tile_StoneWall"), new Vector2(1f, 4f), 0.1f);
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
                if (game) LeafNormals(r, sm);
                for (int i = 0; i < sm.Length; i++)
                {
                    string n = sm[i] != null ? sm[i].name : "";
                    Material m = null;
                    if (n.StartsWith("Fabric_Banner")) m = banner != null ? banner : bannerOurs;
                    else if (n.StartsWith("Metal_Iron") && name == "Roof_Dome") m = copper;
                    else if (n.StartsWith("Glass_Window") && name == "Lamp_Garden" && lampGlass != null) m = lampGlass; // 庭の灯りのガラスは夜に光る
                    else foreach (var kv in mats) if (n.StartsWith(kv.Key)) { m = kv.Value; break; }
                    if (m != null) sm[i] = m;
                }
                r.sharedMaterials = sm;
            }
            placed++;
            return holder.gameObject;
        }

        // ---------------- 葉のかたまりを丸く光らせる（球の法線。屋外の作り方.md の 4.） ----------------
        static readonly Dictionary<Mesh, Mesh> leafMeshCache = new Dictionary<Mesh, Mesh>();

        /// <summary>葉の材質の面の向きを、つながった葉のかたまりの中心から外向きに寄せる（小さな面がばらばらに光るのを止める）。同じ模型は1回だけ作る。</summary>
        static void LeafNormals(Renderer r, Material[] sm)
        {
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;
            bool any = false;
            for (int i = 0; i < sm.Length; i++) if (sm[i] != null && IsLeafMat(sm[i].name)) any = true;
            if (!any) return;
            var src = mf.sharedMesh;
            if (leafMeshCache.TryGetValue(src, out var done)) { mf.sharedMesh = done; return; }
            if (!src.isReadable) { leafMeshCache[src] = src; return; }
            var mesh = Instantiate(src); mesh.name = src.name + "_Leaf";
            var v = mesh.vertices; var nrm = mesh.normals;
            if (nrm == null || nrm.Length != v.Length) { leafMeshCache[src] = src; return; }
            // 同じ位置の点はつなげる（面の向きで分かれた点も同じかたまりに）
            var parent = new int[v.Length];
            for (int i = 0; i < v.Length; i++) parent[i] = i;
            System.Func<int, int> find = null;
            find = a => { while (parent[a] != a) { parent[a] = parent[parent[a]]; a = parent[a]; } return a; };
            var weld = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < v.Length; i++)
            {
                var key = new Vector3Int(Mathf.RoundToInt(v[i].x * 1000f), Mathf.RoundToInt(v[i].y * 1000f), Mathf.RoundToInt(v[i].z * 1000f));
                if (weld.TryGetValue(key, out int o)) parent[find(i)] = find(o); else weld[key] = i;
            }
            var leafV = new HashSet<int>();
            int subs = Mathf.Min(mesh.subMeshCount, sm.Length);
            for (int s = 0; s < subs; s++)
            {
                if (sm[s] == null || !IsLeafMat(sm[s].name)) continue;
                var t = mesh.GetTriangles(s);
                for (int k = 0; k < t.Length; k += 3)
                {
                    parent[find(t[k + 1])] = find(t[k]); parent[find(t[k + 2])] = find(t[k]);
                    leafV.Add(t[k]); leafV.Add(t[k + 1]); leafV.Add(t[k + 2]);
                }
            }
            var sum = new Dictionary<int, Vector3>(); var cnt = new Dictionary<int, int>();
            foreach (int i in leafV) { int g = find(i); sum.TryGetValue(g, out var a); sum[g] = a + v[i]; cnt.TryGetValue(g, out int c); cnt[g] = c + 1; }
            foreach (int i in leafV)
            {
                int g = find(i);
                Vector3 ctr = sum[g] / cnt[g];
                Vector3 sp = v[i] - ctr;
                if (sp.sqrMagnitude < 1e-8f) continue;
                nrm[i] = Vector3.Slerp(nrm[i].normalized, sp.normalized, 0.85f).normalized;
            }
            mesh.normals = nrm;
            mesh.RecalculateTangents();
            leafMeshCache[src] = mesh;
            mf.sharedMesh = mesh;
        }

        static bool IsLeafMat(string n) { return n.StartsWith("Leaf_") || n.StartsWith("Flower"); }

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

        Material Tiled(string tile, float u, float v) { return TilePBR.Apply(ProtoArt.Mat(Color.white, T(tile), new Vector2(Mathf.Max(1f, u), Mathf.Max(1f, v)), 0.1f), tile); }

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
                var go = Box("Path", new Vector3(c.x, game ? -0.01f : 0.03f, c.y), new Vector3(width, game ? 0.05f : 0.06f, len + width * 0.6f), Tiled("Tile_Flagstone", width / 2f, len / 2f));
                float yaw = Mathf.Atan2(b.x - a.x, b.y - a.y) * Mathf.Rad2Deg;
                go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                if (game && width >= 2f)
                {
                    // 縁石（両わきに低い石。2026-11-02 クオリティアップ）
                    Vector2 n = new Vector2(b.y - a.y, -(b.x - a.x)) / Mathf.Max(0.01f, len);
                    for (int k = -1; k <= 1; k += 2)
                    {
                        Vector2 p = c + n * k * (width / 2f + 0.08f);
                        var cb = Box("Curb", new Vector3(p.x, 0.02f, p.y), new Vector3(0.16f, 0.1f, len), curbMat);
                        cb.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                    }
                }
            }
        }

        // ---------------- 道を1枚の石畳に（2026-11-02） ----------------
        struct PSeg { public Vector2 c, u; public float half, w, ext; public Vector2 lo, hi; }

        /// <summary>道の形の「内か外か」の距離（負＝道の中）。線分は平らなはしの帯、折れ目は円。別々の道どうしは角を少し丸めてつなぐ。</summary>
        static float PathSdf(List<PSeg> segs, List<Vector3> discs, Vector2 p)
        {
            float d = 99f;
            const float k = 0.9f; // 交わる所の丸み（m）
            foreach (var s in segs)
            {
                if (p.x < s.lo.x || p.y < s.lo.y || p.x > s.hi.x || p.y > s.hi.y) continue;
                Vector2 r = p - s.c;
                float t = Mathf.Abs(Vector2.Dot(r, s.u)) - (s.half + s.ext), n = Mathf.Abs(r.x * -s.u.y + r.y * s.u.x) - s.w / 2f;
                float e = new Vector2(Mathf.Max(t, 0f), Mathf.Max(n, 0f)).magnitude + Mathf.Min(Mathf.Max(t, n), 0f);
                d = SMin(d, e, k);
            }
            foreach (var c in discs)
            {
                float dx = p.x - c.x, dy = p.y - c.y;
                if (Mathf.Abs(dx) > c.z + 3f || Mathf.Abs(dy) > c.z + 3f) continue;
                d = Mathf.Min(d, Mathf.Sqrt(dx * dx + dy * dy) - c.z);
            }
            return d;
        }
        static float SMin(float a, float b, float k)
        {
            if (a > 50f) return b;
            float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
            return Mathf.Lerp(b, a, h) - k * h * (1f - h);
        }

        /// <summary>道の網を、重なりのない1枚の石畳（模様は場所で決まるので継ぎ目なし）と、外の線だけの縁石にする。</summary>
        void PathNetwork(List<CampusPlan.PathDef> defs)
        {
            var segs = new List<PSeg>(); var discs = new List<Vector3>();
            Vector2 mn = new Vector2(1e9f, 1e9f), mx = -mn;
            foreach (var p in defs)
                for (int i = 0; i + 1 < p.pts.Length; i++)
                {
                    Vector2 a = p.pts[i], b = p.pts[i + 1], d = b - a;
                    var s = new PSeg { c = (a + b) / 2f, u = d.normalized, half = d.magnitude / 2f, w = p.w, ext = 0f };
                    if (i == 0) s.ext = Mathf.Max(s.ext, 0f);
                    float m = s.half + p.w + 2f;
                    s.lo = s.c - new Vector2(m, m); s.hi = s.c + new Vector2(m, m);
                    segs.Add(s);
                    if (i > 0) discs.Add(new Vector3(a.x, a.y, p.w / 2f));
                    mn = Vector2.Min(mn, Vector2.Min(a, b)); mx = Vector2.Max(mx, Vector2.Max(a, b));
                }
            mn -= Vector2.one * 4f; mx += Vector2.one * 4f;
            const float cs = 0.25f;
            int nx = Mathf.CeilToInt((mx.x - mn.x) / cs) + 1, ny = Mathf.CeilToInt((mx.y - mn.y) / cs) + 1;
            var f = new float[nx * ny];
            for (int j = 0; j < ny; j++) for (int i = 0; i < nx; i++) f[j * nx + i] = PathSdf(segs, discs, new Vector2(mn.x + i * cs, mn.y + j * cs));

            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            var cv = new List<Vector3>(); var cuv = new List<Vector2>(); var ctri = new List<int>();
            const float py = 0.015f, curbW = 0.18f, curbH = 0.07f;
            var poly = new List<Vector2>(8); var cross = new List<Vector2>(4);
            for (int j = 0; j + 1 < ny; j++)
                for (int i = 0; i + 1 < nx; i++)
                {
                    // 角は 左下・右下・右上・左上 の順
                    int[] ix = { i, i + 1, i + 1, i }, jy = { j, j, j + 1, j + 1 };
                    float[] val = new float[4]; int inside = 0;
                    for (int q = 0; q < 4; q++) { val[q] = f[jy[q] * nx + ix[q]]; if (val[q] < 0f) inside++; }
                    if (inside == 0) continue;
                    poly.Clear(); cross.Clear();
                    for (int q = 0; q < 4; q++)
                    {
                        int r = (q + 1) % 4;
                        Vector2 pq = new Vector2(mn.x + ix[q] * cs, mn.y + jy[q] * cs), pr = new Vector2(mn.x + ix[r] * cs, mn.y + jy[r] * cs);
                        if (val[q] < 0f) poly.Add(pq);
                        if ((val[q] < 0f) != (val[r] < 0f))
                        {
                            // 同じ辺はどちらのマスからも同じ点になるように、格子の順で計算する
                            bool sw = (jy[q] > jy[r]) || (jy[q] == jy[r] && ix[q] > ix[r]);
                            Vector2 A = sw ? pr : pq, B = sw ? pq : pr; float va = sw ? val[r] : val[q], vb = sw ? val[q] : val[r];
                            Vector2 e = Vector2.Lerp(A, B, va / (va - vb));
                            poly.Add(e); cross.Add(e);
                        }
                    }
                    int b0 = v.Count;
                    foreach (var p in poly) { v.Add(new Vector3(p.x, H(p.x, p.y) + py, p.y)); uv.Add(p * 0.5f); }
                    for (int t = 1; t + 1 < poly.Count; t++)
                    {
                        // 上を向く面にする（Unity は Cross(b-a, c-a) の向きが表）
                        bool up = Vector3.Cross(v[b0 + t] - v[b0], v[b0 + t + 1] - v[b0]).y > 0f;
                        tri.Add(b0); tri.Add(up ? b0 + t : b0 + t + 1); tri.Add(up ? b0 + t + 1 : b0 + t);
                    }
                    // 縁石（外の線にそって）
                    for (int c = 0; c + 1 < cross.Count; c += 2)
                    {
                        Vector2 p0 = cross[c], p1 = cross[c + 1];
                        Vector2 n0 = SdfGrad(segs, discs, p0), n1 = SdfGrad(segs, discs, p1);
                        float h0 = H(p0.x, p0.y), h1 = H(p1.x, p1.y);
                        Vector3 a0 = new Vector3(p0.x, h0 + curbH, p0.y), a1 = new Vector3(p1.x, h1 + curbH, p1.y);
                        Vector3 o0 = new Vector3(p0.x + n0.x * curbW, h0 + curbH, p0.y + n0.y * curbW), o1 = new Vector3(p1.x + n1.x * curbW, h1 + curbH, p1.y + n1.y * curbW);
                        Vector3 i0 = new Vector3(p0.x, h0 + py, p0.y), i1 = new Vector3(p1.x, h1 + py, p1.y);
                        Vector3 g0 = new Vector3(o0.x, h0 - 0.03f, o0.z), g1 = new Vector3(o1.x, h1 - 0.03f, o1.z);
                        float L = (p1 - p0).magnitude, u0 = (p0.x + p0.y) * 0.5f;
                        Vector3 nIn = new Vector3(-(n0.x + n1.x), 0f, -(n0.y + n1.y));
                        Quad(cv, cuv, ctri, i0, i1, a1, a0, u0, L, curbH, nIn);
                        Quad(cv, cuv, ctri, a0, a1, o1, o0, u0, L, curbW, Vector3.up);
                        Quad(cv, cuv, ctri, o0, o1, g1, g0, u0, L, curbH, -nIn);
                    }
                }
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, name = "PathNetwork" };
            mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0);
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            var go = new GameObject("PathNetwork");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = TilePBR.Apply(ProtoArt.Mat(Color.white, T("Tile_Flagstone"), null, 0.1f), "Tile_Flagstone");
            var cm = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, name = "PathCurbs" };
            cm.SetVertices(cv); cm.SetUVs(0, cuv); cm.SetTriangles(ctri, 0);
            cm.RecalculateNormals(); cm.RecalculateTangents(); cm.RecalculateBounds();
            var cgo = new GameObject("PathCurbs");
            cgo.transform.SetParent(root, false);
            cgo.AddComponent<MeshFilter>().sharedMesh = cm;
            cgo.AddComponent<MeshRenderer>().sharedMaterial = TilePBR.Apply(ProtoArt.Mat(new Color(0.86f, 0.83f, 0.78f), T("Tile_StoneTrim") != null ? T("Tile_StoneTrim") : T("Tile_StoneWall"), null, 0.1f), T("Tile_StoneTrim") != null ? "Tile_StoneTrim" : "Tile_StoneWall");
        }

        static Vector2 SdfGrad(List<PSeg> segs, List<Vector3> discs, Vector2 p)
        {
            const float e = 0.05f;
            float dx = PathSdf(segs, discs, p + new Vector2(e, 0f)) - PathSdf(segs, discs, p - new Vector2(e, 0f));
            float dy = PathSdf(segs, discs, p + new Vector2(0f, e)) - PathSdf(segs, discs, p - new Vector2(0f, e));
            var g = new Vector2(dx, dy);
            return g.sqrMagnitude > 1e-8f ? g.normalized : Vector2.up;
        }

        /// <summary>4点の面を、表が face の向きになるように。u は長さの方向に 1m＝0.5。</summary>
        static void Quad(List<Vector3> v, List<Vector2> uv, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d, float u0, float len, float h, Vector3 face)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            uv.Add(new Vector2(u0, 0f)); uv.Add(new Vector2(u0 + len * 0.5f, 0f)); uv.Add(new Vector2(u0 + len * 0.5f, h * 0.5f)); uv.Add(new Vector2(u0, h * 0.5f));
            bool ok = Vector3.Dot(Vector3.Cross(b - a, c - a), face) >= 0f;
            if (ok) { t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3); }
            else { t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2); }
        }

        void Lawn(float x, float y, float r)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Lawn";
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(x, game ? -0.02f : 0.04f, y);
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
                    verts.Add(new Vector3(x, H(x, y) - (game ? 0.04f : 0f), y));
                    uvs.Add(game ? new Vector2((x - x0) / (x1 - x0), (y - y0) / (y1 - y0)) : new Vector2(x / 4f, y / 4f)); // 新しい学園：島全体で 0〜1（色むらの画像）
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
            go.AddComponent<MeshRenderer>().sharedMaterial = game ? GroundMaterial(x0, y0, x1, y1) : grass;

            // 湖（島のまわりと街の前。新しい学園では遠くの山まで）
            var w = GameObject.CreatePrimitive(PrimitiveType.Plane);
            w.name = "Lake";
            w.transform.SetParent(root, false);
            w.transform.localPosition = new Vector3(20f, WaterY, 40f);
            w.transform.localScale = game ? new Vector3(240f, 1f, 240f) : new Vector3(70f, 1f, 50f);
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
            // 正門の扉（依頼21。木と鉄の両開き）。跳ね橋の鎖に当たらないよう、城の内側へ開いた形で置く
            // ちょうつがい：Blender (±3.56, -3.14, 0.04) → 門の向き 180° で (∓3.56, 0.04, -3.14)
            gateL = Put("Gate_Door", -3.56f, 0.04f, 5f - 3.14f, GateOpen ? 90f : 180f);
            gateR = Put("Gate_Door", 3.56f, 0.04f, 5f - 3.14f, GateOpen ? -90f : 180f, new Vector3(-1f, 1f, 1f));
            gateK = GateOpen ? 1f : 0f;
            Put("Drawbridge", 0f, -0.8f, 5f - 15.64f, 180f);
            // 街：石の護岸と家並み（背景）
            const float qz = -24f;
            for (float x = -135f; x <= 165f; x += 10f) Put("Quay", x, -4f, qz - 1.5f, 0f);
            Box("Town_Ground", new Vector3(15f, -0.05f, -60f), new Vector3(320f, 0.1f, 70f), Tiled("Tile_Flagstone", 160f, 35f));
            if (game && Has("Town_House_A"))
            {
                // 依頼23 の街の家（2階建て・塔つき・店）を4列に、間かく・大きさ・向きを少しずつ変えて並べる
                string[] kinds = { "Town_House_A", "Town_House_B", "Town_Shop", "Town_House_A" };
                float[] rows = { -33f, -47f, -62f, -77f };
                for (int r = 0; r < rows.Length; r++)
                {
                    float x = -134f + r * 3.1f;
                    int k = r * 7;
                    while (x < 166f)
                    {
                        float h = Hash01(k, r * 31 + 5);
                        string kind = kinds[(int)(h * 4f) % 4];
                        float sc = 0.9f + Hash01(r, k) * 0.25f;
                        float yaw = (r % 2 == 0 ? 0f : 180f) + (Hash01(k * 3, r) - 0.5f) * 8f;
                        Put(kind, x, 0f, rows[r] + (Hash01(k, r) - 0.5f) * 2f, yaw, Vector3.one * sc);
                        x += (kind == "Town_House_B" ? 9.6f : 8.8f) * sc + Hash01(k * 7, r) * 1.6f;
                        k++;
                    }
                }
            }
            else
            {
                int t = 0;
                for (float x = -130f; x <= 160f; x += 42f, t++)
                {
                    Put("Town_Block", x, 0f, -44f, (t % 2 == 0) ? 0f : 180f);
                    Put("Town_Block", x + 21f, 0f, -74f, (t % 2 == 0) ? 180f : 0f);
                }
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

        void ArcadeLine(Vector2 ctr, Vector2 a, Vector2 b, float depth = 1f, float extA = 0f)
        {
            a -= (b - a).normalized * extA; // つなぎ目で隣の廊下と屋根を重ねる分
            Vector2 d = b - a; float L = d.magnitude; int k = Mathf.Max(1, Mathf.RoundToInt(L / 3f)); float seg = L / k;
            for (int s = 0; s < k; s++)
            {
                Vector2 p = a + d.normalized * (seg * (s + 0.5f));
                Vector2 n = new Vector2(-d.y, d.x).normalized; if (Vector2.Dot(n, ctr - p) < 0f) n = -n;
                Put("Arcade", p.x, 0f, p.y, YawFacing(n), new Vector3(seg / 3f, 1f, depth));
            }
        }

        /// <summary>折れ目で屋根のすき間が出ないように、両方の廊下を伸ばして重ねる長さ（奥行 3m の部品）。</summary>
        static float Miter(Vector2 d0, Vector2 d1)
        {
            float ang = Vector2.Angle(d0, d1);
            return 1.5f * Mathf.Tan(ang * 0.5f * Mathf.Deg2Rad) + 0.05f;
        }

        /// <summary>地上の廊下（屋根つき）を折れ線で。部品の長さの方向（X）を道の向きに（2026-11-02：前は横向きに並んでいた）。</summary>
        void Walkway(params Vector2[] pts)
        {
            if (game)
            {
                // 中庭の曲線の渡り廊下にぶつかる所は、はしを渡り廊下の外の軒の下（中心線から 2.0m）にそろえる
                var last = pts[pts.Length - 1];
                if (CurveDist(last) < 6f)
                {
                    Vector2 a = pts[pts.Length - 2], dir = (last - a).normalized;
                    float bestT = 0f, bestE = 1e9f;
                    for (float tt = -5f; tt <= 5f; tt += 0.05f) { float e = Mathf.Abs(CurveDist(last + dir * tt) - 2.0f); if (e < bestE) { bestE = e; bestT = tt; } }
                    pts = (Vector2[])pts.Clone();
                    pts[pts.Length - 1] = last + dir * bestT;
                }
            }
            for (int i = 0; i + 1 < pts.Length; i++)
            {
                Vector2 a = pts[i], b = pts[i + 1], dn = (b - a).normalized;
                float e0 = i > 0 ? Miter(pts[i] - pts[i - 1], b - a) : 0f, e1 = i + 2 < pts.Length ? Miter(b - a, pts[i + 2] - b) : 0f;
                a -= dn * e0; b += dn * e1;
                Vector2 d = b - a; float L = d.magnitude; int k = Mathf.Max(1, Mathf.RoundToInt(L / 3f)); float seg = L / k;
                for (int s = 0; s < k; s++)
                {
                    Vector2 p = a + dn * (seg * (s + 0.5f));
                    Put("Arcade", p.x, 0f, p.y, YawAlong(d), new Vector3(seg / 3f * 1.02f, game ? 0.9f : 1f, 1f)); // 少し低くして、渡り廊下の軒の下に入れる
                }
            }
        }

        /// <summary>中庭を囲む曲線の渡り廊下の中心線からの距離。</summary>
        static float CurveDist(Vector2 p)
        {
            float best = 1e9f; Vector2 prev = CampusPlan.ArcadeA;
            for (int i = 1; i <= 40; i++)
            {
                float t = i / 40f; Vector2 a = CampusPlan.ArcadeA, c = CampusPlan.ArcadeC, b = CampusPlan.ArcadeB;
                Vector2 q = (1 - t) * (1 - t) * a + 2 * (1 - t) * t * c + t * t * b;
                best = Mathf.Min(best, DistSeg(p, prev, q)); prev = q;
            }
            return best;
        }

        // ---------------- 寮 ----------------
        void OurDorm()
        {
            // わたしたちの寮（31×24m・3階建て。外見だけの試し。中はあとで）
            float x0 = 46f, x1 = 77f, y0 = 64f, y1 = 88f;
            StoneBox("OurDorm", x0, y0, x1, y1, 0f, 10.5f);
            // 屋根は高さの違う屋根を組み合わせる（絵のように。famuru「直線的すぎる」）
            RoofAlongY(y0, y1, 72f, 10.6f, 10.5f, 6.5f);          // 東の帯（食堂・浴場の上。いちばん高い）
            RoofAlongY(y0 + 4f, y1 - 4f, 49f, 6.6f, 10.5f, 4.6f);  // 西の帯（入口の側）
            RoofAlongX(52f, 67f, 84.6f, 7.4f, 10.5f, 5.2f);       // 北の個室の棟
            RoofAlongX(52f, 67f, 67.4f, 7.4f, 10.5f, 5.2f);       // 南の個室の棟
            RoofAlongX(52f, 67f, 76f, 10.4f, 10.5f, 2.6f);        // 吹き抜けの上（低い屋根）
            for (int i = 0; i < 4; i++) { Put("Dormer", 54f + i * 4f, 11.2f, y0 + 2.2f, 180f); Put("Dormer", 54f + i * 4f, 11.2f, y1 - 2.2f, 0f); }
            for (int i = 0; i < 3; i++) Put("Dormer", 77.3f, 11.3f, 70f + i * 6f, 90f);
            Put("Chimney", 60f, 13.6f, 84.6f, 0f, new Vector3(1.2f, 1.6f, 1.2f)); Put("Chimney", 60f, 13.6f, 67.4f, 0f, new Vector3(1.2f, 1.6f, 1.2f));
            Put("Chimney", 72f, 15.2f, 80f, 0f, new Vector3(1.2f, 1.8f, 1.2f));
            Put("Ivy", x0 - 0.05f, 0f, 70f, -90f); Put("Ivy", 57f, 0f, y0 - 0.05f, 180f); Put("Ivy", 66f, 0f, y1 + 0.05f, 0f);
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
            Put("Dorm_Mage", 61.5f, 0f, 25f, 0f, null, bannerMage); // 正面（サンルーム・玄関）を北の学園の側へ
            Put("Dorm_Tower", -72f, 0f, 29f, 180f, null, bannerTower);
            if (!game) { Put("Greenhouse", -59f, 0f, 99.5f, 180f); Put("Hut", -44.5f, 0f, 99f, 180f); } // 新しい学園では温室と工房は NewDorm.cs（中に入れる）
            Put("Training_Hall", -43f, H(-43f, 124f), 124f, 180f);
            Put("Dungeon_Gate", -67f, H(-67f, 131f), 131f, 180f);
            // 地上の廊下：騎士団の寮→本館の西のはし、塔の寮・魔術師団の寮→中庭の渡り廊下の南のすみ（famuru）
            if (game)
            {
                foreach (var w in CampusPlan.Walkways) Walkway(w);
                return;
            }
            Walkway(new Vector2(-47f, 71f), new Vector2(-40f, 71f), new Vector2(-40f, 81f), new Vector2(-35.5f, 81f));
            Walkway(new Vector2(-58f, 31f), new Vector2(-40f, 35f), new Vector2(-24f, 40f));
            Walkway(new Vector2(52f, 37f), new Vector2(40f, 35f), new Vector2(24f, 40f));
        }

        void Paths()
        {
            if (game)
            {
                // 道は1枚の石畳にまとめる（重なった所は切って1つの形に。交わる所は角を少し丸め、縁石は外の線だけ。famuru 2026-11-02）
                var net = new List<CampusPlan.PathDef>();
                net.Add(new CampusPlan.PathDef(4f, new Vector2(0, 3.5f), new Vector2(0, 11))); // 門の中から（跳ね橋の上には出さない）
                net.AddRange(CampusPlan.Paths);
                net.Add(new CampusPlan.PathDef(2.5f, new Vector2(-52, 92), new Vector2(-72, 94), new Vector2(-87, 99))); // 温室の前から裏門へ
                net.Add(new CampusPlan.PathDef(2.2f, new Vector2(-87, 99), new Vector2(-80, 112), new Vector2(-70, 122)));
                net.Add(new CampusPlan.PathDef(2.2f, new Vector2(-80, 112), new Vector2(-50, 118)));
                PathNetwork(net);
                Lawn(0f, 18f, 7.5f); Lawn(26f, 25f, 4.5f); Lawn(-26f, 25f, 4.5f);
                return;
            }
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
                    if (game && Has("Tree_Pine"))
                    {
                        // 依頼23 の木：奥は針葉樹が多め、手前は広葉樹も（ときどき今の木）
                        float pick = Hash01((int)(px * 10f), (int)(py * 10f)), deep = Mathf.InverseLerp(115f, 140f, py);
                        string kind = pick < 0.35f + deep * 0.3f ? "Tree_Pine" : (pick < 0.85f ? "Tree_Oak" : "Tree");
                        float sc = kind == "Tree_Oak" ? 0.7f + jy * 0.45f : (kind == "Tree_Pine" ? 0.85f + jy * 0.5f : 0.9f + jy * 0.6f);
                        Put(kind, px, H(px, py), py, jx * 360f, Vector3.one * sc);
                    }
                    else Put("Tree", px, H(px, py), py, jx * 360f, Vector3.one * (0.9f + jy * 0.6f));
                    n++;
                }
            // 城壁の中の木（道ぞい）
            var inner = game
                ? new[] { new Vector2(-10, 28), new Vector2(10, 28), new Vector2(-50, 40), new Vector2(52, 52), new Vector2(-24, 99), new Vector2(26, 98), new Vector2(86, 40), new Vector2(-86, 50), new Vector2(-38, 60) }
                : new[] { new Vector2(-14, 40), new Vector2(14, 40), new Vector2(-50, 40), new Vector2(50, 50), new Vector2(-30, 95), new Vector2(30, 98), new Vector2(86, 40), new Vector2(-86, 50) };
            int ii = 0;
            foreach (var p in inner)
            {
                if (game && Has("Tree_Oak")) Put(ii % 3 == 1 ? "Tree_Blossom" : "Tree_Oak", p.x, 0f, p.y, p.x * 7f, Vector3.one * (ii % 3 == 1 ? 1.2f : 0.8f));
                else Put("Tree", p.x, 0f, p.y, p.x * 7f, Vector3.one * 1.1f);
                ii++;
            }
        }

        void Build()
        {
            Ground();
            Cliffs();
            Walls();
            Paths();
            if (game) ArcadeRing(); // 本館・体育館・大講堂・わたしたちの寮は NewDorm.cs（本当の建物）
            else { School(); OurDorm(); }
            OtherDorms();
            Trees();
            if (game) Decorations();
            if (game && Has("Bush_Round")) Nature();
            if (game) { CliffDressing(); if (Has("Bush_Round")) BaseShrubs(); GrassFieldBuild(); StartCoroutine(LawnFix()); }
        }

        static bool Has(string n) { return Resources.Load<GameObject>("Furniture/Furn_" + n) != null; }

        // ---------------- 植物・岩・小物（依頼23。2026-11-02 クオリティアップ） ----------------
        /// <summary>道・建物・芝生から離れているか（草むら・岩・花をばらまくとき）。</summary>
        bool FreeSpot(Vector2 p, float margin, bool lawns = true)
        {
            Rect[] blocks =
            {
                Rect.MinMaxRect(-37, 43, 37, 92), Rect.MinMaxRect(43, 58, 93, 91), Rect.MinMaxRect(-79, 48, -45, 78), Rect.MinMaxRect(-91, 12, -53, 46),
                Rect.MinMaxRect(44, 10, 79, 40), Rect.MinMaxRect(-64, 95, -40, 104), Rect.MinMaxRect(-13, -2, 13, 13), Rect.MinMaxRect(-18, 29, 18, 46),
                Rect.MinMaxRect(-53, 116, -33, 132), Rect.MinMaxRect(-76, 124, -58, 138),
            };
            foreach (var b in blocks) if (p.x > b.xMin - margin && p.x < b.xMax + margin && p.y > b.yMin - margin && p.y < b.yMax + margin) return false;
            if (lawns && (p - new Vector2(0, 18)).magnitude < 10f + margin) return false;
            if (lawns && ((p - new Vector2(26, 25)).magnitude < 5f || (p - new Vector2(-26, 25)).magnitude < 5f)) return false;
            if (!Inside(Island, p)) return false;
            // 城壁のすぐそば
            for (int i = 0; i < WallPts.Length; i++)
            {
                Vector2 a = WallPts[i], b2 = WallPts[(i + 1) % WallPts.Length];
                if (DistSeg(p, a, b2) < 3.5f) return false;
            }
            foreach (var path in CampusPlan.Paths) for (int i = 0; i + 1 < path.pts.Length; i++) if (DistSeg(p, path.pts[i], path.pts[i + 1]) < path.w / 2f + margin) return false;
            foreach (var wk in CampusPlan.Walkways) for (int i = 0; i + 1 < wk.Length; i++) if (DistSeg(p, wk[i], wk[i + 1]) < 1.8f + margin) return false;
            Vector2[][] extra = { new[] { new Vector2(-52, 92), new Vector2(-72, 94), new Vector2(-87, 99), new Vector2(-80, 112), new Vector2(-70, 122) }, new[] { new Vector2(-80, 112), new Vector2(-50, 118) } };
            foreach (var e in extra) for (int i = 0; i + 1 < e.Length; i++) if (DistSeg(p, e[i], e[i + 1]) < 1.2f + margin) return false;
            return true;
        }

        static float DistSeg(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 d = b - a; float l2 = d.sqrMagnitude;
            float t = l2 > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, d) / l2) : 0f;
            return (a + d * t - p).magnitude;
        }

        void Nature()
        {
            // 茂み：中庭の南の渡り廊下の外がわ・寮の前庭のわき・ほかの寮の正面
            var curve = new List<Vector2>();
            for (int i = 1; i < 14; i++) { float t = i / 14f; Vector2 a = CampusPlan.ArcadeA, c = CampusPlan.ArcadeC, b = CampusPlan.ArcadeB; curve.Add((1 - t) * (1 - t) * a + 2 * (1 - t) * t * c + t * t * b); }
            for (int i = 0; i < curve.Count; i++)
            {
                if (i == 6) continue; // 入口
                Vector2 p = curve[i] + (curve[i] - new Vector2(0f, 44f)).normalized * 3.4f;
                Put(i % 3 == 0 ? "Bush_Flower" : "Bush_Round", p.x, 0f, p.y, i * 47f, Vector3.one * (0.9f + Hash01(i, 3) * 0.3f));
            }
            foreach (var p in new[] { new Vector2(36.5f, 72f), new Vector2(36.5f, 76f), new Vector2(36.5f, 80f), new Vector2(43.5f, 59f), new Vector2(-62f + 4.5f, 46.5f - 2.6f), new Vector2(-62f - 4.5f, 46.5f - 2.6f), new Vector2(57f, 42.8f), new Vector2(66f, 42.8f) })
                Put("Bush_Round", p.x, 0f, p.y, p.x * 13f, Vector3.one * (0.95f + Hash01((int)p.x, (int)p.y) * 0.3f));
            // 生け垣：正門から芝生まで・芝生から中庭までの道の両わき
            foreach (float y in new[] { 13.4f, 15.4f, 25.5f, 27.5f, 29.5f })
                for (int k = -1; k <= 1; k += 2) Put("Hedge", k * 3.5f, 0f, y + 1f, 90f);
            // 花壇に花を植える・芝生のふちと原っぱに花の群れ
            var lc = new Vector2(0f, 18f);
            foreach (float a in new[] { 35f, 60f, 120f, 145f, 200f, 235f, 305f, 340f })
            {
                Vector2 d = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
                Vector2 p = lc + d * 5.6f;
                Put("FlowerPatch", p.x, 0.22f, p.y, YawFacing(-d), new Vector3(0.75f, 1f, 0.7f));
            }
            foreach (var p in new[] { new Vector2(-3.4f, 35.5f), new Vector2(3.4f, 35.5f), new Vector2(42.6f, 67f), new Vector2(37.4f, 67f) })
                Put("FlowerPatch", p.x, 0.22f, p.y, 0f, new Vector3(0.75f, 1f, 0.7f));
            int placedFlowers = 0, placedGrass = 0, placedRocks = 0;
            for (int i = 0; i < 900 && (placedFlowers < 70 || placedGrass < 320); i++)
            {
                float x = -100f + Hash01(i, 11) * 200f, y = 2f + Hash01(i, 23) * 150f;
                var p = new Vector2(x, y);
                bool forest = y > 108f;
                if (!FreeSpot(p, 0.8f)) continue;
                if (!forest && placedFlowers < 70 && Hash01(i, 37) < 0.3f) { Put("FlowerPatch", x, H(x, y), y, Hash01(i, 41) * 360f, Vector3.one * (0.8f + Hash01(i, 43) * 0.5f)); placedFlowers++; continue; }
                if (placedGrass < 320) { Put("GrassTuft", x, H(x, y) - 0.03f, y, Hash01(i, 47) * 360f, Vector3.one * (0.8f + Hash01(i, 53) * 0.7f)); placedGrass++; }
            }
            // 岩：森と丘、島のふち
            for (int i = 0; i < 400 && placedRocks < 46; i++)
            {
                float x = -112f + Hash01(i, 61) * 224f, y = 4f + Hash01(i, 67) * 152f;
                var p = new Vector2(x, y);
                bool edge = !Inside(Island, p + new Vector2(0f, 6f)) || !Inside(Island, p + new Vector2(6f, 0f)) || !Inside(Island, p - new Vector2(6f, 0f)) || !Inside(Island, p - new Vector2(0f, 6f));
                if (!(y > 112f || edge)) continue;
                if (!FreeSpot(p, 1.5f)) continue;
                bool big = Hash01(i, 71) < 0.35f;
                Put(big ? "Rock_Large" : "Rock_Small", x, H(x, y) - (big ? 0.25f : 0.05f), y, Hash01(i, 73) * 360f, Vector3.one * (0.7f + Hash01(i, 79) * 0.8f));
                placedRocks++;
            }
            // 正門の内の松明・かがり火・旗竿・案内板
            foreach (int k in new[] { -1, 1 })
            {
                var tch = Put("WallTorch", k * 4.6f, 2.3f, 10.05f, 0f);
                AddFireLight(tch, new Vector3(0f, 0.75f, -0.25f), 1.6f, 6f);
                var br = Put("Brazier", k * 5f, 0f, 10.8f, 0f);
                AddFireLight(br, new Vector3(0f, 1.3f, 0f), 2.2f, 8f);
                var br2 = Put("Brazier", k * 3.2f, 0f, 31.2f, 0f);
                AddFireLight(br2, new Vector3(0f, 1.3f, 0f), 2.2f, 8f);
                Put("BannerPole", k * 6.5f, 0f, 11.5f, 0f, null, bannerOurs);
                Put("BannerPole", k * 6.2f, 0f, 30f, 0f, null, bannerOurs);
            }
            Put("Signpost", 2.1f, 0f, 19.9f, -45f);
            Put("Signpost", -44f, 0f, 93.2f, 20f);
            // 庭の灯り：魔術師団の寮への道・寮の前庭への道・本館の北の道
            for (float x = 44f; x < 60f; x += 5f) { Put("Lamp_Garden", x, 0f, 38.7f, 0f); Put("Lamp_Garden", x + 2.5f, 0f, 42.2f, 0f); }
            for (float y = 64.5f; y < 70f; y += 2.6f) { Put("Lamp_Garden", 38.1f, 0f, y, 0f); Put("Lamp_Garden", 41.9f, 0f, y, 0f); }
            for (float x = -4f; x > -28f; x -= 6f) Put("Lamp_Garden", x, 0f, 97.6f, 0f);
            if (lampGlass != null) sky.AddLamp(null, lampGlass);
        }

        void AddFireLight(GameObject h, Vector3 local, float intensity, float range)
        {
            if (h == null) return;
            var l = new GameObject("FireLight").AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(1f, 0.62f, 0.3f); l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
            l.transform.SetParent(h.transform, false);
            l.transform.localPosition = local;
            sky.AddLamp(l, null);
        }

        // ---------------- 芝生の色むら（2026-11-02 クオリティアップ） ----------------
        /// <summary>島全体の色むらの画像（緑の濃淡・乾いた所・森の下草・道のまわりの踏まれた土・小さな花）に、細かい草の模様を重ねた材質。</summary>
        Material GroundMaterial(float x0, float y0, float x1, float y1)
        {
            const int W = 1024;
            int Hh = Mathf.RoundToInt(W * (y1 - y0) / (x1 - x0));
            var segs = new List<Vector4>(); var widths = new List<float>();
            foreach (var p in CampusPlan.Paths) for (int i = 0; i + 1 < p.pts.Length; i++) { segs.Add(new Vector4(p.pts[i].x, p.pts[i].y, p.pts[i + 1].x, p.pts[i + 1].y)); widths.Add(p.w); }
            foreach (var wk in CampusPlan.Walkways) for (int i = 0; i + 1 < wk.Length; i++) { segs.Add(new Vector4(wk[i].x, wk[i].y, wk[i + 1].x, wk[i + 1].y)); widths.Add(3f); }
            segs.Add(new Vector4(0, 0, 0, 11)); widths.Add(4f);
            segs.Add(new Vector4(-52, 92, -72, 94)); widths.Add(2.5f); segs.Add(new Vector4(-72, 94, -87, 99)); widths.Add(2.5f);
            segs.Add(new Vector4(-87, 99, -80, 112)); widths.Add(2.2f); segs.Add(new Vector4(-80, 112, -70, 122)); widths.Add(2.2f); segs.Add(new Vector4(-80, 112, -50, 118)); widths.Add(2.2f);
            var px = new Color[W * Hh];
            Color dark = new Color(0.36f, 0.50f, 0.17f), light = new Color(0.55f, 0.66f, 0.25f), dry = new Color(0.68f, 0.66f, 0.34f), forest = new Color(0.22f, 0.33f, 0.14f), dirt = new Color(0.50f, 0.43f, 0.30f), litter = new Color(0.36f, 0.30f, 0.18f);
            Color[] petals = { new Color(0.97f, 0.96f, 0.92f), new Color(0.98f, 0.72f, 0.80f), new Color(0.98f, 0.86f, 0.35f), new Color(0.62f, 0.56f, 0.92f) };
            for (int j = 0; j < Hh; j++)
            {
                float wy = y0 + (j + 0.5f) / Hh * (y1 - y0);
                for (int i = 0; i < W; i++)
                {
                    float wx = x0 + (i + 0.5f) / W * (x1 - x0);
                    float n1 = Mathf.PerlinNoise(wx * 0.035f + 11f, wy * 0.035f + 7f), n2 = Mathf.PerlinNoise(wx * 0.13f + 50f, wy * 0.13f + 50f);
                    Color c = Color.Lerp(dark, light, n1 * 0.7f + n2 * 0.3f);
                    float n3 = Mathf.PerlinNoise(wx * 0.05f + 90f, wy * 0.05f + 3f);
                    if (n3 > 0.62f) c = Color.Lerp(c, dry, Mathf.Clamp01((n3 - 0.62f) * 4f) * 0.6f);
                    float f = Mathf.SmoothStep(0f, 1f, (wy - 106f) / 14f);
                    if (f > 0f)
                    {
                        c = Color.Lerp(c, forest, f * 0.75f);
                        if (Mathf.PerlinNoise(wx * 0.2f + 5f, wy * 0.2f) > 0.6f) c = Color.Lerp(c, litter, f * 0.5f);
                    }
                    // 道のまわりの踏まれた土
                    float best = 999f, bw = 3f;
                    for (int k = 0; k < segs.Count; k++)
                    {
                        Vector4 s4 = segs[k];
                        float dx = s4.z - s4.x, dy = s4.w - s4.y, l2 = dx * dx + dy * dy;
                        float t = l2 > 0f ? Mathf.Clamp01(((wx - s4.x) * dx + (wy - s4.y) * dy) / l2) : 0f;
                        float ex = s4.x + dx * t - wx, ey = s4.y + dy * t - wy;
                        float d = Mathf.Sqrt(ex * ex + ey * ey) - widths[k] / 2f;
                        if (d < best) { best = d; bw = widths[k]; }
                    }
                    if (best < 1.4f) c = Color.Lerp(c, dirt, Mathf.Clamp01(1f - best / 1.4f) * (0.45f + n2 * 0.25f));
                    // 小さな花（道から離れた原っぱ。森は少なめ）
                    if (best > 2.5f)
                    {
                        float dens = Mathf.PerlinNoise(wx * 0.06f + 200f, wy * 0.06f + 40f);
                        float h = Hash01(i, j);
                        if (dens > 0.55f && h < (dens - 0.55f) * 0.25f * (1f - f * 0.7f)) c = petals[(int)(Hash01(j, i) * 4f) % 4];
                    }
                    px[j * W + i] = c;
                }
            }
            var tex = new Texture2D(W, Hh, TextureFormat.RGB24, true) { wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
            tex.SetPixels(px); tex.Apply(true);
            groundTex = tex; groundRect = Rect.MinMaxRect(x0, y0, x1, y1);
            // 細かい草の模様（灰色の 0.5 前後。×2 で重ねる）
            const int D = 256;
            var dp = new Color[D * D];
            for (int y = 0; y < D; y++)
                for (int x = 0; x < D; x++)
                {
                    float v = 0.5f + (Mathf.PerlinNoise(x * 0.35f, y * 0.06f) - 0.5f) * 0.32f + (Mathf.PerlinNoise(x * 0.08f + 3f, y * 0.08f) - 0.5f) * 0.18f + (Hash01(x, y) - 0.5f) * 0.10f;
                    dp[y * D + x] = new Color(v, v * 1.02f, v * 0.97f);
                }
            Texture2D det = T("Tile_GrassDetail"); // 依頼24 の芝の灰色の版があればそれを使う
            if (det == null) { det = new Texture2D(D, D, TextureFormat.RGB24, true) { wrapMode = TextureWrapMode.Repeat }; det.SetPixels(dp); det.Apply(true); }
            var m = ProtoArt.MatNew(Color.white, tex, null, 0.04f);
            m.SetTexture("_DetailAlbedoMap", det);
            m.SetFloat("_DetailAlbedoMapScale", 0.6f); // 細かい草の模様は弱めに（地面が暗く沈むため）
            m.SetTextureScale("_DetailAlbedoMap", new Vector2((x1 - x0) / 1.6f, (y1 - y0) / 1.6f));
            m.EnableKeyword("_DETAIL_MULX2");
            var dn = T("Tile_GrassDetail_Normal"); // 依頼24 の芝の凹凸（草の葉の重なり）
            if (dn != null) { m.SetTexture("_DetailNormalMap", dn); m.SetFloat("_DetailNormalMapScale", 0.8f); }
            return m;
        }

        Texture2D groundTex; Rect groundRect;

        // ---------------- 草（GrassField）と崖の下の岩（2026-11-02 famuru「草地・水面・崖がのっぺり」） ----------------
        void GrassFieldBuild()
        {
            var sh = Shader.Find("LifeSim/Foliage");
            if (sh == null || groundTex == null) return;
            var mat = new Material(sh) { name = "GrassField" };
            mat.SetFloat("_UseVertexColor", 1f); mat.SetFloat("_UseVertexAlpha", 1f);
            mat.SetFloat("_WindStrength", 0.09f); mat.SetFloat("_WindSpeed", 1.7f); mat.SetFloat("_WindScale", 0.12f);
            mat.SetFloat("_Wrap", 0f); mat.SetFloat("_UpNormal", 1f); mat.SetFloat("_TipLight", 0f); mat.SetFloat("_Translucency", 0f);
            mat.SetFloat("_FadeStart", 50f); mat.SetFloat("_FadeEnd", 72f); mat.SetFloat("_Cull", 0f);
            var pos = new List<Vector3>(); var yaw = new List<float>(); var sc = new List<float>(); var col = new List<Color>();
            const float step = 0.62f;
            int ix = 0;
            for (float y = 1f; y < 150f; y += step, ix++)
            {
                int jx = 0;
                for (float x = -110f; x < 110f; x += step, jx++)
                {
                    float px = x + (Hash01(ix, jx * 3 + 1) - 0.5f) * step, py = y + (Hash01(jx, ix * 5 + 2) - 0.5f) * step;
                    var p = new Vector2(px, py);
                    // 原っぱの中で、むらを付ける（すき間がある方が自然）
                    float dens = Mathf.PerlinNoise(px * 0.09f + 13f, py * 0.09f + 71f);
                    if (dens < 0.24f) continue;
                    if (Mathf.Abs(px) < 2.6f && py < 11.6f) continue; // 正門の中の道
                    if (!FreeSpot(p, 0.35f, true)) continue; // 刈った芝生（丸い芝生）には生やさない
                    if (CurveDist(p) < 2.3f) continue; // 曲線の渡り廊下の床
                    float u = (px - groundRect.xMin) / groundRect.width, v = (py - groundRect.yMin) / groundRect.height;
                    Color g = groundTex.GetPixelBilinear(u, v) * 0.85f; // 地面の色（地面は細かい模様と陰で少し暗く見えるので合わせる）
                    float bright = 1f + (Hash01(jx * 7, ix) - 0.5f) * 0.12f;
                    col.Add(new Color(g.r * bright, g.g * bright, g.b * bright, 1f));
                    Vector3 local = new Vector3(px, H(px, py), py);
                    pos.Add(root.TransformPoint(local));
                    yaw.Add(Hash01(ix * 11, jx) * 360f);
                    sc.Add(0.75f + dens * 0.4f + Hash01(jx, ix * 13) * 0.2f);
                }
            }
            var gf = gameObject.AddComponent<GrassField>();
            gf.Build(pos, yaw, sc, col, mat);
            Debug.Log("[CampusPreview] 草の束 " + pos.Count);
        }

        /// <summary>芝生の模様（Tile_Grass）は依頼24 の版が暗い（平均 0.26,0.34,0.17）。新しい学園の芝生をぜんぶ、案3の明るい黄緑にそろえる（寮・学校の中庭も。建物が組み終わった次のコマで）。</summary>
        System.Collections.IEnumerator LawnFix()
        {
            yield return null; yield return null;
            var cache = new Dictionary<Material, Material>();
            Color target = new Color(0.42f, 0.52f, 0.21f); // 芝生の色（光を当てる前）。原っぱより少し明るく黄みに
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                var sm = r.sharedMaterials; bool ch = false;
                for (int i = 0; i < sm.Length; i++)
                {
                    var m = sm[i];
                    if (m == null || !m.HasProperty("_BaseMap")) continue;
                    var tx = m.GetTexture("_BaseMap");
                    if (tx == null || tx.name != "Tile_Grass") continue;
                    if (!cache.TryGetValue(m, out var nm))
                    {
                        bool v2 = tx.height >= 2048;
                        Color avg = v2 ? new Color(0.264f, 0.343f, 0.174f) : new Color(0.481f, 0.597f, 0.325f);
                        nm = new Material(m) { name = m.name + "_Lawn" };
                        nm.SetColor("_BaseColor", new Color(target.r / avg.r, target.g / avg.g, target.b / avg.b));
                        if (nm.HasProperty("_Smoothness")) nm.SetFloat("_Smoothness", 0.05f);
                        cache[m] = nm;
                    }
                    sm[i] = nm; ch = true;
                }
                if (ch) r.sharedMaterials = sm;
            }
            Debug.Log("[CampusPreview] 芝生の材質をそろえた " + cache.Count);
        }

        /// <summary>城壁の内の根元と道のふちに植え込み、ところどころ糸杉（細長い木）。案3の絵は建物の根元と道のふちに必ず緑がある（屋外の作り方.md の 4.）。</summary>
        void BaseShrubs()
        {
            var ctr = new Vector2(0f, 60f);
            int k = 0;
            for (int i = 0; i < WallPts.Length; i++)
            {
                Vector2 a = WallPts[i], b = WallPts[(i + 1) % WallPts.Length];
                Vector2 d = b - a; float L = d.magnitude; d /= L;
                Vector2 n = new Vector2(-d.y, d.x);
                if (Vector2.Dot(n, ctr - (a + b) / 2f) < 0f) n = -n; // 内向き
                for (float t = 2.5f; t < L - 2.5f; t += 3.2f + Hash01(i, (int)t) * 1.6f, k++)
                {
                    Vector2 p = a + d * t + n * (4.3f + Hash01(k, 5) * 0.8f);
                    if (!FreeSpot(p, 0.4f, true)) continue;
                    if (k % 5 == 2)
                    {
                        Put("Tree_Pine", p.x, H(p.x, p.y), p.y, Hash01(k, 9) * 360f, new Vector3(0.5f, 1.05f + Hash01(k, 3) * 0.3f, 0.5f)); // 糸杉の代わり（細長く）
                        continue;
                    }
                    Put(k % 3 == 0 ? "Bush_Flower" : "Bush_Round", p.x, H(p.x, p.y), p.y, Hash01(k, 7) * 360f, Vector3.one * (1.0f + Hash01(k, 11) * 0.45f));
                }
            }
            // 道のふち：ところどころ茂み（すき間をあけて）
            foreach (var path in CampusPlan.Paths)
                for (int i = 0; i + 1 < path.pts.Length; i++)
                {
                    Vector2 a = path.pts[i], b = path.pts[i + 1], d = b - a; float L = d.magnitude; d /= L;
                    Vector2 n = new Vector2(-d.y, d.x);
                    for (float t = 3f; t < L - 3f; t += 5.5f + Hash01(i, (int)(t * 3f)) * 4f, k++)
                        for (int sd = -1; sd <= 1; sd += 2)
                        {
                            if (Hash01(k, sd + 20) < 0.45f) continue;
                            Vector2 p = a + d * t + n * sd * (path.w / 2f + 0.95f);
                            if (!FreeSpot(p, 0.25f, true)) continue;
                            Put(Hash01(k, sd + 30) < 0.3f ? "Bush_Flower" : "Bush_Round", p.x, H(p.x, p.y), p.y, Hash01(k, sd + 40) * 360f, Vector3.one * (0.7f + Hash01(k, sd + 50) * 0.3f));
                        }
                }
            // 正門の内と中庭の入口の両わきに糸杉
            foreach (var p in new[] { new Vector2(-8.6f, 13f), new Vector2(8.6f, 13f), new Vector2(-5.2f, 33.6f), new Vector2(5.2f, 33.6f) })
                Put("Tree_Pine", p.x, H(p.x, p.y), p.y, p.x * 20f, new Vector3(0.48f, 1.1f, 0.48f));
        }

        /// <summary>崖のふもとの水ぎわに大きな岩を並べて、崖と水のさかいを一直線にしない（依頼26 の崖の岩が届くまで、今の岩で）。</summary>
        void CliffDressing()
        {
            if (!Has("Rock_Large")) return;
            float area = 0f;
            for (int i = 0, j = Island.Length - 1; i < Island.Length; j = i++) area += (Island[j].x * Island[i].y - Island[i].x * Island[j].y);
            bool ccw = area > 0f;
            int k = 0;
            for (int i = 0; i < Island.Length; i++)
            {
                Vector2 a = Island[i], b = Island[(i + 1) % Island.Length];
                Vector2 d = b - a; float L = d.magnitude; d /= L;
                Vector2 n = ccw ? new Vector2(d.y, -d.x) : new Vector2(-d.y, d.x);
                for (float s = 2f; s < L - 1f; s += 4.5f + Hash01(i, (int)s) * 3f, k++)
                {
                    Vector2 p = a + d * s;
                    if (Mathf.Abs(p.x) < 9f && p.y < 14f) continue; // 跳ね橋のまわり
                    float h1 = Hash01(k, 3), h2 = Hash01(k, 7), h3 = Hash01(k, 11);
                    Vector2 q = p + n * (0.3f + h1 * 1.6f) + d * (h2 - 0.5f) * 2f;
                    Put("Rock_Large", q.x, WaterY - 0.9f - h3 * 0.5f, q.y, h2 * 360f, new Vector3(1.4f + h1 * 1.3f, 1.2f + h3 * 1.4f, 1.3f + h2 * 1.2f));
                    if (h3 > 0.45f)
                    {
                        Vector2 r = q + d * (1.6f + h1) + n * 0.8f;
                        Put("Rock_Small", r.x, WaterY - 0.25f, r.y, h1 * 360f, Vector3.one * (1.4f + h2));
                    }
                }
            }
        }

        static float Hash01(int x, int y) { unchecked { uint h = (uint)(x * 73856093 ^ y * 19349663); h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15; return (h & 0xffffff) / 16777215f; } }

        // ---------------- 道の灯り・ベンチ・花壇（2026-11-02 クオリティアップ。今ある部品で） ----------------
        void Decorations()
        {
            var glow = ProtoArt.Glow(new Color(1f, 0.75f, 0.45f), 1.5f);
            Vector2[] lamps =
            {
                new Vector2(2.4f, 13), new Vector2(-2.4f, 13), new Vector2(2.4f, 24), new Vector2(-2.4f, 24), new Vector2(2.4f, 31), new Vector2(-2.4f, 31),
                new Vector2(21.6f, 22f), new Vector2(36.8f, 33f), new Vector2(45.2f, 48), new Vector2(44.2f, 60),
                new Vector2(-21.6f, 22f), new Vector2(-36.8f, 33f), new Vector2(-42.2f, 45.4f), new Vector2(-55, 49.2f),
                new Vector2(2.2f, 93), new Vector2(-16, 97.6f), new Vector2(-38, 96.8f), new Vector2(52, 42.2f),
            };
            foreach (var p in lamps)
            {
                var h = Put("LampPost", p.x, 0f, p.y, 0f);
                if (h == null) continue;
                var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                g.name = "LampGlow";
                Destroy(g.GetComponent<Collider>());
                g.transform.SetParent(h.transform, false);
                g.transform.localPosition = new Vector3(0f, 2.28f, 0f);
                g.transform.localScale = Vector3.one * 0.2f;
                var gr = g.GetComponent<Renderer>(); gr.sharedMaterial = glow; gr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var l = new GameObject("LampLight").AddComponent<Light>();
                l.type = LightType.Point; l.color = new Color(1f, 0.74f, 0.45f); l.intensity = 2.6f; l.range = 9f; l.shadows = LightShadows.None;
                l.transform.SetParent(h.transform, false);
                l.transform.localPosition = new Vector3(0f, 2.2f, 0f);
                sky.AddLamp(l, null);
            }
            sky.AddLamp(null, glow);
            // 正門の内の円い芝生：ベンチ4つ（道のない向き）と花壇
            var lc = new Vector2(0f, 18f);
            foreach (float a in new[] { 53f, 127f, 217f, 323f })
            {
                Vector2 d = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
                Vector2 p = lc + d * 8.9f;
                Put("BenchBack", p.x, 0f, p.y, YawFacing(-d));
            }
            foreach (float a in new[] { 35f, 60f, 120f, 145f, 200f, 235f, 305f, 340f })
            {
                Vector2 d = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
                Vector2 p = lc + d * 5.6f;
                Put("FlowerBed", p.x, 0f, p.y, YawFacing(-d));
            }
            // 中庭の入口の前と、寮の前庭への道ばたにも花壇
            foreach (var p in new[] { new Vector2(-3.4f, 35.5f), new Vector2(3.4f, 35.5f), new Vector2(42.6f, 67f), new Vector2(37.4f, 67f) })
                Put("FlowerBed", p.x, 0f, p.y, 0f);
        }

        // ---------------- 中庭を囲む渡り廊下（本当の学校に合わせる） ----------------
        /// <summary>
        /// 本当の学校（配置図の座標で、教室の棟 y 69〜90・外廊下 y 67〜69・大講堂 x −35〜−17・体育館 x 17〜35（y 44〜67）・中庭 x −17〜17）に合わせる。
        /// 北は学校の外廊下がそのまま渡り廊下。西と東は大講堂・体育館の壁ぞい、南は外へふくらむ曲線で、真ん中に入口（famuru）。
        /// </summary>
        void ArcadeRing()
        {
            var ctr = new Vector2(0f, 55f);
            // 両わきのまっすぐな所：前は奥行を 0.8 倍に細くして曲線の所と屋根の幅が合わなかった → 奥行はそのまま、外へ 0.3m ずらして中庭の側の面は前と同じ所に（2026-11-02）
            Vector2 tA = (CampusPlan.ArcadeC - CampusPlan.ArcadeA).normalized, tB = (CampusPlan.ArcadeC - CampusPlan.ArcadeB).normalized;
            float mA = Miter(new Vector2(0f, -1f), tA), mB = Miter(new Vector2(0f, -1f), tB);
            ArcadeLine(ctr, new Vector2(-16.1f, CampusPlan.ArcadeA.y), new Vector2(-16.1f, 66.5f), 1f, CampusPlan.ArcadeA.x < 0f ? mA : mB);
            ArcadeLine(ctr, new Vector2(16.1f, CampusPlan.ArcadeA.y), new Vector2(16.1f, 66.5f), 1f, CampusPlan.ArcadeA.x < 0f ? mB : mA);
            var curve = new List<Vector2>();
            for (int i = 0; i <= 14; i++)
            {
                float t = i / 14f;
                Vector2 a = CampusPlan.ArcadeA, c = CampusPlan.ArcadeC, b = CampusPlan.ArcadeB;
                curve.Add((1 - t) * (1 - t) * a + 2 * (1 - t) * t * c + t * t * b);
            }
            for (int i = 0; i + 1 < curve.Count; i++)
            {
                Vector2 a = curve[i], b = curve[i + 1], m = (a + b) / 2f, d = b - a;
                Vector2 n = new Vector2(-d.y, d.x).normalized; if (Vector2.Dot(n, ctr - m) < 0f) n = -n;
                if (i == 6 || i == 7) { if (i == 6) Put("Arcade_Gate", curve[7].x, 0f, curve[7].y, YawFacing(-n)); continue; }
                // 両はしは、まっすぐな所と屋根を重ねる分だけ外へ伸ばす
                float ext = (i == 0 || i == curve.Count - 2) ? Miter(new Vector2(0f, -1f), (i == 0 ? d : -d).normalized) : 0f;
                Vector2 mm = m + (i == 0 ? -d.normalized : d.normalized) * (ext * 0.5f);
                Put("Arcade", mm.x, 0f, mm.y, YawFacing(n), new Vector3((d.magnitude + ext) / 3f * 1.04f, 1f, 1f));
            }
        }

        // ---------------- 写真 ----------------
        Camera shotCam;
        float nextPoll;

        void Update()
        {
            // 正門の扉：朝6時に開け、夜9時に閉める（3秒かけて動かす）
            if (game && gateL != null && gateR != null && LoungeWorld.Instance != null)
            {
                float m = LoungeWorld.Instance.MinuteOfDay;
                float want = (m >= 6f * 60f && m < 21f * 60f) ? 1f : 0f;
                if (gateK != want)
                {
                    gateK = Mathf.MoveTowards(gateK, want, Time.unscaledDeltaTime / 3f);
                    float k = Mathf.SmoothStep(0f, 1f, gateK);
                    gateL.transform.localRotation = Quaternion.Euler(0f, Mathf.Lerp(180f, 90f, k), 0f);
                    gateR.transform.localRotation = Quaternion.Euler(0f, Mathf.Lerp(180f, -90f, k), 0f);
                }
            }
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

    /// <summary>
    /// 新しい学園の配置図（famuru 決定。正門が 0,0、x は東、y は北）とゲームの座標の対応、学園の道（歩ける所）。
    /// ゲームの座標：わたしたちの寮の真ん中が 0,0。配置図の東＝＋z、北＝−x（寮は配置図の x 46〜77・y 64〜88、学校は x −35〜35・y 44〜90）。
    /// 2026-11-01 家具のチャット
    /// </summary>
    public static class CampusPlan
    {
        public const float OX = 61.5f, OY = 76f;
        public static Vector3 G(float px, float py, float h = 0f) { return new Vector3(OY - py, h, px - OX); }
        public static Vector3 Origin { get { return G(0f, 0f); } }
        public static readonly Quaternion Rotation = Quaternion.Euler(0f, -90f, 0f);

        /// <summary>中庭の南の渡り廊下（曲線）：はし A・B と、ふくらみの制御点 C。入口は真ん中（y 33.25。中庭の東屋をよける）。</summary>
        public static readonly Vector2 ArcadeA = new Vector2(-15.5f, 44.5f), ArcadeB = new Vector2(15.5f, 44.5f), ArcadeC = new Vector2(0f, 22f);

        /// <summary>ほかの寮の出入り口（0 冒険者寮＝崖の上の塔の寮、1 騎士寮＝騎士団の寮、2 魔法師寮＝魔術師団の寮）。地上の廊下のはし。</summary>
        static readonly Vector2[] MobDoors = { new Vector2(-54.5f, 31f), new Vector2(-46f, 71f), new Vector2(45f, 30f) };
        public static Vector3 MobDoor(int d) { var p = MobDoors[Mathf.Clamp(d, 0, 2)]; return G(p.x, p.y); }

        /// <summary>地上の廊下（屋根つき）：塔の寮・魔術師団の寮→中庭の南のすみ、騎士団の寮→本館の西のはし（famuru）。</summary>
        public static readonly Vector2[][] Walkways =
        {
            new[] { new Vector2(-54.5f, 31f), new Vector2(-40f, 35f), new Vector2(-19f, 42f) },
            new[] { new Vector2(45f, 30f), new Vector2(38f, 34f), new Vector2(19f, 42f) },
            new[] { new Vector2(-46f, 71f), new Vector2(-41f, 71f), new Vector2(-41f, 79.5f), new Vector2(-35.5f, 79.5f) },
        };

        public class PathDef { public float w; public Vector2[] pts; public PathDef(float w, params Vector2[] pts) { this.w = w; this.pts = pts; } }
        /// <summary>石畳の道（見た目と歩ける所の両方）。</summary>
        public static readonly PathDef[] Paths =
        {
            new PathDef(3f, new Vector2(0, 10), new Vector2(0, 34.5f)),                                                                       // 正門→中庭の入口
            new PathDef(3f, new Vector2(0, 18), new Vector2(14, 22), new Vector2(28, 28), new Vector2(40, 40), new Vector2(43, 52), new Vector2(40, 64), new Vector2(40, 70)), // →わたしたちの寮の前庭
            new PathDef(3f, new Vector2(0, 18), new Vector2(-14, 22), new Vector2(-28, 28), new Vector2(-40, 40), new Vector2(-46, 46), new Vector2(-62, 47)),             // →騎士団の寮の正面
            new PathDef(2.5f, new Vector2(40, 40), new Vector2(61.5f, 40.5f)),                                                              // →魔術師団の寮の正面
            new PathDef(2.5f, new Vector2(0, 90.5f), new Vector2(0, 96), new Vector2(-30, 96), new Vector2(-52, 92)),                         // 本館の北の玄関→温室・工房の前
            new PathDef(2f, new Vector2(-46, 93.4f), new Vector2(-46, 97.6f)),                                                               // →工房の扉
            new PathDef(2f, new Vector2(-59, 92.6f), new Vector2(-59, 97.1f)),                                                               // →温室の扉
        };

        /// <summary>学園の道を1階の歩ける網に開ける（建物の中は NewDorm・SchoolBuilder が開ける）。</summary>
        public static void OpenPaths(GridNav n)
        {
            foreach (var p in Paths) OpenLine(n, p.w, p.pts);
            foreach (var w in Walkways) OpenLine(n, 3f, w);
            // 中庭の南（中庭と曲線の渡り廊下のあいだ）
            OpenPlanRect(n, -16f, 31.5f, 16f, 44.6f);
            // 正門の内側
            OpenPlanRect(n, -4f, 9f, 4f, 13f);
        }

        static void OpenPlanRect(GridNav n, float x0, float y0, float x1, float y1)
        {
            Vector3 a = G(x0, y0), b = G(x1, y1);
            n.Open(Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.z, b.z), Mathf.Max(a.x, b.x), Mathf.Max(a.z, b.z)));
        }

        static void OpenLine(GridNav n, float w, Vector2[] pts)
        {
            float h = w / 2f - 0.25f;
            for (int i = 0; i + 1 < pts.Length; i++)
            {
                float len = Vector2.Distance(pts[i], pts[i + 1]);
                int k = Mathf.Max(1, Mathf.CeilToInt(len / 0.25f));
                for (int s = 0; s <= k; s++)
                {
                    Vector2 p = Vector2.Lerp(pts[i], pts[i + 1], s / (float)k);
                    Vector3 g = G(p.x, p.y);
                    n.Open(Rect.MinMaxRect(g.x - h, g.z - h, g.x + h, g.z + h));
                }
            }
        }
    }
}
