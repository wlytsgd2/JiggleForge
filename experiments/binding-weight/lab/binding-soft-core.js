/* Pure binding eligibility + the existing JiggleForge spatial field.
 * No bone matrices, hierarchy, anatomy, pivot, graph or time integration.
 * positions are the UNDEFORMED current skinned positions, in one coordinate frame.
 */
(function(root,factory){const api=factory(typeof module==='object'&&module.exports?require('./deformation-core.js'):root.DeformationCore);
 if(typeof module==='object'&&module.exports)module.exports=api;else root.BindingSoftCore=api;
})(typeof globalThis==='object'?globalThis:this,function(D){
 'use strict';
 function merged(ids,weights){const out=new Map();for(let k=0;k<ids.length;k++){
  const b=ids[k],w=weights[k];if(!Number.isInteger(b)||b<0||!Number.isFinite(w)||w<0)throw Error('Invalid binding');
  if(w>1e-8)out.set(b,(out.get(b)||0)+w);
 }return out;}
 function frequencies(weights,indices){
  if(weights.length!==indices.length||weights.length%4)throw Error('Invalid blend layout');
  const count=new Map(),n=weights.length/4;
  for(let i=0;i<n;i++){const ws=merged(indices.subarray(i*4,i*4+4),weights.subarray(i*4,i*4+4));for(const[b,w]of ws)if(w>1e-5)count.set(b,(count.get(b)||0)+1);}
  const rarity=new Map();for(const[b,c]of count)rarity.set(b,1+Math.log((n+1)/(c+1)));
  return {rarity,vertices:n};
 }
 function prepareAnchor(bindings){
  const values=merged(bindings.map(x=>x.bone),bindings.map(x=>x.weight)),entries=[...values].sort((a,b)=>b[1]-a[1]);
  if(!entries.length)throw Error('Empty captured binding');
  return {values,primary:entries[0][0],primaryWeight:entries[0][1]};
 }
 function isDirectMode(mode){return ['none','shared','shared-vertex','shared-product'].includes(mode);}
 function similarity(ids,weights,anchor,mode='weighted',frequency=null){
  if(mode==='none')return 1;
  const current=merged(ids,weights);
  if(['shared','shared-vertex','shared-product'].includes(mode)){
   // Membership is a hard nonzero-ID intersection. The two weighted variants
   // scale the original field directly, without cosine normalization or gate().
   let sum=0;for(const[b,w]of current)if(anchor.values.has(b)){
    if(mode==='shared')return 1;
    sum+=mode==='shared-vertex'?w:w*anchor.values.get(b);
   }return sum;
  }
  if(mode==='primary')return Math.min(1,(current.get(anchor.primary)||0)/Math.max(anchor.primaryWeight,1e-8));
  let dot=0,a2=0,v2=0;
  const importance=b=>mode==='weighted'?(frequency?.rarity.get(b)||1):1;
  for(const[b,w]of anchor.values){const v=w*importance(b);a2+=v*v;}
  for(const[b,w]of current){const s=importance(b),v=w*s;v2+=v*v;dot+=v*((anchor.values.get(b)||0)*s);}
  if(!['weighted','cosine'].includes(mode))throw Error('Unknown binding gate');
  return Math.max(0,Math.min(1,dot/Math.sqrt(Math.max(a2*v2,1e-30))));
 }
 function gate(score,threshold=.5,transition=.25){
  if(![score,threshold,transition].every(Number.isFinite)||threshold<0||threshold>=1||transition<0)throw Error('Invalid gate parameters');
  if(score<=0)return 0;
  const end=Math.min(1,threshold+transition);if(end<=threshold)return score>=threshold?1:0;
  const t=Math.max(0,Math.min(1,(score-threshold)/(end-threshold)));return t*t*(3-2*t);
 }
 function masks({weights,indices,bindings,mode='weighted',threshold=.5,transition=.25,frequency=null}){
  if(weights.length!==indices.length||weights.length%4)throw Error('Invalid blend layout');
  const anchor=prepareAnchor(bindings),n=weights.length/4,out=new Float32Array(n),scores=new Float32Array(n);
  const stats=frequency||(mode==='weighted'?frequencies(weights,indices):null);let eligible=0;
  for(let i=0;i<n;i++){
   const score=similarity(indices.subarray(i*4,i*4+4),weights.subarray(i*4,i*4+4),anchor,mode,stats);
   scores[i]=score;out[i]=isDirectMode(mode)?score:gate(score,threshold,transition);if(out[i]>1e-6)eligible++;
  }return {mask:out,scores,eligible,anchor,frequency:stats};
 }
 function apply({positions,mask,anchor,displacement,params}){
  if(positions.length!==mask.length*3||![...anchor,...displacement,params.radius,params.falloff,params.volume,params.maxOffset].every(Number.isFinite)||!(params.radius>0)||params.maxOffset<0)throw Error('Invalid soft input');
  const delta=D.clampOffset(displacement,params.maxOffset),out=new Float32Array(positions),amount=new Float32Array(mask.length);
  let moved=0,maxMovement=0;
  for(let i=0;i<mask.length;i++){
   const m=mask[i];if(m<=0)continue;const x=[positions[i*3],positions[i*3+1],positions[i*3+2]];
   const u=D.displacement('runtime',x,[0,0,1],anchor,[0,0,1],delta,params).map(v=>v*m),l=Math.hypot(...u);
   if(l>1e-9)moved++;maxMovement=Math.max(maxMovement,l);amount[i]=l;
   for(let k=0;k<3;k++)out[i*3+k]=x[k]+u[k];
  }
  return {positions:out,amount,moved,maxMovement,delta,limited:Math.hypot(...displacement)>params.maxOffset};
 }
 return {merged,frequencies,prepareAnchor,isDirectMode,similarity,gate,masks,apply};
});
