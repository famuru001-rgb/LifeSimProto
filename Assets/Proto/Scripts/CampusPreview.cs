using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 新しい学園（作り直しの第5案。famuru 2026-10-31 決定）の外側。
    /// ・新しい学園で遊ぶとき（ふだん＝LoungeWorld.NewLayout。Logs\old_campus_play.txt を置くと前の学園）は、寮と学校の本当の場所に合わせて組む（配置図の座標→CampusPlan.G）。
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
            // 北（城壁 y 108 より奥）は森の奥行きを2倍に（famuru 2026-11-08）。東の浜辺の北のはし（110.39, 110.6）までは前の岸のまま
            new Vector2(116,100), new Vector2(110.39f,110.6f), new Vector2(98,160), new Vector2(84,184), new Vector2(62,196), new Vector2(30,204), new Vector2(0,208), new Vector2(-34,204), new Vector2(-66,196), new Vector2(-90,180), new Vector2(-104,156), new Vector2(-120,92), new Vector2(-118,38)
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
                        if (k == "Grass_Blade") { fm.SetColor("_BaseColor", new Color(0.5f, 0.66f, 0.36f)); fm.SetFloat("_Translucency", 0.1f); } // 草むらの画像が白っぽく、雑草に見えたため（2026-11-06）
                        mats[k] = fm;
                    }
                }
            }
            if (game)
            {
                // 依頼27 の植物：葉っぱの板（切り抜き）・頂点の色（rgb＝内側の暗さ、a＝風）・面の向きは作りこみ済み
                var fol = Shader.Find("LifeSim/Foliage");
                plants2 = fol != null && Resources.Load<GameObject>("Furniture/Plants2/Furn_Tree2_Broad_A") != null;
                if (plants2)
                    foreach (var k in new[] { "Leaf_Card", "Leaf_Card_Fine", "Leaf_Card_Ivy", "Leaf_Card_Dark", "Blossom_Card" })
                    {
                        var tx = Resources.Load<Texture2D>("Furniture/Plants2/Textures/" + k);
                        if (tx == null) continue;
                        var fm = new Material(fol) { name = "Foliage2_" + k, enableInstancing = true };
                        fm.SetTexture("_BaseMap", tx);
                        fm.SetFloat("_Cutoff", 0.55f);
                        fm.SetFloat("_UseVertexColor", 1f); fm.SetFloat("_UseVertexAlpha", 1f);
                        bool ivy = k == "Leaf_Card_Ivy";
                        fm.SetFloat("_WindStrength", ivy ? 0.015f : 0.06f);
                        fm.SetFloat("_WindSpeed", 1.2f);
                        fm.SetFloat("_Wrap", 0.35f);
                        fm.SetFloat("_UpNormal", 0f);
                        fm.SetFloat("_Translucency", 0.3f);
                        fm.SetFloat("_Cull", 0f);
                        // 葉の画像は黄みが強いので少し落ち着かせる。森の木はさらに暗く青みに（遠く）
                        fm.SetColor("_BaseColor", k == "Leaf_Card_Fine" ? new Color(0.74f, 0.84f, 0.8f) : (k == "Blossom_Card" ? Color.white : new Color(0.8f, 0.9f, 0.76f)));
                        mats[k] = fm;
                    }
            }
            mats["Glow_Fire"] = ProtoArt.Glow(new Color(1f, 0.55f, 0.2f), 2.6f);
            if (game)
            {
                // 依頼26 の崖（崖_v1）：色は物ごとの焼き込み画像（2枚目のUV＝BakeUV。すき間の暗さ・苔・水ぎわの濡れ色入り）、細かい凹凸は岩肌の法線（1枚目のUV、10m で1回）。LifeSim/Cliff
                var cliffSh = Shader.Find("LifeSim/Cliff");
                if (cliffSh != null)
                    foreach (var k in Cliff2Names)
                    {
                        var bake = Resources.Load<Texture2D>("Furniture/Cliffs2/Textures/Cliff2_Bake_" + k);
                        if (bake == null) continue;
                        for (int r = 0; r < 2; r++)
                        {
                            var m = new Material(cliffSh) { name = (r == 0 ? "Cliff2Rock_" : "Cliff2Moss_") + k, enableInstancing = true };
                            m.SetTexture("_BakeMap", bake);
                            var nm = T("Tile_Cliff_Normal");
                            if (r == 0 && nm != null) { m.SetTexture("_BumpMap", nm); m.SetFloat("_UseBump", 1f); m.SetFloat("_BumpScale", 0.8f); }
                            else m.SetFloat("_UseBump", 0f);
                            if (r == 1) m.SetColor("_BaseColor", new Color(Tune("Moss2_r", 1f), Tune("Moss2_g", 1f), Tune("Moss2_b", 1f))); // 苔の明るさ（崖_v2 で模型の苔を明るいオリーブ色にしたので 1）
                            if (T("Tile_Cliff") != null) m.SetTexture("_DetailMap", T("Tile_Cliff"));
                            m.SetFloat("_DetailStrength", 0f);
                            mats[(r == 0 ? "Rock_Cliff@" : "Moss@") + k] = m;
                        }
                    }
            }
            if (T("Tile_PoolTile") != null) mats["Pool_Tile"] = TilePBR.Apply(ProtoArt.Mat(Color.white, T("Tile_PoolTile"), null, 0.7f), "Tile_PoolTile"); // 依頼29 のプールの中のタイル
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
        /// <summary>部品の読み込み。Furniture のほか、依頼27 の植物（Plants2）・依頼25 の中庭（Courtyard2）も探す（同じ名前の画像がぶつからないよう別のフォルダにした）。</summary>
        static GameObject LoadFurn(string n)
        {
            var pf = Resources.Load<GameObject>("Furniture/Furn_" + n);
            if (pf == null) pf = Resources.Load<GameObject>("Furniture/Plants2/Furn_" + n);
            if (pf == null) pf = Resources.Load<GameObject>("Furniture/Courtyard2/Furn_" + n);
            if (pf == null) pf = Resources.Load<GameObject>("Furniture/Buildings3/Furn_" + n); // 依頼28 の建物（建物_v3）
            if (pf == null) pf = Resources.Load<GameObject>("Furniture/PoolBeach/Furn_" + n);  // 依頼29 のプールと浜辺
            if (pf == null) pf = Resources.Load<GameObject>("Furniture/Cliffs2/Furn_" + n);    // 依頼26 の崖と岸の岩（崖_v1）
            if (pf == null) pf = Resources.Load<GameObject>("Furniture/Ground1/Furn_" + n);    // 依頼30 の地面の草・花・下草（地面_v1）
            return pf;
        }

        /// <summary>依頼23 の木と茂みを、依頼27 の植物（葉っぱの板・famuru 2026-11-05「場所で3案を使い分け」）に置きかえる。学園の中＝案1、森と島のふち＝案3。</summary>
        string PlantAlias(string name, float x, float y, ref Vector3? scale)
        {
            if (!game || plants2 == false) return name;
            int hsh = Mathf.Abs((int)(x * 13f) * 31 + (int)(y * 7f));
            Vector3 sc = scale ?? Vector3.one;
            bool forest = y > 108f || !Inside(Island, new Vector2(x, y) + new Vector2(0f, 8f));
            switch (name)
            {
                case "Tree_Pine":
                    if (sc.x < 0.6f) { scale = new Vector3(1f, sc.y * 0.85f, 1f); return hsh % 3 == 0 ? "Tree2_Cypress_S" : "Tree2_Cypress_L"; } // 細く伸ばした針葉樹＝糸杉の代わりだった
                    scale = sc * 0.85f; return hsh % 2 == 0 ? "Tree2_Forest_A" : "Tree2_Forest_B";
                case "Tree": scale = sc * 0.8f; return "Tree2_Forest_B";
                case "Tree_Oak":
                    if (forest) { scale = sc * 0.85f; return hsh % 2 == 0 ? "Tree2_Forest_A" : "Tree2_Forest_B"; }
                    scale = sc * 0.9f; return hsh % 2 == 0 ? "Tree2_Broad_A" : "Tree2_Broad_B";
                case "Tree_Blossom": scale = sc * 0.85f; return "Tree2_Blossom";
                case "Bush_Round": return hsh % 4 == 0 ? "Topiary_Ball" : "Shrub2_A";
                case "Bush_Flower": return "Shrub2_B";
            }
            return name;
        }
        bool plants2;

        // ---------------- 依頼28 の建物（建物_v3。famuru 2026-11-07「進めていい」） ----------------
        /// <summary>新しい建物を使うか。Logs\old_buildings.txt を置くと前の建物（見比べ用）。</summary>
        bool v3;
        Dictionary<string, float> tune;
        /// <summary>大きさなどの調整値。Logs\buildings3.txt に「名前 数字」を1行ずつ書くと、組み直したときにその値を使う（無ければ決めた値）。</summary>
        float Tune(string key, float def)
        {
            if (tune == null)
            {
                tune = new Dictionary<string, float>();
                string f = Path.Combine(LogsDir, "buildings3.txt");
                if (File.Exists(f))
                    foreach (var line in File.ReadAllLines(f))
                    {
                        var w = line.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                        float v;
                        if (w.Length >= 2 && float.TryParse(w[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) tune[w[0]] = v;
                    }
            }
            float r;
            return tune.TryGetValue(key, out r) ? r : def;
        }

        GameObject Put(string name, float x, float h, float y, float yaw, Vector3? scale = null, Material banner = null)
        {
            name = PlantAlias(name, x, y, ref scale);
            if (name.StartsWith("Tree")) treePts.Add(new Vector2(x, y)); // 依頼30：木の根元に草と落ち葉
            var pf = LoadFurn(name);
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
                if (game && !name.StartsWith("Tree2_") && !name.StartsWith("Shrub2_") && !name.StartsWith("Topiary_") && !name.StartsWith("Ivy_")) LeafNormals(r, sm); // 依頼27 の植物は面の向きを作りこみ済み
                for (int i = 0; i < sm.Length; i++)
                {
                    string n = sm[i] != null ? sm[i].name : "";
                    Material m = null;
                    if (n.StartsWith("Fabric_Banner")) m = banner != null ? banner : bannerOurs;
                    else if (n.StartsWith("Metal_Iron") && name == "Roof_Dome") m = copper;
                    else if (n.StartsWith("Glass_Window") && name == "Lamp_Garden" && lampGlass != null) m = lampGlass; // 庭の灯りのガラスは夜に光る
                    else if (n.StartsWith("Glass_Window") && (name == "Pool_Hut" || name == "Beach_Hut")) { if (hutGlass == null) hutGlass = ProtoArt.GlassClear(); m = hutGlass; } // 依頼29 追記4：小屋の窓は透明
                    else if (mats.TryGetValue(n + "@" + name, out var per)) m = per; // 物ごとの材質（依頼26 の崖：焼き込み画像が物ごとにちがう）
                    else if (mats.TryGetValue(n, out var exact)) m = exact; // 名前がぴったりの材質を先に（Leaf_Card と Leaf_Card_Fine など）
                    else foreach (var kv in mats) if (n.StartsWith(kv.Key)) { m = kv.Value; break; }
                    if (m != null) sm[i] = m;
                    // 小屋（依頼29 追記3・4）：壁の内側の面が無い所は、屋根を消して中を見ると外の地面が透けるので両面に（2026-11-09）
                    if ((name == "Pool_Hut" || name == "Beach_Hut") && sm[i] != null && sm[i] != hutGlass)
                    {
                        Material ds;
                        if (!hutTwoSided.TryGetValue(sm[i], out ds)) { ds = new Material(sm[i]) { name = sm[i].name + "_TwoSided" }; if (ds.HasProperty("_Cull")) ds.SetFloat("_Cull", 0f); ds.doubleSidedGI = true; hutTwoSided[sm[i]] = ds; }
                        sm[i] = ds;
                    }
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
                    if (pathCovers.Count > 0)
                    {
                        Vector2 cc = new Vector2(mn.x + (i + 0.5f) * cs, mn.y + (j + 0.5f) * cs);
                        bool covered = false;
                        foreach (var pc in pathCovers) if (pc.Contains(cc)) { covered = true; break; }
                        if (covered) continue; // 相手（石畳・壁・階段）が上に重なる所
                    }
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
        /// <summary>地面の高さ：城壁の外の森は少し高く、森の東半分に丘（famuru 2026-11-08「森の右の部分を丘に。北東よりにてっぺん、高さ 10m くらい」）。</summary>
        static float H(float x, float y)
        {
            float b = HBase(x, y);
            // 浜辺（famuru 2026-11-08「草の地面と砂浜の高さが違っており大きな隙間」）：地面も砂浜の形に下げて、帯の両はしは 10m かけて草原の高さへ戻す
            float across; float w = BeachLowW(x, y, out across);
            return w <= 0f ? b : Mathf.Lerp(b, SandH(Mathf.Min(across, 11.2f)) - 0.2f, w);
        }

        /// <summary>浜辺で地面を砂浜の高さへ下げる割合（1＝砂浜の高さ、0＝草原のまま）。はしは丸く、ゆるい坂で草原へ戻す。</summary>
        static float BeachLowW(float x, float y, out float across)
        {
            across = 999f;
            if (!PoolBeachOn || x < 84f || y < 46f || y > 134f) return 0f;
            float along; CoastLocal(new Vector2(x, y), out along, out across);
            float len = CoastLen;
            if (along <= 0.01f) { Vector2 d0 = (Coast[1] - Coast[0]).normalized; along = Vector2.Dot(new Vector2(x, y) - Coast[0], d0); across = Vector2.Dot(new Vector2(x, y) - Coast[0], SegIn(0)); }
            else if (along >= len - 0.01f) { int l = Coast.Length - 1; Vector2 d1 = (Coast[l] - Coast[l - 1]).normalized; along = len + Vector2.Dot(new Vector2(x, y) - Coast[l], d1); across = Vector2.Dot(new Vector2(x, y) - Coast[l], SegIn(l - 1)); }
            if (across > 18f || across < -30f) return 0f;
            if (Mathf.Abs(along - StairAlong) < 1.6f && across > 11f) return 0f; // 階段の上の踊り場と小道
            float edge = Mathf.Min(along, len - along);                       // 帯のはしまでの長さ（外はマイナス）
            float endK = Mathf.SmoothStep(0f, 1f, (6f - edge) / 10f);        // はしに近いほど 1
            float rc = Mathf.Lerp(1.2f, 6f, endK);                            // 土手の幅：真ん中は急（土手の岩でかくす）、はしはゆるい坂
            float u = Mathf.Clamp01(across / 11.2f);
            float extA = 6f * Mathf.Sqrt(1f - u * u);                          // 砂を帯の外へ丸くのばす（水ぎわ 6m・土手 0m）
            float dA = Mathf.Max(0f, -edge - extA), dC = Mathf.Max(0f, across - 11.2f);
            return (1f - Mathf.SmoothStep(0f, 1f, dA / 10f)) * (1f - Mathf.SmoothStep(0f, 1f, dC / rc));
        }

        /// <summary>地面のます目の大きさ。浜辺のはしの坂で砂のふちがぎざぎざにならないよう 2.5→1.25m（2026-11-08）。</summary>
        const float GroundStep = 1.25f;

        /// <summary>地面のメッシュ（ます目の三角形）の、その点での高さ。砂を地面にそわせてかくすのに使う。</summary>
        static float GroundAt(float x, float y)
        {
            const float step = GroundStep, gx0 = -125f, gy0 = -5f;
            float fxA = (x - gx0) / step, fyA = (y - gy0) / step;
            int i = Mathf.FloorToInt(fxA), j = Mathf.FloorToInt(fyA);
            float fx = fxA - i, fy = fyA - j;
            float xa = gx0 + i * step, ya = gy0 + j * step;
            float ha = H(xa, ya), hb = H(xa + step, ya), hc = H(xa, ya + step), hd = H(xa + step, ya + step);
            float h = fx + fy <= 1f ? ha + fx * (hb - ha) + fy * (hc - ha) : hd + (1f - fx) * (hc - hd) + (1f - fy) * (hb - hd);
            return h - 0.04f;
        }

        /// <summary>浜辺で下げる前の地面の高さ。</summary>
        static float HBase(float x, float y)
        {
            float f = Mathf.SmoothStep(0f, 1f, (y - 110f) / 12f) * 1.6f;
            float dx = (x - HillTop.x) / 34f, dy = (y - HillTop.y) / 28f;
            return f + (HillH - 1.6f * Mathf.SmoothStep(0f, 1f, (HillTop.y - 110f) / 12f)) * Mathf.Exp(-(dx * dx + dy * dy)) * Mathf.SmoothStep(0f, 1f, (y - 108f) / 10f);
        }
        /// <summary>丘のてっぺん（配置図）と高さ。</summary>
        static readonly Vector2 HillTop = new Vector2(62f, 172f);
        const float HillH = 10f;

        static bool Inside(Vector2[] poly, Vector2 p)
        {
            bool c = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if (((poly[i].y > p.y) != (poly[j].y > p.y)) && (p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)) c = !c;
            return c;
        }

        void Ground()
        {
            const float step = GroundStep;
            float x0 = -125f, x1 = 125f, y0 = -5f, y1 = 214f; // 北の森を2倍にしたので 162→214（2026-11-08）
            int nx = Mathf.CeilToInt((x1 - x0) / step), ny = Mathf.CeilToInt((y1 - y0) / step);
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            var inc = new bool[nx * ny]; // 地面を張ったます目
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
                    if (poolBeach && InPoolHole(cx, cy)) continue; // プールの穴（浜辺は地面を砂浜の高さに下げて、その上に砂。2026-11-08）
                    int a = j * (nx + 1) + i, b = a + 1, c = a + nx + 1, d = c + 1;
                    tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(b); tris.Add(c); tris.Add(d);
                    inc[j * nx + i] = true;
                }
            if (cliffs2)
            {
                // 依頼26 の崖：地面のふち（2.5m のます目の階段）が崖の岩より外へ出ないよう、島のふちの点を島の線の少し内側へ寄せる
                float margin = Tune("Ground_edge_in", 0.5f);
                var used = new HashSet<int>(tris);
                System.Func<int, int, bool> cellOn = (ci, cj) => ci >= 0 && cj >= 0 && ci < nx && cj < ny && inc[cj * nx + ci];
                foreach (int vi in used)
                {
                    int vx = vi % (nx + 1), vy = vi / (nx + 1);
                    bool edge = !(cellOn(vx - 1, vy - 1) && cellOn(vx, vy - 1) && cellOn(vx - 1, vy) && cellOn(vx, vy)); // 張っていないます目にとなる点＝地面のふち
                    var v = verts[vi]; var q = new Vector2(v.x, v.z);
                    Vector2 best = q; float bd = float.MaxValue; Vector2 bn = Vector2.zero;
                    for (int e = 0, f = Island.Length - 1; e < Island.Length; f = e++)
                    {
                        Vector2 ea = Island[f], eb = Island[e], ed = eb - ea; float el = ed.magnitude; ed /= el;
                        Vector2 cp = ea + ed * Mathf.Clamp(Vector2.Dot(q - ea, ed), 0f, el);
                        float dd = (q - cp).sqrMagnitude;
                        if (dd < bd) { bd = dd; best = cp; bn = new Vector2(ed.y, -ed.x); }
                    }
                    if (Inside(Island, best + bn * 0.5f)) bn = -bn; // bn＝外向き
                    bool inside = Inside(Island, q);
                    float dist = Mathf.Sqrt(bd);
                    if (!inside || dist < margin || (edge && dist < step * 1.6f)) // ふちの点は島の線の内側 margin へそろえる（階段をなくす）
                    {
                        Vector2 np = best - bn * margin;
                        verts[vi] = new Vector3(np.x, H(np.x, np.y) - 0.04f, np.y);
                    }
                }
            }
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            if (ground1) mesh.RecalculateTangents(); // 依頼30 の地面の凹凸の画像に要る
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
            if (cliffs2) { Cliffs2(); return; }
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
                    if (poolBeach && BeachAcross(p, 4f) < 3f) continue; // 浜辺の所は崖なし（依頼29。はしの先まで。上の面が地面から出ないように）
                    float top = H(p.x, p.y) - 0.12f; // 草の面より少し下（ちらつき防止）
                    float bottom = WaterY - 4f;
                    Vector2 c = p - n * 4f;
                    Put("Cliff", c.x, bottom, c.y, YawFacing(n), new Vector3(seg / 10f * 1.08f, (top - bottom) / 20f, 1f));
                }
            }
        }

        // ---------------- 依頼26 の崖（崖_v1。famuru 2026-11-07「崖の作り直し（依頼26）」） ----------------
        // 崖のかたまり A/B/C を島のふちに約 8m おきに重ね、30°以上曲がる所は丸い角。上のふちが芝生の高さ。Logs\old_cliffs.txt で前の崖に戻す
        bool cliffs2;
        static readonly string[] Cliff2Names = { "Cliff2_A", "Cliff2_B", "Cliff2_C", "Cliff2_Corner", "Cliff2_Top", "Rock_Shore_A", "Rock_Shore_B", "Rock_Ledge", "Rock_Rim_A", "Rock_Rim_B" };
        static readonly string[] Cliff2Walls = { "Cliff2_A", "Cliff2_B", "Cliff2_C" };

        /// <summary>浜辺の帯の中か（岸の線の両はしの先は、はしの向きにのばして測る。BeachAcross は両はしで along が 0／全長に止まるため）。</summary>
        /// <summary>浜辺で地面を下げた所（砂の上と、草原へ戻る坂の下のほう）か。木や草を置かない。</summary>
        static bool OnLowBeach(Vector2 p, float wMin) { float a; return BeachLowW(p.x, p.y, out a) > wMin; }

        static bool NearBeach(Vector2 p, float ext, float acrossMax)
        {
            float along, across; CoastLocal(p, out along, out across);
            float len = CoastLen;
            if (along <= 0.01f) { Vector2 d0 = (Coast[1] - Coast[0]).normalized; along = Vector2.Dot(p - Coast[0], d0); across = Vector2.Dot(p - Coast[0], SegIn(0)); }
            else if (along >= len - 0.01f) { int l = Coast.Length - 1; Vector2 d1 = (Coast[l] - Coast[l - 1]).normalized; along = len + Vector2.Dot(p - Coast[l], d1); across = Vector2.Dot(p - Coast[l], SegIn(l - 1)); }
            return along > -ext && along < len + ext && across < acrossMax && across > -40f;
        }

        /// <summary>崖のかたまりを1つ置く。p＝島のふち（芝生のはし）、n＝外向き。部品の原点は上のふちの真ん中で、芝生のはしは原点の 1.8m 前（報告26）。</summary>
        GameObject PutCliff2(string kind, Vector2 p, Vector2 n, float yaw, float sx, float ground, float rise = 0f, float pitch = 0f, float roll = 0f, float ysMul = 1f)
        {
            float ys = Tune("Cliff2_ys", 0.6f) * ysMul, back = Tune("Cliff2_back", 1.8f);
            Vector2 c = p - n * back * sx;
            var go = Put(kind, c.x, ground + Tune("Cliff2_dy", 0.1f) + rise, c.y, yaw, new Vector3(sx, ys, sx));
            if (go != null && (pitch != 0f || roll != 0f)) Tilt(go, pitch, roll);
            return go;
        }
        /// <summary>置いた部品を、向きはそのままで前後（pitch）・左右（roll）に傾ける。原点（上の真ん中）を中心に回る。</summary>
        static void Tilt(GameObject go, float pitch, float roll)
        {
            go.transform.localRotation = go.transform.localRotation * Quaternion.Euler(pitch, 0f, roll);
        }

        void Cliffs2()
        {
            float area = 0f;
            for (int i = 0, j = Island.Length - 1; i < Island.Length; j = i++) area += (Island[j].x * Island[i].y - Island[i].x * Island[j].y);
            bool ccw = area > 0f;
            int N = Island.Length;
            System.Func<Vector2, Vector2> outN = d => ccw ? new Vector2(d.y, -d.x) : new Vector2(-d.y, d.x);
            var corner = new bool[N];
            float step = Tune("Cliff2_step", 8f), cgap = Tune("Cliff2_cgap", 6.4f), cmin = Tune("Cliff2_corner_deg", 25f);
            for (int i = 0; i < N; i++)
            {
                Vector2 dIn = (Island[i] - Island[(i + N - 1) % N]).normalized, dOut = (Island[(i + 1) % N] - Island[i]).normalized;
                if (Vector2.Angle(dIn, dOut) < cmin) continue;
                Vector2 p = Island[i];
                if (poolBeach && NearBeach(p, 4f, 3f)) continue;
                corner[i] = true;
                Vector2 bis = (outN(dIn) + outN(dOut)).normalized;
                float sc = Tune("Cliff2_corner_scale", 1.1f);
                Vector2 c = p - bis * Tune("Cliff2_corner_back", 1.6f) * sc;
                Put("Cliff2_Corner", c.x, H(c.x, c.y) + Tune("Cliff2_dy", 0.1f), c.y, YawFacing(bis), new Vector3(sc, Tune("Cliff2_ys", 0.6f), sc));
            }
            bool top = Tune("Cliff2_top", 1f) > 0.5f;
            for (int i = 0; i < N; i++)
            {
                Vector2 a = Island[i], b = Island[(i + 1) % N];
                Vector2 d = b - a; float L = d.magnitude; d /= L;
                Vector2 n = outN(d);
                float s0 = corner[i] ? cgap : step * 0.5f, s1 = corner[(i + 1) % N] ? cgap : step * 0.5f;
                float span = L - s0 - s1;
                int cnt = span <= 0.5f ? 0 : Mathf.Max(1, Mathf.RoundToInt(span / step));
                for (int j = 0; j <= cnt; j++)
                {
                    float s = cnt == 0 ? (s0 + Mathf.Max(0f, span) * 0.5f) : s0 + span * j / cnt;
                    if (cnt == 0 && span < -2f) s = L * 0.5f;
                    Vector2 p = a + d * s;
                    if (poolBeach && NearBeach(p, 12f, 3f)) continue; // 浜辺の所は崖なし（依頼29。浜辺の両はしは下で1つずつ）
                    float h1 = Hash01(i * 31 + j, 41), h2 = Hash01(j, i * 17 + 5), h3 = Hash01(i + j * 13, 77);
                    string kind = Cliff2Walls[Mathf.Min(2, (int)(h1 * 3f))];
                    float sx = 0.96f + h3 * 0.16f;
                    float yaw = YawFacing(n) + (h2 - 0.5f) * Tune("Cliff2_yawvar", 10f);
                    float g = H(p.x, p.y);
                    // famuru 2026-11-07「上が平たんになっているので改善したい」：かたまりごとに高さ・傾き・縦の長さを変えて、上のふちを一直線の平らにしない
                    float h4 = Hash01(i * 11 + j, 131), h5 = Hash01(j * 5 + i, 137), h6 = Hash01(i + j * 3, 139), h7 = Hash01(i * 3 + j * 7, 149);
                    // 上げすぎると上の苔の平らな面が芝生より上に出て目立つので少しだけ。前（海の側）へ下がるように傾けて、上の面を斜めに
                    float rise = -Tune("Cliff2_sink", 0.08f) - h5 * Tune("Cliff2_rise", 0.15f); // 崖_v2（依頼26 追記1）で上のふちに岩の頭が付いたので上げない。後ろの低い面が芝生から出ないよう少し下げる
                    float tilt = Tune("Cliff2_tilt", 3f);
                    PutCliff2(kind, p, n, yaw, sx, g, rise, -Tune("Cliff2_lean", 1f) - h6 * tilt * 0.6f, (h7 - 0.5f) * tilt * 0.8f, 0.9f + h6 * 0.25f);
                    // ふちの石（斜めに埋めた岩。上の線に出っぱりを作る）
                    if (h7 < Tune("Cliff2_rim_rate", 0.45f))
                    {
                        Vector2 q = p - n * (0.4f + h5 * 0.9f) + d * (h4 - 0.5f) * 6f;
                        float rs = 0.55f + h6 * 0.4f;
                        if (Has("Rock_Rim_A"))
                        {
                            // 崖_v2 の芝生のふちの石（上が丸い。原点は下の真ん中なので、高さの半分くらい埋める）
                            bool ra = h5 >= 0.5f; float hgt = ra ? 1.8f : 1.2f;
                            var rk = Put(ra ? "Rock_Rim_A" : "Rock_Rim_B", q.x, g - hgt * rs * Tune("Rim_bury", 0.5f), q.y, YawFacing(n) + (h5 - 0.5f) * 120f, Vector3.one * (rs + 0.3f));
                            if (rk != null) Tilt(rk, (h6 - 0.5f) * 14f, (h4 - 0.5f) * 14f);
                        }
                        else
                        {
                            var rk = Put(h5 < 0.5f ? "Rock_Shore_B" : "Rock_Shore_A", q.x, g + 0.1f + h4 * 0.3f, q.y, h5 * 360f, Vector3.one * rs);
                            if (rk != null) Tilt(rk, 35f + h6 * 20f, (h4 - 0.5f) * 40f); // 苔の平らな面が横を向くまで倒す
                        }
                    }
                    if (top) Put("Cliff2_Top", (p - n * Tune("Cliff2_top_back", 0.9f)).x, g + Tune("Cliff2_top_dy", 0.05f), (p - n * Tune("Cliff2_top_back", 0.9f)).y, YawFacing(n), new Vector3(sx * 0.9f, 1f, 1f));
                    // 崖の途中の棚の岩（形をくずす。水面より上）
                    if (Hash01(i * 7 + j, 93) < Tune("Cliff2_ledge", 0.12f))
                    {
                        Vector2 q = p + n * Tune("Cliff2_ledge_out", 0.5f) + d * (h2 - 0.5f) * 4f;
                        Put("Rock_Ledge", q.x, g - 1.2f - h1 * 0.6f, q.y, YawFacing(n) + (h3 - 0.5f) * 30f, Vector3.one * (0.6f + h2 * 0.4f));
                    }
                }
            }
            // 浜辺の両はしのふた（崖のかたまり）は、地面を坂にしてなめらかにつないだのでやめた（2026-11-08）
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
                    if (v3 && !postern) Put("CastleWall2", p.x, 0f, p.y, YawAlong(d) + Tune("CastleWall2_yaw", 0f), new Vector3(seg / 6f, 1f, 1f));
                    else Put(postern ? "Postern" : "CastleWall", p.x, 0f, p.y, YawAlong(d), new Vector3(seg / 6f, 1f, 1f));
                }
            }
            for (int i = 0; i < WallPts.Length; i++)
            {
                if (i == 1 || i == 2) continue;
                Vector2 p = WallPts[i];
                if (v3) { Put("Tower2_Round", p.x, 0f, p.y, 0f); Put("Tower2_Roof", p.x, 10f, p.y, 0f); continue; } // 依頼28：窓のない丸い塔と円すい屋根
                Put("Tower_Round_Base", p.x, 0f, p.y, 0f);
                Put("Tower_Round", p.x, 3.5f, p.y, 45f);
                Put("Battlement_Top_Round", p.x, 7f, p.y, 0f);
            }
            // 正門（崖のきわ。前が南）と跳ね橋
            if (v3)
            {
                // famuru 2026-11-07「正門と橋は基本的に閉じている形にして」：扉の閉じた正門（依頼28 の Tripo 版）と、上げた跳ね橋（開け閉めはしない）
                var gh = Put("Gatehouse2", Tune("Gate_dx", 0f), 0f, 5f + Tune("Gate_dy", 0f), 180f + Tune("Gate_yaw", 0f), Vector3.one * Tune("Gate_scale", 1f));
                var br = Put("Drawbridge", 0f, -0.8f, 5f - 15.64f, 180f);
                if (br != null) RaiseBridge(br, gh != null ? LocalBounds(gh).min.z : 1f);
            }
            else
            {
            Put("Gatehouse", 0f, 0f, 5f, 180f);
            // 正門の扉（依頼21。木と鉄の両開き）。跳ね橋の鎖に当たらないよう、城の内側へ開いた形で置く
            // ちょうつがい：Blender (±3.56, -3.14, 0.04) → 門の向き 180° で (∓3.56, 0.04, -3.14)
            gateL = Put("Gate_Door", -3.56f, 0.04f, 5f - 3.14f, GateOpen ? 90f : 180f);
            gateR = Put("Gate_Door", 3.56f, 0.04f, 5f - 3.14f, GateOpen ? -90f : 180f, new Vector3(-1f, 1f, 1f));
            gateK = GateOpen ? 1f : 0f;
            Put("Drawbridge", 0f, -0.8f, 5f - 15.64f, 180f);
            }
            // 街：石の護岸と家並み（背景）
            const float qz = -24f;
            for (float x = -135f; x <= 165f; x += 10f) Put("Quay", x, -4f, qz - 1.5f, 0f);
            Box("Town_Ground", new Vector3(15f, -0.05f, -60f), new Vector3(320f, 0.1f, 70f), Tiled("Tile_Flagstone", 160f, 35f));
            if (game && Has("Town_House_A"))
            {
                // 依頼23 の街の家（2階建て・塔つき・店）を4列に、間かく・大きさ・向きを少しずつ変えて並べる
                string[] kinds = v3 ? new[] { "Town2_House_A", "Town2_House_B", "Town2_Shop", "Town2_House_C" } : new[] { "Town_House_A", "Town_House_B", "Town_Shop", "Town_House_A" };
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
                        float wide = kind == "Town_House_B" ? 9.6f : 8.8f;
                        if (v3) wide = (kind == "Town2_House_B" || kind == "Town2_House_C") ? 9.8f : 8.3f; // 依頼28 の家の幅（9.2m・7.7m）＋すき間
                        x += wide * sc + Hash01(k * 7, r) * 1.6f;
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

        /// <summary>跳ね橋を門の側のはしで起こして立てる（上げた形）。長さは門の高さに合うよう縮める（Logs\buildings3.txt の Bridge_len・Bridge_angle で調整）。</summary>
        /// <summary>部品の大きさの箱（配置図の座標＝root の中で）。</summary>
        Bounds LocalBounds(GameObject go)
        {
            var b = new Bounds(); bool first = true;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var bb = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var l = root.InverseTransformPoint(new Vector3((i & 1) == 0 ? bb.min.x : bb.max.x, (i & 2) == 0 ? bb.min.y : bb.max.y, (i & 4) == 0 ? bb.min.z : bb.max.z));
                    if (first) { b = new Bounds(l, Vector3.zero); first = false; } else b.Encapsulate(l);
                }
            }
            return b;
        }

        void RaiseBridge(GameObject br, float gateFront)
        {
            var rs = br.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return;
            // 配置図の座標（root の中）で、橋の北のはし（門の側）を探す
            float maxZ = float.MinValue, minZ = float.MaxValue, topY = float.MinValue;
            foreach (var r in rs)
            {
                var bb = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var c = new Vector3((i & 1) == 0 ? bb.min.x : bb.max.x, (i & 2) == 0 ? bb.min.y : bb.max.y, (i & 4) == 0 ? bb.min.z : bb.max.z);
                    var l = root.InverseTransformPoint(c);
                    maxZ = Mathf.Max(maxZ, l.z); minZ = Mathf.Min(minZ, l.z); topY = Mathf.Max(topY, l.y);
                }
            }
            float len = maxZ - minZ, want = Tune("Bridge_len", 7.5f);
            var t = br.transform;
            if (len > want + 0.1f)
            {
                // 門の側のはしを動かさずに、橋の向き（配置図の z）だけ縮める
                float k = want / len;
                var ls = t.localScale; ls.z *= k; t.localScale = ls; // 部品の向き 180° でも z は橋の長さ
                var lp = t.localPosition; lp.z = maxZ - (maxZ - lp.z) * k; t.localPosition = lp;
            }
            Vector3 hinge = root.TransformPoint(new Vector3(0f, topY, maxZ));
            t.RotateAround(hinge, root.TransformDirection(Vector3.right), Tune("Bridge_angle", 82f));
            // 立てたあと、下のはしを門の地面に、門の側の面を正門の前の面に合わせる
            var nb = LocalBounds(br);
            Vector3 move = new Vector3(0f, Tune("Bridge_y", 0.8f) - nb.min.y, gateFront + Tune("Bridge_in", 9f) - nb.max.z);
            t.position += root.TransformDirection(move);
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
            if (v3)
            {
                // 依頼28 の Tripo 版の寮（騎士団＝案2、魔術師団＝案3、冒険者＝真ん中の太い塔とまわりの塔3つ）。前の寮より小さいので少し大きくして置く
                Put("Dorm_Knight2", -62f + Tune("Knight_dx", 0f), 0f, 63f + Tune("Knight_dy", 0f), 180f + Tune("Knight_yaw", 0f), Vector3.one * Tune("Knight_scale", 1.4f));
                Put("Dorm_Mage2", 61.5f + Tune("Mage_dx", 0f), 0f, 25f + Tune("Mage_dy", 0f), 0f + Tune("Mage_yaw", 0f), Vector3.one * Tune("Mage_scale", 1.5f));
                Put("Dorm_Adventurer2", -72f + Tune("Adv_dx", 0f), 0f, 29f + Tune("Adv_dy", 0f), 180f + Tune("Adv_yaw", 0f), Vector3.one * Tune("Adv_scale", 1.25f));
            }
            else
            {
                Put("Dorm_Knight", -62f, 0f, 63f, 180f, null, bannerKnight);
                Put("Dorm_Mage", 61.5f, 0f, 25f, 0f, null, bannerMage); // 正面（サンルーム・玄関）を北の学園の側へ
                Put("Dorm_Tower", -72f, 0f, 29f, 180f, null, bannerTower);
            }
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
                if (v3) net.AddRange(CampusPlan.DormLinksV3); // 依頼28 の寮の玄関・地上の廊下のはしへの道（famuru 2026-11-07。歩ける所にも入れた）
                if (poolBeach)
                {
                    // famuru 2026-11-07「道のつなぎ目をきれいに」：道のはしは相手（プールの石畳・城壁の扉・階段の踊り場）の下までのばし、相手の上に重なる所は道を作らない（PathCovers）
                    net.Add(new CampusPlan.PathDef(2.2f, new Vector2(42.5f, 51f), new Vector2(PoolRect.xMin - 3f + 1.2f, 51f)));   // わたしたちの寮の前の道→プールの石畳
                    pathCovers.Add(new PathCover(new Vector2(PoolRect.center.x, PoolRect.center.y), Vector2.right, PoolRect.width / 2f + 3f - 0.3f, PoolRect.height / 2f + 3f - 0.3f));
                    Vector2 wd = (new Vector2(80, 108) - new Vector2(96, 86)).normalized, wn = new Vector2(wd.y, -wd.x); if (wn.x < 0f) wn = -wn;
                    net.Add(new CampusPlan.PathDef(2f, BeachDoor + wn * 0.6f + wd * Tune("Door_path_dx", 0.05f), BeachDoor + wn * 4f + wd * Tune("Door_path_dx", 0.05f), BeachStairTop)); // 城壁の小さな扉→浜辺の階段の上の踊り場（扉の前はまっすぐ出て、道の中心を扉の中心に合わせる。famuru 2026-11-07）
                    pathCovers.Add(new PathCover(BeachDoor, wd, 2f, 1.15f));                                  // 城壁の厚みの中は道なし（扉の面から始まる）
                    net.Add(ShorePoolPath);  // プールの石畳から中庭の北を通る近道
                    net.Add(ShoreInnerPath); // 寮の西の芝生の小道：本館の北の玄関の道→城壁の小さな扉の内側（famuru 2026-11-08「プールと浜辺で過ごせるように」）
                    Vector2 sn; Vector2 sp = CoastPoint(StairAlong, StairOriginS, out sn);
                    pathCovers.Add(new PathCover(sp, -sn, 2.45f - 0.35f, 0.95f));                              // 階段の上（踊り場の手前まで道を重ねる）
                }
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
            // 2026-11-08 famuru「森の奥行きを2倍・木の密度を2倍」：島を北へ広げ、木の間隔を 7m→4.95m（1/√2）に。城壁から 10m は木なし、丘のてっぺんは大きな木1本だけ
            const float tStep = 4.95f;
            for (float y = 112f; y < 210f; y += tStep)
                for (float x = -116f; x < 116f; x += tStep)
                {
                    float jx = Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f; jx -= Mathf.Floor(jx);
                    float jy = Mathf.Sin(x * 39.346f + y * 11.135f) * 24634.6345f; jy -= Mathf.Floor(jy);
                    float px = x + (jx - 0.5f) * 5f, py = y + (jy - 0.5f) * 5f;
                    if (!Inside(Island, new Vector2(px, py))) continue;
                    if (poolBeach && (NearBeach(new Vector2(px, py), 4f, 17f) || OnLowBeach(new Vector2(px, py), 0.15f))) continue; // 浜辺（依頼29）。前は帯の北の先の森まで木を置いていなかった（2026-11-08 famuru「森の東奥に木がない」）
                    if (Mathf.Abs(px + 60f) < 18f && Mathf.Abs(py - 126f) < 10f) continue; // 訓練の施設・ダンジョンの入り口
                    if ((new Vector2(px, py) - HillTop).magnitude < 10f) continue; // 丘のてっぺんのまわりはあける
                    float dw = float.MaxValue;
                    for (int wi = 0; wi < WallPts.Length; wi++) dw = Mathf.Min(dw, DistSeg(new Vector2(px, py), WallPts[wi], WallPts[(wi + 1) % WallPts.Length]));
                    if (dw < 10f) continue; // 城壁から 10m は木なし
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
            // 丘のてっぺんの少し大きな木
            if (game) Put(Has("Tree2_Broad_A") ? "Tree2_Broad_A" : "Tree_Oak", HillTop.x, H(HillTop.x, HillTop.y) - 0.1f, HillTop.y, 30f, Vector3.one * 1.6f);
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
            v3 = game && Has("Dorm_Knight2") && !File.Exists(Path.Combine(LogsDir, "old_buildings.txt"));
            poolBeach = game && Has("Pool_Basin") && !File.Exists(Path.Combine(LogsDir, "no_poolbeach.txt"));
            ground1 = game && T("Tile_Lawn") != null && Has("Grass_Lawn_A") && Shader.Find("LifeSim/Ground") != null && !File.Exists(Path.Combine(LogsDir, "old_ground.txt"));
            cliffs2 = game && Has("Cliff2_A") && mats.ContainsKey("Rock_Cliff@Cliff2_A") && !File.Exists(Path.Combine(LogsDir, "old_cliffs.txt"));
            Ground();
            Cliffs();
            Walls();
            Paths();
            if (game) ArcadeRing(); // 本館・体育館・大講堂・わたしたちの寮は NewDorm.cs（本当の建物）
            else { School(); OurDorm(); }
            OtherDorms();
            Trees();
            if (game) Decorations();
            if (poolBeach) { Pool(); Beach(); ShoreLights(); }
            if (game && Has("Bush_Round")) Nature();
            if (game) { CliffDressing(); if (Has("Bush_Round")) BaseShrubs(); GrassFieldBuild(); StartCoroutine(LawnFix()); FoamMask(); }
        }

        static bool Has(string n) { return LoadFurn(n) != null; }

        /// <summary>湖の泡を島の岸の近く（岸から約 16m まで）だけにする目印の画像（famuru 2026-11-08「岸の泡と波」。沖に白い点が浮いて見えたため）。</summary>
        void FoamMask()
        {
            if (water == null || !water.HasProperty("_FoamMask")) return;
            const int W = 400, Hh = 320; float x0 = -200f, x1 = 200f, y0 = -80f, y1 = 240f;
            var px = new Color[W * Hh];
            for (int j = 0; j < Hh; j++)
                for (int i = 0; i < W; i++)
                {
                    var p = new Vector2(x0 + (i + 0.5f) / W * (x1 - x0), y0 + (j + 0.5f) / Hh * (y1 - y0));
                    float v = 1f;
                    if (!Inside(Island, p))
                    {
                        float d = float.MaxValue;
                        for (int e = 0, f = Island.Length - 1; e < Island.Length; f = e++) d = Mathf.Min(d, DistSeg(p, Island[f], Island[e]));
                        v = 1f - Mathf.SmoothStep(0f, 1f, (d - 10f) / 8f);
                    }
                    px[j * W + i] = new Color(v, v, v, 1f);
                }
            var t = new Texture2D(W, Hh, TextureFormat.RGBA32, false, true) { wrapMode = TextureWrapMode.Clamp, name = "FoamMask" };
            t.SetPixels(px); t.Apply(false);
            water.SetTexture("_FoamMask", t);
            water.SetMatrix("_FoamW2L", root.worldToLocalMatrix);
            water.SetVector("_FoamRect", new Vector4(x0, y0, 1f / (x1 - x0), 1f / (y1 - y0)));
        }

        // ---------------- プールと浜辺（依頼29。famuru 2026-11-06 決定・2026-11-07「進めて」） ----------------
        // プール：わたしたちの寮と魔術師団の寮のあいだの芝生（縁の外 20×10m、中心 72,51。縁の上が地面の高さ、底 −1.4m）。まわりに石畳、更衣小屋、寝椅子とパラソル
        // 浜辺：北東の城壁の外。島のふち（東の岸）を約46m、崖をやめて白い砂浜に。草原から木の階段で降りる。城壁に小さな扉（閉じた形）。Logs\no_poolbeach.txt で無しに戻す
        bool poolBeach;
        static readonly Rect PoolRect = Rect.MinMaxRect(62f, 46f, 82f, 56f);
        static readonly Rect PoolArea = Rect.MinMaxRect(58f, 42f, 90f, 60f);
        static bool InPoolHole(float x, float y) { return x > PoolRect.xMin + 0.5f && x < PoolRect.xMax - 0.5f && y > PoolRect.yMin + 0.5f && y < PoolRect.yMax - 0.5f; }

        // 浜辺の岸の線（島のふちの上。南→北）
        static readonly Vector2[] Coast = { new Vector2(118.34f, 66f), new Vector2(116.69f, 90f), new Vector2(116f, 100f), new Vector2(110.39f, 110.6f) };
        const float BankS = 12f; // 岸から草原のふち（土手）までの奥行き
        static float CoastLen { get { float L = 0f; for (int i = 0; i + 1 < Coast.Length; i++) L += (Coast[i + 1] - Coast[i]).magnitude; return L; } }
        static Vector2 SegIn(int i) { Vector2 d = (Coast[i + 1] - Coast[i]).normalized; return new Vector2(-d.y, d.x); } // 島の内側（西）向き
        /// <summary>岸の線にそった位置 along（m）と、岸からの奥行き across（内側が＋）。</summary>
        static void CoastLocal(Vector2 p, out float along, out float across)
        {
            float best = float.MaxValue, acc = 0f; along = 0f; across = 0f;
            for (int i = 0; i + 1 < Coast.Length; i++)
            {
                Vector2 a = Coast[i], b = Coast[i + 1], d = b - a; float L = d.magnitude; d /= L;
                float t = Mathf.Clamp(Vector2.Dot(p - a, d), 0f, L);
                Vector2 q = a + d * t;
                float dist = (p - q).magnitude;
                if (dist < best) { best = dist; along = acc + t; across = Vector2.Dot(p - q, SegIn(i)); }
                acc += L;
            }
        }
        /// <summary>浜辺の帯の中なら岸からの奥行きを返す（帯の外は 999）。ext＝帯の両はしを延ばす長さ。</summary>
        static float BeachAcross(Vector2 p, float ext)
        {
            float along, across; CoastLocal(p, out along, out across);
            if (along < -ext || along > CoastLen + ext || across < -40f) return 999f;
            if ((along <= 0.01f || along >= CoastLen - 0.01f) && ext <= 0f) return 999f;
            return across;
        }
        /// <summary>岸の線の位置 along から、奥行き across の点と、そこでの内向き。</summary>
        static Vector2 CoastPoint(float along, float across, out Vector2 inward)
        {
            float acc = 0f;
            for (int i = 0; i + 1 < Coast.Length; i++)
            {
                float L = (Coast[i + 1] - Coast[i]).magnitude;
                if (along <= acc + L || i + 2 == Coast.Length)
                {
                    float t = Mathf.Clamp(along - acc, 0f, L);
                    Vector2 n = SegIn(i);
                    // 折れ目の近くは向きをなめらかに
                    if (t < 2f && i > 0) n = Vector2.Lerp((SegIn(i - 1) + n).normalized, n, t / 2f).normalized;
                    if (t > L - 2f && i + 2 < Coast.Length) n = Vector2.Lerp(n, (SegIn(i + 1) + n).normalized, (t - (L - 2f)) / 2f).normalized;
                    inward = n;
                    return Coast[i] + (Coast[i + 1] - Coast[i]).normalized * t + n * across;
                }
                acc += L;
            }
            inward = SegIn(0); return Coast[0];
        }
        /// <summary>浜辺の波打ちぎわ（寄せては返す波・泡の線・濡れた砂。LifeSim/Swash）。famuru 2026-11-08「岸の泡と波」。砂の上に薄い帯を重ねる。</summary>
        void Swash(float total)
        {
            var sh = Shader.Find("LifeSim/Swash");
            if (sh == null) return;
            const float wl = 2.25f; // 水ぎわ（SandH が水面 −3 になる奥行き）
            var acr = new List<float>();
            for (float a = 0.8f; a <= 6.01f; a += 0.25f) acr.Add(a);
            int nc = acr.Count, na = Mathf.CeilToInt(total / 0.5f);
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var col = new List<Color>(); var tr = new List<int>();
            for (int i = 0; i <= na; i++)
            {
                float al = Mathf.Min(total, i * 0.5f);
                float fade = Mathf.Clamp01(Mathf.Min(al, total - al) / 4f);
                for (int j = 0; j < nc; j++)
                {
                    Vector2 n; Vector2 p = CoastPoint(al, acr[j], out n);
                    float noise = (Mathf.PerlinNoise(p.x * 0.35f, p.y * 0.35f) - 0.5f) * 0.12f * Mathf.Clamp01(acr[j] / 3f);
                    v.Add(new Vector3(p.x, SandH(acr[j]) + noise + 0.02f, p.y));
                    uv.Add(new Vector2(al, acr[j] - wl));
                    col.Add(new Color(1f, 1f, 1f, fade * Mathf.Clamp01((6f - acr[j]) / 1f)));
                }
            }
            for (int i = 0; i < na; i++)
                for (int j = 0; j + 1 < nc; j++)
                {
                    int a0 = i * nc + j, a1 = a0 + 1, b0 = a0 + nc, b1 = b0 + 1;
                    tr.Add(a0); tr.Add(b0); tr.Add(a1); tr.Add(a1); tr.Add(b0); tr.Add(b1);
                }
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, name = "Beach_Swash" };
            mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetColors(col); mesh.SetTriangles(tr, 0);
            mesh.RecalculateNormals();
            if (mesh.normals.Length > 0 && mesh.normals[nc / 2].y < 0f) { tr.Reverse(); mesh.SetTriangles(tr, 0); mesh.RecalculateNormals(); }
            mesh.RecalculateBounds();
            var m = new Material(sh) { name = "Beach_Swash" };
            if (water != null && water.HasProperty("_NoiseTex")) m.SetTexture("_NoiseTex", water.GetTexture("_NoiseTex"));
            m.SetFloat("_Reach", Tune("Swash_reach", 2.2f)); m.SetFloat("_Period", Tune("Swash_period", 7f));
            var go = new GameObject("Beach_Swash");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = m;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>CoastPoint と同じ。ただし岸の線の両はしより先は、はしの向きのまま延ばす。</summary>
        static Vector2 CoastPointExt(float along, float across)
        {
            float total = CoastLen; Vector2 n;
            if (along < 0f) { Vector2 d0 = (Coast[1] - Coast[0]).normalized; return Coast[0] + d0 * along + SegIn(0) * across; }
            if (along > total) { int l = Coast.Length - 1; Vector2 d1 = (Coast[l] - Coast[l - 1]).normalized; return Coast[l] + d1 * (along - total) + SegIn(l - 1) * across; }
            return CoastPoint(along, across, out n);
        }

        static float SandH(float across)
        {
            if (across >= 0f) return Mathf.Lerp(-3.2f, -2.5f, Mathf.Pow(Mathf.Clamp01(across / BankS), 0.75f));
            return Mathf.Max(-9.5f, -3.2f + across * 0.32f); // 沖へ深く（水の色が自然に濃くなるように）
        }
        // 城壁の小さな扉（北東のななめの城壁の上）と、浜辺の階段の上
        static Vector2 BeachDoor { get { return Vector2.Lerp(new Vector2(96, 86), new Vector2(80, 108), 0.32f); } }
        static Vector2 BeachDoorOut { get { Vector2 d = new Vector2(80, 108) - new Vector2(96, 86); Vector2 n = new Vector2(d.y, -d.x).normalized; if (n.x < 0f) n = -n; return BeachDoor + n * 2.2f; } }
        const float StairAlong = 27f;
        const float StairOriginS = BankS - 0.4f - 2.45f; // 階段の原点の奥行き（上の踊り場が土手のふちのすぐ外）
        static Vector2 BeachStairTop { get { Vector2 n; return CoastPoint(StairAlong, StairOriginS + 2.45f - 0.2f, out n); } } // 上の踊り場の上まで

        // ---------------- プールと浜辺で過ごす（famuru 2026-11-08「プールと浜辺で過ごせるように」。家具のチャット） ----------------
        // 歩ける所（OpenShore）・浜の高さ（GroundLift）・席と見どころの位置（LoungeWorld の CampusShore.cs が使う）
        static int poolBeachOn = -1;
        /// <summary>プールと浜辺があるか（依頼29 の家具があり、Logs\no_poolbeach.txt が無い）。</summary>
        public static bool PoolBeachOn
        {
            get
            {
                if (poolBeachOn < 0) poolBeachOn = LoungeWorld.NewLayout && Has("Pool_Basin") && !File.Exists(Path.Combine(LogsDir, "no_poolbeach.txt")) ? 1 : 0;
                return poolBeachOn == 1;
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetShore() { poolBeachOn = -1; }

        static Vector2 WallN { get { Vector2 d = (new Vector2(80, 108) - new Vector2(96, 86)).normalized; Vector2 n = new Vector2(d.y, -d.x); if (n.x < 0f) n = -n; return n; } }
        /// <summary>寮の西の芝生の小道：本館の北の玄関の道（−4,96）→ 城の北の城壁ぞい → 城壁の小さな扉の内側（見た目と歩ける所の両方）。</summary>
        public static CampusPlan.PathDef ShoreInnerPath { get { return new CampusPlan.PathDef(2.2f, new Vector2(-4f, 96f), new Vector2(-1.5f, 100.3f), new Vector2(36f, 100.3f), new Vector2(62f, 100f), new Vector2(80f, 98f), BeachDoor - WallN * 1.25f); } }

        /// <summary>プールの石畳の北西のすみ → 寮の中庭の北（城壁とのあいだ）→ 塔と中庭のすきま → 城壁の小さな扉の内側（近道。2026-11-08）。</summary>
        public static CampusPlan.PathDef ShorePoolPath { get { return new CampusPlan.PathDef(1.6f, new Vector2(84.5f, 58.5f), new Vector2(92f, 60f), new Vector2(92.1f, 82f), new Vector2(91.7f, 87f), new Vector2(90.5f, 90.5f), BeachDoor - WallN * 1.25f); } }
        public static float ShoreLen { get { return CoastLen; } }
        public const float ShoreBank = BankS;
        public const float StairAlongPos = StairAlong;
        public const float PierAlong = 16f;
        /// <summary>浜辺の位置（岸の線にそって along・岸からの奥行き across）を、ゲームの座標（1階の床の高さ）で。</summary>
        public static Vector3 ShoreW(float along, float across) { Vector2 n; Vector2 p = CoastPoint(along, across, out n); return CampusPlan.G(p.x, p.y); }
        /// <summary>その位置での島の内側（陸）の向き（ゲームの座標）。</summary>
        public static Vector3 ShoreIn(float along) { Vector2 n; CoastPoint(along, 5f, out n); return new Vector3(-n.y, 0f, n.x); }
        /// <summary>配置図の点（px, py）をゲームの座標に。</summary>
        public static Vector3 PlanW(float px, float py) { return CampusPlan.G(px, py); }
        /// <summary>配置図の向き（dx, dy）をゲームの向きに。</summary>
        public static Vector3 PlanDir(float dx, float dy) { return new Vector3(-dy, 0f, dx); }
        public static Rect PoolRim { get { return PoolRect; } }
        public static Rect PoolZone { get { return Rect.MinMaxRect(58.5f, 42.5f, 89.5f, 59.5f); } }
        /// <summary>ゲームの矩形（配置図の矩形から）。</summary>
        public static Rect PlanRectW(Rect r) { Vector3 a = CampusPlan.G(r.xMin, r.yMin), b = CampusPlan.G(r.xMax, r.yMax); return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.z, b.z), Mathf.Max(a.x, b.x), Mathf.Max(a.z, b.z)); }
        static Vector2 ToPlan(Vector3 w) { return new Vector2(w.z + CampusPlan.OX, CampusPlan.OY - w.x); }

        /// <summary>浜の中（砂・階段・桟橋）なら true と、岸にそった位置・奥行き。</summary>
        static bool OnShore(Vector3 w, out float along, out float across)
        {
            CoastLocal(ToPlan(w), out along, out across);
            return along > -2f && along < CoastLen + 2f && across > -20f && across < BankS + 0.5f;
        }

        /// <summary>プールか浜辺にいるか（水着に着がえる所）。</summary>
        public static bool InSwimZone(Vector3 w)
        {
            if (!PoolBeachOn || w.y > 0.5f) return false;
            float al, ac;
            return PoolZone.Contains(ToPlan(w)) || OnShore(w, out al, out ac);
        }

        /// <summary>
        /// 1階の床（高さ 0）を歩く人の見た目だけを、浜の砂・階段・桟橋の高さに下げる量（m。浜の外は 0）。
        /// 歩く網は床の高さのまま（famuru 2026-11-08）。
        /// </summary>
        public static float GroundLift(Vector3 w)
        {
            if (!PoolBeachOn || w.y > 0.5f) return 0f;
            float al, ac;
            if (!OnShore(w, out al, out ac)) return 0f;
            // 階段（下の踊り場 奥行き 6.4・高さ −2.53 → 上の踊り場 奥行き 11.6・高さ +0.05）
            const float sBot = StairOriginS - 2.75f, sTop = StairOriginS + 2.45f, hBot = -2.836f + 0.307f, hTop = -2.836f + 2.886f;
            if (Mathf.Abs(al - StairAlong) < 1.3f && ac > sBot - 0.6f)
                return ac >= sTop ? 0f : Mathf.Lerp(hBot, hTop, Mathf.Clamp01((ac - sBot) / (sTop - sBot)));
            if (ac >= BankS - 0.3f) return 0f;
            // 桟橋（板の上 −4.25 + 1.79）
            if (Mathf.Abs(al - PierAlong) < 1.4f && ac < 1.8f) return Mathf.Max(SandH(ac), -4.25f + 1.79f);
            // 浜の休憩小屋の床（依頼29 追記3）
            Vector2 hl = HutLocal(true, w);
            if (Mathf.Abs(hl.x) < 1.95f && hl.y > -2.1f && hl.y < 2.4f) return SandH(BeachHutAcross);
            return Mathf.Max(SandH(ac), -3.1f);
        }

        /// <summary>プールと浜辺を1階の歩ける網に開ける（NewDorm の NNav。ふさぐ所は LoungeWorld の blockers で）。</summary>
        public static void OpenShore(GridNav n)
        {
            if (!PoolBeachOn) return;
            System.Action<float, Vector2[]> line = (w, pts) =>
            {
                float h = w / 2f - 0.25f;
                for (int i = 0; i + 1 < pts.Length; i++)
                {
                    float len = Vector2.Distance(pts[i], pts[i + 1]);
                    int k = Mathf.Max(1, Mathf.CeilToInt(len / 0.25f));
                    for (int s = 0; s <= k; s++)
                    {
                        Vector2 p = Vector2.Lerp(pts[i], pts[i + 1], s / (float)k);
                        Vector3 g = CampusPlan.G(p.x, p.y);
                        n.Open(Rect.MinMaxRect(g.x - h, g.z - h, g.x + h, g.z + h));
                    }
                }
            };
            // プール：寮の前の道からの小道と、まわりの石畳（水の上は開けない）
            line(2.2f, new[] { new Vector2(42.5f, 51f), new Vector2(PoolRect.xMin - 1f, 51f) });
            float o = 3f - 0.3f, x0 = PoolRect.xMin, x1 = PoolRect.xMax, y0 = PoolRect.yMin, y1 = PoolRect.yMax;
            n.Open(PlanRectW(Rect.MinMaxRect(x0 - o, y0 - o, x1 + o, y0 - 0.2f)));
            n.Open(PlanRectW(Rect.MinMaxRect(x0 - o, y1 + 0.2f, x1 + o, y1 + o)));
            n.Open(PlanRectW(Rect.MinMaxRect(x0 - o, y0 - o, x0 - 0.2f, y1 + o)));
            n.Open(PlanRectW(Rect.MinMaxRect(x1 + 0.2f, y0 - o, x1 + o, y1 + o)));
            // 更衣小屋の前と中（中が見える小屋が届くまでは、中に入ると見えない）
            n.Open(PlanRectW(Rect.MinMaxRect(x1 + o, PoolRect.center.y + 0.6f, x1 + 4.0f, PoolRect.center.y + 3.4f)));
            n.Open(PlanRectW(PoolHutInside));
            // 寮の西の芝生の小道 → 城壁の小さな扉（壁の中を通る）→ 浜辺の階段の上
            line(2.2f, ShoreInnerPath.pts);
            line(1.6f, ShorePoolPath.pts);
            Vector2 wn = WallN;
            line(1.6f, new[] { BeachDoor - wn * 1.25f, BeachDoor + wn * 0.6f });
            line(2f, new[] { BeachDoor + wn * 0.6f, BeachDoor + wn * 4f, BeachStairTop });
            // 階段（上の踊り場 → 下の踊り場の先の砂）
            {
                Vector2 nn; Vector2 top = CoastPoint(StairAlong, StairOriginS + 2.3f, out nn), bot = CoastPoint(StairAlong, StairOriginS - 3.4f, out nn);
                line(1.4f, new[] { BeachStairTop, top, bot });
            }
            // 砂浜（岸から 1.2〜10.5m。階段の両わきの砂の上の部分はよける）
            float total = CoastLen;
            for (float al = 1.2f; al <= total - 1.2f; al += 0.25f)
                for (float ac = 1.2f; ac <= 10.5f; ac += 0.25f)
                {
                    if (Mathf.Abs(al - StairAlong) < 1.35f && ac > StairOriginS - 2.9f) continue;
                    Vector2 nn; Vector2 p = CoastPoint(al, ac, out nn);
                    Vector3 g = CampusPlan.G(p.x, p.y);
                    n.Open(Rect.MinMaxRect(g.x - 0.18f, g.z - 0.18f, g.x + 0.18f, g.z + 0.18f));
                }
            // 浜の休憩小屋の中
            {
                Vector3 c = ShoreW(BeachHutAlong, BeachHutAcross);
                n.Open(new Rect(c.x - 1.3f, c.z - 1.3f, 2.6f, 2.6f));
            }
            // 桟橋（岸 → 湖へ 20m）
            {
                Vector2 nn; Vector2 a = CoastPoint(PierAlong, 1.6f, out nn), b = CoastPoint(PierAlong, -18.2f, out nn);
                line(1.5f, new[] { a, b });
            }
        }

        /// <summary>更衣小屋の中（配置図の矩形）。</summary>
        public static Rect PoolHutInside { get { float c = PoolRect.xMax + 3f + 2.2f, cy = PoolRect.center.y + 2f; return Rect.MinMaxRect(c - 1.6f, cy - 1.6f, c + 1.6f, cy + 1.6f); } }

        // ---- 依頼29 追記3 の小屋（プールと浜辺_v2）：置き方と、小屋の中の点（入れ物の座標 x＝横・z＝正面が＋）→ ゲームの座標 ----
        public static void PoolHutPose(out Vector2 p, out float h, out float yaw) { p = new Vector2(PoolRect.xMax + 3f + 2.2f, PoolRect.center.y + 2f); h = 0f; yaw = -90f; }
        public static void BeachHutPose(out Vector2 p, out float h, out float yaw) { Vector2 n; p = CoastPoint(BeachHutAlong, BeachHutAcross, out n); h = SandH(BeachHutAcross) - 0.05f; yaw = YawFacing(-n); }
        public static Vector3 HutW(bool beach, float lx, float lz)
        {
            Vector2 p; float h, yaw; if (beach) BeachHutPose(out p, out h, out yaw); else PoolHutPose(out p, out h, out yaw);
            Vector3 v = Quaternion.Euler(0f, yaw, 0f) * new Vector3(lx, 0f, lz);
            return CampusPlan.G(p.x + v.x, p.y + v.z);
        }
        public static Vector3 HutDir(bool beach, float lx, float lz)
        {
            Vector2 p; float h, yaw; if (beach) BeachHutPose(out p, out h, out yaw); else PoolHutPose(out p, out h, out yaw);
            Vector3 v = Quaternion.Euler(0f, yaw, 0f) * new Vector3(lx, 0f, lz);
            return PlanDir(v.x, v.z).normalized;
        }
        /// <summary>配置図の点が小屋の床の上か（まわり m をふくむ）。</summary>
        static bool PlanInHut(Vector2 p, float m)
        {
            Rect r = PoolHutInside; if (p.x > r.xMin - 0.2f - m && p.x < r.xMax + 0.2f + m && p.y > r.yMin - 0.2f - m && p.y < r.yMax + 0.2f + m) return true;
            Vector2 hp; float hh, yaw; BeachHutPose(out hp, out hh, out yaw);
            Vector3 v = Quaternion.Euler(0f, -yaw, 0f) * new Vector3(p.x - hp.x, 0f, p.y - hp.y);
            return Mathf.Abs(v.x) < 2.05f + m && v.z > -2.4f - m && v.z < 2.4f + m;
        }

        /// <summary>ゲームの点を、小屋の入れ物の座標（x, z）に。</summary>
        public static Vector2 HutLocal(bool beach, Vector3 w)
        {
            Vector2 p; float h, yaw; if (beach) BeachHutPose(out p, out h, out yaw); else PoolHutPose(out p, out h, out yaw);
            Vector2 q = ToPlan(w) - p;
            Vector3 v = Quaternion.Euler(0f, -yaw, 0f) * new Vector3(q.x, 0f, q.y);
            return new Vector2(v.x, v.z);
        }
        /// <summary>小屋の壁（入れ物の座標の線分 x0,z0,x1,z1）。プールと浜辺_v2 の高さ 1m の平面図から（2026-11-09）。</summary>
        public static readonly float[] PoolHutWalls = {
            -1.75f, -1.7f, 1.75f, -1.7f,   -1.65f, -1.75f, -1.65f, 1.75f,   1.7f, -1.75f, 1.7f, 1.75f,   // 奥・左・右
            -1.75f, 1.7f, -1.3f, 1.7f,   -0.25f, 1.7f, 0.28f, 1.7f,   1.38f, 1.7f, 1.75f, 1.7f,        // 正面（扉2つ。口は約 1m）
            0f, 1.55f, 0f, 0.4f,   0f, 0.4f, -0.6f, 0.4f,   -0.6f, 0.4f, -0.6f, -0.5f,   -0.6f, -0.5f, 0.55f, -0.5f,   0.5f, -0.5f, 0.5f, -1.6f }; // 段違いの仕切り
        public static readonly float[] BeachHutWalls = {
            -1.85f, 0.5f, -1.5f, 0.5f,   -0.32f, 0.5f, 1.85f, 0.5f,                                       // 正面（扉1つ）
            -1.7f, 0.5f, -1.7f, -2.0f,   1.7f, 0.5f, 1.7f, -2.0f,   -1.8f, -2.0f, 1.8f, -2.0f,            // 左・右・奥
            1.1f, -1.15f, 1.6f, -1.15f,   1.1f, -1.65f, 1.6f, -1.65f,   1.1f, -1.15f, 1.1f, -1.65f,        // 小棚
            -0.3f, 1.25f, 1.5f, 1.25f,                                                                     // 外のベンチ
            -1.9f, 2.1f, -1.9f, 2.1f,   1.9f, 2.1f, 1.9f, 2.1f };                                         // ひさしの柱
        public const float BeachHutAlong = 37f, BeachHutAcross = 8.5f;
        public const float CampfireAlong = 31f, CampfireAcross = 5f;
        public static readonly float[] PoolLoungerX = { 2f, 4f, 11f, 13f }, PoolLoungerXS = { 8f, 10f };
        public static readonly float[] BeachLoungerAlong = { 6f, 8f, 21f, 23f };
        public const float BeachLoungerAcross = 5.5f;

        /// <summary>道を作らない所（向きのある長方形）。つなぐ相手が上に重なる所。</summary>
        struct PathCover
        {
            public Vector2 c, u; public float hl, hw;
            public PathCover(Vector2 c, Vector2 u, float hl, float hw) { this.c = c; this.u = u.normalized; this.hl = hl; this.hw = hw; }
            public bool Contains(Vector2 p) { Vector2 d = p - c; return Mathf.Abs(Vector2.Dot(d, u)) < hl && Mathf.Abs(d.x * u.y - d.y * u.x) < hw; }
        }
        readonly List<PathCover> pathCovers = new List<PathCover>();

        void Pool()
        {
            Vector2 c = PoolRect.center;
            Put("Pool_Basin", c.x, 0f, c.y, 0f);
            // 水（縁より 0.2m 下）
            var wm = new Material(Shader.Find("LifeSim/Water") ?? water.shader) { name = "Pool_Water" };
            if (wm.HasProperty("_ShallowColor")) { wm.SetColor("_ShallowColor", new Color(0.42f, 0.86f, 0.88f, 0.45f)); wm.SetColor("_DeepColor", new Color(0.12f, 0.52f, 0.66f, 0.85f)); wm.SetFloat("_DepthDist", 2.2f); wm.SetFloat("_FoamDist", 0.12f); }
            else wm.color = new Color(0.3f, 0.75f, 0.85f);
            if (water != null && water.HasProperty("_NormalTex") && wm.HasProperty("_NormalTex")) wm.SetTexture("_NormalTex", water.GetTexture("_NormalTex"));
            var w = GameObject.CreatePrimitive(PrimitiveType.Quad);
            w.name = "Pool_Water";
            w.transform.SetParent(root, false);
            w.transform.localPosition = new Vector3(c.x, -0.2f, c.y);
            w.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            w.transform.localScale = new Vector3(PoolRect.width - 1f, PoolRect.height - 1f, 1f);
            w.GetComponent<Renderer>().sharedMaterial = wm;
            Destroy(w.GetComponent<Collider>());
            // まわりの石畳（縁の外から 3m。穴のふちの地面のでこぼこを隠す）
            float o = 3f, x0 = PoolRect.xMin, x1 = PoolRect.xMax, y0 = PoolRect.yMin, y1 = PoolRect.yMax;
            var deck = Tiled("Tile_Flagstone", 1f, 1f);
            System.Action<string, float, float, float, float> slab = (nm, a0, b0, a1, b1) =>
            {
                var g = Box(nm, new Vector3((a0 + a1) / 2f, -0.11f, (b0 + b1) / 2f), new Vector3(a1 - a0, 0.3f, b1 - b0), Tiled("Tile_Flagstone", (a1 - a0) / 3f, (b1 - b0) / 3f)); // 上の面 +0.04（道 +0.015 より少し上で、道のはしを下に隠す）
            };
            slab("Pool_Deck_S", x0 - o, y0 - o, x1 + o, y0);
            slab("Pool_Deck_N", x0 - o, y1, x1 + o, y1 + o);
            slab("Pool_Deck_W", x0 - o, y0, x0, y1);
            slab("Pool_Deck_E", x1, y0, x1 + o, y1);
            // はしご（南と北の長い辺。底から）
            Put("Pool_Ladder", x0 + 3.5f, -1.4f, y0 + 0.6f, 0f);
            Put("Pool_Ladder", x0 + 3.5f, -1.4f, y1 - 0.6f, 180f);
            // 更衣小屋（東。正面をプールへ）
            {
                // 依頼29 追記3：中が見える更衣小屋（扉が開く・カメラが近いと屋根を消す）
                Vector2 hp; float hh, hy; PoolHutPose(out hp, out hh, out hy);
                var hut = Put("Pool_Hut", hp.x, hh, hp.y, hy); SetupMoving(hut);
                // 部品の床が奥の部屋と通路で欠けて芝生が見えるので、板の床を下に敷く
                if (hut != null && mats.ContainsKey("Wood_Dark"))
                {
                    var f = GameObject.CreatePrimitive(PrimitiveType.Cube); f.name = "Pool_Hut_Floor"; Destroy(f.GetComponent<Collider>());
                    f.transform.SetParent(hut.transform, false); f.transform.localPosition = new Vector3(0f, 0.02f, 0f); f.transform.localScale = new Vector3(3.4f, 0.03f, 3.4f);
                    f.GetComponent<Renderer>().sharedMaterial = mats["Wood_Dark"];
                }
            }
            // 寝椅子とパラソル（北の石畳に4つ・南に2つ。頭は外側）
            foreach (float x in new[] { x0 + 2f, x0 + 4f, x0 + 11f, x0 + 13f }) Put("Lounger", x, 0.02f, y1 + 1.4f, 180f);
            Put("Parasol", x0 + 3f, 0.02f, y1 + 2.2f, 0f); Put("Parasol", x0 + 12f, 0.02f, y1 + 2.2f, 30f);
            foreach (float x in new[] { x0 + 8f, x0 + 10f }) Put("Lounger", x, 0.02f, y0 - 1.4f, 0f);
            Put("Parasol", x0 + 9f, 0.02f, y0 - 2.2f, 0f);
            // 糸杉と灯り
            foreach (var t in new[] { new Vector2(x0 - o - 1f, y0 - o - 1f), new Vector2(x0 - o - 1f, y1 + o + 1f), new Vector2(x1 + o + 1f, y0 - o - 1f) })
                Put("Tree2_Cypress_L", t.x, 0f, t.y, t.x * 13f);
            if (Has("Lamp_Garden"))
                foreach (var t in new[] { new Vector2(x0 - o + 0.4f, y0 - o + 0.4f), new Vector2(x0 - o + 0.4f, y1 + o - 0.4f), new Vector2(x1 + o - 0.4f, y0 - o + 0.4f), new Vector2(x1 + o - 0.4f, y1 + o - 0.4f) })
                {
                    var lg = Put("Lamp_Garden", t.x, 0f, t.y, 0f);
                    if (game) AddLampLight(lg, new Vector3(0f, 0.9f, 0f), 1.3f, 6f); // 夜のプール（2026-11-09）
                }
        }

        // ---------------- 浜辺とプールの夜の明かり（famuru 2026-11-09「浜辺とプールの夜の明かり」。家具のチャット） ----------------
        // 寮から城壁の扉までの小道・プールからの小道に街灯、城壁の小さな扉の内と外に松明、浜辺の階段の上に街灯、桟橋と浜の小屋の軒にランタン。
        // 光は影なしの点の明かり（昼は CampusSky が消す）。
        void ShoreLights()
        {
            if (!game) return;
            var glow = ProtoArt.Glow(new Color(1f, 0.75f, 0.45f), 1.5f);
            System.Action<Vector2, float> post = (p, h) =>
            {
                var g = Put("LampPost", p.x, h, p.y, 0f);
                if (g == null) return;
                var s = GameObject.CreatePrimitive(PrimitiveType.Sphere); s.name = "LampGlow"; Destroy(s.GetComponent<Collider>());
                s.transform.SetParent(g.transform, false); s.transform.localPosition = new Vector3(0f, 2.28f, 0f); s.transform.localScale = Vector3.one * 0.2f;
                var r = s.GetComponent<Renderer>(); r.sharedMaterial = glow; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                AddLampLight(g, new Vector3(0f, 2.2f, 0f), 2.4f, 9f);
            };
            // 寮の西の芝生 → 城壁の小さな扉の小道（北がわ 1.9m）と、プールからの小道（西がわ）
            foreach (var p in new[] { new Vector2(14f, 102.2f), new Vector2(32f, 102.2f), new Vector2(50f, 102.1f), new Vector2(68f, 101.6f) }) post(p, H(p.x, p.y));
            post(new Vector2(90.2f, 71f), 0f);
            // 城壁の小さな扉の内と外の松明（扉の左右どちらか 1.7m）
            if (Has("WallTorch"))
            {
                Vector2 wn = WallN, wd = new Vector2(-wn.y, wn.x);
                var tin = Put("WallTorch", (BeachDoor - wn * Tune("Torch_in", 1.3f) + wd * 1.7f).x, 2.3f, (BeachDoor - wn * Tune("Torch_in", 1.3f) + wd * 1.7f).y, YawFacing(-wn));
                AddFireLight(tin, new Vector3(0f, 0.75f, -0.25f), 1.4f, 6f);
                var tout = Put("WallTorch", (BeachDoor + wn * Tune("Torch_out", 1.3f) + wd * 1.7f).x, 2.3f, (BeachDoor + wn * Tune("Torch_out", 1.3f) + wd * 1.7f).y, YawFacing(wn));
                AddFireLight(tout, new Vector3(0f, 0.75f, -0.25f), 1.4f, 6f);
            }
            // 浜辺の階段の上の踊り場のわき
            {
                Vector2 n; Vector2 p = CoastPoint(StairAlong + 2.1f, StairOriginS + 2.4f, out n);
                post(p, HBase(p.x, p.y));
            }
            // 桟橋の付け根と先のランタン（板の上 −4.25 + 1.79）、浜の小屋の軒のランタン
            System.Action<Vector2, float> lantern = (p, h) =>
            {
                var g = Put("Lantern", p.x, h, p.y, 0f, Vector3.one * 1.6f);
                if (g == null) return;
                var s = GameObject.CreatePrimitive(PrimitiveType.Sphere); s.name = "LanternGlow"; Destroy(s.GetComponent<Collider>());
                s.transform.SetParent(g.transform, false); s.transform.localPosition = new Vector3(0f, 0.17f, 0f); s.transform.localScale = Vector3.one * 0.12f;
                var r = s.GetComponent<Renderer>(); r.sharedMaterial = glow; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                AddLampLight(g, new Vector3(0f, 0.4f, 0f), 1.6f, 6f);
            };
            {
                Vector2 n;
                lantern(CoastPoint(PierAlong + 0.95f, -3.2f, out n), -4.25f + 1.79f);
                lantern(CoastPoint(PierAlong - 0.95f, -17.6f, out n), -4.25f + 1.79f);
                Vector2 hp; float hh, hy; BeachHutPose(out hp, out hh, out hy);
                Vector3 v = Quaternion.Euler(0f, hy, 0f) * new Vector3(-1.75f, 0f, 2.0f);
                lantern(new Vector2(hp.x + v.x, hp.y + v.z), hh + 0.02f);
            }
            sky.AddLamp(null, glow);
        }

        void AddLampLight(GameObject h, Vector3 local, float intensity, float range)
        {
            if (h == null) return;
            var l = new GameObject("LampLight").AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(1f, 0.74f, 0.45f); l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
            l.transform.SetParent(h.transform, false);
            l.transform.localPosition = local;
            sky.AddLamp(l, null);
        }

        void Beach()
        {
            float total = CoastLen;
            // 砂浜の地面（乾いた砂と、水ぎわの濡れた砂）
            var dry = TilePBR.Apply(ProtoArt.Mat(Color.white, T("Tile_Sand"), null, 0.08f), "Tile_Sand");
            var wet = TilePBR.Apply(ProtoArt.Mat(Color.white, T("Tile_SandWet") ?? T("Tile_Sand"), null, 0.35f), "Tile_SandWet");
            const float step = 1f;
            int na = Mathf.CeilToInt(total / step), nc = 0;
            var across = new List<float>();
            // famuru 2026-11-08「浜辺の上下の端に隙間があるので埋めて」：砂を草原の下まで（奥行き 13.8・15.5 は地面の高さのすぐ下）のばし、
            // 帯の両はしも 5m 先までのばす（地面を張らない所が残っても、下から砂が見えて水が見えない）
            across.Add(BankS + 3.5f); across.Add(BankS + 1.8f);
            for (float a = BankS + 0.6f; a > -22.01f; a -= (a > -6f ? 1f : 2f)) across.Add(a);
            nc = across.Count;
            const int ext = 14;
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tDry = new List<int>(); var tWet = new List<int>();
            for (int i = -ext; i <= na + ext; i++)
            {
                float al = i < 0 ? i * step : (i > na ? total + (i - na) * step : Mathf.Min(total, i * step));
                bool outside = al < 0f || al > total;
                for (int j = 0; j < nc; j++)
                {
                    float acr = outside ? Mathf.Max(across[j], -3f) : across[j];
                    Vector2 p = CoastPointExt(al, acr);
                    float noise = (Mathf.PerlinNoise(p.x * 0.35f, p.y * 0.35f) - 0.5f) * 0.12f * Mathf.Clamp01(acr / 3f);
                    float endK = 1f - Mathf.Clamp01(Mathf.Min(al, total - al) / 8f); // 帯のはしの水の中は深くして、浅い所の色の境目を目立たせない
                    float hh = SandH(acr) - (acr < 1f ? endK * endK * 7f * Mathf.Clamp01((1f - acr) / 4f) : 0f);
                    // 砂は、地面を下げきった所（砂浜）だけ地面の上に出し、坂と草原では地面のすぐ下にそわせてかくす（2026-11-08）
                    float bacr; float vis = Mathf.SmoothStep(0f, 1f, (BeachLowW(p.x, p.y, out bacr) - 0.86f) / 0.11f);
                    hh = Mathf.Min(acr > BankS + 1f ? hh + 5f : hh, GroundAt(p.x, p.y) + Mathf.Lerp(-0.3f, 0.5f, vis));
                    verts.Add(new Vector3(p.x, hh + noise, p.y));
                    uvs.Add(new Vector2(p.x / 2f, p.y / 2f));
                }
            }
            na += ext * 2;
            for (int i = 0; i < na; i++)
                for (int j = 0; j + 1 < nc; j++)
                {
                    int a0 = i * nc + j, a1 = a0 + 1, b0 = a0 + nc, b1 = b0 + 1;
                    var list = (across[j] + across[j + 1]) / 2f > 2.5f ? tDry : tWet;
                    list.Add(a0); list.Add(b0); list.Add(a1); list.Add(a1); list.Add(b0); list.Add(b1);
                }
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.subMeshCount = 2; mesh.SetTriangles(tDry, 0); mesh.SetTriangles(tWet, 1);
            // 面の向きが下ならひっくり返す
            mesh.RecalculateNormals();
            if (mesh.normals.Length > 0 && mesh.normals[nc / 2].y < 0f) { tDry.Reverse(); tWet.Reverse(); mesh.SetTriangles(tDry, 0); mesh.SetTriangles(tWet, 1); mesh.RecalculateNormals(); }
            mesh.RecalculateBounds();
            var go = new GameObject("Beach_Sand");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = new[] { dry, wet };
            Swash(total);

            // 草原と砂浜のあいだの土手（崖の部品を低く）と、両はしのふた
            if (cliffs2)
            {
                // 依頼26 の崖のかたまりを低い土手に（下は砂に埋まる）
                int bi = 0;
                for (float al = 3f; al < total - 2f; al += Tune("Bank2_step", 6f), bi++)
                {
                    Vector2 n; Vector2 p = CoastPoint(al, BankS, out n);
                    float sx = Tune("Bank2_sx", 0.72f) * (0.95f + Hash01(bi, 211) * 0.12f);
                    PutCliff2(Cliff2Walls[bi % 3], p, -n, YawFacing(-n) + (Hash01(bi, 223) - 0.5f) * 8f, sx, HBase(p.x, p.y));
                }
            }
            else
            for (float al = 2f; al < total - 1f; al += 5f)
            {
                Vector2 n; Vector2 p = CoastPoint(al + 2.5f, BankS, out n);
                Vector2 c = p + n * 4f;
                Put("Cliff", c.x, -3.4f, c.y, YawFacing(-n), new Vector3(5f / 10f * 1.15f, 3.3f / 20f, 1f)); // 上の面は草の下（-0.1）に隠す
            }
            // 浜辺の両はし：崖をやめた所の島のふちを、大きな岩でかくす
            if (Has("Rock_Large"))
                for (int e = 0; e < 2; e++)
                {
                    Vector2 a = e == 0 ? Coast[0] : Coast[Coast.Length - 1];
                    Vector2 d = e == 0 ? (Coast[0] - Coast[1]).normalized : (Coast[Coast.Length - 1] - Coast[Coast.Length - 2]).normalized; // 帯の外へ
                    Vector2 inw = e == 0 ? SegIn(0) : SegIn(Coast.Length - 2);
                    for (int k = 0; k < 4; k++)
                    {
                        float h1 = Hash01(e * 7 + k, 13), h2 = Hash01(k, e * 5 + 3);
                        Vector2 q = a + d * (0.5f + k * 2.3f) + inw * (1.5f - k * 0.4f + (h1 - 0.5f) * 1.5f);
                        if (cliffs2) Put(k % 2 == 0 ? "Rock_Shore_A" : "Rock_Shore_B", q.x, WaterY + 0.9f + h2 * 0.8f, q.y, h1 * 360f, Vector3.one * (1.3f + h1 * 0.5f)); // 依頼26 の岸の岩
                        else Put("Rock_Large", q.x, WaterY - 1.2f + h2 * 0.6f, q.y, h1 * 360f, new Vector3(2.2f + h1 * 1.2f, 2.4f + h2 * 1.4f, 2.0f + h2));
                    }
                }

            // 城壁の小さな扉（閉じた形。famuru「正門と橋は基本的に閉じている形」と同じく）
            {
                Vector2 d = new Vector2(80, 108) - new Vector2(96, 86); Vector2 n = new Vector2(d.y, -d.x).normalized; if (n.x < 0f) n = -n;
                SetupMoving(Put("Castle_SmallDoor", BeachDoor.x, 0f, BeachDoor.y, YawFacing(n))); // 依頼29 追記3：人が通るときだけ開く
            }
            // 草原から砂浜へ降りる木の階段（上の踊り場が土手のふち、下が砂の上）
            {
                Vector2 n; Vector2 p = CoastPoint(StairAlong, StairOriginS, out n);
                Put("Beach_Stairs", p.x, -2.886f + 0.05f, p.y, YawFacing(-n));
            }
            // 桟橋（岸から湖へ2区間）とボート
            {
                Vector2 n; Vector2 p1 = CoastPoint(16f, -3.5f, out n); Vector2 p2 = CoastPoint(16f, -13.5f, out n);
                Put("Pier", p1.x, -4.25f, p1.y, YawFacing(-n));
                Put("Pier", p2.x, -4.25f, p2.y, YawFacing(-n));
                Vector2 side = new Vector2(n.y, -n.x);
                Vector2 b = CoastPoint(16f, -9f, out n) + side * 2.6f;
                Put("Boat", b.x, WaterY - 0.3f, b.y, YawFacing(-n) + 12f);
            }
            // 浜の休憩小屋・寝椅子とパラソル・たき火・流木
            {
                Vector2 n; Vector2 p = CoastPoint(37f, 8.5f, out n);
                { Vector2 hp; float hh, hy; BeachHutPose(out hp, out hh, out hy); SetupMoving(Put("Beach_Hut", hp.x, hh, hp.y, hy)); }
                foreach (var al in new[] { 6f, 8f, 21f, 23f })
                {
                    Vector2 q = CoastPoint(al, 5.5f, out n);
                    Put("Lounger", q.x, SandH(5.5f), q.y, YawFacing(-n));
                }
                foreach (var al in new[] { 7f, 22f })
                {
                    Vector2 q = CoastPoint(al, 7f, out n);
                    Put("Parasol", q.x, SandH(7f), q.y, al * 9f);
                }
                Vector2 f = CoastPoint(31f, 5f, out n);
                var fire = Put("Campfire", f.x, SandH(5f), f.y, 20f);
                AddFireLight(fire, new Vector3(0f, 0.7f, 0f), 2.4f, 9f);
                Vector2 dw = CoastPoint(3.5f, 2.6f, out n);
                Put("Driftwood", dw.x, SandH(2.6f) - 0.05f, dw.y, 70f);
                Vector2 dw2 = CoastPoint(42f, 3.2f, out n);
                Put("Driftwood", dw2.x, SandH(3.2f) - 0.05f, dw2.y, -40f, Vector3.one * 0.8f);
                if (Has("Rock_Large"))
                    foreach (var r in new[] { new Vector3(1.5f, 1.2f, 1.1f), new Vector3(11f, -1.5f, 0.9f), new Vector3(44.5f, 0.5f, 1.3f), new Vector3(28f, -2.2f, 0.8f) })
                    {
                        Vector2 q = CoastPoint(r.x, r.y, out n);
                        Put("Rock_Large", q.x, SandH(r.y) - 0.5f, q.y, r.x * 37f, Vector3.one * r.z);
                    }
            }
        }

        // ---------------- 植物・岩・小物（依頼23。2026-11-02 クオリティアップ） ----------------
        /// <summary>道・建物・芝生から離れているか（草むら・岩・花をばらまくとき）。</summary>
        bool FreeSpot(Vector2 p, float margin, bool lawns = true, bool walls = true)
        {
            Rect[] blocks =
            {
                Rect.MinMaxRect(-37, 43, 37, 92), Rect.MinMaxRect(43, 58, 93, 91), Rect.MinMaxRect(-79, 48, -45, 78), Rect.MinMaxRect(-91, 12, -53, 46),
                Rect.MinMaxRect(44, 10, 79, 40), Rect.MinMaxRect(-64, 95, -40, 104), Rect.MinMaxRect(-13, -2, 13, 13), Rect.MinMaxRect(-18, 29, 18, 46),
                Rect.MinMaxRect(-53, 116, -33, 132), Rect.MinMaxRect(-76, 124, -58, 138),
            };
            foreach (var b in blocks) if (p.x > b.xMin - margin && p.x < b.xMax + margin && p.y > b.yMin - margin && p.y < b.yMax + margin) return false;
            if (poolBeach && (p.x > PoolArea.xMin - margin && p.x < PoolArea.xMax + margin && p.y > PoolArea.yMin - margin && p.y < PoolArea.yMax + margin)) return false; // プール（依頼29）
            if (poolBeach && (NearBeach(p, 3f, 15f + margin) || OnLowBeach(p, 0.15f))) return false; // 浜辺（帯の先の森は除かない。2026-11-08）
            if (poolBeach && DistSeg(p, BeachDoorOut, BeachStairTop) < 1.4f + margin) return false; // 浜辺への小道
            if (poolBeach && PlanInHut(p, margin + 0.3f)) return false; // 小屋の中（床から草が生えないように。2026-11-09）
            if (lawns && (p - new Vector2(0, 18)).magnitude < 10f + margin) return false;
            if (lawns && ((p - new Vector2(26, 25)).magnitude < 5f || (p - new Vector2(-26, 25)).magnitude < 5f)) return false;
            if (!Inside(Island, p)) return false;
            // 城壁のすぐそば
            if (walls) for (int i = 0; i < WallPts.Length; i++)
            {
                Vector2 a = WallPts[i], b2 = WallPts[(i + 1) % WallPts.Length];
                if (DistSeg(p, a, b2) < 3.5f) return false;
            }
            foreach (var path in CampusPlan.Paths) for (int i = 0; i + 1 < path.pts.Length; i++) if (DistSeg(p, path.pts[i], path.pts[i + 1]) < path.w / 2f + margin) return false;
            if (v3) foreach (var path in CampusPlan.DormLinksV3) for (int i = 0; i + 1 < path.pts.Length; i++) if (DistSeg(p, path.pts[i], path.pts[i + 1]) < path.w / 2f + margin) return false;
            if (poolBeach && DistSeg(p, new Vector2(42.5f, 51f), new Vector2(PoolRect.xMin - 1.8f, 51f)) < 1.1f + margin) return false; // プールへの道
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
            if (Has("Topiary_Cone")) // 生け垣のはしに刈りこんだ円すいの植え込み（案3 の絵。依頼27）
                foreach (var tp in new[] { new Vector2(-3.5f, 13.0f), new Vector2(3.5f, 13.0f), new Vector2(-3.5f, 32.0f), new Vector2(3.5f, 32.0f) })
                    Put("Topiary_Cone", tp.x, 0f, tp.y, 0f);
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
            for (int i = 0; i < 900 && !ground1 && (placedFlowers < 70 || placedGrass < 320); i++)
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
                float x = -112f + Hash01(i, 61) * 224f, y = 4f + Hash01(i, 67) * 204f;
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
            if (v3) foreach (var p in CampusPlan.DormLinksV3) for (int i = 0; i + 1 < p.pts.Length; i++) { segs.Add(new Vector4(p.pts[i].x, p.pts[i].y, p.pts[i + 1].x, p.pts[i + 1].y)); widths.Add(p.w); }
            foreach (var wk in CampusPlan.Walkways) for (int i = 0; i + 1 < wk.Length; i++) { segs.Add(new Vector4(wk[i].x, wk[i].y, wk[i + 1].x, wk[i + 1].y)); widths.Add(3f); }
            segs.Add(new Vector4(0, 0, 0, 11)); widths.Add(4f);
            segs.Add(new Vector4(-52, 92, -72, 94)); widths.Add(2.5f); segs.Add(new Vector4(-72, 94, -87, 99)); widths.Add(2.5f);
            segs.Add(new Vector4(-87, 99, -80, 112)); widths.Add(2.2f); segs.Add(new Vector4(-80, 112, -70, 122)); widths.Add(2.2f); segs.Add(new Vector4(-80, 112, -50, 118)); widths.Add(2.2f);
            var px = new Color[W * Hh];
            var cpx = ground1 ? new Color[W * Hh] : null; // 依頼30：混ぜ具合（r 芝生・g 原っぱ・b 踏まれた土・a 森の地面）
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
                    if (ground1)
                    {
                        // 依頼30：場所ごとの模様の混ぜ具合。城壁の内＝刈った芝生（ところどころ原っぱ）、外＝原っぱ、北＝森、道のふち＝踏まれた土
                        var wp = new Vector2(wx, wy);
                        float dirtW = best < 1.0f ? Mathf.Clamp01(1f - best / 1.0f) * (0.6f + n2 * 0.3f) : 0f;
                        bool inWall = Inside(WallPts, wp);
                        float lawnFrac = inWall ? Mathf.Clamp01(0.85f - Mathf.Clamp01((n3 - 0.55f) * 3f) * 0.6f) : Mathf.Clamp01(0.15f + (n1 - 0.5f) * 0.4f);
                        if ((wp - new Vector2(0, 18)).magnitude < 10.5f || (wp - new Vector2(26, 25)).magnitude < 5.5f || (wp - new Vector2(-26, 25)).magnitude < 5.5f) lawnFrac = 1f; // 丸い芝生
                        float wF = f, wD = dirtW * (1f - f * 0.5f), rr = Mathf.Max(0f, 1f - wF - wD);
                        cpx[j * W + i] = new Color(rr * lawnFrac, rr * (1f - lawnFrac), wD, wF);
                    }
                    // 小さな花（道から離れた原っぱ。森は少なめ）。依頼30 の地面では立体の花にするので描かない
                    if (best > 2.5f && !ground1)
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
            if (ground1)
            {
                // 依頼30 の地面_v1：芝生・原っぱ・土・森の4つの模様を混ぜ具合の画像で混ぜ、大きな色むらを重ねる（LifeSim/Ground）
                var ctl = new Texture2D(W, Hh, TextureFormat.RGBA32, true, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Ground_Control" };
                ctl.SetPixels(cpx); ctl.Apply(true);
                ctrlPx = cpx; ctrlW = W; ctrlH = Hh;
                var gm = new Material(Shader.Find("LifeSim/Ground")) { name = "Ground_v1" };
                gm.SetTexture("_Control", ctl);
                string[] lay = { "Tile_Lawn", "Tile_Meadow", "Tile_Dirt", "Tile_ForestFloor" };
                for (int k = 0; k < 4; k++) { gm.SetTexture("_L" + k, T(lay[k])); var nm = T(lay[k] + "_Normal"); if (nm != null) gm.SetTexture("_N" + k, nm); }
                var mac = T("Ground_Macro"); if (mac != null) gm.SetTexture("_Macro", mac);
                gm.SetVector("_Area", new Vector4(x1 - x0, y1 - y0, 0f, 0f));
                gm.SetVector("_Tiles", new Vector4(2f, 3f, 2f, 3f));
                gm.SetFloat("_MacroStrength", Tune("Ground_macro", 0.6f));
                gm.SetFloat("_BumpScale", Tune("Ground_bump", 1f));
                gm.SetFloat("_BlendSharp", Tune("Ground_sharp", 0.5f));
                gm.SetColor("_BaseColor", new Color(Tune("Ground_r", 0.9f), Tune("Ground_g", 0.88f), Tune("Ground_b", 0.86f))); // 芝生の模様は明るくあざやかすぎたので少しおさえる
                // 草の色合わせ用の地面の色（famuru 2026-11-08「色が地面と合っていない」）：4つの模様の平均の色を混ぜ具合で混ぜ、大きな色むら（Ground_Macro）と色の補正を掛ける。草はこの色を写す
                Color[] meanC = { new Color(136.4f / 255f, 159.2f / 255f, 53.3f / 255f), new Color(129f / 255f, 153.8f / 255f, 57.2f / 255f), new Color(135.2f / 255f, 111.1f / 255f, 78.9f / 255f), new Color(110.9f / 255f, 97.2f / 255f, 58.5f / 255f) };
                Color tnt = new Color(Tune("Ground_r", 0.9f), Tune("Ground_g", 0.88f), Tune("Ground_b", 0.86f));
                bool macR = mac != null && mac.isReadable; float mS = Tune("Ground_macro", 0.6f);
                var gcol = new Color[W * Hh];
                for (int jj = 0; jj < Hh; jj++)
                    for (int ii = 0; ii < W; ii++)
                    {
                        var cw = cpx[jj * W + ii];
                        Color c = meanC[0] * cw.r + meanC[1] * cw.g + meanC[2] * cw.b + meanC[3] * cw.a;
                        if (macR)
                        {
                            float mx = (ii + 0.5f) / W * (x1 - x0), my = (jj + 0.5f) / Hh * (y1 - y0);
                            float m1 = mac.GetPixelBilinear(mx / 64f, my / 64f).r, m2 = mac.GetPixelBilinear(mx / (64f * 0.37f) + 0.31f, my / (64f * 0.37f) + 0.31f).r;
                            c *= Mathf.Pow(Mathf.Lerp(1f, 0.7f + 0.6f * (m1 * 0.7f + m2 * 0.3f), mS), 1f / 2.2f); // 地面の材質は光の計算の色（リニア）で掛けるので、画像の色に直して掛ける
                        }
                        gcol[jj * W + ii] = new Color(c.r * tnt.r, c.g * tnt.g, c.b * tnt.b, 1f);
                    }
                var gt = new Texture2D(W, Hh, TextureFormat.RGB24, true) { wrapMode = TextureWrapMode.Clamp, name = "Ground_GrassColor" };
                gt.SetPixels(gcol); gt.Apply(true);
                groundTex = gt;
                return gm;
            }
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

        // ---------------- 依頼30 の地面（地面_v1。famuru 2026-11-08「次は地面の改良をしたい」） ----------------
        // 模様は LifeSim/Ground で場所ごとに混ぜる（GroundMaterial）。草・花・道のふち・壁の根元・森の下草は部品ごとにまとめて描く（GrassField.BuildParts）。Logs\old_ground.txt で前の地面
        bool ground1;
        Color[] ctrlPx; int ctrlW, ctrlH;
        readonly List<Vector2> treePts = new List<Vector2>();
        class PartSet { public List<Vector3> pos = new List<Vector3>(); public List<float> yaw = new List<float>(), sc = new List<float>(); public List<Color> col = new List<Color>(); }

        /// <summary>その場所の混ぜ具合（x 芝生・y 原っぱ・z 土・w 森）。</summary>
        Vector4 Ctrl(Vector2 p)
        {
            if (ctrlPx == null) return new Vector4(0f, 1f, 0f, 0f);
            int i = Mathf.Clamp((int)((p.x - groundRect.xMin) / groundRect.width * ctrlW), 0, ctrlW - 1);
            int j = Mathf.Clamp((int)((p.y - groundRect.yMin) / groundRect.height * ctrlH), 0, ctrlH - 1);
            var c = ctrlPx[j * ctrlW + i];
            return new Vector4(c.r, c.g, c.b, c.a);
        }

        void AddPart(Dictionary<string, PartSet> sets, string name, float x, float y, float yaw, float scale, float bright = 1f)
        {
            if (!sets.TryGetValue(name, out var st)) sets[name] = st = new PartSet();
            st.pos.Add(root.TransformPoint(new Vector3(x, H(x, y) - 0.03f, y)));
            st.yaw.Add(yaw + root.eulerAngles.y);
            st.sc.Add(scale * root.lossyScale.x);
            st.col.Add(new Color(bright, bright, bright, 1f));
        }

        /// <summary>道のふちに草の帯（道の上へ少し垂れる）。</summary>
        void EdgeAlong(Dictionary<string, PartSet> sets, Vector2[] pts, float w)
        {
            for (int i = 0; i + 1 < pts.Length; i++)
            {
                Vector2 a = pts[i], b = pts[i + 1], d = b - a; float L = d.magnitude; if (L < 0.5f) continue; d /= L;
                Vector2 nrm = new Vector2(-d.y, d.x);
                for (float sAl = 0.5f; sAl < L - 0.3f; sAl += Tune("Edge_step", 0.95f))
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector2 e = a + d * sAl + nrm * side * (w * 0.5f);
                        Vector2 probe = e + nrm * side * 0.6f;
                        if (!FreeSpot(probe, 0f, false)) continue;
                        if (Ctrl(probe).w > 0.5f) continue;
                        Vector2 c = e + nrm * side * Tune("Edge_in", 0.12f);
                        float h = Hash01((int)(c.x * 7f), (int)(c.y * 7f));
                        AddPart(sets, h < 0.5f ? "Grass_Edge_A" : "Grass_Edge_B", c.x, c.y, YawFacing(-nrm * side), 1f + h * 0.15f, 0.95f + h * 0.1f);
                    }
            }
        }

        void GroundParts()
        {
            var fol = Shader.Find("LifeSim/Foliage");
            var card = Resources.Load<Texture2D>("Furniture/Ground1/Textures/Ground_Card");
            if (fol == null || card == null) { Debug.LogWarning("[CampusPreview] 依頼30 の草の画像がない"); return; }
            var sets = new Dictionary<string, PartSet>();
            // 1. 芝生と原っぱの草
            float stepL = Tune("Lawn_step", 0.38f), lsc = Tune("Lawn_scale", 1.1f); // 2026-11-08 famuru「束がぽつぽつ」：小さく細かく
            int ix = 0;
            for (float y = 1f; y < 210f; y += stepL, ix++)
            {
                int jx = 0;
                for (float x = -118f; x < 118f; x += stepL, jx++)
                {
                    float px = x + (Hash01(ix, jx * 3 + 1) - 0.5f) * stepL, py = y + (Hash01(jx, ix * 5 + 2) - 0.5f) * stepL;
                    var p = new Vector2(px, py);
                    if (Mathf.Abs(px) < 2.6f && py < 11.6f) continue; // 正門の中の道
                    if (!FreeSpot(p, 0.25f, false)) continue;
                    if (CurveDist(p) < 2.3f) continue; // 曲線の渡り廊下の床
                    var w = Ctrl(p);
                    if (w.w > 0.45f || w.z > 0.5f) continue; // 森・土
                    float dens = Mathf.PerlinNoise(px * 0.09f + 13f, py * 0.09f + 71f);
                    float br = 0.92f + Hash01(jx * 7, ix) * 0.16f;
                    if (w.y > w.x)
                    {
                        if (dens < 0.12f || Hash01(jx * 7, ix * 3) > Tune("Meadow_rate", 0.8f)) continue; // 原っぱはすき間なく、ふさふさに
                        AddPart(sets, Hash01(ix, jx) < 0.5f ? "Grass_Meadow_A" : "Grass_Meadow_B", px, py, Hash01(ix * 11, jx) * 360f, 0.9f + dens * 0.5f, br);
                    }
                    else
                    {
                        if (dens < 0.08f) continue;
                        AddPart(sets, Hash01(ix, jx) < 0.5f ? "Grass_Lawn_A" : "Grass_Lawn_B", px, py, Hash01(ix * 11, jx) * 360f, (0.85f + dens * 0.4f) * lsc, br);
                    }
                }
            }
            // 2. 花（まだらの群れ。種類も場所でまとまる）
            int fi = 0;
            for (float y = 2f; y < 150f; y += 1.1f, fi++)
            {
                int fj = 0;
                for (float x = -116f; x < 116f; x += 1.1f, fj++)
                {
                    float px = x + (Hash01(fi * 3, fj) - 0.5f) * 1.1f, py = y + (Hash01(fj * 3, fi) - 0.5f) * 1.1f;
                    float patch = Mathf.PerlinNoise(px * 0.07f + 200f, py * 0.07f + 40f);
                    float th = Tune("Flower_patch", 0.52f);
                    if (patch < th || Hash01(fj * 5, fi * 7) > (patch - th) * Tune("Flower_dens", 3f)) continue;
                    var p = new Vector2(px, py);
                    if (!FreeSpot(p, 0.6f, false)) continue;
                    var w = Ctrl(p); if (w.w > 0.3f || w.z > 0.3f) continue;
                    float sp = Mathf.PerlinNoise(px * 0.03f + 7f, py * 0.03f + 9f);
                    string fl = sp < 0.42f ? "Flower_Daisy" : sp < 0.58f ? "Flower_Clover" : "Flower_Buttercup";
                    if (Hash01(fi, fj * 11) < 0.2f) fl = "Flower_Daisy";
                    AddPart(sets, fl, px, py, Hash01(fj, fi * 13) * 360f, 0.9f + Hash01(fi * 5, fj) * 0.5f);
                }
            }
            // 3. 道のふちの草の帯
            foreach (var path in CampusPlan.Paths) EdgeAlong(sets, path.pts, path.w);
            if (v3) foreach (var path in CampusPlan.DormLinksV3) EdgeAlong(sets, path.pts, path.w);
            if (poolBeach) EdgeAlong(sets, new[] { new Vector2(42.5f, 51f), new Vector2(PoolRect.xMin - 1.8f, 51f) }, 2.2f);
            // 4. 城壁の根元（内と外）の草
            Vector2 cen = new Vector2(0f, 55f);
            for (int i = 0; i < WallPts.Length; i++)
            {
                if (i == 1) continue; // 正門
                Vector2 a = WallPts[i], b = WallPts[(i + 1) % WallPts.Length], d = b - a; float L = d.magnitude; d /= L;
                Vector2 nIn = new Vector2(-d.y, d.x); if (Vector2.Dot(cen - (a + b) * 0.5f, nIn) < 0f) nIn = -nIn;
                for (int side = 0; side < 2; side++)
                {
                    Vector2 nn = side == 0 ? nIn : -nIn; float off = Tune(side == 0 ? "WallWeeds_in" : "WallWeeds_out", 1.6f); // 城壁の厚み 2.5m の外の面のすぐ前
                    for (float sAl = 1f; sAl < L - 1f; sAl += side == 0 ? 1.0f : 1.6f)
                    {
                        Vector2 c = a + d * sAl + nn * off;
                        if (Mathf.Abs(c.x) < 12f && c.y < 16f) continue; // 正門のまわり
                        if (!FreeSpot(c, 0.1f, false, false)) continue;
                        float h = Hash01(i * 131 + (int)(sAl * 3f), side * 7 + 1);
                        if (h < 0.15f) continue;
                        if (h > 0.85f) AddPart(sets, "Grass_Tall_A", c.x + nn.x * 0.3f, c.y + nn.y * 0.3f, h * 360f, 0.8f + h * 0.3f);
                        else AddPart(sets, "Weeds_WallBase_A", c.x, c.y, YawFacing(nn) + (h - 0.5f) * 10f, (0.95f + h * 0.2f) * Tune("WallWeeds_scale", 1.3f));
                    }
                }
            }
            // 5. 木の根元：長い草・落ち葉（森の木はシダ）
            for (int t = 0; t < treePts.Count; t++)
            {
                var tp = treePts[t]; bool forest = Ctrl(tp).w > 0.5f;
                int n = forest ? 2 : 3;
                for (int k = 0; k < n; k++)
                {
                    float a = Hash01(t, k * 7 + 3) * 6.283f, r = 0.7f + Hash01(k, t * 5 + 1) * 0.8f;
                    Vector2 q = tp + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    if (!FreeSpot(q, 0.1f, false)) continue;
                    AddPart(sets, forest ? "Fern_B" : (k == 0 ? "Grass_Tall_A" : "Grass_Meadow_A"), q.x, q.y, a * 57f, 0.8f + Hash01(t, k) * 0.3f);
                }
                if (forest || Hash01(t, 99) < 0.5f)
                {
                    Vector2 q = tp + new Vector2(Hash01(t, 5) - 0.5f, Hash01(t, 6) - 0.5f) * 1.2f;
                    if (FreeSpot(q, 0f, false)) AddPart(sets, "LeafLitter_A", q.x, q.y, Hash01(t, 7) * 360f, 0.8f + Hash01(t, 8) * 0.5f);
                }
            }
            // 6. 森の地面：シダ・下草・落ち葉・枝・きのこ。森のふちに長い草
            int gi = 0;
            for (float y = 100f; y < 212f; y += 1.5f, gi++)
            {
                int gj = 0;
                for (float x = -120f; x < 120f; x += 1.5f, gj++)
                {
                    float px = x + (Hash01(gi * 13, gj) - 0.5f) * 1.5f, py = y + (Hash01(gj * 17, gi) - 0.5f) * 1.5f;
                    var p = new Vector2(px, py);
                    var w = Ctrl(p);
                    if (w.w < 0.4f)
                    {
                        if (w.w > 0.12f && Hash01(gj, gi * 3) < 0.3f && FreeSpot(p, 0.3f, false)) AddPart(sets, "Grass_Tall_A", px, py, Hash01(gi, gj) * 360f, 0.8f + Hash01(gj, gi) * 0.4f);
                        continue;
                    }
                    if (!FreeSpot(p, 0.3f, false)) continue;
                    float dn = Mathf.PerlinNoise(px * 0.08f + 31f, py * 0.08f + 17f); if (dn < 0.35f) continue;
                    float h = Hash01(gj * 7 + 1, gi * 11 + 5);
                    string pn = h < 0.22f ? "Fern_A" : h < 0.42f ? "Fern_B" : h < 0.55f ? "Undergrowth_A" : h < 0.78f ? "LeafLitter_A" : h < 0.83f ? "Branch_Fallen" : h < 0.9f ? "Mushroom_Cluster" : "Grass_Tall_A";
                    AddPart(sets, pn, px, py, Hash01(gi, gj * 5) * 360f, 0.75f + Hash01(gj, gi) * 0.5f);
                }
            }
            // まとめて描く
            Material bark = null;
            if (mats.TryGetValue("Bark", out var bk)) bark = new Material(bk) { name = "Ground1_Bark", enableInstancing = true };
            int total = 0; var log = new System.Text.StringBuilder();
            foreach (var kv in sets)
            {
                var pf = LoadFurn(kv.Key);
                var mf = pf != null ? pf.GetComponentInChildren<MeshFilter>() : null;
                if (mf == null || mf.sharedMesh == null) { Debug.LogWarning("[CampusPreview] 部品がない：" + kv.Key); continue; }
                Matrix4x4 bm = pf.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                var mr = mf.GetComponent<MeshRenderer>();
                var names = mr != null ? mr.sharedMaterials : new Material[0];
                string k0 = kv.Key;
                float wind = k0.StartsWith("Grass_Meadow") || k0.StartsWith("Grass_Tall") ? 0.09f : k0.StartsWith("Grass") || k0.StartsWith("Weeds") || k0.StartsWith("Flower") ? 0.05f : k0.StartsWith("Fern") || k0.StartsWith("Undergrowth") ? 0.04f : 0f;
                float dd = k0.StartsWith("Grass_Lawn") ? Tune("Lawn_dist", 36f) : k0.StartsWith("Mushroom") ? 42f : k0.StartsWith("Flower") ? 48f : k0.StartsWith("Grass_Meadow") || k0.StartsWith("Grass_Edge") ? 58f : k0.StartsWith("LeafLitter") ? 62f : k0.StartsWith("Fern") || k0.StartsWith("Undergrowth") ? 80f : 70f;
                float up = k0.StartsWith("Grass") || k0.StartsWith("Weeds") ? 0.85f : k0.StartsWith("Flower") ? 0.7f : k0.StartsWith("Fern") || k0.StartsWith("Undergrowth") ? 0.5f : 0.25f;
                bool shadow = k0.StartsWith("Fern_A") || k0.StartsWith("Undergrowth") || k0.StartsWith("Branch");
                var cm = new Material(fol) { name = "Ground1_" + k0, enableInstancing = true };
                cm.SetTexture("_BaseMap", card);
                cm.SetFloat("_Cutoff", 0.5f); cm.SetFloat("_UseVertexColor", 1f); cm.SetFloat("_UseVertexAlpha", 1f);
                cm.SetFloat("_WindStrength", wind); cm.SetFloat("_WindSpeed", 1.5f); cm.SetFloat("_WindScale", 0.12f);
                cm.SetFloat("_Wrap", 0.3f); cm.SetFloat("_UpNormal", up); cm.SetFloat("_Translucency", 0.3f); cm.SetFloat("_Cull", 0f);
                cm.SetFloat("_FadeStart", dd * 0.72f); cm.SetFloat("_FadeEnd", dd * 0.97f);
                float gb = k0.StartsWith("Grass_Edge") ? Tune("Edge_bright", 1.1f) : 1f;
                cm.SetColor("_BaseColor", new Color(gb * Tune("Card_r", 1f), gb * Tune("Card_g", 1.02f), gb * Tune("Card_b", 0.88f)));
                if ((k0.StartsWith("Grass_Lawn") || k0.StartsWith("Grass_Meadow")) && groundTex != null)
                {
                    // 芝生と原っぱの草は、下の地面の色をそのまま写す（famuru 2026-11-08「色が地面と合っていない」）。草の画像の平均の色（リニア 0.253,0.367,0.043）で割って、画像の明暗だけを残す
                    float km = k0.StartsWith("Grass_Lawn") ? Tune("Lawn_match", 1.6f) : Tune("Meadow_match", 1.4f); // 根元の暗さのぶん地面より暗く見えるので明るめに
                    cm.SetFloat("_UseGround", 1f); cm.SetTexture("_GroundTex", groundTex); cm.SetFloat("_GroundMul", Tune("Grass_gmul", 1f));
                    cm.SetMatrix("_GroundW2L", root.worldToLocalMatrix);
                    cm.SetVector("_GroundRect", new Vector4(groundRect.xMin, groundRect.yMin, 1f / groundRect.width, 1f / groundRect.height));
                    cm.SetColor("_BaseColor", Color.white); cm.SetFloat("_GroundGray", km / 0.319f); // 草の画像の平均の明るさ（リニア 0.319）で割る
                    cm.SetFloat("_Translucency", 0.1f); cm.SetFloat("_Wrap", 0f);
                }
                var sub = new Material[Mathf.Max(1, names.Length)];
                for (int k = 0; k < sub.Length; k++) sub[k] = (k < names.Length && names[k] != null && names[k].name.StartsWith("Bark") && bark != null) ? bark : cm;
                var gf = gameObject.AddComponent<GrassField>();
                gf.BuildParts(mf.sharedMesh, sub, bm, kv.Value.pos, kv.Value.yaw, kv.Value.sc, kv.Value.col, dd, shadow);
                total += kv.Value.pos.Count; log.Append(" " + k0 + "=" + kv.Value.pos.Count);
            }
            Debug.Log("[CampusPreview] 依頼30 の地面の部品 " + total + "：" + log);
        }

        // ---------------- 草（GrassField）と崖の下の岩（2026-11-02 famuru「草地・水面・崖がのっぺり」） ----------------
        void GrassFieldBuild()
        {
            if (ground1) { GroundParts(); return; }
            var sh = Shader.Find("LifeSim/Foliage");
            if (sh == null || groundTex == null) return;
            var mat = new Material(sh) { name = "GrassField" };
            mat.SetFloat("_UseVertexColor", 1f); mat.SetFloat("_UseVertexAlpha", 1f);
            mat.SetFloat("_WindStrength", 0.09f); mat.SetFloat("_WindSpeed", 1.7f); mat.SetFloat("_WindScale", 0.12f);
            mat.SetFloat("_Wrap", 0f); mat.SetFloat("_UpNormal", 1f); mat.SetFloat("_TipLight", 0f); mat.SetFloat("_Translucency", 0f);
            mat.SetFloat("_FadeStart", 50f); mat.SetFloat("_FadeEnd", 72f); mat.SetFloat("_Cull", 0f);
            mat.SetTexture("_GroundTex", groundTex); mat.SetFloat("_UseGround", 1f); mat.SetFloat("_GroundMul", 0.8f);
            mat.SetMatrix("_GroundW2L", root.worldToLocalMatrix);
            mat.SetVector("_GroundRect", new Vector4(groundRect.xMin, groundRect.yMin, 1f / groundRect.width, 1f / groundRect.height));
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
                    Color g = groundTex.GetPixelBilinear(u, v) * 0.72f; // 地面の色（地面は細かい模様と陰で少し暗く見えるので合わせる）
                    float bright = 1f + (Hash01(jx * 7, ix) - 0.5f) * 0.12f;
                    col.Add(new Color(bright, bright, bright, 0f)); // 色はシェーダーが地面の画像から取る（a=0：インスタンスの色は使わない）
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
                        if (ground1 && T("Tile_Lawn") != null)
                        {
                            // 依頼30 の刈った芝生の模様（中庭の芝生も島の芝生と同じに）
                            nm.SetTexture("_BaseMap", T("Tile_Lawn"));
                            var ln = T("Tile_Lawn_Normal");
                            if (ln != null && nm.HasProperty("_BumpMap")) { nm.SetTexture("_BumpMap", ln); nm.EnableKeyword("_NORMALMAP"); }
                            nm.SetColor("_BaseColor", new Color(0.9f, 0.88f, 0.86f) * Tune("Lawn_court", 1f));
                        }
                        else nm.SetColor("_BaseColor", new Color(target.r / avg.r, target.g / avg.g, target.b / avg.b));
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
                    if (poolBeach && (cliffs2 ? NearBeach(p, -3f, 4f) : BeachAcross(p, -3f) < 4f)) continue; // 浜辺
                    if (poolBeach && (cliffs2 ? NearBeach(p, 2f, 4f) : BeachAcross(p, 2f) < 4f) && Hash01(k, 5) < 0.6f) continue; // 浜辺のはしは岩を少なく
                    float h1 = Hash01(k, 3), h2 = Hash01(k, 7), h3 = Hash01(k, 11);
                    if (cliffs2)
                    {
                        // 依頼26 の岸の岩（原点は上の真ん中。半分沈める＝水面＋高さの半分）
                        if (h1 < Tune("Shore2_skip", 0.7f)) continue;
                        Vector2 q2 = p + n * (Tune("Shore2_out", 1.4f) + h1 * 1.4f) + d * (h2 - 0.5f) * 2f;
                        bool bigA = h3 > 0.4f; float sc2 = 0.7f + h2 * 0.6f;
                        Put(bigA ? "Rock_Shore_A" : "Rock_Shore_B", q2.x, WaterY + (bigA ? 1.25f : 0.9f) * sc2 * Tune("Shore2_up", 1.1f), q2.y, h2 * 360f, Vector3.one * sc2);
                        if (h3 > 0.7f)
                        {
                            Vector2 r2 = q2 + d * (2.2f + h1) + n * 0.6f;
                            Put("Rock_Shore_B", r2.x, WaterY + 0.5f, r2.y, h1 * 360f, Vector3.one * (0.55f + h2 * 0.3f));
                        }
                        continue;
                    }
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

        // ---- 動く扉と、消せる屋根（依頼29 追記3） ----
        class SwingDoor { public Transform leaf, holder; public Quaternion closed; public Vector3 center; public float sign = -1f, k; }
        class HutRoof { public Renderer[] rs; public Vector3 pos; public bool hidden; }
        readonly List<SwingDoor> swingDoors = new List<SwingDoor>();
        readonly Dictionary<SimCharacter, Vector3> lastPos = new Dictionary<SimCharacter, Vector3>();
        Material hutGlass;
        readonly Dictionary<Material, Material> hutTwoSided = new Dictionary<Material, Material>();
        readonly List<HutRoof> hutRoofs = new List<HutRoof>();
        float nextRoofCheck;

        readonly List<Transform> movingHolders = new List<Transform>();
        void SetupMoving(GameObject h)
        {
            if (h == null) return;
            movingHolders.Add(h.transform);
            foreach (var t in h.GetComponentsInChildren<Transform>())
            {
                if (t.name.StartsWith("Door_Leaf"))
                {
                    var r = t.GetComponent<Renderer>();
                    var d = new SwingDoor { leaf = t, holder = h.transform, closed = t.rotation, center = r != null ? r.bounds.center : t.position };
                    // 内側（入れ物の −z）へ開く向きを選ぶ
                    Vector3 v = d.center - t.position;
                    float zn = h.transform.InverseTransformDirection(Quaternion.AngleAxis(-90f, h.transform.up) * v).z, zp = h.transform.InverseTransformDirection(Quaternion.AngleAxis(90f, h.transform.up) * v).z;
                    d.sign = zn < zp ? -1f : 1f;
                    swingDoors.Add(d);
                }
                else if (t.name == "Roof") hutRoofs.Add(new HutRoof { rs = t.GetComponentsInChildren<Renderer>(), pos = h.transform.position });
            }
        }

        void UpdateMoving()
        {
            var w = LoungeWorld.Instance;
            if (swingDoors.Count > 0 && w != null)
                foreach (var d in swingDoors)
                {
                    if (d.leaf == null) continue;
                    bool want = false;
                    foreach (var c in w.characters)
                    {
                        if (c == null) continue;
                        Vector3 q = c.transform.position - d.center;
                        float dd = q.x * q.x + q.z * q.z;
                        if (Mathf.Abs(q.y) >= 2.5f || dd >= 1.7f * 1.7f) continue;
                        // 歩いている人か、戸口に立っている人だけ（そばのベンチや長いすに座っている人では開けない。2026-11-09）
                        Vector3 lp; bool moving = lastPos.TryGetValue(c, out lp) && (c.transform.position - lp).sqrMagnitude > 0.003f * 0.003f;
                        if (moving || dd < 0.8f * 0.8f) { want = true; break; }
                    }
                    float k = Mathf.MoveTowards(d.k, want ? 1f : 0f, Mathf.Min(0.2f, Time.deltaTime) * 2.5f);
                    if (k == d.k) continue;
                    d.k = k;
                    d.leaf.rotation = Quaternion.AngleAxis(d.sign * 90f * Mathf.SmoothStep(0f, 1f, k), d.holder.up) * d.closed;
                }
            if (w != null && swingDoors.Count > 0) foreach (var c in w.characters) if (c != null) lastPos[c] = c.transform.position;
            if (hutRoofs.Count > 0 && Time.unscaledTime >= nextRoofCheck)
            {
                nextRoofCheck = Time.unscaledTime + 0.25f;
                var cam = Camera.main;
                foreach (var r in hutRoofs)
                {
                    bool hide = cam != null && (cam.transform.position - r.pos).sqrMagnitude < 26f * 26f; // カメラが近いと屋根を消す（影は残す）
                    if (hide == r.hidden) continue;
                    r.hidden = hide;
                    foreach (var rr in r.rs) if (rr != null) rr.shadowCastingMode = hide ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
                }
            }
        }

        // 試験用：部品の子の名前・位置・大きさ（入れ物の座標）を Logs\model_dump.txt に足す
        void DumpModel(GameObject h)
        {
            if (h == null) return;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("== " + h.name + " pos(plan)=" + h.transform.localPosition + " yaw=" + h.transform.localEulerAngles.y);
            foreach (var t in h.GetComponentsInChildren<Transform>())
            {
                var lp = h.transform.InverseTransformPoint(t.position);
                string line = t.name + " p=" + lp.ToString("F3") + " fwd=" + h.transform.InverseTransformDirection(t.forward).ToString("F2") + " up=" + h.transform.InverseTransformDirection(t.up).ToString("F2");
                var r = t.GetComponent<Renderer>();
                if (r != null)
                {
                    var b = r.bounds; var a = h.transform.InverseTransformPoint(b.min); var c = h.transform.InverseTransformPoint(b.max);
                    line += " box=" + Vector3.Min(a, c).ToString("F2") + ".." + Vector3.Max(a, c).ToString("F2") + " mats=" + string.Join(",", System.Array.ConvertAll(r.sharedMaterials, m => m != null ? m.name : "-"));
                }
                sb.AppendLine(line);
            }
            // 高さ 0.9〜1.3m の面の頂点・三角形の中心を 0.2m のます目に（壁の平面図）
            var occ = new HashSet<Vector2Int>();
            foreach (var mf in h.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.name.StartsWith("Roof") || mf.name.StartsWith("Door")) continue;
                var mesh = mf.sharedMesh; if (mesh == null) continue;
                var vs = mesh.vertices; var tr = mesh.triangles;
                for (int i = 0; i < tr.Length; i += 3)
                {
                    Vector3 v0 = h.transform.InverseTransformPoint(mf.transform.TransformPoint(vs[tr[i]])), v1 = h.transform.InverseTransformPoint(mf.transform.TransformPoint(vs[tr[i + 1]])), v2 = h.transform.InverseTransformPoint(mf.transform.TransformPoint(vs[tr[i + 2]]));
                    for (float u = 0f; u <= 1f; u += 0.1f) for (float w = 0f; u + w <= 1f; w += 0.1f)
                    {
                        Vector3 q = v0 + (v1 - v0) * u + (v2 - v0) * w;
                        if (q.y > 0.9f && q.y < 1.3f) occ.Add(new Vector2Int(Mathf.RoundToInt(q.x / 0.2f), Mathf.RoundToInt(q.z / 0.2f)));
                    }
                }
            }
            if (occ.Count > 0)
            {
                int mnx = int.MaxValue, mxx = int.MinValue, mnz = int.MaxValue, mxz = int.MinValue;
                foreach (var c in occ) { mnx = Mathf.Min(mnx, c.x); mxx = Mathf.Max(mxx, c.x); mnz = Mathf.Min(mnz, c.y); mxz = Mathf.Max(mxz, c.y); }
                sb.AppendLine("plan x " + (mnx * 0.2f) + ".." + (mxx * 0.2f) + "  z " + (mnz * 0.2f) + ".." + (mxz * 0.2f) + "（上の行が +z＝正面）");
                for (int z = mxz; z >= mnz; z--)
                {
                    var row = new System.Text.StringBuilder();
                    for (int x = mnx; x <= mxx; x++) row.Append(occ.Contains(new Vector2Int(x, z)) ? '#' : '.');
                    sb.AppendLine(row.ToString() + "  z=" + (z * 0.2f).ToString("F1"));
                }
            }
            try { File.AppendAllText(Path.Combine(LogsDir, "model_dump.txt"), sb.ToString()); } catch { }
        }

        // ---------------- 写真 ----------------
        Camera shotCam;
        float nextPoll;

        void Update()
        {
            UpdateMoving();
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
                if (a.Length >= 2 && a[0] == "view" && LoungeWorld.Instance != null)
                {
                    // 試験用：view all＝寮と学校の全部の階と壁を見せる（外から外壁を撮るとき）／view auto＝ふだんに戻す
                    var w = LoungeWorld.Instance;
                    w.wallMode = a[1] == "all" ? WallMode.All : WallMode.Auto;
                    var rig = w.mainCamera != null ? w.mainCamera.GetComponent<CameraRig>() : null;
                    if (rig != null && a[1] == "all") { rig.follow = null; rig.pivot = new Vector3(rig.pivot.x, 12f, rig.pivot.z); }
                    result.Add("view: " + a[1]);
                    continue;
                }
                if (a.Length >= 5 && a[0] == "sitat" && LoungeWorld.Instance != null)
                {
                    // 試験用：sitat 番号 x z 席の名前 … その子を (x, z) に立たせてから、そこにいちばん近い空いた席（名前）へ歩いて座らせる
                    var w = LoungeWorld.Instance; var ci = System.Globalization.CultureInfo.InvariantCulture;
                    var c = w.characters[int.Parse(a[1])];
                    var at = new Vector3(float.Parse(a[2], ci), 0f, float.Parse(a[3], ci));
                    string label = string.Join(" ", a, 4, a.Length - 4);
                    Seat best = null; float bd = float.MaxValue;
                    foreach (var st in w.seats) if (st.label == label && st.occupant == null) { float dd = (st.position - at).sqrMagnitude; if (dd < bd) { bd = dd; best = st; } }
                    bool ok = false;
                    if (best != null)
                    {
                        c.transform.position = at;
                        var t = typeof(SimCharacter); var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                        var actT = t.GetNestedType("Act", System.Reflection.BindingFlags.NonPublic);
                        ok = (bool)t.GetMethod("StartSitAt", bf).Invoke(c, new object[] { best, System.Enum.Parse(actT, "Rest"), null });
                    }
                    result.Add("sitat " + c.displayName + " " + label + " " + (best != null ? best.position.ToString("F1") : "なし") + " " + ok);
                    continue;
                }
                if (a.Length >= 2 && a[0] == "who" && LoungeWorld.Instance != null)
                {
                    // 試験用：who 番号 … その子の位置・していること
                    for (int i = 1; i < a.Length; i++)
                    {
                        var c = LoungeWorld.Instance.characters[int.Parse(a[i])];
                        result.Add(i + " " + c.displayName + " " + c.transform.position.ToString("F1") + " " + c.actionText + " / " + c.StateName() + " / " + c.DebugWhere());
                    }
                    continue;
                }
                if (a.Length >= 1 && a[0] == "huts")
                {
                    foreach (var h in movingHolders)
                        foreach (var r in h.GetComponentsInChildren<Renderer>(true))
                            result.Add(h.name + "/" + r.name + " on=" + r.enabled + "/" + r.gameObject.activeInHierarchy + " mode=" + r.shadowCastingMode + " mat=" + (r.sharedMaterial != null ? r.sharedMaterial.name + "/" + r.sharedMaterial.shader.name : "-") + " b=" + r.bounds.min.ToString("F2") + ".." + r.bounds.max.ToString("F2"));
                    continue;
                }
                if (a.Length >= 1 && a[0] == "seatmarks" && LoungeWorld.Instance != null)
                {
                    // 試験用：小屋の部品の座る目印（Seat_*）の位置と、LoungeWorld の席の位置
                    foreach (var h in movingHolders)
                        foreach (var t in h.GetComponentsInChildren<Transform>())
                            if (t.name.StartsWith("Seat")) result.Add("mark " + h.name + "/" + t.name + " " + t.position.ToString("F2") + " fwd(game)=" + t.up.ToString("F2") + "," + (-t.forward).ToString("F2"));
                    foreach (var st in LoungeWorld.Instance.seats) if (st.label.Contains("小屋")) result.Add("seat " + st.label + " " + st.position.ToString("F2") + " fwd=" + st.forward.ToString("F2") + " app=" + st.approach.ToString("F2"));
                    continue;
                }
                if (a.Length >= 1 && a[0] == "doors" && LoungeWorld.Instance != null)
                {
                    // 試験用：動く扉の開き具合と、いちばん近い人
                    foreach (var d in swingDoors)
                    {
                        string near = "-"; float best = 999f;
                        foreach (var c in LoungeWorld.Instance.characters) { if (c == null) continue; float dd = Vector3.Distance(c.transform.position, d.center); if (dd < best) { best = dd; near = c.displayName + " " + c.transform.position.ToString("F1"); } }
                        result.Add("door " + d.holder.name + "/" + d.leaf.name + " k=" + d.k.ToString("F2") + " sign=" + d.sign + " center=" + d.center.ToString("F1") + " near=" + near + " " + best.ToString("F1"));
                    }
                    continue;
                }
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
        /// <summary>依頼28 の寮（Tripo 版）の玄関の階段の前（上からの写真で測った位置。2026-11-07 家具のチャット：famuru「見た目と動きに違いがある所を探して」→ 学生が芝生の上の廊下のはしで出入りしていたのを、本当の玄関に）。</summary>
        static readonly Vector2[] MobDoorsV3 = { new Vector2(-71.2f, 18.9f), new Vector2(-66.7f, 49.0f), new Vector2(65.1f, 28.8f) };
        public static Vector3 MobDoor(int d) { var p = (V3 ? MobDoorsV3 : MobDoors)[Mathf.Clamp(d, 0, 2)]; return G(p.x, p.y); }
        /// <summary>依頼28 の建物を使うか（Resources に建物_v3 があり、Logs\old_buildings.txt が無い）。</summary>
        public static bool V3
        {
            get
            {
                if (v3c < 0) v3c = (Resources.Load<GameObject>("Furniture/Buildings3/Furn_Dorm_Knight2") != null && !System.IO.File.Exists(System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Logs/old_buildings.txt")))) ? 1 : 0;
                return v3c == 1;
            }
        }
        static int v3c = -1;
        /// <summary>依頼28 の寮の玄関・地上の廊下のはしへの道（見た目と歩ける所の両方）。</summary>
        public static readonly PathDef[] DormLinksV3 =
        {
            new PathDef(2.5f, new Vector2(61.5f, 40.5f), new Vector2(65.1f, 37f), new Vector2(65.1f, 28.0f)),          // 魔術師団の寮の玄関
            new PathDef(3f, new Vector2(-62f, 47f), new Vector2(-66.7f, 47.2f), new Vector2(-66.7f, 49.9f)),            // 騎士団の寮の玄関
            new PathDef(2.2f, new Vector2(-54.5f, 31f), new Vector2(-65f, 24.5f), new Vector2(-71.2f, 19.0f)),          // 地上の廊下のはし→冒険者の寮の玄関
            new PathDef(2.2f, new Vector2(45f, 30f), new Vector2(47.5f, 35.5f), new Vector2(49.5f, 40.2f)),            // 地上の廊下のはし→魔術師団の寮への道
            new PathDef(2.2f, new Vector2(-46f, 71f), new Vector2(-46f, 47f)),                                          // 地上の廊下のはし→騎士団の寮の前の道
        };

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
            if (V3) foreach (var p in DormLinksV3) OpenLine(n, p.w, p.pts);
            foreach (var w in Walkways) OpenLine(n, 3f, w);
            // 中庭の南（中庭と曲線の渡り廊下のあいだ）
            OpenPlanRect(n, -16f, 31.5f, 16f, 44.6f);
            // 正門の内側
            if (V3)
            {
                // 依頼28 の正門（19×17.4m。中心 0,5）の中は歩けないように。先生はそのすぐ内側（y 14.8）から出入りする（2026-11-07 家具のチャット：見た目と動きのずれ）
                OpenPlanRect(n, -4f, 13.9f, 4f, 16f);
                Vector3 a = G(-9.7f, -4f), b = G(9.7f, 13.8f);
                n.Block(Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.z, b.z), Mathf.Max(a.x, b.x), Mathf.Max(a.z, b.z)));
            }
            else OpenPlanRect(n, -4f, 9f, 4f, 13f);
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
