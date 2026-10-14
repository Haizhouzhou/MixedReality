Shader "TwinInspector/Annotation"
{
    Properties { _Color("Type colour", Color) = (1,0,0,1) _Selected("Selected", Float) = 0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float _Selected;
            CBUFFER_END
            struct A { float3 vertex:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 position:SV_POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            V Vert(A i) { V o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.position=TransformObjectToHClip(i.vertex); o.uv=i.uv; return o; }
            half4 Frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float r=length(i.uv-.5); clip(.5-r);
                float ring=step(.475,r);
                half3 ink=half3(.0056,.0056,.0056), paper=half3(.888,.879,.839);
                half3 fill=lerp(_Color.rgb,ink,_Selected);
                return half4(lerp(fill,lerp(ink,paper,_Selected),ring),1);
            }
            ENDHLSL
        }
    }
}
