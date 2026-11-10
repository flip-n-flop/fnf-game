Shader "Custom/Sprite_Halftone"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Halftone Dot Thickness)]
        _Thickness ("Dot Thickness (Pixels)", Range(0.0, 12.0)) = 4.0
        _Softness ("Edge Softness", Range(0.05, 1.0)) = 0.35
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
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            float _Thickness;
            float _Softness;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            // Amostragem bilinear manual para manter as bordas das bolinhas redondas e suaves
            // mesmo quando a textura está configurada com Filter Mode = Point.
            fixed4 SampleBilinear(float2 uv)
            {
                float2 texSize = _MainTex_TexelSize.zw;
                float2 invSize = _MainTex_TexelSize.xy;
                float2 pixel = uv * texSize - 0.5;
                float2 basePixel = floor(pixel);
                float2 fracPixel = pixel - basePixel;
                float2 uv00 = (basePixel + 0.5) * invSize;

                fixed4 c00 = tex2D(_MainTex, uv00);
                fixed4 c10 = tex2D(_MainTex, uv00 + float2(invSize.x, 0.0));
                fixed4 c01 = tex2D(_MainTex, uv00 + float2(0.0, invSize.y));
                fixed4 c11 = tex2D(_MainTex, uv00 + invSize);

                return lerp(lerp(c00, c10, fracPixel.x), lerp(c01, c11, fracPixel.x), fracPixel.y);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 center = SampleBilinear(IN.texcoord);

                if (_Thickness <= 0.001)
                {
                    return fixed4(center.rgb * IN.color.rgb, center.a * IN.color.a);
                }

                // Se já estamos no interior totalmente opaco da bolinha original, retorna direto
                if (center.a >= 0.99)
                {
                    return fixed4(center.rgb * IN.color.rgb, IN.color.a);
                }

                float maxA = center.a;
                // No Halftone.png os pixels transparentes têm RGB branco (1,1,1) e as bolinhas têm RGB escuro (~0.247).
                // Guardamos o RGB da amostra com maior alpha para que a borda expandida mantenha a cor exata da bolinha.
                fixed3 dotRgb = (center.a > 0.05) ? center.rgb : fixed3(0.2470588, 0.2470588, 0.2470588);

                float2 texel = _MainTex_TexelSize.xy;

                // 16 direções distribuídas uniformemente no círculo (a cada 22.5 graus)
                static const float2 dirs[16] = {
                    float2( 1.000000,  0.000000),
                    float2( 0.923880,  0.382683),
                    float2( 0.707107,  0.707107),
                    float2( 0.382683,  0.923880),
                    float2( 0.000000,  1.000000),
                    float2(-0.382683,  0.923880),
                    float2(-0.707107,  0.707107),
                    float2(-0.923880,  0.382683),
                    float2(-1.000000,  0.000000),
                    float2(-0.923880, -0.382683),
                    float2(-0.707107, -0.707107),
                    float2(-0.382683, -0.923880),
                    float2( 0.000000, -1.000000),
                    float2( 0.382683, -0.923880),
                    float2( 0.707107, -0.707107),
                    float2( 0.923880, -0.382683)
                };

                // Anéis internos para preencher sem buracos mesmo nas bolinhas menores
                [unroll]
                for (int r = 1; r <= 3; r++)
                {
                    float radiusPx = _Thickness * (r * 0.25);
                    float2 stepUV = texel * radiusPx;

                    [unroll]
                    for (int i = 0; i < 16; i++)
                    {
                        fixed4 s = tex2D(_MainTex, IN.texcoord + dirs[i] * stepUV);
                        if (s.a > maxA)
                        {
                            maxA = s.a;
                            dotRgb = s.rgb;
                        }
                    }
                }

                // Anel externo com amostragem bilinear para deixar a borda da bolinha redonda e suave
                float2 outerStepUV = texel * _Thickness;
                [unroll]
                for (int j = 0; j < 16; j++)
                {
                    fixed4 s = SampleBilinear(IN.texcoord + dirs[j] * outerStepUV);
                    if (s.a > maxA)
                    {
                        maxA = s.a;
                        if (s.a > 0.1)
                            dotRgb = s.rgb;
                    }
                }

                float halfSoft = clamp(_Softness * 0.45, 0.02, 0.48);
                float finalAlpha = smoothstep(0.5 - halfSoft, 0.5 + halfSoft, maxA) * IN.color.a;

                return fixed4(dotRgb * IN.color.rgb, finalAlpha);
            }
            ENDCG
        }
    }
}
