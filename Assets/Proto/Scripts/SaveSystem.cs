using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>セーブの中身（JSON にして保存する）。</summary>
    [System.Serializable]
    public class SaveData
    {
        public int version = 1;
        public string savedAt;     // 保存した日時（パソコンの時計）
        public int loop, day;
        public double gameMinutes;
        public int dormReputation, money;
        public List<IntPair> items = new List<IntPair>();
        public List<CharSave> chars = new List<CharSave>();
        public int relCount;
        public float[] affinity;   // relCount × relCount
        public float[] love;
        public bool[] lover;
        public double[] loveCooldown;
        // ループで残る物
        public float[] fSkills, fAbilities;
        public List<string> zukan = new List<string>(), openedPlaces = new List<string>(), nightTogether = new List<string>(), album = new List<string>();
        public List<int> magic = new List<int>();
        public bool escaped, endless;
        public List<HLog.Entry> hlog = new List<HLog.Entry>();
        public List<StrInt> hTotal = new List<StrInt>();
        public List<StrInt> storyDone = new List<StrInt>();
        public List<string> hands = new List<string>(), storyFlags = new List<string>(), storyPending = new List<string>();
        public List<StrStr> achievements = new List<StrStr>();
        public List<StrInt> furniture = new List<StrInt>();
    }

    [System.Serializable] public class IntPair { public int key, value; }
    [System.Serializable] public class StrInt { public string key; public int value; }
    [System.Serializable] public class StrStr { public string key, value; }

    /// <summary>
    /// セーブとロード（famuru 決定 2026-10-22：毎朝6時の自動セーブ＋手動3枠）。
    /// 保存先はパソコンの Unity の保存場所（Application.persistentDataPath\saves）。
    /// 読み込むと寮を作り直して、保存したときの日と時刻から始まる。キャラの「今している行動」はやり直しになる（中身の数値・関係・持ち物・物語などは元どおり）。
    /// </summary>
    public static class SaveSystem
    {
        public const int ManualSlots = 3;
        /// <summary>読み込み待ちの中身（寮を作り直したあと LoungeWorld.Start で使う）。</summary>
        public static SaveData Pending;
        static readonly Dictionary<int, SaveData> peekCache = new Dictionary<int, SaveData>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetAll() { Pending = null; peekCache.Clear(); }

        public static string Folder { get { return Path.Combine(Application.persistentDataPath, "saves"); } }
        static string PathOf(int slot) { return Path.Combine(Folder, slot == 0 ? "auto.json" : "slot" + slot + ".json"); }
        public static string SlotName(int slot) { return slot == 0 ? "自動（毎朝）" : "セーブ" + slot; }

        /// <summary>枠の中身の見出し（なければ null）。</summary>
        public static SaveData Peek(int slot)
        {
            SaveData d;
            if (peekCache.TryGetValue(slot, out d)) return d;
            d = Read(slot);
            peekCache[slot] = d;
            return d;
        }

        static SaveData Read(int slot)
        {
            try
            {
                string p = PathOf(slot);
                if (!File.Exists(p)) return null;
                return JsonUtility.FromJson<SaveData>(File.ReadAllText(p, System.Text.Encoding.UTF8));
            }
            catch (System.Exception e) { Debug.LogWarning("セーブを読めませんでした：" + e.Message); return null; }
        }

        public static string Describe(SaveData d)
        {
            if (d == null) return "（空き）";
            return d.loop + "周目 " + d.day + "日目 " + ScheduleMaker.Clock(d.gameMinutes) + "（" + Calendar.SeasonNames[Calendar.Season(d.day)] + "・" + Calendar.WeekdayNames[Calendar.Weekday(d.day)] + "）　銀貨 " + d.money + "　保存：" + d.savedAt;
        }

        // ================= 保存 =================
        public static bool Save(LoungeWorld w, int slot)
        {
            try
            {
                var d = Capture(w);
                Directory.CreateDirectory(Folder);
                File.WriteAllText(PathOf(slot), JsonUtility.ToJson(d), System.Text.Encoding.UTF8);
                peekCache[slot] = d;
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("セーブできませんでした：" + e.Message);
                w.Log("セーブできませんでした（" + e.Message + "）");
                return false;
            }
        }

        static SaveData Capture(LoungeWorld w)
        {
            var d = new SaveData
            {
                savedAt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                loop = Loop.Count, day = w.Day, gameMinutes = w.gameMinutes,
                dormReputation = w.dormReputation,
                money = w.player != null ? w.player.money : 0,
                fSkills = Loop.fSkills, fAbilities = Loop.fAbilities,
                escaped = Loop.Escaped, endless = Loop.EndlessDays,
            };
            if (w.player != null) foreach (var kv in w.player.inventory) if (kv.Value > 0) d.items.Add(new IntPair { key = (int)kv.Key, value = kv.Value });
            foreach (var c in w.characters) d.chars.Add(c.ToSave());
            int n = w.rel.count;
            d.relCount = n;
            d.affinity = new float[n * n];
            for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) d.affinity[i * n + j] = w.rel.Get(i, j);
            d.love = (float[])w.romance.love.Clone();
            d.lover = (bool[])w.romance.lover.Clone();
            d.loveCooldown = (double[])w.romance.cooldownUntil.Clone();
            d.zukan.AddRange(Loop.Zukan);
            d.openedPlaces.AddRange(Loop.OpenedPlaces);
            d.nightTogether.AddRange(Loop.NightTogether);
            d.album.AddRange(Album.Entries);
            d.magic.AddRange(FMagic.Unlocked);
            d.hlog.AddRange(HLog.Entries);
            foreach (var kv in HLog.Total) d.hTotal.Add(new StrInt { key = kv.Key, value = kv.Value });
            foreach (var kv in Story.Done) d.storyDone.Add(new StrInt { key = kv.Key, value = kv.Value });
            d.hands.AddRange(Story.Hands);
            d.storyFlags.AddRange(Story.Flags);
            d.storyPending.AddRange(Story.Pending);
            foreach (var kv in Achievements.Got) d.achievements.Add(new StrStr { key = kv.Key, value = kv.Value });
            foreach (var p in RoomDecor.Placed) d.furniture.Add(new StrInt { key = p.Key, value = (int)p.Value });
            return d;
        }

        // ================= 読み込み =================
        /// <summary>枠を読み込む：ループで残る物を戻してから、寮を作り直す（続きは ApplyPending）。</summary>
        public static bool Load(int slot)
        {
            var d = Read(slot);
            if (d == null) return false;
            Loop.Count = Mathf.Max(1, d.loop);
            Loop.fSkills = d.fSkills != null && d.fSkills.Length > 0 ? d.fSkills : null;
            Loop.fAbilities = d.fAbilities != null && d.fAbilities.Length > 0 ? d.fAbilities : null;
            Loop.Escaped = d.escaped; Loop.EndlessDays = d.endless; Loop.AskChoice = false;
            Fill(Loop.Zukan, d.zukan);
            Loop.OpenedPlaces.Clear(); if (d.openedPlaces != null) Loop.OpenedPlaces.AddRange(d.openedPlaces);
            Fill(Loop.NightTogether, d.nightTogether);
            Album.Entries.Clear(); if (d.album != null) Album.Entries.AddRange(d.album);
            FMagic.Unlocked.Clear(); if (d.magic != null) foreach (var m in d.magic) FMagic.Unlocked.Add(m);
            HLog.Entries.Clear(); if (d.hlog != null) HLog.Entries.AddRange(d.hlog);
            HLog.Total.Clear(); if (d.hTotal != null) foreach (var kv in d.hTotal) HLog.Total[kv.key] = kv.value;
            Story.Done.Clear(); if (d.storyDone != null) foreach (var kv in d.storyDone) Story.Done[kv.key] = kv.value;
            Fill(Story.Hands, d.hands); Fill(Story.Flags, d.storyFlags); Fill(Story.Pending, d.storyPending);
            Story.ActiveLetter = null; Story.ActiveEp = -1; Story.ActiveFinal = false;
            Achievements.Got.Clear(); if (d.achievements != null) foreach (var kv in d.achievements) Achievements.Got[kv.key] = kv.value;
            RoomDecor.Placed.Clear(); if (d.furniture != null) foreach (var kv in d.furniture) RoomDecor.Placed.Add(new KeyValuePair<string, ItemKind>(kv.key, (ItemKind)kv.value));
            Loop.RefreshRewind();
            Pending = d;
            Loop.ReloadScene();
            return true;
        }

        static void Fill(HashSet<string> set, List<string> from) { set.Clear(); if (from != null) foreach (var s in from) set.Add(s); }

        /// <summary>寮を作り直したあと（LoungeWorld.Start）：キャラ・関係・持ち物などを戻す。読み込んだら true。</summary>
        public static bool ApplyPending(LoungeWorld w)
        {
            var d = Pending;
            Pending = null;
            if (d == null) return false;
            w.gameMinutes = d.gameMinutes;
            w.dormReputation = d.dormReputation;
            if (w.player != null)
            {
                w.player.money = d.money;
                w.player.inventory.Clear();
                if (d.items != null) foreach (var it in d.items) w.player.inventory[(ItemKind)it.key] = it.value;
            }
            if (d.chars != null)
                foreach (var cs in d.chars)
                {
                    var c = w.characters.Find(x => x.letter == cs.letter);
                    if (c != null) c.FromSave(cs);
                }
            int n = Mathf.Min(d.relCount, w.rel.count);
            if (d.affinity != null && d.affinity.Length >= d.relCount * d.relCount)
                for (int i = 0; i < n; i++) for (int j = i + 1; j < n; j++) w.rel.SetRaw(i, j, d.affinity[i * d.relCount + j]);
            for (int i = 0; i < w.romance.love.Length; i++)
            {
                if (d.love != null && i < d.love.Length) w.romance.love[i] = d.love[i];
                if (d.lover != null && i < d.lover.Length) w.romance.lover[i] = d.lover[i];
                if (d.loveCooldown != null && i < d.loveCooldown.Length) w.romance.cooldownUntil[i] = d.loveCooldown[i];
            }
            return true;
        }
    }
}
