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
                // 試験用：skipto（日 時）＝ゲームの時刻を進める／speed（倍）＝速さ／gamelog＝寮の記録を game_log.txt に書く
                var w = EditorApplication.isPlaying ? LifeSimProto.LoungeWorld.Instance : null;
                if (cmd.StartsWith("skipto ") || cmd.StartsWith("speed ") || cmd == "gamelog")
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
    }
}
