// 연못 물: 가운데가 밝고 가장자리가 어두운 바닷물 + 일렁이는 빛무늬(코스틱)
// Built-in / URP 모두에서 동작하는 단순 Unlit 셰이더입니다.
Shader "FishPond/Water"
{
    Properties
    {
        _CenterColor ("Center", Color) = (0.2, 0.66, 0.68, 1)
        _EdgeColor ("Edge", Color) = (0.04, 0.26, 0.36, 1)
        _SandColor ("Sand", Color) = (0.86, 0.8, 0.6, 1)
        _SandAmount ("Sand Amount", Range(0, 1)) = 0.32
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

            fixed4 _CenterColor, _EdgeColor, _CausticColor, _SandColor;
            float _SandAmount;
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

            // 물속 빛무늬: 일렁이는 격자의 0선(sin x · sin y ≈ 0)을 가는 빛줄기로 그림.
            // 나눗셈이 없어 값이 깨지지 않고, 항상 밝게만 더해집니다.
            float caustic(float2 p, float t)
            {
                float2 q = p;
                float c = 0.0;
                [unroll]
                for (int n = 0; n < 3; n++)
                {
                    q += float2(sin(q.y * 1.7 + t * 1.3 + n), cos(q.x * 1.5 - t * 1.1 + n * 1.7)) * 0.55;
                    c += abs(sin(q.x) * sin(q.y));
                }
                c /= 3.0;
                return pow(saturate(1.0 - c), 6.0);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 p = (i.uv - 0.5) * 2.0;
                float d = length(p);
                float alpha = 1.0 - smoothstep(_Radius - 0.012, _Radius, d);

                float3 col = lerp(_CenterColor.rgb, _EdgeColor.rgb, pow(saturate(d / _Radius), 1.6));

                // 모래 바닥: 큰 얼룩 + 물결 자국이 물빛 아래로 비침
                float patch = 0.5 + 0.25 * sin(p.x * 3.1 + sin(p.y * 2.3) * 1.7) + 0.25 * sin(p.y * 2.7 + sin(p.x * 1.9) * 2.1);
                float ripple = sin(dot(p, float2(0.8, 0.6)) * 34.0 + sin(p.y * 6.0) * 2.0 + sin(p.x * 4.0) * 1.5);
                float sand = _SandAmount * saturate(patch) * (1.0 - smoothstep(0.55, 1.0, d / _Radius) * 0.6);
                col = lerp(col, _SandColor.rgb * (0.92 + 0.08 * ripple), sand);
                col *= 0.97 + 0.03 * ripple;
                float t = _Time.y * _Speed;
                float c = caustic(p * _CausticScale, t);
                col += _CausticColor.rgb * c * _CausticColor.a * (1.0 - d * 0.5);
                col *= 1.0 - smoothstep(0.75, 1.0, d / _Radius) * 0.35;
                return fixed4(saturate(col), alpha);
            }
            ENDCG
        }
    }
}
