Shader "MirrorTrial/Pixel Water"
{
    Properties
    {
        _WaterTex("Water Texture", 2D) = "white" {}
        _ReflectionTex("Reflection", 2D) = "black" {}
        _ShallowColor("Shallow", Color) = (0.13,0.36,0.39,0.78)
        _DeepColor("Deep", Color) = (0.035,0.16,0.19,0.92)
        _HighlightColor("Highlight", Color) = (0.55,0.78,0.75,0.82)
        _FlowSpeed("Flow Speed", Float) = 0.16
        _WaveScale("Wave Scale", Float) = 0.8
        _PixelDensity("Pixel Density", Float) = 14
        _WaterTop("Water Top", Float) = 0
        _WorldTiling("World Tiling", Float) = 0.22
        _ReflectionStrength("Reflection Strength", Range(0,1)) = 0.24
        _ReflectionSurfaceV("Reflection Surface V", Float) = 0.5
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_WaterTex); SAMPLER(sampler_WaterTex);
            TEXTURE2D(_ReflectionTex); SAMPLER(sampler_ReflectionTex);
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 world : TEXCOORD0; float2 local : TEXCOORD1; float4 screenPos : TEXCOORD2; };
            CBUFFER_START(UnityPerMaterial)
            half4 _ShallowColor, _DeepColor, _HighlightColor;
            float _FlowSpeed, _WaveScale, _PixelDensity, _WaterTop;
            float _WorldTiling, _ReflectionStrength, _ReflectionSurfaceV;
            CBUFFER_END
            Varyings vert(Attributes i)
            {
                Varyings o;
                float3 world = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(world);
                o.world = world;
                o.local = i.positionOS.xy + 0.5;
                o.screenPos = ComputeScreenPos(o.positionCS);
                return o;
            }
            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }
            half4 frag(Varyings i) : SV_Target
            {
                float2 p = floor(i.world.xy * _PixelDensity) / _PixelDensity;
                float time = _Time.y * _FlowSpeed;
                float wave = sin((p.x + time) * 2.15 * _WaveScale) * 0.025;
                wave += sin((p.x * 0.43 - time * 0.72) * 4.7 * _WaveScale) * 0.012;
                clip((_WaterTop + wave) - i.world.y);
                float depth = saturate(1.0 - i.local.y);
                float2 waterUV = p * _WorldTiling;
                waterUV.x += time * 0.055;
                half3 waterDetail = SAMPLE_TEXTURE2D(_WaterTex, sampler_WaterTex, waterUV).rgb;
                half luminance = dot(waterDetail, half3(0.22, 0.67, 0.11));
                half3 baseColor = lerp(_DeepColor.rgb, _ShallowColor.rgb, saturate(luminance * 1.1));
                half4 color = half4(baseColor, lerp(_ShallowColor.a, _DeepColor.a, depth));

                float2 screenUV = i.screenPos.xy / max(i.screenPos.w, 0.0001);
                float reflectedV = _ReflectionSurfaceV + (_ReflectionSurfaceV - screenUV.y);
                float distortion = (sin((p.y * 29.0 + time * 2.0)) + sin((p.x * 7.0 - time))) * 0.0018;
                float2 reflectionUV = float2(screenUV.x + distortion, reflectedV);
                half4 reflection = SAMPLE_TEXTURE2D(_ReflectionTex, sampler_ReflectionTex, reflectionUV);
                float reflectionFade = saturate(1.0 - depth * 2.8) * step(0.0, reflectedV) * step(reflectedV, 1.0);
                float brokenReflection = lerp(0.35, 1.0, step(0.15, frac(p.y * 13.0 + sin(p.x * 3.0))));
                reflection.rgb *= half3(0.44, 0.61, 0.58);
                color.rgb = lerp(color.rgb, reflection.rgb, reflection.a * reflectionFade * brokenReflection * _ReflectionStrength);

                float row = floor(p.y * 5.0);
                float segment = floor((p.x + time * (0.7 + frac(row * 0.37))) * 2.2);
                float segmentNoise = Hash21(float2(segment, row));
                float rippleShape = sin(p.x * (4.2 + segmentNoise * 2.0) + row * 1.7 + time * 2.0);
                float thinRipple = step(0.93, rippleShape) * step(0.68, segmentNoise);
                float surfaceFade = saturate(1.0 - abs(i.world.y - (_WaterTop + wave)) * 1.8);
                float highlight = thinRipple * lerp(0.10, 0.38, surfaceFade);
                color.rgb = lerp(color.rgb, _HighlightColor.rgb, highlight);

                float darkRipple = step(0.95, -rippleShape) * step(0.45, Hash21(float2(segment + 7.0, row)));
                color.rgb *= 1.0 - darkRipple * 0.08 * depth;
                return color;
            }
            ENDHLSL
        }
    }
}
