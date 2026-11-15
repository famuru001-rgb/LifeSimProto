using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 視界の制限（フォグ。LoungeWorld.Fog.cs）：篤から見えない所にいる子を隠す、または黒い影だけにする（2026-11-15 本体チャット）。
    /// 隠す：今の「見ている階より上の子を隠す」と同じく、体を出さず・クリックもできない。
    /// 影：体は出したまま材質を黒にする（クリックはできない）。もとの材質は覚えておいて、見えたら戻す。
    /// </summary>
    public partial class SimCharacter
    {
        bool fogHidden, fogShadow;
        Material[][] fogSavedMats;
        Material[][] fogShadowArrays;
        static Material fogShadowMat;

        public bool FogHidden { get { return fogHidden; } }

        public void SetFogHidden(bool hidden, bool asShadow)
        {
            bool shadow = hidden && asShadow;
            bool hide = hidden && !asShadow;
            bool changed = false;
            if (shadow != fogShadow) { fogShadow = shadow; ApplyFogShadow(shadow); changed = true; }
            else if (shadow && Time.frameCount % 15 == id % 15) KeepFogShadow();
            if (hide != fogHideBody) { fogHideBody = hide; changed = true; }
            fogHidden = hidden;
            if (changed) ApplyVisible();
        }

        bool fogHideBody;   // ApplyVisible が見る（隠すときだけ true。影のときはクリックだけ止める＝fogShadow）

        static Material FogShadowMat
        {
            get
            {
                if (fogShadowMat == null)
                {
                    var sh = Shader.Find("Universal Render Pipeline/Unlit");
                    fogShadowMat = new Material(sh != null ? sh : Shader.Find("Unlit/Color")) { name = "FogShadow" };
                    var c = new Color(0.07f, 0.07f, 0.09f, 1f);
                    if (fogShadowMat.HasProperty("_BaseColor")) fogShadowMat.SetColor("_BaseColor", c);
                    if (fogShadowMat.HasProperty("_Color")) fogShadowMat.SetColor("_Color", c);
                }
                return fogShadowMat;
            }
        }

        void ApplyFogShadow(bool on)
        {
            if (renderers == null) return;
            if (on)
            {
                fogSavedMats = new Material[renderers.Length][];
                fogShadowArrays = new Material[renderers.Length][];
                for (int i = 0; i < renderers.Length; i++)
                {
                    var r = renderers[i];
                    if (r == null || r.gameObject == ring) continue;
                    fogSavedMats[i] = r.sharedMaterials;
                    var arr = new Material[fogSavedMats[i].Length];
                    for (int k = 0; k < arr.Length; k++) arr[k] = FogShadowMat;
                    fogShadowArrays[i] = arr;
                    r.sharedMaterials = arr;
                }
            }
            else if (fogSavedMats != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    var r = renderers[i];
                    if (r == null || fogSavedMats[i] == null) continue;
                    // 影のあいだにほかの所（お風呂の材質など）が材質を変えていたら、それを残す
                    if (IsFogShadowArray(r.sharedMaterials)) r.sharedMaterials = fogSavedMats[i];
                }
                fogSavedMats = null; fogShadowArrays = null;
            }
        }

        /// <summary>影のあいだに、ほかの所が材質を変えたら、それを覚え直して黒に戻す。</summary>
        void KeepFogShadow()
        {
            if (renderers == null || fogSavedMats == null) return;
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || fogShadowArrays[i] == null) continue;
                var cur = r.sharedMaterials;
                if (IsFogShadowArray(cur)) continue;
                fogSavedMats[i] = cur;
                var arr = new Material[cur.Length];
                for (int k = 0; k < arr.Length; k++) arr[k] = FogShadowMat;
                fogShadowArrays[i] = arr;
                r.sharedMaterials = arr;
            }
        }

        static bool IsFogShadowArray(Material[] ms)
        {
            if (ms == null || ms.Length == 0) return false;
            foreach (var m in ms) if (m != fogShadowMat) return false;
            return true;
        }
    }
}
