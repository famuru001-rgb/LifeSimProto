using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 二人きりの時間・自慰の効果音と声（2026-11-05 モーションのチャット。famuru「わかる奴だけあてて」）。
    /// 音は Resources/Sound/H（famuru の音から：名前で分かる物と声。自分で使う範囲だけ・販売などは禁止 → 配るときは差しかえる）。
    /// 名前の頭で種類を分ける：piston_w/m/s（打つ音・弱中強）・wet・wetg（水音）・insert（挿入）・cum（射精）・squirt（潮吹き）・finger・hand・voice_xxx（声の主ごと）。
    /// 鳴らす時刻は「部品の中の割合 u（0〜1）」で表に持つ（claude/SE_効果音と声の計画.md）。
    /// </summary>
    public static class HSound
    {
        public static bool Enabled = true;
        public static float Volume = 0.8f, VoiceVolume = 0.9f;
        /// <summary>ゲームの早送りがこれより速いときは鳴らさない（音が詰まるため）。</summary>
        public static float MaxFastForward = 3f;
        /// <summary>確認の場（F8）では、カメラの位置に関係なく同じ大きさで聞こえるようにする。</summary>
        public static bool Flat;

        struct Ev { public float u; public string key; public float vol, prob; public Ev(float u, string k, float v = 1f, float p = 1f) { this.u = u; key = k; vol = v; prob = p; } }
        static readonly Dictionary<string, Ev[]> Table = new Dictionary<string, Ev[]>
        {
            // 正常位・後背位・側位・立位など（mis）
            { "mis/Deep",    new[] { new Ev(0.33f, "piston"), new Ev(0.02f, "wet", 0.5f, 0.5f) } },
            { "mis/Shallow", new[] { new Ev(0.33f, "piston_w", 0.8f), new Ev(0.02f, "wet", 0.4f, 0.3f) } },
            { "mis/Grind",   new[] { new Ev(0.25f, "wet", 0.7f), new Ev(0.75f, "wet", 0.7f) } },
            { "mis/Finish",  new[] { new Ev(0.12f, "piston_s"), new Ev(0.14f, "cum"), new Ev(0.4f, "wet", 0.5f), new Ev(0.62f, "wet", 0.5f) } },
            // 騎乗位（ride）
            { "ride/Bounce",      new[] { new Ev(0.72f, "piston"), new Ev(0.70f, "wet", 0.4f, 0.5f) } },
            { "ride/BounceSmall", new[] { new Ev(0.72f, "piston_w", 0.8f) } },
            { "ride/Grind",       new[] { new Ev(0.25f, "wet", 0.7f), new Ev(0.75f, "wet", 0.7f) } },
            { "ride/Circle",      new[] { new Ev(0.0f, "wet", 0.6f), new Ev(0.33f, "wet", 0.6f), new Ev(0.66f, "wet", 0.6f) } },
            { "ride/Finish",      new[] { new Ev(0.1f, "wet", 0.6f), new Ev(0.17f, "cum") } },
            // 対面座位（seat）
            { "seat/Rock",      new[] { new Ev(0.35f, "wet", 0.8f), new Ev(0.35f, "piston_w", 0.5f) } },
            { "seat/RockSmall", new[] { new Ev(0.35f, "wet", 0.6f) } },
            { "seat/Bounce",    new[] { new Ev(0.72f, "piston") } },
            { "seat/Circle",    new[] { new Ev(0.0f, "wet", 0.6f), new Ev(0.33f, "wet", 0.6f), new Ev(0.66f, "wet", 0.6f) } },
            { "seat/Finish",    new[] { new Ev(0.15f, "cum") } },
            // 自慰（solo）
            { "solo/Slow",   new[] { new Ev(0.0f, "wet", 0.3f), new Ev(0.5f, "wet", 0.3f) } },
            { "solo/Fast",   new[] { new Ev(0.0f, "wet", 0.3f), new Ev(0.25f, "wet", 0.25f), new Ev(0.5f, "wet", 0.3f), new Ev(0.75f, "wet", 0.25f) } },
            { "solo/Finish", new[] { new Ev(0.3f, "squirt", 0.7f, 0.6f) } },
        };

        // ---------- 音の読み込み ----------
        static Dictionary<string, List<AudioClip>> clips;
        static List<string> voices;
        static void Load()
        {
            if (clips != null) return;
            clips = new Dictionary<string, List<AudioClip>>(); voices = new List<string>();
            foreach (var c in Resources.LoadAll<AudioClip>("Sound/H"))
            {
                string n = c.name, key;
                if (n.StartsWith("voice_")) key = n.Length >= 9 ? n.Substring(0, 9) : n;           // voice_kuo0001 → voice_kuo
                else { int i = n.LastIndexOf('_'); key = i > 0 ? n.Substring(0, i) : n; }           // piston_w_3 → piston_w
                List<AudioClip> l; if (!clips.TryGetValue(key, out l)) clips[key] = l = new List<AudioClip>();
                l.Add(c);
                if (key.StartsWith("voice_") && !voices.Contains(key)) voices.Add(key);
            }
            if (clips.ContainsKey("wetg")) { if (!clips.ContainsKey("wet")) clips["wet"] = new List<AudioClip>(); clips["wet"].AddRange(clips["wetg"]); }
            voices.Sort();
            Debug.Log("HSound: 音 " + clips.Count + " 種類・声 " + voices.Count + " 人分を読み込んだ");
        }
        static readonly Dictionary<string, int> lastPick = new Dictionary<string, int>();
        static AudioClip Pick(string key)
        {
            Load(); List<AudioClip> l;
            if (!clips.TryGetValue(key, out l) || l.Count == 0) return null;
            int last; lastPick.TryGetValue(key, out last);
            int i = Random.Range(0, l.Count); if (l.Count > 1 && i == last) i = (i + 1) % l.Count;
            lastPick[key] = i; return l[i];
        }

        // ---------- 鳴らす ----------
        static readonly List<AudioSource> pool = new List<AudioSource>();
        static Transform root;
        static AudioSource Source()
        {
            if (root == null) { var go = new GameObject("HSound"); Object.DontDestroyOnLoad(go); root = go.transform; pool.Clear(); }
            foreach (var s in pool) if (s != null && !s.isPlaying) return s;
            if (pool.Count < 16)
            {
                var g = new GameObject("src"); g.transform.SetParent(root);
                var s = g.AddComponent<AudioSource>(); s.playOnAwake = false; s.spatialBlend = 0.6f; s.minDistance = 2.5f; s.maxDistance = 40f; s.rolloffMode = AudioRolloffMode.Linear;
                pool.Add(s); return s;
            }
            return pool[Random.Range(0, pool.Count)];
        }
        public static AudioSource Play(string key, Vector3 pos, float vol = 1f, float pitchVar = 0.05f)
        {
            if (!Enabled) return null;
            var c = Pick(key); if (c == null) return null;
            var s = Source(); s.transform.position = pos; s.clip = c; s.spatialBlend = Flat ? 0f : 0.6f; s.volume = Mathf.Clamp01(vol * Volume);
            s.pitch = 1f + Random.Range(-pitchVar, pitchVar); s.Play(); return s;
        }

        static readonly Dictionary<string, float> lastAt = new Dictionary<string, float>();
        /// <summary>部品 part の中で u0→u1 の間に来た音を鳴らす（u1 &lt; u0 はくり返しで1周した）。speed は今の速さ。</summary>
        public static void Events(string form, string part, float u0, float u1, float speed, Vector3 pos)
        {
            if (!Enabled || string.IsNullOrEmpty(form) || string.IsNullOrEmpty(part)) return;
            Ev[] evs; if (!Table.TryGetValue(HMotionBook.Family(form) + "/" + part, out evs)) return;
            foreach (var e in evs)
            {
                bool hit = u1 >= u0 ? (e.u > u0 && e.u <= u1) || (u0 == 0f && e.u == 0f) : (e.u > u0 || e.u <= u1);
                if (!hit || Random.value > e.prob) continue;
                string key = e.key;
                if (key == "piston") key = speed < 0.9f ? "piston_w" : (speed < 1.5f ? "piston_m" : "piston_s");
                float now = Time.unscaledTime, la;
                if (lastAt.TryGetValue(key, out la) && now - la < 0.08f) continue;    // 同じ音が詰まらないように
                lastAt[key] = now;
                Play(key, pos, e.vol);
            }
        }

        /// <summary>キャラの記号 → 声の主（同じ子はいつも同じ声）。声が無い・篤（F）は null。</summary>
        public static string VoiceFor(string letter)
        {
            Load();
            if (string.IsNullOrEmpty(letter) || letter == "F" || voices.Count == 0) return null;
            int h = 0; foreach (char ch in letter + "voice") h = h * 31 + ch;
            return voices[Mathf.Abs(h) % voices.Count];
        }
    }

    /// <summary>女の子の声：場面が進むほど間かくが短く強く。フィニッシュは長い声を1回。声が重ならないようにする。</summary>
    public class HVoice
    {
        float next = 1.5f; int lastPhase = -1; AudioSource cur;
        public void Tick(string letter, int phase, float dt, Vector3 pos)
        {
            if (!HSound.Enabled) return;
            string v = HSound.VoiceFor(letter); if (v == null) return;
            if (phase != lastPhase) { lastPhase = phase; next = phase == 3 ? 0.1f : Mathf.Min(next, 1.5f); }
            if (cur != null && cur.isPlaying && cur.clip != null && cur.clip.name.StartsWith(v)) return;
            next -= dt; if (next > 0f) return;
            float vol = phase <= 0 ? 0.6f : phase == 1 ? 0.75f : 0.9f;
            cur = HSound.Play(v, pos + Vector3.up * 0.4f, vol * HSound.VoiceVolume / Mathf.Max(0.01f, HSound.Volume), 0.02f);
            next = phase <= 0 ? Random.Range(5f, 8f) : phase == 1 ? Random.Range(3f, 5f) : phase == 2 ? Random.Range(1.5f, 3f) : 999f;
        }
    }
}
