Shader "MirrorTrial/Mirror Glass 2D"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _GlassTint ("Glass Tint", Color) = (0.20,0.27,0.38,1)
        _Opacity ("Opacity", Range(0,1)) = 0.24
        _HighlightStrength ("Moving Highlight", Range(0,1)) = 0.10
        _FlowSpeed ("Highlight Speed", Range(-1,1)) = 0.05
        _LeftEdgeScale ("Left Edge Scale", Range(0.7,1.3)) = 1.08
        _RightEdgeScale ("Right Edge Scale", Range(0.7,1.3)) = 0.9
        _VerticalSkew ("Vertical Skew", Range(-0.3,0.3)) = -0.05
        [Toggle(PIXELSNAP_ON)] PixelSnap ("Pixel snap", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
            "RenderPipeline"="UniversalPipeline"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ PIXELSNAP_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _GlassTint;
                float _Opacity;
                float _HighlightStrength;
                float _FlowSpeed;
                float _LeftEdgeScale;
                float _RightEdgeScale;
                float _VerticalSkew;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionOS = input.positionOS.xyz;
                float side = lerp(_LeftEdgeScale, _RightEdgeScale, input.uv.x);
                positionOS.y *= side;
                positionOS.x += (input.uv.y - 0.5) * _VerticalSkew;
                output.positionCS = TransformObjectToHClip(positionOS);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 source = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float diagonal = frac(input.uv.x * 0.72 + input.uv.y * 0.28 + _Time.y * _FlowSpeed);
                float highlight = 1.0 - smoothstep(0.055, 0.16, abs(diagonal - 0.5));
                float edge = saturate(abs(input.uv.x - 0.5) * 2.0);

                half3 glass = lerp(_GlassTint.rgb, source.rgb, 0.10);
                glass += highlight * _HighlightStrength;
                glass += edge * _HighlightStrength * 0.18;

                half alpha = source.a * input.color.a * _Opacity;
                return half4(glass * input.color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
