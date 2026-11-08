// 湖と堀の水（アニメ調）：深さで色が変わる・岸と岩のまわりに白い泡・日の光のきらめき・空の色の映り込み。新しい学園（2026-11-02 家具のチャット）
Shader "LifeSim/Water"
{
    Properties
    {
        _ShallowColor ("Shallow", Color) = (0.36, 0.66, 0.66, 0.55)
        _DeepColor ("Deep", Color) = (0.10, 0.27, 0.40, 0.95)
        _SkyColor ("Sky (set by CampusSky)", Color) = (0.7, 0.8, 0.9, 1)
        _DepthDist ("Depth Distance", Float) = 6
        _FoamDist ("Foam Distance", Float) = 1.2
        _FoamColor ("Foam", Color) = (0.95, 0.97, 1, 1)
        _NormalTex ("Normal", 2D) = "bump" {}
        _NoiseTex ("Noise", 2D) = "gray" {}
        _WaveScale ("Wave Scale (m)", Float) = 9
        _WaveSpeed ("Wave Speed", Float) = 0.035
        _NormalStrength ("Normal Strength", Float) = 0.55
        _SpecPower ("Spec Power", Float) = 260
        _Night ("Night (0..1)", Float) = 0
        _FoamMask ("Foam mask (plan, 1=near shore)", 2D) = "white" {}
        _FoamRect ("Foam mask rect (x0,y0,1/w,1/h)", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-10" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            TEXTURE2D(_NormalTex); SAMPLER(sampler_NormalTex);
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
            TEXTURE2D(_FoamMask); SAMPLER(sampler_FoamMask);
            float4x4 _FoamW2L;
            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor, _DeepColor, _SkyColor, _FoamColor;
                float4 _NormalTex_ST, _NoiseTex_ST;
                float _DepthDist, _FoamDist, _WaveScale, _WaveSpeed, _NormalStrength, _SpecPower, _Night;
                float4 _FoamRect;
            CBUFFER_END

            struct A { float4 positionOS:POSITION; };
            struct V { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float4 screen:TEXCOORD1; float fog:TEXCOORD2; };

            V vert(A i)
            {
                V o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.screen = ComputeScreenPos(o.positionCS);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half3 UnpackN(float2 uv) { return UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex, uv), _NormalStrength); }

            half4 frag(V i) : SV_Target
            {
                float t = _Time.y * _WaveSpeed;
                float2 xz = i.positionWS.xz;
                float3 toCam = _WorldSpaceCameraPos.xyz - i.positionWS;
                float dist = length(toCam);
                // 2026-11-08 famuru「沖の色と波」：大中小の波を向きを変えて重ね、くり返しを消す。風のむら（ゆっくり流れる大きな模様）で波立ちを強弱
                half wind = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, xz / 170.0 + float2(t * 0.8, t * 0.3)).r;
                half wind2 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, xz / 63.0 + float2(-t * 0.5, t * 0.9)).r;
                half gust = saturate(wind * 0.75 + wind2 * 0.5 - 0.15);
                half far = saturate(dist / 160.0);
                float2 w0 = xz / _WaveScale;
                half3 n1 = UnpackN(float2(dot(w0, float2(0.8, 0.6)), dot(w0, float2(-0.6, 0.8))) + float2(t, t * 0.6));
                half3 n2 = UnpackN(w0 * 2.3 + float2(-t * 0.9, t * 1.3));
                // 大きなうねりは雲のような模様の傾きから（同じ波の模様を大きくすると格子のくり返しが見えるため）
                float2 su = xz / 38.0 + float2(t * 0.35, -t * 0.22);
                half s0 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, su).r;
                half sx = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, su + float2(0.01, 0)).r;
                half sy = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, su + float2(0, 0.01)).r;
                half2 swell = half2(s0 - sx, s0 - sy) * 9.0 * _NormalStrength;
                half small = (1.0 - far * 0.85) * (0.5 + gust * 0.7);
                half2 nxy = n1.xy * (0.45 + gust * 0.45) * (1.0 - far * 0.5) + n2.xy * small * 0.45 + swell;
                half3 nt = normalize(half3(nxy, 1.0));
                float3 N = normalize(float3(nt.x, nt.z, nt.y));
                half3 ntG = normalize(half3(n1.xy * 0.8 + n2.xy * 0.6, 1.0)); // きらめき用（細かい波だけ。大きなうねりを入れると光がかたまりになったため）
                float3 NG = normalize(float3(ntG.x, ntG.z, ntG.y));

                // 深さ（水の下の物までの距離）
                float2 suv = i.screen.xy / i.screen.w;
                float surfEye = i.screen.w;
                float depth0 = max(0, LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams) - surfEye);
                // 屈折：下の物が波でゆらいで見える（水の前にある物はゆがめない）
                float2 duv = suv + nt.xy * 0.035 * saturate(depth0 * 0.6) / max(1.0, surfEye * 0.02);
                float depthD = LinearEyeDepth(SampleSceneDepth(duv), _ZBufferParams) - surfEye;
                if (depthD < 0) duv = suv; else depth0 = depthD;
                float depth = depth0;
                float dk = saturate(depth / _DepthDist);
                half4 water = lerp(_ShallowColor, _DeepColor, smoothstep(0, 1, dk));
                water.rgb *= lerp(1.06, 0.9, gust * dk); // 風で波立つ所は少し暗く
                half3 under = SampleSceneColor(duv);

                float3 Vd = toCam / max(dist, 1e-3);
                float4 sc = TransformWorldToShadowCoord(i.positionWS);
                Light L = GetMainLight(sc);
                half sh = lerp(0.55, 1.0, L.shadowAttenuation);
                half3 amb = SampleSH(float3(0,1,0));
                half3 lit = water.rgb * (amb * 0.95 + L.color * 0.6 * sh);
                // 浅い所は下が透けて見える（赤から先に吸われて青緑に）
                half3 trans = exp(-depth * half3(0.55, 0.22, 0.16) * (6.0 / _DepthDist));
                half3 col = under * trans * half3(0.85, 1.0, 1.0) + lit * (1.0 - trans.g);

                // 空の色の映り込み（ななめから見るほど強く）
                half fres = 0.02 + 0.98 * pow(1.0 - saturate(dot(N, Vd)), 5.0);
                col = lerp(col, _SkyColor.rgb, saturate(fres * 0.8));
                col = lerp(col, _SkyColor.rgb, saturate((dist - 60.0) / 520.0) * 0.45); // 遠くの水は空の色に近づける（空気遠近）

                // 日の光のきらめき（アニメ調：くっきりした点）
                float3 H = normalize(L.direction + Vd);
                half sp = pow(saturate(dot(NG, H)), _SpecPower * 2.5);
                half glint = smoothstep(0.45, 0.7, sp) * (1.0 - far * 0.6);
                col += L.color * glint * 0.8 * sh * (1.0 - _Night * 0.7);

                // 岸と岩のまわりの泡（2本の線がゆっくり動く）
                // 2026-11-08 famuru「岸の泡と波」：岸や岩に当たる所にいつもある泡の線＋岸へ寄せてくる泡の帯（ちぎれながら近づいて消える）
                half noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, xz * 0.35 + float2(t * 3, -t * 2)).r;
                half noiseB = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, xz * 0.95 + float2(-t * 2, t * 1.5)).r;
                half fdepth = depth / _FoamDist;
                half edge = 1.0 - smoothstep(0.1, 0.32, fdepth + (noiseB - 0.5) * 0.3);
                half ph = frac(fdepth * 1.5 + _Time.y * 0.2);
                half band = smoothstep(0.0, 0.04, ph) * (1.0 - smoothstep(0.12, 0.2, ph));
                band *= smoothstep(0.4, 0.62, noise + (1.0 - fdepth) * 0.35) * (1.0 - smoothstep(0.55, 1.0, fdepth));
                half foam = saturate(max(edge, band * 0.85));
                foam *= step(0.0001, depth);
                if (_FoamRect.z > 0)
                {
                    // 島の岸から遠い沖では泡を出さない（沖に白い点が浮いて見えたため）
                    float3 lp = mul(_FoamW2L, float4(i.positionWS, 1)).xyz;
                    foam *= SAMPLE_TEXTURE2D(_FoamMask, sampler_FoamMask, float2((lp.x - _FoamRect.x) * _FoamRect.z, (lp.z - _FoamRect.y) * _FoamRect.w)).r;
                }
                half3 foamCol = _FoamColor.rgb * (amb + L.color * 0.8 * sh);
                col = lerp(col, foamCol, foam * 0.65);

                half alpha = 1.0;
                col = MixFog(col, i.fog);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}
