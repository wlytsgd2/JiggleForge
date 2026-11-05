'use strict';
// Replay the ACTUAL INI BeginDraw assignments against alternating resources,
// including render-only frames. This is a command-list regression, not a claim
// that a particular player's frame resources were measured.
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const ini=fs.readFileSync(path.join(process.argv[2],'JiggleForge.ini'),'utf8').replace(/\r/g,'');
const body=ini.match(/\[CommandListBindingBeginDraw\]\n([\s\S]*?)(?=\n\[|$)/)[1];
assert(!body.includes('== $bindingFrame'),'Cannot require skin to run every render frame');
assert(!body.includes('@vs-cb1')&&!body.includes('vs-cb1->Offset'),'Cannot freeze a rotating object CB address');
assert(body.includes('@PoolBindingOwner[$bindingKey] == $bindingKey'),'Unowned cached SO must not be reused');
const pool=new Map(),frames=[];
function skin(so,blend,frame,mode=0){pool.set(so,{owner:so,blend,frame,mode,bytes:32*5});}
function begin({so,cb,offset,frame}){
 const vars={bindingFrame:frame,bindingResolveFrames:0,bindingCanResolve:1,demandMotionActive:1,boneWeightEnabled:1},params={},bindings={},stack=[];
 const poolUID=(name,key)=>name==='PoolBindingOwner'?(pool.get(key)?.owner||0):0;
 const poolVar=(name,key)=>name==='PoolBindingSource'?(pool.get(key)?.blend||0):name==='PoolBindingFrame'?(pool.get(key)?.frame??-1):name==='PoolBindingMode'?(pool.get(key)?.mode??2):0;
 const poolSize=(name,key)=>name==='PoolBindingSource'?(pool.get(key)?.bytes||0):0;
 const attr=(resource,name)=>resource==='vb0'?({Stride:40,Offset:0,Size:200}[name]):name==='Offset'?offset:0;
 const uid=resource=>resource==='vb0'?so:resource==='vs-cb1'?cb:0;
 function expression(s){
  s=s.replace(/@(Pool\w+)\[\$(\w+)\]/g,(_,n,k)=>`poolUID('${n}',vars.${k})`)
   .replace(/(Pool\w+)\[\$(\w+)\]->Size/g,(_,n,k)=>`poolSize('${n}',vars.${k})`)
   .replace(/\$(Pool\w+)\[\$(\w+)\]/g,(_,n,k)=>`poolVar('${n}',vars.${k})`)
   .replace(/(vb0|vs-cb1)->(Stride|Offset|Size)/g,(_,r,n)=>`attr('${r}','${n}')`)
   .replace(/@(vb0|vs-cb1)/g,(_,r)=>`uid('${r}')`)
   .replace(/\b([xyzw]\d+)\b/g,(_,k)=>`(params['${k}']||0)`)
   .replace(/\$(\w+)/g,(_,n)=>`(vars.${n}||0)`);
  return Function('vars','params','poolUID','poolVar','poolSize','attr','uid',`return (${s});`)(vars,params,poolUID,poolVar,poolSize,attr,uid);
 }
 for(let line of body.split('\n')){
  line=line.split(';')[0].trim();if(!line)continue;
  if(line.startsWith('if ')){const parent=stack.at(-1)?.active??true,active=parent&&!!expression(line.slice(3));stack.push({parent,active,taken:active});continue;}
  if(line==='else'){const branch=stack.at(-1);branch.active=branch.parent&&!branch.taken;branch.taken=true;continue;}
  if(line==='endif'){stack.pop();continue;}
  if(stack.at(-1)?.active===false)continue;
  if(line==='run = CommandListBindingPrepareViews')continue; // skin() supplies an already validated view in this replay.
  let m=line.match(/^\$(\w+)\s*=\s*(.+)$/);if(m){vars[m[1]]=expression(m[2]);continue;}
  m=line.match(/^([xyzw]\d+)\s*=\s*(.+)$/);if(m){params[m[1]]=expression(m[2]);continue;}
  m=line.match(/^(vs-t\d+)\s*=\s*(.+)$/);if(m){bindings[m[1]]=m[2];continue;}
  throw Error('Unexpected executed BeginDraw command: '+line);
 }
 return {metadata:['x113','y113','z113','w113'].map(k=>params[k]),bound:bindings['vs-t79']!=='null'||bindings['vs-t80']!=='null',typed:params.y114===1};
}
let capture=null;
for(let frame=0;frame<24;frame++){
 const so=101+(frame%2),cb=301+(frame%2),offset=(frame%2)*256;
 // Both buffer aliases are seen in the first two frames; then skin is skipped
 // on 2/3 frames while the game continues rendering the existing output.
 const skinned=frame<2||frame%3===0;if(skinned)skin(so,202,frame+1,frame%2);
 const current=begin({so,cb,offset,frame:frame+1});if(!capture)capture=current.metadata;
 assert(current.bound);assert.deepEqual(current.metadata,capture,'Hold must not blink on CB/SO alternation or skipped skin frames');
 const oldIdentity=[so,202,cb,offset],oldAllowed=frame===0||JSON.stringify(oldIdentity)===JSON.stringify([101,202,301,0]);
 const oldFresh=(pool.get(so)?.frame===frame+1);
 frames.push({frame,skinned,oldGate:oldAllowed&&oldFresh?1:0,newGate:1});
}
const unknown=begin({so:999,cb:301,offset:0,frame:30});assert(!unknown.bound);assert.deepEqual(unknown.metadata,[0,0,0,0]);
skin(101,888,30);assert.notDeepEqual(begin({so:101,cb:301,offset:0,frame:30}).metadata,capture,'Changing actual mesh binding must invalidate the frozen binding');
pool.get(102).owner=0;assert(!begin({so:102,cb:301,offset:0,frame:30}).bound,'Dropping ownership must invalidate the cache');
console.log(JSON.stringify({pass:true,frames:frames.length,oldRejectedFrames:frames.filter(f=>!f.oldGate).length,newRejectedFrames:0,skippedSkinFrames:frames.filter(f=>!f.skinned).length,unknownStreamRejected:true,changedBindingRejected:true,unownedStreamRejected:true,actualIniAssignmentsReplayed:true}));
