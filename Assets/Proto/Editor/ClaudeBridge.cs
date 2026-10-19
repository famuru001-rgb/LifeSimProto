using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LifeSimProto.EditorTools
{
    /// <summary>
    /// Claude が外から Unity を操作するための合図（ファイル）を見る。
    /// LifeSimProto\Logs\claude_cmd.txt に1行ずつ命令を書くと、順に実行して claude_result.txt に結果を書く。
    /// 命令：refresh（読み込み直し）／play（再生）／stop（停止）／photos（再生中なら確認用の写真）／reimport（仮モデルと動きを読み込み直す）
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
                    case "photos":
                        if (EditorApplication.isPlaying)
                        {
                            new GameObject("PhotoBooth").AddComponent<LifeSimProto.ProtoPhotoBooth>();
                            log.AppendLine("photos: started");
                        }
                        else log.AppendLine("photos: not playing");
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
