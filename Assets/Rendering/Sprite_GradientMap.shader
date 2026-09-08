Shader "Custom/Sprite_GradientMap"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _GradientMap ("Gradient Map Ramp", 2D) = "white" {}
        _Intensity ("Intensity", Range(0.1, 2.0)) = 1.0
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
            sampler2D _GradientMap;
            fixed4 _Color;
            float _Intensity;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, IN.texcoord) * IN.color;

                // Extrai luminância (tons de preto/cinza/branco do halftone)
                float lum = dot(col.rgb, float3(0.299, 0.587, 0.114));
                lum = saturate(lum * _Intensity);

                // Lê a cor correspondente na régua de degradê
                fixed4 grad = tex2D(_GradientMap, float2(lum, 0.5));

                // Preserva o canal alpha original da textura
                return fixed4(grad.rgb, col.a);
            }
            ENDCG
        }
    }
}