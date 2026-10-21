using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LifeSimProto.EditorTools
{
    /// <summary>
    /// Claude が外から Unity を操作するための合図（ファイル）を見る。
    /// LifeSimProto\Logs\claude_cmd.txt に1行ずつ命令を書くと、順に実行して claude_result.txt に結果を書く。
    /// 命令：refresh（読み込み直し）／play（再生）／stop（停止）／photos（再生中なら確認用の写真）／reimport（仮モデルと動きを読み込み直す）／checkchars（本番キャラの検品）／shadercheck（トゥーン表示のシェーダーのエラーを shader_check.txt に書く）／matinfo（キャラBの材質の色と画像を mat_info.txt に書く）
    /// famuru さんが Unity を操作しているときの邪魔はしない（ファイルが置かれたときだけ動く）。
    /// </summary>
    [InitializeOnLoad]
    public static class ClaudeBridge
    {
        static double next;
        static string Dir { get { return Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs")); } }
        static string CmdPath { get { return Path.Combine(Dir, "claude_cmd.txt"); } }
        static string ResultPath { get { return Path.Combine(Dir, "claude_result.txt"); } }

        static ClaudeBridge()
        {
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            if (EditorApplication.timeSinceStartup < next) return;
            next = EditorApplication.timeSinceStartup + 1.0;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            string text;
            try
            {
                if (!File.Exists(CmdPath)) return;
                text = File.ReadAllText(CmdPath, Encoding.UTF8);
                File.Delete(CmdPath);
            }
            catch { return; }

            var log = new StringBuilder();
            foreach (var raw in text.Split('\n'))
            {
                string cmd = raw.Trim().ToLowerInvariant();
                if (cmd.Length == 0) continue;
                // 試験用：skipto（日 時）＝ゲームの時刻を進める／speed（倍）＝速さ／gamelog＝寮の記録を game_log.txt に書く／openall＝新しい場所を全部開く／placeshots＝場所の写真
                var w = EditorApplication.isPlaying ? LifeSimProto.LoungeWorld.Instance : null;
                if (cmd.StartsWith("skipto ") || cmd.StartsWith("speed ") || cmd == "gamelog" || cmd == "openall" || cmd == "placeshots" || cmd == "status" || cmd == "testprivate" || cmd == "testseek" || cmd == "testnight" || cmd == "testescort" || cmd.StartsWith("testitem "))
                {
                    if (w == null) { log.AppendLine(cmd + ": not playing"); continue; }
                    var a = cmd.Split(' ');
                    if (a[0] == "skipto" && a.Length >= 3)
                    {
                        double target = (int.Parse(a[1]) - 1) * 1440.0 + double.Parse(a[2], System.Globalization.CultureInfo.InvariantCulture) * 60.0;
                        if (target > w.gameMinutes) w.gameMinutes = target;
                        log.AppendLine("skipto: " + w.Day + "日目 " + LifeSimProto.ScheduleMaker.Clock(w.Now));
                    }
                    else if (a[0] == "speed" && a.Length >= 2)
                    {
                        w.SetUserSpeed(float.Parse(a[1], System.Globalization.CultureInfo.InvariantCulture));
                        log.AppendLine("speed: " + w.userSpeed);
                    }
                    else if (cmd == "openall")
                    {
                        // 試験用：新しい場所を全部開く（ループの持ち越しにも残るので、確認が終わったら再生し直す）
                        foreach (var g in LifeSimProto.PlaceGates.All) if (!LifeSimProto.Loop.OpenedPlaces.Contains(g.place)) LifeSimProto.Loop.OpenedPlaces.Add(g.place);
                        log.AppendLine("openall: ok");
                    }
                    else if (cmd == "placeshots")
                    {
                        new GameObject("PlaceShots").AddComponent<LifeSimProto.PlaceShots>();
                        log.AppendLine("placeshots: started");
                    }
                    else if (cmd == "testprivate")
                    {
                        // 試験用：二人きりの時間を起こしやすくする（2人の気持ちを「好き」にして、性欲を上げる）
                        var f = w.RomanceTarget;
                        int n = 0;
                        foreach (var c in w.characters)
                        {
                            if (c.IsTarget || n >= 2) continue;
                            w.romance.love[c.id] = Mathf.Max(w.romance.love[c.id], 60f);
                            c.desire = 95f; n++;
                        }
                        if (f != null) f.desire = 80f;
                        log.AppendLine("testprivate: ok");
                    }
                    else if (cmd == "testseek")
                    {
                        // 試験用：昼の「相手を探して移動」を起こす（1人目は性欲100、2・3人目は性欲85で好き）
                        var f = w.RomanceTarget;
                        int n = 0;
                        foreach (var c in w.characters)
                        {
                            if (c.IsTarget || !c.Present || n >= 3) continue;
                            w.romance.love[c.id] = Mathf.Max(w.romance.love[c.id], n == 0 ? 70f : 65f);
                            c.desire = n == 0 ? 100f : 85f; n++;
                        }
                        if (f != null) f.desire = Mathf.Max(f.desire, 80f);
                        log.AppendLine("testseek: ok");
                    }
                    else if (cmd == "testnight")
                    {
                        // 試験用：夜に性欲が最大のまま眠る子を作る（1人目を性欲100、篤とほかの子は0にして、二人きりの時間を起こさない）
                        var f = w.RomanceTarget;
                        int n = 0;
                        foreach (var c in w.characters)
                        {
                            if (c.IsTarget) continue;
                            if (n == 0) { w.romance.love[c.id] = Mathf.Max(w.romance.love[c.id], 65f); c.desire = 100f; c.sleepy = Mathf.Max(c.sleepy, 90f); }
                            else c.desire = 0f;
                            n++;
                        }
                        if (f != null) f.desire = 0f;
                        log.AppendLine("testnight: ok");
                    }
                    else if (cmd == "testescort")
                    {
                        // 試験用：篤から離れた所で、近くに別の子がいる2人を選ぶ（1人目は性欲100、近くの子は性欲85で好き）
                        var f = w.RomanceTarget;
                        string res = "testescort: no pair";
                        if (f != null)
                        {
                            foreach (var g0 in w.characters)
                            {
                                if (g0.IsTarget || !g0.Present || g0.InPrivate) continue;
                                if (Vector3.Distance(g0.transform.position, f.transform.position) < 8f) continue;
                                LifeSimProto.SimCharacter b = null;
                                foreach (var c in w.characters)
                                {
                                    if (c == g0 || c.IsTarget || !c.Present || c.InPrivate) continue;
                                    if (Vector3.Distance(g0.transform.position, c.transform.position) < 2.5f) { b = c; break; }
                                }
                                if (b == null) continue;
                                w.romance.love[g0.id] = Mathf.Max(w.romance.love[g0.id], 70f); g0.desire = 100f;
                                w.romance.love[b.id] = Mathf.Max(w.romance.love[b.id], 65f); b.desire = 85f;
                                f.desire = Mathf.Max(f.desire, 40f);
                                res = "testescort: " + g0.displayName + " + " + b.displayName;
                                break;
                            }
                        }
                        log.AppendLine(res);
                    }
                    else if (a[0] == "testitem" && a.Length >= 3)
                    {
                        // 試験用：testitem 品物の名前 キャラの番号（0〜9）。篤がその子に渡した（または自分に使った）ことにする
                        var kind = (LifeSimProto.ItemKind)System.Enum.Parse(typeof(LifeSimProto.ItemKind), a[1], true);
                        var who = w.characters[int.Parse(a[2])];
                        who.ApplyItem(kind, w.RomanceTarget);
                        log.AppendLine("testitem: " + kind + " -> " + who.displayName);
                    }
                    else if (cmd == "status")
                    {
                        // 試験用：みんなが今どこで何をしているかを status.txt に追記する
                        var sb = new StringBuilder();
                        sb.AppendLine("== " + w.Day + "日目 " + LifeSimProto.ScheduleMaker.Clock(w.Now));
                        foreach (var c in w.characters) sb.AppendLine(c.displayName + "：" + w.AreaName(c.transform.position) + "　" + c.actionText + (string.IsNullOrEmpty(c.bubble) ? "" : "　「" + c.bubble + "」") + "　［性欲" + Mathf.RoundToInt(c.desire) + (c.IsTarget ? "" : "・恋" + c.LoveStage + "・たまり" + Mathf.RoundToInt(c.Heat)) + "］" + ModText(c));
                        File.AppendAllText(Path.Combine(Path.GetDirectoryName(ResultPath), "status.txt"), sb.ToString(), Encoding.UTF8);
                        log.AppendLine("status: ok");
                    }
                    else if (cmd == "gamelog")
                    {
                        File.WriteAllText(Path.Combine(Path.GetDirectoryName(ResultPath), "game_log.txt"), string.Join("\n", w.log.ToArray()), Encoding.UTF8);
                        log.AppendLine("gamelog: ok");
                    }
                    continue;
                }
                switch (cmd)
                {
                    case "refresh":
                        AssetDatabase.Refresh();
                        log.AppendLine("refresh: ok");
                        break;
                    case "reimport":
                        EditorApplication.ExecuteMenuItem("LifeSim/仮モデルと動きを読み込み直す");
                        log.AppendLine("reimport: ok");
                        break;
                    case "play":
                        if (!EditorApplication.isPlaying) EditorApplication.EnterPlaymode();
                        log.AppendLine("play: ok");
                        break;
                    case "stop":
                        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
                        log.AppendLine("stop: ok");
                        break;
                    case "checkchars":
                        CharCheck.Run();
                        log.AppendLine("checkchars: ok");
                        break;
                    case "photos":
                        if (EditorApplication.isPlaying)
                        {
                            new GameObject("PhotoBooth").AddComponent<LifeSimProto.ProtoPhotoBooth>();
                            log.AppendLine("photos: started");
                        }
                        else log.AppendLine("photos: not playing");
                        break;
                    case "shadercheck":
                        {
                            var sb = new StringBuilder();
                            var sh = Shader.Find("LifeSim/Toon");
                            if (sh == null) sb.AppendLine("LifeSim/Toon が見つからない");
                            else
                            {
                                sb.AppendLine("エラーあり=" + ShaderUtil.ShaderHasError(sh) + " 対応=" + sh.isSupported + " パス数=" + sh.passCount);
                                foreach (var m in ShaderUtil.GetShaderMessages(sh))
                                    sb.AppendLine("[" + m.severity + "] " + m.platform + " 行" + m.line + "：" + m.message + " / " + m.messageDetails);
                            }
                            File.WriteAllText(Path.Combine(Dir, "shader_check.txt"), sb.ToString(), Encoding.UTF8);
                            log.AppendLine("shadercheck: ok");
                        }
                        break;
                    case "matinfo":
                        {
                            var sb = new StringBuilder();
                            foreach (var o in AssetDatabase.LoadAllAssetsAtPath("Assets/Proto/Resources/Chars/B/CharB.fbx"))
                            {
                                var m = o as Material;
                                if (m == null) continue;
                                sb.Append(m.name + " / " + m.shader.name);
                                foreach (var p in new[] { "_BaseColor", "_Color" }) if (m.HasProperty(p)) sb.Append(" " + p + "=" + m.GetColor(p));
                                foreach (var p in new[] { "_BaseMap", "_MainTex" }) if (m.HasProperty(p)) sb.Append(" " + p + "=" + (m.GetTexture(p) != null ? m.GetTexture(p).name : "なし"));
                                sb.AppendLine();
                            }
                            foreach (var o in AssetDatabase.LoadAllAssetsAtPath("Assets/Proto/Resources/Chars/B/CharB.fbx"))
                            {
                                var r = o as SkinnedMeshRenderer;
                                if (r == null) continue;
                                sb.Append("部品 " + r.name + "：");
                                foreach (var m in r.sharedMaterials) sb.Append((m != null ? m.name : "なし") + "，");
                                sb.AppendLine();
                            }
                            File.WriteAllText(Path.Combine(Dir, "mat_info.txt"), sb.ToString(), Encoding.UTF8);
                            log.AppendLine("matinfo: ok");
                        }
                        break;
                    default:
                        log.AppendLine(cmd + ": unknown");
                        break;
                }
            }
            try
            {
                File.WriteAllText(ResultPath, System.DateTime.Now + "\n" + log + "playing=" + EditorApplication.isPlaying + "\n", Encoding.UTF8);
            }
            catch { }
        }
    
        static string ModText(LifeSimProto.SimCharacter c)
        {
            var l = new System.Collections.Generic.List<string>();
            foreach (var m in c.ItemMods) l.Add(m.mod.name);
            return l.Count > 0 ? "［効：" + string.Join("・", l.ToArray()) + "］" : "";
        }
    }
}
