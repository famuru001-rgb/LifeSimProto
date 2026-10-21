using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// イベントを起こす部品。
    /// 寮の行事：お茶会（休日15時）・誕生日会（19:45）・試験（土の日の午前）と結果発表（18時）・季節のお祭り（季節の最後の日16時）
    /// 偶然：差し入れ・魔法灯の停電・迷い込んだ使い魔
    /// 恋愛：デート（休日13時）・手作りお菓子
    /// 天気：雨や雪の日は散歩に出ない（キャラ側で判断）
    /// </summary>
    public class EventDirector : MonoBehaviour
    {
        LoungeWorld world;
        int lastDay = -1;
        readonly HashSet<string> fired = new HashSet<string>();

        // その日の偶然の出来事の時刻（-1 は起きない）
        float snackTime = -1, blackoutTime = -1, creatureTime = -1;
        readonly Dictionary<SimCharacter, float> bakeTimes = new Dictionary<SimCharacter, float>();
        Creature creature;

        void Start()
        {
            world = GetComponent<LoungeWorld>();
        }

        void Update()
        {
            if (world == null || world.characters.Count == 0) return;
            if (world.Day != lastDay) OnNewDay();
            CheckTimed();
            ProcessEndedGatherings();
        }

        bool Once(string key)
        {
            if (fired.Contains(key)) return false;
            fired.Add(key);
            return true;
        }

        double DayStart { get { return ScheduleMaker.DayStart(world.Day); } }

        void OnNewDay()
        {
            lastDay = world.Day;
            fired.Clear();
            world.todayNotes.Clear();
            int day = world.Day;
            var hol = world.holidayWeekdays;
            world.Log("―― " + day + "日目　" + Calendar.DateText(day, hol) + " ――");

            var rnd = new System.Random(day * 9973 + 5);
            snackTime = rnd.NextDouble() < 0.3 ? 15.5f * 60f : -1f;
            blackoutTime = rnd.NextDouble() < 0.15 ? 20.25f * 60f : -1f;
            creatureTime = rnd.NextDouble() < 0.25 ? (10f + (float)rnd.NextDouble() * 9f) * 60f : -1f;
            bakeTimes.Clear();
            foreach (var c in world.characters)
                if (!c.IsTarget && c.LoveStage >= 2 && rnd.NextDouble() < 0.25) bakeTimes[c] = (16.5f + (float)rnd.NextDouble()) * 60f;

            if (Calendar.IsHoliday(day, hol) && !Calendar.IsFestival(day)) world.todayNotes.Add("お茶会 15:00");
            if (Calendar.IsHoliday(day, hol) && !Calendar.IsFestival(day)) world.todayNotes.Add("デートのチャンス 13:00");
            if (Calendar.IsExamDay(day, hol)) world.todayNotes.Add("試験（午前）・結果発表 18:00");
            if (Calendar.IsExamEve(day, hol))
            {
                world.todayNotes.Add("明日は試験（みんな勉強ぎみ）");
                world.Log("明日は試験。みんな少しそわそわしている");
            }
            if (Calendar.Weekday(day) == 5) world.todayNotes.Add("課題が出る 9:00（光の日 23:00 まで）");
            if (Calendar.IsClassDay(day, hol)) world.todayNotes.Add("授業の日");
            if (Calendar.IsFullMoon(day)) world.todayNotes.Add("満月の夜");
            foreach (var c in world.characters)
            {
                if (!Calendar.IsBirthday(c.id, day)) continue;
                c.PositiveEvent(1f);
                world.todayNotes.Add(c.displayName + "の誕生日会 19:45");
                world.Log("今日は" + c.displayName + "の誕生日");
                Loop.Learn(world, c, "誕生日", Calendar.SeasonNames[Calendar.Season(day)] + "の" + Calendar.WeekdayNames[Calendar.Weekday(day)]);
            }
            Weather w = Calendar.WeatherOf(day);
            if (w == Weather.Rain) world.Log("今日は雨。外には出にくい");
            if (w == Weather.Snow) world.Log("今日は雪。暖炉のまわりが人気");
            if (Calendar.Weekday(day) == 0)
                foreach (var c in world.characters) { c.weekStudy = 0f; for (int s = 0; s < Academics.Count; s++) c.weekSubject[s] = 0f; }
        }

        void CheckTimed()
        {
            float m = world.MinuteOfDay;
            int day = world.Day;
            var hol = world.holidayWeekdays;
            bool holiday = Calendar.IsHoliday(day, hol);

            // ---- 寮の行事：お茶会 ----
            if (holiday && !Calendar.IsFestival(day) && m >= 900f && m < 960f && Once("tea"))
            {
                world.gatherings.Add(new Gathering
                {
                    key = "tea", label = "お茶会に参加している", area = "談話室", focus = new Vector3(0f, 0f, 0.5f),
                    start = world.Now, end = world.Now + 60, joinChance = 0.85f
                });
                world.Log("談話室でお茶会が始まった");
            }

            // ---- 誕生日会 ----
            if (m >= 1185f && m < 1245f && Once("birthday"))
            {
                foreach (var c in world.characters)
                {
                    if (!Calendar.IsBirthday(c.id, day)) continue;
                    world.gatherings.Add(new Gathering
                    {
                        key = "birthday", label = c.displayName + "の誕生日会", area = "談話室", focus = new Vector3(0f, 0f, 3.0f),
                        start = world.Now, end = world.Now + 60, joinChance = 0.9f, honoree = c
                    });
                    world.Log("★ " + c.displayName + "の誕生日会が始まった！");
                    break;
                }
            }

            // ---- 課題（全員に出る。学力と進み具合で行動が変わる）----
            if (Calendar.Weekday(day) == 5 && m >= 540f && Once("assign"))
            {
                double deadline = ScheduleMaker.DayStart(day + 2) + 23 * 60;
                foreach (var c in world.characters) c.GiveAssignment(deadline);
                world.Log("課題が出た！（光の日 23:00 まで）");
            }

            // ---- 試験の結果発表（取っている科目ごと。1位はご機嫌・篤は成績で奨学金）----
            if (Calendar.IsExamDay(day, hol) && m >= 1080f && Once("exam")) AnnounceExam(day);

            // ---- デート（休日の午後。Fと、Fを好きな子のだれか1人）----
            if (holiday && !Calendar.IsFestival(day) && m >= 780f && m < 900f && Once("date"))
            {
                var f = world.RomanceTarget;
                SimCharacter best = null;
                float bestScore = 0f;
                if (f != null && f.AvailableForTalk)
                {
                    foreach (var c in world.characters)
                    {
                        if (c.IsTarget || c.LoveStage < 2 || !c.AvailableForTalk) continue;
                        float s = world.romance.love[c.id] + (c.IsLover ? 30f : 0f) + Random.Range(0f, 20f);
                        if (s > bestScore) { bestScore = s; best = c; }
                    }
                }
                if (best != null)
                {
                    string place = world.BadWeatherToday ? "街のカフェ" : "湖のほとり";
                    world.gatherings.Add(new Gathering
                    {
                        key = "date", label = place + "へデートに出かけている", outing = true,
                        start = world.Now, end = world.Now + 150, joinChance = 1f,
                        invited = new List<SimCharacter> { f, best }
                    });
                    world.Log("♥ " + best.displayName + "と" + f.displayName + "が" + place + "へデートに出かけるらしい");
                }
            }

            // ---- 偶然：差し入れ ----
            if (snackTime > 0 && m >= snackTime && Once("snack"))
            {
                world.snackBonusUntil = world.Now + 90;
                world.Log("寮母さんから差し入れが届いた（談話室のおやつワゴン）");
            }

            // ---- 偶然：魔法灯の停電 ----
            if (blackoutTime > 0 && m >= blackoutTime && Once("blackout"))
            {
                world.blackoutUntil = world.Now + 60;
                world.gatherings.Add(new Gathering
                {
                    key = "blackout", label = "暖炉のまわりに集まっている", area = "談話室", focus = new Vector3(0f, 0f, 3.2f),
                    start = world.Now, end = world.Now + 60, joinChance = 0.7f
                });
                world.Log("魔法灯がいっせいに消えた！ 暖炉の火だけが明るい");
            }
            if (blackoutTime > 0 && fired.Contains("blackout") && !world.Blackout && Once("blackoutEnd"))
                world.Log("魔法灯が戻った");

            // ---- 偶然：迷い込んだ使い魔 ----
            if (creatureTime > 0 && m >= creatureTime && Once("creature"))
            {
                var go = new GameObject("StrayFamiliar");
                creature = go.AddComponent<Creature>();
                creature.Init(world, world.Now + 90);
                world.Log("小さな使い魔が談話室に迷い込んできた");
            }
            if (creature != null && creature.Finished)
            {
                creature.Remove();
                creature = null;
                world.Log("使い魔は主人のもとへ帰っていった");
            }

            // ---- 恋愛：手作りお菓子 ----
            foreach (var kv in bakeTimes)
            {
                if (m < kv.Value || !Once("bake" + kv.Key.id)) continue;
                if (kv.Key.StartBaking()) world.Log(kv.Key.displayName + "が食堂でお菓子を作りはじめた");
            }
        }

        void AnnounceExam(int day)
        {
            Academics.ClearExam();
            Academics.lastExamDay = day;
            var avg = new Dictionary<SimCharacter, float>();
            foreach (var c in world.characters)
            {
                if (c.profile == null || c.profile.enrolled.Length == 0) continue;
                float sum = 0f;
                foreach (int s in c.profile.enrolled)
                {
                    float score = c.skill[s] * 0.8f + Mathf.Min(25f, c.weekSubject[s] * 0.4f) + Random.Range(0f, 15f) - c.GetStage(StatusLine.Sleep) * 5f
                        + (c.Ab(Abilities.ForSubject(s)) - 50f) * 0.1f; // 勉学は知力、実技は魔力（体術は体力）
                    score = Mathf.Clamp(score, 0f, 100f);
                    Academics.LastExam[s].Add(new Academics.ExamEntry { who = c, score = score, mark = Academics.Mark(score) });
                    sum += score;
                }
                avg[c] = sum / c.profile.enrolled.Length;
            }
            foreach (var l in Academics.LastExam) l.Sort((x, y) => y.score.CompareTo(x.score));
            foreach (var kv in avg) Academics.LastOverall.Add(kv);
            Academics.LastOverall.Sort((x, y) => y.Value.CompareTo(x.Value));

            // ご機嫌・落ち込み（平均の点）と、篤の奨学金（科目ごとの印）
            var parts = new List<string>();
            foreach (var kv in Academics.LastOverall)
            {
                var c = kv.Key;
                float v = kv.Value;
                string mark = Academics.Mark(v);
                if (v >= 55f) { c.PositiveEvent(0.8f); c.ShowBubble(c.Say("examGood", "やった！"), 2f); }
                else if (v < 32f) { c.NegativeEvent(0.8f); c.ShowBubble(c.Say("examBad", "うぅ…"), 2f); }
                parts.Add(c.displayName + mark);
                if (c.IsTarget && world.player != null)
                {
                    int money = 0;
                    for (int s = 0; s < Academics.Count; s++)
                        foreach (var e in Academics.LastExam[s])
                            if (e.who == c) money += e.mark == "◎" ? 25 : (e.mark == "○" ? 12 : (e.mark == "△" ? 5 : 0));
                    world.player.Earn(money, "奨学金（試験の成績）");
                }
            }
            bool yearEnd = Calendar.Season(day) == 3;
            world.Log((yearEnd ? "年度末の試験の結果発表：" : "試験の結果発表：") + string.Join("　", parts.ToArray()) + "（G キーで成績表）");
            float all = 0f;
            foreach (var kv in Academics.LastOverall) all += kv.Value;
            if (Academics.LastOverall.Count > 0)
            {
                int rep = Mathf.RoundToInt(all / Academics.LastOverall.Count / (yearEnd ? 8f : 20f));
                world.dormReputation += rep;
                if (yearEnd) Album.Add(world, "年度末の試験。総合1位は" + Academics.LastOverall[0].Key.displayName);
            }

            // 科目ごとの1位（同じ子は1回だけご機嫌に）
            var top = new HashSet<SimCharacter>();
            var tops = new List<string>();
            for (int s = 0; s < Academics.Count; s++)
            {
                var l = Academics.LastExam[s];
                if (l.Count < 2) continue;
                var best = l[0];
                tops.Add(Academics.Names[s] + "：" + best.who.displayName);
                if (best.mark == "◎") best.who.AddSkill(s, 1f);
                if (top.Add(best.who)) { best.who.PositiveEvent(0.6f); best.who.ShowBubble(best.who.Say("rank1", "一番！"), 2.2f); }
            }
            if (tops.Count > 0) world.Log("★ 科目の1位　" + string.Join("／", tops.ToArray()));
        }

        /// <summary>終わった集まりの効果（ご機嫌・仲の良さ・恋の気持ち）。</summary>
        void ProcessEndedGatherings()
        {
            for (int i = world.gatherings.Count - 1; i >= 0; i--)
            {
                var g = world.gatherings[i];
                if (world.Now < g.end || g.processed) continue;
                g.processed = true;
                var list = new List<SimCharacter>(g.members);
                if (list.Count > 0)
                {
                    foreach (var a in list)
                    {
                        if (g.key == "festival" || g.key == "date" || g.key == "birthday") a.PositiveEvent(0.8f);
                        foreach (var b in list) if (a.id < b.id) world.rel.Add(a.id, b.id, g.key == "date" ? 6f : 2f, world.simTime);
                        if (g.honoree != null && a != g.honoree) world.rel.Add(a.id, g.honoree.id, 3f, world.simTime);
                    }
                    var f = world.RomanceTarget;
                    if (f != null && g.members.Contains(f))
                        foreach (var a in list) if (!a.IsTarget && a.LoveStage >= 1) world.ChangeLove(a, g.key == "date" ? 8f : 3f);
                    var names = new List<string>();
                    foreach (var a in list) names.Add(a.displayName);
                    if (!g.key.StartsWith("year_")) world.Log(g.label.Replace("に参加している", "").Replace("に出かけている", "").Replace("に集まっている", "") + "が終わった（" + string.Join("・", names.ToArray()) + "）");
                }
                if (world.Now > g.end + 120) world.gatherings.RemoveAt(i);
            }
        }
    }

    /// <summary>迷い込んだ使い魔。談話室をうろうろし、まわりにキャラが集まる。</summary>
    public class Creature : MonoBehaviour
    {
        LoungeWorld world;
        double until;
        Poi poi;
        readonly List<Vector3> path = new List<Vector3>();
        float wait;
        public bool Finished { get { return world != null && world.Now >= until; } }

        public void Init(LoungeWorld w, double endTime)
        {
            world = w;
            until = endTime;
            transform.position = world.NearestFree(new Vector3(0f, 0f, -2.5f));
            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.transform.SetParent(transform, false);
            body.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            body.transform.localScale = new Vector3(0.45f, 0.4f, 0.5f);
            body.GetComponent<Renderer>().sharedMaterial = ProtoArt.Mat(new Color(0.95f, 0.9f, 0.8f), ProtoArt.Fabric, new Vector2(2f, 2f), 0.2f);
            Destroy(body.GetComponent<Collider>());
            for (int i = -1; i <= 1; i += 2)
            {
                var ear = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ear.transform.SetParent(transform, false);
                ear.transform.localPosition = new Vector3(0.12f * i, 0.5f, 0.05f);
                ear.transform.localScale = new Vector3(0.08f, 0.18f, 0.05f);
                ear.GetComponent<Renderer>().sharedMaterial = ProtoArt.Mat(new Color(0.9f, 0.6f, 0.6f));
                Destroy(ear.GetComponent<Collider>());
            }
            var glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            glow.transform.SetParent(transform, false);
            glow.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            glow.transform.localScale = Vector3.one * 0.08f;
            glow.GetComponent<Renderer>().sharedMaterial = ProtoArt.Glow(new Color(0.6f, 0.8f, 1f), 2f);
            Destroy(glow.GetComponent<Collider>());

            poi = new Poi { label = "使い魔と遊んでいる", dynamicSpot = true, slots = new Vector3[3], occupants = new SimCharacter[3] };
            UpdateSlots();
            world.pois.Add(poi);
        }

        void UpdateSlots()
        {
            Vector3 p = transform.position;
            poi.lookAt = p + Vector3.up * 0.3f;
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2f / 3f;
                poi.slots[i] = world.NearestFree(p + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.9f);
            }
        }

        void Update()
        {
            if (world == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            bool watched = false;
            foreach (var o in poi.occupants) if (o != null) watched = true;
            if (path.Count == 0)
            {
                wait -= dt;
                if (wait <= 0f && !watched)
                {
                    var p = world.nav0.RandomFreePoint(new Rect(-5f, -4f, 10f, 7f));
                    var np = world.nav0.FindPath(transform.position, p);
                    if (np != null) path.AddRange(np);
                    wait = Random.Range(3f, 8f);
                }
            }
            else if (!watched)
            {
                Vector3 to = path[0] - transform.position;
                float step = 0.8f * dt;
                if (to.magnitude <= step) { transform.position = path[0]; path.RemoveAt(0); }
                else
                {
                    transform.position += to.normalized * step;
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(new Vector3(to.x, 0f, to.z)), dt * 6f);
                }
            }
            if (!watched) UpdateSlots();
        }

        public void Remove()
        {
            if (poi != null)
            {
                world.pois.Remove(poi);
            }
            Destroy(gameObject);
        }
    }
}
