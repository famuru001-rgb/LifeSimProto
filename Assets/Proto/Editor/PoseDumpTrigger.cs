using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LifeSimProto.EditorTools
{
    /// <summary>
    /// 確認用（2026-11-14 取り込みのチャット）：Logs\posedump_request.txt に「（記号） （保存先：Unity確認からの相対）」を置くと、
    /// 再生中に PoseDumpShots（立ち姿・歩く姿勢の書き出し）を動かす。結果は Logs\posedump_result.txt。
    /// </summary>
    [InitializeOnLoad]
    public static class PoseDumpTrigger
    {
        static double next;
        static readonly string Logs = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs"));
        static PoseDumpTrigger() { EditorApplication.update += Tick; }
        static void Tick()
        {
            if (EditorApplication.timeSinceStartup < next || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            next = EditorApplication.timeSinceStartup + 1;
            string req = Path.Combine(Logs, "posedump_request.txt");
            if (!File.Exists(req)) return;
            string[] a;
            try { a = File.ReadAllText(req).Trim().Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries); File.Delete(req); }
            catch (IOException) { return; }   // 書いている途中なら次の回に読む（エラーを出すと再生が一時停止するため。2026-11-14）
            var result = new StringBuilder(DateTime.Now.ToString("s") + "\n");
            try
            {
                if (!EditorApplication.isPlaying) throw new Exception("not playing");
                LifeSimProto.PoseDumpShots.Letter = a.Length > 0 ? a[0].ToUpperInvariant() : "H";
                LifeSimProto.PoseDumpShots.SubDir = a.Length > 1 ? a[1] : "姿勢_" + LifeSimProto.PoseDumpShots.Letter;
                new GameObject("PoseDumpShots").AddComponent<LifeSimProto.PoseDumpShots>();
                result.AppendLine("posedump: started " + LifeSimProto.PoseDumpShots.Letter + " " + LifeSimProto.PoseDumpShots.SubDir);
            }
            catch (Exception ex) { result.AppendLine("posedump: " + ex.Message); }
            File.WriteAllText(Path.Combine(Logs, "posedump_result.txt"), result.ToString(), new UTF8Encoding(false));
        }
    }
}
