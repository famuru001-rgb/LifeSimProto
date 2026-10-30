// LifeSim のキャラ用トゥーン表示（アニメ調の2段の陰影＋輪郭線）
// Universal Render Pipeline 用。Claude 作成（2026-10-20）
// 2026-10-28 肌の質感（影の境目の赤み・ふちの光・ほんのりツヤ）を追加。強さ0なら今までと同じ見た目
Shader "LifeSim/Toon"
{
    Properties
    {
        _BaseMap ("基本色の画像", 2D) = "white" {}
        _BaseColor ("基本色", Color) = (1,1,1,1)
        _ShadeColor ("影の色（基本色にかける）", Color) = (0.78,0.70,0.76,1)
        _ShadeStep ("影の境目", Range(-1,1)) = 0.05
        _ShadeSoftness ("影の境目のぼかし", Range(0.001,0.5)) = 0.05
        _AmbientStrength ("まわりの明るさ", Range(0,2)) = 1.0
        _LightStrength ("光の強さ", Range(0,2)) = 1.0
        _OutlineColor ("輪郭線の色", Color) = (0.22,0.16,0.16,1)
        _OutlineWidth ("輪郭線の太さ", Range(0,0.01)) = 0.0018
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("裏面", Float) = 2
        [Header(Skin)]
        _ScatterColor ("肌：影の境目の赤みの色", Color) = (1,0.42,0.32,1)
        _ScatterStrength ("肌：影の境目の赤みの強さ", Range(0,1)) = 0
        _ScatterWidth ("肌：赤みの幅", Range(0.05,1)) = 0.35
        _RimColor ("肌：ふちの光の色", Color) = (1,0.9,0.85,1)
        _RimStrength ("肌：ふちの光の強さ", Range(0,1)) = 0
        _RimPower ("肌：ふちの光の細さ", Range(1,8)) = 3
        _GlossStrength ("肌：ツヤの強さ", Range(0,0.5)) = 0
        _GlossPower ("肌：ツヤの広さ（小さいほど広い）", Range(2,64)) = 12
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _ShadeColor;
            half _ShadeStep;
            half _ShadeSoftness;
            half _AmbientStrength;
            half _LightStrength;
            half4 _OutlineColor;
            float _OutlineWidth;
            float _Cull;
            half4 _ScatterColor;
            half _ScatterStrength;
            half _ScatterWidth;
            half4 _RimColor;
            half _RimStrength;
            half _RimPower;
            half _GlossStrength;
            half _GlossPower;
        CBUFFER_END
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        ENDHLSL

        Pass
        {
            Name "ToonForward"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _FORWARD_PLUS _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half Ramp(half ndl)
            {
                return smoothstep(_ShadeStep - _ShadeSoftness, _ShadeStep + _ShadeSoftness, ndl);
            }

            half4 frag(Varyings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                half4 baseCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                float3 n = normalize(i.normalWS);
                n = IS_FRONT_VFACE(face, n, -n);

                // まわりの明るさ（陰影なしで全体を照らす）
                half3 ambient = max(SampleSH(n), half3(0,0,0)) * _AmbientStrength;
                half3 shade = baseCol.rgb * _ShadeColor.rgb;
                half3 col = shade * ambient;

                // 太陽・月の光（2段の陰影）
                float4 sc = TransformWorldToShadowCoord(i.positionWS);
                Light ml = GetMainLight(sc);
                half ndl = dot(n, ml.direction);
                half lm = Ramp(ndl) * ml.shadowAttenuation;
                col += lerp(shade, baseCol.rgb, lm) * ml.color * _LightStrength * max(lm, 0.25h);

                // 肌：影の境目にほんのり赤み（光が肌の中で散って赤く見える感じ）
                half band = saturate(1.0h - abs(ndl - _ShadeStep) / _ScatterWidth);
                band *= band * max(ml.shadowAttenuation, 0.5h);
                col += baseCol.rgb * _ScatterColor.rgb * band * _ScatterStrength * ml.color * _LightStrength;

                // 肌：ふちの光とほんのりツヤ
                float3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                half rim = pow(1.0h - saturate(dot(n, v)), _RimPower);
                col += baseCol.rgb * _RimColor.rgb * rim * _RimStrength * (ml.color * _LightStrength * max(lm, 0.35h) + ambient * 0.5h);
                float3 hv = normalize(ml.direction + v);
                half gloss = pow(saturate(dot(n, hv)), _GlossPower) * _GlossStrength * lm;
                col += ml.color * _LightStrength * gloss;

                // ランプなどの光
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalWS = n;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light l = GetAdditionalLight(lightIndex, i.positionWS, half4(1,1,1,1));
                    half la = Ramp(dot(n, l.direction)) * l.shadowAttenuation;
                    half3 lc = l.color * l.distanceAttenuation * _LightStrength;
                    col += lerp(shade, baseCol.rgb, la) * lc * max(la, 0.25h);
                LIGHT_LOOP_END

                col = MixFog(col, i.fogFactor);
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ToonOutline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float fogFactor : TEXCOORD1; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                float3 posWS = TransformObjectToWorld(i.positionOS.xyz);
                float3 nWS = normalize(TransformObjectToWorldNormal(i.normalOS));
                float dist = length(GetCameraPositionWS() - posWS);
                posWS += nWS * _OutlineWidth * clamp(dist, 0.6, 6.0);
                o.positionCS = TransformWorldToHClip(posWS);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 baseCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;
                half3 col = lerp(_OutlineColor.rgb, baseCol * 0.35h, 0.5h);
                col = MixFog(col, i.fogFactor);
                return half4(col, 1);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
    FallBack "Universal Render Pipeline/Lit"
}
