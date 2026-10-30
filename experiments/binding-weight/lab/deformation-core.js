/* Spatial fields only, no spring/impulse/input controller. Normal reconstruction
 * uses the runtime's forward surface-difference method with an orthogonal frame.
 * Runtime source SHA256: 0FC764ED75D0411772F20B578C06D56CF42106995F9554FE98EBE7FB117421AC
 */
(function(root,factory){const common=typeof module==='object'&&module.exports,api=factory(null);if(common)module.exports=api;else root.DeformationCore=api;})(typeof globalThis==='object'?globalThis:this,function(S){
  'use strict';
  const ids={none:0,runtime:1,gaussian:2,normal:3,support:4};
  const defaults={radius:.12,falloff:1,volume:2,A:2.25,B:1,C:1/6,gain:1,maxOffset:.1,supportRadius:.015,supportThickness:'normal',supportGuess:'circle',localThickness:true};
  const add=(a,b)=>a.map((x,i)=>x+b[i]),sub=(a,b)=>a.map((x,i)=>x-b[i]),mul=(a,s)=>a.map(v=>v*s),dot=(a,b)=>a.reduce((s,v,i)=>s+v*b[i],0),cross=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];
  const length=v=>Math.hypot(...v),unit=(v,fallback=[0,0,1])=>dot(v,v)>1e-20?mul(v,1/length(v)):[...fallback];
  function clampOffset(d,maximum,gain=1){const v=mul(d,gain),l=length(v);return l>maximum&&l>0?mul(v,maximum/l):v;}
  function displacement(algorithm,x,n,p,np,d,params=defaults,supportCenter=params.supportCenter){
    const id=typeof algorithm==='string'?ids[algorithm]:algorithm,r=sub(x,p),radius=Math.max(params.radius,1e-6);
    if(id===0||dot(d,d)===0)return [0,0,0];
    if(id===1){
      const distance=length(r),q=distance/radius;if(q>=1)return [0,0,0];
      const support=Math.pow(Math.max(0,1-q*q),Math.max(params.falloff,1)),ratio=1/(4*(1-.45)),center=1.5-ratio,eps=Math.max(params.radius*.35,1e-5),e2=eps*eps,inv=1/Math.sqrt(dot(r,r)+e2),inv3=inv*inv*inv,normalization=eps/center;
      const isotropic=normalization*((1-ratio)*inv+.5*e2*inv3),directional=normalization*ratio*inv3*dot(d,r)*Math.max(params.volume,0);
      return mul(add(mul(d,isotropic),mul(r,directional)),support);
    }
    if(id===2)return mul(d,Math.exp(-.5*dot(r,r)/(radius*radius)));
    if(id===3){const N=unit(np),q=mul(sub(r,mul(N,dot(r,N))),1/radius),delta=dot(d,N),dt=sub(d,mul(N,delta)),s=.5*dot(q,q),g=Math.exp(-s),normalAmount=(1-params.B*s)*delta+params.C*dot(q,dt);
      return mul(add(sub(dt,mul(q,params.A*delta)),mul(N,normalAmount)),g);}
    if(id===4)return S.displacement(x,n,p,np,d,params,supportCenter);
    throw Error('Unknown deformation algorithm');
  }
  function position(algorithm,x,n,p,np,d,params=defaults,mask=1){return add(x,mul(displacement(algorithm,x,n,p,np,d,params),mask));}
  function normalFromField(x,n,field,radius){n=unit(n);const axis=Math.abs(n[1])<.9?[0,1,0]:[1,0,0],t=unit(cross(axis,n)),b=cross(n,t),h=Math.max(.0005,Math.min(.005,radius*.01)),c=field(x),dt=add(t,mul(sub(field(add(x,mul(t,h))),c),1/h)),db=add(b,mul(sub(field(add(x,mul(b,h))),c),1/h));
    let normal=unit(cross(dt,db),n);if(dot(normal,n)<0)normal=mul(normal,-1);return normal;
  }
  const glsl=`
vec3 jfUnit(vec3 v,vec3 fallback){float s=dot(v,v);return s>1e-20?v*inversesqrt(s):fallback;}
vec3 jfField(int algorithm,vec3 x,vec3 n,vec3 p,vec3 np,vec3 d,float radius,float falloff,float volume,vec3 abc){
  if(algorithm==0||dot(d,d)==0.0)return vec3(0.0);
  vec3 r=x-p;float a=max(radius,1e-6);
  if(algorithm==1){float q=length(r)/a;if(q>=1.0)return vec3(0.0);float support=pow(clamp(1.0-q*q,0.0,1.0),max(falloff,1.0));
    float ratio=1.0/(4.0*(1.0-.45)),center=1.5-ratio,eps=max(radius*.35,1e-5),e2=eps*eps,inv=inversesqrt(dot(r,r)+e2),inv3=inv*inv*inv,z=eps/center;
    float iso=z*((1.0-ratio)*inv+.5*e2*inv3),dir=z*ratio*inv3*dot(d,r)*max(volume,0.0);return (d*iso+r*dir)*support;}
  if(algorithm==2)return d*exp(-.5*dot(r,r)/(a*a));
  vec3 N=jfUnit(np,vec3(0.0,0.0,1.0)),q=(r-dot(r,N)*N)/a;float delta=dot(d,N);vec3 dt=d-delta*N;float s=.5*dot(q,q),g=exp(-s);
  return g*(dt-abc.x*delta*q+((1.0-abc.y*s)*delta+abc.z*dot(q,dt))*N);
}
`;
  return {ids,defaults,add,sub,mul,dot,cross,length,unit,clampOffset,displacement,position,normalFromField,glsl};
});
