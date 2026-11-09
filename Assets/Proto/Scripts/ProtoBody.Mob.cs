using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// ふだんのモブ（寮生以外の学生90人）を新しい3D（制服つき）にする（MOB07。famuru 2026-11-09 20:55 決定）。
    /// ・Resources/Chars/Mob/CharMob（モブ_v2：体3つ・頭・髪3つ・胸ごとのシャツと上着・スカートなど）を使う。先生（mobT〜）は今の仮モデルのまま
    /// ・胸：モブの記号を並べた順で 1・2・3 を3分の1ずつ（MOB05 と同じ決め方。先生も数に入れて並べる）。髪：今の決まりの髪から
    ///   （Hair_Bob→ショート、Hair_HimeSide→セミロング、Hair_Pony→ポニー）。身長：heightCm（頭のてっぺん 162.9cm を合わせる）
    /// ・制服のシャツと上着は、その子の胸に合う物（_Bust1〜3）だけ出す
    /// ・頭（Mob_Head）にも肌の設定（MOB05 で首に色の段差が出たため）
    /// ・上着の「腕を下ろす」補正（Corrective_Arm_Down）を、腕の下がり方（T ポーズから 65度で 1）に合わせて動かす
    /// ・モブの服は制服だけ：お休みの日の私服・水着の場所でも制服のまま（今の仮モデルと同じ。モブはお風呂・寝る所で見えない）
    /// MobNewModel = false で今までどおり（仮モデル）。
    /// </summary>
    public partial class ProtoBody
    {
        public static bool MobNewModel = true;
        public const float MobHeadTopCm = 162.9f;   // モブ_v2 の頭のてっぺん（髪なし、T ポーズ）
        public static float ArmDownFull = 65f;      // この角度まで腕を下ろすと補正が全部（T ポーズから）

        static GameObject mobPrefab;
        static bool mobLooked;
        static List<string> mobOrder;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetMob() { mobPrefab = null; mobLooked = false; mobOrder = null; mobTwoSided.Clear(); }

        public int MobBust { get; private set; }
        public string MobHair { get; private set; }
        readonly List<KeyValuePair<SkinnedMeshRenderer, int>> armDownKeys = new List<KeyValuePair<SkinnedMeshRenderer, int>>();

        static CharProfile ProfileOf(string letter)
        {
            foreach (var p in CharProfiles.All) if (p != null && p.letter == letter) return p;
            return null;
        }

        /// <summary>新しい3Dで出すモブなら、その元の形（無ければ null）。</summary>
        static GameObject MobPrefabFor(string letter, out CharProfile prof)
        {
            prof = null;
            if (!MobNewModel || string.IsNullOrEmpty(letter)) return null;
            var p = ProfileOf(letter);
            if (p == null || !p.mob || p.teacher) return null;
            if (!mobLooked) { mobPrefab = Resources.Load<GameObject>("Chars/Mob/CharMob"); mobLooked = true; }
            if (mobPrefab == null) return null;
            prof = p;
            return mobPrefab;
        }

        /// <summary>胸 1〜3：モブ（先生も含む）の記号を並べた順で 1・2・3 をくり返す（MOB05 と同じ）。</summary>
        public static int MobBustOf(string letter)
        {
            if (mobOrder == null)
            {
                mobOrder = new List<string>();
                foreach (var p in CharProfiles.All) if (p != null && p.mob) mobOrder.Add(p.letter);
                mobOrder.Sort(System.StringComparer.Ordinal);
            }
            int i = mobOrder.IndexOf(letter);
            return i < 0 ? 1 : i % 3 + 1;
        }

        public static string MobHairOf(CharProfile p)
        {
            var parts = p.look != null ? p.look.parts : null;
            if (parts != null)
            {
                if (System.Array.IndexOf(parts, "Hair_Bob") >= 0) return "Hair_Short";
                if (System.Array.IndexOf(parts, "Hair_HimeSide") >= 0) return "Hair_Semi";
                if (System.Array.IndexOf(parts, "Hair_Pony") >= 0) return "Hair_Pony";
            }
            return new[] { "Hair_Short", "Hair_Semi", "Hair_Pony" }[Mathf.Abs(p.mobNo) % 3];
        }

        /// <summary>Setup のあとに呼ぶ：体・髪・制服を、その子の胸と髪だけにする。</summary>
        void ApplyMobLook(CharProfile p)
        {
            int bust = MobBustOf(p.letter);
            string hair = MobHairOf(p);
            MobBust = bust; MobHair = hair;
            string bodyName = "Body_Mob_Bust" + bust, bustTail = "_Bust" + bust;
            foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                string n = r.gameObject.name;
                bool keep;
                if (n.StartsWith("Body_Mob_Bust")) keep = n == bodyName;
                else if (n.StartsWith("Hair_")) keep = n == hair;
                else if (n.StartsWith("Outfit_") && n.Contains("_Bust")) keep = n.EndsWith(bustTail);
                else keep = true;
                if (!keep)
                {
                    r.gameObject.SetActive(false);
                    foreach (var kv in outfits) kv.Value.Remove(r.gameObject);   // 制服をまとめて出すときにも出さない
                    continue;
                }
                // スカートは薄い布で、材質名に DoubleSided が無いので裏が描かれない（前から見るとスカートが消える）：両面にする
                if (n.StartsWith("Outfit_Uniform_Skirt")) MakeTwoSided(r);
                if (n == "Body_MobHead")
                    foreach (var m in r.sharedMaterials) if (m != null && m.shader != null && m.shader.name == "LifeSim/Toon") SetSkinLook(m);
                if (r.sharedMesh != null)
                {
                    int k = r.sharedMesh.GetBlendShapeIndex("Corrective_Arm_Down");
                    if (k >= 0) armDownKeys.Add(new KeyValuePair<SkinnedMeshRenderer, int>(r, k));
                }
            }
            curOutfit = null;   // 次に ShowOutfit したとき、残した物だけで出し直す
        }

        static readonly Dictionary<Material, Material> mobTwoSided = new Dictionary<Material, Material>();

        static void MakeTwoSided(Renderer r)
        {
            var ms = r.sharedMaterials;
            for (int i = 0; i < ms.Length; i++)
            {
                if (ms[i] == null || !ms[i].HasProperty("_Cull")) continue;
                Material m2;
                if (!mobTwoSided.TryGetValue(ms[i], out m2) || m2 == null) { m2 = new Material(ms[i]); m2.name = ms[i].name + "_DoubleSided"; m2.SetFloat("_Cull", 0f); m2.doubleSidedGI = true; mobTwoSided[ms[i]] = m2; }
                ms[i] = m2;
            }
            r.sharedMaterials = ms;
        }

        /// <summary>毎コマ：上着の「腕を下ろす」補正。</summary>
        void UpdateArmDown()
        {
            if (armDownKeys.Count == 0) return;
            float a = ArmDownDeg("Left"), b = ArmDownDeg("Right");
            float w = Mathf.Clamp01((a + b) * 0.5f / ArmDownFull) * 100f;
            foreach (var kv in armDownKeys) if (kv.Key != null && kv.Key.gameObject.activeInHierarchy) kv.Key.SetBlendShapeWeight(kv.Value, w);
        }

        float ArmDownDeg(string side)
        {
            Transform a = B(side + "Arm"), f = B(side + "ForeArm");
            if (a == null || f == null) return 0f;
            Vector3 v = (f.position - a.position).normalized;
            return Mathf.Max(0f, Mathf.Asin(Mathf.Clamp(Vector3.Dot(v, -transform.up), -1f, 1f)) * Mathf.Rad2Deg);
        }
    }
}
