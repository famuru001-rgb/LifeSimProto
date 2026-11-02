// 草・花・葉っぱ用（風でゆれる・やわらかい光）。新しい学園の屋外（2026-11-02 家具のチャット）
// _UseVertexAlpha=1：頂点の色の a を「ゆれの強さ」に使う（Unity で作る草むら）。0：模型の高さ（_WindYScale）で決める（チャッピーの模型）
Shader "LifeSim/Foliage"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _BumpMap ("Normal", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 1
        _UseBump ("Use Normal Map", Float) = 0
        _UseVertexColor ("Use Vertex Color", Float) = 0
        _UseVertexAlpha ("Use Vertex Alpha As Wind Weight", Float) = 0
        _WindStrength ("Wind Strength", Float) = 0.08
        _WindSpeed ("Wind Speed", Float) = 1.6
        _WindScale ("Wind Scale", Float) = 0.15
        _WindYScale ("Wind Y Scale", Float) = 0.5
        _Wrap ("Light Wrap", Range(0,1)) = 0.45
        _UpNormal ("Up Normal Blend", Range(0,1)) = 0.0
        _TipLight ("Tip Highlight", Range(0,1)) = 0.0
        _Translucency ("Translucency", Range(0,2)) = 0.0
        _FadeStart ("Fade Start", Float) = 10000
        _FadeEnd ("Fade End", Float) = 10001
        _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Cull [_Cull]

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float _BumpScale, _UseBump;
            half4 _BaseColor;
            float _UseVertexColor, _UseVertexAlpha, _WindStrength, _WindSpeed, _WindScale, _WindYScale, _Wrap, _UpNormal, _TipLight, _Translucency, _FadeStart, _FadeEnd, _Cull;
        CBUFFER_END
        UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_DEFINE_INSTANCED_PROP(float4, _InstColor)
        UNITY_INSTANCING_BUFFER_END(Props)

        float WindWeight(float3 posOS, float4 col)
        {
            return _UseVertexAlpha > 0.5 ? col.a : saturate(posOS.y * _WindYScale);
        }

        // 風でずらした世界の位置（遠くは根元へ縮めて消す）
        float3 WindPos(float3 posOS, float4 col, out float fade)
        {
            float3 ws = TransformObjectToWorld(posOS);
            float3 origin = float3(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m13, UNITY_MATRIX_M._m23);
            float w = WindWeight(posOS, col);
            float t = _Time.y * _WindSpeed;
            float ph = dot(origin.xz, float2(0.7, 0.4)) * _WindScale * 6.2831 + ws.x * 0.05;
            float gust = sin(t * 0.37 + origin.x * 0.031 + origin.z * 0.017) * 0.5 + 0.5;
            float2 sway = float2(sin(t + ph), sin(t * 0.83 + ph * 1.3) * 0.5) * _WindStrength * (0.55 + gust * 0.9);
            ws.xz += sway * w * w;
            ws.y -= dot(sway, sway) * w * 0.5;
            float d = distance(_WorldSpaceCameraPos.xyz, origin);
            fade = 1.0 - saturate((d - _FadeStart) / max(0.01, _FadeEnd - _FadeStart));
            ws = lerp(origin, ws, fade);
            return ws;
        }
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

            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 tangentOS:TANGENT; float4 color:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float4 color:TEXCOORD2; float2 uv:TEXCOORD3; float fog:TEXCOORD4; float4 tangentWS:TEXCOORD5; UNITY_VERTEX_INPUT_INSTANCE_ID };

            V vert(A i)
            {
                V o; UNITY_SETUP_INSTANCE_ID(i); UNITY_TRANSFER_INSTANCE_ID(i, o);
                float fade; float3 ws = WindPos(i.positionOS.xyz, i.color, fade);
                o.positionWS = ws;
                o.positionCS = TransformWorldToHClip(ws);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.tangentWS = float4(TransformObjectToWorldDir(i.tangentOS.xyz), i.tangentOS.w * GetOddNegativeScale());
                o.color = i.color;
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(V i, bool front : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                half4 ic = UNITY_ACCESS_INSTANCED_PROP(Props, _InstColor);
                if (ic.a > 0.0) tex.rgb *= ic.rgb;
                if (_UseVertexColor > 0.5) tex.rgb *= i.color.rgb;
                float3 n = normalize(front ? i.normalWS : -i.normalWS);
                if (_UseBump > 0.5)
                {
                    half3 nt = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv), _BumpScale);
                    float3 t = normalize(i.tangentWS.xyz); float3 b = cross(n, t) * i.tangentWS.w;
                    n = normalize(nt.x * t + nt.y * b + nt.z * n);
                }
                n = normalize(lerp(n, float3(0,1,0), _UpNormal));
                float4 sc = TransformWorldToShadowCoord(i.positionWS);
                Light L = GetMainLight(sc);
                half ndl = saturate((dot(n, L.direction) + _Wrap) / (1.0 + _Wrap));
                half sh = L.shadowAttenuation;
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(i.positionCS));
                    half aoD = ao.directAmbientOcclusion, aoI = ao.indirectAmbientOcclusion;
                #else
                    half aoD = 1, aoI = 1;
                #endif
                half3 amb = SampleSH(n) * aoI;
                half3 col = tex.rgb * (amb + L.color * ndl * sh * aoD);
                // 先の方を少し明るく（光が透ける感じ）
                half tip = _UseVertexAlpha > 0.5 ? i.color.a : 0;
                col += tex.rgb * L.color * sh * tip * _TipLight * 0.35;
                // 日の向こう側から見ると透けて明るい（透過）。光の向きの逆と視線が近いほど強い
                float3 Vd = normalize(_WorldSpaceCameraPos.xyz - i.positionWS);
                half tr = pow(saturate(dot(Vd, -L.direction) * 0.5 + 0.5), 4.0) * _Translucency;
                half back = saturate(-dot(n, L.direction) + 0.4);
                col += tex.rgb * half3(1.0, 1.05, 0.7) * L.color * tr * back * (0.4 + 0.6 * sh);
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
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; };
            V vert(A i)
            {
                V o; UNITY_SETUP_INSTANCE_ID(i);
                float fade; float3 ws = WindPos(i.positionOS.xyz, i.color, fade);
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
            struct A { float4 positionOS:POSITION; float4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; };
            V vert(A i) { V o; UNITY_SETUP_INSTANCE_ID(i); float fade; o.positionCS = TransformWorldToHClip(WindPos(i.positionOS.xyz, i.color, fade)); return o; }
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
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; };
            V vert(A i) { V o; UNITY_SETUP_INSTANCE_ID(i); float fade; o.positionCS = TransformWorldToHClip(WindPos(i.positionOS.xyz, i.color, fade)); o.normalWS = TransformObjectToWorldNormal(i.normalOS); return o; }
            half4 frag(V i, bool front : SV_IsFrontFace) : SV_Target
            {
                float3 n = normalize(front ? i.normalWS : -i.normalWS);
                n = normalize(lerp(n, float3(0,1,0), _UpNormal));
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
