/* Row-major matrices, column vectors, quaternion [x,y,z,w].
 * Same arithmetic as portable/bone-rotation.hlsl. No Three.js or IK dependency.
 */
(function(root,factory){const api=factory();if(typeof module==='object'&&module.exports)module.exports=api;else root.BoneRotationCore=api;})(typeof globalThis==='object'?globalThis:this,function(){
  'use strict';
  const add=(a,b)=>a.map((v,i)=>v+b[i]),sub=(a,b)=>a.map((v,i)=>v-b[i]),scale=(a,s)=>a.map(v=>v*s);
  const dot=(a,b)=>a.reduce((s,v,i)=>s+v*b[i],0),cross=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];
  const length=a=>Math.hypot(...a),clamp=(x,a,b)=>Math.max(a,Math.min(b,x)),identity=()=>[0,0,0,1];
  function normalize(q){const l=length(q);return l>1e-8?scale(q,1/l):identity();}
  function multiply(a,b){return [a[3]*b[0]+a[0]*b[3]+a[1]*b[2]-a[2]*b[1],a[3]*b[1]-a[0]*b[2]+a[1]*b[3]+a[2]*b[0],a[3]*b[2]+a[0]*b[1]-a[1]*b[0]+a[2]*b[3],a[3]*b[3]-dot(a.slice(0,3),b.slice(0,3))];}
  function rotate(q,v){const t=scale(cross(q.slice(0,3),v),2);return add(v,add(scale(t,q[3]),cross(q.slice(0,3),t)));}
  function angle(q){q=normalize(q);return 2*Math.atan2(length(q.slice(0,3)),Math.abs(q[3]));}
  function limit(q,maxAngle){q=normalize(q);if(q[3]<0)q=scale(q,-1);const a=2*Math.atan2(length(q.slice(0,3)),q[3]),m=clamp(maxAngle,0,Math.PI);if(a<=m||a<1e-8)return q;return [...scale(q.slice(0,3),Math.sin(m/2)/length(q.slice(0,3))),Math.cos(m/2)];}
  function fromTo(a,b){const la=length(a),lb=length(b);if(la<1e-7||lb<1e-7)return identity();a=scale(a,1/la);b=scale(b,1/lb);const c=clamp(dot(a,b),-1,1),v=cross(a,b),s=length(v);
    if(s>1e-6){const h=.5*Math.atan2(s,c);return [...scale(v,Math.sin(h)/s),Math.cos(h)];}
    if(c>=0)return identity();
    // Exactly opposite vectors have no unique swing axis: choose a stable one.
    const axis=Math.abs(a[0])<=Math.abs(a[1])&&Math.abs(a[0])<=Math.abs(a[2])?[1,0,0]:Math.abs(a[1])<=Math.abs(a[2])?[0,1,0]:[0,0,1],v2=cross(a,axis);
    return [...scale(v2,1/length(v2)),0];
  }
  function inverseAffine(m){const a=m[0],b=m[1],c=m[2],d=m[4],e=m[5],f=m[6],g=m[8],h=m[9],i=m[10],det=a*(e*i-f*h)-b*(d*i-f*g)+c*(d*h-e*g);
    if(!Number.isFinite(det)||Math.abs(det)<1e-12)throw Error('Singular reference frame');
    const r=[(e*i-f*h)/det,(c*h-b*i)/det,(b*f-c*e)/det,0,(f*g-d*i)/det,(a*i-c*g)/det,(c*d-a*f)/det,0,(d*h-e*g)/det,(b*g-a*h)/det,(a*e-b*d)/det,0,0,0,0,1];
    for(let j=0;j<3;j++)r[j*4+3]=-r[j*4]*m[3]-r[j*4+1]*m[7]-r[j*4+2]*m[11];return r;
  }
  function deltaMatrix(pivot,q){q=normalize(q);const x=rotate(q,[1,0,0]),y=rotate(q,[0,1,0]),z=rotate(q,[0,0,1]),t=sub(pivot,rotate(q,pivot));return [x[0],y[0],z[0],t[0],x[1],y[1],z[1],t[1],x[2],y[2],z[2],t[2],0,0,0,1];}
  function applyRows(rows,pivot,q){const x=rotate(q,[rows[0],rows[4],rows[8]]),y=rotate(q,[rows[1],rows[5],rows[9]]),z=rotate(q,[rows[2],rows[6],rows[10]]),t=add(pivot,rotate(q,sub([rows[3],rows[7],rows[11]],pivot)));return [x[0],y[0],z[0],t[0],x[1],y[1],z[1],t[1],x[2],y[2],z[2],t[2]];}
  /** movableSum=sum(w_i*M_i*p), fixedSum=other bones; weight=sum(w_i).
   * Rotation is absolute relative to UNMODIFIED current animation, not a delta
   * accumulated each mousemove/frame. startRotation is frozen at grab start.
   */
  function solveGrab({pivot,movableSum,fixedSum,weight,target,startRotation=identity(),maxAngle=Math.PI/4}){
    if(![...pivot,...movableSum,...fixedSum,...target,...startRotation,weight,maxAngle].every(Number.isFinite)||weight<0||maxAngle<0)throw Error('Invalid rotation input');
    const base=add(fixedSum,scale(pivot,weight)),lever=sub(movableSum,scale(pivot,weight)),desired=sub(target,base),start=normalize(startRotation);
    const swing=fromTo(rotate(start,lever),desired),candidate=normalize(multiply(swing,start)),q=weight>1e-6?limit(candidate,maxAngle):identity(),actual=add(base,rotate(q,lever));
    return {quaternion:q,actual,pivot:[...pivot],angle:angle(q),limited:angle(candidate)>maxAngle+1e-6,error:length(sub(actual,target)),radius:length(lever)};
  }
  return {add,sub,scale,dot,cross,length,identity,normalize,multiply,rotate,angle,limit,fromTo,inverseAffine,deltaMatrix,applyRows,solveGrab};
});
