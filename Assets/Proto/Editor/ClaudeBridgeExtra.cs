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
                case "hstat":
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
                        string letter = what == "detail" && a.Length > 2 ? a[2].ToUpperInvariant() : null;
                        int tab = 0;
                        if (what == "detail" && a.Length > 3) tab = int.Parse(a[3]);
                        else if (what != "detail" && a.Length > 2) tab = int.Parse(a[2]);
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
                    case "finvite":
                    case "ftalk":
                    {
                        // 試験用：finvite 番号／ftalk 番号（篤がその子をエッチに誘う／話しかける。右クリックの選択肢と同じ）
                        var f = w.RomanceTarget; var g = w.characters[int.Parse(a[1])];
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
                        // 試験用：fmove x z（篤を1階のその場所へ。会話中なら切り上げる）
                        var ci = System.Globalization.CultureInfo.InvariantCulture;
                        var f = w.RomanceTarget;
                        f.OrderMove(new Vector3(float.Parse(a[1], ci), 0f, float.Parse(a[2], ci)));
                        log.AppendLine("fmove: " + f.StateName());
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
