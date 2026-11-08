// Ashen Realm world shader: pixel-art detail textures from a Texture2DArray, banded "painterly" lighting
// with cool shadow tint, optional alpha clip, wind sway, emission and a stepped metal sheen.
//
// Vertex data (written by the editor MeshBuilder):
//   COLOR      rgb = albedo tint (linear), a = blend weight from layer A to layer B
//   TEXCOORD0  xy = detail UV, z = layer A, w = layer B
//   TEXCOORD1  x = direct colour (1 = use the texture's own sRGB colour), y = emission, z = wind weight, w = sheen
Shader "AshenRealm/PixelLit"
{
    Properties
    {
        _DetailArray ("Detail Texture Array", 2DArray) = "" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        _LightBands ("Light Bands", Float) = 3
        _BandSoftness ("Band Softness", Range(0.001, 0.5)) = 0.08
        _ShadowColor ("Shadow Tint", Color) = (0.7, 0.74, 0.95, 1)
        _WindStrength ("Wind Strength", Float) = 0.12
        _WindSpeed ("Wind Speed", Float) = 1.6
        _SheenColor ("Sheen Colour", Color) = (1, 0.95, 0.85, 1)
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clip", Float) = 0
        [Toggle(_WIND_ON)] _Wind ("Wind", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D_ARRAY(_DetailArray);
        SAMPLER(sampler_DetailArray);

        CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            half _Cutoff;
            half _LightBands;
            half _BandSoftness;
            half4 _ShadowColor;
            float _WindStrength;
            float _WindSpeed;
            half4 _SheenColor;
        CBUFFER_END

        float3 ApplyWind(float3 positionWS, float weight)
        {
        #if defined(_WIND_ON)
            float phase = _Time.y * _WindSpeed + positionWS.x * 0.35 + positionWS.z * 0.27;
            float sway = sin(phase) * 0.7 + sin(phase * 2.3 + 1.7) * 0.3;
            positionWS.xz += float2(sway, sway * 0.6) * (_WindStrength * weight);
        #endif
            return positionWS;
        }

        half4 SampleDetail(float4 uv, half blend)
        {
            half4 a = SAMPLE_TEXTURE2D_ARRAY(_DetailArray, sampler_DetailArray, uv.xy, uv.z);
            half4 b = SAMPLE_TEXTURE2D_ARRAY(_DetailArray, sampler_DetailArray, uv.xy, uv.w);
            return lerp(a, b, blend);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma require 2darray
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_vertex _WIND_ON

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 color : TEXCOORD2;
                float4 uv0 : TEXCOORD3;
                half4 params : TEXCOORD4;
                half fogFactor : TEXCOORD5;
            #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                half3 vertexLight : TEXCOORD6;
            #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = ApplyWind(TransformObjectToWorld(input.positionOS.xyz), input.uv1.z);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                output.uv0 = input.uv0;
                output.params = input.uv1;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
            #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                output.vertexLight = VertexLighting(positionWS, output.normalWS);
            #endif
                return output;
            }

            // Quantises light into a few flat bands with a short soft edge between them.
            half Band(half value)
            {
                half scaled = saturate(value) * _LightBands;
                half level = floor(scaled) + smoothstep(1.0h - _BandSoftness, 1.0h, frac(scaled));
                return level / _LightBands;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 detail = SampleDetail(input.uv0, input.color.a);
            #if defined(_ALPHATEST_ON)
                clip(detail.a - _Cutoff);
            #endif
                half3 textureFactor = lerp(detail.rgb * 2.0h, FastSRGBToLinear(detail.rgb), input.params.x);
                half3 albedo = input.color.rgb * textureFactor * _Tint.rgb;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = NormalizeNormalPerPixel(input.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogFactor;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.bakedGI = SampleSH(inputData.normalWS);

                half4 shadowMask = half4(1, 1, 1, 1);
                Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, shadowMask);
                half nDotL = saturate(dot(inputData.normalWS, mainLight.direction));
                half direct = Band(nDotL * mainLight.shadowAttenuation * mainLight.distanceAttenuation);
                half3 lighting = inputData.bakedGI * _ShadowColor.rgb + mainLight.color * direct;
                half3 color = albedo * lighting;

                // Stepped sheen for metal surfaces.
                half3 halfDir = SafeNormalize(mainLight.direction + inputData.viewDirectionWS);
                half sheen = step(0.55h, pow(saturate(dot(inputData.normalWS, halfDir)), 24.0h)) * input.params.w * mainLight.shadowAttenuation;
                color += _SheenColor.rgb * mainLight.color * sheen * 0.7h;

            #if defined(_ADDITIONAL_LIGHTS)
                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light light = GetAdditionalLight(lightIndex, inputData.positionWS, shadowMask);
                    half amount = saturate(dot(inputData.normalWS, light.direction)) * light.distanceAttenuation * light.shadowAttenuation;
                    // Quantise the radiance, not attenuation alone: otherwise every point light cuts off at two metres,
                    // regardless of its intensity or range. Normalising the hue also caps the close-up band at one.
                    half strength = max(light.color.r, max(light.color.g, light.color.b));
                    half3 hue = light.color / max(strength, 0.0001h);
                    color += albedo * hue * Band(amount * strength);
                LIGHT_LOOP_END
            #endif
            #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                color += albedo * input.vertexLight;
            #endif

                color += albedo * input.params.y * 2.5h;
                color = MixFog(color, inputData.fogCoord);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma require 2darray
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_vertex _WIND_ON
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv0 : TEXCOORD0;
                half blend : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float3 positionWS = ApplyWind(TransformObjectToWorld(input.positionOS.xyz), input.uv1.z);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                output.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS)));
                output.uv0 = input.uv0;
                output.blend = input.color.a;
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
            #if defined(_ALPHATEST_ON)
                clip(SampleDetail(input.uv0, input.blend).a - _Cutoff);
            #endif
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma require 2darray
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_vertex _WIND_ON
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv0 : TEXCOORD0;
                half blend : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float3 positionWS = ApplyWind(TransformObjectToWorld(input.positionOS.xyz), input.uv1.z);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv0 = input.uv0;
                output.blend = input.color.a;
                return output;
            }

            half DepthFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
            #if defined(_ALPHATEST_ON)
                clip(SampleDetail(input.uv0, input.blend).a - _Cutoff);
            #endif
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
