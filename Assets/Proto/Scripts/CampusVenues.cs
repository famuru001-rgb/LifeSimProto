using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 外出をなくす（2026-11-07 famuru「今、正門から外出する仕組みになっているが、基本的に外出はなしにして」）。
    /// famuru の決定：正門から街へ出るもの（デート・街のアルバイト・できごとのあとの買い出し・行事の外出）は全部なくす。
    /// 行事は学園の敷地の中で行う（学園祭・夏祭りも。湖の野外実習は森と新しく作った浜辺）。
    /// 会場の区画が無いとき（まだ作っていない浜辺など）は、次の候補の区画を使う。
    /// </summary>
    public partial class LoungeWorld
    {
        /// <summary>false：正門から街へ出かけない（デート・アルバイト・買い出し）。true にすると前のとおり。</summary>
        public static bool OutingsEnabled = false;

        /// <summary>名前の区画（「本館1階・〜」のような前つきも。1階を先に）。</summary>
        public AreaInfo FindArea(string name)
        {
            AreaInfo hit = null;
            foreach (var a in areaList)
            {
                if (a == null || !(a.name == name || a.name.EndsWith(name) || a.name.EndsWith("・" + name))) continue;
                if (hit == null || (a.floor == 0 && hit.floor != 0)) hit = a;
                if (a.name == name && a.floor == 0) return a;
            }
            return hit;
        }

        /// <summary>区画の真ん中（歩ける所）。</summary>
        public Vector3 AreaCenter(AreaInfo a)
        {
            return NearestFree(new Vector3(a.rect.center.x, a.floor * FloorH, a.rect.center.y));
        }

        /// <summary>行事を敷地の中の会場で行う。names は会場の候補（前から順に、ある区画を使う）。altNames があれば、参加者を2か所に分ける（野外実習の森と浜辺）。</summary>
        public bool PlaceOnCampus(Gathering g, string[] names, string[] altNames = null)
        {
            AreaInfo a = null;
            foreach (var n in names) { a = FindArea(n); if (a != null) break; }
            if (a == null) return false;
            g.outing = false;
            g.area = a.name;
            g.focus = AreaCenter(a);
            g.spread = Mathf.Clamp(Mathf.Min(a.rect.width, a.rect.height) * 0.35f, 2.2f, 9f);
            if (altNames != null)
                foreach (var n in altNames)
                {
                    var b = FindArea(n);
                    if (b != null && b != a) { g.altArea = b.name; g.altFocus = AreaCenter(b); g.altSpread = Mathf.Clamp(Mathf.Min(b.rect.width, b.rect.height) * 0.35f, 2.2f, 9f); break; }
                }
            return true;
        }

        /// <summary>行事ごとの、敷地の中の会場の候補。</summary>
        public static string[] CampusVenueOf(string key, out string[] alt)
        {
            alt = null;
            switch (key)
            {
                case "entrance": return new[] { "大講堂", "本館の中庭" };
                case "planting": return new[] { "本館の中庭", "前庭", "中庭" };
                case "lake": alt = new[] { "森", "本館の中庭" }; return new[] { "浜辺", "森", "本館の中庭" };   // 野外実習：森と浜辺に分かれる
                case "race": return new[] { "浜辺", "本館の中庭", "体育館" };
                case "schoolfest": return new[] { "本館の中庭", "前庭" };
                case "contest": return new[] { "体育館", "本館の中庭" };
                case "stars": return new[] { "屋上", "前庭" };
                case "waterplay": return new[] { "浜辺", "プール", "前庭" };
                case "summerfest": return new[] { "前庭", "本館の中庭" };
                case "tools": return new[] { "大講堂", "魔道具学の教室" };
                default: return new[] { "本館の中庭", "前庭" };
            }
        }
    }
}
