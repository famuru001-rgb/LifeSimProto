// 新しい学園の島の地面（依頼30 の地面_v1。2026-11-08 家具のチャット）
// 混ぜ具合の画像（_Control：r＝芝生・g＝原っぱ・b＝踏まれた土・a＝森の地面）で4つの模様を混ぜ、大きな色むら（_Macro）を重ねる。
// 1枚目のUV は島全体で 0〜1（_Area＝その広さ m）。夜の灯り（点の明かり）も受ける
Shader "LifeSim/Ground"
{
    Properties
    {
        _Control ("Control (r lawn, g meadow, b dirt, a forest)", 2D) = "red" {}
        _L0 ("Lawn", 2D) = "white" {}
        _L1 ("Meadow", 2D) = "white" {}
        _L2 ("Dirt", 2D) = "white" {}
        _L3 ("Forest", 2D) = "white" {}
        _N0 ("Lawn Normal", 2D) = "bump" {}
        _N1 ("Meadow Normal", 2D) = "bump" {}
        _N2 ("Dirt Normal", 2D) = "bump" {}
        _N3 ("Forest Normal", 2D) = "bump" {}
        _Macro ("Macro (gray, 0.5 neutral, linear)", 2D) = "grey" {}
        _Area ("Area size (m)", Vector) = (250, 167, 0, 0)
        _Tiles ("Tile size m (lawn, meadow, dirt, forest)", Vector) = (2, 3, 2, 3)
        _MacroSize ("Macro size m", Float) = 64
        _MacroStrength ("Macro strength", Range(0,1)) = 0.6
        _BumpScale ("Normal scale", Float) = 1
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _BlendSharp ("Blend sharpness", Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_Control); SAMPLER(sampler_Control);
        TEXTURE2D(_L0); SAMPLER(sampler_L0);
        TEXTURE2D(_L1); TEXTURE2D(_L2); TEXTURE2D(_L3);
        TEXTURE2D(_N0); SAMPLER(sampler_N0);
        TEXTURE2D(_N1); TEXTURE2D(_N2); TEXTURE2D(_N3);
        TEXTURE2D(_Macro); SAMPLER(sampler_Macro);
        CBUFFER_START(UnityPerMaterial)
            float4 _Control_ST;
            float4 _Area, _Tiles;
            float _MacroSize, _MacroStrength, _BumpScale, _BlendSharp;
            half4 _BaseColor;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 tangentOS:TANGENT; float2 uv:TEXCOORD0; };
            struct V { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float4 tangentWS:TEXCOORD2; float2 uv:TEXCOORD3; float fog:TEXCOORD4; };

            V vert(A i)
            {
                V o;
                float3 ws = TransformObjectToWorld(i.positionOS.xyz);
                o.positionWS = ws;
                o.positionCS = TransformWorldToHClip(ws);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.tangentWS = float4(TransformObjectToWorldDir(i.tangentOS.xyz), i.tangentOS.w * GetOddNegativeScale());
                o.uv = i.uv;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                half4 w = SAMPLE_TEXTURE2D(_Control, sampler_Control, i.uv);
                float2 m = i.uv * _Area.xy;
                float2 u0 = m / _Tiles.x, u1 = m / _Tiles.y, u2 = m / _Tiles.z, u3 = m / _Tiles.w;
                half3 c0 = SAMPLE_TEXTURE2D(_L0, sampler_L0, u0).rgb, c1 = SAMPLE_TEXTURE2D(_L1, sampler_L0, u1).rgb;
                half3 c2 = SAMPLE_TEXTURE2D(_L2, sampler_L0, u2).rgb, c3 = SAMPLE_TEXTURE2D(_L3, sampler_L0, u3).rgb;
                // 模様の明るさで混ぜ目を少しくっきり（草のすき間から土がのぞく感じ）
                half4 h = half4(dot(c0, 0.333), dot(c1, 0.333), 1.0 - dot(c2, 0.333), dot(c3, 0.333));
                w = max(0, w * (1.0 + (h - 0.5) * 2.0 * _BlendSharp));
                w /= max(1e-3, dot(w, 1));
                half3 alb = (c0 * w.r + c1 * w.g + c2 * w.b + c3 * w.a) * _BaseColor.rgb;
                half mac = SAMPLE_TEXTURE2D(_Macro, sampler_Macro, m / _MacroSize).r;
                half mac2 = SAMPLE_TEXTURE2D(_Macro, sampler_Macro, m / (_MacroSize * 0.37) + 0.31).r;
                alb *= lerp(1.0, 0.7 + 0.6 * (mac * 0.7 + mac2 * 0.3), _MacroStrength);

                half3 n0 = UnpackNormal(SAMPLE_TEXTURE2D(_N0, sampler_N0, u0)), n1 = UnpackNormal(SAMPLE_TEXTURE2D(_N1, sampler_N0, u1));
                half3 n2 = UnpackNormal(SAMPLE_TEXTURE2D(_N2, sampler_N0, u2)), n3 = UnpackNormal(SAMPLE_TEXTURE2D(_N3, sampler_N0, u3));
                half3 nt = n0 * w.r + n1 * w.g + n2 * w.b + n3 * w.a;
                nt.xy *= _BumpScale; nt = normalize(nt);
                float3 nW = normalize(i.normalWS);
                float3 t = normalize(i.tangentWS.xyz); float3 b = cross(nW, t) * i.tangentWS.w;
                float3 n = normalize(nt.x * t + nt.y * b + nt.z * nW);

                float4 sc = TransformWorldToShadowCoord(i.positionWS);
                Light L = GetMainLight(sc);
                half ndl = saturate(dot(n, L.direction));
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(i.positionCS));
                    half aoD = ao.directAmbientOcclusion, aoI = ao.indirectAmbientOcclusion;
                #else
                    half aoD = 1, aoI = 1;
                #endif
                half3 light = SampleSH(n) * aoI + L.color * ndl * L.shadowAttenuation * aoD;
                #if defined(_ADDITIONAL_LIGHTS)
                    InputData inputData = (InputData)0;
                    inputData.positionWS = i.positionWS;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                    uint count = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(count)
                        Light l = GetAdditionalLight(lightIndex, i.positionWS, half4(1,1,1,1));
                        light += l.color * l.distanceAttenuation * l.shadowAttenuation * saturate(dot(n, l.direction));
                    LIGHT_LOOP_END
                #endif
                half3 col = alb * light;
                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection; float3 _LightPosition;
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct V { float4 positionCS:SV_POSITION; };
            V vert(A i)
            {
                V o;
                float3 ws = TransformObjectToWorld(i.positionOS.xyz);
                float3 nws = TransformObjectToWorldNormal(i.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 ld = normalize(_LightPosition - ws);
                #else
                    float3 ld = _LightDirection;
                #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(ws, nws, ld));
                #if UNITY_REVERSED_Z
                    cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.positionCS = cs;
                return o;
            }
            half4 frag(V i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct A { float4 positionOS:POSITION; };
            struct V { float4 positionCS:SV_POSITION; };
            V vert(A i) { V o; o.positionCS = TransformObjectToHClip(i.positionOS.xyz); return o; }
            half4 frag(V i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct V { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; };
            V vert(A i) { V o; o.positionCS = TransformObjectToHClip(i.positionOS.xyz); o.normalWS = TransformObjectToWorldNormal(i.normalOS); return o; }
            half4 frag(V i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct = PackNormalOctQuadEncode(n);
                    return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0);
                #else
                    return half4(n, 0);
                #endif
            }
            ENDHLSL
        }
    }
}
