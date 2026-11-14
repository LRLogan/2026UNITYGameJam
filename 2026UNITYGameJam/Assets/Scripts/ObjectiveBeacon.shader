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
            struct Varyings { float4 positionCS : SV_POSITION; half across : TEXCOORD0; half4 color : COLOR; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.across = v.uv.y;
                o.color = v.color;
                // A segmented line interpolates these slowly varying values cheaply.
                half topFade = 1.0h - smoothstep(0.75, 1.0, v.uv.x);
                half pulse = 0.92h + 0.08h * sin(_Time.y * 2.0 - v.uv.x * 8.0);
                o.color.a *= topFade * pulse;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                // LineRenderer UV.x runs along the beam; UV.y runs across it.
                half center = saturate(1.0h - abs(i.across * 2.0h - 1.0h));
                half squared = center * center;
                half fourth = squared * squared;
                half core = fourth * fourth * fourth;
                half glow = squared * 0.35h + core * 0.65h;
                return half4(lerp(i.color.rgb, half3(1,1,1), core * 0.7) * _Intensity,
                    glow * i.color.a);
            }
            ENDHLSL
        }
    }
}
