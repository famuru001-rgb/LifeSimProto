using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 置いた家具の効きめ（2026-11-15 famuru：案のまま・置いた部屋に来た子にも効く・使うと回復は生活の場面のときに）。
    /// 設置_品物.csv の「効きめ」の列を読む。「／」で区切って書く：
    ///   ・（ゲージ）の減り方×0.9 … そのゲージが減りにくい（ゲージ：心の満たされ・体力・楽しさ・満腹度・勉強・趣味・元気・恋心・性欲）
    ///   ・（ゲージ）の戻り×1.2 … 寝る・休む・食べるなどで戻るとき、よく戻る
    ///   ・性欲のたまり×1.2／恋心のたまり×1.2 … たまりやすい
    ///   ・精力の戻り×1.3／精力の最大＋20（篤）・好感度の伸び×1.2（その部屋で話したとき）・勉強の伸び×1.1
    ///   ・条件：夜だけ／昼だけ／二人きりのとき／満月の夜だけ（その行の効きめ全部にかかる）
    ///   ・「使うと」「来たとき」「泊まると」「置くと」で始まるものは、生活の場面（家具を使う）を作るときに入れる。今は説明に出すだけ
    /// 好きな物の列に合う子には、効きめが 1.5 倍（今の寮生の部屋の家具と同じ）。
    /// その部屋（区画）にいるあいだだけ、補正（NeedMod。id は "place:名前"）として付く。画面の「効いている物」に出る。
    /// </summary>
    public static partial class PlaceBook
    {
        public class Effect
        {
            public NeedMod mod;                 // ゲージ・勉強の伸び
            public float vigorRegen = 1f, vigorMaxAdd, affinity = 1f;
            public bool night, day, privateOnly, fullMoon;
            public string later = "";           // 使う・来たとき など（まだ入れていない）
            public bool Any { get { return mod != null || vigorRegen != 1f || vigorMaxAdd != 0f || affinity != 1f; } }
        }

        static readonly Dictionary<string, NeedKind> GaugeWords = new Dictionary<string, NeedKind>
        {
            { "心の満たされ", NeedKind.Lonely }, { "さみしさ", NeedKind.Lonely }, { "体力", NeedKind.Tired }, { "楽しさ", NeedKind.Bored },
            { "満腹度", NeedKind.Hunger }, { "勉強", NeedKind.Study }, { "趣味", NeedKind.Hobby }, { "元気", NeedKind.Sleepy },
            { "恋心", NeedKind.Love }, { "性欲", NeedKind.Desire },
        };

        static readonly Regex EffectRe = new Regex(@"^(.+?)の?(減り方|減り|戻り|たまり|伸び|最大)\s*([×xX＊\*＋\+])\s*([0-9.]+)$");

        public static Effect ParseEffect(string name, string text, List<string> errors)
        {
            var e = new Effect();
            if (string.IsNullOrEmpty(text)) return e;
            NeedMod m = null;
            var later = new List<string>();
            foreach (var raw in text.Split('／', '/'))
            {
                string t = raw.Trim().Replace("　", "");
                if (t.Length == 0) continue;
                if (t == "夜だけ") { e.night = true; continue; }
                if (t == "昼だけ") { e.day = true; continue; }
                if (t == "二人きりのとき") { e.privateOnly = true; continue; }
                if (t == "満月の夜だけ") { e.fullMoon = true; continue; }
                if (t.StartsWith("使うと") || t.StartsWith("来たとき") || t.StartsWith("泊まると") || t.StartsWith("置くと") || t.StartsWith("冬は")) { later.Add(t); continue; }
                var mm = EffectRe.Match(t);
                float v;
                if (!mm.Success || !float.TryParse(mm.Groups[4].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v))
                { if (errors != null) errors.Add(name + "：効きめ「" + t + "」が読めない"); continue; }
                string what = mm.Groups[1].Value, how = mm.Groups[2].Value;
                if (what == "精力" && how == "戻り") { e.vigorRegen = v; continue; }
                if (what == "精力" && how == "最大") { e.vigorMaxAdd = v; continue; }
                if (what == "好感度" && how == "伸び") { e.affinity = v; continue; }
                if ((what == "勉強" || what == "学力") && how == "伸び")
                {
                    if (m == null) m = new NeedMod("place:" + name, name, ModSource.Furniture);
                    m.Skill(v); continue;
                }
                NeedKind n;
                if (!GaugeWords.TryGetValue(what, out n)) { if (errors != null) errors.Add(name + "：効きめのゲージ「" + what + "」が分からない"); continue; }
                if (m == null) m = new NeedMod("place:" + name, name, ModSource.Furniture);
                if (how == "戻り") m.Relief(n, v); else m.Rate(n, v);   // 減り方・減り・たまり
            }
            if (m != null) m.Desc("置いた家具：" + text);
            e.mod = m;
            e.later = string.Join("・", later.ToArray());
            return e;
        }

        /// <summary>好きな物に合う子用：効きめを 1.5 倍（1 からの差を 1.5 倍）にした補正。</summary>
        public static NeedMod LikedMod(NeedMod m)
        {
            if (m == null) return null;
            NeedMod r;
            if (likedCache.TryGetValue(m.id, out r)) return r;
            r = new NeedMod(m.id + "♪", m.name + "♪", ModSource.Furniture).Desc(m.desc + "（好きな物なので 1.5 倍）");
            foreach (var kv in m.rate) r.Rate(kv.Key, Boost(kv.Value));
            foreach (var kv in m.relief) r.Relief(kv.Key, Boost(kv.Value));
            r.Skill(Boost(m.skillGrowth));
            likedCache[m.id] = r;
            return r;
        }
        static readonly Dictionary<string, NeedMod> likedCache = new Dictionary<string, NeedMod>();
        static float Boost(float v) { return 1f + (v - 1f) * 1.5f; }
    }

    public partial class LoungeWorld
    {
        float placeEffectTimer;

        static bool EveningNight(float m) { return m >= 1080f || m < 360f; }   // 18:00〜6:00

        /// <summary>毎コマ（Update）：0.5 秒ごとに、だれがどの家具の部屋にいるかを見て、効きめを付け外しする。</summary>
        void UpdatePlaceEffects()
        {
            placeEffectTimer -= Time.unscaledDeltaTime;
            if (placeEffectTimer > 0f) return;
            placeEffectTimer = 0.5f;
            var areaItems = new Dictionary<string, List<PlaceBook.Def>>();
            foreach (var p in PlaceBook.Placed)
            {
                var d = PlaceBook.Get(p.name);
                if (d == null || !d.effect.Any || p.area == null) continue;
                List<PlaceBook.Def> l;
                if (!areaItems.TryGetValue(p.area, out l)) areaItems[p.area] = l = new List<PlaceBook.Def>();
                if (!l.Contains(d)) l.Add(d);   // 同じ家具を2つ置いても効きめは1つ分
            }
            float min = MinuteOfDay;
            bool night = EveningNight(min);
            bool fullMoon = night && Calendar.IsFullMoon(Day);
            var want = new HashSet<string>();
            foreach (var c in characters)
            {
                if (c == null) continue;
                want.Clear();
                float vr = 1f, vm = 0f, af = 1f;
                List<PlaceBook.Def> here = null;
                if (c.Present && areaItems.Count > 0)
                {
                    var a = AreaAt(c.transform.position + Vector3.up * 0.3f);
                    if (a != null) areaItems.TryGetValue(a.name, out here);
                }
                if (here != null)
                    foreach (var d in here)
                    {
                        var e = d.effect;
                        if (e.night && !night) continue;
                        if (e.day && night) continue;
                        if (e.fullMoon && !fullMoon) continue;
                        if (e.privateOnly && !c.InPrivate) continue;
                        bool liked = d.Liked(c.profile);
                        if (e.mod != null)
                        {
                            var m = liked ? PlaceBook.LikedMod(e.mod) : e.mod;
                            want.Add(m.id);
                            if (!c.HasMod(m.id)) c.AddMod(m, 0f);
                        }
                        float k = liked ? 1.5f : 1f;
                        vr *= 1f + (e.vigorRegen - 1f) * k;
                        vm += e.vigorMaxAdd * k;
                        af *= 1f + (e.affinity - 1f) * k;
                    }
                c.RemovePlaceModsExcept(want);
                c.placeVigorRegen = vr;
                c.placeVigorMaxAdd = vm;
                c.placeAffinity = af;
            }
        }

        /// <summary>話したとき（ReportTalk）：家具の部屋なら、好感度の伸びを足す。</summary>
        void PlaceTalkBonus(SimCharacter a, SimCharacter b, TalkResult result)
        {
            float mul = Mathf.Max(a.placeAffinity, b.placeAffinity);
            if (mul <= 1f || result == TalkResult.Awkward) return;
            float baseD = result == TalkResult.Fun ? Relationships.FunDelta : Relationships.NormalDelta;
            rel.Add(a.id, b.id, baseD * (mul - 1f), simTime);
        }
    }

    public partial class SimCharacter
    {
        // 置いた家具の効きめ（Placement.Effects.cs）。その部屋にいるあいだだけ
        public float placeVigorRegen = 1f, placeVigorMaxAdd, placeAffinity = 1f;

        public bool HasMod(string id) { foreach (var a in mods) if (a.mod != null && a.mod.id == id) return true; return false; }

        public void RemovePlaceModsExcept(HashSet<string> keep)
        {
            mods.RemoveAll(a => a.mod != null && a.mod.id.StartsWith("place:") && !keep.Contains(a.mod.id));
        }
    }
}
