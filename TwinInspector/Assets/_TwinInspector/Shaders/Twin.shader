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
                // A second derivative of the reconstructed normal is undefined
                // on some GPUs. Use first-derivative surface relief and grazing
                // silhouette contrast, keeping Technical independent of lights.
                float3 view = normalize(GetWorldSpaceViewDir(input.world));
                float edge = pow(saturate(1 - abs(dot(normal, view))), 5);
                float relief = .42 + .58 * abs(dot(normal, normalize(float3(-.35,.8,.48))));
                half3 colour = SRGBToLinear(input.color.rgb);
                half luminance = dot(colour, half3(.2126,.7152,.0722));
                half3 technical = _TwinTint.rgb * relief * (.8 + .2 * luminance);
                half3 fill = lerp(colour, technical, _TwinTechnical);
                fill = lerp(fill, half3(.0056,.0056,.0056), edge * .7 * _TwinTechnical);
                return half4(fill, _TwinAlpha);
            }
            ENDHLSL
        }
    }
}
