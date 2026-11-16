using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 確認用（2026-11-16 取り込みのチャット）：服ごとの全身の写真。撮影用の体だけを作る（住人・セーブは触らない）。
    /// 服（Uniform・Underwear・Swim）ごとに：立つ（前・横・後ろ・低い前）・歩く（前・横・低い前を3コマ）・椅子（いすなし。前・横・低い前）・床（前・横・低い前）。
    /// 合図：Logs\posedump_request.txt に「outfit （記号） （保存先：Unity確認からの相対）」（Editor\PoseDumpTrigger.cs）。
    /// </summary>
    public class OutfitShots : MonoBehaviour
    {
        public static string Letter = "H";
        public static string SubDir = "服_H";
        const int PhotoLayer = 31;
        Camera cam; RenderTexture rt; Light lamp; string dir;
        readonly StringBuilder log = new StringBuilder();

        ProtoBody Make(string name, int i, string outfit)
        {
            var go = new GameObject("OutfitShot_" + name);
            go.transform.position = new Vector3(60f + i * 4f, 0f, 260f);
            var pb = ProtoBody.Create(go, CharLook.Get(Letter, Letter == "F", Color.gray), Letter);
            if (pb == null) { Destroy(go); return null; }
            pb.testMode = true; pb.testLetter = Letter; pb.testPose = BodyPose.Stand; pb.testOutfit = outfit; pb.testFace = "";
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.gameObject.layer = PhotoLayer;
            return pb;
        }

        void S(Vector3 from, Vector3 at, string file) { SwayShots.Shot(cam, rt, lamp, from, at, Path.Combine(dir, file + ".png")); }
        IEnumerator Wait(int frames) { for (int i = 0; i < frames; i++) yield return null; }

        void Shown(ProtoBody pb, string tag)
        {
            var sb = new StringBuilder();
            foreach (var r in pb.GetComponentsInChildren<Renderer>(false))
                if (r.gameObject.activeInHierarchy && r.enabled && r.gameObject.name.StartsWith("Outfit_")) sb.Append(r.gameObject.name + " ");
            log.AppendLine(tag + "：出ている服の部品 " + sb);
        }

        IEnumerator Start()
        {
            dir = Path.Combine(ProtoPhotoBooth.OutDir, SubDir);
            Directory.CreateDirectory(dir);
            cam = new GameObject("OutfitCam").AddComponent<Camera>();
            cam.enabled = false; cam.fieldOfView = 30f; cam.nearClipPlane = 0.02f;
            cam.cullingMask = 1 << PhotoLayer; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.3f, 0.32f, 0.36f);
            rt = new RenderTexture(600, 800, 24); cam.targetTexture = rt;
            lamp = new GameObject("OutfitLamp").AddComponent<Light>(); lamp.type = LightType.Point; lamp.range = 12f; lamp.intensity = 1.0f;
            float keepCapture = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 30f;
            log.AppendLine(System.DateTime.Now + " " + Letter + "（撮影用の体だけ。住人・セーブは触らない）");
            string[] outfits = { "Uniform", "Underwear", "Swim" };
            string[] names = { "制服", "下着", "水着" };
            int idx = 0;
            for (int o = 0; o < outfits.Length; o++)
            {
                string on = names[o];
                // 立つ
                var pb = Make(on + "_立つ", idx++, outfits[o]);
                if (pb != null)
                {
                    yield return Wait(30);
                    Shown(pb, on);
                    var tr = pb.transform; Vector3 c = tr.position + Vector3.up * 0.75f;
                    S(c + tr.forward * 2.6f + Vector3.up * 0.1f, c, on + "_立つ_前");
                    S(c + tr.right * 2.6f + Vector3.up * 0.1f, c, on + "_立つ_横");
                    S(c - tr.forward * 2.6f + Vector3.up * 0.1f, c, on + "_立つ_後ろ");
                    Vector3 hip = tr.position + Vector3.up * 0.65f;
                    S(tr.position + tr.forward * 1.0f + Vector3.up * 0.2f, hip, on + "_立つ_低い前");
                    S(tr.position - tr.forward * 1.0f + Vector3.up * 0.2f, hip, on + "_立つ_低い後ろ");
                    Destroy(pb.gameObject);
                }
                // 歩く
                pb = Make(on + "_歩く", idx++, outfits[o]);
                if (pb != null)
                {
                    pb.testSpeed = 1.4f;
                    yield return Wait(30);
                    var tr = pb.transform;
                    for (int f = 0; f < 3; f++)
                    {
                        Vector3 c = tr.position + Vector3.up * 0.75f;
                        S(c + tr.forward * 2.6f + Vector3.up * 0.1f, c, on + "_歩く_前_" + f);
                        S(c + tr.right * 2.6f + Vector3.up * 0.1f, c, on + "_歩く_横_" + f);
                        Vector3 hip = tr.position + Vector3.up * 0.65f;
                        S(tr.position + tr.forward * 1.0f + Vector3.up * 0.2f, hip, on + "_歩く_低い前_" + f);
                        yield return Wait(8);
                    }
                    Destroy(pb.gameObject);
                }
                // 椅子（いすなし）・床
                foreach (var floorSit in new[] { false, true })
                {
                    string sn = floorSit ? "床" : "椅子";
                    pb = Make(on + "_" + sn, idx++, outfits[o]);
                    if (pb == null) continue;
                    pb.testPose = floorSit ? BodyPose.SitFloor : BodyPose.Sit;
                    yield return Wait(150);
                    var tr = pb.transform;
                    Transform hips = pb.Bone("Hips");
                    Vector3 hp = hips != null ? hips.position : tr.position + Vector3.up * 0.5f;
                    Vector3 c = hp + Vector3.up * 0.2f;
                    S(c + tr.forward * 2.4f + Vector3.up * 0.1f, c, on + "_" + sn + "_前");
                    S(c + tr.right * 2.4f + Vector3.up * 0.1f, c, on + "_" + sn + "_横");
                    S(hp + tr.forward * 1.1f - Vector3.up * Mathf.Max(0f, hp.y - tr.position.y - 0.1f), hp, on + "_" + sn + "_低い前");
                    log.AppendLine(on + " " + sn + "：" + pb.DebugClipNow());
                    Destroy(pb.gameObject);
                }
            }
            Time.captureDeltaTime = keepCapture;
            File.WriteAllText(Path.Combine(dir, "数値.txt"), log.ToString(), new UTF8Encoding(true));
            File.WriteAllText(Path.Combine(dir, "済み.txt"), System.DateTime.Now.ToString());
            Destroy(cam.gameObject); Destroy(lamp.gameObject); rt.Release();
            Destroy(gameObject);
        }
    }
}
