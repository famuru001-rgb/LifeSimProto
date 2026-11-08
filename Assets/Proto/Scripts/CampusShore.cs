using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// プールと浜辺で過ごす（famuru 2026-11-08「プールと浜辺で過ごせるように」。家具のチャット）。
    /// 見た目（プール・砂浜・寝椅子・たき火・桟橋・小屋）は CampusPreview.cs の Pool・Beach。ここでは場所・席・見どころ・ふさぐ所を作る。
    /// 歩ける所は CampusPreview.OpenShore（NNav）、浜の高さは CampusPreview.GroundLift（見た目だけ下げる）。
    /// famuru の決定：寝椅子で休む・たき火を囲む（夕方と夜も行きやすい）・水をながめる・水ぎわで遊ぶ・砂浜を散歩・
    /// 更衣小屋で水着に着がえる（水着の服ができるまでは制服のまま）・エッチできる所（更衣小屋の中・浜の休憩小屋の中・だれもいない寝椅子・浜のはしの岩かげ）。季節はいつでも。
    /// Logs\no_poolbeach.txt があれば何も作らない。
    /// </summary>
    public partial class LoungeWorld
    {
        public const string PoolArea = "プール", BeachArea = "浜辺";
        public const string PoolHutArea = "更衣小屋", BeachHutArea = "浜の休憩小屋", RockShadeArea = "浜のはしの岩かげ";
        public const string PoolLoungerLabel = "プールの寝椅子", BeachLoungerLabel = "浜の寝椅子";
        /// <summary>エッチできる所（区画の名前）。</summary>
        public static readonly string[] ShoreHAreas = { PoolHutArea, BeachHutArea, RockShadeArea };
        /// <summary>エッチできる寝椅子（席の名前）。</summary>
        public static readonly string[] ShoreHSeats = { PoolLoungerLabel, BeachLoungerLabel };

        void NShore()
        {
            if (!CampusPreview.PoolBeachOn) return;
            float total = CampusPreview.ShoreLen;
            Rect rim = CampusPreview.PoolRim;
            float x0 = rim.xMin, x1 = rim.xMax, y0 = rim.yMin, y1 = rim.yMax;
            Vector3 poolC = CampusPreview.PlanW(rim.center.x, rim.center.y);
            Vector3 seaLook = CampusPreview.ShoreW(total / 2f, -28f) + Vector3.up * 1f;

            // ---- 小さな区画（エッチできる所）を先に。区画を探すときは前のものが先に見つかる ----
            AddArea(PoolHutArea, CampusPreview.PlanRectW(CampusPreview.PoolHutInside), 0);
            Vector3 bh = CampusPreview.ShoreW(CampusPreview.BeachHutAlong, CampusPreview.BeachHutAcross);
            AddArea(BeachHutArea, new Rect(bh.x - 1.3f, bh.z - 1.3f, 2.6f, 2.6f), 0);
            Vector3 rs = CampusPreview.ShoreW(3.2f, 5f);
            AddArea(RockShadeArea, new Rect(rs.x - 2f, rs.z - 2f, 4f, 4f), 0);
            // ---- プールと浜辺（行事の会場「プール」「浜辺」にも使われる） ----
            AddArea(PoolArea, CampusPreview.PlanRectW(CampusPreview.PoolZone), 0);
            {
                float mnx = float.MaxValue, mnz = float.MaxValue, mxx = float.MinValue, mxz = float.MinValue;
                for (float al = 0f; al <= total; al += 2f)
                    foreach (float ac in new[] { 1f, 11f })
                    {
                        var p = CampusPreview.ShoreW(al, ac);
                        mnx = Mathf.Min(mnx, p.x); mxx = Mathf.Max(mxx, p.x); mnz = Mathf.Min(mnz, p.z); mxz = Mathf.Max(mxz, p.z);
                    }
                AddArea(BeachArea, Rect.MinMaxRect(mnx, mnz, mxx, mxz), 0);
            }
            Label(poolC + Vector3.up * 2.2f, PoolArea);
            Label(CampusPreview.ShoreW(total / 2f, 7f) + Vector3.up * 2.2f, BeachArea);

            // ---- プールの寝椅子（北に4つ・南に2つ。足はプールのほう） ----
            System.Action<Vector3, Vector3, Vector3, string, string> lounger = (pos, fwd, app, label, area) =>
            {
                seats.Add(new Seat { kind = SeatKind.Sit, label = label, area = area, position = pos, forward = fwd, approach = app });
                Vector3 side = new Vector3(fwd.z, 0f, -fwd.x);
                // 寝椅子の形（長さ 2m・幅 0.9m）をふさぐ
                Vector3 a = pos - fwd * 1.0f - side * 0.45f, b = pos + fwd * 1.0f + side * 0.45f;
                blockers[0].Add(Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.z, b.z), Mathf.Max(a.x, b.x), Mathf.Max(a.z, b.z)));
            };
            foreach (float x in CampusPreview.PoolLoungerX)
                lounger(CampusPreview.PlanW(x0 + x, y1 + 1.4f), CampusPreview.PlanDir(0f, -1f), CampusPreview.PlanW(x0 + x + 0.95f, y1 + 0.9f), PoolLoungerLabel, PoolArea);
            foreach (float x in CampusPreview.PoolLoungerXS)
                lounger(CampusPreview.PlanW(x0 + x, y0 - 1.4f), CampusPreview.PlanDir(0f, 1f), CampusPreview.PlanW(x0 + x + 0.95f, y0 - 0.9f), PoolLoungerLabel, PoolArea);
            // パラソルの柱
            foreach (var q in new[] { new Vector2(x0 + 3f, y1 + 2.2f), new Vector2(x0 + 12f, y1 + 2.2f), new Vector2(x0 + 9f, y0 - 2.2f) })
            {
                var w = CampusPreview.PlanW(q.x, q.y);
                blockers[0].Add(new Rect(w.x - 0.15f, w.z - 0.15f, 0.3f, 0.3f));
            }

            // ---- 浜の寝椅子（4つ。足は湖のほう） ----
            foreach (float al in CampusPreview.BeachLoungerAlong)
            {
                Vector3 inw = CampusPreview.ShoreIn(al);
                lounger(CampusPreview.ShoreW(al, CampusPreview.BeachLoungerAcross), -inw, CampusPreview.ShoreW(al + 1f, CampusPreview.BeachLoungerAcross - 0.6f), BeachLoungerLabel, BeachArea);
            }
            foreach (float al in new[] { 7f, 22f })
            {
                var w = CampusPreview.ShoreW(al, 7f);
                blockers[0].Add(new Rect(w.x - 0.15f, w.z - 0.15f, 0.3f, 0.3f));
            }

            // ---- たき火（砂の上に座る席5つと、立ってあたる所。夜でも行きやすい） ----
            Vector3 fire = CampusPreview.ShoreW(CampusPreview.CampfireAlong, CampusPreview.CampfireAcross);
            blockers[0].Add(new Rect(fire.x - 0.75f, fire.z - 0.75f, 1.5f, 1.5f));
            Vector3 fin = CampusPreview.ShoreIn(CampusPreview.CampfireAlong);
            for (int i = 0; i < 5; i++)
            {
                Vector3 d = Quaternion.Euler(0f, 36f + i * 72f, 0f) * fin;
                Vector3 p = fire + d * 1.55f;
                seats.Add(new Seat { kind = SeatKind.Sit, floorSeat = true, label = "たき火のそば", area = BeachArea, position = p, forward = -d, approach = p + d * 0.6f });
            }
            {
                var slots = new List<Vector3>();
                for (int i = 0; i < 3; i++) slots.Add(fire + (Quaternion.Euler(0f, i * 120f + 70f, 0f) * fin) * 2.5f);
                var fp = MakePoi("たき火にあたっている", fire + Vector3.up * 0.5f, slots.ToArray());
                fp.outdoor = true; fp.stars = true; fp.shore = true; // 夜でも行きやすい
                pois.Add(fp);
            }

            // ---- 見どころ ----
            {
                var p = MakePoi("桟橋から湖をながめている", CampusPreview.ShoreW(CampusPreview.PierAlong, -40f) + Vector3.up * 1f,
                    CampusPreview.ShoreW(CampusPreview.PierAlong - 0.45f, -17.4f), CampusPreview.ShoreW(CampusPreview.PierAlong + 0.45f, -17.4f), CampusPreview.ShoreW(CampusPreview.PierAlong + 0.4f, -9f));
                p.outdoor = true; p.shore = true; pois.Add(p);
            }
            {
                var p = MakePoi("プールのふちで水をながめている", poolC,
                    CampusPreview.PlanW(x0 + 7.5f, y1 + 0.5f), CampusPreview.PlanW(x0 + 16f, y1 + 0.5f), CampusPreview.PlanW(x0 + 5f, y0 - 0.5f),
                    CampusPreview.PlanW(x0 + 14f, y0 - 0.5f), CampusPreview.PlanW(x0 - 0.5f, rim.center.y));
                p.outdoor = true; p.shore = true; pois.Add(p);
            }
            {
                var p = MakePoi("水ぎわで遊んでいる", seaLook,
                    CampusPreview.ShoreW(11f, 1.6f), CampusPreview.ShoreW(13f, 1.7f), CampusPreview.ShoreW(25f, 1.6f), CampusPreview.ShoreW(34f, 1.7f), CampusPreview.ShoreW(40f, 1.6f));
                p.outdoor = true; p.shore = true; pois.Add(p);
            }
            {
                var p = MakePoi("砂浜を散歩している", seaLook,
                    CampusPreview.ShoreW(4.5f, 4f), CampusPreview.ShoreW(14f, 3.5f), CampusPreview.ShoreW(19f, 4.2f), CampusPreview.ShoreW(29f, 3.2f), CampusPreview.ShoreW(36f, 4.5f), CampusPreview.ShoreW(44f, 3.8f));
                p.outdoor = true; p.shore = true; pois.Add(p);
            }

            // ---- 岩と流木（砂の上にかかる所） ----
            foreach (var r in new[] { new Vector3(1.5f, 1.2f, 1.4f), new Vector3(44.5f, 0.5f, 1.6f), new Vector3(3.5f, 2.6f, 0.9f), new Vector3(42f, 3.2f, 0.8f) })
            {
                var w = CampusPreview.ShoreW(r.x, r.y);
                blockers[0].Add(new Rect(w.x - r.z, w.z - r.z, r.z * 2f, r.z * 2f));
            }
        }
    }
}
