using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    public enum SeatKind { Sit, Bed, Bath }

    /// <summary>座る・寝る・湯船につかる場所1つ分。</summary>
    public class Seat
    {
        public Vector3 position;  // その場所での立ち位置（床の高さ）
        public Vector3 forward;   // 向き
        public Vector3 approach;  // 使う前・使った後に立つ場所
        public string label;
        public SeatKind kind = SeatKind.Sit;
        public bool floorSeat;    // 床に座る（座布団）・床に寝る（布団）
        public RoomInfo room;     // 個室の中の場所なら、その部屋
        public string area;       // 「食堂」など。食事の席探しに使う
        public int owner = -1;    // ベッドの持ち主
        public SimCharacter occupant;
    }

    /// <summary>暇つぶしの場所（暖炉・本棚・窓・机など）。</summary>
    public class Poi
    {
        public string label;
        public Vector3 lookAt;
        public Vector3[] slots;
        public SimCharacter[] occupants;
        public int owner = -1;    // 個室の机など、持ち主だけが使う場所
        public bool counter;      // 食堂の受け取り口
        public int hobby = -1;    // 趣味専用の場所（SimCharacter.HobbyKind）。-1 はだれでも
        public bool snack;        // 談話室のおやつワゴン
        public bool dynamicSpot;  // 使い魔など、動く相手のまわり
        public string place;      // 新しい場所（中庭など）の見どころなら、その場所の名前
        public int subject = -1;  // その科目の自主練習に使う場所（Academics の番号）
        public bool classOnly;    // 実技の授業のときだけ使う
        public bool outdoor;      // 外（雨や雪の日は使われにくい）
        public bool kitchen;      // 台所（お菓子づくり）

        public int FreeSlot()
        {
            for (int i = 0; i < occupants.Length; i++) if (occupants[i] == null) return i;
            return -1;
        }
    }

    /// <summary>1階と2階をつなぐ階段。</summary>
    public class Stair
    {
        public Vector3 bottomApproach, bottom, top, topApproach;
    }

    public enum WallMode { Auto, Low, All }

    /// <summary>みんなが集まる行事（お茶会・誕生日会・停電のときの暖炉など）や、外出の行事（お祭り・デート）。</summary>
    public class Gathering
    {
        public string key;
        public string label;        // 頭の上に出す行動（例：「お茶会に参加している」）
        public string area;         // 集まる区画（外出なら空）
        public Vector3 focus;       // 集まる中心
        public double start, end;
        public bool outing;         // 外出の行事なら true
        public List<SimCharacter> invited;  // null ならだれでも参加できる
        public float joinChance = 0.85f;
        public SimCharacter honoree;
        public bool processed;
        public readonly HashSet<SimCharacter> members = new HashSet<SimCharacter>();
        public readonly HashSet<SimCharacter> declined = new HashSet<SimCharacter>();

        public bool Active(double now) { return now >= start && now < end; }
    }

    /// <summary>
    /// 魔法大学の寮（談話室・食堂・浴場・2階の個室10部屋）を組み立てて、試作を動かす中心の部品。
    /// 設定（1日の長さ・倍速など）はこの部品の Inspector で変更できる。
    /// </summary>
    public partial class LoungeWorld : MonoBehaviour
    {
        public static LoungeWorld Instance { get; private set; }

        [Header("1日の長さ（1倍速での実際の時間・分）")]
        [Tooltip("夜も含めた1日の長さ（分）")]
        public float oneDayRealMinutes = 20f;
        [Tooltip("そのうち夜（23:00〜6:30）にあてる時間（分）")]
        public float nightRealMinutes = 5f;

        [Header("倍速の設定")]
        [Tooltip("画面上の速さボタン（倍）")]
        public float[] speedOptions = { 1f, 2f, 4f, 10f };
        /// <summary>デバッグ用の速さ（画面の上のボタン・5と6のキー）。</summary>
        [System.NonSerialized] public float[] debugSpeedOptions = { 30f, 60f };
        [Tooltip("全員が寝ている間の速さ（倍）")]
        public float allAsleepSpeed = 30f;

        [Header("暦（1週間7日：光・火・水・風・土・月・星）")]
        [Tooltip("休日にする曜日（チェックした曜日が休日）。初期値は水の日と星の日")]
        public bool[] holidayWeekdays = { false, false, true, false, false, false, true };

        [Header("状態異常")]
        [Tooltip("1人が同時に持てる状態の数（系統の数）")]
        public int maxStatuses = 3;
        [Tooltip("状態が起きる確率の倍率（1 で標準。小さくすると起きにくい）")]
        public float statusChance = 0.6f;

        [Header("開始の時刻")]
        [Tooltip("試作を始める時刻（時）。6.2 = 6時12分")]
        public float startHour = 6.2f;

        public const float NightStartMin = 23f * 60f, NightEndMin = 6.5f * 60f;
        public const float Floor2Y = 3f;

        // ---- 状態 ----
        public GridNav nav0, nav1;
        public Relationships rel;
        public Romance romance;
        public readonly List<SimCharacter> characters = new List<SimCharacter>();
        public readonly List<Seat> seats = new List<Seat>();
        public readonly List<Poi> pois = new List<Poi>();
        public readonly List<Stair> stairs = new List<Stair>();
        public readonly List<KeyValuePair<Vector3, string>> worldLabels = new List<KeyValuePair<Vector3, string>>();
        public readonly List<string> log = new List<string>();
        public float simTime;
        public double gameMinutes;
        public Camera mainCamera;
        public WallMode wallMode = WallMode.Auto;
        public float userSpeed = 1f;
        public bool autoFast;

        // ---- イベント ----
        public readonly List<Gathering> gatherings = new List<Gathering>();
        public readonly List<string> todayNotes = new List<string>();
        public double blackoutUntil = -1, snackBonusUntil = -1;
        public bool Blackout { get { return Now < blackoutUntil; } }
        public bool SnackBonus { get { return Now < snackBonusUntil; } }
        public bool IsHolidayToday { get { return Calendar.IsHoliday(Day, holidayWeekdays); } }
        public bool BadWeatherToday { get { return Calendar.BadWeather(Day); } }
        public bool ExamEveToday { get { return Calendar.IsExamEve(Day, holidayWeekdays); } }
        public PlayerControl player;
        public int dormReputation;   // 寮の評判（行事の成功や試験で上がる）

        public Gathering ActiveGatheringFor(SimCharacter c)
        {
            foreach (var g in gatherings)
            {
                if (!g.Active(Now)) continue;
                if (g.invited != null && !g.invited.Contains(c)) continue;
                return g;
            }
            return null;
        }

        public Vector3 exitPoint = new Vector3(0f, 0f, -13.2f);     // 授業へ出かけるときの行き先（外）
        public Vector3 entrancePoint = new Vector3(0f, 0f, -10.2f); // 帰ってきて最初に立つ場所

        public int Day { get { return (int)(gameMinutes / 1440.0) + 1; } }
        public float MinuteOfDay { get { return (float)(gameMinutes % 1440.0); } }
        public double Now { get { return gameMinutes; } }

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            Application.runInBackground = true; // ほかのウィンドウを触っている間も、寮の時間は進む
            gameMinutes = startHour * 60.0;
            Time.timeScale = 1f;
            Build();
            SpawnCharacters();
            SetupCamera();
            player = gameObject.AddComponent<PlayerControl>();
            gameObject.AddComponent<EventDirector>();
            gameObject.AddComponent<YearEvents>();
            gameObject.AddComponent<SimHud>();
            Log("寮の試作を開始しました（" + Day + "日目 " + ScheduleMaker.Clock(Now) + "）");
            if (Loop.Count > 1) Log("★ " + Loop.Count + "周目の春の朝。篤だけが、前の1年を覚えている");
        }

        void Update()
        {
            simTime += Time.deltaTime;
            gameMinutes += Time.deltaTime * GameMinutesPerSecond();
            UpdateSpeed();
            UpdateLighting();
            UpdateCutaway();
            UpdatePlaces();
        }

        // ================= 時間 =================
        public static bool IsNight(float minuteOfDay) { return minuteOfDay >= NightStartMin || minuteOfDay < NightEndMin; }

        /// <summary>1秒（ゲームの時間の流れ）あたりに進むゲーム内の分。</summary>
        public float GameMinutesPerSecond()
        {
            float nightGame = 1440f - NightStartMin + NightEndMin;  // 450分
            float dayGame = 1440f - nightGame;                       // 990分
            float nightReal = Mathf.Max(0.5f, nightRealMinutes) * 60f;
            float dayReal = Mathf.Max(1f, oneDayRealMinutes - nightRealMinutes) * 60f;
            return IsNight(MinuteOfDay) ? nightGame / nightReal : dayGame / dayReal;
        }

        /// <summary>ゲーム内の分を、1倍速での実際の秒に直す（目安）。</summary>
        public float GameToSimSeconds(float gameMin) { return gameMin / Mathf.Max(0.01f, GameMinutesPerSecond()); }

        public string TimeOfDayName()
        {
            float m = MinuteOfDay;
            if (m < 300) return "深夜";
            if (m < 420) return "早朝";
            if (m < 600) return "朝";
            if (m < 960) return "昼";
            if (m < 1140) return "夕方";
            if (m < 1380) return "夜";
            return "深夜";
        }

        public void SetUserSpeed(float s) { userSpeed = s; }

        void UpdateSpeed()
        {
            bool allAsleep = characters.Count > 0;
            foreach (var c in characters) if (c.state != SimCharacter.State.Sleeping) { allAsleep = false; break; }
            autoFast = allAsleep && userSpeed > 0f && allAsleepSpeed > userSpeed;
            float ts = userSpeed <= 0f ? 0f : (autoFast ? allAsleepSpeed : userSpeed);
            Time.timeScale = Mathf.Clamp(ts, 0f, 100f);
            // 速い速度でも1コマの時間を切り詰めない（Unity の画面が裏にあってコマ数が落ちても。キャラは SimCharacter で細かく分けて動く）
            Time.maximumDeltaTime = ts > 8f ? Mathf.Max(0.6f, ts / 10f) : 0.3333f; // 1秒に10コマまで落ちても速さを保つ
        }

        // ================= 道さがし =================
        public static int FloorOf(float y) { return y > 1.5f ? 1 : 0; }
        public GridNav Nav(int floor) { return floor == 0 ? nav0 : nav1; }
        public GridNav NavAt(Vector3 p) { return Nav(FloorOf(p.y)); }
        public Vector3 NearestFree(Vector3 p) { return NavAt(p).NearestFreePoint(p); }

        public static bool OnFloorLevel(Vector3 p)
        {
            return Mathf.Abs(p.y) < 0.05f || Mathf.Abs(p.y - Floor2Y) < 0.05f;
        }

        /// <summary>階段をまたぐ道も探す。</summary>
        public List<Vector3> FindPath(Vector3 from, Vector3 to)
        {
            int ff = FloorOf(from.y), tf = FloorOf(to.y);
            if (ff == tf) return Nav(ff).FindPath(from, to);
            Vector3 upper = ff == 1 ? from : to;
            Stair s = upper.x < 0f ? stairs[0] : stairs[1];
            var res = new List<Vector3>();
            if (ff == 0)
            {
                var p1 = nav0.FindPath(from, s.bottomApproach);
                var p2 = nav1.FindPath(s.topApproach, to);
                if (p1 == null || p2 == null) return null;
                res.AddRange(p1);
                res.Add(s.bottom); res.Add(s.top); res.Add(s.topApproach);
                res.AddRange(p2);
            }
            else
            {
                var p1 = nav1.FindPath(from, s.topApproach);
                var p2 = nav0.FindPath(s.bottomApproach, to);
                if (p1 == null || p2 == null) return null;
                res.AddRange(p1);
                res.Add(s.top); res.Add(s.bottom); res.Add(s.bottomApproach);
                res.AddRange(p2);
            }
            return res;
        }

        public AreaInfo AreaAt(Vector3 p)
        {
            int f = FloorOf(p.y);
            foreach (var a in areaList)
                if (a.floor == f && a.rect.Contains(new Vector2(p.x, p.z))) return a;
            return null;
        }

        public string AreaName(Vector3 p)
        {
            var a = AreaAt(p);
            return a != null ? a.name : "";
        }

        // ================= 記録 =================
        public void Log(string message)
        {
            log.Add("[" + Day + "日目 " + ScheduleMaker.Clock(Now) + "] " + message);
            if (log.Count > 80) log.RemoveAt(0);
        }

        public SimCharacter RomanceTarget { get { return romance != null && romance.targetId < characters.Count ? characters[romance.targetId] : null; } }

        // すれ違いのあいさつの記録（同じ2人は1時間に1回まで）
        readonly Dictionary<int, double> greetTimes = new Dictionary<int, double>();
        public bool GreetReady(int a, int b)
        {
            double t;
            int key = Mathf.Min(a, b) * 100 + Mathf.Max(a, b);
            return !greetTimes.TryGetValue(key, out t) || Now - t > 60.0;
        }
        public void MarkGreet(int a, int b) { greetTimes[Mathf.Min(a, b) * 100 + Mathf.Max(a, b)] = Now; }

        public void ReportTalk(SimCharacter a, SimCharacter b, TalkResult result, bool koibana = false, string place = null)
        {
            Vector2Int stage = rel.Apply(a.id, b.id, result, simTime);
            if (koibana && result == TalkResult.Fun) stage = new Vector2Int(stage.x, rel.Add(a.id, b.id, 3f, simTime).y);
            string word = result == TalkResult.Fun ? "楽しく話した ♪" : (result == TalkResult.Normal ? "ふつうに話した" : "少し気まずかった");
            if (koibana) word = result == TalkResult.Fun ? "恋バナで盛り上がった ♪" : "恋バナをした";
            // その場での会話（食事中・お風呂・座ったまま）は、楽しかったときだけ記録する
            if (place == null || result == TalkResult.Fun)
                Log(a.displayName + "と" + b.displayName + "：" + (place != null ? place + "で" : "") + word + "（好感度 " + Mathf.RoundToInt(rel.Get(a.id, b.id)) + "）");
            if (stage.y > stage.x)
                Log("★ " + a.displayName + "と" + b.displayName + "が「" + Relationships.StageName(stage.y) + "」になった！");
            else if (stage.y < stage.x)
                Log(a.displayName + "と" + b.displayName + "の関係が「" + Relationships.StageName(stage.y) + "」に戻った");

            // Fとの会話は恋愛の気持ちも動く
            if (romance != null)
            {
                SimCharacter other = a.id == romance.targetId ? b : (b.id == romance.targetId ? a : null);
                if (other != null)
                {
                    float d = result == TalkResult.Fun ? 6f : (result == TalkResult.Normal ? 2f : -3f);
                    ChangeLove(other, d);
                    // やきもち：Fが楽しそうに話しているのを、Fを好きな子が近くで見ている（仲は悪くならない）
                    if (result == TalkResult.Fun)
                    {
                        var f = RomanceTarget;
                        foreach (var c in characters)
                        {
                            if (c == other || c == f || !c.Present || c.InPrivate || c.LoveStage < 2) continue;
                            Vector3 dd = c.transform.position - other.transform.position;
                            if (Mathf.Abs(dd.y) > 1f || dd.sqrMagnitude > 81f) continue;
                            c.SawFWithOther(f, other);
                        }
                    }
                }
            }
        }

        public void ChangeLove(SimCharacter c, float delta)
        {
            if (romance == null || c.id == romance.targetId) return;
            Vector2Int st = romance.Add(c.id, delta, simTime);
            var t = RomanceTarget;
            string tn = t != null ? t.displayName : "篤";
            if (st.y > st.x && st.y <= 2)
                Log("♥ " + c.displayName + "が" + tn + "のことを「" + Romance.StageName(st.y) + "」になった");
            else if (st.y < st.x && st.y < 3)
                Log(c.displayName + "の" + tn + "への気持ちが「" + Romance.StageName(st.y) + "」に落ち着いた");
        }

        // ================= キャラ =================
        void SpawnCharacters()
        {
            Academics.ClearExam();
            rel = new Relationships(10, 12345);
            romance = new Romance(10, 5, 777);
            // 名前・性格・好き嫌い・科目・口調は CharProfiles（企画\ゲーム内容_たたき台.md のとおり）
            for (int i = 0; i < CharProfiles.All.Count && i < 10; i++) AddCharacter(i, CharProfiles.All[i]);
            CharProfiles.ApplyInitialRelations(rel, 12345);
        }

        void AddCharacter(int id, CharProfile p)
        {
            string letter = p.letter, name = p.name;
            Color color = p.color;
            bool male = p.male;
            var go = new GameObject("Char_" + letter);
            go.transform.SetParent(root, false);

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(go.transform, false);
            body.GetComponent<Renderer>().sharedMaterial = ProtoArt.Mat(color, ProtoArt.Fabric, new Vector2(2f, 2f), 0.3f);

            var face = GameObject.CreatePrimitive(PrimitiveType.Cube);
            face.name = "FaceMark";
            face.transform.SetParent(body.transform, false);
            face.transform.localPosition = new Vector3(0f, 0.62f, 0.42f);
            face.transform.localScale = new Vector3(0.6f, 0.08f, 0.25f);
            face.GetComponent<Renderer>().sharedMaterial = ProtoArt.Mat(new Color(0.95f, 0.85f, 0.75f));
            Destroy(face.GetComponent<Collider>());

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "SelectRing";
            ring.transform.SetParent(go.transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            ring.transform.localScale = new Vector3(0.9f, 0.01f, 0.9f);
            ring.GetComponent<Renderer>().sharedMaterial = ringMat;
            Destroy(ring.GetComponent<Collider>());
            ring.SetActive(false);

            // 仮モデル（Blenderで作った人型）があれば使い、仮の人形は見えなくする（クリック用の当たり判定は残す）
            var look = CharLook.Get(letter, male, color);
            if (ProtoBody.Create(go, look, letter) != null)
            {
                body.GetComponent<Renderer>().enabled = false;
                face.GetComponent<Renderer>().enabled = false;
            }

            var c = go.AddComponent<SimCharacter>();
            c.id = id;
            c.letter = letter;
            c.profile = p;
            c.displayName = name;
            c.shortName = p.shortName;
            c.note = p.race + "・" + p.personality;
            c.color = color;
            c.sociability = p.social;
            c.whim = p.whim;
            c.male = male;
            c.diligence = p.diligence;
            c.hobby = p.hobby;
            c.room = id < rooms.Count ? rooms[id] : null;
            if (c.room != null)
            {
                c.room.owner = id;
                c.room.bed.owner = id;
                c.room.desk.owner = id;
                foreach (var cu in c.room.cushions) cu.label = name + "の部屋";
                c.room.futon.label = name + "の部屋の布団";
                worldLabels.Add(new KeyValuePair<Vector3, string>(c.room.labelPos, name + "の部屋"));
            }
            c.Init(this, body.transform, ring);
            characters.Add(c);
        }

        // ================= カメラ =================
        void SetupCamera()
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                mainCamera = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = new Color(0.04f, 0.04f, 0.06f);
            mainCamera.nearClipPlane = 0.1f;
            mainCamera.farClipPlane = 300f;
            mainCamera.fieldOfView = 50f;
            if (mainCamera.GetComponent<CameraRig>() == null) mainCamera.gameObject.AddComponent<CameraRig>();
        }
    }
}
