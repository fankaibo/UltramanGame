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
#endif
