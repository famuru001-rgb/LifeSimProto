#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;

namespace LifeSimProto
{
    // Explicit editor preview only. Never called by ordinary NPC creation.
    public partial class ProtoBody
    {
        public float Mob05HeightAtCreation;
        public static ProtoBody CreateMob05(GameObject owner, int bust, int hair, float heightCm, string letter)
        {
            if (bust < 1 || bust > 3 || hair < 0 || hair > 2) throw new ArgumentOutOfRangeException();
            var source = Resources.Load<GameObject>("Chars/Mob/CharMob");
            if (source == null) throw new Exception("MOB05 model not imported");
            var copy = Instantiate(source, owner.transform, false);
            var pb = owner.AddComponent<ProtoBody>();
            pb.real = true;
            pb.testMode = true;
            pb.testLetter = letter; pb.testScene = ""; pb.testMood = ""; pb.testClip = "Idle";
            if (!pb.Setup(copy.transform, new CharLook())) throw new Exception("MOB05 Setup failed");
            // Mob_Head lacks the generic skin-name words. Keep original material name;
            // use the same existing skin settings as Mob_Body, only on this preview asset.
            foreach (var r in copy.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (r.name == "Body_MobHead") foreach (var mat in r.sharedMaterials)
                    if (mat.shader.name == "LifeSim/Toon") SetSkinLook(mat);
            string hairName = new[] { "Hair_Short", "Hair_Semi", "Hair_Pony" }[hair];
            foreach (var r in copy.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                r.gameObject.SetActive(r.name == "Body_Mob_Bust" + bust || r.name == "Body_MobHead" || r.name == hairName);
            // Calibrate height against actual head surface, not hair or Head bone.
            var head = copy.GetComponentsInChildren<SkinnedMeshRenderer>().Single(r => r.name == "Body_MobHead");
            var baked = new Mesh(); head.BakeMesh(baked, true);
            float top = baked.vertices.Max(v => owner.transform.InverseTransformPoint(head.transform.TransformPoint(v)).y);
            Destroy(baked);
            if (top < 1 || top > 2) throw new Exception("Unexpected source head height " + top);
            copy.transform.localScale *= heightCm * .01f / top;
            baked = new Mesh(); head.BakeMesh(baked, true);
            pb.Mob05HeightAtCreation = baked.vertices.Max(v => owner.transform.InverseTransformPoint(head.transform.TransformPoint(v)).y) * 100;
            Destroy(baked);
            if (Mathf.Abs(pb.Mob05HeightAtCreation - heightCm) > .2f) throw new Exception("Height calibration failed target="+heightCm+" measured="+pb.Mob05HeightAtCreation+" base="+top);
            return pb;
        }
        public void Mob05TurnHead(float yaw)
        {
            // Direct 45-degree stress check; normal game look splits neck/head 40/60.
            var h = B("Head"); if (h != null) h.rotation = Quaternion.AngleAxis(yaw, transform.up) * h.rotation;
        }
    }

    public static class Mob05Variants
    {
        public static CharProfile[] Profiles => CharProfiles.All.Where(p => p.mob).OrderBy(p => p.letter, StringComparer.Ordinal).ToArray();
        public static int Bust(string letter)
        {
            int i = Array.FindIndex(Profiles, p => p.letter == letter);
            if (i < 0) throw new ArgumentException("Unknown mob symbol " + letter);
            return i % 3 + 1;
        }
        public static int Hair(CharProfile p)
        {
            if (p.look.parts.Contains("Hair_Bob")) return 0;
            if (p.look.parts.Contains("Hair_HimeSide")) return 1;
            if (p.look.parts.Contains("Hair_Pony")) return 2;
            if (p.teacher && p.look.parts.Contains("Hair_LongBack")) return (p.mobNo - 1) % 3;
            throw new ArgumentException("Unmapped hair: " + p.letter);
        }
    }
}
#endif
