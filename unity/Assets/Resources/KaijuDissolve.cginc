#ifndef KAIJU_DISSOLVE_INCLUDED
#define KAIJU_DISSOLVE_INCLUDED
// Analytic waves are mirrored by MonsterDissolve.Field so a mote starts at
// the same surface band that disappears. No frame time or unstable hash noise.
float KaijuDissolveField(float3 p,float2 bounds) {
 float height=saturate((p.y-bounds.x)/max(.01,bounds.y));
 float large=.5+.5*sin(p.x*11.7+sin(p.z*7.3)*2+p.y*4.7);
 float small=.5+.5*sin(p.z*29.1+p.x*21.3+sin(p.y*18.7));
 return .02+height*.64+large*.22+small*.10;
}
float KaijuDissolveEdge(float3 p,float2 bounds,float amount) {
 if(amount<=0)return 0;
 float distance=KaijuDissolveField(p,bounds)-amount;
 clip(distance);
 return (1-smoothstep(0,.065,distance))*smoothstep(0,.04,amount);
}
#endif
