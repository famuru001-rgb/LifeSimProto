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

    /// <summary>
    /// 魔法大学の寮（談話室・食堂・浴場・2階の個室10部屋）を組み立てて、試作を動かす中心の部品。
    /// 設定（1日の長さ・倍速など）はこの部品の Inspector で変更できる。
    /// </summary>
    public partial class LoungeWorld : MonoBehaviour
    {
        public static LoungeWorld Instance { get; private set; }

        [Header("1日の長さ（1倍速での実際の時間・分）")]
        [Tooltip("夜も含めた1日の長さ（分）")]
        public float oneDayRealMinutes = 40f;
        [Tooltip("そのうち夜（23:00〜6:30）にあてる時間（分）")]
        public float nightRealMinutes = 10f;

        [Header("倍速の設定")]
        [Tooltip("画面上の速さボタン（倍）")]
        public float[] speedOptions = { 1f, 2f, 4f, 10f };
        [Tooltip("全員が寝ている間の速さ（倍）")]
        public float allAsleepSpeed = 30f;

        [Header("開始の時刻")]
        [Tooltip("試作を始める時刻（時）。6.2 = 6時12分")]
        public float startHour = 6.2f;

        public const float NightStartMin = 23f * 60f, NightEndMin = 6.5f * 60f;
        public const float Floor2Y = 3f;

        // ---- 状態 ----
        public GridNav nav0, nav1;
        public Relationships rel;
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
            gameMinutes = startHour * 60.0;
            Time.timeScale = 1f;
            Build();
            SpawnCharacters();
            SetupCamera();
            gameObject.AddComponent<SimHud>();
            Log("寮の試作を開始しました（" + Day + "日目 " + ScheduleMaker.Clock(Now) + "）");
        }

        void Update()
        {
            simTime += Time.deltaTime;
            gameMinutes += Time.deltaTime * GameMinutesPerSecond();
            UpdateSpeed();
            UpdateLighting();
            UpdateCutaway();
        }

        // ================= 時間 =================
        public static bool IsNight(float minuteOfDay) { return minuteOfDay >= NightStartMin || minuteOfDay < NightEndMin; }

        float GameMinutesPerSecond()
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
            Time.maximumDeltaTime = ts > 8f ? 0.6f : 0.3333f;
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

        public void ReportTalk(SimCharacter a, SimCharacter b, TalkResult result)
        {
            Vector2Int stage = rel.Apply(a.id, b.id, result, simTime);
            string word = result == TalkResult.Fun ? "楽しく話した ♪" : (result == TalkResult.Normal ? "ふつうに話した" : "少し気まずかった");
            Log(a.displayName + "と" + b.displayName + "：" + word + "（好感度 " + Mathf.RoundToInt(rel.Get(a.id, b.id)) + "）");
            if (stage.y > stage.x)
                Log("★ " + a.displayName + "と" + b.displayName + "が「" + Relationships.StageName(stage.y) + "」になった！");
            else if (stage.y < stage.x)
                Log(a.displayName + "と" + b.displayName + "の関係が「" + Relationships.StageName(stage.y) + "」に戻った");
        }

        // ================= キャラ =================
        void SpawnCharacters()
        {
            rel = new Relationships(6, 12345);
            AddCharacter(0, "A", "青髪・角", new Color(0.55f, 0.8f, 1f), 0.5f, 0.15f, false);
            AddCharacter(1, "B", "エルフ", new Color(0.35f, 0.7f, 0.35f), 0.2f, 0.15f, false);
            AddCharacter(2, "C", "猫耳", new Color(0.85f, 0.2f, 0.2f), 0.85f, 0.85f, false);
            AddCharacter(3, "D", "黒髪ロング", new Color(0.13f, 0.13f, 0.15f), 0.25f, 0.5f, false);
            AddCharacter(4, "E", "金髪ツインテール", new Color(0.95f, 0.8f, 0.35f), 0.8f, 0.5f, false);
            AddCharacter(5, "F", "男性", new Color(0.55f, 0.55f, 0.58f), 0.5f, 0.5f, true);
        }

        void AddCharacter(int id, string name, string note, Color color, float social, float whim, bool male)
        {
            var go = new GameObject("Char_" + name);
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

            var c = go.AddComponent<SimCharacter>();
            c.id = id;
            c.displayName = name;
            c.note = note;
            c.color = color;
            c.sociability = social;
            c.whim = whim;
            c.male = male;
            c.room = id < rooms.Count ? rooms[id] : null;
            if (c.room != null)
            {
                c.room.bed.owner = id;
                c.room.desk.owner = id;
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
