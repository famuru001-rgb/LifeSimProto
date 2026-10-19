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

            if (Calendar.IsHoliday(day, hol)) world.todayNotes.Add("お茶会 15:00");
            if (Calendar.IsHoliday(day, hol) && !Calendar.IsFestival(day)) world.todayNotes.Add("デートのチャンス 13:00");
            if (Calendar.IsExamDay(day, hol)) world.todayNotes.Add("試験（午前）・結果発表 18:00");
            if (Calendar.IsExamEve(day, hol))
            {
                world.todayNotes.Add("明日は試験");
                foreach (var c in world.characters) c.AddStatus(StatusKind.ExamEve, DayStart + 1440 - world.Now);
                world.Log("明日は試験。みんな少しそわそわしている");
            }
            if (Calendar.IsFestival(day))
            {
                string fn = Calendar.FestivalNames[Calendar.Season(day)];
                world.todayNotes.Add(fn + " 16:00");
                world.Log("今日は" + fn + "！");
            }
            foreach (var c in world.characters)
            {
                if (!Calendar.IsBirthday(c.id, day)) continue;
                c.AddStatus(StatusKind.Birthday, DayStart + 1440 - world.Now);
                world.todayNotes.Add(c.displayName + "の誕生日会 19:45");
                world.Log("今日は" + c.displayName + "の誕生日");
            }
            Weather w = Calendar.WeatherOf(day);
            if (w == Weather.Rain) world.Log("今日は雨。外には出にくい");
            if (w == Weather.Snow) world.Log("今日は雪。暖炉のまわりが人気");
            if (Calendar.Weekday(day) == 0) foreach (var c in world.characters) c.weekStudy = 0f;
        }

        void CheckTimed()
        {
            float m = world.MinuteOfDay;
            int day = world.Day;
            var hol = world.holidayWeekdays;
            bool holiday = Calendar.IsHoliday(day, hol);

            // ---- 寮の行事：お茶会 ----
            if (holiday && m >= 900f && m < 960f && Once("tea"))
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

            // ---- 試験の結果発表 ----
            if (Calendar.IsExamDay(day, hol) && m >= 1080f && Once("exam"))
            {
                var parts = new List<string>();
                foreach (var c in world.characters)
                {
                    float score = c.weekStudy * 0.5f + c.diligence * 30f + Random.Range(0f, 20f);
                    if (c.HasStatus(StatusKind.SleepDeprived)) score -= 10f;
                    string mark;
                    if (score >= 60f) { mark = "◎"; c.AddStatus(StatusKind.Cheerful, 480); }
                    else if (score < 30f) { mark = "×"; c.AddStatus(StatusKind.Down, 360); }
                    else mark = "○";
                    parts.Add(c.displayName + mark);
                }
                world.Log("試験の結果発表：" + string.Join("　", parts.ToArray()));
            }

            // ---- 季節のお祭り（みんなで出かける。Fと恋人は一緒に回る）----
            if (Calendar.IsFestival(day) && m >= 960f && m < 1140f && Once("festival"))
            {
                string fn = Calendar.FestivalNames[Calendar.Season(day)];
                world.gatherings.Add(new Gathering
                {
                    key = "festival", label = fn + "に出かけている", outing = true,
                    start = world.Now, end = DayStart + 1140, joinChance = 0.9f
                });
                world.Log(fn + "に出かける人が集まりはじめた");
            }

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
                        if (g.key == "festival" || g.key == "date" || g.key == "birthday") a.AddStatus(StatusKind.Cheerful, 360);
                        foreach (var b in list) if (a.id < b.id) world.rel.Add(a.id, b.id, g.key == "date" ? 6f : 2f, world.simTime);
                        if (g.honoree != null && a != g.honoree) world.rel.Add(a.id, g.honoree.id, 3f, world.simTime);
                    }
                    var f = world.RomanceTarget;
                    if (f != null && g.members.Contains(f))
                        foreach (var a in list) if (!a.IsTarget && a.LoveStage >= 1) world.ChangeLove(a, g.key == "date" ? 8f : 3f);
                    var names = new List<string>();
                    foreach (var a in list) names.Add(a.displayName);
                    world.Log(g.label.Replace("に参加している", "").Replace("に出かけている", "").Replace("に集まっている", "") + "が終わった（" + string.Join("・", names.ToArray()) + "）");
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
