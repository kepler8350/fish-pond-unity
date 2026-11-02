// 모래섬: 바닥 모델(에셋)의 모래 텍스처를 둥글게 오려 물 위에 깝니다.
// 가장자리는 젖은 모래로 어둡게, 그 바깥은 물에 부드럽게 녹아들고, 하얀 파도 거품이 살랑입니다.
Shader "FishPond/SandIsland"
{
    Properties
    {
        _MainTex ("Sand texture", 2D) = "white" {}
        _Tint ("Sand tint", Color) = (1.05, 0.98, 0.86, 1)
        _Tiling ("Texture tiling", Float) = 1.5
        _Radius ("Island radius (uv)", Float) = 0.9
        _Soft ("Edge softness (uv)", Float) = 0.06
        _Foam ("Foam color", Color) = (1, 1, 1, 0.75)
    }
    SubShader
    {
        Tags { "Queue"="Transparent-5" "RenderType"="Transparent" "IgnoreProjector"="True" }
        ZWrite Off
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _Tiling, _Radius, _Soft;
            fixed4 _Tint, _Foam;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert (appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o; }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 c = (i.uv - 0.5) * 2.0;
                float ang = atan2(c.y, c.x);
                // 해안선이 매끈한 원이 아니도록 살짝 출렁이게
                float wob = sin(ang * 5.0) * 0.012 + sin(ang * 11.0 + 1.3) * 0.007 + sin(ang * 3.0 + _Time.y * 0.6) * 0.006;
                float d = length(c) / _Radius - wob;
                fixed3 sand = tex2D(_MainTex, i.uv * _Tiling).rgb * _Tint.rgb;
                // 젖은 모래 (해안선 가까이 어둡게)
                float wet = smoothstep(0.82, 1.0, d);
                sand *= lerp(1.0, 0.72, wet);
                float a = 1.0 - smoothstep(1.0 - _Soft * 0.3, 1.0 + _Soft, d);
                // 파도 거품: 해안선을 따라 밀려왔다 빠지는 하얀 띠
                float wave = 0.985 + sin(_Time.y * 0.9 + ang * 2.0) * 0.012;
                float foam = (1.0 - smoothstep(0.0, 0.025, abs(d - wave))) * (0.55 + 0.45 * sin(ang * 23.0 + _Time.y * 1.7));
                foam = saturate(foam) * _Foam.a;
                fixed3 col = lerp(sand, _Foam.rgb, foam);
                return fixed4(col, saturate(a + foam * 0.6));
            }
            ENDCG
        }
    }
}
