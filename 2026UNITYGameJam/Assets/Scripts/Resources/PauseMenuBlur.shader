Shader "GameJam/Pause Menu Blur"
{
    Properties { _MainTex ("Paused Frame", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o; }
            half4 frag(v2f i) : SV_Target
            {
                float2 d = _MainTex_TexelSize.xy * 8;
                half3 color = 0;
                [unroll] for (int x=-2; x<=2; x++)
                    [unroll] for (int y=-2; y<=2; y++)
                        color += tex2D(_MainTex, saturate(i.uv + float2(x,y)*d)).rgb;
                return half4(color / 25.0 * 0.3 + half3(0.015, 0.03, 0.03), 1);
            }
            ENDCG
        }
    }
}
