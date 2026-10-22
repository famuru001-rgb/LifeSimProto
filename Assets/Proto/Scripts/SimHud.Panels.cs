using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 画面の表示の続き（2026-10-22 整理）：キャラの情報（見出しで切り替え）・思い出（アルバム・図鑑・物語と針・実績・篤・エッチの記録）・
    /// セーブとロード・物語の場面。
    /// </summary>
    public partial class SimHud
    {
        // ---- キャラの情報 ----
        static readonly string[] DetailTabs = { "ようす", "関係", "能力・成績", "持ち物", "記録", "物語" };
        const int TabNow = 0, TabRel = 1, TabAbility = 2, TabItems = 3, TabLog = 4, TabStory = 5;
        int detailTab;
        Vector2 detailScroll;
        float detailContentH = 400f;

        // ---- 思い出 ----
        static readonly string[] MemoryTabs = { "アルバム", "図鑑", "物語と針", "実績", "篤", "エッチの記録" };
        const int MemoryTabH = 5;
        int memoryTab;
        int hLoopView = 1;
        float memoryContentH = 400f;

        // ---- セーブ ----
        int loadConfirm = -1;
        string saveNotice;
        float saveNoticeUntil;
        Rect SavePanel { get { return new Rect(VW / 2f - 480f, 160f, 960f, 380f); } }

        // ---- 物語 ----
        Rect StoryPanel { get { return new Rect(VW / 2f - 600f, VirtualHeight - 360f, 1200f, 340f); } }

        static readonly Color HiLite = new Color(1f, 0.85f, 0.4f);

        // ================= キャラの情報 =================
        void DrawDetail()
        {
            Rect r = DetailPanel;
            var c = selected;
            GUI.Box(r, GUIContent.none, panel);
            float y = r.y + 8;
            float tx = r.x + 14;
            var face = Portrait(c, "face");
            if (face != null) { GUI.DrawTexture(new Rect(r.x + 12, y, 52, 52), face, ScaleMode.ScaleToFit, true); tx = r.x + 72; }
            GUI.Label(new Rect(tx, y, r.xMax - tx - 14, 30), c.displayName + "（" + c.note + "）" + (c.IsTarget ? "　あなたが操作" : ""), title);
            GUI.Label(new Rect(tx, y + 30, r.xMax - tx - 14, 24), "今：" + c.actionText, small);
            y += 58;
            if (!c.IsTarget)
            {
                string roomName = c.room != null ? "2階・" + c.room.name + "の部屋" : "なし";
                string loveText = "篤への気持ち：" + Romance.StageName(world.romance.Stage(c.id)) + "（" + Mathf.RoundToInt(world.romance.love[c.id]) + "）";
                var f = F;
                string relF = f != null ? "　篤との仲：" + Relationships.StageName(world.rel.Stage(c.id, f.id)) + "（" + Mathf.RoundToInt(world.rel.Get(c.id, f.id)) + "）" : "";
                GUI.Label(new Rect(r.x + 14, y, r.width - 28, 22), loveText + relF + "　部屋：" + roomName, tiny); y += 22;
                if (Story.Pending.Contains(c.letter))
                {
                    int ep = Story.DoneOf(c.letter);
                    var d = StoryText.Get(c.letter);
                    Color old = GUI.color; GUI.color = HiLite;
                    GUI.Label(new Rect(r.x + 14, y, r.width - 28, 22), "★ 物語の気配：話しかけると 第" + (ep + 1) + "話「" + (d != null ? d.titles[ep] : "") + "」が始まる", small);
                    GUI.color = old;
                    y += 24;
                }
            }
            y += 4;
            if (c.IsTarget) DrawFActions(r, ref y);
            else DrawGirlActions(r, c, ref y);
            y += 4;

            // 見出し
            float tw = (r.width - 28f) / DetailTabs.Length;
            for (int i = 0; i < DetailTabs.Length; i++)
                if (GUI.Button(new Rect(r.x + 14 + i * tw, y, tw - 4, 30), DetailTabs[i], detailTab == i ? buttonOn : smallButton)) { detailTab = i; detailScroll = Vector2.zero; }
            y += 36;

            Rect view = new Rect(r.x + 10, y, r.width - 20, r.yMax - y - 8);
            Rect content = new Rect(0, 0, view.width - 20, Mathf.Max(view.height, detailContentH));
            detailScroll = GUI.BeginScrollView(view, detailScroll, content);
            float cy = 2f, w = content.width - 8f;
            switch (detailTab)
            {
                case TabNow: TabNowGui(c, w, ref cy); break;
                case TabRel: TabRelGui(c, w, ref cy); break;
                case TabAbility: TabAbilityGui(c, w, ref cy); break;
                case TabItems: TabItemsGui(c, w, ref cy); break;
                case TabLog: TabLogGui(c, w, ref cy); break;
                case TabStory: TabStoryGui(c, w, ref cy); break;
            }
            detailContentH = cy + 10f;
            GUI.EndScrollView();
        }

        void DrawFActions(Rect r, ref float y)
        {
            var f = selected;
            GUI.Label(new Rect(r.x + 14, y, r.width - 28, 22), "床をクリックで移動・キャラをクリックで話しかける", tiny); y += 24;
            if (GUI.Button(new Rect(r.x + 14, y, 180, 32), "寮の手伝い（+20）", smallButton)) f.OrderWork();
            if (GUI.Button(new Rect(r.x + 202, y, 200, 32), "街のアルバイト（+70）", smallButton)) f.OrderPartTime();
            if (GUI.Button(new Rect(r.x + 410, y, 150, 32), "購買部を開く", smallButton)) shopOpen = true;
            y += 38;
            // 篤の魔法（自分に・まわりに使うもの。1日1回ずつ）
            float mx = r.x + 14;
            bool anyMagic = false;
            foreach (var sp in FMagic.All)
            {
                if (!FMagic.Has(sp.subject) || sp.subject == 6 || sp.subject == 7) continue;
                anyMagic = true;
                GUI.enabled = !FMagic.UsedToday(sp.subject, world.Day);
                if (GUI.Button(new Rect(mx, y, 180, 32), "魔法：" + sp.name, smallButton)) FMagic.Use(world, f, sp.subject, null);
                GUI.enabled = true;
                mx += 188;
            }
            if (anyMagic) y += 38;
        }

        void DrawGirlActions(Rect r, SimCharacter c, ref float y)
        {
            var f = F;
            if (f == null) return;
            if (GUI.Button(new Rect(r.x + 14, y, 150, 32), Story.Pending.Contains(c.letter) ? "話しかける ★" : "話しかける", Story.Pending.Contains(c.letter) ? buttonOn : smallButton)) { f.OrderTalk(c); world.Log("篤への指示：「" + c.displayName + "と話す」"); }
            if (GUI.Button(new Rect(r.x + 172, y, 180, 32), "お願い：勉強して", smallButton)) { f.OrderRequest(c, RequestKind.Study, null); world.Log("篤への指示：" + c.displayName + "に「勉強して」とお願いする"); }
            if (GUI.Button(new Rect(r.x + 360, y, 180, 32), "お願い：休んで", smallButton)) { f.OrderRequest(c, RequestKind.Rest, null); world.Log("篤への指示：" + c.displayName + "に「休んで」とお願いする"); }
            y += 38;
            int ts = c.TeachSubjectFor(f);
            GUI.enabled = ts >= 0;
            if (GUI.Button(new Rect(r.x + 14, y, 300, 32), ts >= 0 ? "お願い：" + Academics.Names[ts] + "を教えて" : "お願い：勉強を教えて（教われる科目なし）", smallButton))
            { f.OrderRequest(c, RequestKind.Teach, null); world.Log("篤への指示：" + c.displayName + "に「勉強を教えて」とお願いする"); }
            GUI.enabled = true;
            // 篤の魔法（相手に使うもの。近くにいるときだけ）
            bool near = f.Present && c.Present && (f.transform.position - c.transform.position).sqrMagnitude < 9f;
            float mx = r.x + 322;
            foreach (int sj in new[] { 6, 7 })
            {
                if (!FMagic.Has(sj)) continue;
                var sp = System.Array.Find(FMagic.All, q => q.subject == sj);
                GUI.enabled = near && !FMagic.UsedToday(sj, world.Day) && (sj != 6 || c.GetStage(StatusLine.Cold) > 0);
                if (GUI.Button(new Rect(mx, y, 120, 32), sp.name, smallButton)) FMagic.Use(world, f, sj, c);
                GUI.enabled = true;
                mx += 126;
            }
            y += 36;
            var pc = Player;
            GUI.Label(new Rect(r.x + 14, y, r.width - 28, 22), "渡す物は「持ち物」から" + (pc != null ? "　最近のお願い " + pc.RecentRequests(c) + "回（多いと断られやすい）" : ""), tiny);
            y += 22;
        }

        // ---- ようす ----
        void TabNowGui(SimCharacter c, float w, ref float y)
        {
            if (c.statuses.Count == 0) { GUI.Label(new Rect(4, y, w, 22), "状態：ふつう", small); y += 24; }
            foreach (var st in c.statuses)
            {
                var def = StatusBook.Get(st.line);
                var one = new List<KeyValuePair<string, Color>> { new KeyValuePair<string, Color>(def.NameOf(st.stage), def.Color) };
                float x = DrawChips(4, y, one);
                string remain = st.until > 0 ? "（あと" + FormatLeft(st.until - world.Now) + "）" : "";
                GUI.Label(new Rect(x + 4, y, w - x, 22), def.EffectOf(st.stage).desc + remain, tiny);
                y += 26;
            }
            y += 4;
            // 欲求：「時間で減る（満たすと増える）」と「時間で増える（満たすと減る）」。減る側は表示だけ「100−値」（famuru 決定 2026-10-21）
            var vals = NeedValues(c);
            var rates = NeedRates(c, vals);
            for (int grp = 0; grp < 2; grp++)
            {
                bool inv = grp == 0;
                GUI.Label(new Rect(4, y, 300, 20), inv ? "▼ 時間で減る（満たすと増える）" : "▲ 時間で増える（満たすと減る）", tiny); y += 20;
                for (int k = 0; k < NeedNames.Length; k++)
                {
                    if (NeedInverted[k] != inv) continue;
                    if (c.IsTarget && k == 7) continue; // 篤に恋心はない
                    float v = inv ? 100f - vals[k] : vals[k], rt = inv ? -rates[k] : rates[k];
                    NeedBar(new Rect(4, y, w, 20), NeedNames[k], v, rt, NeedColors[k]); y += 22;
                }
            }
            if (GUI.Button(new Rect(4, y, 220, 24), ShowHidden ? "隠しパラメータ：表示中" : "隠しパラメータ：非表示", smallButton)) ShowHidden = !ShowHidden;
            y += 28;
            if (ShowHidden) { NeedBar(new Rect(4, y, w, 20), "性欲のたまり", vals[9], rates[9], new Color(0.7f, 0.3f, 0.45f)); y += 22; }
            y += 6;
            GUI.Label(new Rect(4, y, w, 22), "今日の予定", small); y += 22;
            int shown = 0;
            foreach (var it in c.schedule)
            {
                if (shown >= 6) break;
                if (it.done && it.end < world.Now && it.kind != TaskKind.Bed) continue;
                string time = ScheduleMaker.Clock(it.start) + (it.kind == TaskKind.Class ? "〜" + ScheduleMaker.Clock(it.end) : "");
                GUI.Label(new Rect(14, y, w - 10, 20), (it.done ? "済 " : "　 ") + time + "　" + it.Label, tiny);
                y += 20;
                shown++;
            }
        }

        // ---- 関係 ----
        void TabRelGui(SimCharacter c, float w, ref float y)
        {
            var f = F;
            if (!c.IsTarget)
            {
                int st = world.romance.Stage(c.id);
                GUI.Label(new Rect(4, y, w, 24), "篤への気持ち：" + (st == 3 ? "♥ " : "") + Romance.StageName(st) + "（" + Mathf.RoundToInt(world.romance.love[c.id]) + "）" + (c.IsLover ? "　恋人" : ""), small); y += 24;
                if (f != null) { GUI.Label(new Rect(4, y, w, 22), "篤との仲：" + Relationships.StageName(world.rel.Stage(c.id, f.id)) + "（好感度 " + Mathf.RoundToInt(world.rel.Get(c.id, f.id)) + "）", small); y += 26; }
            }
            if (c.profile != null)
            {
                var pf = c.profile;
                string hob = !string.IsNullOrEmpty(pf.hobbyText) ? pf.hobbyText : HobbyName(c.hobby);
                GUI.Label(new Rect(4, y, w, 22), "趣味：" + hob + (pf.likes.Length > 0 ? "　好き：" + string.Join("・", pf.likes) : "") + (pf.dislikes.Length > 0 ? "　苦手：" + string.Join("・", pf.dislikes) : ""), tiny); y += 20;
                if (pf.traitText.Length > 0) { GUI.Label(new Rect(4, y, w, 22), "特徴：" + pf.traitText, tiny); y += 20; }
            }
            y += 8;
            GUI.Label(new Rect(4, y, w, 22), c.IsTarget ? "みんなとの仲と、篤への気持ち" : "ほかの子との仲（好感度の高い順）", small); y += 24;
            var others = new List<SimCharacter>();
            foreach (var o in world.characters) if (o != c) others.Add(o);
            others.Sort((a, b) => world.rel.Get(c.id, b.id).CompareTo(world.rel.Get(c.id, a.id)));
            Color old = GUI.color;
            foreach (var o in others)
            {
                float v = world.rel.Get(c.id, o.id);
                int stg = Relationships.StageOf(v);
                var fi = Portrait(o, "face");
                if (fi != null) GUI.DrawTexture(new Rect(4, y, 24, 24), fi, ScaleMode.ScaleToFit, true);
                GUI.Label(new Rect(32, y, 120, 24), o.displayName, small);
                Rect bar = new Rect(156, y + 7, 180, 12);
                GUI.DrawTexture(bar, barBg);
                GUI.color = stg == 2 ? new Color(0.75f, 0.3f, 0.5f) : (stg == 1 ? new Color(0.35f, 0.65f, 0.4f) : new Color(0.55f, 0.55f, 0.6f));
                GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(v / 100f), bar.height), ProtoArt.White);
                GUI.color = old;
                string extra = c.IsTarget && !o.IsTarget ? "　気持ち：" + Romance.StageName(world.romance.Stage(o.id)) : "";
                GUI.Label(new Rect(344, y, w - 344, 24), Relationships.StageName(stg) + "（" + Mathf.RoundToInt(v) + "）" + extra, tiny);
                y += 26;
            }
        }

        // ---- 能力・成績 ----
        void TabAbilityGui(SimCharacter c, float w, ref float y)
        {
            Color old = GUI.color;
            GUI.Label(new Rect(4, y, w, 22), "能力", small); y += 24;
            for (int a = 0; a < Abilities.Count; a++)
            {
                float v = c.Ab(a);
                GUI.Label(new Rect(14, y, 80, 22), Abilities.Names[a], small);
                Rect bar = new Rect(96, y + 6, 240, 12);
                GUI.DrawTexture(bar, barBg);
                GUI.color = new Color(0.55f, 0.75f, 0.95f);
                GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(v / 100f), bar.height), ProtoArt.White);
                GUI.color = old;
                GUI.Label(new Rect(346, y, 120, 22), Abilities.Rank(v) + "（" + Mathf.RoundToInt(v) + "）", small);
                y += 24;
            }
            y += 8;
            GUI.Label(new Rect(4, y, w, 22), "科目と学力（★得意 ▽苦手）　平均 " + Mathf.RoundToInt(c.academic), small); y += 24;
            var pf = c.profile;
            if (pf != null)
                foreach (int sj in pf.enrolled)
                {
                    float v = c.skill[sj];
                    GUI.Label(new Rect(14, y, 110, 22), Academics.Names[sj] + (pf.IsStrong(sj) ? "★" : (pf.IsWeak(sj) ? "▽" : "")), small);
                    Rect bar = new Rect(126, y + 6, 210, 12);
                    GUI.DrawTexture(bar, barBg);
                    GUI.color = Academics.IsPractical(sj) ? new Color(0.95f, 0.65f, 0.45f) : new Color(0.6f, 0.8f, 0.6f);
                    GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(v / 100f), bar.height), ProtoArt.White);
                    GUI.color = old;
                    GUI.Label(new Rect(346, y, 80, 22), Mathf.RoundToInt(v).ToString(), small);
                    y += 24;
                }
            y += 6;
            string assign = c.hasAssignment ? "課題（" + SimCharacter.SubjectName(c.assignSubject) + "）" + Mathf.RoundToInt(c.assignProgress) + "%（締切 " + ScheduleMaker.Clock(c.assignDeadline) + "）" : "課題なし";
            GUI.Label(new Rect(4, y, w, 22), assign + "　今週の勉強 " + Mathf.RoundToInt(c.weekStudy) + "　魔力のたまり " + Mathf.RoundToInt(c.mana), tiny); y += 22;
            GUI.Label(new Rect(4, y, w, 22), "話したい度 " + Level(c.sociability) + "　気まぐれ " + Level(c.whim) + "　真面目さ " + Level(c.diligence), tiny); y += 22;
        }

        // ---- 持ち物 ----
        void TabItemsGui(SimCharacter c, float w, ref float y)
        {
            var pc = Player;
            GUI.Label(new Rect(4, y, w, 22), "効いている物", small); y += 24;
            bool anyMod = false;
            foreach (var a in c.ItemMods)
            {
                anyMod = true;
                GUI.Label(new Rect(14, y, w - 10, 22), a.mod.name + (a.until > 0 ? "（あと" + FormatLeft(a.until - world.Now) + "）" : "") + "　" + a.mod.desc, tiny);
                y += 22;
            }
            if (!anyMod) { GUI.Label(new Rect(14, y, w - 10, 22), "なし", tiny); y += 22; }
            y += 6;
            if (!c.IsTarget)
            {
                GUI.Label(new Rect(4, y, w, 22), "部屋の家具（置き場ごとに1つ）", small); y += 24;
                var have = RoomDecor.In(c.letter);
                for (int i = 0; i < RoomDecor.SlotCount; i++)
                {
                    string n = "―";
                    foreach (var k in have) { var dd = RoomDecor.Get(k); if (dd != null && (int)dd.slot == i) { var idf = ItemBook.Get(k); n = idf != null ? idf.name : n; } }
                    GUI.Label(new Rect(14, y, w - 10, 20), RoomDecor.SlotNames[i] + "：" + n, tiny);
                    y += 20;
                }
                y += 6;
                if (pc == null) return;
                GUI.Label(new Rect(4, y, w, 22), "渡す（篤が持っていく。家具は部屋に置かれる）", small); y += 24;
                float x = 4;
                bool any = false;
                foreach (var d in ItemBook.All)
                {
                    int n = pc.Count(d.kind);
                    if (n <= 0) continue;
                    any = true;
                    GUI.enabled = pc.CanUseToday(d.kind) && F != null;
                    string lab = d.name + "×" + n + (d.furniture && RoomDecor.Liked(c.profile, d.kind) ? " ♪" : "");
                    if (GUI.Button(new Rect(x, y, 170, 30), lab, smallButton))
                    {
                        if (pc.Take(d.kind))
                        {
                            world.Log("篤への指示：" + c.displayName + "に「" + d.name + "」を渡す");
                            F.OrderGive(c, d.kind);
                        }
                    }
                    GUI.enabled = true;
                    x += 178;
                    if (x > w - 170) { x = 4; y += 34; }
                }
                if (x > 4) y += 34;
                if (!any) { GUI.Label(new Rect(4, y, w, 22), "持ち物なし（購買部で買える。♪はその子の好きな物に合う家具）", tiny); y += 24; }
            }
            else
            {
                if (pc == null) return;
                GUI.Label(new Rect(4, y, w, 22), "持ち物（自分に使う。家具は寮生に贈る物）", small); y += 24;
                float x = 4;
                bool any = false;
                foreach (var d in ItemBook.All)
                {
                    int n = pc.Count(d.kind);
                    if (n <= 0) continue;
                    any = true;
                    if (!d.selfUse) GUI.Label(new Rect(x, y + 4, 170, 24), d.name + "×" + n, tiny);
                    else if (GUI.Button(new Rect(x, y, 170, 30), d.name + "×" + n + " 使う", smallButton)) { if (pc.Take(d.kind)) c.ApplyItem(d.kind, c); }
                    x += 178;
                    if (x > w - 170) { x = 4; y += 34; }
                }
                if (x > 4) y += 34;
                if (!any) { GUI.Label(new Rect(4, y, w, 22), "なし（購買部で買える）", tiny); y += 24; }
            }
        }

        // ---- 記録（エッチの記録） ----
        void TabLogGui(SimCharacter c, float w, ref float y)
        {
            if (c.IsTarget)
            {
                GUI.Label(new Rect(4, y, w, 22), "エッチの記録（通算・この周）　一覧は「記録」ボタン（L）", small); y += 26;
                foreach (var o in world.characters)
                {
                    if (o.IsTarget) continue;
                    var last = HLog.LastOf(o.letter);
                    GUI.Label(new Rect(14, y, w - 10, 20), o.displayName + "：通算 " + HLog.TotalOf(o.letter) + "回・この周 " + HLog.LoopCountOf(o.letter, Loop.Count) + "回" + (last != null ? "　最後：" + HLog.WhenText(last) : ""), tiny);
                    y += 20;
                }
                y += 6;
                GUI.Label(new Rect(4, y, w, 22), "この周（新しい順）", small); y += 24;
                var list = HLog.OfLoop(Loop.Count);
                if (list.Count == 0) { GUI.Label(new Rect(14, y, w, 20), "まだない", tiny); y += 20; }
                for (int i = list.Count - 1, k = 0; i >= 0 && k < 40; i--, k++) { HLogRow(list[i], 14, w - 10, ref y); }
                return;
            }
            var lastE = HLog.LastOf(c.letter);
            GUI.Label(new Rect(4, y, w, 24), "エッチ：通算 " + HLog.TotalOf(c.letter) + "回（ループしても残る）・この周 " + HLog.LoopCountOf(c.letter, Loop.Count) + "回", small); y += 26;
            GUI.Label(new Rect(4, y, w, 22), "最後：" + (lastE != null ? HLog.WhenText(lastE) + "　" + lastE.place + "　" + HLog.Names(lastE) : "まだない"), tiny); y += 26;
            GUI.Label(new Rect(4, y, w, 22), "記録（新しい順・全部の周）", small); y += 24;
            int shown = 0;
            for (int i = HLog.Entries.Count - 1; i >= 0 && shown < 60; i--)
            {
                var e = HLog.Entries[i];
                if (System.Array.IndexOf(e.letters, c.letter) < 0) continue;
                HLogRow(e, 14, w - 10, ref y);
                shown++;
            }
            if (shown == 0) { GUI.Label(new Rect(14, y, w, 20), "まだない", tiny); y += 20; }
        }

        void HLogRow(HLog.Entry e, float x, float w, ref float y)
        {
            string inv = string.IsNullOrEmpty(e.inviter) ? "" : "（" + HLog.NameOf(e.inviter) + "から）";
            GUI.Label(new Rect(x, y, w, 20), e.loop + "周 " + e.day + "日目 " + ScheduleMaker.Clock(e.minute) + (e.night ? "（夜）" : "") + "　" + e.place + "　" + HLog.Names(e) + inv, tiny);
            y += 20;
        }

        // ---- 物語 ----
        void TabStoryGui(SimCharacter c, float w, ref float y)
        {
            if (c.IsTarget)
            {
                GUI.Label(new Rect(4, y, w, 24), "止まった針：" + Story.GirlHands(world) + " / " + Story.GirlCount(world) + (Story.Hands.Contains("F") ? "　篤の針も止まった（ループを抜けられる）" : "　みんなの針が止まると、篤の最後の物語"), small); y += 28;
                foreach (var o in world.characters)
                {
                    if (o.IsTarget || StoryText.Get(o.letter) == null) continue;
                    GUI.Label(new Rect(14, y, w - 10, 20), o.displayName + "：" + Story.StatusText(o), tiny);
                    y += 20;
                }
                return;
            }
            var d = StoryText.Get(c.letter);
            if (d == null) { GUI.Label(new Rect(4, y, w, 22), "物語はまだない", tiny); y += 22; return; }
            int done = Story.DoneOf(c.letter);
            bool wishKnown = Loop.Known(c, "願い", "わかった"), solveKnown = Loop.Known(c, "解決", "わかった");
            GUI.Label(new Rect(4, y, w, 22), "願い：" + (wishKnown ? d.wish : "？（第2話でわかる）"), tiny); y += 22;
            GUI.Label(new Rect(4, y, w, 22), "解決：" + (solveKnown ? d.solve : "？（第3話でわかる）"), tiny); y += 28;
            for (int ep = 0; ep < 3; ep++)
            {
                string state = ep < done ? "済" : (ep == done && Story.Pending.Contains(c.letter) ? "気配あり" : (ep == done ? "条件：" + Story.Hint(c.letter, ep) : "まだ"));
                GUI.Label(new Rect(4, y, w, 22), "第" + (ep + 1) + "話「" + (ep <= done ? d.titles[ep] : "？") + "」　" + state, small); y += 24;
            }
            y += 6;
            bool hand = Story.Hands.Contains(c.letter);
            GUI.Label(new Rect(4, y, w, 22), "針：" + (hand ? "止まった（ループしても止まったまま）" : (done >= 3 ? "まだ動いている（" + Story.Hint(c.letter, 3) + "）" + (c.IsLover ? "　恋人○" : "　恋人×") + (Loop.NightTogether.Contains(c.letter) ? "　夜○" : "　夜×") : "第3話のあと")), small);
            y += 24;
        }

        // ================= 思い出 =================
        void DrawMemory()
        {
            Rect r = MemoryPanel;
            GUI.Box(r, GUIContent.none, panel);
            GUI.Box(r, GUIContent.none, panel); // 後ろの文字が透けないように重ねる
            GUI.Label(new Rect(r.x + 14, r.y + 8, r.width - 120, 30), "思い出（" + Loop.Count + "周目）　ループしても残る", title);
            if (GUI.Button(new Rect(r.xMax - 92, r.y + 8, 80, 30), "閉じる", smallButton)) memoryOpen = false;
            float tw = (r.width - 28f) / MemoryTabs.Length;
            for (int i = 0; i < MemoryTabs.Length; i++)
                if (GUI.Button(new Rect(r.x + 14 + i * tw, r.y + 44, tw - 4, 30), MemoryTabs[i], memoryTab == i ? buttonOn : smallButton)) { memoryTab = i; memoryScroll = Vector2.zero; if (i == MemoryTabH) hLoopView = Loop.Count; }
            float top = r.y + 82;
            if (memoryTab == MemoryTabH)
            {
                // 周の切り替え（スクロールの外）
                int maxLoop = HLog.MaxLoop();
                hLoopView = Mathf.Clamp(hLoopView, 1, maxLoop);
                GUI.enabled = hLoopView > 1;
                if (GUI.Button(new Rect(r.x + 14, top, 50, 30), "◀", smallButton)) hLoopView--;
                GUI.enabled = true;
                GUI.Label(new Rect(r.x + 70, top + 3, 170, 26), hLoopView + "周目" + (hLoopView == Loop.Count ? "（今）" : ""), small);
                GUI.enabled = hLoopView < maxLoop;
                if (GUI.Button(new Rect(r.x + 240, top, 50, 30), "▶", smallButton)) hLoopView++;
                GUI.enabled = true;
                GUI.Label(new Rect(r.x + 310, top + 4, r.width - 330, 24), "この周 " + HLog.OfLoop(hLoopView).Count + "回　通算 " + HLog.Entries.Count + "回（描写はしない）", tiny);
                top += 38;
            }
            Rect view = new Rect(r.x + 10, top, r.width - 20, r.yMax - top - 10);
            Rect content = new Rect(0, 0, view.width - 20, Mathf.Max(view.height, memoryContentH));
            memoryScroll = GUI.BeginScrollView(view, memoryScroll, content);
            float y = 2f, w = content.width - 8f;
            switch (memoryTab)
            {
                case 0: MemAlbum(w, ref y); break;
                case 1: MemZukan(w, ref y); break;
                case 2: MemStory(w, ref y); break;
                case 3: MemAchieve(w, ref y); break;
                case 4: MemF(w, ref y); break;
                case MemoryTabH: MemH(w, ref y); break;
            }
            memoryContentH = y + 10f;
            GUI.EndScrollView();
        }

        void MemAlbum(float w, ref float y)
        {
            GUI.Label(new Rect(4, y, w, 22), "アルバム（" + Album.Entries.Count + "）　新しい順", small); y += 26;
            for (int i = Album.Entries.Count - 1; i >= 0; i--) { GUI.Label(new Rect(4, y, w, 22), Album.Entries[i], tiny); y += 22; }
        }

        void MemZukan(float w, ref float y)
        {
            GUI.Label(new Rect(4, y, w, 22), "キャラ図鑑（会話と物語で埋まる）", small); y += 28;
            foreach (var c in world.characters)
            {
                if (c.IsTarget || c.profile == null) continue;
                var face = Portrait(c, "face");
                if (face != null) GUI.DrawTexture(new Rect(4, y, 44, 44), face, ScaleMode.ScaleToFit, true);
                var known = new List<string>();
                foreach (var t in c.profile.likes) known.Add(Loop.Known(c, "好き", t) ? t : "？");
                var bad = new List<string>();
                foreach (var t in c.profile.dislikes) bad.Add(Loop.Known(c, "苦手", t) ? t : "？");
                bool bd = false;
                foreach (var z in Loop.Zukan) if (z.StartsWith(c.letter + ":誕生日:")) { bd = true; break; }
                var d = StoryText.Get(c.letter);
                GUI.Label(new Rect(56, y, w - 56, 22), c.displayName + "（" + c.profile.race + "）　好き：" + string.Join("・", known.ToArray()) + "　苦手：" + string.Join("・", bad.ToArray()) + "　誕生日：" + (bd ? "わかった" : "？"), tiny);
                GUI.Label(new Rect(56, y + 22, w - 56, 22), "願い：" + (d != null && Loop.Known(c, "願い", "わかった") ? d.wish : "？") + "　物語：" + Story.StatusText(c) + "　エッチ通算 " + HLog.TotalOf(c.letter) + "回", tiny);
                y += 52;
            }
        }

        void MemStory(float w, ref float y)
        {
            int hands = Story.GirlHands(world), total = Story.GirlCount(world);
            GUI.Label(new Rect(4, y, w, 24), "地下の古い時計：止まった針 " + hands + " / " + total + (Story.Hands.Contains("F") ? "　＋ 篤の針（ループを抜けられる）" : "　＋ 篤の針（みんなの針が止まると、最後の物語）"), small); y += 30;
            Color old = GUI.color;
            foreach (var c in world.characters)
            {
                if (c.IsTarget) continue;
                var d = StoryText.Get(c.letter);
                if (d == null) continue;
                int done = Story.DoneOf(c.letter);
                bool hand = Story.Hands.Contains(c.letter);
                var face = Portrait(c, "face");
                if (face != null) GUI.DrawTexture(new Rect(4, y, 40, 40), face, ScaleMode.ScaleToFit, true);
                string marks = "";
                for (int i = 0; i < 3; i++) marks += i < done ? "●" : "○";
                GUI.color = hand ? HiLite : old;
                GUI.Label(new Rect(52, y, w - 52, 22), c.displayName + "　" + marks + "　" + Story.StatusText(c), small);
                GUI.color = old;
                string next = hand ? "" : (done < 3 ? "次（第" + (done + 1) + "話）：" + Story.Hint(c.letter, done) : "針：" + Story.Hint(c.letter, 3));
                GUI.Label(new Rect(52, y + 22, w - 52, 20), next, tiny);
                y += 46;
            }
        }

        void MemAchieve(float w, ref float y)
        {
            GUI.Label(new Rect(4, y, w, 22), "実績 " + Achievements.Got.Count + " / " + Achievements.All.Count, small); y += 28;
            Color old = GUI.color;
            foreach (var a in Achievements.All)
            {
                string when;
                bool got = Achievements.Got.TryGetValue(a.id, out when);
                GUI.color = got ? HiLite : new Color(0.75f, 0.72f, 0.68f);
                GUI.Label(new Rect(4, y, w, 22), (got ? "◆ " : "◇ ") + a.name + "　" + a.desc + (got ? "（" + when + "）" : ""), got ? small : tiny);
                GUI.color = old;
                y += 24;
            }
        }

        void MemF(float w, ref float y)
        {
            var magic = new List<string>();
            foreach (var sp in FMagic.All) magic.Add(FMagic.Has(sp.subject) ? sp.name : "？（" + Academics.Names[sp.needSubject] + " " + Mathf.RoundToInt(sp.need) + "で開く）");
            GUI.Label(new Rect(4, y, w, 22), "篤の魔法：", small); y += 24;
            foreach (var m in magic) { GUI.Label(new Rect(14, y, w, 20), m, tiny); y += 20; }
            y += 6;
            var fx = world.RomanceTarget;
            if (fx != null) { GUI.Label(new Rect(4, y, w, 22), "篤の能力（持ち越し）：" + fx.AbilityText(), tiny); y += 26; }
            GUI.Label(new Rect(4, y, w, 22), "場所（篤の能力＋縁のある子の物語の第1話で開く）：", small); y += 24;
            foreach (var g in PlaceGates.All)
            {
                GUI.Label(new Rect(14, y, w, 20), g.place + (Loop.OpenedPlaces.Contains(g.place) ? "（開いた）" : "（" + PlaceGates.Text(g) + "）"), tiny);
                y += 20;
            }
        }

        void MemH(float w, ref float y)
        {
            // キャラごとの回数
            var parts = new List<string>();
            foreach (var c in world.characters)
            {
                if (c.IsTarget) continue;
                parts.Add(c.displayName + " " + HLog.LoopCountOf(c.letter, hLoopView) + "／" + HLog.TotalOf(c.letter));
            }
            GUI.Label(new Rect(4, y, w, 22), "キャラごとの回数（この周／通算）", small); y += 24;
            for (int k = 0; k < parts.Count; k += 4)
            {
                GUI.Label(new Rect(14, y, w - 10, 20), string.Join("　", parts.GetRange(k, Mathf.Min(4, parts.Count - k)).ToArray()), tiny);
                y += 20;
            }
            y += 8;
            GUI.Label(new Rect(4, y, w, 22), hLoopView + "周目の記録（新しい順）", small); y += 24;
            var list = HLog.OfLoop(hLoopView);
            if (list.Count == 0) { GUI.Label(new Rect(14, y, w, 20), "この周の記録はまだない", tiny); y += 20; }
            for (int i = list.Count - 1; i >= 0; i--) HLogRow(list[i], 14, w - 10, ref y);
        }

        // ================= セーブとロード =================
        void DrawSave()
        {
            Rect r = SavePanel;
            GUI.Box(r, GUIContent.none, panel);
            GUI.Box(r, GUIContent.none, panel);
            GUI.Label(new Rect(r.x + 14, r.y + 8, r.width - 120, 30), "セーブとロード", title);
            if (GUI.Button(new Rect(r.xMax - 92, r.y + 8, 80, 30), "閉じる", smallButton)) { saveOpen = false; loadConfirm = -1; }
            GUI.Label(new Rect(r.x + 14, r.y + 42, r.width - 28, 22), "自動セーブは毎朝6時。読み込むと、保存した日と時刻から続く（キャラの今の行動はやり直し）", tiny);
            float y = r.y + 72;
            for (int slot = 0; slot <= SaveSystem.ManualSlots; slot++)
            {
                var d = SaveSystem.Peek(slot);
                GUI.Label(new Rect(r.x + 14, y + 4, 140, 26), SaveSystem.SlotName(slot), small);
                GUI.Label(new Rect(r.x + 156, y + 6, r.width - 156 - 330, 24), SaveSystem.Describe(d), tiny);
                if (slot > 0)
                {
                    GUI.enabled = !Story.IsActive;
                    if (GUI.Button(new Rect(r.xMax - 324, y, 120, 34), "ここに保存", smallButton))
                    {
                        loadConfirm = -1;
                        bool ok = SaveSystem.Save(world, slot);
                        saveNotice = ok ? SaveSystem.SlotName(slot) + "に保存しました" : "保存できませんでした";
                        saveNoticeUntil = Time.unscaledTime + 3f;
                        if (ok) world.Log("（" + SaveSystem.SlotName(slot) + "に保存した）");
                    }
                    GUI.enabled = true;
                }
                GUI.enabled = d != null;
                bool confirm = loadConfirm == slot;
                if (GUI.Button(new Rect(r.xMax - 196, y, 182, 34), confirm ? "本当に読み込む" : "読み込む", confirm ? buttonOn : smallButton))
                {
                    if (confirm) { loadConfirm = -1; saveOpen = false; SaveSystem.Load(slot); }
                    else loadConfirm = slot;
                }
                GUI.enabled = true;
                y += 46;
            }
            if (loadConfirm >= 0) { GUI.Label(new Rect(r.x + 14, y + 4, r.width - 28, 22), "保存していない今の続きは消えます。もう一度押すと読み込みます", small); y += 26; }
            if (!string.IsNullOrEmpty(saveNotice) && Time.unscaledTime < saveNoticeUntil) GUI.Label(new Rect(r.x + 14, y + 4, r.width - 28, 22), saveNotice, small);
            GUI.Label(new Rect(r.x + 14, r.yMax - 28, r.width - 28, 22), "保存先：" + SaveSystem.Folder, tiny);
        }

        // ================= 試験用（ClaudeBridge の ui 命令） =================
        /// <summary>試験用：画面を開く。what：detail（letter・見出しの番号）／memory（見出しの番号）／save／shop／close。</summary>
        public void DebugOpen(string what, string letter, int tab)
        {
            switch (what)
            {
                case "detail":
                    var c = world.characters.Find(x => x.letter == letter);
                    if (c != null) Select(c);
                    detailTab = Mathf.Clamp(tab, 0, DetailTabs.Length - 1);
                    detailScroll = Vector2.zero;
                    break;
                case "memory":
                    memoryOpen = true; memoryTab = Mathf.Clamp(tab, 0, MemoryTabs.Length - 1); memoryScroll = Vector2.zero; hLoopView = Loop.Count;
                    break;
                case "save": saveOpen = true; loadConfirm = -1; break;
                case "shop": shopOpen = true; shopShelf = (ItemShelf)Mathf.Clamp(tab, 0, ShelfNames.Length - 1); break;
                default: memoryOpen = false; saveOpen = false; shopOpen = false; examOpen = false; Select(null); break;
            }
        }

        // ================= 物語の場面 =================
        void DrawStory()
        {
            Rect r = StoryPanel;
            GUI.Box(r, GUIContent.none, panel);
            GUI.Box(r, GUIContent.none, panel); // 少し濃く
            GUI.Label(new Rect(r.x + 18, r.y + 10, r.width - 36, 30), Story.ActiveTitle, title);
            var lines = Story.ActiveLines;
            int cur = Mathf.Clamp(Story.ActiveLine, 0, Mathf.Max(0, lines.Length - 1));
            int from = Mathf.Max(0, cur - 6);
            float y = r.y + 48;
            Color old = GUI.color;
            for (int i = from; i <= cur && i < lines.Length; i++)
            {
                string line = lines[i];
                bool now = i == cur;
                // 話している人の顔
                int colon = line.IndexOf('：');
                if (colon > 0 && colon < 10)
                {
                    string who = line.Substring(0, colon);
                    var sc = world.characters.Find(x => x.displayName == who);
                    var fi = sc != null ? Portrait(sc, "face") : null;
                    if (fi != null) GUI.DrawTexture(new Rect(r.x + 18, y, 28, 28), fi, ScaleMode.ScaleToFit, true);
                }
                GUI.color = now ? Color.white : new Color(1f, 1f, 1f, 0.55f);
                GUI.Label(new Rect(r.x + 52, y + 2, r.width - 70, 28), line, now ? label : small);
                GUI.color = old;
                y += 34;
            }
            bool last = cur >= lines.Length - 1;
            if (GUI.Button(new Rect(r.xMax - 220, r.yMax - 48, 200, 38), last ? "閉じる（スペース）" : "次へ（スペース）", button)) Story.Next(world);
            GUI.Label(new Rect(r.x + 18, r.yMax - 40, 600, 24), "（場面の絵はあとで入れる。時間は止まっている）", tiny);
        }
    }
}
