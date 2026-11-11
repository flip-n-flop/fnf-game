Shader "Custom/Sprite_Holographic"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [PerRendererData] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Holographic Colors)]
        _HoloColor ("Holo Base Color (Pink)", Color) = (1.0, 0.36, 0.76, 1.0)
        _HoloColorMix ("Holo Color Intensity", Range(0.0, 1.0)) = 0.469
        _Iridescence ("Iridescent Shift", Range(0.0, 1.0)) = 1.0
        _ShadowDepth ("Foil Valley Contrast (Shadows)", Range(0.0, 1.0)) = 0.35
        _Highlight ("Crease Specular Boost", Range(0.0, 2.0)) = 0.65

        [Header(Foil Faces vs Edges)]
        _VertexCount ("Vertex Count (Quantidade de Vertices)", Range(1.0, 24.0)) = 3.0
        _FaceStrength ("Face Contrast (Forca das Faces)", Range(0.0, 1.0)) = 0.9
        _FaceBend ("Face Metallic Gradient (Degrade na Face)", Range(0.0, 1.0)) = 0.45
        _EdgeStrength ("Edge Line Strength (Forca das Arestas)", Range(0.0, 1.0)) = 0.12
        _Sharpening ("Sharpening (Triangular to Square)", Range(0.0, 1.0)) = 0.2
        _Blur ("Blur / Face Softness", Range(0.0, 3.0)) = 0.08

        [Header(Motion and Scale)]
        _Speed ("Scroll Speed", Range(-5.0, 5.0)) = 0.15
        _Angle ("Flow Angle (Degrees)", Range(-180.0, 180.0)) = -35.0
        _Scale ("Wave Scale", Range(0.5, 10.0)) = 0.5
        _WarpStrength ("Fold Distortion / Crinkle", Range(0.0, 2.0)) = 0.45
        [Toggle] _UseWorldSpace ("Use World Coordinates (For LineRenderer)", Float) = 0.0
        _WorldScale ("World Coordinate Scale (Lines)", Range(0.05, 3.0)) = 0.35

        [Header(Opacity and Gaps)]
        _Intensity ("Overall Opacity / Intensity", Range(0.0, 2.5)) = 2.5
        _BaseTintStrength ("Minimum Base Fill", Range(0.0, 1.0)) = 1.0
        _BreakupCutoff ("Gap Threshold (Falhas)", Range(0.0, 0.8)) = 0.0

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
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex    : SV_POSITION;
                fixed4 color     : COLOR;
                float2 texcoord  : TEXCOORD0;
                float2 worldPos  : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
            };

            sampler2D _MainTex;
            fixed4 _RendererColor;
            fixed4 _Color;

            fixed4 _HoloColor;
            float _HoloColorMix;
            float _Iridescence;
            float _ShadowDepth;
            float _Highlight;

            float _VertexCount;
            float _FaceStrength;
            float _FaceBend;
            float _EdgeStrength;
            float _Sharpening;
            float _Blur;

            float _Speed;
            float _Angle;
            float _Scale;
            float _WarpStrength;
            float _UseWorldSpace;
            float _WorldScale;

            float _Intensity;
            float _BaseTintStrength;
            float _BreakupCutoff;

            float _EnableRightFade;
            float _RightFadeStart;
            float _RightFadeEnd;
            fixed4 _BackgroundColor;

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // Altura 3D animada de cada vértice da malha de foil
            float VertexHeight(float2 vertexId, float t)
            {
                float hBase = Hash21(vertexId);
                float hPhase = Hash21(vertexId + float2(19.7, 43.1));
                return sin(hBase * 6.2831853 + t * (1.2 + hPhase * 0.8)) * 0.5 + 0.5;
            }

            // Malha real de FACES poligonais (Triângulos Simplex <-> Quadrados) com normais por face
            void EvaluateFoilFaces(
                float2 uv,
                float vertexDensity,
                float sharpenSquare,
                float blur,
                float faceBend,
                float t,
                out float faceLight,
                out float faceHueId,
                out float creaseEdge)
            {
                float2 p = uv * max(1.0, vertexDensity);

                float skew = lerp(0.3660254, 0.0, sharpenSquare);
                float2 skewedP = p + (p.x + p.y) * skew;
                float2 cellId = floor(skewedP);
                float2 f = frac(skewedP);

                float softWidth = max(0.005, blur * 0.45);
                float diagBlend = smoothstep(-softWidth, softWidth, f.x - f.y);
                float useTriangles = 1.0 - sharpenSquare * 0.85;
                float cellSmooth = smoothstep(0.15, 1.35, blur);
                float deepBlur = saturate((blur - 0.8) / 2.2);

                float2 v00 = cellId;
                float2 v10 = cellId + float2(1.0, 0.0);
                float2 v01 = cellId + float2(0.0, 1.0);
                float2 v11 = cellId + float2(1.0, 1.0);

                float h00 = VertexHeight(v00, t);
                float h10 = VertexHeight(v10, t);
                float h01 = VertexHeight(v01, t);
                float h11 = VertexHeight(v11, t);

                float2 gradLower = float2(h10 - h00, h11 - h10);
                float2 gradUpper = float2(h11 - h01, h01 - h00);
                float2 gradQuad = float2((h10 - h00 + h11 - h01) * 0.5, (h01 - h00 + h11 - h10) * 0.5);

                float2 triGrad = lerp(gradUpper, gradLower, diagBlend);
                float2 facetGrad = lerp(gradQuad, triGrad, useTriangles);

                float3 baryLower = float3(1.0 - f.x, f.x - f.y, f.y);
                float3 baryUpper = float3(1.0 - f.y, f.y - f.x, f.x);
                float3 bary = lerp(baryUpper, baryLower, diagBlend);
                float facetHeight = lerp(
                    lerp(h00, h10, f.x) * (1.0 - f.y) + lerp(h01, h11, f.x) * f.y,
                    bary.x * h00 + bary.y * lerp(h01, h10, diagBlend) + bary.z * h11,
                    useTriangles
                );

                float hueLower = Hash21(cellId + float2(13.1, 7.7));
                float hueUpper = Hash21(cellId + float2(29.3, 51.9));
                float facetHue = lerp(hueLower, lerp(hueUpper, hueLower, diagBlend), useTriangles);

                // Suavização contínua (B-Spline quadrática 3x3) para eliminar costuras entre células em blurs altos
                float2 vm1m1 = cellId + float2(-1.0, -1.0);
                float2 v0m1  = cellId + float2( 0.0, -1.0);
                float2 v1m1  = cellId + float2( 1.0, -1.0);
                float2 vm10  = cellId + float2(-1.0,  0.0);
                float2 vm11  = cellId + float2(-1.0,  1.0);

                float hm1m1 = VertexHeight(vm1m1, t);
                float h0m1  = VertexHeight(v0m1, t);
                float h1m1  = VertexHeight(v1m1, t);
                float hm10  = VertexHeight(vm10, t);
                float hm11  = VertexHeight(vm11, t);

                float wx0 = 0.5 * (1.0 - f.x) * (1.0 - f.x);
                float wx1 = 0.5 + f.x * (1.0 - f.x);
                float wx2 = 0.5 * f.x * f.x;
                float wy0 = 0.5 * (1.0 - f.y) * (1.0 - f.y);
                float wy1 = 0.5 + f.y * (1.0 - f.y);
                float wy2 = 0.5 * f.y * f.y;

                float dwx0 = -(1.0 - f.x);
                float dwx1 = 1.0 - 2.0 * f.x;
                float dwx2 = f.x;
                float dwy0 = -(1.0 - f.y);
                float dwy1 = 1.0 - 2.0 * f.y;
                float dwy2 = f.y;

                float row_m1 = wx0 * hm1m1 + wx1 * h0m1 + wx2 * h1m1;
                float row_0  = wx0 * hm10  + wx1 * h00  + wx2 * h10;
                float row_1  = wx0 * hm11  + wx1 * h01  + wx2 * h11;

                float drow_m1 = dwx0 * hm1m1 + dwx1 * h0m1 + dwx2 * h1m1;
                float drow_0  = dwx0 * hm10  + dwx1 * h00  + dwx2 * h10;
                float drow_1  = dwx0 * hm11  + dwx1 * h01  + dwx2 * h11;

                float splineHeight = wy0 * row_m1 + wy1 * row_0 + wy2 * row_1;
                float2 splineGrad = float2(
                    wy0 * drow_m1 + wy1 * drow_0 + wy2 * drow_1,
                    dwy0 * row_m1 + dwy1 * row_0 + dwy2 * row_1
                );

                float2 hueOffset = float2(13.1, 7.7);
                float hRow_m1 = wx0 * Hash21(vm1m1 + hueOffset) + wx1 * Hash21(v0m1 + hueOffset) + wx2 * Hash21(v1m1 + hueOffset);
                float hRow_0  = wx0 * Hash21(vm10  + hueOffset) + wx1 * hueLower               + wx2 * Hash21(v10  + hueOffset);
                float hRow_1  = wx0 * Hash21(vm11  + hueOffset) + wx1 * Hash21(v01  + hueOffset) + wx2 * Hash21(v11  + hueOffset);
                float splineHue = wy0 * hRow_m1 + wy1 * hRow_0 + wy2 * hRow_1;

                float2 finalGrad = lerp(facetGrad, splineGrad, cellSmooth);
                float interpolatedHeight = lerp(facetHeight, splineHeight, cellSmooth);
                faceHueId = lerp(facetHue, splineHue, cellSmooth);

                float normalSlope = lerp(2.4, 0.85, deepBlur);
                float3 faceNormal = normalize(float3(finalGrad * normalSlope, 1.0));
                float3 lightDir = normalize(float3(0.55, 0.45, 0.7));
                float flatShade = saturate(dot(faceNormal, lightDir) * 0.85 + 0.15);

                float cellTilt = dot(f - 0.5, finalGrad) * (1.0 - cellSmooth);
                float sweepFreq = lerp(1.8, 1.0, deepBlur);
                float internalSweep = sin((interpolatedHeight * sweepFreq + cellTilt) * 3.14159265) * 0.5 + 0.5;
                faceLight = lerp(flatShade, saturate(flatShade * 0.65 + internalSweep * 0.55), faceBend);

                float borderQuad = min(min(f.x, 1.0 - f.x), min(f.y, 1.0 - f.y));
                float borderDiag = abs(f.x - f.y) * 0.7071;
                float edgeDist = lerp(borderQuad, min(borderQuad, borderDiag), useTriangles);

                float edgeThickness = lerp(0.025, 0.42, saturate(blur / 2.5));
                float edgeFade = 1.0 - smoothstep(0.6, 2.2, blur);
                creaseEdge = (1.0 - smoothstep(0.0, edgeThickness, edgeDist)) * edgeFade;
            }

            float3 ShiftHue(float3 col, float shiftRadians)
            {
                float3 k = float3(0.57735, 0.57735, 0.57735);
                float cosAngle = cos(shiftRadians);
                return saturate(col * cosAngle + cross(k, col) * sin(shiftRadians) + k * dot(k, col) * (1.0 - cosAngle));
            }

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.worldPos = mul(unity_ObjectToWorld, IN.vertex).xy;
                OUT.screenPos = ComputeScreenPos(OUT.vertex);
                OUT.color = IN.color * _Color * _RendererColor;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, IN.texcoord);
                if (tex.a <= 0.01)
                    return fixed4(0, 0, 0, 0);

                float2 uv = (_UseWorldSpace > 0.5)
                    ? (IN.worldPos * _WorldScale)
                    : (IN.texcoord * float2(1.8, 1.0));

                float rad = _Angle * 0.0174532925;
                float2 dir = float2(cos(rad), sin(rad));
                float2 ortho = float2(-dir.y, dir.x);

                float t = _Time.y * _Speed;

                float2 rotatedUV = float2(dot(uv, dir), dot(uv, ortho));
                float2 flowUV = rotatedUV - float2(t, t * 0.35);

                float warpScale = max(0.5, _Scale * 2.0);
                float warpX = ValueNoise(flowUV * warpScale + float2(t * 0.3, 1.7)) - 0.5;
                float warpY = ValueNoise(flowUV * (warpScale * 1.15) - float2(2.3, t * 0.25)) - 0.5;
                float2 crinkledUV = flowUV + float2(warpX, warpY) * (_WarpStrength * 0.45);

                float faceLight1, faceHue1, edge1;
                EvaluateFoilFaces(crinkledUV, _VertexCount, _Sharpening, _Blur, _FaceBend, t * 2.5, faceLight1, faceHue1, edge1);

                float faceLight2, faceHue2, edge2;
                float2 secondaryUV = mul(float2x2(0.8, -0.6, 0.6, 0.8), crinkledUV) + float2(1.37, 2.91);
                EvaluateFoilFaces(secondaryUV, _VertexCount * 1.35, _Sharpening, _Blur, _FaceBend, -t * 1.8, faceLight2, faceHue2, edge2);

                float faceSoftness = saturate((_Blur - 0.5) / 2.5);
                float combinedFace = saturate(faceLight1 * 0.72 + faceLight2 * 0.28);
                float contrastLow = lerp(0.15, 0.0, faceSoftness);
                float contrastHigh = lerp(0.85, 1.0, faceSoftness);
                float contrastedFace = lerp(0.5, smoothstep(contrastLow, contrastHigh, combinedFace), _FaceStrength * lerp(1.0, 0.75, faceSoftness));

                float combinedEdge = max(edge1, edge2 * 0.6) * _EdgeStrength;
                float foilPattern = saturate(contrastedFace + combinedEdge * 0.5);

                float gapWidth = lerp(0.08, 0.75, saturate(_Blur / 2.5));
                float gapMask = smoothstep(_BreakupCutoff, _BreakupCutoff + gapWidth, foilPattern + (1.0 - _BreakupCutoff) * 0.25);
                float holoMask = foilPattern * gapMask;

                float3 signalRgb = IN.color.rgb;
                float3 pinkHoloRgb = _HoloColor.rgb;

                float shadowMask = saturate(1.0 - contrastedFace * lerp(1.5, 1.15, faceSoftness));
                float3 darkFaceRgb = signalRgb * (1.0 - _ShadowDepth * 0.85);
                float3 baseMetallicRgb = lerp(signalRgb, darkFaceRgb, shadowMask * _ShadowDepth);

                float facetHue = lerp(faceHue1, faceHue2, 0.35);
                float hueShift = (facetHue - 0.5) * (_Iridescence * 1.4);
                float3 shiftedPink = ShiftHue(pinkHoloRgb, hueShift);
                float3 tintedHolo = lerp(signalRgb, saturate(signalRgb * 0.5 + shiftedPink * 0.8), _HoloColorMix);

                float litLow = lerp(0.35, 0.12, faceSoftness);
                float litHigh = lerp(0.85, 0.98, faceSoftness);
                float litFaceMask = smoothstep(litLow, litHigh, contrastedFace);
                float3 finalRgb = lerp(baseMetallicRgb, tintedHolo, litFaceMask);

                float faceSpecular = pow(saturate(contrastedFace), lerp(3.0, 1.5, faceSoftness)) * (_Highlight * 0.65);
                float edgeSpecular = combinedEdge * _Highlight;
                float3 crestColor = lerp(float3(1.0, 1.0, 1.0), saturate(pinkHoloRgb * 1.15 + 0.2), _HoloColorMix * 0.85);
                finalRgb = saturate(finalRgb + (faceSpecular + edgeSpecular) * crestColor);

                float alphaFactor = saturate(_BaseTintStrength + holoMask * _Intensity);
                float finalAlpha = tex.a * IN.color.a * alphaFactor;

                if (_EnableRightFade > 0.5)
                {
                    float w = max(IN.screenPos.w, 0.0001);
                    float screenX = IN.screenPos.x / w;
                    float rightFade = smoothstep(_RightFadeStart, _RightFadeEnd, screenX);
                    finalRgb = lerp(finalRgb, _BackgroundColor.rgb, rightFade * _BackgroundColor.a);
                }

                return fixed4(finalRgb, finalAlpha);
            }
            ENDCG
        }
    }
}
