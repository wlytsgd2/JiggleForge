Buffer<uint> JiggleForgeDirectStateIndex : register(t72);
Buffer<float4> JF_MotionState : register(t75);
Buffer<float4> JF_GroupParams : register(t76);
Buffer<float4> JF_GlobalMotionState : register(t77);
Buffer<float4> JF_GlobalGroupParams : register(t78);
Texture1D<float4> IniParams : register(t120);
#include "draw_state_consumer.hlsl"
#define JF_SafeNormalize JF_MotionSafeNormalize
#define JF_MotionState JF_ValidationMotionState
#include "motion_model.hlsl"
#undef JF_MotionState
#undef JF_SafeNormalize
Buffer<float4> Point : register(t0);
RWBuffer<float4> Output : register(u0);
cbuffer ObjectTransform : register(b0) { float4 Model[29]; }
[numthreads(1,1,1)]
void main(uint3 tid:SV_DispatchThreadID) {
    JF_SetBindingVertex((uint)max(IniParams[115].x,0.0),Model[3].xyz);bool moving;float radius;
    float3 u=JF_EvaluateBoundStates(Point[0].xyz,1.0,moving,radius);
    Output[0]=float4(u,moving?radius:0.0);
    JF_GroupParameters parameters=JF_DecodeGroupParameters(JF_GroupParams[0],JF_GroupParams[1],JF_GroupParams[2],JF_GroupParams[3],JF_GroupParams[4]);
    Output[1]=float4(parameters.MaxOffset,0.0,0.0,0.0);
}
