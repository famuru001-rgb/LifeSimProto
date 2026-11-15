// 視界の制限（フォグ）：篤から見えない所を、色を抜いた灰色にする（2026-11-15 famuru「視界は上からの指示型のまま進める」。本体チャット）
// 画面いっぱいの三角形を、不透明な物を描いたあと・半透明の物の前に描く。画面の深さから世界の位置を出し、
// 明るい区画（_FogLit）・円（外）・夜の扇形に入っていなければ、色を抜いて暗くする。
// 中身は LoungeWorld.Fog.cs が毎コマ入れる。
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
            float4 _FogCircle;          // 外：x, z, 半径, 1＝使う
            float4 _FogCone;            // 夜：x, z, 半径, 1＝使う
            float4 _FogConeDir;         // 夜：向き x, z, cos(半分の角度), 篤のまわりのいつも見える半径
            float4 _FogLook;            // 残す色の割合, 暗さ, 外のふちのぼかし（m）, 0
            float4 _FogTint;

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
