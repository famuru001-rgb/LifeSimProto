using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 学校の本館の内装と什器（2026-10-25 famuru「簡単に学校の内装と什器を作って。教室は同じ形で大丈夫」）。
    /// 見た目とふさがる所だけ（座る席や見どころは、授業のしくみを作るときに足す。今は寮から遠くへ行きすぎないように）。
    /// 部屋の中の位置は u（部屋の西の壁から東へ m）と v（部屋の北の壁から南へ m）で書く。
    /// </summary>
    public partial class LoungeWorld
    {
        Material boardMat, metalMat, curtainMat, schoolTileMat;

        void SchoolInteriorMaterials()
        {
            if (boardMat != null) return;
            boardMat = ProtoArt.Mat(new Color(0.16f, 0.3f, 0.22f), null, null, 0.1f);       // 黒板
            metalMat = ProtoArt.Mat(new Color(0.55f, 0.58f, 0.62f), null, null, 0.5f);     // ロッカー
            curtainMat = ProtoArt.Mat(new Color(0.85f, 0.9f, 0.92f), ProtoArt.Fabric, new Vector2(2f, 2f), 0.05f); // 保健室のカーテン
            schoolTileMat = ProtoArt.Mat(new Color(0.82f, 0.88f, 0.9f), ProtoArt.Flagstone, new Vector2(4f, 4f), 0.3f); // シャワー・トイレの床
        }

        // ---- 部屋の中の位置（x0：部屋の西のはしの sx、sy0：部屋の北のはしの sy） ----
        static Vector3 RU(float x0, float sy0, float u, float v, float y) { return S3(x0 + u / SK, sy0 + v / SK, y); }

        /// <summary>箱を置く。su は東西（u）の長さ、sv は南北（v）の長さ（m）。</summary>
        GameObject FBox(string name, float x0, float sy0, int f, float u, float v, float y0, float su, float h, float sv, Material m, bool block = false)
        {
            var p = RU(x0, sy0, u, v, FloorY(f) + y0 + h / 2f);
            var go = Box(name, p, new Vector3(sv, h, su), m);
            if (block) blockers[f].Add(new Rect(p.x - sv / 2f, p.z - su / 2f, sv, su));
            return go;
        }

        void FBlock(float x0, float sy0, int f, float u0, float v0, float u1, float v1)
        {
            Vector3 a = RU(x0, sy0, u0, v0, 0f), b = RU(x0, sy0, u1, v1, 0f);
            blockers[f].Add(Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.z, b.z), Mathf.Max(a.x, b.x), Mathf.Max(a.z, b.z)));
        }

        /// <summary>机と椅子1組。face：座った人が向く方向（0＝西 1＝東 2＝北 3＝南）。</summary>
        void FDesk(float x0, float sy0, int f, float u, float v, int face, float w = 1.2f)
        {
            float du = face == 0 ? -1f : (face == 1 ? 1f : 0f), dv = face == 2 ? -1f : (face == 3 ? 1f : 0f);
            bool alongV = du != 0f; // 机の長い方が南北
            float su = alongV ? 0.6f : w, sv = alongV ? w : 0.6f;
            FBox("Desk_Top", x0, sy0, f, u, v, 0.72f, su, 0.05f, sv, woodMid);
            FBox("Desk_Front", x0, sy0, f, u + du * 0.25f, v + dv * 0.25f, 0.1f, alongV ? 0.05f : w - 0.1f, 0.62f, alongV ? w - 0.1f : 0.05f, woodDark);
            float cu = u - du * 0.55f, cv = v - dv * 0.55f;
            FBox("Chair_Seat", x0, sy0, f, cu, cv, 0.42f, 0.42f, 0.05f, 0.42f, woodMid);
            FBox("Chair_Back", x0, sy0, f, cu - du * 0.2f, cv - dv * 0.2f, 0.47f, alongV ? 0.05f : 0.42f, 0.45f, alongV ? 0.42f : 0.05f, woodMid);
            FBox("Chair_Legs", x0, sy0, f, cu, cv, 0f, 0.36f, 0.42f, 0.36f, woodDark);
            float h = w / 2f;
            if (alongV) FBlock(x0, sy0, f, Mathf.Min(u + du * 0.3f, cu - du * 0.3f), v - h, Mathf.Max(u + du * 0.3f, cu - du * 0.3f), v + h);
            else FBlock(x0, sy0, f, u - h, Mathf.Min(v + dv * 0.3f, cv - dv * 0.3f), u + h, Mathf.Max(v + dv * 0.3f, cv - dv * 0.3f));
        }

        /// <summary>椅子だけ。face：座った人が向く方向（0＝西 1＝東 2＝北 3＝南）。</summary>
        void FChair(float x0, float sy0, int f, float u, float v, int face)
        {
            float du = face == 0 ? -1f : (face == 1 ? 1f : 0f), dv = face == 2 ? -1f : (face == 3 ? 1f : 0f);
            bool alongV = du != 0f;
            FBox("Chair_Seat", x0, sy0, f, u, v, 0.42f, 0.42f, 0.05f, 0.42f, woodMid);
            FBox("Chair_Back", x0, sy0, f, u - du * 0.2f, v - dv * 0.2f, 0.47f, alongV ? 0.05f : 0.42f, 0.45f, alongV ? 0.42f : 0.05f, woodMid);
            FBox("Chair_Legs", x0, sy0, f, u, v, 0f, 0.36f, 0.42f, 0.36f, woodDark);
            FBlock(x0, sy0, f, u - 0.25f, v - 0.25f, u + 0.25f, v + 0.25f);
        }

        /// <summary>ベッド（頭は北）。</summary>
        void FBed(float x0, float sy0, int f, float u, float v)
        {
            FBox("Bed_Frame", x0, sy0, f, u, v, 0f, 0.95f, 0.45f, 2.0f, iron, true);
            FBox("Bed_Mattress", x0, sy0, f, u, v, 0.45f, 0.9f, 0.12f, 1.95f, fabricWhite);
            FBox("Bed_Pillow", x0, sy0, f, u, v - 0.75f, 0.57f, 0.6f, 0.1f, 0.35f, fabricWhite);
            FBox("Bed_Blanket", x0, sy0, f, u, v + 0.25f, 0.57f, 0.92f, 0.04f, 1.3f, curtainMat);
        }

        /// <summary>壁ぎわの棚。wall：0＝西 1＝東 2＝北 3＝南の壁。c は壁に沿った真ん中の位置（m）。</summary>
        void FShelf(float x0, float sy0, int f, float W, float D, int wall, float c, float w, float h, Material a, Material b, int seed)
        {
            float u, v, yaw;
            switch (wall)
            {
                case 0: u = 0.35f; v = c; yaw = 0f; break;
                case 1: u = W - 0.35f; v = c; yaw = 180f; break;
                case 2: u = c; v = 0.35f; yaw = 90f; break;
                default: u = c; v = D - 0.35f; yaw = -90f; break;
            }
            ShelfRow(RU(x0, sy0, u, v, FloorY(f)), yaw, w, h, Mathf.Max(2, Mathf.RoundToInt(h / 0.5f)), a, b, seed);
        }

        void FLockers(float x0, float sy0, int f, float u0, float u1, float v, bool north)
        {
            for (float u = u0; u < u1 - 0.3f; u += 0.62f)
            {
                FBox("Locker", x0, sy0, f, u + 0.3f, v, 0f, 0.58f, 1.9f, 0.45f, metalMat);
                FBox("Locker_Line", x0, sy0, f, u + 0.3f, v + (north ? 0.23f : -0.23f), 1.2f, 0.4f, 0.03f, 0.01f, iron);
            }
            FBlock(x0, sy0, f, u0, v - 0.25f, u1, v + 0.25f);
        }

        void FStalls(float x0, float sy0, int f, float u0, float u1, float vWall, float depth, bool south, Material door)
        {
            float vIn = south ? vWall - depth : vWall + depth;
            for (float u = u0; u <= u1 + 0.01f; u += 1.3f)
                FBox("Stall_Wall", x0, sy0, f, u, (vWall + vIn) / 2f, 0f, 0.05f, 2.0f, depth, woodDark);
            for (float u = u0; u < u1 - 0.3f; u += 1.3f)
                FBox("Stall_Door", x0, sy0, f, u + 0.65f, vIn, 0f, 1.1f, 1.8f, 0.04f, door);
            FBlock(x0, sy0, f, u0, Mathf.Min(vWall, vIn), u1, Mathf.Max(vWall, vIn));
        }

        void FTable(float x0, float sy0, int f, float u, float v, float su, float sv, bool benches)
        {
            FBox("Table_Top", x0, sy0, f, u, v, 0.72f, su, 0.06f, sv, woodMid);
            FBox("Table_Legs", x0, sy0, f, u, v, 0f, su - 0.3f, 0.72f, sv - 0.3f, woodDark);
            if (benches)
            {
                bool longU = su >= sv;
                for (int k = -1; k <= 1; k += 2)
                {
                    if (longU) FBox("Bench", x0, sy0, f, u, v + k * (sv / 2f + 0.35f), 0f, su, 0.45f, 0.35f, woodDark);
                    else FBox("Bench", x0, sy0, f, u + k * (su / 2f + 0.35f), v, 0f, 0.35f, 0.45f, sv, woodDark);
                }
                if (longU) FBlock(x0, sy0, f, u - su / 2f, v - sv / 2f - 0.55f, u + su / 2f, v + sv / 2f + 0.55f);
                else FBlock(x0, sy0, f, u - su / 2f - 0.55f, v - sv / 2f, u + su / 2f + 0.55f, v + sv / 2f);
            }
            else FBlock(x0, sy0, f, u - su / 2f, v - sv / 2f, u + su / 2f, v + sv / 2f);
        }

        void FCircle(float x0, float sy0, int f, float u, float v, float r, Color c)
        {
            Cyl("MagicCircle", RU(x0, sy0, u, v, FloorY(f) + 0.015f), new Vector3(r * 2f, 0.004f, r * 2f), Quaternion.identity, ProtoArt.Glow(c, 0.6f));
        }

        void FDummy(float x0, float sy0, int f, float u, float v)
        {
            var p = RU(x0, sy0, u, v, FloorY(f));
            Cyl("Dummy_Post", p + Vector3.up * 0.8f, new Vector3(0.12f, 0.8f, 0.12f), Quaternion.identity, woodDark);
            Cyl("Dummy_Body", p + Vector3.up * 1.35f, new Vector3(0.45f, 0.35f, 0.45f), Quaternion.identity, fabricBrown);
            Cyl("Dummy_Head", p + Vector3.up * 1.85f, new Vector3(0.28f, 0.14f, 0.28f), Quaternion.identity, fabricBrown);
            blockers[f].Add(new Rect(p.x - 0.3f, p.z - 0.3f, 0.6f, 0.6f));
        }

        // ================= 部屋ごとの什器 =================
        void FurnishSchoolRoom(int f, SRoom r, float sy0, float sy1, bool southSide)
        {
            SchoolInteriorMaterials();
            if (r.kind == SKind.Vacant || r.kind == SKind.Pass || r.kind == SKind.Stair) return;
            float x0 = r.x0;
            float W = (r.x1 - r.x0) * SK, D = (sy1 - sy0) * SK;
            int seed = Mathf.RoundToInt(r.x0 * 10f) + f * 1000 + (southSide ? 500 : 0);
            // 扉は廊下の側（北の列は南の壁 v＝D、南の列は北の壁 v＝0）の真ん中。扉の前 2.5m はあけておく
            float vSafe0 = southSide ? 2.6f : 1.3f, vSafe1 = southSide ? D - 1.3f : D - 2.6f;
            string n = r.name;

            if (r.kind == SKind.Hall) { FurnishEntrance(f, x0, sy0, W, D); return; }

            if (n.EndsWith("の教室"))
            {
                // 黒板（西の壁）・先生の机・生徒の机（西を向く）
                FBox("Blackboard_Frame", x0, sy0, f, 0.1f, D / 2f, 0.95f, 0.06f, 1.5f, Mathf.Min(7.2f, D - 3f), woodDark);
                FBox("Blackboard", x0, sy0, f, 0.14f, D / 2f, 1.0f, 0.04f, 1.4f, Mathf.Min(7f, D - 3.2f), boardMat);
                FBox("Chalk_Tray", x0, sy0, f, 0.2f, D / 2f, 0.95f, 0.12f, 0.04f, Mathf.Min(7f, D - 3.2f), woodMid);
                FBox("Teacher_Desk", x0, sy0, f, 2.0f, D / 2f, 0f, 0.8f, 0.8f, 1.8f, woodDark, true);
                for (float u = 4.0f; u <= W - 1.5f; u += 1.9f)
                    for (float v = vSafe0; v <= vSafe1 + 0.01f; v += 2.0f)
                        FDesk(x0, sy0, f, u, v, 0);
                FShelf(x0, sy0, f, W, D, 1, D / 2f, 3.0f, 1.2f, paperMat, woodFloor, seed); // 後ろの低い棚
                return;
            }
            switch (n)
            {
                case "職員室":
                    // 机の島3つ（向かい合わせ）と、窓の下の低い棚
                    for (int i = 0; i < 3; i++)
                    {
                        float cu = 3.5f + i * (W - 7f) / 2f;
                        for (int k = 0; k < 3; k++)
                        {
                            FDesk(x0, sy0, f, cu - 0.4f, vSafe0 + 2.2f + k * 1.4f, 1);
                            FDesk(x0, sy0, f, cu + 0.4f, vSafe0 + 2.2f + k * 1.4f, 0);
                        }
                    }
                    FShelf(x0, sy0, f, W, D, 3, W * 0.3f, 4f, 1.0f, paperMat, woodFloor, seed);
                    FShelf(x0, sy0, f, W, D, 3, W * 0.7f, 4f, 1.0f, paperMat, woodFloor, seed + 1);
                    FDesk(x0, sy0, f, W - 2.2f, D - 3.0f, 2, 1.6f); // 学年主任の机
                    break;
                case "保健室":
                case "医学実習室":
                {
                    int beds = n == "保健室" ? 3 : 4;
                    for (int i = 0; i < beds; i++)
                    {
                        float u = 1.4f + i * 2.1f;
                        if (u > W - 1f) break;
                        FBed(x0, sy0, f, u, D - 1.6f);
                        if (n == "保健室") FBox("Curtain", x0, sy0, f, u + 1.05f, D - 1.6f, 0.05f, 0.04f, 1.9f, 2.2f, curtainMat);
                    }
                    FShelf(x0, sy0, f, W, D, 0, 4.0f, 2.0f, 1.8f, glassMat, fabricWhite, seed);
                    FTable(x0, sy0, f, W / 2f, 5.0f, 2.0f, 1.0f, false);
                    if (n == "医学実習室") FTable(x0, sy0, f, W / 2f, 7.0f, 2.0f, 1.0f, false);
                    FDesk(x0, sy0, f, W - 1.5f, 5.0f, 0);
                    break;
                }
                case "宿直室":
                    FBed(x0, sy0, f, 1.4f, D - 1.6f);
                    FDesk(x0, sy0, f, W - 1.2f, D - 3.0f, 1);
                    FShelf(x0, sy0, f, W, D, 0, 5.0f, 2.0f, 1.6f, paperMat, woodFloor, seed);
                    FBox("Rug_Night", x0, sy0, f, W / 2f, D / 2f + 1f, 0.006f, 2.4f, 0.01f, 2.0f, rugMat);
                    break;
                case "トイレ":
                    FBox("Toilet_Floor", x0, sy0, f, W / 2f, D / 2f, 0.004f, W - 0.2f, 0.01f, D - 0.2f, schoolTileMat);
                    if (southSide) FStalls(x0, sy0, f, 0.4f, W - 0.4f, D, 1.6f, true, woodMid);
                    else FStalls(x0, sy0, f, 0.4f, W - 0.4f, 0f, 1.6f, false, woodMid);
                    for (float v = 4.5f; v <= D - 4.5f; v += 1.2f) // 手洗い（西の壁）
                    {
                        FBox("Sink", x0, sy0, f, 0.35f, v, 0.75f, 0.5f, 0.12f, 0.8f, fabricWhite);
                        FBox("Mirror", x0, sy0, f, 0.05f, v, 1.2f, 0.03f, 0.6f, 0.7f, waterMat);
                    }
                    FBlock(x0, sy0, f, 0f, 4.0f, 0.65f, D - 4.0f);
                    break;
                case "更衣室":
                    FLockers(x0, sy0, f, 0.4f, W - 0.4f, 0.3f, true);
                    FLockers(x0, sy0, f, 0.4f, W / 2f - 1.5f, D - 0.4f, false);
                    for (int i = 0; i < 3; i++) FBox("Bench", x0, sy0, f, W * (i + 1) / 4f, D / 2f, 0f, 2.0f, 0.45f, 0.45f, woodDark, true);
                    break;
                case "シャワー":
                    FBox("Shower_Floor", x0, sy0, f, W / 2f, D / 2f, 0.004f, W - 0.2f, 0.01f, D - 0.2f, schoolTileMat);
                    FStalls(x0, sy0, f, 0.4f, W - 0.4f, 0f, 1.8f, false, curtainMat);
                    for (float u = 1.05f; u < W - 0.4f; u += 1.3f) Cyl("ShowerHead", RU(x0, sy0, u, 0.2f, FloorY(f) + 2.1f), new Vector3(0.2f, 0.03f, 0.2f), Quaternion.identity, metalMat);
                    FLockers(x0, sy0, f, W - 4.5f, W - 0.4f, D - 0.4f, false);
                    break;
                case "用具室":
                    FShelf(x0, sy0, f, W, D, 2, W / 2f, W - 2f, 2.0f, fabricBrown, woodMid, seed);
                    FShelf(x0, sy0, f, W, D, 0, D / 2f, 4f, 2.0f, fabricRed, woodMid, seed + 1);
                    for (int i = 0; i < 4; i++) FBox("Crate", x0, sy0, f, 2.5f + i * 1.6f, 4.0f + (i % 2) * 1.2f, 0f, 0.9f, 0.7f, 0.9f, woodMid, true);
                    for (int i = 0; i < 5; i++)
                    {
                        var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        ball.name = "Ball"; ball.transform.SetParent(root, false);
                        ball.transform.localPosition = RU(x0, sy0, W - 2.5f + (i % 3) * 0.45f, 5.0f + (i / 3) * 0.45f, FloorY(f) + 0.2f);
                        ball.transform.localScale = Vector3.one * 0.38f;
                        ball.GetComponent<Renderer>().sharedMaterial = i % 2 == 0 ? fabricRed : fabricWhite;
                        Destroy(ball.GetComponent<Collider>());
                    }
                    FBlock(x0, sy0, f, W - 2.8f, 4.7f, W - 1.4f, 5.7f);
                    break;
                case "ダンジョン実習の準備室":
                    FTable(x0, sy0, f, W / 2f, D / 2f - 1f, 3.0f, 1.6f, false);
                    FBox("Dungeon_Map", x0, sy0, f, W / 2f, D / 2f - 1f, 0.78f, 2.2f, 0.01f, 1.2f, paperMat);
                    FShelf(x0, sy0, f, W, D, 1, D / 2f, 4f, 2.0f, iron, fabricBrown, seed);
                    // 武器の棚（剣と盾）
                    FBox("WeaponRack", x0, sy0, f, 3.5f, 0.4f, 0f, 3.0f, 1.8f, 0.3f, woodDark, true);
                    for (int i = 0; i < 5; i++) FBox("Sword", x0, sy0, f, 2.3f + i * 0.6f, 0.6f, 0.3f, 0.06f, 1.2f, 0.05f, metalMat);
                    for (int i = 0; i < 3; i++) Cyl("Shield", RU(x0, sy0, 6.5f + i * 1.0f, 0.3f, FloorY(f) + 1.2f), new Vector3(0.7f, 0.03f, 0.7f), Quaternion.Euler(90f, 90f, 0f), fabricRed);
                    for (int i = 0; i < 3; i++) FBox("Crate", x0, sy0, f, 1.2f + i * 1.1f, D - 3.4f, 0f, 0.9f, 0.7f, 0.9f, woodMid, true);
                    break;
                case "学食・購買部":
                {
                    // 西：購買部のカウンターと棚。東：学食の受け取り口。真ん中に長机と長いす
                    FBox("Shop_Counter", x0, sy0, f, 3.0f, D / 2f, 0f, 0.8f, 1.0f, 5.0f, woodDark, true);
                    FShelf(x0, sy0, f, W, D, 0, D / 2f, 5f, 2.0f, foodMat, paperMat, seed);
                    FBox("Kitchen_Counter", x0, sy0, f, W - 2.5f, D / 2f, 0f, 0.8f, 1.0f, 6.0f, woodDark, true);
                    for (int i = 0; i < 4; i++) FBox("Food", x0, sy0, f, W - 2.5f, D / 2f - 2.2f + i * 1.4f, 1.0f, 0.35f, 0.12f, 0.35f, foodMat);
                    FShelf(x0, sy0, f, W, D, 1, D / 2f, 5f, 2.0f, fabricWhite, terraMat, seed + 1);
                    for (float u = 7.0f; u <= W - 6.0f; u += 4.2f)
                        for (float v = 2.6f; v <= D - 3.0f; v += 3.2f)
                            FTable(x0, sy0, f, u, v, 2.8f, 0.9f, true);
                    Label(RU(x0, sy0, 3.0f, D / 2f, FloorY(f) + 2.2f), "購買部");
                    break;
                }
                case "魔法薬の調合室":
                    for (int i = 0; i < 4; i++)
                    {
                        float u = 3.0f + (i % 2) * (W - 6f), v = vSafe0 + 1.8f + (i / 2) * 4.0f;
                        FTable(x0, sy0, f, u, v, 2.2f, 1.2f, false);
                        Cyl("Cauldron", RU(x0, sy0, u + 1.8f, v, FloorY(f) + 0.35f), new Vector3(0.8f, 0.35f, 0.8f), Quaternion.identity, iron);
                        Cyl("Cauldron_Brew", RU(x0, sy0, u + 1.8f, v, FloorY(f) + 0.71f), new Vector3(0.68f, 0.01f, 0.68f), Quaternion.identity, greenLampMat);
                        FBlock(x0, sy0, f, u + 1.35f, v - 0.45f, u + 2.25f, v + 0.45f);
                        for (int k = 0; k < 3; k++) Cyl("Flask", RU(x0, sy0, u - 0.6f + k * 0.6f, v, FloorY(f) + 0.88f), new Vector3(0.14f, 0.12f, 0.14f), Quaternion.identity, k == 1 ? crystalMat : glassMat);
                    }
                    FShelf(x0, sy0, f, W, D, 2, W / 2f, W - 3f, 2.2f, glassMat, crystalMat, seed);
                    break;
                case "魔法実習室":
                    FCircle(x0, sy0, f, W * 0.3f, D / 2f, 2.2f, new Color(0.4f, 0.55f, 1f));
                    FCircle(x0, sy0, f, W * 0.7f, D / 2f, 2.2f, new Color(0.9f, 0.5f, 1f));
                    for (int i = 0; i < 3; i++) FDummy(x0, sy0, f, W - 1.0f, 3.0f + i * 3.0f);
                    FShelf(x0, sy0, f, W, D, 0, D / 2f, 4f, 2.0f, crystalMat, paperMat, seed);
                    break;
                case "生徒会室":
                    FTable(x0, sy0, f, W / 2f, D / 2f - 0.5f, 4.0f, 1.4f, false);
                    for (int k = 0; k < 3; k++)
                    {
                        FChair(x0, sy0, f, W / 2f - 1.3f + k * 1.3f, D / 2f - 1.75f, 3);
                        FChair(x0, sy0, f, W / 2f - 1.3f + k * 1.3f, D / 2f + 0.75f, 2);
                    }
                    FShelf(x0, sy0, f, W, D, 2, W / 2f, W - 3f, 2.2f, paperMat, fabricRed, seed);
                    FBox("Banner", x0, sy0, f, 0.05f, D / 2f, 1.4f, 0.03f, 1.6f, 2.4f, fabricRed);
                    break;
                default:
                    if (n.EndsWith("部の部室"))
                    {
                        FTable(x0, sy0, f, W / 2f, D / 2f, 3.0f, 1.2f, true);
                        FShelf(x0, sy0, f, W, D, 2, W / 2f, W - 4f, 2.0f, paperMat, woodFloor, seed);
                        if (n.StartsWith("ほうき"))
                            for (int i = 0; i < 5; i++) FBox("Broom", x0, sy0, f, 1.0f + i * 0.5f, D - 2.2f, 0f, 0.05f, 1.5f, 0.05f, woodMid);
                        else if (n.StartsWith("剣術"))
                        {
                            for (int i = 0; i < 4; i++) FBox("WoodSword", x0, sy0, f, 1.0f + i * 0.5f, D - 2.2f, 0.3f, 0.05f, 1.1f, 0.05f, woodDark);
                            FDummy(x0, sy0, f, W - 1.5f, D - 3.0f);
                        }
                        else
                        {
                            FCircle(x0, sy0, f, W - 2.5f, D - 3.5f, 1.2f, new Color(0.4f, 0.55f, 1f));
                            Cyl("Crystal", RU(x0, sy0, W / 2f, D / 2f, FloorY(f) + 0.95f), new Vector3(0.3f, 0.2f, 0.3f), Quaternion.identity, crystalMat);
                        }
                    }
                    break;
            }
        }

        /// <summary>玄関ホール：掲示板・長いす・鉢植え・じゅうたん。</summary>
        void FurnishEntrance(int f, float x0, float sy0, float W, float D)
        {
            if (f > 0) return;
            FBox("Hall_Rug", x0, sy0, f, W / 2f, D / 2f, 0.006f, 4.0f, 0.01f, D - 3f, rugMat);
            FBox("NoticeBoard", x0, sy0, f, 0.1f, D / 2f, 1.1f, 0.06f, 1.4f, 3.0f, woodMid);
            for (int i = 0; i < 4; i++) FBox("Notice", x0, sy0, f, 0.14f, D / 2f - 1.1f + i * 0.75f, 1.3f + (i % 2) * 0.4f, 0.02f, 0.45f, 0.35f, paperMat);
            for (int k = 0; k < 2; k++)
            {
                float u = k == 0 ? 1.5f : W - 1.5f;
                FBox("Hall_Bench", x0, sy0, f, u, D / 2f + 3f, 0f, 0.5f, 0.45f, 2.2f, woodDark, true);
                Pot(RU(x0, sy0, u, D - 1.2f, FloorY(f)), 0.5f, leafMat);
                blockers[f].Add(new Rect(RU(x0, sy0, u, D - 1.2f, 0f).x - 0.3f, RU(x0, sy0, u, D - 1.2f, 0f).z - 0.3f, 0.6f, 0.6f));
            }
        }

        /// <summary>大講堂：壇に向かって長いすを並べる（真ん中に通路）。体育館：壁ぎわに用具。</summary>
        void FurnishSchoolHall(float sx0, float sx1, string name)
        {
            SchoolInteriorMaterials();
            float W = (sx1 - sx0) * SK, D = SVer0 * SK;
            if (name == "大講堂")
            {
                float aisle = W / 2f;
                for (float v = 8.5f; v <= D - 6.0f; v += 1.6f)
                    for (int k = 0; k < 2; k++)
                    {
                        float u0 = k == 0 ? 3.0f : aisle + 1.5f, u1 = k == 0 ? aisle - 1.5f : W - 3.0f;
                        FBox("Pew_Seat", sx0, 0f, 0, (u0 + u1) / 2f, v, 0.4f, u1 - u0, 0.06f, 0.45f, woodMid);
                        FBox("Pew_Back", sx0, 0f, 0, (u0 + u1) / 2f, v + 0.25f, 0.4f, u1 - u0, 0.55f, 0.06f, woodDark);
                        FBox("Pew_Legs", sx0, 0f, 0, (u0 + u1) / 2f, v, 0f, u1 - u0 - 0.2f, 0.4f, 0.3f, woodDark);
                        FBlock(sx0, 0f, 0, u0, v - 0.3f, u1, v + 0.35f);
                    }
                FBox("Lectern", sx0, 0f, 0, W / 2f, 4.6f, 0.6f, 0.7f, 1.1f, 0.5f, woodDark);
                FBox("Hall_Banner", sx0, 0f, 0, W / 2f - 4f, 0.1f, 2.5f, 1.4f, 3.0f, 0.03f, fabricRed);
                FBox("Hall_Banner", sx0, 0f, 0, W / 2f + 4f, 0.1f, 2.5f, 1.4f, 3.0f, 0.03f, fabricGreen);
            }
            else
            {
                // 体育館：壁ぎわに打ち込み台と、用具の箱・跳び箱
                for (int i = 0; i < 4; i++) FDummy(sx0, 0f, 0, 2.0f + i * 2.2f, 1.2f);
                FBox("VaultBox", sx0, 0f, 0, W - 2.5f, 2.0f, 0f, 1.6f, 0.9f, 0.9f, woodMid, true);
                FBox("Mat", sx0, 0f, 0, W - 2.5f, 4.5f, 0f, 2.0f, 0.12f, 1.2f, fabricGreen);
                for (int i = 0; i < 3; i++) FBox("Crate", sx0, 0f, 0, W - 1.2f, 7.0f + i * 1.2f, 0f, 0.9f, 0.7f, 0.9f, woodMid, true);
            }
        }
    }
}
