#ifndef TRAINING_BACKDROP_ATMOSPHERE
#define TRAINING_BACKDROP_ATMOSPHERE
float2 AtmosphereUV(float2 uv,float clock)
{
    float heat=1-smoothstep(.12,.62,uv.y);
    return uv+float2(sin(clock*.72+uv.y*34+uv.x*8)*.0018,cos(clock*.55+uv.x*17)*.0008)*heat;
}
float AtmosphereShade(float2 uv)
{
    return 1-saturate(length(uv-.5)*1.15*.12);
}
float AtmosphereMeteor(float2 uv,float clock,float seed)
{
    float phase=frac(clock*.055+seed);
    // The matte is deliberately oversized around the camera, so this band sits
    // in its visible upper-middle sky rather than being clipped at the quad edge.
    float2 anchor=float2(phase*1.3-.16,.64-seed*.07);
    float2 direction=float2(.24,-.14);
    float2 rel=uv-anchor;
    float along=dot(rel,direction)/dot(direction,direction);
    float distance=abs(rel.x*direction.y-rel.y*direction.x)/length(direction);
    float ribbon=(1-smoothstep(0,.012,distance))*smoothstep(0,.10,along)*(1-smoothstep(.54,1,along));
    float fade=smoothstep(0,.07,phase)*(1-smoothstep(.82,1,phase));
    return ribbon*fade;
}
#endif
