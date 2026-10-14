Shader "TwinInspector/Twin"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "TwinUnlit"
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite On
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            float _TwinAlpha, _TwinTechnical;
            float4 _TwinTint;
            struct Attributes { float3 positionOS : POSITION; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 world : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.world = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(output.world);
                output.color = input.color;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                clip(_TwinAlpha - .0001);
                float3 normal = normalize(cross(ddx(input.world), ddy(input.world)) + 1e-9);
                float edge = saturate(length(fwidth(normal)) * 2.5);
                half3 fill = lerp(SRGBToLinear(input.color.rgb), _TwinTint.rgb, _TwinTechnical);
                fill = lerp(fill, half3(.0056,.0056,.0056), edge * .7 * _TwinTechnical);
                return half4(fill, _TwinAlpha);
            }
            ENDHLSL
        }
    }
}
