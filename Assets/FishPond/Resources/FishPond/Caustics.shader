// 물빛: 바닥 위에 깔리는 둥근 물 층.
// 1패스: 바닥을 물색으로 곱해 물속처럼 보이게 / 2패스: 물결 빛(코스틱) 텍스처 두 장을 섞어 더함.
// 생물·장식보다 뒤(z가 큼)에 있어서 깊이 테스트로 그것들 위에는 그려지지 않음.
Shader "FishPond/Caustics"
{
    Properties
    {
        _Tex0 ("Caustic frame A", 2D) = "black" {}
        _Tex1 ("Caustic frame B", 2D) = "black" {}
        _Blend ("A→B", Range(0,1)) = 0
        _Tint ("Water tint (multiply)", Color) = (0.45, 0.78, 0.82, 1)
        _Light ("Caustic light color", Color) = (0.75, 1, 0.95, 1)
        _Strength ("Caustic strength", Float) = 0.55
        _Tiling ("Caustic tiling", Float) = 3
        _Drift ("Second layer drift", Vector) = (0.013, 0.009, 0, 0)
        _Radius ("Circle radius (uv)", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent-10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        ZWrite Off
        Cull Off

        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _Tex0, _Tex1;
        float _Blend, _Strength, _Tiling, _Radius;
        fixed4 _Tint, _Light;
        float4 _Drift;
        struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
        struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
        v2f vert (appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o; }
        float Dist(float2 uv) { return length(uv - 0.5) * 2.0 / _Radius; }
        float Circle(float2 uv) { return saturate((1.0 - Dist(uv)) * 60.0); }
        ENDCG

        Pass
        {
            Blend DstColor Zero
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            fixed4 frag (v2f i) : SV_Target
            {
                float m = Circle(i.uv);
                // 가장자리로 갈수록 조금 더 깊고 어둡게, 원 밖은 검게(투사되지 않는 곳)
                float d = Dist(i.uv);
                fixed3 tint = _Tint.rgb * lerp(1.05, 0.8, d * d);
                return fixed4(tint * m, 1);
            }
            ENDCG
        }
        Pass
        {
            Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            fixed4 frag (v2f i) : SV_Target
            {
                float m = Circle(i.uv);
                float2 cuv = (i.uv - 0.5) / _Radius + 0.5;
                float2 uv1 = cuv * _Tiling;
                float2 uv2 = cuv * _Tiling * 0.73 + _Drift.xy * _Time.y + 0.37;
                float a = lerp(tex2D(_Tex0, uv1).r, tex2D(_Tex1, uv1).r, _Blend);
                float b = lerp(tex2D(_Tex1, uv2).r, tex2D(_Tex0, uv2).r, _Blend);
                float c = a * 0.65 + b * 0.35;
                c = c * c * 1.6;
                return fixed4(_Light.rgb * c * _Strength * m, 1);
            }
            ENDCG
        }
    }
}
