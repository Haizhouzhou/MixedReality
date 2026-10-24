Shader "TwinInspector/ScanSurface"
{
    Properties { _ScanFloor("Floor height (world)",Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            // Separate alpha keeps the passthrough composite correct (alpha = a + dst·(1 − a)).
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _ScanFloor;
            CBUFFER_END
            struct A { float3 vertex:POSITION; float3 normal:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 position:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            V Vert(A i)
            {
                V o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.world=TransformObjectToWorld(i.vertex); o.normal=TransformObjectToWorldNormal(i.normal); o.position=TransformWorldToHClip(o.world);
                return o;
            }
            half4 Frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 n=normalize(i.normal); float height=i.world.y-_ScanFloor;
                // Colours follow AutoAligner's evidence: walls above the furniture band (blue), floor (green), the rest (grey).
                bool wall=abs(n.y)<.3, floorLike=abs(n.y)>.9 && abs(height)<.3;
                half4 colour=wall&&height>.8?half4(.10,.55,.95,.38):floorLike?half4(.20,.80,.45,.24):half4(.75,.75,.72,.14);
                // A 25 cm world grid shows the scanned surface's shape and extent; the axis along the normal draws no lines.
                float3 cell=abs(frac(i.world/.25+.5)-.5)/max(fwidth(i.world/.25),1e-4), an=abs(n);
                float d=1e3;
                if(an.x<.7) d=min(d,cell.x);
                if(an.y<.7) d=min(d,cell.y);
                if(an.z<.7) d=min(d,cell.z);
                float rule=1-saturate(d-.5); // "line" is an HLSL keyword
                return half4(colour.rgb,saturate(colour.a+rule*.35));
            }
            ENDHLSL
        }
    }
}
