// 연못 물: 가운데가 밝고 가장자리가 어두운 바닷물 + 일렁이는 빛무늬(코스틱)
// Built-in / URP 모두에서 동작하는 단순 Unlit 셰이더입니다.
Shader "FishPond/Water"
{
    Properties
    {
        _CenterColor ("Center", Color) = (0.11, 0.62, 0.68, 1)
        _EdgeColor ("Edge", Color) = (0.03, 0.24, 0.32, 1)
        _CausticColor ("Caustic (A = 세기)", Color) = (0.78, 1, 0.96, 0.32)
        _CausticScale ("Caustic Scale", Float) = 5.5
        _Speed ("Speed", Float) = 0.45
        _Radius ("Radius", Float) = 0.98
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _CenterColor, _EdgeColor, _CausticColor;
            float _CausticScale, _Speed, _Radius;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // 가볍게 계산되는 물속 빛무늬
            float caustic(float2 p, float t)
            {
                float2 i = p;
                float c = 1.0;
                const float inten = 0.005;
                [unroll]
                for (int n = 0; n < 4; n++)
                {
                    float tt = t * (1.0 - 3.5 / (n + 1.0));
                    i = p + float2(cos(tt - i.x) + sin(tt + i.y), sin(tt - i.y) + cos(tt + i.x));
                    float2 q = float2(p.x / (sin(i.x + tt) / inten), p.y / (cos(i.y + tt) / inten));
                    c += 1.0 / max(length(q), 1e-4);
                }
                c /= 4.0;
                c = 1.17 - pow(abs(c), 1.4);
                return pow(abs(c), 8.0);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 p = (i.uv - 0.5) * 2.0;
                float d = length(p);
                float alpha = 1.0 - smoothstep(_Radius - 0.012, _Radius, d);

                float3 col = lerp(_CenterColor.rgb, _EdgeColor.rgb, pow(saturate(d / _Radius), 1.6));
                float t = _Time.y * _Speed;
                float c = caustic(p * _CausticScale + 20.0, t);
                col += _CausticColor.rgb * saturate(c) * _CausticColor.a * (1.0 - d * 0.5);
                col *= 1.0 - smoothstep(0.75, 1.0, d / _Radius) * 0.35;
                return fixed4(col, alpha);
            }
            ENDCG
        }
    }
}
