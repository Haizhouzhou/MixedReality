Shader "TwinInspector/FloorGrid"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent" }
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
            struct A { float3 vertex:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 position:SV_POSITION; float2 grid:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            V Vert(A i) { V o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.position=TransformObjectToHClip(i.vertex); o.grid=i.vertex.xz; return o; }
            half4 Frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 width=max(fwidth(i.grid),.0001);
                float2 d=abs(frac(i.grid+.5)-.5);
                float thin=1-saturate(min(d.x/width.x,d.y/width.y)-.001/max(width.x,width.y));
                float2 major=abs(frac(i.grid/10+.5)-.5)*10;
                float thick=1-saturate(min(major.x/width.x,major.y/width.y)-.002/max(width.x,width.y));
                return half4(.0056,.0056,.0056,max(thin*.25,thick*.5));
            }
            ENDHLSL
        }
    }
}
