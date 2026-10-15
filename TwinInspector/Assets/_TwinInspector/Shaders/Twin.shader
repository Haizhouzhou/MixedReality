Shader "TwinInspector/Twin"
{
    Properties { _SoftReveal("Soft shell", Float)=0 _ZWrite("Depth write", Float)=1 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        // Transparent surfaces write depth only in the solid inner reveal. The
        // soft outer shell blends without occluding passthrough or other tiles.
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "TwinInspection.hlsl"
        CBUFFER_START(UnityPerMaterial)
        float _SoftReveal;
        CBUFFER_END
        struct A { float3 positionOS:POSITION; float3 normal:NORMAL; float2 cavity:TEXCOORD1; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct V { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; half3 normal:TEXCOORD1; half cavity:TEXCOORD2; half4 color:COLOR; UNITY_VERTEX_OUTPUT_STEREO };
        V Vert(A i)
        {
            V o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            o.world=TransformObjectToWorld(i.positionOS); o.positionCS=TransformWorldToHClip(o.world);
            o.normal=TransformObjectToWorldNormal(i.normal); o.cavity=i.cavity.x; o.color=i.color; return o;
        }
        half4 Shade(V i)
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
            float reveal=TwinReveal(i.world); clip(reveal*_TwinAlpha-.001);
            half3 colour=SRGBToLinear(i.color.rgb);
            float3 n=normalize(i.normal);
            float tone=.76+.24*abs(dot(n,normalize(float3(-.35,.8,.48))));
            half3 technical=SRGBToLinear(half3(.77,.79,.80))*tone*(1-saturate(i.cavity)*.12);
            technical=lerp(technical,half3(.0056,.0056,.0056),Contour(TwinCoordinate(i.world).z-_TwinDatum));
            return half4(lerp(colour,technical,_TwinTechnical),_TwinAlpha*reveal);
        }
        ENDHLSL
        Pass
        {
            Name "TwinSurface"
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite [_ZWrite] Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            half4 Frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float reveal=TwinReveal(i.world);
                if(_SoftReveal>.5) clip(.999-reveal); else clip(reveal-.999);
                return Shade(i);
            }
            ENDHLSL
        }
    }
}
