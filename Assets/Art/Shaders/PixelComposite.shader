// Screen composite for the low-resolution world render: posterises colour with ordered (Bayer) dithering
// in the render texture's own pixel grid, for a limited-palette pixel-art look. Used by a full-screen RawImage.
Shader "AshenRealm/PixelComposite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Levels ("Colour Levels", Float) = 18
        _Dither ("Dither Strength", Range(0, 1)) = 0.55
        _Saturation ("Saturation", Float) = 1.1
        _Contrast ("Contrast", Float) = 1.04
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One Zero

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            float _Levels;
            float _Dither;
            float _Saturation;
            float _Contrast;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float Bayer4(float2 pixel)
            {
                float2 p = fmod(floor(pixel), 4.0);
                // 4x4 Bayer matrix, row major.
                float4 row0 = float4(0, 8, 2, 10);
                float4 row1 = float4(12, 4, 14, 6);
                float4 row2 = float4(3, 11, 1, 9);
                float4 row3 = float4(15, 7, 13, 5);
                float4 row = p.y < 1 ? row0 : p.y < 2 ? row1 : p.y < 3 ? row2 : row3;
                float value = p.x < 1 ? row.x : p.x < 2 ? row.y : p.x < 3 ? row.z : row.w;
                return value / 16.0 - 0.5;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 c = tex2D(_MainTex, i.uv).rgb;
                c = pow(saturate(c), 1.0 / 2.2);                       // linear -> display space
                float luma = dot(c, float3(0.299, 0.587, 0.114));
                c = lerp(luma.xxx, c, _Saturation);
                c = (c - 0.5) * _Contrast + 0.5;
                float threshold = Bayer4(i.uv * _MainTex_TexelSize.zw) * _Dither;
                c = floor(saturate(c) * _Levels + 0.5 + threshold) / _Levels;
                c = pow(saturate(c), 2.2) * _Color.rgb;                 // back to linear for the UI pass
                return fixed4(c, 1.0);
            }
            ENDCG
        }
    }
}
