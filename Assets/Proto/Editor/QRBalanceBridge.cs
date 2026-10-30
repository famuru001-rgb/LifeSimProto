using System;using System.IO;using UnityEditor;using UnityEngine;
namespace LifeSimProto.EditorTools {
[InitializeOnLoad]public static class QRBalanceBridge {
 static double next;static QRBalanceBridge(){EditorApplication.update+=Tick;}
 static void Tick(){if(EditorApplication.timeSinceStartup<next||EditorApplication.isCompiling||EditorApplication.isUpdating)return;next=EditorApplication.timeSinceStartup+1;
 string logs=Path.GetFullPath(Path.Combine(Application.dataPath,"../Logs"));string p=Path.Combine(logs,"qr_balance_cmd.txt");if(!File.Exists(p))return;string lk=Path.Combine(logs,"作業中.txt");if(!File.Exists(lk)||!File.ReadAllText(lk).Contains("チャッピー_ティナ・ティア"))return;
 string cmd=File.ReadAllText(p).Trim();File.Delete(p);try{if(!EditorApplication.isPlaying||!System.Text.RegularExpressions.Regex.IsMatch(cmd,"^[a-z0-9_]+$"))throw new Exception("Invalid state");QRBalanceCapture.Variant=cmd;new GameObject("QRBalanceCapture").AddComponent<QRBalanceCapture>();File.WriteAllText(Path.Combine(logs,"qr_balance_result.txt"),DateTime.Now+" "+cmd+" OK");}catch(Exception e){File.WriteAllText(Path.Combine(logs,"qr_balance_result.txt"),e.ToString());Debug.LogException(e);}}
}}
