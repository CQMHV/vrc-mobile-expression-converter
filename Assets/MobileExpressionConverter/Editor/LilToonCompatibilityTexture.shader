Shader "Hidden/MobileExpressionConverter/LilToonTexture"
{
    Properties
    {
        _MainTex ("Base texture", 2D) = "white" {}
        _Color ("Base color", Color) = (1,1,1,1)
        _AlphaMask ("Alpha mask", 2D) = "white" {}
        _AlphaMaskMode ("Alpha mode", Int) = 0
        _AlphaMaskScale ("Alpha scale", Float) = 1
        _AlphaMaskValue ("Alpha offset", Float) = 0
        _Poiyomi ("Poiyomi", Float) = 0
        _IgnoreAlpha ("Ignore texture alpha", Float) = 0
        _MaskInvert ("Invert mask", Float) = 0
        _AlphaMod ("Alpha adjustment", Float) = 0
        _Cutoff ("Alpha cutoff", Float) = 0
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            UNITY_DECLARE_TEX2D(_MainTex);
            UNITY_DECLARE_TEX2D_NOSAMPLER(_AlphaMask);
            float4 _Color;
            float4 _AlphaMask_ST;
            int _AlphaMaskMode;
            float _AlphaMaskScale;
            float _AlphaMaskValue;
            float _Poiyomi, _IgnoreAlpha, _MaskInvert, _AlphaMod, _Cutoff;

            float4 frag(v2f_img input) : SV_Target
            {
                float4 color = UNITY_SAMPLE_TEX2D(_MainTex, input.uv);
                if (_Poiyomi != 0) color.a = max(color.a, _IgnoreAlpha);
                color *= _Color;
                if (_AlphaMaskMode != 0)
                {
                    float2 maskUV = input.uv * _AlphaMask_ST.xy + _AlphaMask_ST.zw;
                    float mask = saturate(UNITY_SAMPLE_TEX2D_SAMPLER(_AlphaMask, _MainTex, maskUV).r *
                        _AlphaMaskScale + _AlphaMaskValue);
                    if (_Poiyomi != 0) mask = lerp(mask, 1 - mask, _MaskInvert);
                    // lilToon applies its mask after base color; Replace overrides base alpha and tint alpha.
                    if (_AlphaMaskMode == 1) color.a = mask;
                    else if (_AlphaMaskMode == 2) color.a *= mask;
                    else if (_AlphaMaskMode == 3) color.a = saturate(color.a + mask);
                    else if (_AlphaMaskMode == 4) color.a = saturate(color.a - mask);
                }
                if (_Poiyomi != 0)
                {
                    color.a = saturate(color.a + _AlphaMod);
                    if (color.a < _Cutoff) color.a = 0;
                }
                return color;
            }
            ENDCG
        }
    }
}
