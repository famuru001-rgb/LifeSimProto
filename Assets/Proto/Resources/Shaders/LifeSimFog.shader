// 視界の制限（フォグ）：篤から見えない所を、色を抜いた灰色にする（2026-11-15 famuru「視界は上からの指示型のまま進める」。本体チャット）
// 画面いっぱいの三角形を、不透明な物を描いたあと・半透明の物の前に描く。画面の深さから世界の位置を出し、
// 明るい区画（_FogLit）・円（外）・夜の扇形に入っていなければ、色を抜いて暗くする。
// 中身は LoungeWorld.Fog.cs が毎コマ入れる。
// 段B（見通し。2026-11-15）：_FogSightTex＝篤の目の高さから 360度に出した線が、さえぎる物に当たるまでの長さ（1本＝1マス）。それより近い所だけ明るい
Shader "Hidden/LifeSim/Fog"
{
    Properties { }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-499" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Fog"
            Tags { "LightMode"="UniversalForward" }
            ZTest Always ZWrite Off Cull Off
            Blend One SrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            #define FOG_MAX 48
            float4 _FogLit[FOG_MAX];    // 明るい四角（xMin, zMin, xMax, zMax）
            float4 _FogLitY[FOG_MAX];   // その高さ（yMin, yMax）
            float4 _FogDark[FOG_MAX];   // 暗い四角（明るい所より先に効く）
            float4 _FogDarkY[FOG_MAX];
            float _FogLitN, _FogDarkN;
            // 置いた道具で見える所（設置の仕組み 段2。2026-11-16）：丸＝x,z,半径,階／四角＝区画（階は _FogSeenRF.x）。篤の見通し・夜の扇形に関係なく見える
            float4 _FogSeenC[16];
            float4 _FogSeenR[16];
            float4 _FogSeenRF[16];
            float _FogSeenCN, _FogSeenRN;
            float4 _FogCircle;          // 外：x, z, 半径, 1＝使う
            float4 _FogCone;            // 夜：x, z, 半径, 1＝使う
            float4 _FogConeDir;         // 夜：向き x, z, cos(半分の角度), 篤のまわりのいつも見える半径
            float4 _FogLook;            // 残す色の割合, 暗さ, 外のふちのぼかし（m）, 0
            float4 _FogTint;
            float4 _FogSight;           // 見通し：篤の x, z, 遠さ, 1＝使う
            float4 _FogSightY;          // 見通し：高さの下, 上, 当たった物の表面の幅, 線の数
            TEXTURE2D(_FogSightTex); SAMPLER(sampler_FogSightTex);
            // 階と階のあいだの床の穴（吹き抜け・階段・建物の外。2026-11-15）：r＝1階と2階のあいだ、g＝2階と3階、b＝3階と屋上。1＝穴
            TEXTURE2D(_FogHoleTex); SAMPLER(sampler_FogHoleTex);
            float4 _FogHoleRect;        // x0, z0, 1/幅, 1/奥行き
            float4 _FogFloorY;          // 各階の床の高さ
            float _FogEyeFloor;

            int PixFloor(float y)
            {
                int f = 0;
                if (y >= _FogFloorY.y - 0.15) f = 1;
                if (y >= _FogFloorY.z - 0.15) f = 2;
                if (y >= _FogFloorY.w - 0.15) f = 3;
                return f;
            }

            float HoleOpen(float3 p)
            {
                int pf = PixFloor(p.y);
                int ef = (int)(_FogEyeFloor + 0.5);
                if (pf == ef) return 1.0;
                float2 huv = (p.xz - _FogHoleRect.xy) * _FogHoleRect.zw;
                if (huv.x < 0.0 || huv.y < 0.0 || huv.x > 1.0 || huv.y > 1.0) return 1.0;
                float4 hm = SAMPLE_TEXTURE2D_LOD(_FogHoleTex, sampler_FogHoleTex, huv, 0);
                int lo = min(pf, ef), hi = max(pf, ef);
                float ok = 1.0;
                if (lo < 1 && hi >= 1) ok *= step(0.5, hm.r);
                if (lo < 2 && hi >= 2) ok *= step(0.5, hm.g);
                if (lo < 3 && hi >= 3) ok *= step(0.5, hm.b);
                return ok;
            }

            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; };
            V vert(A i)
            {
                V o;
                o.positionCS = float4(i.positionOS.xy, 0.5, 1.0);
                return o;
            }

            float InRect(float3 p, float4 r, float4 y)
            {
                return (p.x >= r.x && p.x <= r.z && p.z >= r.y && p.z <= r.w && p.y >= y.x && p.y <= y.y) ? 1.0 : 0.0;
            }

            half4 frag(V i) : SV_Target
            {
                float2 uv = GetNormalizedScreenSpaceUV(i.positionCS);
                #if UNITY_REVERSED_Z
                    real depth = SampleSceneDepth(uv);
                    if (depth <= 0.000001) return half4(0, 0, 0, 1);   // 空
                #else
                    real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(uv));
                    if (depth >= 0.999999) return half4(0, 0, 0, 1);
                #endif
                float3 p = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);

                float lit = 0.0;
                int n = (int)_FogLitN;
                for (int k = 0; k < FOG_MAX; k++) { if (k >= n) break; lit = max(lit, InRect(p, _FogLit[k], _FogLitY[k])); }
                if (_FogCircle.w > 0.5)
                {
                    float d = distance(p.xz, _FogCircle.xy);
                    lit = max(lit, 1.0 - smoothstep(_FogCircle.z - _FogLook.z, _FogCircle.z, d));
                }
                if (_FogSight.w > 0.5 && HoleOpen(p) > 0.5)
                {
                    float2 v = p.xz - _FogSight.xy;
                    float d = length(v);
                    float u = frac(atan2(v.y, v.x) / (2.0 * PI) + 1.0);
                    float hit = SAMPLE_TEXTURE2D_LOD(_FogSightTex, sampler_FogSightTex, float2(u, 0.5), 0).r;
                    float s = (d <= hit + _FogSightY.z) ? 1.0 : 0.0;
                    s *= 1.0 - smoothstep(_FogSight.z - _FogLook.z, _FogSight.z, d);
                    lit = max(lit, s);
                }
                int m = (int)_FogDarkN;
                for (int j = 0; j < FOG_MAX; j++) { if (j >= m) break; if (InRect(p, _FogDark[j], _FogDarkY[j]) > 0.5) { lit = 0.0; break; } }
                if (_FogCone.w > 0.5)
                {
                    float2 v = p.xz - _FogCone.xy;
                    float d = length(v);
                    float inCone = (d <= _FogCone.z && dot(v / max(d, 0.0001), _FogConeDir.xy) >= _FogConeDir.z) ? 1.0 : 0.0;
                    if (d <= _FogConeDir.w) inCone = 1.0;
                    lit *= inCone;
                }
                if (lit < 0.999 && (_FogSeenCN > 0.5 || _FogSeenRN > 0.5))
                {
                    int pfl = PixFloor(p.y);
                    int sc = (int)_FogSeenCN, sr = (int)_FogSeenRN;
                    for (int a = 0; a < 16; a++) { if (a >= sc) break; float4 c4 = _FogSeenC[a]; if ((int)(c4.w + 0.5) == pfl && distance(p.xz, c4.xy) <= c4.z) { lit = 1.0; break; } }
                    for (int b = 0; b < 16; b++) { if (b >= sr) break; float4 r4 = _FogSeenR[b]; if ((int)(_FogSeenRF[b].x + 0.5) == pfl && p.x >= r4.x && p.x <= r4.z && p.z >= r4.y && p.z <= r4.w) { lit = 1.0; break; } }
                }
                if (lit >= 0.999) return half4(0, 0, 0, 1);   // そのまま

                // 色を抜いて暗く：結果＝（元の色 × keep）＋（明るさ × 灰色）
                half3 c = SampleSceneColor(uv);
                half l = dot(c, half3(0.299, 0.587, 0.114));
                half keep = lerp(_FogLook.x, 1.0, lit);
                half3 g = l * _FogTint.rgb * _FogLook.y * (1.0 - keep);
                return half4(g, keep);
            }
            ENDHLSL
        }
    }
}
