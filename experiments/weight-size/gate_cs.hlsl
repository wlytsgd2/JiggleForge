Texture1D<float4> IniParams : register(t120);
#include "binding_consumer.hlsl"
RWBuffer<float4> Result : register(u0);
cbuffer ObjectTransform : register(b0) { float4 Model[29]; }
[numthreads(1,1,1)]
void main(uint3 tid : SV_DispatchThreadID) {
    JF_SetBindingVertex((uint)max(IniParams[115].x,0.0),Model[3].xyz);
    float weight=JF_GetBindingMultiplier(3.0),scale=JF_LastBindingScale;
    float again=JF_GetBindingMultiplier(3.0),cachedScale=JF_LastBindingScale;
    Result[0]=float4(weight,scale,again,cachedScale);
}
