using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 篤が誘うときに決める内容（2026-10-31 famuru「誘う時に部屋と内容を決められるようにしたい」）。
    /// null の項目は「おまかせ」。
    /// </summary>
    public class HPlan
    {
        public static readonly string[] Poses = { "正常位", "騎乗位", "後背位", "対面座位" };
        public static readonly string[] DepthNames = { "キスだけ", "愛撫まで", "最後まで" };
        public static readonly string[] FinishNames = { "おまかせ", "中に", "外に" };

        public string placeKey;     // 場所（null＝おまかせ）。SimCharacter.InvitePlaces の key
        public string placeName;    // 画面に出す名前
        public string pose;         // 体位（null＝おまかせ）
        public int depth = 2;       // 0 キスだけ・1 愛撫まで・2 最後まで
        public bool solo;           // 二人きり（ほかの子は加わらない）
        public int finish;          // 0 おまかせ（中）・1 中に・2 外に

        public string Summary()
        {
            var parts = new List<string>();
            parts.Add(pose ?? "体位はおまかせ");
            parts.Add(DepthNames[Mathf.Clamp(depth, 0, 2)]);
            parts.Add(solo ? "二人きり" : "加わってよい");
            if (depth >= 2 && finish > 0) parts.Add(FinishNames[finish]);
            return string.Join("・", parts.ToArray());
        }
    }

    /// <summary>誘うときに選べる場所1つ（画面の一覧用）。</summary>
    public class HPlaceInfo
    {
        public string key, name;
        public float dist;   // 相手からの距離（m）
        public int people;   // いま中にいる人の数（寮生・寮生以外の学生）
    }

    public partial class SimCharacter
    {
        HPlan invitePlan; // 篤が誘うときの内容（OrderInvite で受け取り、PrivateRoutine で使う）

        /// <summary>篤がエッチに誘いに行く（場所と内容つき。plan が null ならおまかせ）。</summary>
        public void OrderInvite(SimCharacter g, HPlan plan)
        {
            invitePlan = plan;
            OrderApproach(g, Intent.Private);
        }

        /// <summary>
        /// 誘うときに選べる場所（相手から近い順）。相手の部屋・篤の部屋・寮の開いている場所（温室・図書室・天文台・中庭）・学校の部屋
        /// （2026-10-31 famuru：保健室・更衣室・シャワー・空き教室・そのほかの部屋。人がいても使える）。
        /// </summary>
        public List<HPlaceInfo> InvitePlaces(SimCharacter girl)
        {
            var res = new List<HPlaceInfo>();
            if (girl == null) return res;
            Vector3 gp = girl.transform.position;
            System.Action<string, string, Vector3> add = (key, name, pos) =>
            {
                var d = pos - gp;
                float dist = new Vector2(d.x, d.z).magnitude + Mathf.Abs(d.y) * 3f;
                res.Add(new HPlaceInfo { key = key, name = name, dist = dist, people = world.PeopleIn(name, pos, girl) });
            };
            if (girl.room != null) add("room:" + girl.id, girl.room.AreaName + "（" + girl.displayName + "）", girl.room.bed.position);
            var f = world.RomanceTarget;
            if (f != null && f.room != null) add("room:" + f.id, f.room.AreaName, f.room.bed.position);
            foreach (var place in new[] { LoungeWorld.Greenhouse, LoungeWorld.Library, LoungeWorld.Observatory, LoungeWorld.Courtyard })
            {
                if (!world.PlaceOpen(place)) continue;
                var a = world.areaList.Find(x => x.name == place);
                if (a == null) continue;
                add("place:" + place, place, new Vector3(a.rect.center.x, LoungeWorld.FloorY(a.floor), a.rect.center.y));
            }
            var rooms = world.HSchoolRooms();
            for (int i = 0; i < rooms.Count; i++)
            {
                var a = rooms[i];
                add("school:" + i, world.SchoolRoomTitle(a), new Vector3(a.rect.center.x, LoungeWorld.FloorY(a.floor), a.rect.center.y));
            }
            res.Sort((x, y) => x.dist.CompareTo(y.dist));
            return res;
        }

        /// <summary>選んだ場所の二人きりの時間の場所を作る（使えなければ null）。</summary>
        PrivateSpot BuildPrivateSpot(string key, List<SimCharacter> group)
        {
            if (string.IsNullOrEmpty(key)) return null;
            int n = group.Count;
            if (key.StartsWith("room:"))
            {
                int cid;
                if (!int.TryParse(key.Substring(5), out cid) || cid < 0 || cid >= world.characters.Count) return null;
                var r = world.characters[cid].room;
                if (r == null) return null;
                var ps = new PrivateSpot { name = r.AreaName, room = r };
                ps.walkTo.AddRange(new[] { r.bed.approach, r.futon.approach, r.cushions[0].approach, r.cushions[1].approach });
                return ps;
            }
            if (key.StartsWith("place:"))
            {
                string place = key.Substring(6);
                var free = world.seats.FindAll(x => x.area == place && x.kind == SeatKind.Sit && x.occupant == null);
                if (free.Count >= n)
                {
                    var first = free[Random.Range(0, free.Count)];
                    free.Sort((x, y) => (x.position - first.position).sqrMagnitude.CompareTo((y.position - first.position).sqrMagnitude));
                    var ps = new PrivateSpot { name = place };
                    for (int i = 0; i < n; i++) { ps.seatList.Add(free[i]); ps.walkTo.Add(free[i].approach); }
                    return ps;
                }
                var a = world.areaList.Find(x => x.name == place);
                return a != null ? AreaSpot(a, place, n) : null;
            }
            if (key.StartsWith("school:"))
            {
                int idx;
                var rooms = world.HSchoolRooms();
                if (!int.TryParse(key.Substring(7), out idx) || idx < 0 || idx >= rooms.Count) return null;
                return AreaSpot(rooms[idx], world.SchoolRoomTitle(rooms[idx]), n);
            }
            return null;
        }

        /// <summary>部屋の真ん中あたりの、歩ける所に並んで床に座る場所（学校の部屋など、決まった席のない所）。</summary>
        PrivateSpot AreaSpot(AreaInfo a, string name, int n)
        {
            var nav = world.Nav(a.floor);
            float y = LoungeWorld.FloorY(a.floor);
            Vector3 c = nav.NearestFreePoint(new Vector3(a.rect.center.x, y, a.rect.center.y));
            if (!a.rect.Contains(new Vector2(c.x, c.z))) return null;
            // まわりの歩ける点（0.25m おき）を真ん中から近い順に見て、0.7m 以上離れた点を選ぶ
            var cands = new List<Vector3>();
            for (float dx = -3.5f; dx <= 3.51f; dx += 0.35f)
                for (float dz = -3.5f; dz <= 3.51f; dz += 0.35f)
                {
                    var p = new Vector3(c.x + dx, y, c.z + dz);
                    if (!a.rect.Contains(new Vector2(p.x, p.z)) || !nav.IsFree(p)) continue;
                    cands.Add(p);
                }
            cands.Sort((p, q) => (p - c).sqrMagnitude.CompareTo((q - c).sqrMagnitude));
            var picked = new List<Vector3>();
            int want = Mathf.Max(n, 2) + 4; // 途中で加わる子の分も
            foreach (var p in cands)
            {
                bool ok = true;
                foreach (var q in picked) if ((p - q).sqrMagnitude < 0.49f) { ok = false; break; }
                if (!ok) continue;
                picked.Add(p);
                if (picked.Count >= want) break;
            }
            if (picked.Count == 0) picked.Add(c);
            Vector3 mid = Vector3.zero;
            foreach (var p in picked) mid += p;
            mid /= picked.Count;
            var ps = new PrivateSpot { name = name };
            foreach (var p in picked)
            {
                Vector3 face = mid - p; face.y = 0f;
                if (face.sqrMagnitude < 0.01f) face = Vector3.forward;
                ps.points.Add(new PrivateSpot.Place { pos = p, facing = face.normalized, floor = true });
                ps.walkTo.Add(p);
            }
            return ps;
        }

        /// <summary>おまかせのとき、学校にいるなら学校のいちばん近い部屋も候補にする。</summary>
        PrivateSpot NearestSchoolSpot(SimCharacter girl, int n)
        {
            if (!LoungeWorld.InSchool(girl.transform.position)) return null;
            var rooms = world.HSchoolRooms();
            AreaInfo best = null; float bd = float.MaxValue;
            Vector3 gp = girl.transform.position;
            foreach (var a in rooms)
            {
                if (a.floor != LoungeWorld.FloorOf(gp.y)) continue;
                float d = (new Vector2(a.rect.center.x, a.rect.center.y) - new Vector2(gp.x, gp.z)).magnitude;
                if (d < bd) { bd = d; best = a; }
            }
            return best != null ? AreaSpot(best, world.SchoolRoomTitle(best), n) : null;
        }
    }

    public partial class LoungeWorld
    {
        List<AreaInfo> hSchoolRooms;

        /// <summary>学校の本館の区画か（寮より南）。</summary>
        public static bool InSchool(Vector3 p) { return p.z <= -24.4f; }

        /// <summary>二人きりの時間に使える学校の部屋（廊下・階段・通路・玄関・中庭は除く）。</summary>
        public List<AreaInfo> HSchoolRooms()
        {
            if (hSchoolRooms != null) return hSchoolRooms;
            hSchoolRooms = new List<AreaInfo>();
            foreach (var a in areaList)
            {
                if (a.rect.yMax > -24.3f) continue;
                string n = a.name;
                if (n.EndsWith("大廊下") || n.EndsWith("階段") || n.EndsWith("通路") || n == "外廊下" || n == "玄関ホール" || n == "本館の中庭" || n == "前庭") continue;
                hSchoolRooms.Add(a);
            }
            return hSchoolRooms;
        }

        Dictionary<AreaInfo, string> hSchoolTitles;

        /// <summary>学校の部屋の表示名（同じ名前の部屋があるので階を付け、同じ階に同じ名前が2つ以上あれば「西から何番目」を付ける）。</summary>
        public string SchoolRoomTitle(AreaInfo a)
        {
            if (hSchoolTitles == null)
            {
                hSchoolTitles = new Dictionary<AreaInfo, string>();
                var rooms = HSchoolRooms();
                foreach (var r in rooms)
                {
                    string t = BaseSchoolTitle(r);
                    var same = rooms.FindAll(x => BaseSchoolTitle(x) == t);
                    if (same.Count > 1)
                    {
                        same.Sort((x, y) => y.rect.center.y.CompareTo(x.rect.center.y)); // 西（z が大きい）から
                        t += "（西から" + (same.IndexOf(r) + 1) + "つ目）";
                    }
                    hSchoolTitles[r] = t;
                }
            }
            string s;
            return hSchoolTitles.TryGetValue(a, out s) ? s : BaseSchoolTitle(a);
        }

        static string BaseSchoolTitle(AreaInfo a)
        {
            if (a.name.StartsWith("本館")) return a.name;
            if (a.name == "体育館" || a.name == "大講堂" || a.name.EndsWith("の2階席")) return a.name;
            return a.name + "（本館" + (a.floor + 1) + "階）";
        }

        /// <summary>その場所にいま何人いるか（篤と、誘う相手は数えない）。</summary>
        public int PeopleIn(string placeName, Vector3 pos, SimCharacter except = null)
        {
            var a = AreaAt(pos);
            if (a == null) return 0;
            int n = 0;
            foreach (var c in characters)
            {
                if (c.IsTarget || c == except || !c.Present) continue;
                if (FloorOf(c.transform.position.y) != a.floor) continue;
                if (a.rect.Contains(new Vector2(c.transform.position.x, c.transform.position.z))) n++;
            }
            return n;
        }
    }
}
