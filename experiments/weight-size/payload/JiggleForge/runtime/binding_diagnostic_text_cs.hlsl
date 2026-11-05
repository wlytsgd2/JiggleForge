#include "binding_layout.hlsl"
#include "binding_diagnostic_labels.hlsl"
Buffer<float4> FramePick : register(t0);
Buffer<float4> Controller : register(t1);
Buffer<float4> CapturedPick : register(t2);
Buffer<float4> Captures : register(t3);
Buffer<float4> FrozenDiagnostic : register(t4);
Buffer<uint> Scan : register(t5);
Buffer<float4> InlineCursor : register(t6);
Buffer<uint> Labels : register(t7);
Texture1D<float4> IniParams : register(t120);
RWBuffer<uint> OutputText : register(u0);
static const uint CAPACITY=768u;
void ch(inout uint c,uint x){if(c+1u<CAPACITY)OutputText[c++]=x;}
void label(inout uint c,uint start)
{
    uint count;Labels.GetDimensions(count);
    [loop]for(uint i=start;i<count&&i<start+80u;++i){uint x=Labels[i];if(x==124u||x==0u)break;ch(c,x);}
}
void number(inout uint c,uint x)
{
    uint divisor=1000000000u;bool started=false;
    [unroll]for(uint i=0u;i<10u;++i){uint digit=x/divisor;if(started||digit>0u||divisor==1u){ch(c,48u+digit);started=true;}x-=digit*divisor;divisor/=10u;}
}
uint u(float x){return (asuint(x)&0x7f800000u)!=0x7f800000u?(uint)max(round(x),0.0):0u;}
void field(inout uint c,uint name,uint x){label(c,name);number(c,x);}
void fixed3(inout uint c,float x){uint scaled=u(clamp(x,0.0,9999.999)*1000.0);number(c,scaled/1000u);ch(c,46u);ch(c,48u+(scaled/100u)%10u);ch(c,48u+(scaled/10u)%10u);ch(c,48u+scaled%10u);}
void nl(inout uint c){ch(c,10u);}
void drawLine(inout uint c,uint name,float4 draw,float4 skin)
{
    label(c,name);field(c,JFD_R,u(draw.x));field(c,JFD_SO,u(draw.y));field(c,JFD_STRIDE,u(draw.z));field(c,JFD_OFFSET,u(draw.w));field(c,JFD_TYPE,u(skin.x));field(c,JFD_SKIN,u(skin.y));nl(c);
}
[numthreads(1,1,1)]
void main(uint3 tid:SV_DispatchThreadID)
{
    uint outputCount;OutputText.GetDimensions(outputCount);if(outputCount<CAPACITY)return;
    [loop]for(uint i=0u;i<CAPACITY;++i)OutputText[i]=0u;
    uint frameCount,controllerCount,captureCount,sideCount,frozenCount,scanCount,cursorCount;
    FramePick.GetDimensions(frameCount);Controller.GetDimensions(controllerCount);CapturedPick.GetDimensions(captureCount);Captures.GetDimensions(sideCount);FrozenDiagnostic.GetDimensions(frozenCount);Scan.GetDimensions(scanCount);InlineCursor.GetDimensions(cursorCount);
    bool hover=frameCount>=13u&&FramePick[0].w>0.5;
    bool held=controllerCount>=2u&&Controller[0].z>0.5;
    bool valid=captureCount>=9u&&CapturedPick[0].w>0.5;
    uint generation=captureCount>=9u?u(CapturedPick[3].w):0u,base=JF_BindingBase(generation);
    float4 header=sideCount>=base+5u?Captures[base]:0.0;
    bool sideValid=header.x==(float)generation;
    uint status=sideValid?u(header.y):0u;
    bool frozen=frozenCount>=4u&&FrozenDiagnostic[0].x==(float)generation;
    float4 draw=frozen?FrozenDiagnostic[1]:0.0;
    float4 skin=frozen?FrozenDiagnostic[2]:0.0;
    float4 metadata=frozen?FrozenDiagnostic[3]:0.0;
    bool scanned=scanCount>=32u&&Scan[0]==generation;
    uint attempts=scanned?Scan[1]:0u,matched=scanned?Scan[2]:0u;
    uint reason=JFD_NO_PIXEL_PICK;
    if(IniParams[116].y<0.5&&IniParams[116].z<0.5)reason=JFD_SLEEP;
    else if(IniParams[117].y>0.5)reason=JFD_WARMUP;
    else if(!held)reason=hover?JFD_HOVER:JFD_NO_PIXEL_PICK;
    else if(valid)
    {
        if(status==2u)reason=JFD_BIND_OK_CHECK_RENDER;
        else if(metadata.x==0.0)
        {
            uint code=u(skin.y);
            reason=code==31u?JFD_SKIN_TYPE3_NOT_REGISTERED:code>=32u&&code<=36u?JFD_SKIN_LAYOUT_REJECTED:u(draw.x)==13u?JFD_RENDER_STRIDE_REJECTED:u(draw.x)==10u?JFD_NO_RENDER_VB:JFD_NO_BINDING_REGISTRATION;
        }
        else if(attempts==0u)reason=JFD_NO_RESOLVE_CALL;
        else if(matched==0u)reason=JFD_CAPTURE_STREAM_NOT_SEEN;
        else if(scanned)
        {
            uint flags=Scan[9];
            if((flags&1u)!=0u)reason=JFD_NO_POSITION_VIEW;
            else if((flags&2u)!=0u)reason=JFD_NO_BLEND_VIEW;
            else if((flags&4u)!=0u)reason=JFD_SCAN_LAYOUT_REJECTED;
            else if(Scan[8]!=0xffffffffu&&asfloat(Scan[8])>0.0016)reason=JFD_NEAREST_OVER_004;
            else if(Scan[11]>=2u&&Scan[11]<=4u)reason=JFD_INVALID_BONE_WEIGHTS;
            else reason=JFD_NO_NEAREST_VERTEX;
        }
    }
    if (IniParams[115].y > 0.5) reason = JFD_DISTANCE_MODE;
    uint c=0u;label(c,JFD_HEADER);nl(c);label(c,JFD_STATE);label(c,reason);nl(c);
    field(c,JFD_A,u(max(IniParams[116].y,IniParams[116].z)));field(c,JFD_P,hover?1u:0u);field(c,JFD_H,held?1u:0u);field(c,JFD_C,valid?1u:0u);field(c,JFD_G,generation);field(c,JFD_W,u(IniParams[117].y));field(c,JFD_MAP,cursorCount>0u?u(InlineCursor[0].z):0u);nl(c);
    drawLine(c,JFD_HOVER_LINE,frameCount>=13u?FramePick[11]:0.0,frameCount>=13u?FramePick[12]:0.0);
    drawLine(c,JFD_CAPTURE_LINE,draw,skin);
    field(c,JFD_BLEND,u(metadata.x));field(c,JFD_N,u(metadata.y));field(c,JFD_STATUS,status);field(c,JFD_BONES,status==2u?u(dot(Captures[base+3u]>JF_BINDING_EPSILON,1.0)):0u);nl(c);
    field(c,JFD_TRY,attempts);field(c,JFD_MATCH,matched);field(c,JFD_OTHER,scanned?Scan[3]:0u);label(c,JFD_DISTANCE);
    if(scanned&&Scan[8]!=0xffffffffu)fixed3(c,sqrt(max(asfloat(Scan[8]),0.0)));else label(c,JFD_NONE);nl(c);
    field(c,JFD_POS,scanned?Scan[4]:0u);field(c,JFD_WEIGHTS,scanned?Scan[5]:0u);field(c,JFD_FORMAT,scanned?Scan[9]:0u);field(c,JFD_RESOLVE,scanned?Scan[11]:0u);nl(c);
    field(c,JFD_SKIN1,u(IniParams[121].x));field(c,JFD_SKIN3,u(IniParams[121].y));field(c,JFD_GOOD,u(IniParams[121].z));field(c,JFD_BAD,u(IniParams[121].w));nl(c);
    OutputText[min(c,CAPACITY-1u)]=0u;
}
