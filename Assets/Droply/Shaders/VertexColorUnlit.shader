// Draws the colours that are baked into the vertices (Assets/Droply/Scripts/Layout/Lighting.cs): no lights, no shadow maps, no textures, no per-pixel maths.
// The whole look of the landscape (warm sun, soft shadows, ambient occlusion, haze towards the horizon) is already in the vertex colour. It is stored as sRGB bytes
// (Look.Encode) and decoded to linear here, so the blend across a triangle is done in linear space like everything else on a linear colour space project
// (the same decode as Tools/Preview/index.html, which is why the preview matches the headset up to the display).
// One pass, written for the Quest: single-pass instanced stereo macros, SRP Batcher compatible (everything per material is in UnityPerMaterial).
Shader "Droply/Vertex Colour Unlit"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [Toggle] _ZWrite ("Depth write", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "IgnoreProjector" = "True" }
        LOD 100

        Pass
        {
            Name "VertexColourUnlit"

            Cull [_Cull]
            ZWrite [_ZWrite]
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 color : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.color = pow(max(input.color.rgb, 0.0001), 2.2) * _BaseColor.rgb;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return half4(input.color, 1.0h);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
