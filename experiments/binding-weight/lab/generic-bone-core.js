/* No names, parents, inverse-bind matrices, anatomy labels or Three.js.
 * Inputs are the actual mesh position/weight/index arrays and live skin palettes.
 * Geometry/weight-graph pivots are HEURISTICS, not recovered skeleton anatomy.
 */
(function(root,factory){const R=typeof module==='object'&&module.exports?require('./bone-rotation-core.js'):root.BoneRotationCore;
 const api=factory(R);if(typeof module==='object'&&module.exports)module.exports=api;else root.GenericBoneCore=api;
})(typeof globalThis==='object'?globalThis:this,function(R){
 'use strict';
 const {add,sub,scale,dot,length}=R;
 const point=(m,p)=>[0,1,2].map(j=>dot(m.slice(j*4,j*4+3),p)+m[j*4+3]);
 const mul=(a,b)=>Array.from({length:16},(_,k)=>{const r=k>>2,c=k%4;let s=0;for(let j=0;j<4;j++)s+=a[r*4+j]*b[j*4+c];return s;});
 function analyze(mesh){
  const {position,weights,indices,boneCount}=mesh,n=position.length/3;
  if(!Number.isInteger(n)||weights.length!==n*4||indices.length!==n*4||!(boneCount>0))throw Error('Invalid mesh layout');
  const stats=Array.from({length:boneCount},()=>({mass:0,mass2:0,count:0,sum:[0,0,0],center:[0,0,0],axis:[0,1,0],extent:0}));
  const edgeMap=new Map(),center=[0,0,0],lo=[Infinity,Infinity,Infinity],hi=[-Infinity,-Infinity,-Infinity];
  for(let i=0;i<n;i++){
   const p=Array.from(position.subarray(i*3,i*3+3));if(!p.every(Number.isFinite))throw Error('Nonfinite mesh');
   for(let a=0;a<3;a++){center[a]+=p[a]/n;lo[a]=Math.min(lo[a],p[a]);hi[a]=Math.max(hi[a],p[a]);}
   // Merge duplicate IDs before constructing the co-weight graph.
   const ws=new Map();let total=0;
   for(let k=0;k<4;k++){const b=indices[i*4+k],w=weights[i*4+k];if(!Number.isFinite(w)||w<0||w>1||b>=boneCount)throw Error('Invalid blend');total+=w;if(w>1e-5)ws.set(b,(ws.get(b)||0)+w);}
   if(Math.abs(total-1)>.002)throw Error('Unnormalized blend');
   const entries=[...ws];for(const[b,w]of entries){const s=stats[b];s.mass+=w;s.mass2+=w*w;s.count++;for(let a=0;a<3;a++)s.sum[a]+=w*w*p[a];}
   for(let a=0;a<entries.length;a++)for(let b=a+1;b<entries.length;b++){
    const x=Math.min(entries[a][0],entries[b][0]),y=Math.max(entries[a][0],entries[b][0]),key=x+':'+y,w=entries[a][1]*entries[b][1];
    let e=edgeMap.get(key);if(!e){e={a:x,b:y,mass:0,sum:[0,0,0]};edgeMap.set(key,e);}e.mass+=w;for(let k=0;k<3;k++)e.sum[k]+=w*p[k];
   }
  }
  const diagonal=Math.max(length(sub(hi,lo)),1e-5);
  for(const s of stats)if(s.mass2>0)s.center=scale(s.sum,1/s.mass2);
  const covariance=stats.map(()=>new Array(9).fill(0));
  for(let i=0;i<n;i++)for(let k=0;k<4;k++){
   const b=indices[i*4+k],w=weights[i*4+k];if(w<1e-5)continue;
   const d=[0,1,2].map(a=>position[i*3+a]-stats[b].center[a]);for(let a=0;a<3;a++)for(let c=0;c<3;c++)covariance[b][a*3+c]+=w*w*d[a]*d[c];
  }
  for(let b=0;b<boneCount;b++){
   const s=stats[b],m=covariance[b];let a=[1,1.1,.7];
   for(let j=0;j<20;j++){const v=[0,1,2].map(r=>dot(m.slice(r*3,r*3+3),a)),l=length(v);if(l<1e-15)break;a=scale(v,1/l);}
   s.axis=a;s.extent=Math.sqrt(Math.max(0,dot(a,[0,1,2].map(r=>dot(m.slice(r*3,r*3+3),a)))/Math.max(s.mass2,1e-8)));
  }
  const adjacency=stats.map(()=>[]),edges=[];
  for(const e of edgeMap.values()){
   e.strength=e.mass/Math.sqrt(stats[e.a].mass2*stats[e.b].mass2);e.center=scale(e.sum,1/e.mass);
   if(e.strength<.0005)continue;edges.push(e);adjacency[e.a].push({bone:e.b,edge:e});adjacency[e.b].push({bone:e.a,edge:e});
  }
  // A geometry-derived anchor, NOT a biological root or the supplied skeleton root.
  const active=stats.map((s,i)=>i).filter(i=>stats[i].mass>.01),maxMass=Math.max(...stats.map(s=>s.mass),1);
  const cost=b=>dot(sub(stats[b].center,center),sub(stats[b].center,center))/(diagonal*diagonal)+.006*Math.sqrt(maxMass/Math.max(stats[b].mass,.01));
  const roots=[],parents=new Array(boneCount).fill(-1),distance=new Array(boneCount).fill(Infinity),done=new Set();
  while(active.some(b=>!done.has(b))){
   const root=active.filter(b=>!done.has(b)).sort((a,b)=>cost(a)-cost(b))[0];roots.push(root);distance[root]=0;
   for(;;){let u=-1,best=Infinity;for(const b of active)if(!done.has(b)&&distance[b]<best){u=b;best=distance[b];}if(u<0)break;
    done.add(u);for(const {bone:v,edge:e}of adjacency[u])if(!done.has(v)){
     const d=best+1/(e.strength+.01);if(d<distance[v]){distance[v]=d;parents[v]=u;}
    }
   }
  }
  return {stats,edges,adjacency,roots,parents,diagonal,center,vertexCount:n};
 }
 function binding(mesh,vertices,bary){
  const out=new Map();for(let v=0;v<vertices.length;v++)for(let k=0;k<4;k++){
   const i=vertices[v]*4+k,w=mesh.weights[i]*bary[v],b=mesh.indices[i];if(w>1e-7)out.set(b,(out.get(b)||0)+w);
  }return [...out].map(([bone,weight])=>({bone,weight})).sort((a,b)=>b.weight-a.weight);
 }
 function choose(analysis,bindings,rule='weight'){
  const mass=Math.max(...analysis.stats.map(s=>s.mass),1);
  return [...bindings].filter(b=>b.weight>=.03).sort((a,b)=>{
   const score=v=>rule==='local'?v.weight/Math.pow(Math.max(analysis.stats[v.bone].mass/mass,.02),.35):v.weight;
   return score(b)-score(a)||a.bone-b.bone;
  })[0]?.bone??null;
 }
 function group(analysis,bone,branch=true){
  const set=new Set([bone]);if(branch){let changed=true;while(changed){changed=false;for(let b=0;b<analysis.parents.length;b++)if(!set.has(b)&&set.has(analysis.parents[b])){set.add(b);changed=true;}}}
  return set;
 }
 function geometryPivot(analysis,bone,method='seam'){
  const anchor=analysis.parents[bone],edge=anchor>=0?analysis.adjacency[bone].find(e=>e.bone===anchor)?.edge:null;
  if(method==='centroid'||!edge)return {point:[...analysis.stats[bone].center],anchor:bone,confidence:.15,source:'centroid',note:edge?'权重质心对照，不是关节中心':'没有可靠的共权重边界；使用质心估计'};
  return {point:[...edge.center],anchor,confidence:Math.min(.7,.2+edge.strength),source:'seam',note:'共权重交界估计；联动图不是实际父子骨架'};
 }
 // Symmetric Jacobi eigensolver: also exposes the OBSERVED rank before regularization.
 function eigenSymmetric(input,n){
  const a=input.map(r=>[...r]),v=Array.from({length:n},(_,i)=>Array.from({length:n},(_,j)=>i===j?1:0));
  for(let it=0;it<160;it++){
   let p=0,q=1,largest=0;for(let i=0;i<n;i++)for(let j=i+1;j<n;j++)if(Math.abs(a[i][j])>largest){largest=Math.abs(a[i][j]);p=i;q=j;}
   if(largest<1e-13)break;const angle=.5*Math.atan2(2*a[p][q],a[q][q]-a[p][p]),c=Math.cos(angle),s=Math.sin(angle);
   const pp=a[p][p],qq=a[q][q],pq=a[p][q];
   a[p][p]=c*c*pp-2*c*s*pq+s*s*qq;a[q][q]=s*s*pp+2*c*s*pq+c*c*qq;a[p][q]=a[q][p]=0;
   for(let k=0;k<n;k++)if(k!==p&&k!==q){const x=a[k][p],y=a[k][q];a[k][p]=a[p][k]=c*x-s*y;a[k][q]=a[q][k]=s*x+c*y;}
   for(let k=0;k<n;k++){const x=v[k][p],y=v[k][q];v[k][p]=c*x-s*y;v[k][q]=s*x+c*y;}
  }return {values:a.map((r,i)=>r[i]),vectors:v};
 }
 function fitPivot(frames,bone,anchor,prior,scaleHint=1){
  if(anchor===bone||frames.length<3)return {valid:false,rank:0,note:'缺少两个不同骨骼的多帧相对运动'};
  const ata=Array.from({length:6},()=>new Array(6).fill(0)),atb=new Array(6).fill(0),rows=[];
  for(const frame of frames){const b=frame[bone],a=frame[anchor];if(!b||!a)continue;
   for(let r=0;r<3;r++){const row=[...b.slice(r*4,r*4+3),...a.slice(r*4,r*4+3).map(x=>-x)],rhs=a[r*4+3]-b[r*4+3];
    if(![...row,rhs].every(Number.isFinite))return {valid:false,rank:0,note:'矩阵数据无效'};rows.push({row,rhs});
    for(let i=0;i<6;i++){atb[i]+=row[i]*rhs;for(let j=0;j<6;j++)ata[i][j]+=row[i]*row[j];}
   }
  }
  const e=eigenSymmetric(ata,6),max=Math.max(...e.values,1e-12),rank=e.values.filter(x=>x>max*1e-6).length,lambda=max*1e-8,prior6=[...prior,...prior],rhs=atb.map((x,i)=>x+lambda*prior6[i]),solution=new Array(6).fill(0);
  for(let k=0;k<6;k++){let projection=0;for(let i=0;i<6;i++)projection+=e.vectors[i][k]*rhs[i];projection/=Math.max(e.values[k],0)+lambda;for(let i=0;i<6;i++)solution[i]+=e.vectors[i][k]*projection;}
  const rms=Math.sqrt(rows.reduce((sum,r)=>sum+Math.pow(dot(r.row,solution)-r.rhs,2),0)/Math.max(rows.length/3,1));
  const plausible=length(sub(solution.slice(0,3),prior))<scaleHint&&length(sub(solution.slice(3),prior))<scaleHint;
  const valid=rank>=5&&rms<scaleHint*.004&&plausible;
  return {valid,rank,rms,point:solution.slice(0,3),anchorPoint:solution.slice(3),anchor,
   confidence:valid?(rank===6?.95:.55):0,note:rank<5?'相对运动不足，支点不可确定':!valid?'拟合残差或位置异常，拒绝此支点':rank===5?'单轴运动：沿轴位置依赖几何先验，未唯一确定':'多轴相对运动：共同点拟合有效'};
 }
 function pivot(analysis,bone,method,frames,palette){
  const g=geometryPivot(analysis,bone,method==='centroid'?'centroid':'seam');
  if(method==='motion'){
   const fit=fitPivot(frames||[],bone,g.anchor,g.point,analysis.diagonal);
   if(fit.valid)return {...fit,source:'motion',world:point(palette[g.anchor],fit.anchorPoint)};
   return {...g,source:'seam-fallback',fit,world:point(palette[g.anchor],g.point),note:fit.note+'；回退权重几何估计'};
  }return {...g,world:point(palette[g.anchor],g.point)};
 }
 function sums(mesh,palette,vertices,bary,set){
  let movable=[0,0,0],fixed=[0,0,0],weight=0;
  for(let v=0;v<vertices.length;v++){const id=vertices[v],p=Array.from(mesh.position.subarray(id*3,id*3+3));
   for(let k=0;k<4;k++){const i=id*4+k,w=mesh.weights[i]*bary[v];if(!w)continue;const b=mesh.indices[i],q=scale(point(palette[b],p),w);
    if(set.has(b)){movable=add(movable,q);weight+=w;}else fixed=add(fixed,q);
   }
  }return {movable,fixed,weight,anchor:add(movable,fixed)};
 }
 function solve({mesh,palette,vertices,bary,set,pivot,target,maxAngle=Math.PI/4,mode='rotate'}){
  const s=sums(mesh,palette,vertices,bary,set);
  if(mode==='translate'){
   const d=s.weight>.02?scale(sub(target,s.anchor),1/s.weight):[0,0,0],cap=mesh.translationCap||.3,l=length(d),delta=l>cap?scale(d,cap/l):d;
   const out=palette.map((m,b)=>{const n=[...m];if(set.has(b)){n[3]+=delta[0];n[7]+=delta[1];n[11]+=delta[2];}return n;});
   return {palette:out,actual:add(s.anchor,scale(delta,s.weight)),angle:0,weight:s.weight,error:length(sub(add(s.anchor,scale(delta,s.weight)),target)),limited:l>cap};
  }
  const solution=R.solveGrab({pivot,movableSum:s.movable,fixedSum:s.fixed,weight:s.weight,target,maxAngle});
  const out=palette.map((m,b)=>set.has(b)?[...R.applyRows(m.slice(0,12),pivot,solution.quaternion),...m.slice(12)]:[...m]);
  return {...solution,palette:out,weight:s.weight};
 }
 function skin(mesh,palette){
  const out=new Float32Array(mesh.position.length);
  for(let i=0;i<out.length/3;i++){const x=mesh.position[i*3],y=mesh.position[i*3+1],z=mesh.position[i*3+2];let a=0,b=0,c=0;
   for(let k=0;k<4;k++){const w=mesh.weights[i*4+k];if(!w)continue;const m=palette[mesh.indices[i*4+k]];
    a+=w*(m[0]*x+m[1]*y+m[2]*z+m[3]);b+=w*(m[4]*x+m[5]*y+m[6]*z+m[7]);c+=w*(m[8]*x+m[9]*y+m[10]*z+m[11]);}
   out[i*3]=a;out[i*3+1]=b;out[i*3+2]=c;
  }return out;
 }
 return {analyze,binding,choose,group,geometryPivot,fitPivot,pivot,sums,solve,skin,point,mul,eigenSymmetric};
});
