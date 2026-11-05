'use strict';
// Independent double-precision reference for the WeightSize r1 experiment.
// No anatomy, nearest-point-dependent radius, old sampled estimator or Tactile
// kernel is used here. Positions are the observed full stream in world units.
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict'),vm=require('node:vm'),crypto=require('node:crypto');
const EPS=1e-8,MAX_VERTICES=262144;
function merged(ids,weights){const m=new Map();for(let k=0;k<4;k++){if(!Number.isFinite(weights[k])||weights[k]<0)return null;if(weights[k]>0)m.set(ids[k],(m.get(ids[k])||0)+weights[k]);}return m;}
function estimate(mesh,{reference=.03,enabled=true,bones=true}={}){
 const n=mesh.positions.length/3,seed=mesh.seedVertex||0,captured=merged(mesh.indices.slice(seed*4,seed*4+4),mesh.weights.slice(seed*4,seed*4+4));
 const neutral=reason=>({valid:false,reason,scale:1,bone:null,rawL:null,usedL:null});
 if(!enabled||!bones)return neutral('disabled');if(!(reference>0)||!Number.isFinite(reference))return neutral('reference');if(!captured||!captured.size)return neutral('capture');
 if(!Number.isInteger(n)||n<1||n>MAX_VERTICES)return neutral('count');
 const bone=[...captured].sort((a,b)=>b[1]-a[1]||a[0]-b[0])[0][0],origin=mesh.positions.slice(0,3),samples=[];let total=0,sum=[0,0,0];
 for(let i=0;i<n;i++){const m=merged(mesh.indices.slice(i*4,i*4+4),mesh.weights.slice(i*4,i*4+4));if(!m)continue;const w=m.get(bone)||0;if(w<=0)continue;const r=Array.from(mesh.positions.slice(i*3,i*3+3),(p,k)=>p-origin[k]);if(!r.every(Number.isFinite))continue;samples.push([r,w]);total+=w;for(let k=0;k<3;k++)sum[k]+=w*r[k];}
 if(!(total>0)||samples.length<2)return neutral('empty');const center=sum.map(v=>v/total);let second=0;for(const[r,w]of samples)second+=w*r.reduce((a,v,k)=>a+(v-center[k])**2,0);
 const rawL=Math.sqrt(Math.max(0,second/total));if(!(rawL>0)||!Number.isFinite(rawL))return neutral('degenerate');const usedL=Math.min(rawL,.04);return{valid:true,bone,rawL,usedL,scale:Math.max(.25,Math.min(4,usedL/reference)),center:center.map((v,k)=>v+origin[k]),total};
}
function field(point,anchor,displacement,radius=.12,falloff=1,volume=2){
 const r=point.map((v,k)=>v-anchor[k]),r2=r.reduce((s,v)=>s+v*v,0),distance=Math.sqrt(r2),q=distance/Math.max(radius,1e-6);if(q>=1)return[0,0,0];
 const support=Math.pow(Math.max(0,Math.min(1,1-q*q)),Math.max(falloff,1)),directional=1/(4*(1-.45)),eps=Math.max(radius*.35,1e-5),inv=1/Math.sqrt(r2+eps*eps),norm=eps/(1.5-directional),iso=norm*((1-directional)*inv+.5*eps*eps*inv**3),dir=norm*directional*inv**3*r.reduce((s,v,k)=>s+v*displacement[k],0)*Math.max(volume,0);
 return r.map((v,k)=>(displacement[k]*iso+v*dir)*support);
}
function sphere(radius=.03,count=512){const positions=new Float32Array(count*3),weights=new Float32Array(count*4),indices=new Uint32Array(count*4);for(let i=0;i<count;i++){const y=1-2*(i+.5)/count,a=i*Math.PI*(3-Math.sqrt(5)),r=Math.sqrt(1-y*y);positions.set([radius*r*Math.cos(a),radius*y,radius*r*Math.sin(a)],i*3);weights[i*4]=1;indices[i*4]=7;}return{positions,weights,indices,seedVertex:0};}
function clone(m){return{positions:new Float32Array(m.positions),weights:new Float32Array(m.weights),indices:new Uint32Array(m.indices),seedVertex:m.seedVertex};}
function run(){
 const out=path.resolve(__dirname,'../../artifacts/weight-size-r1/validation');fs.mkdirSync(out,{recursive:true});let checks=0;const near=(a,b,e=1e-6)=>{assert(Math.abs(a-b)<=e,`${a} != ${b}`);checks++;};
 const base=sphere(),e=estimate(base);assert(e.valid);near(e.rawL,.03,2e-7);near(e.scale,1,1e-5);
 const duplicate=clone(base);for(let i=0;i<duplicate.weights.length;i+=4){duplicate.weights.set([.3,.7,0,0],i);duplicate.indices.set([7,7,0,0],i);}near(estimate(duplicate).rawL,e.rawL);
 for(const seedVertex of [0,11,120,511]){near(estimate({...base,seedVertex}).rawL,e.rawL);checks++;}
 const scaled=clone(base);scaled.positions=Float32Array.from(base.positions,v=>v*.5);near(estimate(scaled).rawL,e.rawL*.5);
 const moved=clone(base);moved.positions=Float32Array.from(base.positions,(v,i)=>v+[12,-8,2][i%3]);near(estimate(moved).rawL,e.rawL,2e-6);
 const rotated=clone(base);for(let i=0;i<base.positions.length;i+=3)rotated.positions.set([.6*base.positions[i]-.8*base.positions[i+1],.8*base.positions[i]+.6*base.positions[i+1],base.positions[i+2]],i);near(estimate(rotated).rawL,e.rawL);
 near(estimate(sphere(.2)).usedL,.04);near(estimate(sphere(.2)).scale,4/3);near(estimate(base,{reference:1}).scale,.25);near(estimate(base,{reference:.001}).scale,4);near(estimate(base,{enabled:false}).scale,1);near(estimate(base,{bones:false}).scale,1);near(estimate(sphere(0)).scale,1);
 const nonUnit=clone(base);nonUnit.weights=Float32Array.from(base.weights,v=>v*.957);near(estimate(nonUnit).rawL,e.rawL);
 // Different regional widths make choosing the wrong single bone detectable.
 const tie=sphere(.025);for(let i=0;i<512;i++){tie.weights.set(i===0?[.25,.5,.25,0]:i<256?[1,0,0,0]:[0,1,0,0],i*4);tie.indices.set([3,7,3,0],i*4);if(i>=256)for(let k=0;k<3;k++)tie.positions[i*3+k]*=.4;}assert.equal(estimate(tie).bone,3);checks++;
 const unequal=clone(tie);unequal.weights.set([.2,.6,.2,0],0);assert.equal(estimate(unequal).bone,7);checks++;
 const gradient=clone(base);for(let i=0;i<512;i++){const w=i===0?1:.1+.9*(i/511);gradient.weights.set([w,1-w,0,0],i*4);gradient.indices.set([7,11,0,0],i*4);}const gradientSquared=clone(gradient);for(let i=0;i<512;i++)gradientSquared.weights[i*4]**=2;assert(Math.abs(estimate(gradient).rawL-estimate(gradientSquared).rawL)>1e-4,'Linear and squared weights must be distinguishable');checks++;
 const invalid=clone(base);invalid.weights[12]=-1;assert(estimate(invalid).valid);near(estimate(invalid).total,511);
 const sparse=sphere(.03,2);assert(estimate(sparse).valid);checks++;
 const large=sphere(.03,MAX_VERTICES),over=sphere(.03,MAX_VERTICES+1);assert(estimate(large).valid);assert.equal(estimate(over).valid,false);checks+=2;
 let similarity=0;for(const k of [.25,.5,1,4/3,2,4])for(let i=0;i<50;i++){const p=Array.from(base.positions.slice(i*3,i*3+3)),a=[0,0,0],d=[.018,-.024,.011],u=field(p,a,d),v=field(p.map(x=>x*k),a,d.map(x=>x*k),.12*k);for(let j=0;j<3;j++)similarity=Math.max(similarity,Math.abs(v[j]/k-u[j]));}assert(similarity<1e-12);checks++;
 const cases=[],identity=[1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1,...Array(100).fill(0)];
 function add(label,mesh,flags=0,offset=0,model=identity,reference=.03){const world=Float32Array.from(mesh.positions,(v,i)=>{const k=i%3,b=i-k;return model[k]*mesh.positions[b]+model[4+k]*mesh.positions[b+1]+model[8+k]*mesh.positions[b+2]+model[12+k];}),anchor=Array.from(world.slice(mesh.seedVertex*3,mesh.seedVertex*3+3)),estimated=estimate({...mesh,positions:world},{reference,enabled:!(flags&4),bones:!(flags&16)}),expected=flags&8?0:estimated.scale;const captured=merged(mesh.indices.slice(mesh.seedVertex*4,mesh.seedVertex*4+4),mesh.weights.slice(mesh.seedVertex*4,mesh.seedVertex*4+4));let weight=0;for(const w of captured.values())if(w>EPS)weight+=w;if(flags&16)weight=1;const point=anchor.map((v,k)=>v+(k===0?.10*expected:0)),motion=[.018,-.024,.011],delta=motion.map(v=>v*expected),expectedField=field(point,anchor,delta,.12*expected).map(v=>v*weight);cases.push({label,mesh,flags,offset,model,reference,anchor,expected,weight,point,expectedField,estimated});}
 for(const r of [.002,.015,.03,.04,.09])for(const flags of [0,1,2,3])add('size-'+r+'-layout-'+flags,sphere(r),flags,5);
 add('disabled',base,4);add('stale-identity',base,8);add('bones-off',base,16);add('duplicated-id',duplicate,3,7);add('ties-choose-smaller-id',tie,3,11);add('strongest-single-bone',unequal,0,3);add('non-unit-authentic-weights',nonUnit,3,5);add('degenerate-neutral',sphere(0),0);add('two-point-region',sparse,3);add('upper-guard',base,0,0,identity,.001);add('lower-guard',base,3,0,identity,1);add('entire-stream-boundary',large,3,11);add('over-limit-neutral',over,0,5);
 add('linear-weight-gradient',gradient,3,5);add('same-bone-different-grab',{...base,seedVertex:120},3,3);add('single-point-neutral',sphere(.03,1),3);add('invalid-region-member-skipped',invalid,3,3);
 add('rigid-model-transform',base,3,7,[.6,.8,0,0,-.8,.6,0,0,0,0,1,0,1.5,-2,3,1,...Array(100).fill(0)]);
 add('model-nonuniform-scale',base,1,3,[.7,0,0,0,0,1.2,0,0,0,0,1.7,0,-.2,.3,.4,1,...Array(100).fill(0)]);
 // Existing local assets are validation inputs only, never shipped in the
 // manual runtime. These are static lab source streams, not game frame dumps.
 const lab=path.resolve(__dirname,'../binding-weight/lab'),asset=process.env.WEIGHT_SIZE_CHARACTER_ASSETS?path.resolve(process.env.WEIGHT_SIZE_CHARACTER_ASSETS):path.resolve(__dirname,'../../artifacts/weight-regions/character-assets.js'),nativeFile=path.join(lab,'model-data.js');
 const context=vm.createContext({window:{}}),sourceFiles=[nativeFile],skipped=[];vm.runInContext(fs.readFileSync(nativeFile,'utf8'),context);
 if(process.env.WEIGHT_SIZE_CHARACTER_ASSETS)assert(fs.existsSync(asset),'Explicit character asset input does not exist');
 if(fs.existsSync(asset)){vm.runInContext(fs.readFileSync(asset,'utf8'),context);sourceFiles.push(asset);}else skipped.push('Additional local character fixtures were not supplied; native and synthetic tests still run.');
 const C=require(path.join(lab,'skin-core.js')),G=require(path.join(lab,'generic-bone-core.js')),decoded=C.decodeModel(context.window.JFSkeletonData),pose=C.createPose(decoded.bones);C.updatePose(decoded.bones,pose);const native=decoded.meshes[0],nativeInput={position:native.position,normal:native.normal,weights:native.weight,indices:native.skinIndex,boneCount:native.inverseBindMatrices.length};
 const actual=[{id:'native',positions:G.skin(nativeInput,C.skinMatrices(native,pose)),weights:native.weight,indices:native.skinIndex,aim:[-.03099498,1.17394632,.11536033]},...(context.window.WeightRegionCharacterAssets?.models||[]).map(m=>({id:m.id,positions:Float32Array.from(m.streams[0].position),weights:Float32Array.from(m.streams[0].weights),indices:Uint32Array.from(m.streams[0].boneIndices),aim:m.id==='zhuyuan-covered'?[-.075,1.28,.12014688111594557]:[-.06,1.18,.11412949844377263]}))];
 for(const m of actual){let closest=Infinity,seedVertex=0;for(let i=0;i<m.positions.length/3;i++){const d=m.aim.reduce((sum,v,k)=>sum+(m.positions[i*3+k]-v)**2,0);if(d<closest){closest=d;seedVertex=i;}}m.seedVertex=seedVertex;assert(estimate(m).valid);add('real-static-'+m.id,m,3,13);checks++;}
 const repo=path.resolve(__dirname,'../..'),realSources=sourceFiles.map(p=>({path:p.startsWith(repo+path.sep)?path.relative(repo,p).replaceAll('\\','/'):'external-fixture/'+path.basename(p),sha256:crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex')}));
 const chunks=[Buffer.alloc(4)];chunks[0].writeUInt32LE(cases.length);for(const c of cases){const n=c.mesh.positions.length/3,b=Buffer.alloc(4*4+4+116*4+3*4+4+4+6*4+n*40+(n+c.offset)*32);let cursor=0;const u=v=>{b.writeUInt32LE(v>>>0,cursor);cursor+=4;},f=v=>{b.writeFloatLE(v,cursor);cursor+=4;};for(const v of[c.flags,n,c.offset,c.mesh.seedVertex+c.offset])u(v);f(c.reference);c.model.forEach(f);c.anchor.forEach(f);f(c.expected);f(c.weight);c.point.forEach(f);c.expectedField.forEach(f);for(let i=0;i<n;i++){for(let k=0;k<3;k++)f(c.mesh.positions[i*3+k]);for(const v of[0,0,1,1,0,0,1])f(v);}for(let i=0;i<n+c.offset;i++){const j=i-c.offset;for(let k=0;k<4;k++)f(j<0?(k===0?1:0):c.mesh.weights[j*4+k]);for(let k=0;k<4;k++)u(j<0?99:c.mesh.indices[j*4+k]);}assert.equal(cursor,b.length);chunks.push(b);}
 fs.writeFileSync(path.join(out,'weight-size-cases.bin'),Buffer.concat(chunks));const report={pass:true,checks,gpuCases:cases.length,maxVertices:MAX_VERTICES,referenceLength:.03,rawLengthCap:.04,scaleBounds:[.25,4],linearRawWeights:true,singleStrongestBone:true,duplicateSlotsMerged:true,seedIndependentForSameBone:true,maxSimilarityError:similarity,realSources,skipped,cases:cases.map(c=>({label:c.label,flags:c.flags,count:c.mesh.positions.length/3,offset:c.offset,scale:c.expected,bone:c.estimated.bone,rawL:c.estimated.rawL}))};fs.writeFileSync(path.join(out,'numeric.json'),JSON.stringify(report,null,2));console.log(JSON.stringify({...report,cases:report.cases.length}));return report;
}
module.exports={estimate,field,run};if(require.main===module)run();
