Shader "GameJam/Jackpot Glow"
{
    Properties { _Round ("Round Particle", Float) = 0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha One
            Cull Off
            ZWrite Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Round;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv; o.color = v.color; return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float edge = lerp(abs(i.uv.y * 2 - 1), length(i.uv * 2 - 1), _Round);
                float glow = saturate(1 - edge);
                return half4(lerp(i.color.rgb, half3(1,1,1), pow(glow, 6) * 0.6) * 2,
                    glow * glow * i.color.a);
            }
            ENDHLSL
        }
    }
}
