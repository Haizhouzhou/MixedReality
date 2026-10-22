Shader "TwinInspector/DiffPatch"
{
    Properties { _Color("Type colour",Color)=(0,0.25,1,.35) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+4" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off ZTest LEqual Cull Off Offset -1,-1
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "TwinInspection.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            CBUFFER_END
            struct A {float3 position:POSITION;float2 uv:TEXCOORD0;float3 other:TEXCOORD1;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 position:SV_POSITION;float3 world:TEXCOORD0;float outline:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
            V Vert(A i)
            {
                V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.world=TransformObjectToWorld(i.position);o.position=TransformWorldToHClip(o.world);o.outline=step(.5,i.uv.y);
                if(o.outline>.5)
                {
                    float4 b=TransformObjectToHClip(i.other);
                    float2 d=(b.xy/max(.001,b.w)-o.position.xy/max(.001,o.position.w))*_ScaledScreenParams.xy*(i.uv.y<1.5?1:-1);
                    float2 perpendicular=float2(-d.y,d.x)/max(.001,length(d));
                    o.position.xy+=perpendicular*i.uv.x*.0005*UNITY_MATRIX_P._m11;
                }
                return o;
            }
            half4 Frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float alpha=lerp(_Color.a,1,i.outline)*TwinReveal(i.world);clip(alpha-.001);
                return half4(lerp(_Color.rgb,half3(.0056,.0056,.0056),i.outline),alpha);
            }
            ENDHLSL
        }
    }
}
