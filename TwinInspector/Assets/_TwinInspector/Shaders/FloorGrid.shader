Shader "TwinInspector/FloorGrid"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+2" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "TwinInspection.hlsl"
            struct A { float3 vertex:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 position:SV_POSITION; float3 world:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            V Vert(A i) { V o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.world=TransformObjectToWorld(i.vertex); o.position=TransformWorldToHClip(o.world); return o; }
            half4 Frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 grid=TwinCoordinate(i.world).xy; float2 width=max(fwidth(grid),.0001);
                float2 d=abs(frac(grid+.5)-.5);
                float2 tickLine=1-smoothstep(width*.3,width*1.2,d);
                float crosshair=max(tickLine.x*(1-smoothstep(.04,.065,d.y)),tickLine.y*(1-smoothstep(.04,.065,d.x)));
                crosshair*=1-smoothstep(.025,.13,max(width.x,width.y));
                float2 major=abs(frac(grid/10+.5)-.5)*10;
                float2 majorLine=1-smoothstep(width*.4,width*1.3,major);
                float alpha=max(crosshair*.38,max(majorLine.x,majorLine.y)*.58)*TwinReveal(i.world);
                clip(alpha-.001); return half4(.0056,.0056,.0056,alpha);
            }
            ENDHLSL
        }
    }
}
