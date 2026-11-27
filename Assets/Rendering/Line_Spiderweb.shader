Shader "Custom/Line_Spiderweb"
{
    Properties
    {
        [PerRendererData] _MainTex ("Mask Texture (Optional)", 2D) = "white" {}

        [Header(Colors)]
        _Color ("Primary Silk Color", Color) = (0.82, 0.84, 0.86, 1.0)
        _SecondaryColor ("Branch & Strand Color", Color) = (0.72, 0.75, 0.78, 1.0)
        _HighlightColor ("Dewdrop Color", Color) = (1.0, 1.0, 1.0, 1.0)
        [Toggle] _UseVertexColor ("Multiply LineRenderer Color", Float) = 1.0
        _Alpha ("Overall Opacity", Range(0.0, 2.0)) = 1.0

        [Header(Mapping and Softness)]
        _LengthTiling ("Length Tiling (UV X)", Range(0.1, 20.0)) = 2.0
        _EdgeFade ("Width Edge Fade", Range(0.01, 0.95)) = 0.08
        _ThreadSoftness ("Edge Softness (Anti-Aliasing)", Range(0.002, 0.15)) = 0.015

        [Header(Main Irregular Silk Cord)]
        _CoreThickness ("Cord Base Thickness", Range(0.0, 0.5)) = 0.11
        _CoreOpacity ("Cord Opacity", Range(0.0, 1.5)) = 1.0
        _CordWanderFreq ("Cord Wander Frequency", Range(0.1, 15.0)) = 2.2
        _CordWanderAmp ("Cord Wander Amount", Range(0.0, 0.6)) = 0.14
        _CordLumpFreq ("Cord Lumpiness Frequency", Range(1.0, 40.0)) = 14.0
        _CordLumpAmount ("Cord Lumpiness Amount", Range(0.0, 0.2)) = 0.045

        [Header(Cord Pores and Holes)]
        _HoleDensity ("Hole Density", Range(0.0, 30.0)) = 9.0
        _HoleChance ("Hole Spawn Chance", Range(0.0, 1.0)) = 0.75
        _HoleSize ("Hole Base Size", Range(0.005, 0.15)) = 0.032
        _HoleAspect ("Hole Elongation", Range(0.3, 3.0)) = 0.85
        _HoleStrength ("Hole Cutout Strength", Range(0.0, 1.0)) = 1.0

        [Header(Extra Wavy Strands)]
        _StrandCount ("Extra Wavy Strands", Range(0, 6)) = 0
        _StrandThickness ("Wavy Strand Thickness", Range(0.005, 0.25)) = 0.025
        _StrandSpread ("Wavy Strand Spread", Range(0.0, 1.0)) = 0.35
        _WaveFrequency ("Wave Frequency", Range(0.5, 25.0)) = 6.5
        _WaveAmplitude ("Wave Amplitude", Range(0.0, 0.6)) = 0.14
        _StrandOpacity ("Wavy Strand Opacity", Range(0.0, 1.5)) = 0.6
        _StrandIrregularity ("Strand Irregularity", Range(0.0, 1.0)) = 0.35

        [Header(Dewdrops and Motion)]
        _DewDensity ("Dewdrop Density", Range(0.0, 20.0)) = 0.0
        _DewSize ("Dewdrop Size", Range(0.0, 0.2)) = 0.035
        _DewIntensity ("Dewdrop Intensity", Range(0.0, 2.0)) = 0.8
        _NoiseScale ("Organic Noise Scale", Range(0.5, 30.0)) = 8.0
        _NoiseStrength ("Organic Warp Strength", Range(0.0, 0.5)) = 0.025
        _WindSpeed ("Wind Shimmer Speed", Range(0.0, 10.0)) = 0.0
        _WindAmount ("Wind Shimmer Amount", Range(0.0, 0.2)) = 0.0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
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
            float4 _MainTex_ST;

            fixed4 _Color;
            fixed4 _SecondaryColor;
            fixed4 _HighlightColor;
            float _UseVertexColor;
            float _Alpha;

            float _LengthTiling;
            float _EdgeFade;
            float _ThreadSoftness;

            float _CoreThickness;
            float _CoreOpacity;
            float _CordWanderFreq;
            float _CordWanderAmp;
            float _CordLumpFreq;
            float _CordLumpAmount;

            float _HoleDensity;
            float _HoleChance;
            float _HoleSize;
            float _HoleAspect;
            float _HoleStrength;

            float _StrandCount;
            float _StrandThickness;
            float _StrandSpread;
            float _WaveFrequency;
            float _WaveAmplitude;
            float _StrandOpacity;
            float _StrandIrregularity;

            float _DewDensity;
            float _DewSize;
            float _DewIntensity;
            float _NoiseScale;
            float _NoiseStrength;
            float _WindSpeed;
            float _WindAmount;

            float hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            float hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float noise2D(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = TRANSFORM_TEX(IN.texcoord, _MainTex);
                OUT.color = IN.color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, IN.texcoord);

                float u = IN.texcoord.x * _LengthTiling;
                float v = (IN.texcoord.y - 0.5) * 2.0; // -1 at bottom edge, 0 at center, +1 at top edge

                // Organic warp and optional wind shimmer
                float warp = (noise2D(float2(u * _NoiseScale, v * _NoiseScale * 0.5)) - 0.5) * _NoiseStrength;
                float wind = sin(u * 2.3 + _Time.y * _WindSpeed) * _WindAmount;
                float vw = v + warp + wind;

                // 1. Main irregular silk cord (wandering centerline + lumpy thickness)
                float wander = (noise2D(float2(u * _CordWanderFreq, 1.37)) - 0.5) * _CordWanderAmp;
                float lump = (noise2D(float2(u * _CordLumpFreq, 5.71)) - 0.5) * _CordLumpAmount;
                float cordHalfW = max(0.005, _CoreThickness + lump);
                float cordDist = abs(vw - wander);
                float cordMask = (1.0 - smoothstep(cordHalfW, cordHalfW + _ThreadSoftness, cordDist)) * _CoreOpacity;

                // 2. Micro-holes / pores punched inside the cord
                if (_HoleDensity > 0.001 && _HoleStrength > 0.001)
                {
                    float hu = u * _HoleDensity;
                    float hCell = floor(hu);
                    float hLu = frac(hu) - 0.5;
                    float hSpawn = hash11(hCell + 3.7);

                    if (hSpawn < _HoleChance)
                    {
                        float hx = (hash11(hCell + 13.1) - 0.5) * 0.42;
                        float hy = (hash11(hCell + 29.7) - 0.5) * cordHalfW * 0.95;
                        float hRadius = _HoleSize * (0.55 + hash11(hCell + 41.3) * 0.85);

                        float2 hp = float2((hLu - hx) * _HoleAspect, vw - wander - hy);
                        float hDist = length(hp);
                        float hole = 1.0 - smoothstep(hRadius, hRadius + _ThreadSoftness * 0.7, hDist);
                        cordMask = saturate(cordMask - hole * _HoleStrength);
                    }
                }

                // 3. Optional extra wavy strands
                float strands = 0.0;
                int count = (int)round(_StrandCount);
                [unroll]
                for (int i = 0; i < 6; i++)
                {
                    if (i < count)
                    {
                        float fi = (float)i;
                        float phase = fi * 2.0943951;
                        float spreadOffset = (hash11(fi + 3.1) - 0.5) * _StrandSpread;
                        float irregWave = (noise2D(float2(u * 1.7 + fi * 5.3, fi * 2.1)) - 0.5) * _StrandIrregularity * 0.6;
                        float waveOffset = sin(u * _WaveFrequency + phase + irregWave * 3.0) * _WaveAmplitude;
                        float d = abs(vw - (spreadOffset + waveOffset + irregWave * 0.35));
                        float s = (1.0 - smoothstep(_StrandThickness, _StrandThickness + _ThreadSoftness, d)) * _StrandOpacity;
                        strands = max(strands, s);
                    }
                }

                // 4. Optional dewdrops
                float dew = 0.0;
                if (_DewDensity > 0.001 && _DewIntensity > 0.001)
                {
                    float du = u * _DewDensity;
                    float dCell = floor(du);
                    float dLu = frac(du) - 0.5;
                    float dRand = hash11(dCell + 53.1);

                    if (dRand > 0.35)
                    {
                        float dewY = wander + (hash11(dCell + 91.7) - 0.5) * cordHalfW;
                        float dewDist = length(float2(dLu * 0.5, vw - dewY));
                        dew = (1.0 - smoothstep(_DewSize, _DewSize + _ThreadSoftness * 0.5, dewDist)) * _DewIntensity;
                    }
                }

                // Width edge fade
                float edgeMask = 1.0 - smoothstep(1.0 - _EdgeFade, 1.0, abs(v));

                // Color & alpha composition
                float secondaryMask = saturate(strands);
                float primaryMask = saturate(cordMask);
                float highlightMask = saturate(dew);

                fixed3 col = _SecondaryColor.rgb * secondaryMask;
                col = lerp(col, _Color.rgb, primaryMask);
                col = lerp(col, _HighlightColor.rgb, highlightMask);

                float rawAlpha = saturate(max(primaryMask, secondaryMask) + highlightMask * 0.65);
                float finalAlpha = saturate(rawAlpha * edgeMask * tex.a * _Alpha);

                if (_UseVertexColor > 0.5)
                {
                    col *= IN.color.rgb;
                    finalAlpha *= IN.color.a;
                }

                return fixed4(col, finalAlpha);
            }
            ENDCG
        }
    }
}
