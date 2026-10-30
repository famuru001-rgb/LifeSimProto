using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 寮生以外の学生の寮（冒険者寮・騎士寮・魔法師寮）。2026-10-31 famuru「npcも校内の寮に住んでいる。他に3つ寮を足す予定」。
    /// 今は仮の建物（中には入れない箱）。本館の北（中庭の開いている側）の原っぱに3つ並べ、学生は朝その寮の扉から出てきて、夕方〜夜に扉から帰る。
    /// 先生は今までどおり正門から。本番の寮を作るときは、この中身を作りかえる。
    /// </summary>
    public partial class LoungeWorld
    {
        /// <summary>寮の建物の西の壁（扉のある面）の x と、奥の x。</summary>
        const float MobDormX0 = 58f, MobDormX1 = 74f;
        /// <summary>寮ごとの南北のはし（z）。0 冒険者寮・1 騎士寮・2 魔法師寮。</summary>
        static readonly float[] MobDormZ0 = { -50f, -78f, -106f }, MobDormZ1 = { -30f, -58f, -86f };
        const float MobDormH = 9f;

        /// <summary>その寮の扉の前（寮生以外の学生が出入りする所）。</summary>
        public static Vector3 MobDormDoor(int dorm)
        {
            dorm = Mathf.Clamp(dorm, 0, 2);
            return new Vector3(MobDormX0 - 1.2f, 0f, (MobDormZ0[dorm] + MobDormZ1[dorm]) / 2f);
        }

        void BuildMobDorms()
        {
            // 原っぱ（もとの地面の東）
            Box("MobDorm_Ground", new Vector3(61.5f, -0.12f, -76f), new Vector3(35f, 0.1f, 112f), groundMat);
            var colors = new[] { new Color(0.62f, 0.45f, 0.30f), new Color(0.52f, 0.56f, 0.62f), new Color(0.46f, 0.38f, 0.62f) };
            var roofs = new[] { new Color(0.45f, 0.22f, 0.16f), new Color(0.22f, 0.28f, 0.42f), new Color(0.28f, 0.18f, 0.40f) };
            Material win = sharedWindowMat ?? ProtoArt.Glow(new Color(0.45f, 0.6f, 0.9f), 0.9f);
            for (int d = 0; d < 3; d++)
            {
                float z0 = MobDormZ0[d], z1 = MobDormZ1[d], cz = (z0 + z1) / 2f, cx = (MobDormX0 + MobDormX1) / 2f;
                var wall = ProtoArt.Mat(colors[d]);
                Box("MobDorm_" + d, new Vector3(cx, MobDormH / 2f, cz), new Vector3(MobDormX1 - MobDormX0, MobDormH, z1 - z0), wall);
                Box("MobDorm_" + d + "_Roof", new Vector3(cx, MobDormH + 0.3f, cz), new Vector3(MobDormX1 - MobDormX0 + 1f, 0.6f, z1 - z0 + 1f), ProtoArt.Mat(roofs[d]));
                // 扉（西の面のまん中）と窓
                Box("MobDorm_" + d + "_Door", new Vector3(MobDormX0 - 0.05f, 1.35f, cz), new Vector3(0.12f, 2.7f, 2.4f), woodDark);
                Box("MobDorm_" + d + "_Step", new Vector3(MobDormX0 - 0.6f, 0.04f, cz), new Vector3(1.2f, 0.08f, 3.2f), flagstone);
                for (int fl = 0; fl < 3; fl++)
                    for (int i = 0; i < 6; i++)
                    {
                        float wz = Mathf.Lerp(z0 + 1.8f, z1 - 1.8f, i / 5f);
                        if (fl == 0 && Mathf.Abs(wz - cz) < 2.2f) continue; // 扉のところ
                        Box("MobDorm_Window", new Vector3(MobDormX0 - 0.04f, 1.6f + fl * 3f, wz), new Vector3(0.06f, 1.3f, 1.0f), win);
                    }
                blockers[0].Add(Rect.MinMaxRect(MobDormX0, z0, MobDormX1, z1));
                Label(new Vector3(MobDormX0 - 0.8f, 3.6f, cz), CharProfiles.MobDormNames[d]);
                LampPost(new Vector3(MobDormX0 - 2.2f, 0f, cz + 2.4f));
                // 扉から北南の小道へ
                Box("MobDorm_Path", new Vector3((MobDormX0 - 1.2f + 51.5f) / 2f, -0.004f, cz), new Vector3(MobDormX0 - 1.2f - 51.5f + 1.6f, 0.04f, 1.6f), flagstone);
            }
            // 本館の中庭の北の口から寮の前を通る小道
            Box("MobDorm_Path", new Vector3(51.5f, -0.004f, -68f), new Vector3(1.8f, 0.04f, 60f), flagstone);
            Box("MobDorm_Path", new Vector3((43.3f + 51.5f) / 2f, -0.004f, -77f), new Vector3(51.5f - 43.3f, 0.04f, 1.8f), flagstone);
            Label(new Vector3(47f, 1.6f, -77f), "寮の通り");
        }

        /// <summary>寮のまわりの歩ける所（1階）。</summary>
        void OpenMobDormNav(GridNav n)
        {
            n.Open(Rect.MinMaxRect(43.7f, -129f, 77f, -25f));   // 原っぱ（建物は blockers でふさぐ）
            n.Open(Rect.MinMaxRect(42.7f, -101f, 43.9f, -53f)); // 本館の中庭の北の口（体育館・大講堂の壁にはつながない）
        }
    }
}
