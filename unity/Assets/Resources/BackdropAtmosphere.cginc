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
float AtmosphereMeteor(float2 uv,float clock)
{
    // One brief streak in the open sky, above the left mountain slope. Keeping
    // this on the distant plate prevents foreground lasers across the fighters.
    float age=frac((clock+11)/16)*16;
    float phase=saturate(age/.85);
    float2 head=lerp(float2(.22,.845),float2(.42,.778),phase);
    float2 direction=float2(-.058,.0194);
    float2 rel=uv-head;
    float along=dot(rel,direction)/dot(direction,direction);
    float distance=abs(rel.x*direction.y-rel.y*direction.x)/length(direction);
    float core=1-smoothstep(.0002,.00075,distance);
    float halo=(1-smoothstep(0,.0022,distance))*.16;
    float tail=smoothstep(-.035,.025,along)*(1-smoothstep(0,1,along));
    float fade=smoothstep(0,.08,age)*(1-smoothstep(.48,.85,age));
    return (core+halo)*tail*fade*.72;
}
#endif
