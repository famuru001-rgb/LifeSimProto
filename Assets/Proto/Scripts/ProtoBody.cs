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

        public static ProtoBody Create(GameObject owner, CharLook look)
        {
            var prefab = Resources.Load<GameObject>(look.male ? "ProtoModels/ProtoMale" : "ProtoModels/ProtoFemale");
            if (prefab == null) return null;
            var inst = Instantiate(prefab, owner.transform, false);
            inst.name = "Model";
            var pb = owner.AddComponent<ProtoBody>();
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
                model.localRotation = Quaternion.Euler(0f, ang, 0f) * model.localRotation;
            }
            fix = model.localRotation;
            // 大きさを確かめる（単位がずれていたら合わせる）
            Transform top = bones.ContainsKey("HeadTop_End") ? bones["HeadTop_End"] : bones["Head"];
            float h = owner.InverseTransformPoint(top.position).y;
            if (h < 1.2f || h > 2.4f)
            {
                if (h > 0.01f) model.localScale *= height / h;
                Debug.LogWarning("仮モデルの大きさを調整しました（" + h + "m → " + height + "m）");
            }
            if (owner.InverseTransformPoint(bones["LeftArm"].position).x > 0f)
                Debug.LogWarning("仮モデルの左右が反対になっています");

            var list = new List<Transform>();
            foreach (var kv in bones) if (!list.Contains(kv.Value)) list.Add(kv.Value);
            boneList = list.ToArray();
            restRot = new Quaternion[boneList.Length];
            for (int i = 0; i < boneList.Length; i++) restRot[i] = boneList[i].localRotation;
            hipsRest = hips.localPosition;

            SetupLook();

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
            Color towel = new Color(0.97f, 0.97f, 0.94f);
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
                case "Skin": case "Hip": return look.skin;
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
            m = ProtoArt.Mat(c, null, null, key == "Eye" ? 0.6f : 0.2f);
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
                output.SetSourcePlayable(mixer);
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
            graph.Connect(p, 0, mixer, 0);
            if (oldIn.IsValid() && curClip != null) graph.Connect(oldIn, 0, mixer, 1);
            else if (oldIn.IsValid()) oldIn.Destroy();
            mixer.SetInputWeight(0, curClip == null ? 1f : 0f);
            mixer.SetInputWeight(1, curClip == null ? 0f : 1f);
            fade = curClip == null ? 1f : 0f;
            curClip = c;
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
            if (model == null) return;
            if (sim == null) sim = GetComponent<SimCharacter>();
            if (sim == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 d = transform.position - lastPos;
            d.y = 0f;
            lastPos = transform.position;
            speed = Mathf.Lerp(speed, Mathf.Min(d.magnitude / dt, 3f), Mathf.Clamp01(dt * 10f));

            BodyPose pose = sim.BodyPoseNow;
            bool talking = sim.IsTalkingNow;
            ShowBath(pose == BodyPose.Soak);

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

            // Mixamo の動きがあれば使う
            AnimationClip clip = null;
            if (humanOk)
            {
                if (pose == BodyPose.Stand)
                {
                    if (speed > 0.25f) clip = Clip("Walk");
                    else if (talking) clip = Clip("Talk") ?? Clip("Idle");
                    else clip = Clip("Idle");
                }
                else if (pose == BodyPose.Sit)
                {
                    clip = talking ? (Clip("SitTalk") ?? Clip("Sit")) : Clip("Sit");
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
                return;
            }
            StopClips();
            Procedural(pose, talking, dt, transform.rotation * frame);
        }

        // ================= 骨を直接曲げる簡単な動き =================
        Transform B(string n) { Transform t; return bones.TryGetValue(n, out t) ? t : null; }

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
                    hips.position += up * (Mathf.Abs(Mathf.Cos(phase)) * 0.02f * walk);
                    if (talking && walk < 0.2f) { faR = new Vector3(0.25f, -0.15f + 0.25f * Mathf.Sin(talkT * 4f), 1f); uaR = new Vector3(0.2f, -1f, 0.25f); }
                    break;
                }
                case BodyPose.Sit:
                    hips.position = transform.position + up * (0.54f * k) - fwd * 0.05f;
                    thL = thR = new Vector3(0f, -0.12f, 1f);
                    shL = shR = new Vector3(0f, -1f, 0.1f);
                    thL.x = -0.06f; thR.x = 0.06f;
                    uaL = new Vector3(-0.15f, -1f, 0.3f); faL = new Vector3(-0.1f, -0.25f, 1f);
                    uaR = new Vector3(0.15f, -1f, 0.3f); faR = new Vector3(0.1f, -0.25f, 1f);
                    if (sim.IsEatingNow) { faL = new Vector3(-0.2f, 0.1f, 1f); faR = new Vector3(0.2f, 0.1f + 0.15f * Mathf.Sin(talkT * 3f), 1f); }
                    else if (talking) faR = new Vector3(0.25f, -0.1f + 0.3f * Mathf.Sin(talkT * 4f), 1f);
                    break;
                case BodyPose.SitFloor:
                    hips.position = transform.position + up * (0.28f * k) - fwd * 0.12f;
                    thL = new Vector3(-0.05f, -0.5f, 1f); thR = new Vector3(0.05f, -0.5f, 1f);
                    shL = shR = new Vector3(0f, 0.02f, -1f);
                    uaL = new Vector3(-0.12f, -1f, 0.3f); faL = new Vector3(-0.1f, -0.4f, 1f);
                    uaR = new Vector3(0.12f, -1f, 0.3f); faR = new Vector3(0.1f, -0.4f, 1f);
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
            if (sim.IsDozingNow)
            {
                Transform neck = B("Neck") ?? B("Head");
                if (neck != null) neck.rotation = Quaternion.AngleAxis(28f, frame * Vector3.right) * neck.rotation;
            }
        }
    }
}
