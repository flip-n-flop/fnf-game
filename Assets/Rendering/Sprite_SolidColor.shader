Shader "Custom/Sprite_SolidColor"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [PerRendererData] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Right Edge Camera Fade)]
        [Toggle] _EnableRightFade ("Enable Right Screen Fade", Float) = 1.0
        _RightFadeStart ("Right Fade Start (Screen %)", Range(0.5, 1.0)) = 0.85
        _RightFadeEnd ("Right Fade End (Screen %)", Range(0.5, 1.1)) = 0.98
        _BackgroundColor ("Background Color (Fade Target)", Color) = (0.1473, 0.138, 0.2075, 1.0)
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
            #pragma target 2.0
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex    : SV_POSITION;
                fixed4 color     : COLOR;
                float2 texcoord  : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _RendererColor;
            fixed4 _Color;

            float _EnableRightFade;
            float _RightFadeStart;
            float _RightFadeEnd;
            fixed4 _BackgroundColor;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color * _RendererColor;
                OUT.screenPos = ComputeScreenPos(OUT.vertex);

                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, IN.texcoord) * IN.color;

                if (_EnableRightFade > 0.5)
                {
                    float w = max(IN.screenPos.w, 0.0001);
                    float screenX = IN.screenPos.x / w;
                    float rightFade = smoothstep(_RightFadeStart, _RightFadeEnd, screenX);
                    col.rgb = lerp(col.rgb, _BackgroundColor.rgb, rightFade * _BackgroundColor.a);
                }

                return col;
            }
            ENDCG
        }
    }
}
