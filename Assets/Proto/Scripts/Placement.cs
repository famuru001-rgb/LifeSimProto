using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 設置の仕組み 段1（2026-11-15 famuru「作っていい」。案：プロジェクトの claude/設置の仕組み_案.md）。
    /// ゲーム中に家具を置く・動かす・回す・しまう。famuru の決定：置いた物は周（ループ）をまたいで残す／置ける数は部屋ごとに違う／購買部のほかでも手に入る。
    /// ・品物の表：Resources/Text/設置_品物.csv（名前・種類・置き方・形・値段・手に入れ方・周をまたぐ・説明）。形は Resources/Furniture の名前
    /// ・場所の表：Resources/Text/設置_場所.csv（区画の名前・家具の上限・道具の上限・置ける時）。置ける時が「いつでも」の行だけ今は使う（段1は篤の部屋）
    /// ・持っている数（Owned）と置いた物（Placed）は static（場面を作り直しても・周が変わっても残る）。セーブにも入る（SaveSystem.cs）
    /// ・置いたら：歩く道をふさぐ（低い敷物はふさがない）・視界の制限の線が当たる・見る階の分け方に入る
    /// 手に入れ方：購買部（SimHud の「置く家具」の棚）。ほかは Give（ダンジョン・行事・作る からあとで呼ぶ。命令 place give）
    /// </summary>
    public static partial class PlaceBook
    {
        public class Def
        {
            public string name, kind, how, model, sources, desc;
            public int price;
            public bool keepLoop = true;
            public string effectText = "", likesText = "";
            public Effect effect = new Effect();
            public string[] likes = new string[0];
            public bool Liked(CharProfile p) { if (p == null) return false; foreach (var t in likes) if (p.Likes(t)) return true; return false; }
            /// <summary>画面に出す説明（効きめつき）。</summary>
            public string FullDesc
            {
                get
                {
                    string s = desc;
                    if (!string.IsNullOrEmpty(effectText)) s += (s.Length > 0 ? "　" : "") + "【" + effectText + "】";
                    if (!string.IsNullOrEmpty(effect.later)) s += "（使う動きはこれから）";
                    if (likes.Length > 0) s += "　♪" + string.Join("・", likes);
                    return s;
                }
            }
            public bool InShop { get { return sources.Contains("購買部"); } }
            public bool IsTool { get { return kind == "道具"; } }
        }

        public class Spot
        {
            public string area, when;
            public int furnMax, toolMax;
            public bool Open { get { return when == "いつでも"; } }
        }

        [System.Serializable]
        public class PlacedItem
        {
            public string name, area;
            public float x, y, z;
            public int yaw;
            public int loop;   // 置いた周
        }

        public static readonly Dictionary<string, int> Owned = new Dictionary<string, int>();
        public static readonly List<PlacedItem> Placed = new List<PlacedItem>();
        static List<Def> defs;
        static List<Spot> spots;
        public static readonly List<string> Errors = new List<string>();

        public static List<Def> All { get { if (defs == null) Load(); return defs; } }
        public static List<Spot> Spots { get { if (spots == null) Load(); return spots; } }

        public static Def Get(string name) { foreach (var d in All) if (d.name == name) return d; return null; }
        public static Spot SpotOf(string area) { foreach (var s in Spots) if (s.area == area) return s; return null; }
        public static int Count(string name) { int n; return Owned.TryGetValue(name, out n) ? n : 0; }
        public static int CountIn(string area, bool tool)
        {
            int n = 0;
            foreach (var p in Placed) { var d = Get(p.name); if (p.area == area && d != null && d.IsTool == tool) n++; }
            return n;
        }

        /// <summary>手に入れる（購買部・ダンジョン・行事・作る など）。</summary>
        public static void Give(string name, int n = 1) { if (Get(name) == null || n <= 0) return; Owned[name] = Count(name) + n; }
        public static bool Take(string name) { if (Count(name) <= 0) return false; Owned[name] = Count(name) - 1; return true; }

        public static void Reload() { defs = null; spots = null; }

        static void Load()
        {
            Errors.Clear();
            defs = new List<Def>();
            spots = new List<Spot>();
            foreach (var row in ReadTable("設置_品物"))
            {
                if (row.Length < 5 || string.IsNullOrEmpty(row[0]) || row[0] == "名前" || row[0].StartsWith("#")) continue;
                var d = new Def
                {
                    name = row[0].Trim(),
                    kind = Col(row, 1, "家具"),
                    how = Col(row, 2, "床"),
                    model = Col(row, 3, ""),
                    sources = Col(row, 5, "購買部"),
                    desc = Col(row, 7, ""),
                };
                int.TryParse(Col(row, 4, "0"), out d.price);
                d.keepLoop = Col(row, 6, "残す") != "残さない";
                d.effectText = Col(row, 8, "");
                d.likesText = Col(row, 9, "");
                d.likes = d.likesText.Length > 0 ? d.likesText.Split('・', '／', '/') : new string[0];
                d.effect = ParseEffect(d.name, d.effectText, Errors);
                if (Resources.Load<GameObject>("Furniture/" + d.model) == null) Errors.Add(d.name + "：形「" + d.model + "」が Resources/Furniture に無い");
                defs.Add(d);
            }
            foreach (var row in ReadTable("設置_場所"))
            {
                if (row.Length < 2 || string.IsNullOrEmpty(row[0]) || row[0] == "区画の名前" || row[0].StartsWith("#")) continue;
                var s = new Spot { area = row[0].Trim(), when = Col(row, 3, "いつでも") };
                int.TryParse(Col(row, 1, "0"), out s.furnMax);
                int.TryParse(Col(row, 2, "0"), out s.toolMax);
                spots.Add(s);
            }
        }

        static string Col(string[] r, int i, string def) { return i < r.Length && !string.IsNullOrEmpty(r[i].Trim()) ? r[i].Trim() : def; }

        static List<string[]> ReadTable(string name)
        {
            byte[] bytes = null;
#if UNITY_EDITOR
            string path = Path.Combine(Application.dataPath, "Proto/Resources/Text/" + name + ".csv");
            if (File.Exists(path)) { try { bytes = File.ReadAllBytes(path); } catch (System.Exception e) { Errors.Add(name + ".csv：読めない（" + e.Message + "）"); } }
#endif
            if (bytes == null) { var ta = Resources.Load<TextAsset>("Text/" + name); if (ta != null) bytes = ta.bytes; }
            if (bytes == null) { Errors.Add(name + ".csv が無い"); return new List<string[]>(); }
            int start = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
            string text;
            try { text = new UTF8Encoding(false, true).GetString(bytes, start, bytes.Length - start); }
            catch { try { text = Encoding.GetEncoding(932).GetString(bytes); } catch { text = Encoding.UTF8.GetString(bytes, start, bytes.Length - start); } }
            return LifeBook.ParseCsv(text);
        }
    }

    /// <summary>置いた物の目じるし（クリックで選ぶ・しまうときに使う）。</summary>
    public class PlacedMarker : MonoBehaviour
    {
        public PlaceBook.PlacedItem item;
        public List<int> navCells;
        public int navFloor;
    }

    public partial class LoungeWorld
    {
        readonly List<PlacedMarker> placedObjects = new List<PlacedMarker>();

        /// <summary>寮を作ったあと（Start）：置いた物を出し直す。</summary>
        void RebuildPlaced()
        {
            placedObjects.Clear();
            foreach (var p in PlaceBook.Placed) SpawnPlaced(p);
        }

        /// <summary>置いた物の大きさ（向き0のとき、原点からの範囲 x0,z0,x1,z1 と高さ）。</summary>
        readonly Dictionary<string, Vector4> footCache = new Dictionary<string, Vector4>();
        readonly Dictionary<string, float> footHeight = new Dictionary<string, float>();

        public bool PlaceFootprint(string model, out Rect local, out float height)
        {
            local = new Rect(); height = 0f;
            Vector4 c4;
            if (footCache.TryGetValue(model, out c4)) { local = Rect.MinMaxRect(c4.x, c4.y, c4.z, c4.w); height = footHeight[model]; return true; }
            var pf = Resources.Load<GameObject>("Furniture/" + model);
            if (pf == null) return false;
            var tmp = Instantiate(pf);
            tmp.transform.position = Vector3.zero;
            tmp.transform.rotation = pf.transform.localRotation;
            bool any = false; Bounds b = new Bounds();
            foreach (var r in tmp.GetComponentsInChildren<Renderer>()) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            DestroyImmediate(tmp);
            if (!any) return false;
            local = Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z);
            height = b.max.y;
            footCache[model] = new Vector4(local.xMin, local.yMin, local.xMax, local.yMax);
            footHeight[model] = height;
            return true;
        }

        /// <summary>向き（90度ごと）を入れた、置いた所の床の範囲。</summary>
        public static Rect TurnedFootprint(Rect local, int yaw, Vector3 pos)
        {
            yaw = ((yaw % 360) + 360) % 360;
            var q = Quaternion.Euler(0f, yaw, 0f);
            Vector3[] cs = { new Vector3(local.xMin, 0, local.yMin), new Vector3(local.xMax, 0, local.yMin), new Vector3(local.xMin, 0, local.yMax), new Vector3(local.xMax, 0, local.yMax) };
            float x0 = float.MaxValue, z0 = float.MaxValue, x1 = float.MinValue, z1 = float.MinValue;
            foreach (var c in cs) { var w = q * c + pos; x0 = Mathf.Min(x0, w.x); z0 = Mathf.Min(z0, w.z); x1 = Mathf.Max(x1, w.x); z1 = Mathf.Max(z1, w.z); }
            return Rect.MinMaxRect(x0, z0, x1, z1);
        }

        /// <summary>ここに置けるか。置けなければ理由（篤の言葉でなく、短い説明）。</summary>
        public string CheckPlace(PlaceBook.Def d, Vector3 pos, int yaw, out AreaInfo area)
        {
            area = AreaAt(pos + Vector3.up * 0.3f);
            if (area == null) return "ここには置けない";
            var spot = PlaceBook.SpotOf(area.name);
            if (spot == null || !spot.Open) return area.name + "には置けない（今は篤の部屋だけ）";
            int max = d.IsTool ? spot.toolMax : spot.furnMax;
            if (PlaceBook.CountIn(area.name, d.IsTool) >= max) return area.name + "はもういっぱい（" + (d.IsTool ? "道具" : "家具") + " " + max + "こまで）";
            Rect local; float h;
            if (!PlaceFootprint(d.model, out local, out h)) return "形が無い（" + d.model + "）";
            Rect fp = TurnedFootprint(local, yaw, pos);
            Rect ar = area.rect;
            if (fp.xMin < ar.xMin - 0.05f || fp.xMax > ar.xMax + 0.05f || fp.yMin < ar.yMin - 0.05f || fp.yMax > ar.yMax + 0.05f) return "部屋からはみ出る";
            // 出入口の前はあける
            EnsureFogDoors();
            List<FogDoor> ds;
            if (fogDoorsOf.TryGetValue(area, out ds))
                foreach (var dd in ds)
                {
                    float dx = Mathf.Max(0f, Mathf.Max(fp.xMin - dd.p.x, dd.p.x - fp.xMax));
                    float dz = Mathf.Max(0f, Mathf.Max(fp.yMin - dd.p.z, dd.p.z - fp.yMax));
                    if (dx * dx + dz * dz < 0.8f * 0.8f) return "出入口の前は通り道";
                }
            // ほかの物・壁と重ならない（見通しの当たりを使う。低い敷物は重なってよい）
            if (h > 0.12f)
            {
                EnsureSightColliders();
                Vector3 c = new Vector3(fp.center.x, FloorY(area.floor) + Mathf.Min(h, 2.2f) * 0.5f + 0.05f, fp.center.y);
                Vector3 half = new Vector3(Mathf.Max(0.02f, fp.width * 0.5f - 0.04f), Mathf.Max(0.02f, Mathf.Min(h, 2.2f) * 0.5f - 0.05f), Mathf.Max(0.02f, fp.height * 0.5f - 0.04f));
                foreach (var col in Physics.OverlapBox(c, half, Quaternion.identity, 1 << SightLayer, QueryTriggerInteraction.Ignore))
                {
                    if (col == null || col.gameObject.name == "FogWindow") continue;
                    if (col.bounds.size.y < 0.12f) continue;
                    var pm = col.GetComponentInParent<PlacedMarker>();
                    return pm != null ? "「" + pm.item.name + "」と重なる" : "壁や家具と重なる";
                }
            }
            return null;
        }

        /// <summary>置く（持ち物から1つ減らす）。置けなければ理由を返す。</summary>
        public string Place(string name, Vector3 pos, int yaw)
        {
            var d = PlaceBook.Get(name);
            if (d == null) return "そんな品物は無い";
            if (PlaceBook.Count(name) <= 0) return "持っていない";
            AreaInfo area;
            string why = CheckPlace(d, pos, yaw, out area);
            if (why != null) return why;
            PlaceBook.Take(name);
            var p = new PlaceBook.PlacedItem { name = name, area = area.name, x = pos.x, y = FloorY(area.floor), z = pos.z, yaw = ((yaw % 360) + 360) % 360, loop = Loop.Count };
            PlaceBook.Placed.Add(p);
            SpawnPlaced(p);
            Log("「" + name + "」を" + area.name + "に置いた");
            return null;
        }

        /// <summary>しまう（持ち物に戻す）。</summary>
        public void Unplace(PlaceBook.PlacedItem p, bool toOwned = true)
        {
            PlaceBook.Placed.Remove(p);
            for (int i = placedObjects.Count - 1; i >= 0; i--)
            {
                var m = placedObjects[i];
                if (m == null || m.item != p) continue;
                if (m.navCells != null) Nav(m.navFloor).UnblockCells(m.navCells);
                var rs = m.GetComponentsInChildren<Renderer>(true);
                if (levelRenderers != null) foreach (var lst in levelRenderers) foreach (var r in rs) lst.Remove(r);
                Destroy(m.gameObject);
                placedObjects.RemoveAt(i);
            }
            if (toOwned) PlaceBook.Give(p.name);
        }

        public List<PlacedMarker> PlacedObjects { get { return placedObjects; } }

        public AreaInfo AreaNamed(string n) { foreach (var a in areaList) if (a.name == n) return a; return null; }

        void SpawnPlaced(PlaceBook.PlacedItem p)
        {
            var d = PlaceBook.Get(p.name);
            if (d == null) return;
            var go = PlaceFurn(d.model, null, root.InverseTransformPoint(new Vector3(p.x, p.y, p.z)), p.yaw);
            if (go == null) return;
            go.name = "Placed_" + d.model;
            var m = go.AddComponent<PlacedMarker>();
            m.item = p;
            Rect local; float h;
            if (!PlaceFootprint(d.model, out local, out h)) h = 1f;
            Rect fp = TurnedFootprint(local, p.yaw, new Vector3(p.x, p.y, p.z));
            // クリックで選ぶための当たり（Ignore Raycast の層：ふつうのクリックには出ない）
            var pick = new GameObject("PlacedPick");
            pick.layer = SightLayer;
            pick.transform.SetParent(go.transform, false);
            float hh = Mathf.Max(0.1f, h);
            pick.transform.position = new Vector3(fp.center.x, p.y + hh * 0.5f, fp.center.y);
            pick.transform.rotation = Quaternion.identity;
            var bc = pick.AddComponent<BoxCollider>();
            float s = Mathf.Max(1e-3f, Mathf.Abs(go.transform.lossyScale.x));
            bc.size = new Vector3(fp.width, hh, fp.height) / s;
            // 歩く道：低い敷物（0.12m より低い）はふさがない
            int floor = FloorOf(p.y + 0.3f);
            if (h > 0.12f)
            {
                var r = Rect.MinMaxRect(fp.xMin - 0.1f, fp.yMin - 0.1f, fp.xMax + 0.1f, fp.yMax + 0.1f);
                m.navCells = Nav(floor).BlockCellsRecord(r);
                m.navFloor = floor;
            }
            // 見る階の分け方・視界の制限の線
            if (levelRenderers != null)
                foreach (var rr in go.GetComponentsInChildren<Renderer>(true))
                {
                    int lv = LevelOf(rr);
                    levelRenderers[lv].Add(rr);
                    rr.enabled = ViewFloor < 0 || lv <= ViewFloor;
                }
            AddSightColliders(go);
            placedObjects.Add(m);
        }
    }
}
