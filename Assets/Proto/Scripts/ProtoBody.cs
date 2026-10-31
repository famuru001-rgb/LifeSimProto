using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace LifeSimProto
{
    public enum BodyPose { Stand, Sit, SitFloor, Lie, LieFloor, Soak }

    /// <summary>キャラごとの見た目（仮）。髪の色・目の色・付ける部品。</summary>
    public class CharLook
    {
        public bool male;
        public Color hair = new Color(0.3f, 0.2f, 0.12f);
        public Color eye = new Color(0.25f, 0.18f, 0.12f);
        public Color skin = new Color(1f, 0.87f, 0.77f);
        public string[] parts = new string[0];

        static CharLook L(Color hair, Color eye, params string[] parts) { return new CharLook { hair = hair, eye = eye, parts = parts }; }

        public static CharLook Get(string name, bool male, Color fallback)
        {
            if (male) return new CharLook { male = true, hair = new Color(0.22f, 0.16f, 0.11f), eye = new Color(0.25f, 0.18f, 0.12f), parts = new[] { "Hair_Short", "Hair_ShortBangs" } };
            switch (name)
            {
                case "A": return L(new Color(0.55f, 0.8f, 1f), new Color(0.75f, 0.2f, 0.3f), "Hair_LongBack", "Acc_Horns");
                case "B": return L(new Color(0.35f, 0.65f, 0.35f), new Color(0.2f, 0.55f, 0.45f), "Hair_LongBack", "Acc_ElfEars");
                case "C": return L(new Color(0.85f, 0.2f, 0.2f), new Color(0.9f, 0.7f, 0.15f), "Hair_Bob", "Acc_CatEars", "Acc_CatTail");
                case "D": return L(new Color(0.08f, 0.08f, 0.1f), new Color(0.3f, 0.15f, 0.1f), "Hair_LongBack", "Hair_HimeSide");
                case "E": return L(new Color(0.95f, 0.8f, 0.35f), new Color(0.2f, 0.4f, 0.85f), "Hair_Twin");
                case "G": return L(new Color(0.82f, 0.84f, 0.9f), new Color(0.5f, 0.3f, 0.7f), "Hair_Bob", "Acc_Glasses");
                case "H": return L(new Color(0.5f, 0.3f, 0.15f), new Color(0.25f, 0.55f, 0.25f), "Hair_Pony");
                case "I": return L(new Color(1f, 0.6f, 0.75f), new Color(0.85f, 0.6f, 0.15f), "Hair_LongBack", "Acc_FoxEars", "Acc_FoxTail");
                case "J": return L(new Color(0.5f, 0.35f, 0.75f), new Color(0.6f, 0.3f, 0.8f));
                case "K": return L(new Color(0.06f, 0.06f, 0.09f), new Color(0.78f, 0.8f, 0.9f), "Hair_LongBack"); // セレネ：黒髪ロング・銀の目
                default: return L(fallback, new Color(0.25f, 0.18f, 0.12f), "Hair_LongBack");
            }
        }
    }

    /// <summary>
    /// 仮モデル（Blenderで作った人型）を表示して動かす。
    /// ・Resources/Motions に Mixamo の動き（Idle, Walk, Sit, Talk, SitTalk）があればそれを再生する
    /// ・ない動きは、骨を直接曲げる簡単な動き（歩く・座る・正座・寝る・お風呂）で代わりにする
    /// ・お風呂ではタオルを巻いた見た目にする
    /// </summary>
    public class ProtoBody : MonoBehaviour
    {
        static readonly string[] AlwaysOn = { "Body", "HeadMesh", "Face", "Tie", "Skirt", "Hair_Cap", "Hair_Bangs" };
        static Dictionary<string, AnimationClip> clipBook;

        SimCharacter sim;
        Transform model;
        CharLook look;
        float height;
        Quaternion fix = Quaternion.identity;
        readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        Transform[] boneList;
        Quaternion[] restRot;
        Vector3 hipsRest;
        Transform hips;

        // 材質（ふだん／お風呂）
        readonly List<Renderer> rends = new List<Renderer>();
        readonly List<Material[]> normalMats = new List<Material[]>();
        readonly List<Material[]> bathMats = new List<Material[]>();
        readonly List<GameObject> hideInBath = new List<GameObject>();
        bool bathShown;

        // 動き
        Animator anim;
        bool humanOk;
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClip curClip;
        float fade = 1f;
        float phase;
        float speed;
        Vector3 lastPos;
        float talkT;
        float measuredHeight, fixAngle;
        bool eatingNow, dozingNow;

        // 確認用（写真を撮るときに姿勢を決め打ちする）
        [System.NonSerialized] public bool testMode;
        [System.NonSerialized] public BodyPose testPose;
        // 床に座る（正座）ときの動き（2026-10-30 モーションのチャット）。空なら今までどおり骨を直接曲げる仮の正座。
        // SitFloorClip＝ゲーム全体の設定（いまは空＝変えていない）。testSitFloorClip＝確認用（null ならゲーム全体の設定に従う）
        // 2026-10-30 famuru「組み合わせ」：座る（SitFloor_Down）→正座（SitFloor）→立つ（SitFloor_Up）。空にすると仮の正座に戻る
        public static string SitFloorClip = "SitFloor";
        // 寝る（ベッド・床）とお風呂の動き（2026-10-30 モーション。Blender で手で付けた姿勢＋呼吸）。空にすると仮の動きに戻る
        // 寝る動きは立った向きのまま手足の形だけを持つ（体を横に倒すのは今まで通りゲームが行う）
        public static string LieClip = "Lie", SoakClip = "Soak";
        enum FloorPhase { None, Down, Seated, Up }
        FloorPhase floorPhase;
        float floorT, dozeW;
        BodyPose lastPose = BodyPose.Stand;
        float upStartAt = -1f;
        static readonly string[] TalkVariants = { "Talk", "Talk2", "Talk3", "Talk4" };
        AnimationClip sceneClip;
        string LetterNow() { return testMode ? testLetter : sim.Letter; }
        string SceneNow() { return testMode ? testScene : sim.MotionScene; }
        string StyleNow() { return MotionBook.StyleOf(LetterNow(), testMode ? false : sim.IsMale); }
        string talkVariant;
        bool wasTalking;
        [System.NonSerialized] public string testSitFloorClip;
        [System.NonSerialized] public bool testStandUp;   // 確認用：床から立ち上がる途中のつもり
        [System.NonSerialized] public string testClip;
        [System.NonSerialized] public string testLetter, testScene, testMood;   // 確認用：キャラの記号・生活の場面・歩くときの気分     // 確認用：姿勢に関係なくこの動きを流す（動きの候補の見比べ。2026-10-30 モーション）
        // ---- 試験用：見た目の腰が歩く速さより速く動いた区間を書き留める（fastwatch 命令。2026-10-31 famuru「椅子に座る直前に高速移動」）----
        Vector3 snapOff, snapStart;    // 見た目の体を、本当の位置からずらしている分（世界の向き）。SnapTime 秒で0へ
        float snapT = 1f, snapDur = 1f;
        bool snapWasPresent;
        const float SnapTime = 0.5f;   // 追いつくまでの秒（いちばん短いとき。長い移動は 1m あたり0.7秒。いちばん速いところで歩く速さくらい）
        public static bool FastWatch;
        public static SimCharacter TraceWho; public static float TraceUntil; public static readonly System.Text.StringBuilder TraceLog = new System.Text.StringBuilder();
        public static readonly System.Text.StringBuilder FastLog = new System.Text.StringBuilder();
        Vector3 fwHips, fwRoot; bool fwHas, fwIn; float fwT, fwDist, fwRootDist; string fwStart;

        string FwInfo() { return (sim != null ? sim.state + "・" + sim.actionText : "") + "・" + (curClip != null ? curClip.name : "なし") + "・切りかえ" + fade.ToString("0.00"); }

        void FastWatchStep(float dt)
        {
            if (hips == null || sim == null) return;
            Vector3 h = hips.position, r = transform.position;
            if (!fwHas || !sim.Present) { fwHips = h; fwRoot = r; fwHas = sim.Present; fwIn = false; return; }
            Vector3 dh = h - fwHips; dh.y = 0f;
            Vector3 dr = r - fwRoot; dr.y = 0f;
            float v = dh.magnitude / dt;
            if (v > 2.5f)
            {
                if (!fwIn) { fwIn = true; fwT = 0f; fwDist = 0f; fwRootDist = 0f; fwStart = FwInfo(); }
                fwT += dt; fwDist += dh.magnitude; fwRootDist += dr.magnitude;
            }
            else if (fwIn)
            {
                fwIn = false;
                if (fwDist > 0.25f && FastLog.Length < 400000)
                    FastLog.AppendLine(LifeSimProto.ScheduleMaker.Clock(LoungeWorld.Instance != null ? LoungeWorld.Instance.Now : 0) + "｜" + sim.displayName + "｜腰 " + fwDist.ToString("0.00") + "m／" + (fwT * 1000f).ToString("0") + "ms（体の位置は " + fwRootDist.ToString("0.00") + "m）｜始め：" + fwStart + "｜終わり：" + FwInfo());
            }
            fwHips = h; fwRoot = r;
        }

        /// <summary>試験用：今の全身の動きの名前と、何秒目か（animdump 命令）。</summary>
        public string DebugClipNow() { if (!graph.IsValid() || curClip == null || !mixer.GetInput(0).IsValid()) return "なし"; double t = mixer.GetInput(0).GetTime(); return curClip.name + " " + t.ToString("0.00") + "秒（長さ " + curClip.length.ToString("0.00") + "・くり返し " + curClip.isLooping + "）"; }
        public void TestSetClipTime(float t) { if (graph.IsValid() && mixer.GetInput(0).IsValid()) mixer.GetInput(0).SetTime(t); }
        public AnimationClip TestFindClip(string n) { LoadClips(); var c = Clip(n); if (c == null) { clipBook = null; LoadClips(); c = Clip(n); } return c; }   // 新しく入れた動きは読み直す
        [System.NonSerialized] public bool testTalk, testEat, testDoze;
        [System.NonSerialized] public float testSpeed;
        [System.NonSerialized] public string testOutfit, testFace;
        [System.NonSerialized] public bool testEyesClosed;
        [System.NonSerialized] public float testBow;              // 確認用：おじぎの角度（度。背骨を前へ曲げる）
        [System.NonSerialized] public float testSkirtSit = -1f;   // 確認用：座りの補正を決め打ち（0〜100。-1 で自動）

        public string DebugInfo
        {
            get
            {
                return (real ? "本番モデル" : "仮モデル") + "あり（人型として読み込み=" + humanOk + "、Mixamoの動き=" + (clipBook != null ? clipBook.Count : 0) +
                       "本、向きの補正=" + Mathf.RoundToInt(fixAngle) + "度、背の高さ=" + measuredHeight.ToString("0.00") + "m、表情=" + FaceKeyList() +
                       "、揺れの骨=" + springs.Count + "本、服=" + OutfitList() + "）";
            }
        }

        // 一瞬の身ぶり（手を振る・喜ぶ）。立っているときだけ、その動きがあれば再生する
        string gestureName;
        float gestureUntil;
        public void Gesture(string clipName, float seconds)
        {
            gestureName = clipName;
            gestureUntil = Time.time + seconds;
        }

        // 本番モデル（Codex 制作。Resources/Chars/（記号）/Char（記号））があればそちらを使う
        bool real;
        public bool IsRealModel { get { return real; } }
        public int SpringCount { get { return springs.Count; } }   // 揺れの骨の数（PerfProbe の数え用）
        public Vector3 HeadPoint { get { return bones["Head"].position + transform.up * 0.07f; } }

        public static ProtoBody Create(GameObject owner, CharLook look, string letter = null)
        {
            GameObject prefab = null;
            bool isReal = false;
            if (!string.IsNullOrEmpty(letter))
            {
                prefab = Resources.Load<GameObject>("Chars/" + letter + "/Char" + letter);
                isReal = prefab != null;
            }
            if (prefab == null) prefab = Resources.Load<GameObject>(look.male ? "ProtoModels/ProtoMale" : "ProtoModels/ProtoFemale");
            if (prefab == null) return null;
            var inst = Instantiate(prefab, owner.transform, false);
            inst.name = "Model";
            var pb = owner.AddComponent<ProtoBody>();
            pb.real = isReal;
            if (!pb.Setup(inst.transform, look))
            {
                Destroy(inst);
                Destroy(pb);
                return null;
            }
            return pb;
        }

        bool Setup(Transform m, CharLook lk)
        {
            model = m;
            look = lk;
            height = lk.male ? 1.82f : 1.70f;
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                int k = n.LastIndexOf(':');
                if (k >= 0) n = n.Substring(k + 1);
                if (!bones.ContainsKey(n)) bones[n] = t;
            }
            foreach (var need in new[] { "Hips", "Spine", "Head", "LeftUpLeg", "LeftLeg", "LeftFoot", "LeftToeBase", "RightUpLeg", "RightLeg", "LeftArm", "LeftForeArm", "RightArm", "RightForeArm" })
            {
                if (!bones.ContainsKey(need)) { Debug.LogWarning("仮モデルに骨 " + need + " がありません"); return false; }
            }
            hips = bones["Hips"];

            // 向きを合わせる（つま先が前を向くように）
            model.localPosition = Vector3.zero;
            Transform owner = model.parent;
            Vector3 toe = owner.InverseTransformPoint(bones["LeftToeBase"].position) - owner.InverseTransformPoint(bones["LeftFoot"].position);
            toe.y = 0f;
            if (toe.sqrMagnitude > 1e-6f)
            {
                float ang = Vector3.SignedAngle(toe, Vector3.forward, Vector3.up);
                fixAngle = ang;
                model.localRotation = Quaternion.Euler(0f, ang, 0f) * model.localRotation;
            }
            fix = model.localRotation;
            // 大きさを確かめる（単位がずれていたら合わせる）
            Transform top = bones.ContainsKey("HeadTop_End") ? bones["HeadTop_End"] : bones["Head"];
            float h = owner.InverseTransformPoint(top.position).y;
            measuredHeight = h;
            // 2026-10-22（取り込みのチャット）：小柄なキャラ（キッカ・ロッテ 135cm、頭の骨 1.13m）まで 1.7m に引き伸ばされていたので、
            // 単位のずれ（100倍・100分の1）のときだけ合わせる
            if (h < 0.5f || h > 3f)
            {
                if (h > 0.01f) model.localScale *= height / h;
                Debug.LogWarning("仮モデルの大きさを調整しました（" + h + "m → " + height + "m）");
            }
            if (owner.InverseTransformPoint(bones["LeftArm"].position).x > 0f)
                Debug.LogWarning("仮モデルの左右が反対になっています");

            // 元に戻す対象は「骨」だけ（モデル全体や部品の置き方は含めない）
            var list = new List<Transform>();
            foreach (var t in hips.GetComponentsInChildren<Transform>(true)) if (!list.Contains(t)) list.Add(t);
            boneList = list.ToArray();
            restRot = new Quaternion[boneList.Length];
            for (int i = 0; i < boneList.Length; i++) restRot[i] = boneList[i].localRotation;
            hipsRest = hips.localPosition;

            if (real) { SetupOutfits(); ApplyToon(); } else SetupLook();
            // 2026-10-27（取り込みのチャット）：本番モデルは表示の範囲（bounds）を毎回計算する。
            // リアの靴が、近くから撮ると消えた（座ったとき・カメラが近いとき。範囲が実際の位置とずれていて、画面の外と判断されていた）
            // 2026-10-30（軽量化のチャット）：毎回の計算（updateWhenOffscreen）は画面外の子の分まで重いので、
            // 代わりに体のまわりを広めに囲んだ範囲を決めておく（靴が消えないよう、腰を中心に背の高さ＋1.2mの箱）
            if (real) SetWideBounds();
            SetupFace();
            SetupSprings();

            anim = model.GetComponent<Animator>();
            if (anim != null)
            {
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                humanOk = anim.avatar != null && anim.avatar.isValid && anim.avatar.isHuman;
                anim.enabled = false;
            }
            LoadClips();
            lastPos = transform.position;
            return true;
        }

        // ================= 見た目 =================
        void SetupLook()
        {
            var keep = new HashSet<string>(AlwaysOn);
            foreach (var p in look.parts) keep.Add(p);
            var cache = new Dictionary<string, Material>();
            Color towel = new Color(0.62f, 0.78f, 0.95f); // 水色のバスタオル
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                string on = r.gameObject.name;
                if (!keep.Contains(on)) { r.gameObject.SetActive(false); continue; }
                if (on == "Tie" || on == "Skirt") hideInBath.Add(r.gameObject);
                var src = r.sharedMaterials;
                var nm = new Material[src.Length];
                var bm = new Material[src.Length];
                for (int i = 0; i < src.Length; i++)
                {
                    string key = src[i] != null ? src[i].name.Replace(" (Instance)", "") : "Skin";
                    nm[i] = MatFor(cache, key, ColorFor(key), key == "Hair" || key == "Skirt");
                    Color bc = BathColor(key, towel);
                    bm[i] = bc == ColorFor(key) ? nm[i] : MatFor(cache, "bath_" + key, bc, false);
                }
                r.sharedMaterials = nm;
                rends.Add(r);
                normalMats.Add(nm);
                bathMats.Add(bm);
            }
        }

        Color ColorFor(string key)
        {
            switch (key)
            {
                case "Skin": return look.skin;
                case "Hip": return new Color(0.11f, 0.11f, 0.14f); // スカートの中は黒（見えても大丈夫な色）
                case "Hair": return look.hair;
                case "Eye": return look.eye;
                case "Blazer": case "Sleeve": return new Color(0.09f, 0.09f, 0.12f);
                case "Shirt": return new Color(0.95f, 0.95f, 0.96f);
                case "Tie": return new Color(0.6f, 0.1f, 0.16f);
                case "Skirt": return new Color(0.11f, 0.11f, 0.14f);
                case "Socks": return new Color(0.06f, 0.06f, 0.07f);
                case "Shoes": return new Color(0.42f, 0.25f, 0.12f);
                case "Pants": return new Color(0.15f, 0.15f, 0.18f);
                case "Mouth": return new Color(0.8f, 0.45f, 0.45f);
                case "Horn": return new Color(0.1f, 0.1f, 0.13f);
                case "Glasses": return new Color(0.2f, 0.15f, 0.1f);
                default: return new Color(0.8f, 0.8f, 0.8f);
            }
        }

        Color BathColor(string key, Color towel)
        {
            if (look.male)
            {
                switch (key)
                {
                    case "Blazer": case "Sleeve": case "Shirt": case "Socks": case "Shoes": return look.skin;
                    case "Pants": return towel;
                }
            }
            else
            {
                switch (key)
                {
                    case "Blazer": case "Shirt": case "Hip": return towel;
                    case "Sleeve": case "Socks": case "Shoes": return look.skin;
                }
            }
            return ColorFor(key);
        }

        static Material MatFor(Dictionary<string, Material> cache, string key, Color c, bool twoSided)
        {
            Material m;
            if (cache.TryGetValue(key, out m)) return m;
            m = ProtoArt.MatNew(c, null, null, key == "Eye" ? 0.6f : 0.2f);   // あとで両面にするので使い回さない
            m.name = key;
            if (twoSided && m.HasProperty("_Cull")) { m.SetFloat("_Cull", 0f); m.doubleSidedGI = true; }
            cache[key] = m;
            return m;
        }

        void ShowBath(bool on)
        {
            if (on == bathShown) return;
            bathShown = on;
            for (int i = 0; i < rends.Count; i++) rends[i].sharedMaterials = on ? bathMats[i] : normalMats[i];
            foreach (var g in hideInBath) g.SetActive(!on);
        }

        // ================= 動き（Mixamo） =================
        static void LoadClips()
        {
            if (clipBook != null) return;
            clipBook = new Dictionary<string, AnimationClip>();
            foreach (var c in Resources.LoadAll<AnimationClip>("Motions"))
            {
                if (c == null || c.name.StartsWith("__preview__")) continue;
                if (!clipBook.ContainsKey(c.name)) clipBook[c.name] = c;
            }
            if (clipBook.Count > 0) Debug.Log("動きを読み込みました：" + string.Join(", ", new List<string>(clipBook.Keys).ToArray()));
        }

        AnimationClip Clip(string name)
        {
            AnimationClip c;
            return clipBook != null && clipBook.TryGetValue(name, out c) ? c : null;
        }

        void PlayClip(AnimationClip c)
        {
            if (!graph.IsValid())
            {
                graph = PlayableGraph.Create(name + "_Motion");
                graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
                var output = AnimationPlayableOutput.Create(graph, "Out", anim);
                mixer = AnimationMixerPlayable.Create(graph, 2);
                // 2段重ね：下の段＝全身の動き、上の段＝腕だけの身ぶり（歩きながら手を振るため）
                layers = AnimationLayerMixerPlayable.Create(graph, 3);
                graph.Connect(mixer, 0, layers, 0);
                layers.SetInputWeight(0, 1f);
                layers.SetLayerMaskFromAvatarMask(1, ArmsMask());
                layers.SetLayerMaskFromAvatarMask(2, TalkUpperMask());   // 3段目：正座で話すときの上半身（2026-10-30 モーション）
                output.SetSourcePlayable(layers);
                graph.Play();
            }
            if (!anim.enabled) { anim.enabled = true; anim.Rebind(); }
            if (c == curClip) return;
            var oldIn = mixer.GetInput(0);
            var olderIn = mixer.GetInput(1);
            graph.Disconnect(mixer, 0);
            graph.Disconnect(mixer, 1);
            if (olderIn.IsValid()) olderIn.Destroy();
            var p = AnimationClipPlayable.Create(graph, c);
            RandomStart(p, c);
            graph.Connect(p, 0, mixer, 0);
            if (oldIn.IsValid() && curClip != null) graph.Connect(oldIn, 0, mixer, 1);
            else if (oldIn.IsValid()) oldIn.Destroy();
            mixer.SetInputWeight(0, curClip == null ? 1f : 0f);
            mixer.SetInputWeight(1, curClip == null ? 0f : 1f);
            fade = curClip == null ? 1f : 0f;
            curClip = c;
        }

        /// <summary>
        /// くり返しの動き（歩く・立つ・座る・場面の動き）は、始める位置をばらばらにする。
        /// 0秒目から始めると、同じ時に始めた子どうしの動きがそろって見えた（2026-10-31 famuru「モーションが同期している」）。
        /// 1回だけの動き（座る・立ち上がる・身ぶり）と、2人で合わせる二人きりの時間の動き（H_）は0秒目から。
        /// </summary>
        static void RandomStart(AnimationClipPlayable p, AnimationClip c)
        {
            if (c == null || !c.isLooping || c.length < 0.5f || c.name.StartsWith("H_")) return;
            p.SetTime(Random.Range(0f, c.length));
        }

        // ---- 腕だけの身ぶり（上の段） ----
        AnimationLayerMixerPlayable layers;
        AnimationClipPlayable upperPlay;
        AnimationClip upperClip;
        float upperW;
        static AvatarMask armsMask;

        static AvatarMask ArmsMask()
        {
            if (armsMask != null) return armsMask;
            armsMask = new AvatarMask();
            for (var part = AvatarMaskBodyPart.Root; part < AvatarMaskBodyPart.LastBodyPart; part++)
            {
                bool arm = part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm ||
                           part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers;
                armsMask.SetHumanoidBodyPartActive(part, arm);
            }
            return armsMask;
        }

        // ---- 正座で話すときの上半身（3段目。腕・指・頭だけ。腰と背中は正座のまま） ----
        AnimationClipPlayable talkPlay;
        AnimationClip talkClip;
        float talkW;
        static AvatarMask talkMask;

        static AvatarMask TalkUpperMask()
        {
            if (talkMask != null) return talkMask;
            talkMask = new AvatarMask();
            for (var part = AvatarMaskBodyPart.Root; part < AvatarMaskBodyPart.LastBodyPart; part++)
            {
                bool on = part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm ||
                          part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers || part == AvatarMaskBodyPart.Head;
                talkMask.SetHumanoidBodyPartActive(part, on);
            }
            return talkMask;
        }

        void SetTalkUpper(AnimationClip want, float dt)
        {
            if (!graph.IsValid()) return;
            if (want != null && want != talkClip)
            {
                if (talkPlay.IsValid()) { graph.Disconnect(layers, 2); talkPlay.Destroy(); }
                talkPlay = AnimationClipPlayable.Create(graph, want);
                RandomStart(talkPlay, want);
                graph.Connect(talkPlay, 0, layers, 2);
                talkClip = want;
            }
            talkW = Mathf.MoveTowards(talkW, want != null ? 1f : 0f, dt / 0.4f);
            if (talkPlay.IsValid()) layers.SetInputWeight(2, talkW);
            if (want == null && talkW <= 0f && talkPlay.IsValid())
            {
                graph.Disconnect(layers, 2);
                talkPlay.Destroy();
                talkClip = null;
            }
        }

        void SetUpper(AnimationClip want, float dt)
        {
            if (!graph.IsValid()) return;
            if (want != null && want != upperClip)
            {
                if (upperPlay.IsValid()) { graph.Disconnect(layers, 1); upperPlay.Destroy(); }
                upperPlay = AnimationClipPlayable.Create(graph, want);
                RandomStart(upperPlay, want);
                graph.Connect(upperPlay, 0, layers, 1);
                upperClip = want;
            }
            upperW = Mathf.MoveTowards(upperW, want != null ? 1f : 0f, dt / 0.2f);
            if (upperPlay.IsValid()) layers.SetInputWeight(1, upperW);
            if (want == null && upperW <= 0f && upperPlay.IsValid())
            {
                graph.Disconnect(layers, 1);
                upperPlay.Destroy();
                upperClip = null;
            }
        }

        void StopClips()
        {
            if (anim != null && anim.enabled) anim.enabled = false;
            curClip = null;
        }

        void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
        }

        // ================= 毎フレーム =================
        void LateUpdate()
        {
            long tp = PerfProbe.Stamp();   // 重さを測る（PerfProbe.cs。2026-10-30 軽量化のチャット）
            try { LateUpdateCore(); } finally { PerfProbe.Add(PerfProbe.Body, tp); }
        }

        void LateUpdateCore()
        {
            if (model == null) return;
            if (sim == null && !testMode) sim = GetComponent<SimCharacter>();
            if (sim == null && !testMode) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 d = transform.position - lastPos;
            d.y = 0f;
            lastPos = transform.position;
            // 座る・立つ・ベッドに入るときの一瞬の位置の移動（0.3〜2.5m）は、見た目だけ少しずつ追いつかせる（2026-10-31 famuru「椅子に座る直前に高速移動」）
            bool appeared = sim != null && sim.Present && !snapWasPresent;   // 見えなかった子が出てきた（登校・授業から戻る）
            snapWasPresent = sim == null || sim.Present;
            bool snapped = !appeared && d.magnitude > 0.3f && d.magnitude < 2.5f && d.magnitude / dt > 4f;
            if (snapped) { snapStart = snapOff - d; snapT = 0f; snapDur = Mathf.Max(SnapTime, snapStart.magnitude * 0.7f); }
            else if (appeared || d.magnitude >= 2.5f) { snapStart = Vector3.zero; snapT = snapDur; } // 遠くへの移動（見えなくなる・出てくる）はそのまま
            snapT = Mathf.Min(snapDur, snapT + dt);
            float sk = snapT / snapDur;
            snapOff = snapStart * (1f - sk * sk * (3f - 2f * sk)); // なめらかに（始めと終わりはゆっくり）
            if (!snapped) speed = Mathf.Lerp(speed, Mathf.Min(d.magnitude / dt, 3f), Mathf.Clamp01(dt * 10f));

            BodyPose pose = testMode ? testPose : sim.BodyPoseNow;
            string outfit = testMode ? testOutfit : sim.OutfitNow;
            if (string.IsNullOrEmpty(outfit)) outfit = pose == BodyPose.Soak ? "Towel" : "Uniform";
            bool talking = testMode ? testTalk : sim.IsTalkingNow;
            eatingNow = testMode ? testEat : sim.IsEatingNow;
            dozingNow = testMode ? testDoze : sim.IsDozingNow;
            if (testMode) speed = testSpeed;
            if (real) ShowOutfit(outfit); else ShowBath(outfit == "Towel");

            // 体の置き方（寝るときは横にする）
            Quaternion frame = Quaternion.identity;
            if (pose == BodyPose.Lie || pose == BodyPose.LieFloor)
            {
                frame = Quaternion.Euler(0f, 180f, 0f) * Quaternion.Euler(-90f, 0f, 0f);
                model.localRotation = frame * fix;
                model.localPosition = new Vector3(0f, pose == BodyPose.Lie ? 0.64f : 0.2f, -height * 0.5f);
            }
            else
            {
                model.localRotation = fix;
                model.localPosition = Vector3.zero;
            }
            if (snapOff != Vector3.zero) model.localPosition += Quaternion.Inverse(transform.rotation) * snapOff;

            // Mixamo の動きがあれば使う
            AnimationClip clip = null;
            if (humanOk)
            {
                // 床から立ち上がる途中（SimCharacter がその場で止めている間）：立つ動きを1回流す
                if (testMode && !string.IsNullOrEmpty(testClip)) clip = Clip(testClip);
                AnimationClip upC = clip != null ? null : !string.IsNullOrEmpty(SitFloorClip) && (testMode ? testStandUp : sim.StandingUpNow) ? Clip(SitFloorClip + "_Up") : null;
                if (clip != null) { }
                else if (pose == BodyPose.Stand && upC != null)
                {
                    if (floorPhase != FloorPhase.Up)
                    {
                        // 座る途中だったら、同じ高さに近い所から立ち始める
                        var dn = Clip(SitFloorClip + "_Down");
                        upStartAt = floorPhase == FloorPhase.Down && dn != null ? Mathf.Max(0f, upC.length - floorT) : 0f;
                        floorPhase = FloorPhase.Up;
                    }
                    clip = upC;
                }
                else if (pose == BodyPose.Stand)
                {
                    if (floorPhase == FloorPhase.Up) floorPhase = FloorPhase.None;
                    // 手を振るのは腕だけ（上の段）。喜ぶ（ジャンプ）は全身
                    // 気持ちの身ぶりは、立ち止まっているときは全身で（歩いている間は上半身だけ。喜ぶは今まで通り全身）
                    // 手を振る・おじぎは、立ち止まっていればキャラのらしさの全身の動き（MotionBook。2026-10-30 モーション）
                    AnimationClip fullG = null;
                    if (gestureName != null && Time.time < gestureUntil && speed <= 0.25f)
                    {
                        if (gestureName == "Wave") fullG = Clip(MotionBook.Bye(StyleNow()));
                        else if (gestureName == "Bow") fullG = Clip(MotionBook.Bow(StyleNow()));
                    }
                    if (fullG != null) clip = fullG;
                    else if (gestureName != null && gestureName != "Wave" && gestureName != "Bow" && Time.time < gestureUntil && Clip(gestureName) != null && (gestureName == "Joy" || speed <= 0.25f)) clip = Clip(gestureName);
                    else if (speed > 0.25f) clip = Clip(MotionBook.Walk(StyleNow(), testMode ? testMood : sim.WalkMood)) ?? Clip(MotionBook.Walk(StyleNow(), null)) ?? Clip("Walk");
                    else if (talking)
                    {
                        // 話す身ぶりは会話ごとに Talk・Talk2〜4 から選ぶ（2026-10-30 モーション）
                        if (!wasTalking || talkVariant == null) talkVariant = TalkVariants[Random.Range(0, TalkVariants.Length)];
                        clip = Clip(talkVariant) ?? Clip("Talk") ?? Clip("Idle");
                    }
                    else if ((sceneClip = MotionBook.StandClip(SceneNow(), LetterNow(), Clip)) != null) clip = sceneClip;   // 生活の場面（料理・魔法の練習など）
                    else if (!testMode && sim.FaceNow == "Sad" && Clip("SadIdle") != null) clip = Clip("SadIdle");   // 気分が沈んでいるときの立ち方
                    else clip = Clip(MotionBook.Idle(LetterNow())) ?? Clip("Idle");
                }
                else if (pose == BodyPose.Sit)
                {
                    clip = talking ? (Clip("SitTalk") ?? Clip("Sit")) : Clip("Sit");
                }
                else if ((pose == BodyPose.Lie || pose == BodyPose.LieFloor) && !string.IsNullOrEmpty(LieClip))
                {
                    clip = Clip(LieClip);
                }
                else if (pose == BodyPose.Soak && !string.IsNullOrEmpty(SoakClip))
                {
                    clip = Clip(SoakClip);
                }
                else if (pose == BodyPose.SitFloor)
                {
                    if (testMode && testSitFloorClip != null) { if (testSitFloorClip != "") clip = Clip(testSitFloorClip); }
                    else if (!string.IsNullOrEmpty(SitFloorClip) && Clip(SitFloorClip) != null)
                    {
                        // 座った瞬間は「座る」を1回流し、終わる少し前から正座のくり返しへ（つなぎ目は PlayClip のなめらかな切りかえ）
                        var dn = Clip(SitFloorClip + "_Down");
                        if (lastPose != BodyPose.SitFloor) { floorPhase = dn != null ? FloorPhase.Down : FloorPhase.Seated; floorT = 0f; }
                        floorT += dt;
                        if (floorPhase == FloorPhase.Down && (dn == null || floorT >= dn.length - 0.3f)) floorPhase = FloorPhase.Seated;
                        if (floorPhase != FloorPhase.Down) floorPhase = FloorPhase.Seated;
                        clip = floorPhase == FloorPhase.Down ? dn : Clip(SitFloorClip);
                    }
                }
            }
            if (clip != null)
            {
                PlayClip(clip);
                if (fade < 1f)
                {
                    fade = Mathf.Min(1f, fade + dt / 0.25f);
                    mixer.SetInputWeight(0, fade);
                    mixer.SetInputWeight(1, 1f - fade);
                }
                if (upStartAt >= 0f && clip == curClip && mixer.GetInput(0).IsValid()) { mixer.GetInput(0).SetTime(upStartAt); upStartAt = -1f; }
                bool waving = pose == BodyPose.Stand && gestureName == "Wave" && Time.time < gestureUntil && clip != Clip(MotionBook.Bye(StyleNow()));
                SetUpper(waving ? Clip("Wave") : null, dt);
                // 正座で話す：Mixamo の「座って話す」の上半身（腕・頭）だけを重ねる（famuru「上半身だけ使う」）
                bool floorTalk = pose == BodyPose.SitFloor && floorPhase == FloorPhase.Seated && talking && !dozingNow;
                // 気持ちの身ぶりを全身で流せないとき（座っている・歩いている）は、上半身（腕・頭）だけ重ねる
                bool gOn = gestureName != null && gestureName != "Wave" && gestureName != "Joy" && Time.time < gestureUntil && Clip(gestureName) != null;
                bool gUpper = gOn && (pose == BodyPose.Sit || (pose == BodyPose.SitFloor && floorPhase == FloorPhase.Seated) || (pose == BodyPose.Stand && speed > 0.25f));
                // 座って行う生活の場面（食べる・お茶・読書・勉強）は、話していなければ上半身だけ重ねる
                AnimationClip seatedAct = null;
                if (!talking && !dozingNow && (pose == BodyPose.Sit || (pose == BodyPose.SitFloor && floorPhase == FloorPhase.Seated)))
                    seatedAct = MotionBook.SeatedClip(SceneNow(), LetterNow(), Clip);
                SetTalkUpper(gUpper ? Clip(gestureName) : (floorTalk ? Clip("SitTalk") : seatedAct), dt);
            }
            else
            {
                StopClips();
                Procedural(pose, talking, dt, transform.rotation * frame);
            }
            // 正座で居眠り：首を前に落とす（動きの上から。2026-10-30 モーション）
            dozeW = Mathf.MoveTowards(dozeW, clip != null && pose == BodyPose.SitFloor && dozingNow ? 1f : 0f, dt / 0.8f);
            if (dozeW > 0f)
            {
                Transform nk = B("Neck") ?? B("Head");
                if (nk != null) nk.rotation = Quaternion.AngleAxis(28f * dozeW, transform.right) * nk.rotation;
            }
            if (pose != BodyPose.SitFloor && floorPhase != FloorPhase.Up) floorPhase = FloorPhase.None;
            lastPose = pose;
            wasTalking = talking;
            if (testMode && Mathf.Abs(testBow) > 0.01f)   // 確認用のおじぎ（ゲームの動きには無い。背骨3本に分けて前へ曲げる）
                foreach (var bn in new[] { "Spine", "Spine1", "Spine2" })
                {
                    var b = B(bn);
                    if (b != null) b.rotation = Quaternion.AngleAxis(testBow / 3f, transform.right) * b.rotation;
                }
            bool eyesClosed = testMode ? testEyesClosed : sim.EyesClosedNow;
            bool speaking = testMode ? testTalk : sim.IsSpeakingNow;
            string face = testMode ? testFace : sim.FaceNow;
            UpdateFace(dt, eyesClosed, speaking, face);
            UpdateCorrectives(dt, pose);
            if (FastWatch) FastWatchStep(dt);
            if (TraceWho == sim && sim != null && Time.time < TraceUntil && hips != null && TraceLog.Length < 2000000)
            {
                Vector3 hl = transform.InverseTransformPoint(hips.position);
                double ct = graph.IsValid() && mixer.GetInput(0).IsValid() ? mixer.GetInput(0).GetTime() : -1;
                TraceLog.AppendLine(Time.time.ToString("0.000") + "\t" + dt.ToString("0.0000") + "\t" + transform.position.x.ToString("0.000") + "\t" + transform.position.z.ToString("0.000") + "\t" + hl.x.ToString("0.000") + "\t" + hl.y.ToString("0.000") + "\t" + hl.z.ToString("0.000") + "\t" + hips.position.x.ToString("0.000") + "\t" + hips.position.z.ToString("0.000") + "\t" + (curClip != null ? curClip.name : "-") + "\t" + ct.ToString("0.000") + "\t" + fade.ToString("0.00") + "\t" + sim.state + "\t" + sim.actionText);
            }
            // 2026-10-30（軽量化のチャット）：揺れは画面に映っている子だけ計算する（映っていない子はそのまま止め、映ったら元の形から始める）
            if (!PerfProbe.NoSway)
            {
                if (AnyVisible() && BigOnScreen()) { long ts = PerfProbe.Stamp(); UpdateSprings(dt, springsAsleep); springsAsleep = false; PerfProbe.Add(PerfProbe.Sway, ts); }
                else springsAsleep = true;
            }
        }

        // ================= トゥーン表示（本番モデル） =================
        // 本番キャラの材質を、アニメ調の陰影と輪郭線の表示（Resources/Shaders/LifeSimToon.shader）に置き換える。
        // false にすると、元の表示（写実的な陰影）に戻る。
        public static bool ToonEnabled = true;
        static Shader toonShader;
        static bool toonLooked;
        static readonly Dictionary<Material, Material> toonCache = new Dictionary<Material, Material>();
        // 顔の部品（Face_）用：輪郭線なし（目や口の部品の縁に線が出るため）
        static readonly Dictionary<Material, Material> toonCacheNoLine = new Dictionary<Material, Material>();

        // 再生のたびに作り直す（再生終了で材質が消えるため。前回の材質を使うとピンク色になる）
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetToon() { toonCache.Clear(); toonCacheNoLine.Clear(); toonShader = null; toonLooked = false; }

        // 2026-10-28 肌の質感：肌の材質だけ、影をやわらかく赤みのある色にし、境目の赤み・ふちの光・ほんのりツヤを足す。
        // false にすると、肌も服と同じ表示に戻る。
        public static bool SkinLookEnabled = true;
        static readonly string[] skinWords = { "Skin", "Face", "Body", "Hands", "Thigh" };
        static readonly string[] notSkinWords = { "Detail", "Eyes", "Mouth", "Uniform", "Hair", "Underwear", "Bra", "Shorts", "Shoes", "Expression" };

        static bool IsSkinMat(string name)
        {
            foreach (var w in notSkinWords) if (name.Contains(w)) return false;
            foreach (var w in skinWords) if (name.Contains(w)) return true;
            return false;
        }

        static void SetSkinLook(Material t)
        {
            t.SetColor("_ShadeColor", new Color(0.90f, 0.72f, 0.72f, 1f)); // 影を赤みのある明るめの色に
            t.SetFloat("_ShadeSoftness", 0.16f);                           // 影の境目をふんわり
            t.SetFloat("_ScatterStrength", 0.32f);
            t.SetFloat("_ScatterWidth", 0.38f);
            t.SetFloat("_RimStrength", 0.22f);
            t.SetFloat("_RimPower", 3.2f);
            t.SetFloat("_GlossStrength", 0.05f);
            t.SetFloat("_GlossPower", 10f);
        }

        void ApplyToon()
        {
            if (!ToonEnabled) return;
            if (!toonLooked)
            {
                toonLooked = true;
                toonShader = Shader.Find("LifeSim/Toon");
                if (toonShader == null || !toonShader.isSupported) { Debug.LogWarning("トゥーン表示のシェーダーが使えません"); toonShader = null; }
            }
            if (toonShader == null) return;
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var ms = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < ms.Length; i++)
                {
                    var src = ms[i];
                    if (src == null) continue;
                    Material t;
                    if (!toonCache.TryGetValue(src, out t) || t == null)
                    {
                        t = new Material(toonShader);
                        t.name = src.name + "_Toon";
                        Texture tex = src.HasProperty("_BaseMap") ? src.GetTexture("_BaseMap") : (src.HasProperty("_MainTex") ? src.GetTexture("_MainTex") : null);
                        if (tex != null) t.SetTexture("_BaseMap", tex);
                        if (src.HasProperty("_BaseMap"))
                        {
                            t.SetTextureScale("_BaseMap", src.GetTextureScale("_BaseMap"));
                            t.SetTextureOffset("_BaseMap", src.GetTextureOffset("_BaseMap"));
                        }
                        Color c = src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor") : (src.HasProperty("_Color") ? src.GetColor("_Color") : Color.white);
                        // 口の中など画像のない材質は、Blender の色（リニア）がそのまま入って暗くなるので、見た目の色に直す
                        if (tex == null && src.name.Contains("Mouth")) c = c.gamma;
                        t.SetColor("_BaseColor", c);
                        float cull = src.HasProperty("_Cull") ? src.GetFloat("_Cull") : 2f;
                        if (src.name.Contains("DoubleSided")) cull = 0f;
                        t.SetFloat("_Cull", cull);
                        if (SkinLookEnabled && IsSkinMat(src.name)) SetSkinLook(t);
                        toonCache[src] = t;
                    }
                    if (r.gameObject.name.StartsWith("Face_"))
                    {
                        // 顔の部品は影を落とさない（顔の上に細い線のような影が出るため）
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        Material t2;
                        if (!toonCacheNoLine.TryGetValue(src, out t2) || t2 == null)
                        {
                            t2 = new Material(t);
                            t2.name = src.name + "_ToonNoLine";
                            t2.SetShaderPassEnabled("SRPDefaultUnlit", false);
                            toonCacheNoLine[src] = t2;
                        }
                        t = t2;
                    }
                    ms[i] = t;
                    changed = true;
                }
                if (changed) r.sharedMaterials = ms;
            }
        }

        // ================= 着替え（本番モデル） =================
        readonly Dictionary<string, List<GameObject>> outfits = new Dictionary<string, List<GameObject>>();
        string curOutfit;

        void SetupOutfits()
        {
            // 材質名に DoubleSided を含むもの（スカートなど薄い布）は裏側も表示する
            var twoSided = new Dictionary<Material, Material>();
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var ms = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < ms.Length; i++)
                {
                    if (ms[i] == null || !ms[i].name.Contains("DoubleSided") || !ms[i].HasProperty("_Cull")) continue;
                    Material m2;
                    if (!twoSided.TryGetValue(ms[i], out m2)) { m2 = new Material(ms[i]); m2.SetFloat("_Cull", 0f); m2.doubleSidedGI = true; twoSided[ms[i]] = m2; }
                    ms[i] = m2;
                    changed = true;
                }
                if (changed) r.sharedMaterials = ms;
            }
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                string n = r.gameObject.name;
                if (n == "Face_Blush") continue;
                if (!n.StartsWith("Outfit_")) continue;
                string rest = n.Substring(7);
                int k = rest.IndexOf('_');
                string key = k >= 0 ? rest.Substring(0, k) : rest;
                List<GameObject> l;
                if (!outfits.TryGetValue(key, out l)) { l = new List<GameObject>(); outfits[key] = l; }
                l.Add(r.gameObject);
            }
            curOutfit = null;
            ShowOutfit("Uniform");
        }

        void ShowOutfit(string name)
        {
            if (outfits.Count == 0) return;
            if (!outfits.ContainsKey(name)) name = outfits.ContainsKey("Uniform") ? "Uniform" : null;
            if (name == null || name == curOutfit) return;
            curOutfit = name;
            foreach (var kv in outfits)
                foreach (var g in kv.Value) g.SetActive(kv.Key == name);
        }

        string OutfitList()
        {
            if (!real) return "仮（制服・タオル）";
            if (outfits.Count == 0) return "なし";
            return string.Join("・", new List<string>(outfits.Keys).ToArray());
        }

        // ================= 表情（シェイプキー） =================
        static readonly string[] FaceKeys = { "Blink", "Smile", "MouthOpen", "Sad", "Angry", "Surprised" };
        readonly Dictionary<string, List<KeyValuePair<SkinnedMeshRenderer, int>>> faceKeys = new Dictionary<string, List<KeyValuePair<SkinnedMeshRenderer, int>>>();
        readonly Dictionary<string, float> faceNow = new Dictionary<string, float>();
        GameObject blush;
        float blinkTimer = 3f, blinkT = -1f, mouthT;

        void SetupFace()
        {
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.gameObject.name == "Face_Blush") { blush = smr.gameObject; blush.SetActive(false); }
                var mesh = smr.sharedMesh;
                if (mesh == null) continue;
                foreach (var key in FaceKeys)
                {
                    int idx = mesh.GetBlendShapeIndex(key);
                    if (idx < 0) continue;
                    List<KeyValuePair<SkinnedMeshRenderer, int>> l;
                    if (!faceKeys.TryGetValue(key, out l)) { l = new List<KeyValuePair<SkinnedMeshRenderer, int>>(); faceKeys[key] = l; }
                    l.Add(new KeyValuePair<SkinnedMeshRenderer, int>(smr, idx));
                }
            }
            foreach (var key in FaceKeys) faceNow[key] = 0f;
            SetupFaceAtlas();
            // 形の補正（Corrective_ で始まるシェイプキー）。例：座ったときのスカート
            skirtSitKeys.Clear();
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = smr.sharedMesh;
                if (mesh == null) continue;
                int idx = mesh.GetBlendShapeIndex("Corrective_Skirt_Sit");
                if (idx >= 0) skirtSitKeys.Add(new KeyValuePair<SkinnedMeshRenderer, int>(smr, idx));
            }
        }

        // ================= 形の補正（座ったときのスカート） =================
        readonly List<KeyValuePair<SkinnedMeshRenderer, int>> skirtSitKeys = new List<KeyValuePair<SkinnedMeshRenderer, int>>();
        float skirtSitNow;

        void UpdateCorrectives(float dt, BodyPose pose)
        {
            if (skirtSitKeys.Count == 0) return;
            float target = pose == BodyPose.Sit ? 100f : 0f;
            skirtSitNow = Mathf.MoveTowards(skirtSitNow, target, 400f * Mathf.Min(dt, 0.1f));
            if (testMode && testSkirtSit >= 0f) skirtSitNow = testSkirtSit;
            foreach (var kv in skirtSitKeys) kv.Key.SetBlendShapeWeight(kv.Value, skirtSitNow);
        }

        string FaceKeyList()
        {
            if (faceKeys.Count == 0) return "なし";
            return string.Join("・", new List<string>(faceKeys.Keys).ToArray());
        }

        void SetKey(string key, float target, float dt, float rate)
        {
            float v = Mathf.MoveTowards(faceNow[key], target, rate * dt);
            faceNow[key] = v;
            List<KeyValuePair<SkinnedMeshRenderer, int>> l;
            if (!faceKeys.TryGetValue(key, out l)) return;
            // 目・口を絵で切り替えるキャラは、その部分のシェイプキーは動かさない（二重に動かないように）
            bool byAtlas = key == "MouthOpen" ? atlasMouth.Count > 0 : atlasEyes.Count > 0;
            foreach (var kv in l) kv.Key.SetBlendShapeWeight(kv.Value, byAtlas ? 0f : v);
        }

        // ================= 目・口の絵の切り替え（2026-10-26 famuru 決定の案3。材質名に FaceEyes／FaceMouth があるキャラだけ） =================
        // 表情ごとの絵を並べた1枚の画像（アトラス）を、材質の絵の位置をずらして切り替える。無いキャラは今までのシェイプキーのまま。
        // 並び（リア_表情の make_atlas_v2.py と同じ。左上が0、右へ・下へ）：
        //   目（3列×3段、1こま 680×456／2048×2048）：0ふつう 1閉じ 2笑い目 3半目 4悲しい 5怒り 6驚き 7悲しい閉じ 8怒り閉じ
        //   口（4列×3段、1こま 512×384／2048×1152）：0ふつう 1口パク 2笑顔 3笑顔で開く 4悲しい 5悲しく開く 6怒り 7怒って開く 8驚き 9驚いて開く
        // モデルの UV は左上のこま（0）に合わせてある。戻すときは材質を元に戻すだけ（このしくみは材質名が無ければ働かない）
        class AtlasSlot { public Renderer r; public int index; public Vector4 baseST; }
        readonly List<AtlasSlot> atlasEyes = new List<AtlasSlot>(), atlasMouth = new List<AtlasSlot>();
        MaterialPropertyBlock atlasBlock;
        int atlasEyeCell = -1, atlasMouthCell = -1;
        static readonly Vector2 EyeCell = new Vector2(680f / 2048f, 456f / 2048f), MouthCell = new Vector2(512f / 2048f, 384f / 1152f);
        const int EyeCols = 3, MouthCols = 4;

        void SetupFaceAtlas()
        {
            atlasEyes.Clear(); atlasMouth.Clear();
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++)
                {
                    if (ms[i] == null) continue;
                    string n = ms[i].name;
                    bool eyes = n.Contains("FaceEyes"), mouth = n.Contains("FaceMouth");
                    if (!eyes && !mouth) continue;
                    Vector4 st = ms[i].HasProperty("_BaseMap_ST") ? ms[i].GetVector("_BaseMap_ST") : new Vector4(1f, 1f, 0f, 0f);
                    (eyes ? atlasEyes : atlasMouth).Add(new AtlasSlot { r = r, index = i, baseST = st });
                }
            }
            atlasEyeCell = atlasMouthCell = -1;
        }

        void SetAtlas(List<AtlasSlot> slots, int cell, int cols, Vector2 size)
        {
            if (atlasBlock == null) atlasBlock = new MaterialPropertyBlock();
            float ox = (cell % cols) * size.x, oy = -(cell / cols) * size.y;
            foreach (var a in slots)
            {
                if (a.r == null) continue;
                a.r.GetPropertyBlock(atlasBlock, a.index);
                atlasBlock.SetVector("_BaseMap_ST", new Vector4(a.baseST.x, a.baseST.y, a.baseST.z + ox, a.baseST.w + oy));
                a.r.SetPropertyBlock(atlasBlock, a.index);
            }
        }

        void UpdateFaceAtlas(float blink, float mouth, string f)
        {
            if (atlasEyes.Count == 0 && atlasMouth.Count == 0) return;
            bool closed = blink > 50f;
            int e;
            if (f == "Sad") e = closed ? 7 : 4;
            else if (f == "Angry") e = closed ? 8 : 5;
            else if (closed) e = 1;
            else if (f == "Smile" || f == "Shy") e = 2;
            else if (f == "Surprised") e = 6;
            else if (dozingNow) e = 3;
            else e = 0;
            int m = f == "Smile" || f == "Shy" ? 2 : f == "Sad" ? 4 : f == "Angry" ? 6 : f == "Surprised" ? 8 : 0;
            if (mouth > 30f) m += 1;   // 話している（口パク）・食べている
            if (e != atlasEyeCell && atlasEyes.Count > 0) { atlasEyeCell = e; SetAtlas(atlasEyes, e, EyeCols, EyeCell); }
            if (m != atlasMouthCell && atlasMouth.Count > 0) { atlasMouthCell = m; SetAtlas(atlasMouth, m, MouthCols, MouthCell); }
        }

        void UpdateFace(float dt, bool eyesClosed, bool speaking, string face)
        {
            if (faceKeys.Count == 0 && blush == null && atlasEyes.Count == 0 && atlasMouth.Count == 0) return;
            dt = Mathf.Min(dt, 0.1f);
            // まばたき
            float blink = 0f;
            if (eyesClosed) blink = 100f;
            else
            {
                blinkTimer -= dt;
                if (blinkTimer <= 0f) { blinkT = 0f; blinkTimer = Random.Range(2.5f, 6f); }
                if (blinkT >= 0f)
                {
                    blinkT += dt;
                    blink = 100f * Mathf.Clamp01(1f - Mathf.Abs(blinkT - 0.07f) / 0.07f);
                    if (blinkT > 0.14f) blinkT = -1f;
                }
            }
            SetKey("Blink", blink, dt, 2000f);
            // 口パク
            mouthT += dt;
            float mouth = speaking ? Mathf.Abs(Mathf.Sin(mouthT * 9f)) * 70f : 0f;
            SetKey("MouthOpen", mouth, dt, 900f);
            // 気分の表情
            string f = face ?? "";
            bool shy = f == "Shy";
            SetKey("Smile", f == "Smile" ? 100f : (shy ? 55f : 0f), dt, 300f);
            SetKey("Sad", f == "Sad" ? 100f : 0f, dt, 300f);
            SetKey("Angry", f == "Angry" ? 100f : 0f, dt, 300f);
            SetKey("Surprised", f == "Surprised" ? 100f : 0f, dt, 400f);
            if (blush != null && blush.activeSelf != shy) blush.SetActive(shy);
            UpdateFaceAtlas(blink, mouth, f);
        }

        // ================= 揺れ（Spring_ で始まる骨） =================
        // 2026-10-26（取り込みのチャット）作り直し。famuru「髪は体の動きに遅れて大きく反応し、すぐ収まる自然な揺れ。
        // 胸は硬く小刻みではなく柔らかくもっちり、歩くだけでも揺れる」
        // ・骨の先を「ばね＋減衰」で元の向きへ引き戻す。freq＝1秒に何回振れるか（小さいほどゆっくり大きく遅れる）、
        //   zeta＝止まりやすさ（0.25＝何度か弾む、0.55＝1回揺り返して収まる）。参考：工程109 の胸の揺れ（2.2Hz・減衰0.26）、VRChat PhysBones
        // ・1コマを細かく（1/120秒ごとに）分けて計算する（画面の速さで揺れ方が変わらない・小刻みに震えない）
        // ・角度の上限・当たり判定で押し戻したときは、その分の勢いも消す（押し戻しで跳ね返って震えないように）
        // ・air＝空気の抵抗の割合（0＝体といっしょに動く、1＝空中で止まろうとする）。髪・スカートに残して、歩くと後ろへなびく
        // ・胸は骨の長さを少し伸び縮みさせ（横は逆に）、もっちりした形の変化を出す
        class SpringBone
        {
            public Transform bone, child, parent;
            public Quaternion restLocal;
            public Vector3 restDirLocal;   // 骨の向き（自分の座標で）
            public Vector3 restScale;
            public float length, maxAngle = 60f;
            public float freq = 1.5f, zeta = 0.5f, air = 0.3f, gravity, stretch;
            public int kind;                          // 1＝スカート、2＝胸
            public float maxIn = 90f, maxSide = 90f;  // スカートが脚の側へ入る角度・胸が左右へ振れる角度の上限
            public Vector3 tip, vel, goalPrev, rootPrev;
            public int depth;
            public bool collide;                      // 髪・ネクタイ：胴・胸・腕に入らないようにする
            public int stretchAxis = -1;              // 伸び縮みする軸（骨の向きに近い軸。0=x 1=y 2=z）
            public float pitch, swing, k = 1f;        // 確認用：上下の角度・元の向きからの角度・伸び
        }

        // 確認用（揺れの連続写真）：胸の上下の角度（度）・伸び（%）、髪の元の向きからの平均の角度（度）
        public string SpringDebug
        {
            get
            {
                float cp = 0f, ck = 0f, hs = 0f; int nc = 0, nh = 0;
                foreach (var s in springs)
                {
                    if (s.kind == 2) { cp += s.pitch; ck += (s.k - 1f) * 100f; nc++; }
                    else if (s.collide && s.kind == 0) { hs += s.swing; nh++; }
                }
                return (nc > 0 ? (cp / nc).ToString("0.0") + "," + (ck / nc).ToString("0.0") : ",") + "," + (nh > 0 ? (hs / nh).ToString("0.0") : "");
            }
        }
        readonly List<SpringBone> springs = new List<SpringBone>();
        Vector3 lastSpringRoot;

        static string BaseName(Transform t)
        {
            string n = t.name;
            int k = n.LastIndexOf(':');
            return k >= 0 ? n.Substring(k + 1) : n;
        }

        // 骨の名前の最後の番号（根元が 1）
        static int ChainIndex(string bn)
        {
            int k = bn.LastIndexOf('_'), n;
            return (k >= 0 && int.TryParse(bn.Substring(k + 1), out n)) ? Mathf.Max(1, n) : 1;
        }

        void SetupSprings()
        {
            foreach (var t in hips.GetComponentsInChildren<Transform>(true))
            {
                string bn = BaseName(t);
                if (!bn.StartsWith("Spring_") || bn.EndsWith("_End")) continue;
                Transform child = null;
                foreach (Transform c in t) if (BaseName(c).StartsWith("Spring_")) { child = c; break; }
                if (child == null) continue;
                var sb = new SpringBone { bone = t, child = child, parent = t.parent, restLocal = t.localRotation, restScale = t.localScale };
                sb.restDirLocal = t.InverseTransformDirection(child.position - t.position).normalized;
                sb.length = (child.position - t.position).magnitude;
                string part = bn.Substring(7);
                // 先の骨ほど少しゆっくり（工程109 の根元1.38／中1.0／先0.9 に近い）
                float soft = Mathf.Pow(0.88f, ChainIndex(bn) - 1);
                // スカート：外へ35度・脚の側へ10度まで（2026-10-21 famuru「1で」）
                if (part.StartsWith("Skirt")) { sb.freq = 2.0f * soft; sb.zeta = 0.5f; sb.air = 0.2f; sb.gravity = 0.4f; sb.maxAngle = 35f; sb.kind = 1; sb.maxIn = 10f; }
                // 胸：上下20度・左右10度まで（2026-10-21 famuru「1で」）。ゆっくりで止まりにくく、少し伸び縮み
                else if (part.StartsWith("Chest")) { sb.freq = 2.2f; sb.zeta = 0.22f; sb.air = 0f; sb.gravity = 0f; sb.maxAngle = 20f; sb.kind = 2; sb.maxSide = 10f; sb.stretch = 0.06f; }
                else if (part.StartsWith("Tail")) { sb.freq = 1.3f * soft; sb.zeta = 0.4f; sb.air = 0.3f; sb.gravity = 0.3f; }
                else if (part.StartsWith("Ear")) { sb.freq = 3.0f; sb.zeta = 0.6f; sb.air = 0.1f; }
                else if (part.StartsWith("RibbonTie") || part.StartsWith("Tie")) { sb.freq = 1.9f * soft; sb.zeta = 0.45f; sb.air = 0.25f; sb.gravity = 0.4f; sb.maxAngle = 45f; }
                else if (part.StartsWith("Ribbon")) { sb.freq = 1.7f * soft; sb.zeta = 0.4f; sb.air = 0.3f; sb.gravity = 0.4f; }
                else if (part.StartsWith("HairFront") || part.StartsWith("HairSide")) { sb.freq = 2.2f * soft; sb.zeta = 0.62f; sb.air = 0.12f; sb.gravity = 0.5f; sb.maxAngle = 25f; } // 顔のまわりの短い束：少し速く・小さく（止まったときに前へ跳ね上がらないように）
                else { sb.freq = 1.6f * soft; sb.zeta = 0.62f; sb.air = 0.12f; sb.gravity = 0.5f; sb.maxAngle = 35f; } // Hair（後ろ髪など）
                sb.collide = part.StartsWith("Hair") || part.StartsWith("RibbonTie") || part.StartsWith("Tie");
                if (sb.stretch > 0f)
                {
                    Vector3 a = new Vector3(Mathf.Abs(sb.restDirLocal.x), Mathf.Abs(sb.restDirLocal.y), Mathf.Abs(sb.restDirLocal.z));
                    int ax = a.x > a.y ? (a.x > a.z ? 0 : 2) : (a.y > a.z ? 1 : 2);
                    if (a[ax] > 0.9f) sb.stretchAxis = ax; else sb.stretch = 0f;   // 骨の向きが軸とずれているときは伸び縮みしない
                }
                int d = 0;
                for (var p = t.parent; p != null && p != model; p = p.parent) d++;
                sb.depth = d;
                sb.tip = sb.goalPrev = child.position;
                sb.rootPrev = t.position;
                sb.vel = Vector3.zero;
                springs.Add(sb);
            }
            springs.Sort((a, b) => a.depth.CompareTo(b.depth));
            lastSpringRoot = transform.position;
            SetupHitShapes();
        }

        // ================= 髪の当たり判定（2026-10-26 famuru 決定：体・腕の簡単な当たり判定。セレネ・リア共通） =================
        // 胴・胸・腕のまわりに見えない筒（両端が丸い）を置き、髪・ネクタイの揺れの骨の先がその中に入ったら外へ押し出す。
        // 元の姿勢ですでに入っている深さまでは許す（髪が体から浮かないように）。太さは肩幅（左右の腕の付け根の間）から決める。
        class HitShape { public Transform a, b; public float r; }
        readonly List<HitShape> hitShapes = new List<HitShape>();

        void AddHit(Transform a, Transform b, float r)
        {
            if (a == null) return;
            hitShapes.Add(new HitShape { a = a, b = b ?? a, r = r });
        }

        void SetupHitShapes()
        {
            hitShapes.Clear();
            Transform la = B("LeftArm"), ra = B("RightArm");
            if (la == null || ra == null) return;
            float w = (la.position - ra.position).magnitude;
            if (w < 1e-4f) return;
            Transform sp2 = B("Spine2") ?? B("Spine1") ?? B("Spine");
            AddHit(hips, sp2, w * 0.40f);                 // 腰〜胸の下
            AddHit(sp2, B("Neck"), w * 0.30f);            // 胸の上〜首の付け根
            foreach (var s in springs)                    // 胸のふくらみ
                if (BaseName(s.bone).StartsWith("Spring_Chest")) AddHit(s.bone, s.child, w * 0.24f);
            AddHit(la, B("LeftForeArm"), w * 0.17f);      // 二の腕
            AddHit(ra, B("RightForeArm"), w * 0.17f);
            AddHit(B("LeftForeArm"), B("LeftHand"), w * 0.13f);   // 前腕
            AddHit(B("RightForeArm"), B("RightHand"), w * 0.13f);
        }

        static float SegDist(Vector3 p, Vector3 a, Vector3 b, out Vector3 q)
        {
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 1e-10f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            q = a + ab * t;
            return (p - q).magnitude;
        }

        Vector3 PushOut(Vector3 p, Vector3 rest, Vector3 root, float len)
        {
            for (int it = 0; it < 2; it++)
            {
                foreach (var h in hitShapes)
                {
                    if (h.a == null) continue;
                    Vector3 a = h.a.position, b = h.b.position, q, q0;
                    float r = Mathf.Min(h.r, SegDist(rest, a, b, out q0)); // 元の姿勢で入っている深さまでは許す
                    float d = SegDist(p, a, b, out q);
                    if (d >= r) continue;
                    Vector3 n = d > 1e-5f ? (p - q) / d : (rest - q0).normalized;
                    if (n.sqrMagnitude < 1e-8f) continue;
                    p = q + n * r;
                }
                Vector3 v = p - root;
                if (v.sqrMagnitude > 1e-10f) p = root + v.normalized * len;
            }
            return p;
        }

        // 骨の先の位置 p を、長さ・角度の上限・当たり判定に合わせる
        Vector3 Constrain(SpringBone s, Vector3 p, Vector3 root, Vector3 goal)
        {
            Vector3 restDir = (goal - root) / Mathf.Max(s.length, 1e-6f);
            Vector3 d = p - root;
            float L = d.magnitude;
            Vector3 dir = L > 1e-6f ? d / L : restDir;
            if (s.kind == 1) // スカート：体の中心（脚）へ向かう向きには maxIn 度まで
            {
                Vector3 radial = Vector3.ProjectOnPlane(root - hips.position, transform.up);
                if (radial.sqrMagnitude > 1e-6f)
                {
                    radial.Normalize();
                    float lim = Mathf.Sin(s.maxIn * Mathf.Deg2Rad);
                    float inward = -Vector3.Dot(dir - restDir, radial);
                    if (inward > lim) dir = (dir + radial * (inward - lim)).normalized;
                }
            }
            else if (s.kind == 2) // 胸：左右へは maxSide 度まで
            {
                Vector3 side = transform.right;
                float lim = Mathf.Sin(s.maxSide * Mathf.Deg2Rad);
                float lat = Vector3.Dot(dir - restDir, side);
                if (Mathf.Abs(lat) > lim) dir = (dir - side * (lat - Mathf.Sign(lat) * lim)).normalized;
            }
            float len = s.stretch > 0f ? Mathf.Clamp(L, s.length * (1f - s.stretch), s.length * (1f + s.stretch)) : s.length;
            if (s.stretch > 0f && L > 1e-6f) len = Mathf.Lerp(s.length, len, Mathf.Abs(Vector3.Dot(dir, restDir)));
            // 当たり判定で押し出してから角度の上限をかける（押し出しで上限を超えて跳ね上がらないように）
            if (s.collide && hitShapes.Count > 0)
            {
                Vector3 q = PushOut(root + dir * len, goal, root, len) - root;
                if (q.sqrMagnitude > 1e-10f) dir = q.normalized;
            }
            dir = Vector3.RotateTowards(restDir, dir, s.maxAngle * Mathf.Deg2Rad, 0f);
            return root + dir * len;
        }

        void ApplyBone(SpringBone s, Vector3 root, Vector3 restDir)
        {
            Vector3 d = s.tip - root;
            s.swing = Vector3.Angle(restDir, d);
            s.pitch = Vector3.SignedAngle(restDir, d, transform.right);
            s.k = 1f;
            if (d.sqrMagnitude > 1e-10f) s.bone.rotation = Quaternion.FromToRotation(restDir, d) * s.bone.rotation;
            if (s.stretchAxis >= 0)
            {
                // 骨の向きの分だけで伸び縮みを決める（横へ振れただけでは伸びない）
                float k = Mathf.Clamp(Vector3.Dot(d, restDir) / Mathf.Max(s.length, 1e-6f), 1f - s.stretch, 1f + s.stretch);
                s.k = k;
                Vector3 sc = s.restScale * (1f / Mathf.Sqrt(k));   // 伸びると細く、縮むと太く（もっちり）
                sc[s.stretchAxis] = s.restScale[s.stretchAxis] * k;
                s.bone.localScale = sc;
            }
        }

        // ---- 2026-10-30（軽量化のチャット）：映っているかどうか・表示の範囲 ----
        bool springsAsleep = true;
        [System.NonSerialized] public bool perfClone;   // PerfProbe の見本（確認用の写真と違い、ふつうのキャラと同じ決まりで揺らす）
        public static float SwayMinPixels = 90f;        // 画面での背の高さがこれより小さい子は揺らさない（遠くて揺れが見えないため）
        bool BigOnScreen()
        {
            if (testMode && !perfClone) return true;   // 確認用の写真（別のカメラで近くから撮る）はいつも揺らす
            var w = LoungeWorld.Instance;
            var cam = w != null && w.mainCamera != null ? w.mainCamera : Camera.main;
            if (cam == null) return true;
            float h = (measuredHeight < 0.5f || measuredHeight > 3f) ? height : measuredHeight;
            float view;
            if (cam.orthographic) view = cam.orthographicSize * 2f;
            else view = 2f * Vector3.Distance(cam.transform.position, hips.position) * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            return h / Mathf.Max(0.01f, view) * Screen.height >= SwayMinPixels;
        }
        SkinnedMeshRenderer[] visRends;
        bool AnyVisible()
        {
            if (visRends == null) visRends = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var r in visRends) if (r != null && r.enabled && r.isVisible) return true;
            return false;
        }

        void SetWideBounds()
        {
            float h = (measuredHeight < 0.5f || measuredHeight > 3f) ? height : measuredHeight;
            float side = Mathf.Max(2.4f, h + 1.2f);
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Transform rb = smr.rootBone != null ? smr.rootBone : smr.transform;
                float s = Mathf.Max(1e-5f, Mathf.Abs(rb.lossyScale.x));
                smr.localBounds = new Bounds(rb.InverseTransformPoint(hips.position), Vector3.one * (side / s));
                smr.updateWhenOffscreen = false;
            }
        }

        void UpdateSprings(float dt, bool forceReset = false)
        {
            if (springs.Count == 0) return;
            bool reset = forceReset || dt > 0.1f || (transform.position - lastSpringRoot).sqrMagnitude > 0.25f;
            lastSpringRoot = transform.position;
            int n = Mathf.Clamp(Mathf.CeilToInt(dt * 120f), 1, 12);  // 1/120秒ごとに分けて計算
            float h = dt / n;
            foreach (var s in springs)
            {
                s.bone.localRotation = s.restLocal;
                if (s.stretchAxis >= 0) s.bone.localScale = s.restScale;
                Vector3 root = s.bone.position;
                Vector3 restDir = s.bone.TransformDirection(s.restDirLocal);
                Vector3 goal = root + restDir * s.length;
                if (reset)
                {
                    s.tip = goal; s.vel = Vector3.zero;
                }
                else
                {
                    Vector3 gv = (goal - s.goalPrev) / dt;      // 体（目標）の速さ
                    float w = 2f * Mathf.PI * s.freq;
                    for (int i = 1; i <= n; i++)
                    {
                        float a = (float)i / n;
                        Vector3 r = Vector3.Lerp(s.rootPrev, root, a);
                        Vector3 g = Vector3.Lerp(s.goalPrev, goal, a);
                        Vector3 start = s.tip;
                        Vector3 relV = s.vel - gv * (1f - s.air);
                        Vector3 acc = w * w * (g - s.tip) - 2f * s.zeta * w * relV + Vector3.down * s.gravity;
                        s.vel += acc * h;
                        Vector3 free = s.tip + s.vel * h;
                        Vector3 p = Constrain(s, free, r, g);
                        // 押し戻した分は勢いに半分だけ入れる（全部入れると、腕や胸に押されたときに髪が弾き飛ばされる）
                        s.vel += (p - free) / h * 0.5f;
                        s.tip = p;
                    }
                }
                s.rootPrev = root;
                s.goalPrev = goal;
                ApplyBone(s, root, restDir);
            }
        }

        // ================= 骨を直接曲げる簡単な動き =================
        Transform B(string n) { Transform t; return bones.TryGetValue(n, out t) ? t : null; }
        public Transform Bone(string n) { return B(n); }   // 確認用の写真から骨の位置を見る

        void Aim(string boneName, string childName, Quaternion frame, Vector3 dir)
        {
            Transform b = B(boneName), c = B(childName);
            if (b == null || c == null) return;
            Vector3 cur = c.position - b.position;
            if (cur.sqrMagnitude < 1e-8f) return;
            b.rotation = Quaternion.FromToRotation(cur, frame * dir.normalized) * b.rotation;
        }

        void Procedural(BodyPose pose, bool talking, float dt, Quaternion frame)
        {
            for (int i = 0; i < boneList.Length; i++) boneList[i].localRotation = restRot[i];
            hips.localPosition = hipsRest;
            float k = height / 1.70f;
            Vector3 up = frame * Vector3.up, fwd = frame * Vector3.forward;
            talkT += dt;

            // 腕（左は -X）
            Vector3 uaL = new Vector3(-0.17f, -1f, 0.02f), faL = new Vector3(-0.07f, -1f, 0.1f);
            Vector3 uaR = new Vector3(0.17f, -1f, 0.02f), faR = new Vector3(0.07f, -1f, 0.1f);
            Vector3 thL = Vector3.down, shL = Vector3.down, thR = Vector3.down, shR = Vector3.down;

            switch (pose)
            {
                case BodyPose.Stand:
                {
                    float walk = Mathf.Clamp01((speed - 0.05f) / 0.6f);
                    phase += speed * dt * (Mathf.PI * 2f / (1.15f * k));
                    float sL = Mathf.Sin(phase), cL = Mathf.Cos(phase);
                    float sR = -sL, cR = -cL;
                    float A = 0.5f * walk;
                    thL = new Vector3(0f, -1f, sL * A);
                    shL = new Vector3(0f, -1f, sL * A - 0.7f * walk * Mathf.Max(0f, cL));
                    thR = new Vector3(0f, -1f, sR * A);
                    shR = new Vector3(0f, -1f, sR * A - 0.7f * walk * Mathf.Max(0f, cR));
                    uaL.z = -sL * 0.35f * walk; faL.z = 0.1f + 0.2f * walk;
                    uaR.z = -sR * 0.35f * walk; faR.z = 0.1f + 0.2f * walk;
                    hips.position -= up * (0.012f * k * walk * Mathf.Abs(sL)); // 足を着くたびに腰が少し下がる（胸・スカート・髪が揺れるきっかけ）
                    hips.position += up * (Mathf.Abs(Mathf.Cos(phase)) * 0.02f * walk);
                    if (talking && walk < 0.2f) { faR = new Vector3(0.25f, -0.15f + 0.25f * Mathf.Sin(talkT * 4f), 1f); uaR = new Vector3(0.2f, -1f, 0.25f); }
                    break;
                }
                case BodyPose.Sit:
                    hips.position = transform.position + up * (0.54f * k) - fwd * 0.05f;
                    thL = thR = new Vector3(0f, -0.12f, 1f);
                    shL = shR = new Vector3(0f, -1f, 0.1f);
                    thL.x = -0.06f; thR.x = 0.06f;
                    uaL = new Vector3(-0.12f, -1f, 0.15f); faL = new Vector3(0.05f, -0.75f, 1f);
                    uaR = new Vector3(0.12f, -1f, 0.15f); faR = new Vector3(-0.05f, -0.75f, 1f);
                    if (eatingNow) { faL = new Vector3(-0.2f, 0.1f, 1f); faR = new Vector3(0.2f, 0.1f + 0.15f * Mathf.Sin(talkT * 3f), 1f); }
                    else if (talking) faR = new Vector3(0.25f, -0.1f + 0.3f * Mathf.Sin(talkT * 4f), 1f);
                    break;
                case BodyPose.SitFloor:
                    hips.position = transform.position + up * (0.28f * k) - fwd * 0.12f;
                    thL = new Vector3(-0.05f, -0.5f, 1f); thR = new Vector3(0.05f, -0.5f, 1f);
                    shL = shR = new Vector3(0f, 0.02f, -1f);
                    uaL = new Vector3(-0.1f, -1f, 0.12f); faL = new Vector3(0.05f, -0.9f, 1f);
                    uaR = new Vector3(0.1f, -1f, 0.12f); faR = new Vector3(-0.05f, -0.9f, 1f);
                    if (talking) faR = new Vector3(0.25f, -0.2f + 0.3f * Mathf.Sin(talkT * 4f), 1f);
                    break;
                case BodyPose.Soak:
                    hips.position = transform.position + up * 0.12f;
                    thL = new Vector3(-0.08f, -0.05f, 1f); thR = new Vector3(0.08f, -0.05f, 1f);
                    shL = shR = new Vector3(0f, -0.08f, 1f);
                    uaL = new Vector3(-0.4f, -1f, -0.1f); faL = new Vector3(-0.2f, -1f, 0.3f);
                    uaR = new Vector3(0.4f, -1f, -0.1f); faR = new Vector3(0.2f, -1f, 0.3f);
                    break;
                case BodyPose.Lie:
                case BodyPose.LieFloor:
                    uaL = new Vector3(-0.2f, -1f, 0.05f); uaR = new Vector3(0.2f, -1f, 0.05f);
                    faL = new Vector3(-0.1f, -1f, 0.2f); faR = new Vector3(0.1f, -1f, 0.2f);
                    break;
            }

            Aim("LeftUpLeg", "LeftLeg", frame, thL);
            Aim("RightUpLeg", "RightLeg", frame, thR);
            Aim("LeftLeg", "LeftFoot", frame, shL);
            Aim("RightLeg", "RightFoot", frame, shR);
            Aim("LeftArm", "LeftForeArm", frame, uaL);
            Aim("RightArm", "RightForeArm", frame, uaR);
            Aim("LeftForeArm", "LeftHand", frame, faL);
            Aim("RightForeArm", "RightHand", frame, faR);

            // 座ったまま居眠り：首を前に傾ける
            if (dozingNow)
            {
                Transform neck = B("Neck") ?? B("Head");
                if (neck != null) neck.rotation = Quaternion.AngleAxis(28f, frame * Vector3.right) * neck.rotation;
            }
        }
    }
}
