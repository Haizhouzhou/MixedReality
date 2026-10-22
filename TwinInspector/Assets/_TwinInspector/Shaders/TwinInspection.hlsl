#ifndef TWIN_INSPECTION_INCLUDED
#define TWIN_INSPECTION_INCLUDED
float _TwinAlpha, _TwinTechnical, _TwinDatum;
float4x4 _WorldToTwin;
float4 _TwinLens; // centre XYZ / radius in metres, shared between both eyes
float _TwinLensEnabled, _TwinLensVisibility;
float TwinReveal(float3 world)
{
    if(_TwinLensEnabled<.5) return 1;
    float d=distance(world,_TwinLens.xyz)/max(.01,_TwinLens.w);
    return (1-smoothstep(.8,1,d))*_TwinLensVisibility;
}
float3 TwinCoordinate(float3 world) { return mul(_WorldToTwin,float4(world,1)).xyz; }
float Contour(float height)
{
    float width=max(fwidth(height),.00001);
    float d=abs(frac(height/.5+.5)-.5)*.5;
    float fade=1-smoothstep(.035,.18,width);
    float ordinary=(1-smoothstep(width*.25,width*1.15,d))*.16*fade;
    float datum=(1-smoothstep(width*.4,width*1.4,abs(height)))*.55;
    // A level horizontal surface coincident with the datum is an area, not a
    // contour stroke. Avoid darkening that whole plane when its gradient is zero.
    return max(ordinary,datum)*smoothstep(.00001,.0001,width);
}
#endif
