Shader "GameJam/Main Menu Frosted Panel"
{
    Properties { _MainTex ("Background", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
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
            fixed4 frag(v2f i) : SV_Target
            {
                float edge = 0.34 + (1.0 - i.uv.y) * 0.12;
                float mask = 1.0 - smoothstep(edge - 0.02, edge + 0.02, i.uv.x);
                clip(mask - 0.001);
                float2 d = _MainTex_TexelSize.xy * 10;
                half3 col = 0;
                [unroll] for (int x=-2; x<=2; x++)
                    [unroll] for (int y=-2; y<=2; y++)
                        col += tex2D(_MainTex, saturate(i.uv + float2(x,y)*d)).rgb;
                col = col / 25.0 * 0.28 + half3(0.018, 0.035, 0.035);
                return half4(col, mask * 0.97);
            }
            ENDCG
        }
    }
}
