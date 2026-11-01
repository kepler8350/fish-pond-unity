// 지느러미·꼬리 움직임이 없는 물고기 모델을 헤엄치는 것처럼 보이게:
// 몸통 축을 따라 머리→꼬리로 갈수록 커지는 옆 물결(사인파)로 정점을 흔듦. 조명은 Standard 그대로.
Shader "FishPond/FishSwim"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _BumpMap ("Normal", 2D) = "bump" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0
        _Cutoff ("Alpha cutoff", Range(0,1)) = 0.3
        _SwimAxis ("Body axis (object, head direction)", Vector) = (0,0,1,0)
        _SwimSide ("Wag direction (object)", Vector) = (1,0,0,0)
        _SwimHead ("Head position along axis", Float) = 0.5
        _SwimLength ("Body length", Float) = 1
        _SwimAmp ("Wag amplitude (x length)", Float) = 0.09
        _SwimWaves ("Waves along body", Float) = 1.3
        _SwimPhase ("Phase", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Cull Off
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow alphatest:_Cutoff
        #pragma target 3.0
        sampler2D _MainTex, _BumpMap;
        fixed4 _Color;
        half _Glossiness, _Metallic;
        float4 _SwimAxis, _SwimSide;
        float _SwimHead, _SwimLength, _SwimAmp, _SwimWaves, _SwimPhase;
        struct Input { float2 uv_MainTex; };

        void vert (inout appdata_full v)
        {
            float along = dot(v.vertex.xyz, _SwimAxis.xyz);
            float t = saturate((_SwimHead - along) / max(_SwimLength, 1e-5));   // 0=머리, 1=꼬리
            float w = t * t;                                                     // 머리는 거의 안 흔들림
            float s = sin(_SwimPhase - t * _SwimWaves * 6.2831853);
            v.vertex.xyz += _SwimSide.xyz * (s * w * _SwimAmp * _SwimLength);
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Alpha = c.a;
            o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_MainTex));
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
