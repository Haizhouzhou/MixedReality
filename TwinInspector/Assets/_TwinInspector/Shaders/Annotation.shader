Shader "TwinInspector/Annotation"
{
    Properties { _Color("Type colour",Color)=(1,0,0,1) _Kind("Type",Float)=0 _Selected("Selected",Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Color; float _Selected,_Kind;
            CBUFFER_END
            struct A { float3 vertex:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 position:SV_POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            V Vert(A i) { V o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.position=TransformObjectToHClip(i.vertex); o.uv=i.uv; return o; }
            float segment(float2 p,float2 a,float2 b) { float2 ab=b-a; return length(p-a-ab*saturate(dot(p-a,ab)/dot(ab,ab))); }
            half4 Frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 p=(i.uv-.5)*2;
                float shape=length(p)-.95;
                if(_Kind>.5 && _Kind<1.5) shape=max(abs(p.x),abs(p.y))-.87;
                if(_Kind>1.5 && _Kind<2.5) shape=max(abs(p.x)*.866025+p.y*.5-.43,-p.y-.82);
                if(_Kind>2.5) shape=(abs(p.x)+abs(p.y))*.707107-.67;
                float aa=max(fwidth(shape),.002);
                float cover=1-smoothstep(-aa,aa,shape); clip(cover-.001);
                half3 ink=half3(.0056,.0056,.0056),paper=half3(.888,.879,.839);
                // Outer paper keyline and inner ink rule survive all backgrounds and selection.
                half3 colour=shape>-.035?paper:shape>-.10?ink:_Color.rgb;
                float symbol=max(abs(p.x)-.37,abs(p.y)-.07);
                if(_Kind>.5 && _Kind<1.5) symbol=min(symbol,max(abs(p.y)-.37,abs(p.x)-.07));
                if(_Kind>1.5 && _Kind<2.5) symbol=min(segment(p,float2(-.3,-.43),float2(.3,-.43)),min(segment(p,float2(.3,-.43),float2(0,.15)),segment(p,float2(0,.15),float2(-.3,-.43))))-.055;
                if(_Kind>2.5) symbol=length(p)-.12;
                // A paper halo gives the interior ink symbol contrast even on red/blue.
                colour=lerp(colour,paper,1-smoothstep(.025,.045,symbol));
                colour=lerp(colour,ink,1-smoothstep(-aa,aa,symbol));
                return half4(colour,cover);
            }
            ENDHLSL
        }
    }
}
