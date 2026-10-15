Shader "TwinInspector/ArchitecturalEdges"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+1" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off ZTest LEqual Cull Off
            Offset -1,-1
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "TwinInspection.hlsl"
            float _TwinOutlineScale;
            struct A { float3 vertex:POSITION; float3 normal:NORMAL; float4 tangent:TANGENT; float2 uv:TEXCOORD0; float3 other:TEXCOORD1; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 position:SV_POSITION; float3 world:TEXCOORD0; float side:TEXCOORD1; float visible:TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            V Vert(A i)
            {
                V o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.world=TransformObjectToWorld(i.vertex); float3 other=TransformObjectToWorld(i.other);
                float4 a=TransformWorldToHClip(o.world),b=TransformWorldToHClip(other);
                float2 delta=(b.xy/max(.001,b.w)-a.xy/max(.001,a.w))*_ScaledScreenParams.xy*(i.uv.y<.5?1:-1);
                float lengthPixels=length(delta)*.5;
                float2 perpendicular=float2(-delta.y,delta.x)/max(length(delta),.0001);
                // Unscaled output pixels keep the apparent stroke constant. XR's
                // screen dimensions describe allocated eye textures, so undo the
                // URP eye-texture scale published by TwinStyle.
                a.xy+=perpendicular*i.uv.x*1.8*_TwinOutlineScale/_ScreenParams.xy*a.w;
                o.position=a; o.side=i.uv.x;
                float3 view=normalize(GetWorldSpaceViewDir((o.world+other)*.5));
                float d1=dot(TransformObjectToWorldNormal(i.normal),view),d2=dot(TransformObjectToWorldNormal(i.tangent.xyz),view);
                float silhouette=1-step(0,d1*d2);
                o.visible=max(i.color.r,silhouette)*smoothstep(1,3,lengthPixels)*step(.02,min(a.w,b.w));
                return o;
            }
            half4 Frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float alpha=(1-smoothstep(.35,1,abs(i.side)))*i.visible*_TwinTechnical*_TwinAlpha*TwinReveal(i.world);
                clip(alpha-.001); return half4(.0056,.0056,.0056,alpha);
            }
            ENDHLSL
        }
    }
}
