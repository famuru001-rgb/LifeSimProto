using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace LifeSimProto.EditorTools
{
    /// <summary>
    /// ClaudeBridge の追加の命令（2026-10-22 クラウド設定のチャット）。
    /// ・compile_log.txt：スクリプトの読み込み（コンパイル）が終わるたびに、エラーと警告を Logs\compile_log.txt に書く
    /// ・teststory（記号）（話の番号1〜3）＝その話を始める／storyall＝場面を最後まで進める
    /// ・save（枠）／load（枠）＝セーブ・ロード（0 は自動）
    /// ・testfurn（記号）（家具の名前 例 furnplant）＝篤がその子に家具を贈ったことにする
    /// ・testhlog（記号）＝その子と篤のエッチの記録を1つ足す（記録の画面の確認用）
    /// ・progress＝物語・実績・記録・家具・セーブの中身を Logs\progress.txt に書く
    /// ・ui（detail 記号 見出し／memory 見出し／save／shop 棚／close）＝画面を開く
    /// ・cam（記号）（距離）（角度）＝その子の部屋を見る
    /// ・shot（名前）＝ゲーム画面を Logs\shot_（名前）.png に撮る（画面の文字も写る）
    /// </summary>
    [InitializeOnLoad]
    public static class ClaudeBridgeExtra
    {
        static string Dir { get { return Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs")); } }

        static ClaudeBridgeExtra()
        {
            CompilationPipeline.assemblyCompilationFinished -= OnCompiled;
            CompilationPipeline.assemblyCompilationFinished += OnCompiled;
        }

        static void OnCompiled(string assembly, CompilerMessage[] messages)
        {
            try
            {
                var sb = new StringBuilder();
                int err = 0, warn = 0;
                foreach (var m in messages)
                {
                    if (m.type == CompilerMessageType.Error) err++;
                    else if (m.type == CompilerMessageType.Warning) { warn++; continue; }
                    sb.AppendLine("[" + m.type + "] " + m.file + "(" + m.line + "): " + m.message);
                }
                string head = "== " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + Path.GetFileName(assembly) + " エラー " + err + " 警告 " + warn + "\n";
                File.AppendAllText(Path.Combine(Dir, "compile_log.txt"), head + sb, Encoding.UTF8);
            }
            catch { }
        }

        public static bool Handle(string cmd, LoungeWorld w, StringBuilder log)
        {
            var a = cmd.Split(' ');
            switch (a[0])
            {
                case "teststory":
                case "storyall":
                case "save":
                case "load":
                case "testfurn":
                case "testhlog":
                case "progress":
                case "ui":
                case "shot":
                case "cam":
                case "campos":
                case "finvite":
                case "ftalk":
                case "fmove":
                case "jumpwatch":
                case "fastwatch":
                case "hipstrace":
                case "camfollow":
                case "hvigor":
                case "dgo":
                case "dtrain":
                case "navcheck":
                case "navpng":
                case "goto":
                case "wall":
                case "fog":
                case "place":
                case "trust":
                case "worry":
                case "dpick":
                case "dstat":
                case "cup":
                case "club":
                case "animdump":
                case "hstat":
                case "perf":
                case "floorshots":
                case "motionshots":
                case "gestureshots":
                case "motionvideo":
                case "usagevideo":
                case "hpreview":
                case "life":
                case "hpreviewshot":
                case "skirtlegshots":
                case "skirtleg":
                case "skirtlegbench":
                case "uniformsitshots":
                    break;
                default:
                    return false;
            }
            if (w == null) { log.AppendLine(cmd + ": not playing"); return true; }
            try
            {
                switch (a[0])
                {
                    case "teststory":
                    {
                        string l = a.Length > 1 ? a[1].ToUpperInvariant() : "B";
                        var c = w.characters.Find(x => x.letter == l);
                        if (c == null) { log.AppendLine("teststory: no " + l); break; }
                        int ep = a.Length > 2 ? Mathf.Clamp(int.Parse(a[2]) - 1, 0, 2) : Story.DoneOf(l);
                        Story.Done[l] = ep;
                        Story.Pending.Add(l);
                        Story.OnTalk(w, c);
                        log.AppendLine("teststory: " + Story.ActiveTitle + " lines=" + Story.ActiveLines.Length);
                        break;
                    }
                    case "storyall":
                    {
                        int n = 0;
                        while (Story.IsActive && n < 50) { Story.Next(w); n++; }
                        log.AppendLine("storyall: " + n + " steps");
                        break;
                    }
                    case "save":
                    {
                        int slot = a.Length > 1 ? int.Parse(a[1]) : 1;
                        log.AppendLine("save " + slot + ": " + SaveSystem.Save(w, slot) + " " + SaveSystem.Folder);
                        break;
                    }
                    case "load":
                    {
                        int slot = a.Length > 1 ? int.Parse(a[1]) : 1;
                        log.AppendLine("load " + slot + ": " + SaveSystem.Load(slot));
                        break;
                    }
                    case "testfurn":
                    {
                        string l = a.Length > 1 ? a[1].ToUpperInvariant() : "B";
                        var c = w.characters.Find(x => x.letter == l);
                        var kind = (ItemKind)System.Enum.Parse(typeof(ItemKind), a.Length > 2 ? a[2] : "FurnPlant", true);
                        var d = ItemBook.Get(kind);
                        if (c == null || d == null) { log.AppendLine("testfurn: ng"); break; }
                        w.GiveFurniture(c, w.RomanceTarget, d);
                        log.AppendLine("testfurn: " + d.name + " -> " + c.displayName);
                        break;
                    }
                    case "testhlog":
                    {
                        string l = a.Length > 1 ? a[1].ToUpperInvariant() : "B";
                        var c = w.characters.Find(x => x.letter == l);
                        var f = w.RomanceTarget;
                        if (c == null || f == null) { log.AppendLine("testhlog: ng"); break; }
                        HLog.Record(w, new List<SimCharacter> { c, f }, c.room != null ? c.room.AreaName : "試験", c);
                        log.AppendLine("testhlog: " + c.displayName + " total=" + HLog.TotalOf(l));
                        break;
                    }
                    case "progress":
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine("== " + Loop.Count + "周目 " + w.Day + "日目 " + ScheduleMaker.Clock(w.Now));
                        foreach (var c in w.characters)
                            sb.AppendLine(c.displayName + "：物語 " + Story.DoneOf(c.letter) + "話・" + (Story.Pending.Contains(c.letter) ? "気配あり・" : "") + (Story.Hands.Contains(c.letter) ? "針止まった・" : "") + "エッチ通算" + HLog.TotalOf(c.letter) + "・家具" + RoomDecor.CountIn(c.letter) + "・好感度(篤)" + (w.RomanceTarget != null ? Mathf.RoundToInt(w.rel.Get(c.id, w.RomanceTarget.id)).ToString() : "-"));
                        sb.AppendLine("実績：" + string.Join("、", new List<string>(Achievements.Got.Keys).ToArray()));
                        sb.AppendLine("場所：" + string.Join("、", Loop.OpenedPlaces.ToArray()));
                        sb.AppendLine("記録：" + HLog.Entries.Count + "件　物語の場面中：" + Story.IsActive);
                        for (int i = 0; i <= SaveSystem.ManualSlots; i++) sb.AppendLine("セーブ" + i + "：" + SaveSystem.Describe(SaveSystem.Peek(i)));
                        sb.AppendLine("銀貨 " + (w.player != null ? w.player.money : 0) + "　評判 " + w.dormReputation);
                        File.AppendAllText(Path.Combine(Dir, "progress.txt"), sb.ToString(), Encoding.UTF8);
                        log.AppendLine("progress: ok");
                        break;
                    }
                    case "ui":
                    {
                        var hud = SimHud.Instance;
                        if (hud == null) { log.AppendLine("ui: no hud"); break; }
                        string what = a.Length > 1 ? a[1] : "close";
                        bool withWho = what == "detail" || what == "invite"; // invite 番号 1/2（エッチに誘う：場所・内容の画面）
                        string letter = withWho && a.Length > 2 ? a[2].ToUpperInvariant() : null;
                        int tab = 0;
                        if (withWho && a.Length > 3) tab = int.Parse(a[3]);
                        else if (!withWho && a.Length > 2) tab = int.Parse(a[2]);
                        hud.DebugOpen(what, letter, tab);
                        log.AppendLine("ui: " + what + " " + letter + " " + tab);
                        break;
                    }
                    case "cam":
                    {
                        // cam（記号）（距離）（角度）＝その子の部屋を上から見る（家具の確認用）
                        string l = a.Length > 1 ? a[1].ToUpperInvariant() : "B";
                        var c = w.characters.Find(x => x.letter == l);
                        var rig = w.mainCamera != null ? w.mainCamera.GetComponent<CameraRig>() : null;
                        if (c == null || rig == null || c.room == null) { log.AppendLine("cam: ng"); break; }
                        rig.follow = null;
                        rig.pivot = new Vector3(c.room.rect.center.x, LoungeWorld.FloorY(c.room.floor) + 0.5f, c.room.rect.center.y);
                        rig.distance = a.Length > 2 ? float.Parse(a[2], System.Globalization.CultureInfo.InvariantCulture) : 6f;
                        rig.pitch = a.Length > 3 ? float.Parse(a[3], System.Globalization.CultureInfo.InvariantCulture) : 60f;
                        w.wallMode = WallMode.Low;
                        log.AppendLine("cam: " + c.room.name);
                        break;
                    }
                    case "campos":
                    {
                        // 試験用：campos x y z 距離 見下ろす角度 向き（カメラを好きな所へ。壁は低く）
                        var rig = w.mainCamera != null ? w.mainCamera.GetComponent<CameraRig>() : null;
                        if (rig == null || a.Length < 4) { log.AppendLine("campos: ng"); break; }
                        var ci = System.Globalization.CultureInfo.InvariantCulture;
                        rig.follow = null;
                        rig.pivot = new Vector3(float.Parse(a[1], ci), float.Parse(a[2], ci), float.Parse(a[3], ci));
                        if (a.Length > 4) rig.distance = float.Parse(a[4], ci);
                        if (a.Length > 5) rig.pitch = float.Parse(a[5], ci);
                        if (a.Length > 6) rig.yaw = float.Parse(a[6], ci);
                        w.wallMode = WallMode.Low;
                        log.AppendLine("campos: ok");
                        break;
                    }
                    case "jumpwatch":
                    {
                        // 試験用：jumpwatch on／off／dump（1コマで大きく動いたキャラを Logs\jump_log.txt に書く。2026-10-31）
                        string m = a.Length > 1 ? a[1] : "dump";
                        if (m == "on") { SimCharacter.JumpLog.Length = 0; SimCharacter.JumpWatch = true; }
                        else if (m == "off") SimCharacter.JumpWatch = false;
                        System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../Logs/jump_log.txt"), SimCharacter.JumpLog.ToString(), new UTF8Encoding(false));
                        log.AppendLine("jumpwatch: " + m + "（" + SimCharacter.JumpLog.Length + "文字）");
                        break;
                    }
                    case "fastwatch":
                    {
                        // 試験用：fastwatch on／off／dump（見た目の腰が速く動いた区間を Logs\\fast_log.txt に書く。2026-10-31）
                        string m = a.Length > 1 ? a[1] : "dump";
                        if (m == "on") { ProtoBody.FastLog.Length = 0; ProtoBody.FastWatch = true; }
                        else if (m == "off") ProtoBody.FastWatch = false;
                        System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../Logs/fast_log.txt"), ProtoBody.FastLog.ToString(), new UTF8Encoding(false));
                        log.AppendLine("fastwatch: " + m + "（" + ProtoBody.FastLog.Length + "文字）");
                        break;
                    }
                    case "hipstrace":
                    {
                        // 試験用：hipstrace 番号 秒／hipstrace dump（1人の体の位置と腰の位置を毎コマ Logs\\hips_trace.txt へ。2026-10-31）
                        if (a.Length > 1 && a[1] == "dump")
                        {
                            System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../Logs/hips_trace.txt"), "time\tdt\trootX\trootZ\thipLX\thipLY\thipLZ\thipX\thipZ\tclip\tclipT\tfade\tstate\tact\n" + ProtoBody.TraceLog, new UTF8Encoding(false));
                            log.AppendLine("hipstrace: dump " + ProtoBody.TraceLog.Length);
                            break;
                        }
                        ProtoBody.TraceLog.Length = 0;
                        ProtoBody.TraceWho = w.characters[int.Parse(a[1])];
                        ProtoBody.TraceUntil = Time.time + (a.Length > 2 ? float.Parse(a[2]) : 10f);
                        log.AppendLine("hipstrace: " + ProtoBody.TraceWho.displayName);
                        break;
                    }
                    case "hvigor":
                    {
                        // 試験用：hvigor 値（篤の精力を変える。2026-11-01）
                        var f = w.RomanceTarget;
                        f.vigor = float.Parse(a[1], System.Globalization.CultureInfo.InvariantCulture);
                        log.AppendLine("hvigor: " + f.vigor);
                        break;
                    }
                    case "wall":
                    {
                        // 試験用：wall all|low|auto（壁の見せ方。campos のあとに使う。2026-11-08 家具のチャット）
                        w.wallMode = a.Length > 1 && a[1] == "all" ? WallMode.All : a.Length > 1 && a[1] == "auto" ? WallMode.Auto : WallMode.Low;
                        log.AppendLine("wall: " + w.wallMode);
                        break;
                    }
                    case "trust":
                    case "worry":
                    {
                        // 試験用：信頼と悩みの段（Bond.cs。2026-11-16 本体チャット）
                        // trust 記号（数字・段・悩み）／trust set 記号 信頼|警戒|関心|段 値／worry give 記号 [悩み]／worry ask 記号／worry act 記号（悩みのためにする。そばでなくても）／worry list／worry reload
                        var bb = w.bond;
                        string m = a.Length > 1 ? a[1] : "list";
                        if (a[0] == "worry" && m == "reload") { WorryBook.Reload(); log.AppendLine("worry reload: 悩み " + WorryBook.All.Count + "・直す所 " + string.Join("／", WorryBook.Errors)); break; }
                        if (a[0] == "worry" && m == "list")
                        {
                            var sbw = new StringBuilder("worry list: ");
                            foreach (var c in w.characters) if (c != null && !c.IsTarget && !c.IsMob && !string.IsNullOrEmpty(bb.worry[c.id])) sbw.Append(c.letter + ":" + bb.worry[c.id] + (bb.worryKnown[c.id] ? "（知っている " + bb.worryProg[c.id] + "）" : "") + " ");
                            log.AppendLine(sbw.ToString());
                            break;
                        }
                        string letter = a[0] == "trust" && m != "set" ? m : (a.Length > 2 ? a[2] : "");
                        var g = w.characters.Find(x => x != null && x.letter == letter.ToUpperInvariant());
                        if (g == null) { log.AppendLine(a[0] + ": 記号が無い"); break; }
                        int gi = g.id;
                        if (a[0] == "trust" && m == "set" && a.Length > 4)
                        {
                            float v = float.Parse(a[4], System.Globalization.CultureInfo.InvariantCulture);
                            if (a[3] == "信頼") bb.trust[gi] = v; else if (a[3] == "警戒") bb.wary[gi] = v; else if (a[3] == "関心") bb.interest[gi] = v; else if (a[3] == "段") bb.stage[gi] = (int)v;
                        }
                        else if (a[0] == "worry" && m == "give") log.AppendLine("worry give: " + bb.TryGiveWorry(w, g, a.Length > 3 ? a[3] : null));
                        else if (a[0] == "worry" && m == "ask") bb.Ask(w, g);
                        else if (a[0] == "worry" && m == "act") { var wd = bb.WorryOf(gi); string why = bb.CanAct(w, g, w.RomanceTarget); bb.ForceAct(w, g); log.AppendLine("worry act: " + (wd != null ? wd.action : "悩みが無い") + "（ふつうなら：" + (why ?? "できる") + "）"); }
                        log.AppendLine(a[0] + ": " + g.displayName + " 段" + bb.stage[gi] + "（" + BondBook.StageNames[Mathf.Clamp(bb.stage[gi], 0, 3)] + "）信頼 " + bb.trust[gi].ToString("0") + "・警戒 " + bb.wary[gi].ToString("0") + "・関心 " + bb.interest[gi].ToString("0") + "・気持ち " + g.LoveStage + "・悩み " + (bb.worry[gi] ?? "なし") + (bb.worryKnown[gi] ? "（知っている " + bb.worryProg[gi] + "）" : "") + (bb.promise[gi] ? "・約束あり" : ""));
                        break;
                    }
                    case "place":
                    {
                        // 試験用：設置の仕組み（Placement.cs。2026-11-15 本体チャット）
                        // place list／give 名前 [数]／forget（試験のあと：全部消す）／fx（効きめが付いている人）／room（篤の部屋の範囲）／check 名前 dx dz [向き]／try 名前 dx dz [向き]（篤の部屋の真ん中から）／ghost 名前 dx dz [向き]（置く場面の形を出す）／end／clear／reload
                        var ci2 = System.Globalization.CultureInfo.InvariantCulture;
                        string m = a.Length > 1 ? a[1] : "list";
                        var room = w.AreaNamed("篤の部屋");
                        if (m == "give" && a.Length > 2) { PlaceBook.Give(a[2], a.Length > 3 ? int.Parse(a[3]) : 1); log.AppendLine("place give: " + a[2] + " 所持 " + PlaceBook.Count(a[2])); break; }
                        if (m == "reload") { PlaceBook.Reload(); log.AppendLine("place reload: 品物 " + PlaceBook.All.Count + "・場所 " + PlaceBook.Spots.Count + "・直す所 " + string.Join("／", PlaceBook.Errors)); break; }
                        if (m == "room") { log.AppendLine("place room: " + (room == null ? "篤の部屋が無い" : room.rect + " 階 " + room.floor + " 床の高さ " + LoungeWorld.FloorY(room.floor))); break; }
                        if (m == "clear") { foreach (var p in new List<PlaceBook.PlacedItem>(PlaceBook.Placed)) w.Unplace(p); log.AppendLine("place clear: 全部しまった"); break; }
                        if (m == "end") { if (SimHud.Instance != null) SimHud.Instance.DebugEndPlace(); log.AppendLine("place end"); break; }
                        if (m == "tick" && a.Length > 2) { w.DebugToolTick(float.Parse(a[2], ci2)); log.AppendLine("place tick: " + a[2] + "分・見つかった " + w.ToolFoundCount + "・時間切れ " + w.ToolExpiredCount + "・置いた物 " + PlaceBook.Placed.Count); break; }
                        if (m == "use" && a.Length > 2) { var ud = PlaceBook.Get(a[2]); if (ud != null && PlaceBook.Count(ud.name) <= 0) PlaceBook.Give(ud.name); w.UseCarry(ud); log.AppendLine("place use: " + a[2] + "・ランタン " + (PlaceBook.LanternOn ?? "なし") + "・透視薬 " + (PlaceBook.PotionUntil > w.Now ? "効いている" : "なし")); break; }
                        if (m == "at" && a.Length > 4)
                        {
                            // 試験用：place at 名前 x z [y]（世界の位置に置く。道具を外や学園に）
                            var ad = PlaceBook.Get(a[2]);
                            if (ad == null) { log.AppendLine("place at: 品物が無い"); break; }
                            if (PlaceBook.Count(ad.name) <= 0) PlaceBook.Give(ad.name);
                            var ap = new Vector3(float.Parse(a[3], ci2), a.Length > 5 ? float.Parse(a[5], ci2) : 0f, float.Parse(a[4], ci2));
                            log.AppendLine("place at: " + ad.name + " → " + (w.Place(ad.name, ap, 0) ?? "置いた"));
                            break;
                        }
                        if (m == "fx")
                        {
                            // 置いた家具の効きめが付いている人（Placement.Effects.cs）
                            var sbx = new StringBuilder("place fx: ");
                            foreach (var c in w.characters)
                            {
                                if (c == null) continue;
                                var ids = new List<string>();
                                foreach (var am in c.ItemMods) if (am.mod != null && am.mod.id.StartsWith("place:")) ids.Add(am.mod.id.Substring(6));
                                if (ids.Count == 0 && c.placeVigorRegen == 1f && c.placeVigorMaxAdd == 0f && c.placeAffinity == 1f) continue;
                                sbx.Append(c.displayName + "[" + string.Join("・", ids.ToArray()) + (c.placeVigorRegen != 1f ? " 精力の戻り×" + c.placeVigorRegen.ToString("0.00") : "") + (c.placeVigorMaxAdd != 0f ? " 精力の最大＋" + c.placeVigorMaxAdd : "") + (c.placeAffinity != 1f ? " 好感度×" + c.placeAffinity.ToString("0.00") : "") + "] ");
                            }
                            log.AppendLine(sbx.ToString());
                            break;
                        }
                        if (m == "forget") { foreach (var p in new List<PlaceBook.PlacedItem>(PlaceBook.Placed)) w.Unplace(p, false); PlaceBook.Owned.Clear(); log.AppendLine("place forget: 持ち物・置いた物を全部消した（試験のあと用）"); break; }
                        if ((m == "check" || m == "try" || m == "ghost") && a.Length > 4 && room != null)
                        {
                            var d = PlaceBook.Get(a[2]);
                            if (d == null) { log.AppendLine("place: 品物が無い " + a[2]); break; }
                            var pos = new Vector3(room.rect.center.x + float.Parse(a[3], ci2), LoungeWorld.FloorY(room.floor), room.rect.center.y + float.Parse(a[4], ci2));
                            int yaw = a.Length > 5 ? int.Parse(a[5]) : 0;
                            AreaInfo ar;
                            string why;
                            if (m == "check") why = w.CheckPlace(d, pos, yaw, out ar);
                            else if (m == "try") { if (PlaceBook.Count(d.name) <= 0) PlaceBook.Give(d.name); why = w.Place(d.name, pos, yaw); }
                            else why = SimHud.Instance != null ? SimHud.Instance.DebugGhost(d, pos, yaw) : "画面が無い";
                            log.AppendLine("place " + m + ": " + d.name + " (" + pos.x.ToString("0.00") + "," + pos.z.ToString("0.00") + ") 向き " + yaw + " → " + (why ?? "置ける"));
                            break;
                        }
                        var sb2 = new StringBuilder("place list: 品物 " + PlaceBook.All.Count + "・持ち物 ");
                        foreach (var kv in PlaceBook.Owned) if (kv.Value > 0) sb2.Append(kv.Key + "×" + kv.Value + " ");
                        sb2.Append("・置いた物 ");
                        foreach (var p in PlaceBook.Placed) sb2.Append(p.name + "@" + p.area + "(" + p.x.ToString("0.0") + "," + p.z.ToString("0.0") + "," + p.yaw + ") ");
                        if (PlaceBook.Errors.Count > 0) sb2.Append("・直す所 " + string.Join("／", PlaceBook.Errors));
                        log.AppendLine(sb2.ToString());
                        break;
                    }
                    case "fog":
                    {
                        // 試験用：fog on／off／hide／shadow／night／day／auto／sight／rooms／rebuild／status（視界の制限。LoungeWorld.Fog.cs。2026-11-15 本体チャット）
                        string m = a.Length > 1 ? a[1] : "status";
                        if (m == "on") LoungeWorld.FogEnabled = true;
                        else if (m == "off") LoungeWorld.FogEnabled = false;
                        else if (m == "hide") LoungeWorld.FogShadowMode = false;
                        else if (m == "shadow") LoungeWorld.FogShadowMode = true;
                        else if (m == "night") LoungeWorld.FogNightOverride = 1;
                        else if (m == "day") LoungeWorld.FogNightOverride = -1;
                        else if (m == "auto") LoungeWorld.FogNightOverride = 0;
                        else if (m == "sight") LoungeWorld.FogSightMode = true;     // 段B：見通し
                        else if (m == "rooms") LoungeWorld.FogSightMode = false;    // 段A：部屋ごと
                        else if (m == "rebuild") w.RebuildFogSight();               // 見えない当たりを作り直す
                        else if (m == "hits") { log.AppendLine("fog hits: " + w.FogHitReport(12)); break; }   // 線がよく当たっている物
                        int hid = 0, all = 0;
                        foreach (var c in w.characters) if (c != null && c.Present) { all++; if (c.FogHidden) hid++; }
                        log.AppendLine("fog: " + (LoungeWorld.FogEnabled ? "入" : "切") + "・" + (LoungeWorld.FogShadowMode ? "影" : "隠す") + "・" + w.FogStatus + "・見えない子 " + hid + "/" + all + "・出入口 " + w.FogDoorCount + "・見通しの当たり " + w.FogSightColliders + "（形どおり " + w.FogMeshColliders + "・窓 " + w.FogWindowColliders + "）・床の穴のマス " + w.FogHoleCells);
                        break;
                    }
                    case "goto":
                    {
                        // 試験用：goto 番号 名前（その子を、その名前の席（空いている物）か見どころへ行かせる。2026-11-08 家具のチャット）
                        var c = w.characters[int.Parse(a[1])];
                        if (a[2] == "@") { var ci2 = System.Globalization.CultureInfo.InvariantCulture; c.OrderMove(new Vector3(float.Parse(a[3], ci2), 0f, float.Parse(a[4], ci2))); log.AppendLine("goto: " + c.displayName + " move"); break; } // goto 番号 @ x z（指示で歩かせる）
                        string label = string.Join(" ", a, 2, a.Length - 2);
                        var t = typeof(SimCharacter);
                        var actT = t.GetNestedType("Act", System.Reflection.BindingFlags.NonPublic);
                        var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                        var s = w.seats.Find(x => x.label == label && x.occupant == null);
                        bool ok = false;
                        if (s != null) ok = (bool)t.GetMethod("StartSitAt", bf).Invoke(c, new object[] { s, System.Enum.Parse(actT, "Rest"), null });
                        else
                        {
                            var pp = w.pois.Find(x => x.label == label && x.FreeSlot() >= 0);
                            if (pp != null) ok = (bool)t.GetMethod("GoToPoi", bf).Invoke(c, new object[] { pp, System.Enum.Parse(actT, "Fun"), pp.label, 600f });
                        }
                        log.AppendLine("goto: " + c.displayName + " " + label + " " + ok);
                        break;
                    }
                    case "navpng":
                    {
                        // 試験用：navpng x0 z0 x1 z1（1階の歩ける所を Logs\navpng.png に。白＝歩ける、10m ごとに線。2026-11-08 家具のチャット）
                        var ci = System.Globalization.CultureInfo.InvariantCulture;
                        float x0 = float.Parse(a[1], ci), z0 = float.Parse(a[2], ci), x1 = float.Parse(a[3], ci), z1 = float.Parse(a[4], ci);
                        var nv = w.Nav(0);
                        int W = Mathf.CeilToInt((x1 - x0) / 0.25f), H = Mathf.CeilToInt((z1 - z0) / 0.25f);
                        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
                        for (int j = 0; j < H; j++)
                            for (int i = 0; i < W; i++)
                            {
                                float x = x0 + (i + 0.5f) * 0.25f, z = z0 + (j + 0.5f) * 0.25f;
                                bool free = nv.IsFree(new Vector3(x, 0f, z));
                                Color c = free ? new Color(0.95f, 0.95f, 0.9f) : new Color(0.15f, 0.17f, 0.2f);
                                if (Mathf.Abs(Mathf.Repeat(x, 10f)) < 0.25f || Mathf.Abs(Mathf.Repeat(z, 10f)) < 0.25f) c = free ? new Color(0.9f, 0.6f, 0.6f) : new Color(0.45f, 0.2f, 0.2f);
                                if (Mathf.Abs(x) < 0.25f || Mathf.Abs(z) < 0.25f) c = new Color(0.2f, 0.5f, 1f);
                                tex.SetPixel(i, j, c);
                            }
                        tex.Apply();
                        File.WriteAllBytes(Path.Combine(Dir, "navpng.png"), tex.EncodeToPNG());
                        Object.DestroyImmediate(tex);
                        log.AppendLine("navpng: " + W + "x" + H);
                        break;
                    }
                    case "navcheck":
                    {
                        // 試験用：navcheck x y z x2 y2 z2（2点のあいだの道があるか。2026-11-02 家具のチャット）
                        var ci = System.Globalization.CultureInfo.InvariantCulture;
                        var p1 = new Vector3(float.Parse(a[1], ci), float.Parse(a[2], ci), float.Parse(a[3], ci));
                        var p2 = new Vector3(float.Parse(a[4], ci), float.Parse(a[5], ci), float.Parse(a[6], ci));
                        var path = w.FindPath(p1, p2);
                        log.AppendLine("navcheck: " + (path == null ? "道なし" : path.Count + "点") + "　近い通れる所 " + w.NearestFree(p1) + " → " + w.NearestFree(p2));
                        break;
                    }
                    case "dtrain":
                    {
                        // 試験用：dtrain 番号 …（篤の組にその子たちを入れて、訓練の施設の事前研修へ出発）／dtrain done・dtrain reset（研修を終えた印を変える）。2026-11-01 家具のチャット
                        var dd = DungeonDirector.Instance;
                        if (a.Length > 1 && a[1] == "done") { DungeonDirector.TrainingDone = true; log.AppendLine("dtrain: done"); break; }
                        if (a.Length > 1 && a[1] == "reset") { DungeonDirector.TrainingDone = false; log.AppendLine("dtrain: reset"); break; }
                        dd.fPicks.Clear();
                        for (int i = 1; i < a.Length; i++) dd.fPicks.Add(w.characters[int.Parse(a[i])]);
                        dd.StartF(true);
                        log.AppendLine("dtrain: " + (dd.FRun != null ? dd.FRun.MemberNames() : "出発できなかった") + "（研修済み " + DungeonDirector.TrainingDone + "）");
                        break;
                    }
                    case "dgo":
                    {
                        // 試験用：dgo 番号 番号 …（篤の組にその子たちを入れて、ダンジョン実習へ出発。2026-11-01）
                        var dd = DungeonDirector.Instance;
                        dd.fPicks.Clear();
                        for (int i = 1; i < a.Length; i++) dd.fPicks.Add(w.characters[int.Parse(a[i])]);
                        dd.StartF();
                        log.AppendLine("dgo: " + (dd.FRun != null ? dd.FRun.MemberNames() : "出発できなかった"));
                        break;
                    }
                    case "dpick":
                    {
                        // 試験用：dpick 番号（篤の組の出来事で、その選択肢を選ぶ）
                        var run = DungeonDirector.Instance.FRun;
                        if (run != null && run.phase == DungeonRun.Phase.Choice) run.picked = int.Parse(a[1]);
                        log.AppendLine("dpick: " + (run != null ? run.phase + " " + run.evText : "組なし"));
                        break;
                    }
                    case "dstat":
                    {
                        // 試験用：ダンジョン実習の組のようす
                        foreach (var r in DungeonDirector.Instance.runs)
                        {
                            log.AppendLine("  " + r.title + "｜" + r.MemberNames() + "｜" + r.phase + "｜" + r.floor + "階・" + r.room + "｜体力" + Mathf.RoundToInt(r.hp) + "/" + Mathf.RoundToInt(r.maxHp) + "｜" + (r.onStage ? "見える" : "見えない") + "｜" + r.evText + "｜選択：" + string.Join("/", r.choices));
                            int from = Mathf.Max(0, r.log.Count - 6);
                            for (int i = from; i < r.log.Count; i++) log.AppendLine("    " + r.log[i]);
                        }
                        log.AppendLine("dstat: " + DungeonDirector.Instance.runs.Count + "組");
                        break;
                    }
                    case "cup":
                    {
                        // 試験用：寮杯の点
                        log.AppendLine("cup: " + HouseCup.Standings());
                        for (int i = 0; i < Mathf.Min(12, HouseCup.Recent.Count); i++) log.AppendLine("  " + HouseCup.Recent[i]);
                        break;
                    }
                    case "club":
                    {
                        // 試験用：club（部ごとの人数と、今活動している人）／club tournament（部の大会をすぐ行う）
                        if (a.Length > 1 && a[1] == "tournament") { Clubs.Tournament(w); log.AppendLine("club: tournament " + Clubs.LastTournament); break; }
                        for (int k = 0; k < Clubs.Count; k++)
                        {
                            int n = 0, act = 0;
                            var names = new List<string>();
                            foreach (var c in w.characters) if (c.club == k) { n++; if (c.InClub) { act++; if (!c.IsMob) names.Add(c.displayName); } }
                            log.AppendLine("  " + Clubs.Names[k] + "｜" + n + "人｜活動中 " + act + "｜" + string.Join("・", names.ToArray()));
                        }
                        if (a.Length > 1 && a[1] == "who") foreach (var c in w.residents) log.AppendLine("  " + c.displayName + "｜" + c.ClubDebug + "｜" + c.actionText);
                        log.AppendLine("club: 篤は" + Clubs.NameOf(w.RomanceTarget != null ? w.RomanceTarget.club : -1));
                        break;
                    }
                    case "camfollow":
                    {
                        // 試験用：camfollow 番号 距離 角度 向き（その子を追いかけて見る。足もとの確認用。2026-10-31）
                        var rig = w.mainCamera != null ? w.mainCamera.GetComponent<CameraRig>() : null;
                        var c = w.characters[int.Parse(a[1])];
                        var ci = System.Globalization.CultureInfo.InvariantCulture;
                        if (rig == null) { log.AppendLine("camfollow: ng"); break; }
                        rig.follow = c.transform;
                        rig.pivot = c.transform.position + Vector3.up * 0.4f;
                        if (a.Length > 2) rig.distance = float.Parse(a[2], ci);
                        if (a.Length > 3) rig.pitch = float.Parse(a[3], ci);
                        if (a.Length > 4) rig.yaw = float.Parse(a[4], ci);
                        w.wallMode = WallMode.Low;
                        log.AppendLine("camfollow: " + c.displayName + "（" + c.StateName() + "）");
                        break;
                    }
                    case "animdump":
                    {
                        // 試験用：見えている全員の、今の動きと何秒目か（Logs\anim_dump.txt に追記。2026-10-31）
                        var sb = new StringBuilder();
                        sb.AppendLine("== " + w.Day + "日目 " + LifeSimProto.ScheduleMaker.Clock(w.Now) + "（Time.time " + Time.time.ToString("0.00") + "）");
                        foreach (var c in w.characters)
                        {
                            if (!c.Present) continue;
                            var b = c.GetComponent<ProtoBody>();
                            sb.AppendLine(c.displayName + "｜" + c.StateName() + "｜" + (b != null ? b.DebugClipNow() : "体なし"));
                        }
                        System.IO.File.AppendAllText(System.IO.Path.Combine(Application.dataPath, "../Logs/anim_dump.txt"), sb.ToString(), new UTF8Encoding(false));
                        log.AppendLine("animdump: ok");
                        break;
                    }
                    case "finvite":
                    case "ftalk":
                    {
                        // 試験用：finvite 番号／ftalk 番号（篤がその子をエッチに誘う／話しかける。右クリックの選択肢と同じ）
                        // finvite 番号 場所の名前の一部 [どこまで0-2] [二人きり0/1] [フィニッシュ0-2] [体位]（2026-10-31 場所と内容を選ぶ誘い方）／finvite 番号 list（場所の一覧）
                        var f = w.RomanceTarget; var g = w.characters[int.Parse(a[1])];
                        if (a[0] == "finvite" && a.Length > 2)
                        {
                            var places = f.InvitePlaces(g);
                            if (a[2] == "list") { foreach (var h in places) log.AppendLine("  " + h.key + "｜" + h.name + "｜" + Mathf.RoundToInt(h.dist) + "m｜" + h.people + "人"); break; }
                            var plan = new HPlan();
                            var hit = places.Find(h => h.name.Contains(a[2]));
                            if (hit != null) { plan.placeKey = hit.key; plan.placeName = hit.name; }
                            if (a.Length > 3) plan.depth = int.Parse(a[3]);
                            if (a.Length > 4) plan.solo = a[4] == "1";
                            if (a.Length > 5) plan.finish = int.Parse(a[5]);
                            if (a.Length > 6) plan.pose = a[6];
                            f.OrderInvite(g, plan);
                            log.AppendLine("finvite: " + g.displayName + "｜" + (plan.placeName ?? "場所おまかせ") + "｜" + plan.Summary());
                            break;
                        }
                        if (a[0] == "finvite") f.OrderInvite(g); else f.OrderTalk(g);
                        log.AppendLine(a[0] + ": " + g.displayName + " / 篤の状態 " + f.StateName() + " 性欲" + Mathf.RoundToInt(g.desire));
                        break;
                    }
                    case "hstat":
                    {
                        // 試験用：エッチの場面の今の様子（全員分）を Logs\status_h2.txt に追記
                        var sb = new StringBuilder();
                        sb.AppendLine("== hstat " + w.Day + "日目 " + LifeSimProto.ScheduleMaker.Clock(w.Now));
                        foreach (var c in w.characters) { var l = c.HStatusLines(); if (l != null) sb.AppendLine(c.displayName + "｜" + string.Join("｜", l.ToArray())); }
                        File.AppendAllText(Path.Combine(Dir, "status_h2.txt"), sb.ToString(), Encoding.UTF8);
                        log.AppendLine("hstat: ok");
                        break;
                    }
                    case "fmove":
                    {
                        // 試験用：fmove x z（篤を1階のその場所へ。会話中なら切り上げる）。fmove x z y で高さ（階）も（2026-11-01 家具のチャット）
                        var ci = System.Globalization.CultureInfo.InvariantCulture;
                        var f = w.RomanceTarget;
                        f.OrderMove(new Vector3(float.Parse(a[1], ci), a.Length > 3 ? float.Parse(a[3], ci) : 0f, float.Parse(a[2], ci)));
                        log.AppendLine("fmove: " + f.StateName());
                        break;
                    }
                    case "perf":
                    {
                        // 重さを測る（PerfProbe.cs。2026-10-30 軽量化のチャット）：perf（秒）（名前）／perf sway on|off／perf offscreen on|off／perf clones（人数）／perf clear
                        string s1 = a.Length > 1 ? a[1] : "5";
                        if (s1 == "sway") { LifeSimProto.PerfProbe.NoSway = a.Length > 2 && a[2] == "off"; log.AppendLine("perf sway: " + (LifeSimProto.PerfProbe.NoSway ? "off" : "on")); }
                        else if (s1 == "offscreen") log.AppendLine(LifeSimProto.PerfProbe.SetOffscreen(!(a.Length > 2 && a[2] == "off")));
                        else if (s1 == "clones") log.AppendLine(LifeSimProto.PerfProbe.AddClones(w, a.Length > 2 ? int.Parse(a[2]) : 22));
                        else if (s1 == "swaypx") { if (a.Length > 2) LifeSimProto.ProtoBody.SwayMinPixels = float.Parse(a[2], System.Globalization.CultureInfo.InvariantCulture); log.AppendLine("perf swaypx: " + LifeSimProto.ProtoBody.SwayMinPixels); }
                        else if (s1 == "render") log.AppendLine(LifeSimProto.PerfProbe.Render(a.Length > 2 ? a[2] : "reset", a.Length > 3 ? a[3] : ""));
                        else if (s1 == "clear") log.AppendLine(LifeSimProto.PerfProbe.Clear());
                        else log.AppendLine(LifeSimProto.PerfProbe.Begin(a.Length > 2 ? a[2] : "測定", float.Parse(s1, System.Globalization.CultureInfo.InvariantCulture)));
                        break;
                    }
                    case "life":   // 生活のできごと（2026-11-02）：life [stat|reload|check|run ID 主役 相手|pick 番号|clear]
                    {
                        if (LifeSimProto.LifeEventDirector.Instance == null) { log.AppendLine("life: できごとの仕組みがない"); break; }
                        log.AppendLine(LifeSimProto.LifeEventDirector.Instance.Command(a));
                        break;
                    }
                    case "hpreview":   // 確認用の場：hpreview [形] [場面 -1..3] [相手の記号] / hpreview close（2026-10-31 モーション）
                    {
                        if (a.Length > 1 && a[1] == "close") { if (LifeSimProto.HPreview.Instance != null) UnityEngine.Object.Destroy(LifeSimProto.HPreview.Instance.gameObject); log.AppendLine("hpreview: closed"); break; }
                        var hp = LifeSimProto.HPreview.Open();
                        if (a.Length > 1) hp.SetForm(a[1]);
                        if (a.Length > 2) hp.SetPhase(int.Parse(a[2]));
                        if (a.Length > 3) hp.SetLetter(a[3]);
                        log.AppendLine("hpreview: " + hp.Status());
                        break;
                    }
                    case "hpreviewshot":   // hpreviewshot ファイル名 [yaw pitch dist]
                    {
                        var hp = LifeSimProto.HPreview.Instance;
                        if (hp == null) { log.AppendLine("hpreviewshot: not open"); break; }
                        if (a.Length > 4) hp.SetView(float.Parse(a[2], System.Globalization.CultureInfo.InvariantCulture), float.Parse(a[3], System.Globalization.CultureInfo.InvariantCulture), float.Parse(a[4], System.Globalization.CultureInfo.InvariantCulture));
                        string path = System.IO.Path.Combine(System.IO.Path.GetFullPath("Logs"), a.Length > 1 ? a[1] : "hpreview.png");
                        hp.Shot(path);
                        log.AppendLine("hpreviewshot: " + path + " / " + hp.Status());
                        break;
                    }
                    case "usagevideo":
                    {
                        new GameObject("UsageVideo").AddComponent<LifeSimProto.UsageVideo>();
                        log.AppendLine("usagevideo: started");
                        break;
                    }
                    case "motionvideo":
                    {
                        new GameObject("MotionVideo").AddComponent<LifeSimProto.MotionVideo>();
                        log.AppendLine("motionvideo: started");
                        break;
                    }
                    case "skirtlegshots":
                    {
                        // スカートと脚の当たり判定の見比べ（2026-11-01 本体）：skirtlegshots [記号,記号…] [Tag] → 仮モデル\Unity確認\スカート脚_（Tag）\
                        LifeSimProto.SkirtLegShots.Letters = a.Length > 1 ? a[1] : "R,I";
                        LifeSimProto.SkirtLegShots.Tag = a.Length > 2 ? a[2] : "now";
                        new GameObject("SkirtLegShots").AddComponent<LifeSimProto.SkirtLegShots>();
                        log.AppendLine("skirtlegshots: started " + LifeSimProto.SkirtLegShots.Letters + " " + LifeSimProto.SkirtLegShots.Tag);
                        break;
                    }
                    case "uniformsitshots":
                    {
                        // 座ったときに服が椅子の座面・床を突き抜けていないか（2026-11-13 共通制服）：uniformsitshots [記号,記号…] [Tag] → 仮モデル\\Unity確認\\制服座り_（Tag）\\
                        LifeSimProto.UniformSitShots.Letters = a.Length > 1 ? a[1] : "I,Q,R";
                        LifeSimProto.UniformSitShots.Tag = a.Length > 2 ? a[2] : "now";
                        new GameObject("UniformSitShots").AddComponent<LifeSimProto.UniformSitShots>();
                        log.AppendLine("uniformsitshots: started " + LifeSimProto.UniformSitShots.Letters + " " + LifeSimProto.UniformSitShots.Tag);
                        break;
                    }
                    case "skirtlegbench":
                    {
                        // 重さを測る見本（12人・Sec 秒で消える）：skirtlegbench [秒]
                        LifeSimProto.SkirtLegBench.Sec = a.Length > 1 ? float.Parse(a[1], System.Globalization.CultureInfo.InvariantCulture) : 60f;
                        new GameObject("SkirtLegBench").AddComponent<LifeSimProto.SkirtLegBench>();
                        log.AppendLine("skirtlegbench: started " + LifeSimProto.SkirtLegBench.Sec + "s");
                        break;
                    }
                    case "skirtleg":
                    {
                        // skirtleg on/off：全員の脚の当たり判定を切りかえ。skirtleg r 太もも根元 太もも先 すね根元 すね先：太さ（これから作る子に効く）
                        var ci = System.Globalization.CultureInfo.InvariantCulture;
                        if (a.Length > 1 && (a[1] == "on" || a[1] == "off")) LifeSimProto.ProtoBody.SkirtLegHit = a[1] == "on";
                        if (a.Length > 5 && a[1] == "r")
                        {
                            LifeSimProto.ProtoBody.ThighR0 = float.Parse(a[2], ci); LifeSimProto.ProtoBody.ThighR1 = float.Parse(a[3], ci);
                            LifeSimProto.ProtoBody.ShinR0 = float.Parse(a[4], ci); LifeSimProto.ProtoBody.ShinR1 = float.Parse(a[5], ci);
                        }
                        log.AppendLine("skirtleg: " + (LifeSimProto.ProtoBody.SkirtLegHit ? "on" : "off") + " 太さ " + LifeSimProto.ProtoBody.ThighR0 + "/" + LifeSimProto.ProtoBody.ThighR1 + "/" + LifeSimProto.ProtoBody.ShinR0 + "/" + LifeSimProto.ProtoBody.ShinR1);
                        break;
                    }
                    case "gestureshots":
                    {
                        new GameObject("GestureShots").AddComponent<LifeSimProto.GestureShots>();
                        log.AppendLine("gestureshots: started");
                        break;
                    }
                    case "motionshots":
                    {
                        // 動きの候補の見比べ（2026-10-30 モーション）：Logs\\motionshots_list.txt（カテゴリ|動きの名前）→ 仮モデル\\Unity確認\\動き候補\\
                        new GameObject("MotionShots").AddComponent<LifeSimProto.MotionShots>();
                        log.AppendLine("motionshots: started");
                        break;
                    }
                    case "floorshots":
                    {
                        // 床に座る動きの見比べ（2026-10-30 モーションのチャット）：仮モデル\\Unity確認\\床座り_比較\\
                        new GameObject("FloorShots").AddComponent<LifeSimProto.FloorShots>();
                        log.AppendLine("floorshots: started");
                        break;
                    }
                    case "shot":
                    {
                        string name = a.Length > 1 ? a[1] : "game";
                        string path = Path.Combine(Dir, "shot_" + name + ".png");
                        ScreenCapture.CaptureScreenshot(path);
                        log.AppendLine("shot: " + path);
                        break;
                    }
                }
            }
            catch (System.Exception e) { log.AppendLine(cmd + ": error " + e.Message); }
            return true;
        }
    }
}
