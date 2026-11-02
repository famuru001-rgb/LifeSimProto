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
            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor, _DeepColor, _SkyColor, _FoamColor;
                float4 _NormalTex_ST, _NoiseTex_ST;
                float _DepthDist, _FoamDist, _WaveScale, _WaveSpeed, _NormalStrength, _SpecPower, _Night;
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
                float2 wuv = i.positionWS.xz / _WaveScale;
                float t = _Time.y * _WaveSpeed;
                half3 n1 = UnpackN(wuv + float2(t, t * 0.6));
                half3 n2 = UnpackN(wuv * 1.9 + float2(-t * 0.7, t * 1.1));
                half3 nt = normalize(half3(n1.xy + n2.xy, n1.z * n2.z));
                float3 N = normalize(float3(nt.x, nt.z, nt.y));

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
                half3 under = SampleSceneColor(duv);

                float3 Vd = normalize(_WorldSpaceCameraPos.xyz - i.positionWS);
                float4 sc = TransformWorldToShadowCoord(i.positionWS);
                Light L = GetMainLight(sc);
                half sh = lerp(0.55, 1.0, L.shadowAttenuation);
                half3 amb = SampleSH(float3(0,1,0));
                half3 lit = water.rgb * (amb * 0.95 + L.color * 0.6 * sh);
                // 浅い所は下が透けて見える（赤から先に吸われて青緑に）
                half3 trans = exp(-depth * half3(0.55, 0.22, 0.16) * (6.0 / _DepthDist));
                half3 col = under * trans * half3(0.85, 1.0, 1.0) + lit * (1.0 - trans.g);

                // 空の色の映り込み（ななめから見るほど強く）
                half fres = pow(1.0 - saturate(dot(N, Vd)), 4.0);
                col = lerp(col, _SkyColor.rgb, fres * 0.75);

                // 日の光のきらめき（アニメ調：くっきりした点）
                float3 H = normalize(L.direction + Vd);
                half sp = pow(saturate(dot(N, H)), _SpecPower);
                half glint = smoothstep(0.35, 0.55, sp);
                col += L.color * glint * sh * (1.0 - _Night * 0.7);

                // 岸と岩のまわりの泡（2本の線がゆっくり動く）
                half noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, i.positionWS.xz * 0.35 + float2(t * 3, -t * 2)).r;
                half fdepth = depth / _FoamDist;
                half edge = 1.0 - saturate(fdepth);
                half band = saturate(sin((fdepth - _Time.y * 0.25) * 6.2831) * 0.5 + 0.5);
                half foam = step(noise * 0.9 + 0.05, edge) ;
                foam = max(foam, step(0.9, band) * step(noise + 0.2, edge * 1.4) * step(fdepth, 1.4));
                foam *= step(0.0001, depth) ;
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
