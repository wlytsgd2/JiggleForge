#include <d3d11.h>
#include <wrl/client.h>
#include <filesystem>
#include <fstream>
#include <vector>
#include <string>
#include <iostream>
#include <cmath>
#include <cstring>
#include <stdexcept>
#include <array>
#undef near
#pragma comment(lib,"d3d11.lib")
using Microsoft::WRL::ComPtr;
using Bytes=std::vector<char>;
void check(HRESULT hr,const char* what){if(FAILED(hr))throw std::runtime_error(std::string(what)+" HRESULT "+std::to_string(hr));}
struct B {ComPtr<ID3D11Buffer>b;ComPtr<ID3D11ShaderResourceView>s;ComPtr<ID3D11UnorderedAccessView>u;UINT bytes;};
struct GPU {
 ComPtr<ID3D11Device>d;ComPtr<ID3D11DeviceContext>c;std::filesystem::path dir;unsigned tests=0;
 GPU(const std::filesystem::path& p,bool hardware):dir(p){D3D_FEATURE_LEVEL fl;check(D3D11CreateDevice(nullptr,hardware?D3D_DRIVER_TYPE_HARDWARE:D3D_DRIVER_TYPE_WARP,nullptr,0,nullptr,0,D3D11_SDK_VERSION,&d,&fl,&c),"device");}
 B buffer(const void* data,UINT bytes,UINT stride=16,bool rw=false,bool structured=false){B b;b.bytes=bytes;D3D11_BUFFER_DESC bd{};bd.ByteWidth=bytes;bd.BindFlags=D3D11_BIND_SHADER_RESOURCE|(rw?D3D11_BIND_UNORDERED_ACCESS:0);if(structured){bd.MiscFlags=D3D11_RESOURCE_MISC_BUFFER_STRUCTURED;bd.StructureByteStride=stride;}D3D11_SUBRESOURCE_DATA init{};init.pSysMem=data;check(d->CreateBuffer(&bd,&init,&b.b),"buffer");D3D11_SHADER_RESOURCE_VIEW_DESC sd{};sd.ViewDimension=D3D11_SRV_DIMENSION_BUFFER;sd.Format=structured?DXGI_FORMAT_UNKNOWN:stride==4?DXGI_FORMAT_R32_UINT:DXGI_FORMAT_R32G32B32A32_FLOAT;sd.Buffer.NumElements=bytes/stride;check(d->CreateShaderResourceView(b.b.Get(),&sd,&b.s),"srv");if(rw){D3D11_UNORDERED_ACCESS_VIEW_DESC ud{};ud.ViewDimension=D3D11_UAV_DIMENSION_BUFFER;ud.Format=sd.Format;ud.Buffer.NumElements=sd.Buffer.NumElements;check(d->CreateUnorderedAccessView(b.b.Get(),&ud,&b.u),"uav");}return b;}
 B floats(std::vector<float>v,bool rw=false){return buffer(v.data(),UINT(v.size()*4),16,rw);}
 std::vector<float> read(B& b){D3D11_BUFFER_DESC bd{};b.b->GetDesc(&bd);bd.Usage=D3D11_USAGE_STAGING;bd.BindFlags=0;bd.CPUAccessFlags=D3D11_CPU_ACCESS_READ;bd.MiscFlags=0;bd.StructureByteStride=0;ComPtr<ID3D11Buffer>s;check(d->CreateBuffer(&bd,nullptr,&s),"stage");c->CopyResource(s.Get(),b.b.Get());D3D11_MAPPED_SUBRESOURCE m{};check(c->Map(s.Get(),0,D3D11_MAP_READ,0,&m),"read");std::vector<float>v(b.bytes/4);memcpy(v.data(),m.pData,b.bytes);c->Unmap(s.Get(),0);return v;}
 void update(B&b,const std::vector<float>&v){if(v.size()*4!=b.bytes)throw std::runtime_error("update size");c->UpdateSubresource(b.b.Get(),0,nullptr,v.data(),0,0);}
 void dispatch(const char* name,UINT groups=1){std::ifstream f(dir/(std::string(name)+".cso"),std::ios::binary|std::ios::ate);if(!f)throw std::runtime_error(name);auto n=f.tellg();Bytes code(static_cast<size_t>(n));f.seekg(0);f.read(code.data(),n);ComPtr<ID3D11ComputeShader>s;check(d->CreateComputeShader(code.data(),code.size(),nullptr,&s),"cs");c->CSSetShader(s.Get(),nullptr,0);c->Dispatch(groups,1,1);ID3D11UnorderedAccessView*nu[8]{};c->CSSetUnorderedAccessViews(0,8,nu,nullptr);ID3D11ShaderResourceView*ns[120]{};c->CSSetShaderResources(0,120,ns);}
 void srv(UINT slot,B& b){auto s=b.s.Get();c->CSSetShaderResources(slot,1,&s);}
 void uav(UINT slot,B& b){auto u=b.u.Get();c->CSSetUnorderedAccessViews(slot,1,&u,nullptr);}
 void close(float a,float b,const char* msg){if(!std::isfinite(a)||std::abs(a-b)>2e-6)throw std::runtime_error(std::string(msg)+" actual="+std::to_string(a)+" expected="+std::to_string(b));tests++;}
};
struct Blend {float w[4];UINT ids[4];};
struct Vertex {float p[3],n[3],t[4];};
int wmain(int argc,wchar_t**argv){try{
 if(argc!=4)return 2;GPU g(argv[1],std::wstring(argv[2])==L"hardware");
 std::vector<float>ini(121*4);ini[80*4+2]=ini[80*4+3]=1000;ini[81*4]=1;ini[81*4+2]=1.f/60;
 ini[113*4]=101;ini[113*4+1]=202;ini[113*4+2]=303;
 D3D11_TEXTURE1D_DESC td{};td.Width=121;td.MipLevels=1;td.ArraySize=1;td.Format=DXGI_FORMAT_R32G32B32A32_FLOAT;td.BindFlags=D3D11_BIND_SHADER_RESOURCE;D3D11_SUBRESOURCE_DATA init{};init.pSysMem=ini.data();ComPtr<ID3D11Texture1D>it;ComPtr<ID3D11ShaderResourceView>is;check(g.d->CreateTexture1D(&td,&init,&it),"ini");check(g.d->CreateShaderResourceView(it.Get(),nullptr,&is),"ini view");auto isp=is.Get();g.c->CSSetShaderResources(120,1,&isp);
 auto updateIni=[&](){g.c->UpdateSubresource(it.Get(),0,nullptr,ini.data(),0,0);};
 std::vector<float>packet(44);packet[0]=0;packet[1]=.5;packet[2]=1;packet[3]=1;packet[7]=50;packet[20+2]=1;packet[20+3]=1;packet[24]=1;packet[27]=1;packet[28+1]=1;packet[31]=1;for(int k=0;k<4;k++)packet[40+k]=ini[113*4+k];
 auto frame=g.floats(packet,true),controller=g.floats(std::vector<float>(8),true),capture=g.floats(std::vector<float>(36),true),context=g.floats(std::vector<float>(4),true),side=g.floats(std::vector<float>(5120*4),true);
 auto input=[&](){g.srv(0,frame);g.uav(0,controller);g.uav(1,capture);g.uav(2,context);g.uav(3,side);g.dispatch("update_input_cs");};
 input();auto cap=g.read(capture);g.close(cap[3],1,"freeze valid");UINT generation=UINT(cap[15]),base=(generation%1024)*20;auto values=g.read(side);g.close(values[base+1],1,"pending");
 const Blend blend[]={{ { .8f,.2f,0,0},{1,0,0,0}},{{.3f,.2f,.5f,0},{1,0,9,9}},{{1,0,0,0},{9,0,0,0}},{{.4f,.4f,.2f,0},{1,1,0,0}},{{1,0,0,0},{0,0,0,0}}};
 const Vertex vertices[]={{{0,0,0},{0,0,1},{1,0,0,1}},{{.01f,0,0},{0,0,1},{1,0,0,1}},{{.02f,0,0},{0,0,1},{1,0,0,1}},{{.03f,0,0},{0,0,1},{1,0,0,1}},{{.04f,0,0},{0,0,1},{1,0,0,1}}};
 auto bs=g.buffer(blend,sizeof(blend),32,false,true),pos=g.buffer(vertices,sizeof(vertices),40,false,true);
 UINT zeros[]={0xffffffff,0xffffffff};auto near=g.buffer(zeros,8,4,true);
 std::vector<float>model(116);model[0]=model[5]=model[10]=model[15]=1;D3D11_BUFFER_DESC cbd{};cbd.ByteWidth=116*4;cbd.BindFlags=D3D11_BIND_CONSTANT_BUFFER;D3D11_SUBRESOURCE_DATA cd{};cd.pSysMem=model.data();ComPtr<ID3D11Buffer>cb;check(g.d->CreateBuffer(&cbd,&cd,&cb),"model");auto cbp=cb.Get();g.c->CSSetConstantBuffers(0,1,&cbp);
 auto resolve=[&](){g.uav(0,near);g.dispatch("binding_clear_cs");for(const char* name:{"binding_distance_cs","binding_index_cs"}){g.srv(0,capture);g.srv(1,side);g.srv(2,pos);g.srv(3,bs);g.uav(0,near);g.dispatch(name);}g.srv(0,capture);g.srv(1,near);g.srv(2,bs);g.uav(0,side);g.dispatch("binding_resolve_cs");};
 resolve();values=g.read(side);g.close(values[base+1],2,"resolved");g.close(values[base+2],0,"nearest vertex");g.close(values[base+8],1,"captured bone1");g.close(values[base+9],0,"bone0 valid");g.close(values[base+12],.8f,"weight1");g.close(values[base+13],.2f,"weight0");
 std::vector<float>p(20);auto positions=g.floats(p),out=g.floats(std::vector<float>(20),true);ini[115*4]=float(generation*2+1);updateIni();
 auto gate=[&](){g.srv(0,positions);g.srv(79,bs);g.srv(81,side);g.uav(0,out);g.dispatch("gate_cs");return g.read(out);};
 auto result=gate();const float expected[]={1,.5f,0,1,1};for(UINT i=0;i<5;i++){g.close(result[i*4+3],expected[i],"raw weight sum");g.close(result[i*4],.03f*expected[i],"field scaled at anchor");}
 // Releasing does not lose the frozen set; the packed held bit is irrelevant.
 ini[115*4]=float(generation*2);updateIni();auto released=gate();for(UINT i=0;i<20;i++)g.close(released[i],result[i],"released freeze");
 // Metadata / generation / pending / moved origin fail closed, no old-distance fallback.
 for(UINT k=0;k<4;k++){ini[113*4+k]+=1;updateIni();auto v=gate();for(float x:v)g.close(x,0,"stream identity isolation");ini[113*4+k]-=1;}updateIni();
 ini[115*4]=float((generation+1024)*2);updateIni();for(float x:gate())g.close(x,0,"generation tag isolation");ini[115*4]=float(generation*2);updateIni();
 auto pending=values;pending[base+1]=1;g.update(side,pending);for(float x:gate())g.close(x,0,"pending gate");g.update(side,values);
 // The captured IDs are a set. Duplicating them cannot double the multiplier.
 auto duplicate=values;duplicate[base+10]=1;duplicate[base+14]=.1f;g.update(side,duplicate);auto dup=gate();for(UINT i=0;i<20;i++)g.close(dup[i],result[i],"duplicate capture IDs");g.update(side,values);
 auto moved=values;moved[base+16]=.051f;g.update(side,moved);for(float x:gate())g.close(x,0,"object origin isolation");g.update(side,values);
 // Real runtime motion solver: use the configured maximum, with no bone-only cap.
 auto parameters=g.floats({1,2,.12f,.7f,1,2,.75f,.5f,.02f,10,.84f,5,0,5,.02f,0,.15f,1,-1,1});
 auto state=g.floats(std::vector<float>(28),true);
 for(int f=0;f<80;f++){ini[80*4]=900;ini[80*4+1]=900;updateIni();input();g.srv(0,controller);g.srv(1,capture);g.srv(2,parameters);g.srv(3,context);g.uav(0,state);g.dispatch("update_motion_cs");auto m=g.read(state);if(std::hypot(m[0],m[1],m[2])>.500001)throw std::runtime_error("configured motion cap exceeded");g.tests++;}
 auto finalState=g.read(state);g.close(finalState[3],1,"motion active");
 // Holding with a new hover packet does not rewrite the frozen binding record.
 g.close(g.read(side)[base+1],2,"held binding persists");
 auto readFile=[](const std::filesystem::path&p){std::ifstream f(p,std::ios::binary|std::ios::ate);if(!f)throw std::runtime_error("Missing real blend fixture");auto n=f.tellg();Bytes v(static_cast<size_t>(n));f.seekg(0);f.read(v.data(),n);return v;};
 unsigned realVertices=0;
 for(const char* mesh:{"native","mod"}){
  auto bytes=readFile(std::filesystem::path(argv[3])/mesh/"blend.bin");UINT n=UINT(bytes.size()/32);auto data=reinterpret_cast<const Blend*>(bytes.data());
  UINT chosen=0;for(UINT i=0;i<n;i++){unsigned nonzero=0;for(float w:data[i].w)if(w>1e-4)nonzero++;if(nonzero>1){chosen=i;break;}}
  auto realBlend=g.buffer(bytes.data(),UINT(bytes.size()),32,false,true),realPos=g.floats(std::vector<float>(n*4)),realOut=g.floats(std::vector<float>(n*4),true);
  auto frozen=values;for(UINT k=0;k<4;k++){frozen[base+8+k]=float(data[chosen].ids[k]);frozen[base+12+k]=data[chosen].w[k];}g.update(side,frozen);
  g.srv(0,realPos);g.srv(79,realBlend);g.srv(81,side);g.uav(0,realOut);g.dispatch("gate_cs",(n+63)/64);auto resultReal=g.read(realOut);
  for(UINT i=0;i<n;i++){float expectedWeight=0;for(UINT k=0;k<4;k++){bool hit=false;for(UINT j=0;j<4;j++)if(data[chosen].w[j]>1e-8&&data[i].ids[k]==data[chosen].ids[j])hit=true;if(hit&&data[i].w[k]>1e-8)expectedWeight+=data[i].w[k];}g.close(resultReal[i*4+3],expectedWeight,"real Mod/native weights");g.close(resultReal[i*4],.03f*expectedWeight,"real Mod/native deformation");}
  auto after=g.read(realBlend);if(memcmp(after.data(),bytes.data(),bytes.size()))throw std::runtime_error("real blend modified");realVertices+=n;
 }
 g.update(side,values);
 auto blendAfter=g.read(bs),positionAfter=g.read(pos);if(memcmp(blendAfter.data(),blend,sizeof(blend))||memcmp(positionAfter.data(),vertices,sizeof(vertices)))throw std::runtime_error("source geometry modified");
 g.uav(0,frame);g.dispatch("reset_frame_pick_cs");auto reset=g.read(frame);for(UINT i=4;i<44;i++)g.close(reset[i],0,"full eleven-record reset");
 std::cout<<"{\"pass\":true,\"checks\":"<<g.tests<<",\"realVertices\":"<<realVertices<<",\"realInputFreeze\":true,\"nearestBinding\":true,\"releasePreservesBinding\":true,\"identityIsolation\":true,\"maxOffset\":0.5,\"sourceGeometryUnchanged\":true}"<<std::endl;
 return 0;
}catch(const std::exception&e){std::cerr<<e.what()<<std::endl;return 1;}}
