Shader "GameJam/Objective Beacon"
{
    Properties { _Intensity ("Brightness", Range(1, 6)) = 2.5 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                // LineRenderer UV.x runs along the beam; UV.y runs across it.
                float center = saturate(1.0 - abs(i.uv.y * 2.0 - 1.0));
                float core = pow(center, 12.0);
                float glow = center * center * 0.35 + core * 0.65;
                float topFade = 1.0 - smoothstep(0.75, 1.0, i.uv.x);
                float pulse = 0.92 + 0.08 * sin(_Time.y * 2.0 - i.uv.x * 8.0);
                return half4(lerp(i.color.rgb, half3(1,1,1), core * 0.7) * _Intensity,
                    glow * topFade * pulse * i.color.a);
            }
            ENDHLSL
        }
    }
}
