// 依頼26 の崖（崖_v1）用。色は2枚目のUV（BakeUV）の焼き込み画像、細かい凹凸は1枚目のUV（10m で1回くり返し）の岩肌の法線（2026-11-07 家具のチャット）
Shader "LifeSim/Cliff"
{
    Properties
    {
        _BakeMap ("Bake Color (UV2)", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _DetailMap ("Detail Color (UV1, tiling)", 2D) = "grey" {}
        _DetailStrength ("Detail Color Strength", Range(0,1)) = 0
        _BumpMap ("Detail Normal (UV1)", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 1
        _UseBump ("Use Normal Map", Float) = 1
        _Tiling ("UV1 Tiling", Float) = 1
        _Wrap ("Light Wrap", Range(0,1)) = 0.1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_BakeMap); SAMPLER(sampler_BakeMap);
        TEXTURE2D(_DetailMap); SAMPLER(sampler_DetailMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BakeMap_ST;
            half4 _BaseColor;
            float _DetailStrength, _BumpScale, _UseBump, _Tiling, _Wrap;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 tangentOS:TANGENT; float2 uv:TEXCOORD0; float2 uv2:TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float4 tangentWS:TEXCOORD2; float4 uv:TEXCOORD3; float fog:TEXCOORD4; UNITY_VERTEX_INPUT_INSTANCE_ID };

            V vert(A i)
            {
                V o; UNITY_SETUP_INSTANCE_ID(i); UNITY_TRANSFER_INSTANCE_ID(i, o);
                float3 ws = TransformObjectToWorld(i.positionOS.xyz);
                o.positionWS = ws;
                o.positionCS = TransformWorldToHClip(ws);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.tangentWS = float4(TransformObjectToWorldDir(i.tangentOS.xyz), i.tangentOS.w * GetOddNegativeScale());
                o.uv = float4(i.uv * _Tiling, TRANSFORM_TEX(i.uv2, _BakeMap));
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(V i, bool front : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half3 alb = SAMPLE_TEXTURE2D(_BakeMap, sampler_BakeMap, i.uv.zw).rgb * _BaseColor.rgb;
                if (_DetailStrength > 0)
                {
                    // 岩肌の画像の明るさのむらだけを掛ける（平均で割る。焼き込みに模様が入っているので弱く）
                    half3 d = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, i.uv.xy).rgb;
                    half3 avg = SAMPLE_TEXTURE2D_LOD(_DetailMap, sampler_DetailMap, float2(0.5, 0.5), 12).rgb;
                    alb *= lerp(1.0, saturate(dot(d, 0.333) / max(dot(avg, 0.333), 0.05)) , _DetailStrength);
                }
                float3 n = normalize(front ? i.normalWS : -i.normalWS);
                if (_UseBump > 0.5)
                {
                    half3 nt = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv.xy), _BumpScale);
                    float3 t = normalize(i.tangentWS.xyz); float3 b = cross(n, t) * i.tangentWS.w;
                    n = normalize(nt.x * t + nt.y * b + nt.z * n);
                }
                float4 sc = TransformWorldToShadowCoord(i.positionWS);
                Light L = GetMainLight(sc);
                half ndl = saturate((dot(n, L.direction) + _Wrap) / (1.0 + _Wrap));
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(i.positionCS));
                    half aoD = ao.directAmbientOcclusion, aoI = ao.indirectAmbientOcclusion;
                #else
                    half aoD = 1, aoI = 1;
                #endif
                half3 col = alb * (SampleSH(n) * aoI + L.color * ndl * L.shadowAttenuation * aoD);
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
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection; float3 _LightPosition;
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; };
            V vert(A i)
            {
                V o; UNITY_SETUP_INSTANCE_ID(i);
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
            #pragma multi_compile_instancing
            struct A { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; };
            V vert(A i) { V o; UNITY_SETUP_INSTANCE_ID(i); o.positionCS = TransformObjectToHClip(i.positionOS.xyz); return o; }
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
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; };
            V vert(A i) { V o; UNITY_SETUP_INSTANCE_ID(i); o.positionCS = TransformObjectToHClip(i.positionOS.xyz); o.normalWS = TransformObjectToWorldNormal(i.normalOS); return o; }
            half4 frag(V i, bool front : SV_IsFrontFace) : SV_Target
            {
                float3 n = normalize(front ? i.normalWS : -i.normalWS);
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
