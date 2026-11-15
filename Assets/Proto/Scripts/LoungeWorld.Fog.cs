using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LifeSimProto
{
    /// <summary>
    /// 視界の制限（フォグ）段A：部屋ごと（2026-11-15 famuru「視界は上からの指示型のまま進める」。依頼：連携\依頼\本体_視界の制限_上からの指示型のまま.md）。
    /// 上から見るカメラと右クリックの指示はそのまま。篤から見える所だけ明るく、見えない所は色を抜いた灰色にする（famuru 決定 11/15：灰色・形は見える）。
    /// ・建物の中：篤のいる区画と、出入口でつながっていて出入口が篤から FogDoorSee m 以内の、となりの区画だけ明るい
    /// ・外（区画が無い所・中庭や浜辺などの外の区画）：篤のまわり FogOutdoorRadius m。建物の中の区画は暗い（すぐそばの出入口でつながる部屋だけ明るい）
    /// ・夜：上のうち、篤の前の扇形（左右 FogNightHalfAngle 度・FogNightRadius m）だけ。向きは、話しているときは相手、ほかは篤の向き
    /// ・見えない所にいるキャラは隠す（FogShadowMode なら黒い影だけ）。吹き出し・名札も出さない
    /// ・最初は切れている（famuru 決定 11/15）。歯車の欄のボタン、または命令 fog on／off で入り切り。再生しなおすと切れる
    /// 見た目は Resources/Shaders/LifeSimFog.shader（画面の深さから場所を出して、灰色にする）。
    ///
    /// 段B：見通し（2026-11-15 famuru「bに進んで」。決定：壁と背の高い家具がさえぎる・部屋の区切りは使わず見通しに置きかえる・見えない子は隠す）。
    /// ・篤の目の高さ（足もと＋FogEyeH）から、まわり 360度に FogRays 本の線を出し、壁や背の高い家具に当たるまでの長さを覚える（毎コマ）
    /// ・その長さより近い所だけ明るい（建物の中は FogSightIndoor m・外は FogOutdoorRadius m まで）。夜はさらに前の扇形だけ
    /// ・線が当たる物：寮・学園・家具の見える物すべてに、見えない当たり（BoxCollider、Ignore Raycast の層＝ほかの当たり判定には出ない）を最初に1回つける。
    ///   目の高さより低い物（机・いす・ベッド）は線が上を通るので、さえぎらない。木（葉の材質）・地面・空・大きなかたまりはつけない
    /// ・FogSightMode = false で段A（部屋ごと）に戻る（命令 fog rooms／fog sight）
    /// </summary>
    public partial class LoungeWorld
    {
        public static bool FogEnabled = false;
        public static bool FogShadowMode = false;      // 見えない所の子：false＝隠す、true＝黒い影だけ
        public static int FogNightOverride = 0;        // 試験用：0＝時刻で、1＝いつも夜、-1＝いつも昼
        public static float FogOutdoorRadius = 25f;
        public static float FogNightRadius = 12f, FogNightHalfAngle = 60f;
        public static float FogNear = 1.2f;            // 篤のまわりのいつも見える半径（夜）
        public static float FogDoorSee = 10f;          // 出入口がこれより近ければ、となりの区画も見える
        public static float FogKeep = 0.22f;           // 見えない所に残す元の色の割合
        public static float FogDarkness = 0.62f;       // 見えない所の明るさ
        public static bool FogSightMode = true;        // 段B：見通し（false で段A：部屋ごと）
        public static float FogSightIndoor = 20f;      // 建物の中で見える遠さ（m）
        public static float FogEyeH = 1.5f;            // 線を出す高さ（篤の足もとから）
        public const int FogRays = 720;
        const int SightLayer = 2;                      // Ignore Raycast（クリックなどの当たり判定に出ない）

        static readonly HashSet<string> FogOutdoorNames = new HashSet<string>
        {
            "外", "前庭", "外廊下", "本館の中庭", "物干し場", "屋上", "中庭", "プール", "浜辺", "浜のはしの岩かげ", "森",
        };

        public static bool IsOutdoorArea(AreaInfo a) { return a == null || FogOutdoorNames.Contains(a.name); }

        class FogDoor { public AreaInfo a, b; public Vector3 p; public AreaInfo Other(AreaInfo x) { return x == a ? b : a; } }
        Dictionary<AreaInfo, List<FogDoor>> fogDoorsOf;
        int fogDoorsAreaCount = -1;

        const int FogMax = 48;
        readonly List<Vector4> fogLit = new List<Vector4>(), fogLitY = new List<Vector4>(), fogDark = new List<Vector4>(), fogDarkY = new List<Vector4>();
        readonly HashSet<AreaInfo> fogLitAreas = new HashSet<AreaInfo>();
        Vector4 fogCircle, fogCone, fogConeDir, fogSight, fogSightY;
        readonly float[] fogHits = new float[FogRays];
        Texture2D fogSightTex;
        bool fogSightBuilt;
        public int FogSightColliders;
        Material fogMat; Mesh fogMesh;
        bool fogWasOn;
        public string FogStatus = "";

        /// <summary>毎コマ（Update）。</summary>
        void UpdateFog()
        {
            var f = RomanceTarget;
            bool on = FogEnabled && mainCamera != null && f != null && f.Present && !f.InDungeon;
            if (!on)
            {
                if (fogWasOn) { foreach (var c in characters) if (c != null) c.SetFogHidden(false, false); fogWasOn = false; FogStatus = "切"; }
                return;
            }
            fogWasOn = true;
            BuildFogRegions(f);
            DrawFog();
            Vector3 up = Vector3.up * 0.9f;
            foreach (var c in characters)
                if (c != null) c.SetFogHidden(c != f && !FogSees(c.transform.position + up), FogShadowMode);
        }

        public bool FogNight
        {
            get
            {
                if (FogNightOverride != 0) return FogNightOverride > 0;
                float m = MinuteOfDay;   // UpdateLighting の昼の明るさが半分より下（18:15〜6:45）
                return m >= 1095f || m < 405f;
            }
        }

        /// <summary>この場所が篤から見えるか（フォグが切れていれば、いつも見える）。</summary>
        public bool FogSees(Vector3 p)
        {
            if (!fogWasOn) return true;
            float lit = 0f;
            for (int k = 0; k < fogLit.Count; k++) if (InRect4(p, fogLit[k], fogLitY[k])) { lit = 1f; break; }
            if (lit < 1f && fogCircle.w > 0.5f && new Vector2(p.x - fogCircle.x, p.z - fogCircle.y).magnitude <= fogCircle.z) lit = 1f;
            if (lit < 1f && fogSight.w > 0.5f && p.y >= fogSightY.x && p.y <= fogSightY.y)
            {
                Vector2 v = new Vector2(p.x - fogSight.x, p.z - fogSight.y);
                float d = v.magnitude;
                if (d <= fogSight.z && d <= SightHitAt(v) + fogSightY.z) lit = 1f;
            }
            if (lit < 1f) return false;
            for (int k = 0; k < fogDark.Count; k++) if (InRect4(p, fogDark[k], fogDarkY[k])) return false;
            if (fogCone.w > 0.5f)
            {
                Vector2 v = new Vector2(p.x - fogCone.x, p.z - fogCone.y);
                float d = v.magnitude;
                if (d <= fogConeDir.w) return true;
                if (d > fogCone.z || Vector2.Dot(v / Mathf.Max(d, 1e-4f), new Vector2(fogConeDir.x, fogConeDir.y)) < fogConeDir.z) return false;
            }
            return true;
        }

        /// <summary>キャラが見えるか（吹き出し・名札を出すか）。</summary>
        public bool FogShows(SimCharacter c) { return !fogWasOn || c == null || c.IsTarget || !c.FogHidden; }

        static bool InRect4(Vector3 p, Vector4 r, Vector4 y) { return p.x >= r.x && p.x <= r.z && p.z >= r.y && p.z <= r.w && p.y >= y.x && p.y <= y.y; }
        static Vector4 R4(Rect r, float grow) { return new Vector4(r.xMin - grow, r.yMin - grow, r.xMax + grow, r.yMax + grow); }
        static Vector4 FloorRange(int floor) { float y = FloorY(floor); return new Vector4(y - 0.6f, y + (floor >= 3 ? 8f : FloorH - 0.25f), 0f, 0f); }

        void AddLit(Rect r, int floor) { if (fogLit.Count < FogMax) { fogLit.Add(R4(r, 0.3f)); fogLitY.Add(FloorRange(floor)); } }
        void AddDark(Rect r, int floor) { if (fogDark.Count < FogMax) { fogDark.Add(R4(r, -0.25f)); fogDarkY.Add(FloorRange(floor)); } }

        void BuildFogRegions(SimCharacter f)
        {
            EnsureFogDoors();
            fogLit.Clear(); fogLitY.Clear(); fogDark.Clear(); fogDarkY.Clear(); fogLitAreas.Clear();
            Vector3 fp = f.transform.position;
            int floor = FloorOf(fp.y);
            var here = AreaAt(fp);
            bool outdoor = IsOutdoorArea(here);
            fogCircle = Vector4.zero;
            fogSight = Vector4.zero;
            if (FogSightMode)
            {
                BuildSight(f, outdoor);
                FogStatus = (here != null ? here.name : "外") + "（見通し " + fogSight.z + "m）";
            }
            else if (!outdoor)
            {
                fogLitAreas.Add(here);
                List<FogDoor> ds;
                if (fogDoorsOf.TryGetValue(here, out ds))
                    foreach (var d in ds)
                    {
                        if (Flat(d.p, fp) > FogDoorSee) continue;
                        var o = d.Other(here);
                        if (o == null || fogLitAreas.Contains(o)) continue;
                        if (IsOutdoorArea(o))
                        {
                            // 外へ開いた出入口：外は篤のまわり 12m の四角だけ
                            Rect box = Rect.MinMaxRect(fp.x - 12f, fp.z - 12f, fp.x + 12f, fp.z + 12f);
                            Rect r = Rect.MinMaxRect(Mathf.Max(box.xMin, o.rect.xMin), Mathf.Max(box.yMin, o.rect.yMin), Mathf.Min(box.xMax, o.rect.xMax), Mathf.Min(box.yMax, o.rect.yMax));
                            if (r.width > 0f && r.height > 0f) AddLit(r, o.floor);
                            continue;
                        }
                        fogLitAreas.Add(o);
                    }
                foreach (var a in fogLitAreas) AddLit(a.rect, a.floor);
                // 明るい区画と重なっていて、AreaAt で先に当たる（＝その場所の持ち主の）明るくない区画は暗く
                for (int i = 0; i < areaList.Count; i++)
                {
                    var e = areaList[i];
                    if (e.floor != floor || fogLitAreas.Contains(e)) continue;
                    foreach (var l in fogLitAreas)
                        if (areaList.IndexOf(l) > i && e.rect.Overlaps(l.rect)) { AddDark(e.rect, e.floor); break; }
                }
                FogStatus = here.name + "（明るい区画 " + fogLitAreas.Count + "）";
            }
            else
            {
                fogCircle = new Vector4(fp.x, fp.z, FogOutdoorRadius, 1f);
                // 建物の中の区画は暗い（近い順。すぐそばの出入口でつながっている部屋だけ明るい）
                var near = new List<KeyValuePair<float, AreaInfo>>();
                foreach (var a in areaList)
                {
                    if (IsOutdoorArea(a)) continue;
                    float dx = Mathf.Max(0f, Mathf.Max(a.rect.xMin - fp.x, fp.x - a.rect.xMax));
                    float dz = Mathf.Max(0f, Mathf.Max(a.rect.yMin - fp.z, fp.z - a.rect.yMax));
                    float d = Mathf.Sqrt(dx * dx + dz * dz);
                    if (d < FogOutdoorRadius + 1f) near.Add(new KeyValuePair<float, AreaInfo>(d, a));
                }
                near.Sort((x, y) => x.Key.CompareTo(y.Key));
                foreach (var kv in near)
                {
                    var a = kv.Value;
                    bool door = false;
                    List<FogDoor> ds;
                    if (a.floor == floor && here != null && fogDoorsOf.TryGetValue(a, out ds))
                        foreach (var d in ds) if (d.Other(a) == here && Flat(d.p, fp) < 4f) { door = true; break; }
                    if (door) { fogLitAreas.Add(a); AddLit(a.rect, a.floor); }
                    else AddDark(a.rect, a.floor);
                }
                FogStatus = (here != null ? here.name : "外") + "（半径 " + FogOutdoorRadius + "m・中の区画 " + near.Count + "）";
            }
            // 夜の扇形
            if (FogNight)
            {
                Vector3 dir = f.transform.forward;
                var tp = f.TalkPartner;
                if (f.state == SimCharacter.State.Talking && tp != null) { Vector3 v = tp.transform.position - fp; v.y = 0f; if (v.sqrMagnitude > 0.01f) dir = v; }
                dir.y = 0f; dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.forward;
                fogCone = new Vector4(fp.x, fp.z, FogNightRadius, 1f);
                fogConeDir = new Vector4(dir.x, dir.z, Mathf.Cos(FogNightHalfAngle * Mathf.Deg2Rad), FogNear);
                FogStatus += "・夜";
            }
            else fogCone = Vector4.zero;
        }

        void DrawFog()
        {
            if (fogMat == null)
            {
                var sh = Shader.Find("Hidden/LifeSim/Fog");
                if (sh == null) { FogStatus = "シェーダーが無い"; return; }
                fogMat = new Material(sh) { name = "LifeSimFog" };
            }
            if (fogMesh == null)
            {
                fogMesh = new Mesh { name = "FogTriangle" };
                fogMesh.vertices = new[] { new Vector3(-1f, -1f, 0f), new Vector3(3f, -1f, 0f), new Vector3(-1f, 3f, 0f) };
                fogMesh.triangles = new[] { 0, 1, 2 };
                fogMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
            }
            SetArray("_FogLit", fogLit); SetArray("_FogLitY", fogLitY);
            SetArray("_FogDark", fogDark); SetArray("_FogDarkY", fogDarkY);
            fogMat.SetFloat("_FogLitN", fogLit.Count);
            fogMat.SetFloat("_FogDarkN", fogDark.Count);
            fogMat.SetVector("_FogCircle", fogCircle);
            fogMat.SetVector("_FogCone", fogCone);
            fogMat.SetVector("_FogConeDir", fogConeDir);
            fogMat.SetVector("_FogLook", new Vector4(FogKeep, FogDarkness, 3f, 0f));
            fogMat.SetVector("_FogSight", fogSight);
            fogMat.SetVector("_FogSightY", fogSightY);
            if (fogSightTex != null) fogMat.SetTexture("_FogSightTex", fogSightTex);
            fogMat.SetVector("_FogTint", new Vector4(0.93f, 0.95f, 1.02f, 1f));
            var rp = new RenderParams(fogMat)
            {
                camera = mainCamera,
                layer = 0,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = new Bounds(mainCamera.transform.position, Vector3.one * 100000f),
            };
            Graphics.RenderMesh(rp, fogMesh, 0, Matrix4x4.identity);
        }

        readonly Vector4[] fogTmp = new Vector4[FogMax];
        void SetArray(string name, List<Vector4> list)
        {
            for (int i = 0; i < FogMax; i++) fogTmp[i] = i < list.Count ? list[i] : Vector4.zero;
            fogMat.SetVectorArray(name, fogTmp);
        }

        // ================= 段B：見通し =================

        /// <summary>篤の目の高さから、まわりに線を出して、さえぎる物までの長さを測る。</summary>
        void BuildSight(SimCharacter f, bool outdoor)
        {
            EnsureSightColliders();
            Vector3 fp = f.transform.position;
            float range = outdoor ? FogOutdoorRadius : FogSightIndoor;
            Vector3 eye = fp + Vector3.up * FogEyeH;
            int mask = 1 << SightLayer;
            RaycastHit h;
            for (int i = 0; i < FogRays; i++)
            {
                float a = (i + 0.5f) / FogRays * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                fogHits[i] = Physics.Raycast(eye, dir, out h, range, mask, QueryTriggerInteraction.Ignore) ? h.distance : range;
            }
            if (fogSightTex == null)
            {
                fogSightTex = new Texture2D(FogRays, 1, TextureFormat.RFloat, false, true) { name = "FogSight", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            }
            fogSightTex.SetPixelData(fogHits, 0);
            fogSightTex.Apply(false, false);
            fogSight = new Vector4(fp.x, fp.z, range, 1f);
            int floor = FloorOf(fp.y);
            Vector4 yr = FloorRange(floor);
            if (outdoor) yr = new Vector4(fp.y - 8f, fp.y + 12f, 0f, 0f);
            fogSightY = new Vector4(yr.x, yr.y, 0.35f, FogRays);   // z：当たった物の表面も明るくする幅
        }

        float SightHitAt(Vector2 v)
        {
            float a = Mathf.Atan2(v.y, v.x);
            if (a < 0f) a += Mathf.PI * 2f;
            int i = Mathf.Clamp(Mathf.FloorToInt(a / (Mathf.PI * 2f) * FogRays), 0, FogRays - 1);
            return fogHits[i];
        }

        static readonly string[] SightSkipWords = { "Glow", "Flame", "Sky", "Water", "Ground", "Grass", "Leaf", "Foliage", "Mountain", "Cloud", "Sea", "Swash", "Light", "Candle" };

        /// <summary>線が当たる見えない当たりを、寮・学園・家具の物につける（最初に1回。命令 fog rebuild で作り直す）。</summary>
        void EnsureSightColliders()
        {
            if (fogSightBuilt) return;
            fogSightBuilt = true;
            int n = 0;
            foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (mr == null || mr.GetComponentInParent<SimCharacter>() != null) continue;
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                int layer = mr.gameObject.layer;
                if (layer == 29 || layer == 30 || layer == SightLayer) continue;    // 座面・地面と階段（ProtoBody.Ground）
                Bounds wb = mr.bounds;
                if (wb.size.y < 0.3f) continue;
                if (wb.size.x * wb.size.z > 150f && Mathf.Min(wb.size.x, wb.size.z) > 2f) continue;   // 地面・大きなかたまり
                bool skip = false;
                foreach (var w in SightSkipWords) if (mr.name.IndexOf(w, System.StringComparison.OrdinalIgnoreCase) >= 0) { skip = true; break; }
                if (!skip)
                    foreach (var m in mr.sharedMaterials)
                        if (m != null && m.shader != null && (m.shader.name.Contains("Foliage") || m.shader.name.Contains("Water") || m.shader.name.Contains("Sky") || m.renderQueue >= 2900)) { skip = true; break; }
                if (skip) continue;
                var go = new GameObject("FogSight");
                go.layer = SightLayer;
                go.transform.SetParent(mr.transform, false);
                var bc = go.AddComponent<BoxCollider>();
                var b = mf.sharedMesh.bounds;
                bc.center = b.center;
                bc.size = new Vector3(Mathf.Max(b.size.x, 0.02f), Mathf.Max(b.size.y, 0.02f), Mathf.Max(b.size.z, 0.02f));
                n++;
            }
            FogSightColliders = n;
        }

        /// <summary>家具を動かしたあとなど：見えない当たりを作り直す。</summary>
        public void RebuildFogSight()
        {
            foreach (var bc in Object.FindObjectsByType<BoxCollider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (bc != null && bc.gameObject.name == "FogSight" && bc.gameObject.layer == SightLayer) Destroy(bc.gameObject);
            fogSightBuilt = false;
        }

        // ================= 区画どうしの出入口 =================

        /// <summary>同じ階のとなり合う区画の境を調べ、歩ける所（壁の切れ目）を出入口として覚える。</summary>
        void EnsureFogDoors()
        {
            if (fogDoorsOf != null && fogDoorsAreaCount == areaList.Count) return;
            fogDoorsAreaCount = areaList.Count;
            fogDoorsOf = new Dictionary<AreaInfo, List<FogDoor>>();
            for (int i = 0; i < areaList.Count; i++)
                for (int j = i + 1; j < areaList.Count; j++)
                {
                    var A = areaList[i]; var B = areaList[j];
                    if (A.floor != B.floor || A == B) continue;
                    FindFogDoors(A, B);
                }
        }

        public int FogDoorCount
        {
            get { EnsureFogDoors(); int n = 0; foreach (var kv in fogDoorsOf) n += kv.Value.Count; return n / 2; }
        }

        void AddFogDoor(AreaInfo A, AreaInfo B, Vector3 p)
        {
            var d = new FogDoor { a = A, b = B, p = p };
            List<FogDoor> l;
            if (!fogDoorsOf.TryGetValue(A, out l)) fogDoorsOf[A] = l = new List<FogDoor>();
            l.Add(d);
            if (!fogDoorsOf.TryGetValue(B, out l)) fogDoorsOf[B] = l = new List<FogDoor>();
            l.Add(d);
        }

        void FindFogDoors(AreaInfo A, AreaInfo B)
        {
            Rect a = A.rect, b = B.rect;
            const float gap = 0.8f;
            float ox = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
            float oz = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
            if (ox < -gap || oz < -gap) return;
            float y = FloorY(A.floor);
            if (ox > 0.5f && oz > 0.5f)
            {
                // 重なっている：ひと続きの場所とみなす（重なりの真ん中を出入口に）
                AddFogDoor(A, B, new Vector3((Mathf.Max(a.xMin, b.xMin) + Mathf.Min(a.xMax, b.xMax)) * 0.5f, y, (Mathf.Max(a.yMin, b.yMin) + Mathf.Min(a.yMax, b.yMax)) * 0.5f));
                return;
            }
            var nav = Nav(A.floor);
            if (oz > 0.6f)
            {
                float x = float.NaN;
                if (Mathf.Abs(a.xMax - b.xMin) < gap) x = (a.xMax + b.xMin) * 0.5f;
                else if (Mathf.Abs(b.xMax - a.xMin) < gap) x = (b.xMax + a.xMin) * 0.5f;
                if (!float.IsNaN(x)) ScanFogEdge(A, B, nav, true, x, Mathf.Max(a.yMin, b.yMin), Mathf.Min(a.yMax, b.yMax), y);
            }
            if (ox > 0.6f)
            {
                float z = float.NaN;
                if (Mathf.Abs(a.yMax - b.yMin) < gap) z = (a.yMax + b.yMin) * 0.5f;
                else if (Mathf.Abs(b.yMax - a.yMin) < gap) z = (b.yMax + a.yMin) * 0.5f;
                if (!float.IsNaN(z)) ScanFogEdge(A, B, nav, false, z, Mathf.Max(a.xMin, b.xMin), Mathf.Min(a.xMax, b.xMax), y);
            }
        }

        /// <summary>境の線にそって 0.2m ごとに、境をまたいで歩けるか調べる。続けて歩ける所の真ん中を出入口に。</summary>
        void ScanFogEdge(AreaInfo A, AreaInfo B, GridNav nav, bool vertical, float at, float s0, float s1, float y)
        {
            float runStart = float.NaN, last = 0f;
            for (float s = s0 + 0.1f; s <= s1 - 0.1f + 1e-3f; s += 0.2f)
            {
                Vector3 p = vertical ? new Vector3(at, y, s) : new Vector3(s, y, at);
                Vector3 n = vertical ? new Vector3(0.5f, 0f, 0f) : new Vector3(0f, 0f, 0.5f);
                bool free = nav.IsFree(p) && nav.IsFree(p - n) && nav.IsFree(p + n);
                if (free) { if (float.IsNaN(runStart)) runStart = s; last = s; }
                else if (!float.IsNaN(runStart)) { EndFogRun(A, B, vertical, at, runStart, last, y); runStart = float.NaN; }
            }
            if (!float.IsNaN(runStart)) EndFogRun(A, B, vertical, at, runStart, last, y);
        }

        void EndFogRun(AreaInfo A, AreaInfo B, bool vertical, float at, float r0, float r1, float y)
        {
            if (r1 - r0 < 0.19f) return;   // 1か所だけ通れるのは、すき間とみなさない
            float m = (r0 + r1) * 0.5f;
            AddFogDoor(A, B, vertical ? new Vector3(at, y, m) : new Vector3(m, y, at));
        }
    }
}
