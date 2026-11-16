using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 信頼と悩みの段の画面（Bond.cs。2026-11-16）。famuru 決定：数字は言葉とゲージの両方で見せる。
    /// ・子の画面の「関係」の欄：段・信頼・警戒・関心（言葉とゲージ）・悩み（篤が知っていれば）
    /// ・右クリック：「悩みを聞く」と、悩みを知っていれば「（篤がすること）」
    /// </summary>
    public partial class SimHud
    {
        void BondGui(SimCharacter c, float w, ref float y)
        {
            var bb = world.bond;
            int i = c.id;
            int st = Mathf.Clamp(bb.stage[i], 0, 3);
            GUI.Label(new Rect(4, y, w, 22), "段：" + st + "「" + BondBook.StageNames[st] + "」" + (bb.promise[i] ? "　二人きりでお礼をする約束がある" : ""), small); y += 24;
            {
                // グループの行は長いので折り返す（2026-11-16）
                var gs = new GUIStyle(tiny) { wordWrap = true };
                var gc = new GUIContent(bb.GroupText(world, c));
                float gh = Mathf.Max(20f, gs.CalcHeight(gc, w - 8));
                GUI.Label(new Rect(4, y, w - 8, gh), gc, gs); y += gh + 2;
            }
            BondBar("信頼", bb.trust[i], BondBook.TrustWord(bb.trust[i]), new Color(0.35f, 0.6f, 0.9f), w, ref y);
            BondBar("警戒", bb.wary[i], BondBook.WaryWord(bb.wary[i]), new Color(0.85f, 0.45f, 0.35f), w, ref y);
            BondBar("関心", bb.interest[i], BondBook.InterestWord(bb.interest[i]), new Color(0.85f, 0.7f, 0.3f), w, ref y);
            var wd = bb.WorryOf(i);
            string ws;
            if (wd == null) ws = "悩み：なし";
            else if (!bb.worryKnown[i]) ws = "悩み：何か悩んでいるようす（右クリックの「悩みを聞く」）";
            else
            {
                int left = Mathf.Max(0, Mathf.CeilToInt((float)((bb.worryUntil[i] - world.Now) / 1440.0)));
                ws = "悩み：「" + wd.name + "」→ " + wd.action + "（" + bb.worryProg[i] + "/" + wd.count + "回・あと " + left + "日" + (wd.when != "いつでも" ? "・" + wd.when : "") + (wd.alone ? "・二人だけで" : "") + (wd.silver > 0 ? "・銀貨" + wd.silver : "") + (wd.item != null ? "・" + wd.item.name : "") + "）";
            }
            GUI.Label(new Rect(4, y, w, 22), ws, tiny); y += 24;
        }

        void BondBar(string label, float v, string word, Color col, float w, ref float y)
        {
            GUI.Label(new Rect(4, y, 60, 20), label, tiny);
            var bar = new Rect(64, y + 6, 150, 10);
            var old = GUI.color;
            GUI.color = new Color(0.2f, 0.2f, 0.22f);
            GUI.DrawTexture(bar, ProtoArt.White);
            GUI.color = col;
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(v / 100f), bar.height), ProtoArt.White);
            GUI.color = old;
            GUI.Label(new Rect(224, y, w - 224, 20), word + "（" + Mathf.RoundToInt(v) + "）", tiny);
            y += 20;
        }

        /// <summary>篤から話しかける・触れる・誘うとき：グループの順番で閉じている子は断ることがある（Bond.cs）。</summary>
        bool ContactOk(SimCharacter g)
        {
            if (world.bond.AcceptContact(world, g)) return true;
            g.ShowBubble("ごめん、今はちょっと……", 2.2f);
            SimCharacter b;
            world.bond.Locked(world, g.id, out b);
            world.Log(g.displayName + "には、まだ距離を置かれている" + (b != null ? "（先に" + b.displayName + "と仲良くなると近づきやすい）" : ""));
            CloseMenu();
            return false;
        }

        void AddWorryItems(List<MenuItem> items, SimCharacter g)
        {
            var f = F;
            var bb = world.bond;
            var wd = bb.WorryOf(g.id);
            items.Add(new MenuItem { label = g.displayName + "の悩みを聞く", enabled = true, act = () => { bb.Ask(world, g); CloseMenu(); } });
            if (wd != null && bb.worryKnown[g.id])
            {
                string why = bb.CanAct(world, g, f);
                items.Add(new MenuItem
                {
                    label = wd.action + "（" + bb.worryProg[g.id] + "/" + wd.count + "）" + (why != null ? "（" + why + "）" : ""),
                    enabled = why == null,
                    act = () => { bb.Act(world, g, f); CloseMenu(); }
                });
            }
        }
    }
}
