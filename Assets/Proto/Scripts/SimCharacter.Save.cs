using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>セーブ用：キャラ1人分の中身。</summary>
    [System.Serializable]
    public class CharSave
    {
        public string letter;
        public float lonely, tired, bored, hunger, study, hobbyNeed, loveNeed, sleepy, desire, heat, mana, weekStudy, assignProgress;
        public float vigor = 100f; // 篤の精力
        public float inside;       // ヒロインの中出しの量（2026-10-25）
        public bool hasAssignment, desireMaxAtNight;
        public int assignSubject = -1;
        public double assignDeadline;
        public float[] skill, ability, weekSubject;
        public List<StatusSave> statuses = new List<StatusSave>();
        public List<ModSave> mods = new List<ModSave>();
        public int[] enrolled;
    }

    [System.Serializable] public class StatusSave { public int line, stage; public double until; public float timer; }
    [System.Serializable] public class ModSave { public string id; public double until; }

    /// <summary>キャラの、セーブ・読み込みの部分。</summary>
    public partial class SimCharacter
    {
        public CharSave ToSave()
        {
            var s = new CharSave
            {
                letter = letter,
                lonely = lonely, tired = tired, bored = bored, hunger = hunger, study = study, hobbyNeed = hobbyNeed, loveNeed = loveNeed, sleepy = sleepy, desire = desire,
                heat = desireBuild.value, mana = mana, weekStudy = weekStudy, assignProgress = assignProgress, vigor = vigor, inside = inside,
                hasAssignment = hasAssignment, desireMaxAtNight = desireMaxAtNight, assignSubject = assignSubject, assignDeadline = assignDeadline,
                skill = (float[])skill.Clone(), ability = (float[])ability.Clone(), weekSubject = (float[])weekSubject.Clone(),
                enrolled = profile != null ? (int[])profile.enrolled.Clone() : null,
            };
            foreach (var st in statuses) s.statuses.Add(new StatusSave { line = (int)st.line, stage = st.stage, until = st.until, timer = st.timer });
            foreach (var m in mods) if (m.mod != null && m.mod.source != ModSource.Race) s.mods.Add(new ModSave { id = m.mod.id, until = m.until });
            return s;
        }

        public void FromSave(CharSave s)
        {
            if (s == null) return;
            lonely = s.lonely; tired = s.tired; bored = s.bored; hunger = s.hunger; study = s.study; hobbyNeed = s.hobbyNeed; loveNeed = s.loveNeed; sleepy = s.sleepy; desire = s.desire;
            desireBuild.value = s.heat; mana = s.mana; weekStudy = s.weekStudy; assignProgress = s.assignProgress; vigor = s.vigor; inside = s.inside;
            hasAssignment = s.hasAssignment; desireMaxAtNight = s.desireMaxAtNight; assignSubject = s.assignSubject; assignDeadline = s.assignDeadline;
            if (s.skill != null && s.skill.Length <= skill.Length) Copy(s.skill, skill); // 科目を後ろに足した（2026-10-30）前のセーブも、ある分だけ使う
            Copy(s.ability, ability);
            if (s.weekSubject != null && s.weekSubject.Length <= weekSubject.Length) Copy(s.weekSubject, weekSubject);
            if (profile != null && s.enrolled != null && s.enrolled.Length > 0) profile.enrolled = (int[])s.enrolled.Clone();
            statuses.Clear();
            if (s.statuses != null)
                foreach (var st in s.statuses) statuses.Add(new ActiveStatus { line = (StatusLine)st.line, stage = st.stage, until = st.until, timer = st.timer });
            mods.RemoveAll(a => a.mod == null || a.mod.source != ModSource.Race);
            if (s.mods != null)
                foreach (var m in s.mods)
                {
                    var mod = NeedBook.Item(m.id);
                    if (mod != null && !mods.Exists(a => a.mod.id == mod.id)) mods.Add(new ActiveMod { mod = mod, until = m.until });
                }
        }

        static void Copy(float[] from, float[] to)
        {
            if (from == null || to == null) return;
            for (int i = 0; i < to.Length && i < from.Length; i++) to[i] = from[i];
        }
    }
}
