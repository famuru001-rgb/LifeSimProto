// 浜辺の波打ちぎわ（寄せては返す波・泡の線・濡れた砂）。famuru 2026-11-08「岸の泡と波」（家具のチャット）
// uv.x＝岸にそった長さ（m）、uv.y＝水ぎわから陸へのきょり（m。水の中は負）。頂点の色の a＝帯のはしで消す
Shader "LifeSim/Swash"
{
    Properties
    {
        _FoamColor ("Foam", Color) = (0.97, 0.98, 1, 1)
        _FilmColor ("Water film", Color) = (0.55, 0.78, 0.8, 1)
        _WetAlpha ("Wet sand darkness", Range(0,1)) = 0.3
        _Reach ("Reach (m)", Float) = 2.2
        _Period ("Period (s)", Float) = 7
        _NoiseTex ("Noise", 2D) = "gray" {}
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-20" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Offset -1, -1
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _FoamColor, _FilmColor;
                float4 _NoiseTex_ST;
                float _WetAlpha, _Reach, _Period;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct V { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float2 uv:TEXCOORD1; float4 color:TEXCOORD2; float fog:TEXCOORD3; };
            V vert(A i)
            {
                V o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = i.uv; o.color = i.color;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                float x = i.uv.x, y = i.uv.y, t = _Time.y;
                // 寄せる波の先：早く上がって、ゆっくり引く。場所で少しずつ時間と届く長さをずらす
                float ph = t / _Period + x * 0.023 + sin(x * 0.11 + t * 0.07) * 0.1;
                float f = frac(ph);
                float up = smoothstep(0.0, 0.32, f), down = 1.0 - smoothstep(0.32, 1.0, f);
                float reach = _Reach * (0.7 + 0.3 * sin(x * 0.31 + floor(ph) * 1.9));
                float front = reach * (f < 0.32 ? up : down);
                half n = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, float2(x * 0.45, y * 0.6) + float2(t * 0.04, -t * 0.02)).r;
                half n2 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, float2(x * 1.3, y * 1.1) + float2(-t * 0.08, t * 0.05)).r;
                float fr = front + (n - 0.5) * 0.4;
                // 濡れた砂（水ぎわほど暗い。波の届く所まで）
                half wet = _WetAlpha * saturate(1.0 - y / (_Reach * 1.05)) * smoothstep(-0.3, 0.2, y);
                // 水の膜（波の先より手前）
                half film = (1.0 - smoothstep(fr - 0.08, fr + 0.02, y)) * 0.45 * smoothstep(-0.6, 0.0, y);
                // 泡の線（波の先）。引くときは薄く、ちぎれる
                half foam = smoothstep(fr - 0.45, fr - 0.08, y) * (1.0 - smoothstep(fr - 0.06, fr + 0.03, y));
                foam *= lerp(1.0, 0.35, saturate((f - 0.32) / 0.68)) * smoothstep(0.3, 0.6, n2 + 0.2);
                foam = max(foam, (1.0 - smoothstep(-0.05, 0.25, abs(y - 0.05) + (n2 - 0.5) * 0.3)) * 0.7); // いつも水ぎわにある細い泡
                float4 sc = TransformWorldToShadowCoord(i.positionWS);
                Light L = GetMainLight(sc);
                half3 light = SampleSH(float3(0, 1, 0)) + L.color * 0.75 * lerp(0.5, 1.0, L.shadowAttenuation);
                half3 col = 0; half a = wet;
                col = lerp(col, _FilmColor.rgb * light, film); a = a + film * (1.0 - a);
                col = lerp(col, _FoamColor.rgb * light, foam); a = a + foam * (1.0 - a);
                a *= i.color.a;
                col = MixFog(col, i.fog);
                return half4(col, a);
            }
            ENDHLSL
        }
    }
}
