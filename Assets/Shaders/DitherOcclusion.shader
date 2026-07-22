// Sprite shader with a screen-space ordered-dither fade, driven by _FadeAmount.
// Used by WallOcclusionController to "punch holes" in a wall sprite when it currently
// occludes the player (Diablo 2-style transparency masking) instead of a flat alpha blend.
// Not visually verified yet — no wall art exists in the project to test against.
Shader "IdleCloud/DitherOcclusion"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FadeAmount ("Fade Amount (0 = opaque, 1 = fully dithered out)", Range(0,1)) = 0
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color  : COLOR;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex    : SV_POSITION;
                fixed4 color     : COLOR;
                float2 uv        : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4    _Color;
            float     _FadeAmount;

            // 4x4 Bayer ordered-dither matrix, normalized thresholds in [0, 1).
            static const float bayer4x4[16] =
            {
                 0.0/16,  8.0/16,  2.0/16, 10.0/16,
                12.0/16,  4.0/16, 14.0/16,  6.0/16,
                 3.0/16, 11.0/16,  1.0/16,  9.0/16,
                15.0/16,  7.0/16, 13.0/16,  5.0/16
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex    = UnityObjectToClipPos(v.vertex);
                o.color     = v.color * _Color;
                o.uv        = v.uv;
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * i.color;

                if (_FadeAmount > 0.001)
                {
                    float2 screenPixel = (i.screenPos.xy / i.screenPos.w) * _ScreenParams.xy;
                    uint2  px          = uint2(screenPixel) % 4;
                    float  threshold   = bayer4x4[px.y * 4 + px.x];
                    if (threshold < _FadeAmount)
                        discard;
                }

                return c;
            }
            ENDCG
        }
    }
}
