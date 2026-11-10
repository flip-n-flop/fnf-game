Shader "Custom/Sprite_PlayerGuide"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Expansion and Edge Blur)]
        _ExpandProgress ("Expand Progress", Range(0.0, 1.0)) = 1.0
        _MaxRadius ("Max World Radius (Half Height)", Float) = 12.0
        _HalfSpriteWorldHeight ("Half Sprite World Height", Float) = 51.9
        _EdgeSoftness ("Vertical Tip Fade Softness", Range(0.05, 0.9)) = 0.55
        _TipBlur ("Tip Blur Strength", Range(0.0, 3.0)) = 1.5
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

            float _ExpandProgress;
            float _MaxRadius;
            float _HalfSpriteWorldHeight;
            float _EdgeSoftness;
            float _TipBlur;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            fixed4 SampleBlurred(float2 uv, float blurAmount)
            {
                if (blurAmount <= 0.001)
                    return tex2D(_MainTex, uv);

                float2 offset = float2(_MainTex_TexelSize.x * 2.5, _MainTex_TexelSize.y * 12.0) * blurAmount;

                fixed4 sum = tex2D(_MainTex, uv) * 0.28;
                sum += tex2D(_MainTex, uv + float2(0.0,  offset.y)) * 0.18;
                sum += tex2D(_MainTex, uv + float2(0.0, -offset.y)) * 0.18;
                sum += tex2D(_MainTex, uv + float2( offset.x, 0.0)) * 0.10;
                sum += tex2D(_MainTex, uv + float2(-offset.x, 0.0)) * 0.10;
                sum += tex2D(_MainTex, uv + float2( offset.x,  offset.y)) * 0.04;
                sum += tex2D(_MainTex, uv + float2(-offset.x,  offset.y)) * 0.04;
                sum += tex2D(_MainTex, uv + float2( offset.x, -offset.y)) * 0.04;
                sum += tex2D(_MainTex, uv + float2(-offset.x, -offset.y)) * 0.04;
                return sum;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                if (_ExpandProgress <= 0.0001)
                    return fixed4(0, 0, 0, 0);

                // Distância vertical exata a partir do centro do Guide (uv.y = 0.5 = posição do Player)
                float normalizedDistY = abs(IN.texcoord.y - 0.5) * 2.0;
                float distFromCenterY = normalizedDistY * max(1.0, _HalfSpriteWorldHeight);

                float currentRadius = _ExpandProgress * max(0.1, _MaxRadius);
                if (distFromCenterY >= currentRadius)
                    return fixed4(0, 0, 0, 0);

                float feather = max(0.8, currentRadius * saturate(_EdgeSoftness));
                float fadeStart = max(0.0, currentRadius - feather);
                float tipFactor = saturate((distFromCenterY - fadeStart) / max(0.001, currentRadius - fadeStart));

                // Curva suave (blur/fade) nas pontas superior e inferior para não parecer truncado
                float smoothFade = 1.0 - smoothstep(0.0, 1.0, tipFactor);
                float verticalFade = smoothFade * smoothFade;

                // Blur progressivo nas bordas conforme se aproxima das extremidades
                float edgeBlur = (tipFactor * tipFactor) * _TipBlur;
                fixed4 tex = SampleBlurred(IN.texcoord, edgeBlur);

                float initialFadeIn = smoothstep(0.0, 0.08, _ExpandProgress);
                float finalAlpha = tex.a * IN.color.a * verticalFade * initialFadeIn;

                return fixed4(tex.rgb * IN.color.rgb, finalAlpha);
            }
            ENDCG
        }
    }
}
