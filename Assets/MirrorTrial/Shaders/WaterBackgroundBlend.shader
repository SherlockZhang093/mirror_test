Shader "MirrorTrial/Water Background Blend"
{
    Properties
    {
        _BlendColor("Blend Color", Color) = (0.08,0.39,0.43,0.3)
        _PixelDensity("Pixel Density", Float) = 14
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
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 local : TEXCOORD0; float3 world : TEXCOORD1; };
            CBUFFER_START(UnityPerMaterial)
            half4 _BlendColor;
            float _PixelDensity;
            CBUFFER_END
            Varyings vert(Attributes i)
            {
                Varyings o;
                float3 world = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(world);
                o.local = i.positionOS.xy + 0.5;
                o.world = world;
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float quantizedY = floor(i.local.y * _PixelDensity) / _PixelDensity;
                float fade = pow(saturate(1.0 - quantizedY), 1.45);
                float subtleVariation = 0.94 + 0.06 * sin(floor(i.world.x * 2.0) * 1.7);
                return half4(_BlendColor.rgb, _BlendColor.a * fade * subtleVariation);
            }
            ENDHLSL
        }
    }
}
