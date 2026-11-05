#define wmain previous_test_entry
#include "../binding-weight/tests/gpu.cpp"
#undef wmain

template<class T> T take(std::ifstream& f){T value;f.read(reinterpret_cast<char*>(&value),sizeof(value));if(!f)throw std::runtime_error("Fixture truncated");return value;}
B typed(GPU&g,const void* data,UINT bytes,DXGI_FORMAT format,UINT stride){
 B b=g.buffer(data,bytes,stride);b.s.Reset();D3D11_SHADER_RESOURCE_VIEW_DESC d{};d.Format=format;d.ViewDimension=D3D11_SRV_DIMENSION_BUFFER;d.Buffer.NumElements=bytes/stride;check(g.d->CreateShaderResourceView(b.b.Get(),&d,&b.s),"typed SRV");return b;
}
int wmain(int argc,wchar_t** argv){try{
 if(argc!=3)return 2;GPU g(argv[1],std::wstring(argv[2])==L"hardware");std::ifstream file(g.dir/"weight-size-cases.bin",std::ios::binary);UINT cases=take<UINT>(file);float maxError=0;
 for(UINT test=0;test<cases;test++){
  UINT flags=take<UINT>(file),count=take<UINT>(file),offset=take<UINT>(file),seed=take<UINT>(file);float reference=take<float>(file);
  std::vector<float>model(116);for(auto&v:model)v=take<float>(file);std::vector<float>anchor(3);for(auto&v:anchor)v=take<float>(file);float expected=take<float>(file),expectedWeight=take<float>(file);
  std::vector<float>point(4),expectedField(3);for(UINT k=0;k<3;k++)point[k]=take<float>(file);for(auto&v:expectedField)v=take<float>(file);
  std::vector<Vertex>vertices(count);file.read(reinterpret_cast<char*>(vertices.data()),count*sizeof(Vertex));std::vector<Blend>blends(count+offset);file.read(reinterpret_cast<char*>(blends.data()),blends.size()*sizeof(Blend));if(!file)throw std::runtime_error("Fixture read");
  auto bs=(flags&1)?typed(g,blends.data(),UINT(blends.size()*sizeof(Blend)),DXGI_FORMAT_R32G32B32A32_UINT,16):g.buffer(blends.data(),UINT(blends.size()*sizeof(Blend)),32,false,true);
  auto ps=(flags&2)?typed(g,vertices.data(),UINT(vertices.size()*sizeof(Vertex)),DXGI_FORMAT_R32_FLOAT,4):g.buffer(vertices.data(),UINT(vertices.size()*sizeof(Vertex)),40,false,true);
  std::vector<float>ini(122*4);ini[113*4]=(flags&8)?102.f:101.f;ini[113*4+1]=float(count+offset);ini[113*4+2]=3;ini[114*4]=float(offset);ini[114*4+1]=(flags&1)?1.f:0;ini[114*4+3]=(flags&2)?1.f:0;ini[115*4]=float(seed-offset);ini[115*4+1]=(flags&16)?1.f:0;ini[115*4+2]=(flags&4)?0:1;ini[115*4+3]=reference;
  D3D11_TEXTURE1D_DESC td{};td.Width=122;td.MipLevels=1;td.ArraySize=1;td.Format=DXGI_FORMAT_R32G32B32A32_FLOAT;td.BindFlags=D3D11_BIND_SHADER_RESOURCE;D3D11_SUBRESOURCE_DATA init{};init.pSysMem=ini.data();ComPtr<ID3D11Texture1D>it;ComPtr<ID3D11ShaderResourceView>is;check(g.d->CreateTexture1D(&td,&init,&it),"ini");check(g.d->CreateShaderResourceView(it.Get(),nullptr,&is),"ini SRV");auto isp=is.Get();g.c->CSSetShaderResources(120,1,&isp);auto updateIni=[&](){g.c->UpdateSubresource(it.Get(),0,nullptr,ini.data(),0,0);};
  D3D11_BUFFER_DESC cd{};cd.ByteWidth=UINT(model.size()*4);cd.BindFlags=D3D11_BIND_CONSTANT_BUFFER;init.pSysMem=model.data();ComPtr<ID3D11Buffer>cb;check(g.d->CreateBuffer(&cd,&init,&cb),"CB");auto cbp=cb.Get();g.c->CSSetConstantBuffers(0,1,&cbp);
  std::vector<float>pick(36);for(UINT i=0;i<3;i++)pick[i]=anchor[i];pick[3]=1;pick[15]=1;
  std::vector<float>frozen(5120*4);frozen[20]=1;frozen[21]=1;frozen[24]=101;frozen[25]=float(count+offset);frozen[26]=3;
  auto capture=g.floats(pick),side=g.floats(frozen,true);UINT nearest[]={0,seed};auto near=g.buffer(nearest,8,4);std::vector<UINT>diag(32);diag[0]=1;auto diagnostic=g.buffer(diag.data(),128,4,true);
  auto resolve=[&](){g.srv(0,capture);g.srv(1,near);g.srv((flags&1)?3:2,bs);g.srv((flags&2)?5:4,ps);g.uav(0,side);g.uav(2,diagnostic);g.dispatch("cs-binding_resolve_cs");};
  resolve();auto values=g.read(side);float error=std::abs(values[23]-expected);maxError=(std::max)(maxError,error);if(!std::isfinite(values[23])||error>3e-5f)throw std::runtime_error("size case "+std::to_string(test)+" actual="+std::to_string(values[23])+" expected="+std::to_string(expected));g.tests++;
  if(flags&8){g.close(values[21],1,"stale identity stays pending");continue;}
  g.close(values[21],2,"resolved");g.close(values[22],float(seed),"seed preserved");for(UINT i=0;i<4;i++){g.close(values[28+i],float(blends[seed].ids[i]),"bone ID");g.close(values[32+i],blends[seed].w[i],"bone weight");}for(UINT i=0;i<3;i++)g.close(values[36+i],model[12+i],"origin");
  // Reads must not modify either genuine source buffer.
  auto sourceP=g.read(ps),sourceB=g.read(bs);if(memcmp(sourceP.data(),vertices.data(),vertices.size()*sizeof(Vertex))||memcmp(sourceB.data(),blends.data(),blends.size()*sizeof(Blend)))throw std::runtime_error("Source mutated");
  // Once resolved, even a changed stream must leave the captured scale frozen.
  for(auto&v:vertices)for(float&p:v.p)p*=1.7f;g.c->UpdateSubresource(ps.b.Get(),0,nullptr,vertices.data(),0,0);resolve();auto repeated=g.read(side);if(memcmp(values.data(),repeated.data(),values.size()*4))throw std::runtime_error("Frozen capture changed");g.tests++;
  auto result=g.floats(std::vector<float>(8),true);
  auto gate=[&](){g.srv((flags&1)?80:79,bs);g.srv(81,side);g.uav(0,result);g.dispatch("gate_cs");return g.read(result);};auto gated=gate();g.close(gated[0],expectedWeight,"binding weight retained");g.close(gated[1],values[23],"scale consumed");g.close(gated[2],expectedWeight,"cached binding weight");g.close(gated[3],values[23],"cached scale");
  ini[115*4+2]=0;updateIni();g.close(gate()[1],1,"size switch off");ini[115*4+2]=1;ini[115*4+1]=1;updateIni();auto disabled=gate();g.close(disabled[0],1,"bones off weight");g.close(disabled[1],1,"bones off scale");
  ini[115*4+2]=(flags&4)?0:1;ini[115*4+1]=(flags&16)?1:0;updateIni();UINT stateId=0;auto ids=g.buffer(&stateId,4,4),pointBuffer=g.floats(point);
  std::vector<float>motion(28);motion[0]=.018f;motion[1]=-.024f;motion[2]=.011f;motion[3]=1;for(UINT k=0;k<3;k++)motion[8+k]=anchor[k];motion[11]=1;motion[27]=3;
  auto state=g.floats(motion),parameters=g.floats({1,2,.12f,.7f,1,2,.75f,.1f,.02f,10,.84f,5,0,5,.02f,0,.15f,1,-1,1});
  auto draw=[&](){g.srv(0,pointBuffer);g.srv(72,ids);g.srv(75,state);g.srv(76,parameters);g.srv((flags&1)?80:79,bs);g.srv(81,side);g.uav(0,result);g.dispatch("draw_gate_cs");return g.read(result);};auto rendered=draw();for(UINT k=0;k<3;k++)g.close(rendered[k],expectedField[k],"original scaled field output");g.close(rendered[3],.12f*values[23],"scaled radius with expanded early cull");g.close(rendered[4],.05f,"all motion decodes cap legacy .1 to .05");
  for(UINT bypass:{112*4,112*4+2,116*4}){ini[bypass]=1;updateIni();auto v=draw();for(UINT k=0;k<4;k++)g.close(v[k],0,"F7/draw/hidden bypass");ini[bypass]=0;}updateIni();
 }
 // Execute both shipped update stages; do not infer the .05 cap just from text.
 std::vector<float>ini(122*4);ini[115*4+1]=1;D3D11_TEXTURE1D_DESC td{};td.Width=122;td.MipLevels=1;td.ArraySize=1;td.Format=DXGI_FORMAT_R32G32B32A32_FLOAT;td.BindFlags=D3D11_BIND_SHADER_RESOURCE;D3D11_SUBRESOURCE_DATA ti{};ti.pSysMem=ini.data();ComPtr<ID3D11Texture1D>it;ComPtr<ID3D11ShaderResourceView>is;check(g.d->CreateTexture1D(&td,&ti,&it),"motion ini");check(g.d->CreateShaderResourceView(it.Get(),nullptr,&is),"motion ini view");auto isp=is.Get();g.c->CSSetShaderResources(120,1,&isp);
 std::vector<float>q(36);q[3]=1;q[4]=1;q[7]=1;q[9]=1;q[11]=1;q[15]=1;q[19]=100;q[23]=100;q[26]=1;q[27]=1;
 auto controller=g.floats({100,100,1,1,900,100,0,1}),capture=g.floats(q),context=g.floats({1000,1000,1.f/60,0}),motion=g.floats(std::vector<float>(28),true);UINT projectData[]={0,0,0,0,1,0,0,0};auto project=g.buffer(projectData,sizeof(projectData),16);
 for(float requested:{.02f,.1f,.5f})for(bool projectMode:{false,true})for(float bonesOff:{0.f,1.f}){
  ini[115*4+1]=bonesOff;g.c->UpdateSubresource(it.Get(),0,nullptr,ini.data(),0,0);auto params=g.floats({1,2,.12f,.7f,1,2,.75f,requested,.02f,10,.84f,5,0,5,.02f,0,.15f,1,-1,1});g.update(motion,std::vector<float>(28));
  for(UINT step=0;step<90;step++){g.srv(0,controller);g.srv(1,capture);g.srv(2,params);if(projectMode){g.srv(4,project);g.srv(5,context);}else g.srv(3,context);g.uav(0,motion);g.dispatch(projectMode?"cs-update_project_motion_cs":"cs-update_motion_cs");}
  auto m=g.read(motion);g.close(std::hypot(m[0],m[1],m[2]),(std::min)(requested,.05f),"global/project/bones modes preserve lower cap and clamp upper cap");
 }
 std::cout<<"{\"pass\":true,\"cases\":"<<cases<<",\"checks\":"<<g.tests<<",\"maxScaleError\":"<<maxError<<",\"typedStructuredOffset\":true,\"frozenCapture\":true,\"weightScaleCache\":true,\"sourceGeometryUnchanged\":true,\"originalField\":true,\"globalAndProjectMotionCap\":true,\"switchOff\":true}"<<std::endl;return 0;
 }catch(const std::exception&e){std::cerr<<e.what()<<std::endl;return 1;}}
