using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// ダンジョン実習（2026-11-01 famuru：探索の画面＋キャラが動いている姿）。
    /// ・学園の「ダンジョン実習の準備室」の魔法陣から、地下の実習用ダンジョンへ（ゲームでは東の離れた所に1つ組み立ててある）
    /// ・1つの階は部屋5つ（入口・出来事の部屋3つ・下への階段）。部屋に着くたびに出来事（魔物・罠・宝箱・泉）がある。最下層は5階
    /// ・篤の組は、出来事ごとにどうするかを画面で選ぶ。寮生だけの組・ほかの寮の学生の組は自分たちで決める
    /// ・結果は能力値（体力・魔力・器用さ・知力）と科目（剣術・魔法術・ダンジョン実習・魔物学・魔道具学）で決まる
    /// ・戦利品（銀貨・装飾品）は篤の組だけ。どの組も寮杯の点（進んだ階・倒した魔物・開けた宝箱）が入り、科目と能力が伸びる
    /// ・ダンジョンの場所は1つなので、使えるのは1組ずつ。ほかの組は見えない所で進む（結果だけ）。篤の組がいちばん先
    /// ・事前研修（2026-11-01 famuru）：新しい学園では、篤が森の「訓練の施設」で事前研修（ダンジョンを再現した場所。1階・部屋3つ＝魔物・罠・宝箱、
    ///   魔物は幻でけがはしない・戦利品と寮杯の点はなし）を終えると、ダンジョン実習に出発できる。研修を終えたことはループで戻る（セーブには残る）
    /// </summary>
    public class DungeonRun
    {
        public enum Phase { Gather, Walk, Event, Choice, Act, Done }
        public enum Ev { None, Monster, Trap, Chest, Spring, Stairs, Altar, TrainGoal }

        public readonly List<SimCharacter> members = new List<SimCharacter>();
        public bool withF;          // 篤の組（出来事を画面で選ぶ）
        public bool training;       // 訓練の施設の事前研修（ダンジョンの再現。2026-11-01）
        public int dorm;            // 寮杯の点が入る寮
        public bool onStage;        // ダンジョンの場所にいる（見える）
        public int floor = 1, room;
        public int targetFloor = 3; // 自分たちで決める組の目標の階
        public float hp, maxHp;
        public int silver, chests, monsters, floorsCleared;
        public readonly List<ItemKind> items = new List<ItemKind>();
        public readonly List<string> log = new List<string>();
        public Phase phase;
        public double until, started;
        public Ev ev;
        public int evCount; public float evStr; public bool evMimic, evInspected, evFought;
        public string evText;
        public string[] choices = new string[0];
        public int picked = -1;
        public int act;             // 選んだ行動
        public string title;

        public void Log(string s) { log.Add(s); if (log.Count > 40) log.RemoveAt(0); }

        public string MemberNames()
        {
            var n = new List<string>();
            foreach (var m in members) n.Add(m.displayName);
            return string.Join("・", n.ToArray());
        }
    }

    public class DungeonDirector : MonoBehaviour
    {
        public static DungeonDirector Instance;
        LoungeWorld world;
        public readonly List<DungeonRun> runs = new List<DungeonRun>();
        public DungeonRun stageRun;           // ダンジョンの場所を使っている組
        /// <summary>篤が誘っている組（出発前）。</summary>
        public readonly List<SimCharacter> fPicks = new List<SimCharacter>();
        public const int MaxFloor = 5, MaxPicks = 3;
        public static int BestFloor;          // 篤の組がたどり着いたいちばん深い階（ループをまたいで残る）
        public static bool TrainingDone;      // 篤が訓練の施設の事前研修を終えた（ループで戻る。2026-11-01）
        /// <summary>ダンジョン実習の前に事前研修が要るか（訓練の施設がある新しい学園だけ）。</summary>
        public static bool NeedTraining { get { return LoungeWorld.NewLayout && !TrainingDone; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetAll() { BestFloor = 0; TrainingDone = false; }

        void Start() { Instance = this; world = GetComponent<LoungeWorld>(); }

        public DungeonRun FRun { get { foreach (var r in runs) if (r.withF) return r; return null; } }

        double nextAutoCheck;

        void Update()
        {
            if (world == null || world.characters.Count == 0) return;
            for (int i = runs.Count - 1; i >= 0; i--) Tick(runs[i]);
            runs.RemoveAll(r => r.phase == DungeonRun.Phase.Done);
            if (world.Now >= nextAutoCheck) { nextAutoCheck = world.Now + 30; AutoParties(); }
        }

        // ================= 組を作る =================
        public bool CanStartNow(out string why)
        {
            float m = world.MinuteOfDay;
            why = null;
            if (m < 8f * 60f || m > 16.5f * 60f) { why = "ダンジョン実習の準備室が開いているのは 8:00〜16:30（出発）"; return false; }
            if (FRun != null) { why = "篤はもう出かけている"; return false; }
            if (NeedTraining) { why = "先に訓練の施設で事前研修を終えると、ダンジョン実習に出発できる"; return false; }
            return true;
        }

        /// <summary>事前研修に出発できるか（訓練の施設：8:00〜16:30）。</summary>
        public bool CanStartTraining(out string why)
        {
            float m = world.MinuteOfDay;
            why = null;
            if (!LoungeWorld.NewLayout) { why = "訓練の施設は新しい学園にある"; return false; }
            if (m < 8f * 60f || m > 16.5f * 60f) { why = "訓練の施設が開いているのは 8:00〜16:30（出発）"; return false; }
            if (FRun != null) { why = "篤はもう出かけている"; return false; }
            return true;
        }

        public void TogglePick(SimCharacter c)
        {
            if (c == null || c.IsTarget || c.IsTeacher) return;
            if (fPicks.Contains(c)) { fPicks.Remove(c); world.Log(c.displayName + "をダンジョン実習の組から外した"); return; }
            if (fPicks.Count >= MaxPicks) { world.Log("ダンジョン実習の組は篤のほかに3人まで"); return; }
            fPicks.Add(c);
            world.Log(c.displayName + "をダンジョン実習の組に入れた（" + fPicks.Count + "人）");
        }

        /// <summary>篤の組で出発（画面のボタン）。training＝訓練の施設の事前研修。</summary>
        public void StartF() { StartF(false); }

        public void StartF(bool training)
        {
            var f = world.RomanceTarget;
            string why;
            bool ok = training ? CanStartTraining(out why) : CanStartNow(out why);
            if (f == null || !ok) { if (f != null) world.Log(why ?? "今は出発できない"); return; }
            var r = new DungeonRun { withF = true, dorm = 0, title = training ? "篤の組（事前研修）" : "篤の組", training = training };
            if (!f.CanJoinDungeon()) { world.Log("篤は今は出かけられない（" + f.StateName() + "）"); return; }
            r.members.Add(f);
            foreach (var c in fPicks)
            {
                if (r.members.Count >= MaxPicks + 1) break;
                if (!c.CanJoinDungeon()) { world.Log(c.displayName + "は今は来られない（" + c.StateName() + "）"); continue; }
                if (!c.IsMob && c.tired > 75f) { world.Log(c.displayName + "は疲れていて行けないみたい"); continue; }
                r.members.Add(c);
            }
            fPicks.Clear();
            // 篤の組がダンジョンの場所を使う：ほかの組は見えない所へ
            if (stageRun != null && stageRun != r) { LeaveStage(stageRun, false); }
            Begin(r);
        }

        string WhyOr(out string why) { CanStartNow(out why); return why ?? "今は出発できない"; }

        void Begin(DungeonRun r)
        {
            r.started = world.Now;
            r.phase = DungeonRun.Phase.Gather;
            r.until = world.Now + 45; // 集まるまで（寮から学校へ。事前研修は訓練の施設へ）
            r.maxHp = 0f;
            foreach (var m in r.members) r.maxHp += 40f + m.Ab(Abilities.Body) * 0.6f;
            r.hp = r.maxHp;
            foreach (var m in r.members) m.JoinDungeon(r);
            runs.Add(r);
            if (!r.withF) { TodayHas(r.dorm); todayKeys.Add("d" + r.dorm); } // 寮ごとに1日1組まで
            world.Log((r.training ? "訓練の施設の事前研修へ出発：" : "ダンジョン実習へ出発：") + r.MemberNames() + (r.withF ? "" : "（" + HouseCup.Name(r.dorm) + "）"));
        }

        /// <summary>寮生だけ・ほかの寮の学生だけの組（休日の昼・授業のない午後に、ときどき）。</summary>
        void AutoParties()
        {
            float m = world.MinuteOfDay;
            bool holiday = world.IsHolidayToday;
            if (m < 10f * 60f || m > 15f * 60f) return;
            if (!holiday && m < 16.5f * 60f && Calendar.IsClassDay(world.Day, world.holidayWeekdays)) return;
            foreach (var e in YearEvents.All) if (e.invited == null && e.school && e.season == Calendar.Season(world.Day) && e.weekday == Calendar.Weekday(world.Day)) return; // 学校の行事の日は行かない
            // 寮生：ダンジョン実習が得意で退屈している子が、仲のいい子を誘う（1日に1組まで）
            if (Random.value < (holiday ? 0.25f : 0.1f) && !TodayHas(0))
            {
                SimCharacter lead = null; float best = 0f;
                foreach (var c in world.residents)
                {
                    if (c.IsTarget || !c.CanJoinDungeon() || c.tired > 50f || c.InDungeon) continue;
                    float v = c.SkillOf(Academics.Dungeon) + c.Ab(Abilities.Body) * 0.5f + c.bored * 0.5f + Random.Range(0f, 30f);
                    if (v > best) { best = v; lead = c; }
                }
                if (lead != null && best > 70f)
                {
                    var r = new DungeonRun { dorm = 0, title = lead.displayName + "たちの組", targetFloor = Random.Range(2, 4) };
                    r.members.Add(lead);
                    var friends = new List<SimCharacter>();
                    foreach (var c in world.residents)
                        if (c != lead && !c.IsTarget && c.CanJoinDungeon() && c.tired < 60f && world.rel.Get(lead.id, c.id) >= 40f) friends.Add(c);
                    friends.Sort((a, b) => world.rel.Get(lead.id, b.id).CompareTo(world.rel.Get(lead.id, a.id)));
                    for (int i = 0; i < Mathf.Min(2, friends.Count); i++) r.members.Add(friends[i]);
                    if (r.members.Count >= 2) Begin(r);
                }
            }
            // ほかの寮：寮ごとに1日1組まで
            for (int d = 0; d < 3; d++)
            {
                if (TodayHas(d + 1) || Random.value > (holiday ? 0.3f : 0.15f)) continue;
                var cand = new List<SimCharacter>();
                foreach (var c in world.mobs) if (!c.IsTeacher && c.profile.mobDorm == d && c.CanJoinDungeon() && !c.InClub) cand.Add(c);
                if (cand.Count < 3) continue;
                var r = new DungeonRun { dorm = d + 1, title = HouseCup.Name(d + 1) + "の組", targetFloor = Random.Range(2, 5) };
                for (int i = 0; i < 3; i++) { int k = Random.Range(0, cand.Count); r.members.Add(cand[k]); cand.RemoveAt(k); }
                Begin(r);
            }
        }

        readonly HashSet<string> todayKeys = new HashSet<string>();
        int todayDay = -1;
        bool TodayHas(int dorm)
        {
            if (todayDay != world.Day) { todayDay = world.Day; todayKeys.Clear(); }
            if (todayKeys.Contains("d" + dorm)) return true;
            foreach (var r in runs) if (!r.withF && r.dorm == dorm) return true;
            return false;
        }

        // ================= 進み方 =================
        void Tick(DungeonRun r)
        {
            double now = world.Now;
            // 夜になったら帰る（20時）
            if (r.phase != DungeonRun.Phase.Gather && world.MinuteOfDay >= 20f * 60f) { GoHome(r, "夜になったので"); return; }
            switch (r.phase)
            {
                case DungeonRun.Phase.Gather:
                {
                    bool all = true;
                    foreach (var m in r.members) if (m.state != SimCharacter.State.Away) { all = false; break; }
                    if (!all && now < r.until) return;
                    if (r.withF && r.members[0].state != SimCharacter.State.Away)
                    {
                        if (now < r.until + 30) return;
                        Finish(r, r.training ? "篤が訓練の施設に来られなかった" : "篤が準備室に来られなかった");
                        return;
                    }
                    // 間に合わなかった子は置いていく
                    var late = r.members.FindAll(m => m.state != SimCharacter.State.Away);
                    foreach (var m in late) { r.members.Remove(m); m.LeaveDungeon(r); world.Log(m.displayName + (r.training ? "は事前研修に間に合わなかった" : "はダンジョン実習に間に合わなかった")); }
                    if (r.members.Count == 0) { Finish(r, "だれも集まらなかった"); return; }
                    bool stage = stageRun == null && (r.withF || Random.value < 0.8f);
                    if (stage) EnterStage(r); else { r.onStage = false; r.Log("（見えない所で探索している）"); }
                    r.room = 0;
                    r.Log(r.training ? "訓練の施設：再現のダンジョンに入った（魔物は幻。けがはしない）" : r.floor + "階に入った");
                    NextRoom(r);
                    break;
                }
                case DungeonRun.Phase.Walk:
                {
                    bool arrived = true;
                    if (r.onStage) foreach (var m in r.members) if (m.state == SimCharacter.State.Walking && m.HasPath) { arrived = false; break; }
                    if (!arrived && now < r.until) return;
                    if (r.onStage) foreach (var m in r.members) m.DungeonStand(RoomCenter(r.room) + Vector3.forward * 1.5f);
                    StartEvent(r);
                    break;
                }
                case DungeonRun.Phase.Choice:
                {
                    if (r.picked < 0)
                    {
                        // 自分たちで決める組は少し考えてから。篤の組も、30分選ばなければ自動で
                        if (!r.withF && now >= r.until) r.picked = AutoPick(r);
                        else if (r.withF && now >= r.until + 30) { r.picked = AutoPick(r); r.Log("（迷っているうちに、みんなで決めた）"); }
                        else return;
                    }
                    Resolve(r, r.picked);
                    r.picked = -1;
                    break;
                }
                case DungeonRun.Phase.Act:
                {
                    if (now < r.until) return;
                    FinishAct(r);
                    break;
                }
            }
        }

        // ================= 部屋と出来事 =================
        void NextRoom(DungeonRun r)
        {
            r.room++;
            r.phase = DungeonRun.Phase.Walk;
            r.until = world.Now + (r.onStage ? 18 : 9);
            if (r.onStage)
            {
                Vector3 c = RoomCenter(r.room);
                for (int i = 0; i < r.members.Count; i++) r.members[i].DungeonWalk(c + Formation(i), (r.withF ? "探索している（" : "探索している（") + r.floor + "階）");
            }
        }

        void StartEvent(DungeonRun r)
        {
            r.evFought = false; r.evInspected = false; r.evMimic = false;
            if (r.training)
            {
                // 事前研修：魔物・罠・宝箱を1つずつ、最後に研修の終わり
                r.ev = r.room == 1 ? DungeonRun.Ev.Monster : (r.room == 2 ? DungeonRun.Ev.Trap : (r.room == 3 ? DungeonRun.Ev.Chest : DungeonRun.Ev.TrainGoal));
                if (r.ev == DungeonRun.Ev.Monster) { r.evCount = 1; r.evStr = MonsterStr(1, 1) * 0.7f; }
                SetupChoice(r);
                if (r.onStage && r.ev != DungeonRun.Ev.TrainGoal) world.DungeonShowEvent(r);
                return;
            }
            if (r.room >= 4) r.ev = r.floor >= MaxFloor ? DungeonRun.Ev.Altar : DungeonRun.Ev.Stairs;
            else
            {
                float x = Random.value;
                float mon = 0.45f + r.floor * 0.03f;
                r.ev = x < mon ? DungeonRun.Ev.Monster : (x < mon + 0.2f ? DungeonRun.Ev.Trap : (x < mon + 0.4f ? DungeonRun.Ev.Chest : (x < mon + 0.5f ? DungeonRun.Ev.Spring : DungeonRun.Ev.None)));
            }
            if (r.ev == DungeonRun.Ev.None) { r.Log(r.floor + "階・" + r.room + "つ目の部屋：何もなかった"); NextRoom(r); return; }
            if (r.ev == DungeonRun.Ev.Monster) { r.evCount = Random.Range(1, Mathf.Min(4, 1 + r.floor)); r.evStr = MonsterStr(r.floor, r.evCount); }
            if (r.ev == DungeonRun.Ev.Chest) r.evMimic = Random.value < 0.15f + r.floor * 0.02f;
            SetupChoice(r);
            if (r.onStage) world.DungeonShowEvent(r);
        }

        static float MonsterStr(int floor, int count) { return (30f + floor * 14f) * Mathf.Pow(count, 0.8f) * Random.Range(0.85f, 1.15f); }

        static readonly string[] MonsterNames = { "スライム", "大コウモリ", "骸骨の兵士", "石の番人", "影の魔物" };
        public static string MonsterName(int floor) { return MonsterNames[Mathf.Clamp(floor - 1, 0, MonsterNames.Length - 1)]; }

        void SetupChoice(DungeonRun r)
        {
            string where = r.training ? "事前研修・" + r.room + "つ目の部屋" : r.floor + "階・" + r.room + "つ目の部屋";
            string back = r.training ? "研修をやめる" : "引き返して帰る";
            switch (r.ev)
            {
                case DungeonRun.Ev.Monster:
                    r.evText = where + "：" + (r.training ? "幻の" : "") + MonsterName(r.floor) + "が" + r.evCount + "体いる（強さ " + Mathf.RoundToInt(r.evStr) + "）";
                    r.choices = new[] { "戦う", "こっそり通り抜ける", back };
                    break;
                case DungeonRun.Ev.Trap:
                    r.evText = where + "：床に罠の魔法陣がある（外す＝器用さ・魔道具学／駆け抜ける＝体力）";
                    r.choices = new[] { "調べて外す", "一気に駆け抜ける", back };
                    break;
                case DungeonRun.Ev.Chest:
                    r.evText = where + (r.training ? "：練習用の宝箱がある（調べてから開けるのが基本）" : "：古い宝箱がある（調べる＝魔道具学・知力。魔物の宝箱かもしれない）");
                    r.choices = new[] { "すぐ開ける", "調べてから開ける", "そのままにする" };
                    break;
                case DungeonRun.Ev.TrainGoal:
                    r.evText = where + "：再現のダンジョンの出口。教官が待っている";
                    r.choices = new[] { "研修を終える" };
                    break;
                case DungeonRun.Ev.Spring:
                    r.evText = where + "：青く光る泉がある";
                    r.choices = new[] { "ひと休みする", "先へ進む" };
                    break;
                case DungeonRun.Ev.Stairs:
                    r.evText = where + "：下の階への階段（" + (r.floor + 1) + "階へ）";
                    r.choices = new[] { "下の階へ進む", "今日はここまで（帰る）" };
                    break;
                case DungeonRun.Ev.Altar:
                    r.evText = where + "：いちばん奥の祭壇。古い宝が眠っている";
                    r.choices = new[] { "宝を持ち帰る" };
                    break;
            }
            r.phase = DungeonRun.Phase.Choice;
            r.picked = -1;
            r.until = world.Now + (r.withF ? 0 : 3);
            if (r.withF) world.Log((r.training ? "事前研修：" : "ダンジョン：") + r.evText + "（どうするか選ぶ）");
        }

        int AutoPick(DungeonRun r)
        {
            float hpRate = r.hp / Mathf.Max(1f, r.maxHp);
            switch (r.ev)
            {
                case DungeonRun.Ev.Monster:
                    if (hpRate < 0.25f) return 2;
                    return Power(r) > r.evStr * 0.9f || hpRate > 0.6f ? 0 : 1;
                case DungeonRun.Ev.Trap: return Best(r, Abilities.Dex) + Best(r, -1, Academics.Tools) * 0.5f > 70f ? 0 : 1;
                case DungeonRun.Ev.Chest: return Best(r, -1, Academics.Tools) > 40f ? 1 : 0;
                case DungeonRun.Ev.Spring: return hpRate < 0.8f ? 0 : 1;
                case DungeonRun.Ev.Stairs:
                    if (r.withF) return hpRate > 0.5f && r.floor < MaxFloor ? 0 : 1;
                    return hpRate > 0.45f && r.floor < r.targetFloor && world.MinuteOfDay < 17f * 60f ? 0 : 1;
            }
            return 0;
        }

        float Power(DungeonRun r)
        {
            float p = 0f, mon = 0f;
            foreach (var m in r.members)
            {
                float sword = m.SkillOf(Academics.Sword) * 0.6f + m.Ab(Abilities.Body) * 0.4f;
                float magic = m.SkillOf(Academics.MagicArts) * 0.6f + m.Ab(Abilities.Magic) * 0.4f;
                p += Mathf.Max(sword, magic) + m.SkillOf(Academics.Dungeon) * 0.3f;
                mon = Mathf.Max(mon, m.SkillOf(Academics.Monster));
            }
            return p + mon * 0.3f;
        }

        float Best(DungeonRun r, int ability, int subject = -1)
        {
            float b = 0f;
            foreach (var m in r.members) b = Mathf.Max(b, ability >= 0 ? m.Ab(ability) : m.SkillOf(subject));
            return b;
        }

        float Avg(DungeonRun r, int ability)
        {
            float s = 0f;
            foreach (var m in r.members) s += m.Ab(ability);
            return r.members.Count > 0 ? s / r.members.Count : 0f;
        }

        /// <summary>選んだ行動を始める（戦う・調べるなどは少し時間がかかる）。</summary>
        void Resolve(DungeonRun r, int choice)
        {
            r.act = choice;
            if (r.withF && choice >= 0 && choice < r.choices.Length) r.Log("→ " + r.choices[choice]);
            double dur = 2;
            switch (r.ev)
            {
                case DungeonRun.Ev.Monster:
                    if (choice == 2) { GoHome(r, "引き返した"); return; }
                    dur = choice == 0 ? Random.Range(6f, 10f) : 4;
                    if (r.onStage) foreach (var m in r.members) m.DungeonPose(choice == 0 ? FightScene(m) : "忍び足", choice == 0 ? MonsterName(r.floor) + "と戦っている" : "そっと通り抜けている");
                    break;
                case DungeonRun.Ev.Trap:
                    if (choice == 2) { GoHome(r, "引き返した"); return; }
                    dur = choice == 0 ? 8 : 2;
                    if (r.onStage) foreach (var m in r.members) m.DungeonPose(choice == 0 ? "工房" : "駆け抜け", choice == 0 ? "罠を調べている" : "罠を駆け抜けている");
                    break;
                case DungeonRun.Ev.Chest:
                    if (choice == 2) { r.Log("宝箱はそのままにした"); NextRoom(r); return; }
                    dur = choice == 1 ? 8 : 2;
                    if (r.onStage) foreach (var m in r.members) m.DungeonPose(choice == 1 ? "工房" : "宝箱", choice == 1 ? "宝箱を調べている" : "宝箱を開けている");
                    break;
                case DungeonRun.Ev.Spring:
                    if (choice == 1) { NextRoom(r); return; }
                    dur = 15;
                    if (r.onStage) foreach (var m in r.members) m.DungeonPose(null, "泉のそばでひと休みしている");
                    break;
                case DungeonRun.Ev.Stairs:
                    if (choice == 1) { GoHome(r, "今日はここまでにした"); return; }
                    dur = 3;
                    break;
                case DungeonRun.Ev.Altar:
                    dur = 5;
                    break;
                case DungeonRun.Ev.TrainGoal:
                    dur = 3;
                    break;
            }
            r.phase = DungeonRun.Phase.Act;
            r.until = world.Now + dur;
        }

        static string FightScene(SimCharacter m)
        {
            float sword = m.SkillOf(Academics.Sword) * 0.6f + m.Ab(Abilities.Body) * 0.4f;
            float magic = m.SkillOf(Academics.MagicArts) * 0.6f + m.Ab(Abilities.Magic) * 0.4f;
            return sword >= magic ? "剣術" : "魔法";
        }

        /// <summary>行動の結果。</summary>
        void FinishAct(DungeonRun r)
        {
            switch (r.ev)
            {
                case DungeonRun.Ev.Monster:
                {
                    bool fight = r.act == 0;
                    if (!fight)
                    {
                        float sneak = Avg(r, Abilities.Dex) / 100f * 0.8f + 0.15f - r.members.Count * 0.04f;
                        if (Random.value < sneak) { r.Log("気づかれずに通り抜けた"); Grow(r, Abilities.Dex, 0.3f, Academics.Dungeon, 0.4f); NextRoom(r); return; }
                        r.Log("見つかって、戦いになった！");
                        Damage(r, r.evStr * 0.2f);
                    }
                    float p = Power(r);
                    float win = Mathf.Clamp(p / (p + r.evStr * 1.1f), 0.12f, 0.95f);
                    Grow(r, Abilities.Body, 0.3f, Academics.Sword, 0.4f);
                    Grow(r, Abilities.Magic, 0.3f, Academics.MagicArts, 0.4f);
                    Grow(r, -1, 0f, Academics.Monster, 0.5f);
                    if (Random.value < win)
                    {
                        Damage(r, r.evStr * Random.Range(0.1f, 0.3f));
                        r.monsters += r.evCount;
                        int s = r.training ? 0 : Random.Range(3, 10) * r.floor * r.evCount;
                        r.silver += s;
                        r.Log((r.training ? "幻の" : "") + MonsterName(r.floor) + "を倒した（体力 " + Mathf.RoundToInt(r.hp) + "/" + Mathf.RoundToInt(r.maxHp) + (s > 0 ? "・銀貨＋" + s : "") + "）");
                        if (r.onStage) world.DungeonClearEvent(true);
                        if (r.hp <= 0f) { GoHome(r, "力を使い果たした"); return; }
                        NextRoom(r);
                    }
                    else
                    {
                        Damage(r, r.evStr * Random.Range(0.5f, 0.9f));
                        r.Log(MonsterName(r.floor) + "に押し返された（体力 " + Mathf.RoundToInt(Mathf.Max(0f, r.hp)) + "/" + Mathf.RoundToInt(r.maxHp) + "）");
                        if (r.hp <= 0f) { GoHome(r, r.training ? "幻の魔物に倒された（けがはない。もう一度挑める）" : "けがをして引き返した"); return; }
                        r.evStr *= 0.8f; // 弱ってはいる
                        SetupChoice(r);
                    }
                    return;
                }
                case DungeonRun.Ev.Trap:
                {
                    float ok = r.act == 0 ? (Best(r, Abilities.Dex) * 0.6f + Best(r, -1, Academics.Tools) * 0.4f) / 100f + 0.2f : Avg(r, Abilities.Body) / 100f + 0.2f;
                    if (Random.value < ok) { r.Log(r.act == 0 ? "罠を外した" : "罠を駆け抜けた"); if (r.act == 0) { if (!r.training) r.silver += 5 * r.floor; Grow(r, Abilities.Dex, 0.3f, Academics.Tools, 0.4f); } }
                    else { float d = 12f + r.floor * 6f; Damage(r, d); r.Log("罠にかかった！（体力 " + Mathf.RoundToInt(Mathf.Max(0f, r.hp)) + "/" + Mathf.RoundToInt(r.maxHp) + "）"); if (r.onStage) world.DungeonFlash(); }
                    Grow(r, -1, 0f, Academics.Dungeon, 0.4f);
                    if (r.hp <= 0f) { GoHome(r, r.training ? "罠で倒れた（けがはない。もう一度挑める）" : "けがをして引き返した"); return; }
                    NextRoom(r);
                    return;
                }
                case DungeonRun.Ev.Chest:
                {
                    if (r.act == 1)
                    {
                        float ok = (Best(r, -1, Academics.Tools) + Best(r, Abilities.Mind)) / 200f + 0.3f;
                        if (r.evMimic && Random.value < ok)
                        {
                            r.Log("宝箱は魔物（ミミック）だった。気づいて避けた");
                            Grow(r, Abilities.Mind, 0.3f, Academics.Monster, 0.4f);
                            NextRoom(r);
                            return;
                        }
                    }
                    if (r.evMimic)
                    {
                        r.Log("宝箱が噛みついてきた！ミミックだ");
                        r.ev = DungeonRun.Ev.Monster; r.evCount = 1; r.evStr = MonsterStr(r.floor + 1, 1);
                        Damage(r, r.evStr * 0.2f);
                        if (r.onStage) world.DungeonMimic();
                        if (r.hp <= 0f) { GoHome(r, "けがをして引き返した"); return; }
                        r.evText = r.floor + "階：ミミックが襲ってくる（強さ " + Mathf.RoundToInt(r.evStr) + "）";
                        r.choices = new[] { "戦う", "こっそり通り抜ける", "引き返して帰る" };
                        r.phase = DungeonRun.Phase.Choice; r.picked = -1; r.until = world.Now + (r.withF ? 0 : 2);
                        return;
                    }
                    if (r.training) { r.Log(r.act == 1 ? "調べてから開けた。中は空っぽ（開け方の練習）" : "開けた。中は空っぽ（本番は魔物の宝箱もあるので、調べてから開けるとよい）"); if (r.act == 1) Grow(r, Abilities.Mind, 0.2f, Academics.Tools, 0.3f); NextRoom(r); return; }
                    Loot(r, r.act == 1 ? 1.2f : 1f);
                    r.chests++;
                    if (r.onStage) world.DungeonClearEvent(false);
                    NextRoom(r);
                    return;
                }
                case DungeonRun.Ev.Spring:
                    r.hp = Mathf.Min(r.maxHp, r.hp + r.maxHp * 0.35f);
                    foreach (var m in r.members) m.tired = Mathf.Max(0f, m.tired - 10f);
                    r.Log("泉の水で元気が戻った（体力 " + Mathf.RoundToInt(r.hp) + "/" + Mathf.RoundToInt(r.maxHp) + "）");
                    NextRoom(r);
                    return;
                case DungeonRun.Ev.Stairs:
                    r.floorsCleared++;
                    r.floor++;
                    r.room = 0;
                    r.Log(r.floor + "階へ降りた");
                    Grow(r, Abilities.Body, 0.2f, Academics.Dungeon, 0.6f);
                    if (r.withF) BestFloor = Mathf.Max(BestFloor, r.floor);
                    if (r.onStage) { world.DungeonBuildFloor(r.floor); foreach (var m in r.members) m.DungeonTeleport(RoomCenter(0) + Formation(r.members.IndexOf(m))); }
                    NextRoom(r);
                    return;
                case DungeonRun.Ev.Altar:
                    r.floorsCleared++;
                    Loot(r, 3f);
                    r.chests++;
                    r.Log("いちばん奥の宝を手に入れた！");
                    if (r.withF) Album.Add(world, "ダンジョン実習で最下層の祭壇にたどり着いた（" + r.MemberNames() + "）");
                    GoHome(r, "最下層まで探索した");
                    return;
                case DungeonRun.Ev.TrainGoal:
                    TrainingDone = true;
                    Grow(r, Abilities.Body, 0.2f, Academics.Dungeon, 0.6f);
                    r.Log("事前研修を終えた。ダンジョン実習に出発できるようになった");
                    world.Log("訓練の施設の事前研修を終えた。これからダンジョン実習に出発できる");
                    Finish(r, "事前研修を終えた");
                    return;
            }
            NextRoom(r);
        }

        void Damage(DungeonRun r, float d)
        {
            r.hp -= d;
            foreach (var m in r.members) m.tired = Mathf.Min(100f, m.tired + d * (r.training ? 0.03f : 0.08f)); // 事前研修の魔物は幻（けがはしない）
        }

        void Grow(DungeonRun r, int ability, float av, int subject, float sv)
        {
            foreach (var m in r.members)
            {
                if (ability >= 0) m.GrowAbility(ability, av);
                if (subject >= 0) m.AddSkill(subject, sv);
            }
        }

        static readonly ItemKind[] LootCommon = { ItemKind.StarPendant, ItemKind.FlowerRibbon, ItemKind.ManaBracelet, ItemKind.GlassesCharm, ItemKind.SunBrooch, ItemKind.NightEarring, ItemKind.SwiftAnklet, ItemKind.HeartyRing, ItemKind.AdventFeather, ItemKind.ScholarBookmark, ItemKind.CrafterThimble, ItemKind.MoonVeil, ItemKind.ClearBeads };
        static readonly ItemKind[] LootRare = { ItemKind.LionBracelet, ItemKind.RoseChoker, ItemKind.ArtisanRing, ItemKind.ManaCrystal, ItemKind.SageMonocle, ItemKind.StudyBadge };

        void Loot(DungeonRun r, float mul)
        {
            int s = Mathf.RoundToInt(Random.Range(10, 31) * r.floor * mul);
            r.silver += s;
            string got = "銀貨" + s;
            float itemChance = 0.25f * mul;
            if (Random.value < itemChance)
            {
                bool rare = r.floor >= 3 && Random.value < 0.35f;
                var k = rare ? LootRare[Random.Range(0, LootRare.Length)] : LootCommon[Random.Range(0, LootCommon.Length)];
                r.items.Add(k);
                var d = ItemBook.Get(k);
                got += "・" + (d != null ? d.name : k.ToString());
            }
            r.Log("宝箱から " + got + " を見つけた");
        }

        // ================= 帰る =================
        void GoHome(DungeonRun r, string why)
        {
            r.Log(why + (r.training ? "。訓練の施設を出る" : "。準備室へ戻る"));
            Finish(r, why);
        }

        void Finish(DungeonRun r, string why)
        {
            r.phase = DungeonRun.Phase.Done;
            if (r.onStage) LeaveStage(r, true);
            int pts = r.floorsCleared * 8 + r.monsters * 2 + r.chests * 4;
            if (r.training)
            {
                // 事前研修：戦利品・寮杯の点はなし。いっしょに受けた子とは少し仲良くなる
                if (r.room > 0)
                    foreach (var a in r.members) foreach (var b in r.members)
                        {
                            if (a.id >= b.id || a.IsMob || b.IsMob) continue;
                            world.rel.Add(a.id, b.id, 2f, world.simTime);
                        }
                world.Log("事前研修の結果（" + r.title + "）：" + why);
                foreach (var m in r.members) m.LeaveDungeon(r);
                return;
            }
            if (r.members.Count > 0 && (r.room > 0 || r.floor > 1))
            {
                HouseCup.Add(world, r.dorm, pts, "ダンジョン実習（" + r.floor + "階まで）", r.dorm == 0);
                if (r.withF && world.player != null)
                {
                    if (r.silver > 0) world.player.Earn(r.silver, "ダンジョン実習の戦利品");
                    foreach (var k in r.items) { world.player.Refund(k); var d = ItemBook.Get(k); world.Log("ダンジョン実習で「" + (d != null ? d.name : k.ToString()) + "」を手に入れた"); }
                }
                // 一緒に探索した寮生どうし・篤と仲良くなる
                foreach (var a in r.members) foreach (var b in r.members)
                    {
                        if (a.id >= b.id || a.IsMob || b.IsMob) continue;
                        world.rel.Add(a.id, b.id, 3f + r.floorsCleared, world.simTime);
                        if (a.IsTarget && !b.IsTarget) world.ChangeLove(b, 2f + r.floorsCleared);
                        if (b.IsTarget && !a.IsTarget) world.ChangeLove(a, 2f + r.floorsCleared);
                    }
                world.Log("ダンジョン実習の結果（" + r.title + "）：" + r.floor + "階まで・魔物 " + r.monsters + "体・宝箱 " + r.chests + "個" + (r.withF ? "・銀貨 " + r.silver : "") + "（" + why + "）");
            }
            foreach (var m in r.members) m.LeaveDungeon(r);
        }

        // ================= ダンジョンの場所 =================
        void EnterStage(DungeonRun r)
        {
            stageRun = r;
            r.onStage = true;
            world.DungeonBuildFloor(r.floor);
            for (int i = 0; i < r.members.Count; i++) r.members[i].DungeonTeleport(RoomCenter(0) + Formation(i));
            if (r.withF) world.FollowInDungeon(r.members[0]);
        }

        /// <summary>場所から出る。done＝探索の終わり（篤の組に場所をゆずるときは、見えない所で続ける）。</summary>
        void LeaveStage(DungeonRun r, bool done)
        {
            if (stageRun == r) stageRun = null;
            r.onStage = false;
            world.DungeonClearStage();
            if (!done) { foreach (var m in r.members) m.DungeonHide(); r.Log("（ほかの組が来たので、見えない所で探索を続けている）"); }
        }

        public static Vector3 RoomCenter(int i) { return LoungeWorld.DungeonOrigin + new Vector3(0f, 0f, i * LoungeWorld.DungeonStep); }
        static Vector3 Formation(int i)
        {
            float x = (i % 2 == 0 ? -0.75f : 0.75f);
            float z = i < 2 ? 0f : -1.1f;
            return new Vector3(x, 0f, z);
        }

        public static bool InArea(Vector3 p) { return p.x > LoungeWorld.DungeonOrigin.x - 15f; }
    }
}
