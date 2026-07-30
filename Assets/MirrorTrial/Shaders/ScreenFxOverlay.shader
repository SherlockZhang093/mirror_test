Shader "MirrorTrial/UI/ScreenFxOverlay"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _BossColor ("Boss Color", Color) = (0.008, 0.012, 0.022, 1)
        _DamageColor ("Damage Color", Color) = (0.48, 0.005, 0.008, 1)
        _BossIntensity ("Boss Intensity", Range(0,1)) = 0
        _DamageIntensity ("Damage Intensity", Range(0,1)) = 0
        _LowHealthIntensity ("Low Health Intensity", Range(0,1)) = 0
        _PhasePulse ("Phase Pulse", Range(0,1)) = 0
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "ScreenFxOverlay"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            float4 _BossColor, _DamageColor;
            float _BossIntensity, _DamageIntensity, _LowHealthIntensity, _PhasePulse, _FlowTime;
            float2 _DamageDirection;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash21(i), hash21(i + float2(1,0)), f.x),
                            lerp(hash21(i + float2(0,1)), hash21(i + 1.0), f.x), f.y);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float aspect = _ScreenParams.x / max(1.0, _ScreenParams.y);
                float2 centered = (uv - 0.5) * float2(aspect, 1.0);
                float edgeDistance = min(min(uv.x, 1.0 - uv.x), min(uv.y, 1.0 - uv.y));
                float n1 = noise(uv * float2(5.0 * aspect, 5.0) + float2(_FlowTime * 0.035, -_FlowTime * 0.018));
                float n2 = noise(uv * float2(11.0 * aspect, 11.0) + float2(-_FlowTime * 0.022, _FlowTime * 0.029));
                float organic = (n1 * 0.7 + n2 * 0.3 - 0.5) * 0.065;

                float bossWidth = 0.115 + _PhasePulse * 0.075 + organic;
                float bossMask = 1.0 - smoothstep(0.008, max(0.025, bossWidth), edgeDistance);
                bossMask = pow(saturate(bossMask), 1.25);

                float directionBias = dot(centered, -_DamageDirection) * 0.045;
                float damageWidth = 0.105 + directionBias;
                float damageMask = 1.0 - smoothstep(0.0, damageWidth, edgeDistance);
                damageMask = pow(saturate(damageMask), 1.65);

                float bossAlpha = saturate((_BossIntensity + _PhasePulse) * bossMask * 0.82);
                float danger = saturate(_DamageIntensity + _LowHealthIntensity);
                float redAlpha = saturate(danger * damageMask * 0.78);
                float totalAlpha = saturate(bossAlpha + redAlpha - bossAlpha * redAlpha * 0.35);
                float3 color = lerp(_BossColor.rgb, _DamageColor.rgb, saturate(redAlpha * 1.35));
                return half4(color, totalAlpha);
            }
            ENDHLSL
        }
    }
}
