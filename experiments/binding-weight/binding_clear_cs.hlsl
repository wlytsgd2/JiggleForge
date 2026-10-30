#include "binding_layout.hlsl"
Buffer<float4> CapturedPick : register(t0);
Buffer<float4> Captures : register(t1);
Texture1D<float4> IniParams : register(t120);
RWBuffer<uint> Nearest : register(u0);
RWBuffer<uint> DispatchArgs : register(u1);
[numthreads(1,1,1)]
void main(uint3 id : SV_DispatchThreadID)
{
    Nearest[0]=0xffffffffu; Nearest[1]=0xffffffffu;
    uint argsCount; DispatchArgs.GetDimensions(argsCount);
    if (argsCount < 6u) return;
    uint generation=(uint)CapturedPick[3].w, base=JF_BindingBase(generation);
    bool pending=CapturedPick[0].w>0.5 && Captures[base].x==(float)generation && Captures[base].y==1.0;
    pending=pending && all(asuint(Captures[base+1u])==asuint(IniParams[113]));
    uint vertices=(uint)max(IniParams[113].y-IniParams[114].x,0.0);
    DispatchArgs[0]=pending?(vertices+63u)/64u:0u;
    DispatchArgs[1]=1u; DispatchArgs[2]=1u;
    DispatchArgs[3]=pending?1u:0u;
    DispatchArgs[4]=1u; DispatchArgs[5]=1u;
}
