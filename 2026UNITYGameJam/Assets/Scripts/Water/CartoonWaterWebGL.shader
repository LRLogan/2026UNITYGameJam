Shader "GameJam/Cartoon Water WebGL"
{
    Properties
    {
        _DeepColor ("Deep Water", Color) = (0.20, 0.52, 0.75, 1)
        _ShallowColor ("Light Water", Color) = (0.32, 0.75, 0.80, 1)
        _FoamColor ("Fleck Color", Color) = (0.88, 0.96, 0.95, 1)
        _WaveScale ("Wave Scale (per World Unit)", Range(0.05, 5)) = 1.2
        _Speed ("Animation Speed", Range(0, 3)) = 0.65
        _NormalStrength ("Wave Normal Strength", Range(0, 1)) = 0.25
        _FoamAmount ("Fleck Coverage", Range(0, 0.5)) = 0.12
        _FleckSize ("Fleck Size", Range(0.3, 1.5)) = 0.85
        _PatchScale ("Color Patch Scale", Range(0.02, 1)) = 0.18
        _RimStrength ("Viewing Angle Highlight", Range(0, 1)) = 0.4
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            Name "CartoonWater"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor;
                half4 _ShallowColor;
                half4 _FoamColor;
                float _WaveScale;
                float _Speed;
                float _NormalStrength;
                float _FoamAmount;
                float _FleckSize;
                float _PatchScale;
                float _RimStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half fogFactor : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            float3 HashCell(float2 p)
            {
                // Arithmetic hash avoids a repeating sine-wave lattice.
                float3 h = frac(float3(p.x, p.y, p.x) * float3(0.1031, 0.1030, 0.0973));
                h += dot(h, h.yxz + 33.33);
                return frac((h.xxy + h.yzz) * h.zyx);
            }

            float Noise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(HashCell(cell).x, HashCell(cell + float2(1, 0)).x, f.x),
                            lerp(HashCell(cell + float2(0, 1)).x,
                                 HashCell(cell + float2(1, 1)).x, f.x), f.y);
            }

            float Flecks(float2 p, float coverage)
            {
                float2 cell = floor(p);
                float3 random = HashCell(cell);
                float3 detail = HashCell(cell + 71.83);
                float2 center = 0.3 + random.xy * 0.4;
                float2 offset = frac(p) - center;
                float angle = detail.x * 6.2831853;
                float s = sin(angle);
                float c = cos(angle);
                offset = float2(c * offset.x - s * offset.y, s * offset.x + c * offset.y);
                float size = lerp(0.055, 0.14, detail.y) * _FleckSize;
                float2 shape = offset / float2(size, size * lerp(0.35, 0.7, detail.z));
                // Slightly asymmetric blobs, with randomized size, direction and occupancy.
                float distanceToEdge = length(shape) + 0.13 * sin(shape.x * 4.0 + detail.z * 6.0);
                float aa = max(fwidth(distanceToEdge), 0.035);
                float mask = 1.0 - smoothstep(1.0 - aa, 1.0 + aa, distanceToEdge);
                // Fade subpixel flecks instead of letting them sparkle at the horizon.
                mask *= 1.0 - smoothstep(0.5, 2.0, aa);
                return mask * step(random.z, coverage) * saturate(coverage * 100.0);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // World-space XZ mapping: intended for horizontal water surfaces.
                float2 p = input.positionWS.xz * _WaveScale;
                float t = _Time.y * _Speed;
                float2 drift = float2(t * 0.08, t * 0.035);
                float2 warp = float2(Noise(p * 0.32 + drift),
                                     Noise(p * 0.32 - drift + 29.7)) - 0.5;
                float2 patches = p * _PatchScale + warp * 0.7 + drift * 0.3;
                float wave = Noise(patches) * 0.75 + Noise(patches * 2.13 + 17.3) * 0.25;
                float band = smoothstep(0.15, 0.85, wave);
                half3 color = lerp(_DeepColor.rgb, _ShallowColor.rgb, band);

                float2 slope = warp * _NormalStrength;
                float3 normalWS = normalize(float3(-slope.x, 1.0, -slope.y));
                float3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float rim = 1.0 - saturate(dot(normalWS, viewWS));
                color = lerp(color, _ShallowColor.rgb, rim * rim * _RimStrength);

                // Two independently drifting layers break up visible cell spacing.
                float2 foamUV = p + warp * 0.55 - drift;
                float ripple = Flecks(foamUV, saturate(_FoamAmount * 2.0));
                float2 secondaryUV = float2(0.8 * p.x - 0.6 * p.y, 0.6 * p.x + 0.8 * p.y);
                ripple = max(ripple, Flecks(secondaryUV * 1.73 + warp * 0.4
                                           - drift * 0.65 + 43.7, _FoamAmount));
                color = lerp(color, _FoamColor.rgb, ripple);
                return half4(MixFog(color, input.fogFactor), 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
