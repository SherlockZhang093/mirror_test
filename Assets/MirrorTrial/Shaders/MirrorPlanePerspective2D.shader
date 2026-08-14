Shader "MirrorTrial/Mirror Plane Perspective 2D"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
        _LeftEdgeScale ("Left Edge Scale", Range(0.7,1.3)) = 1.08
        _RightEdgeScale ("Right Edge Scale", Range(0.7,1.3)) = 0.9
        _VerticalSkew ("Vertical Skew", Range(-0.3,0.3)) = -0.05
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex PerspectiveSpriteVert
            #pragma fragment SpriteFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"
            float _LeftEdgeScale;
            float _RightEdgeScale;
            float _VerticalSkew;
            v2f PerspectiveSpriteVert(appdata_t input)
            {
                float side = lerp(_LeftEdgeScale, _RightEdgeScale, input.texcoord.x);
                input.vertex.y *= side;
                input.vertex.x += (input.texcoord.y - 0.5) * _VerticalSkew;
                return SpriteVert(input);
            }
            ENDCG
        }
    }
}
