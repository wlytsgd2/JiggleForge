/* Laboratory input producer ONLY. The native hierarchy/inverse binds here are
 * used to SIMULATE the game's supplied skin matrices, never passed to inference.
 * Generated animations are synthetic test samples, not captured game animation.
 */
window.GenericInputAdapter=(()=>{
 const C=window.SkinLabCore,R=window.BoneRotationCore;
 function native(){
  const data=C.decodeModel(window.JFSkeletonData),pose=C.createPose(data.bones);C.updatePose(data.bones,pose);
  const streams=data.meshes.map((m,i)=>({label:'原生模型流 '+i,visible:m.visible,index:m.index,
   input:{position:m.position,normal:m.normal,weights:m.weight,indices:m.skinIndex,boneCount:m.inverseBindMatrices.length},
   palette:C.skinMatrices(m,pose)}));
  function frames(count=56){const result=streams.map(()=>[]);
   for(let f=0;f<count;f++){
    const p=C.createPose(data.bones),t=f/(count-1)*Math.PI*2;
    for(let i=0;i<data.bones.length;i++){
     const a=.15*Math.sin(t+i*.43),b=.12*Math.sin(t*1.7+i*.67);
     p.offsets[i].quaternion=R.multiply([Math.sin(a/2),0,0,Math.cos(a/2)],[0,0,Math.sin(b/2),Math.cos(b/2)]);
    }C.updatePose(data.bones,p);
    data.meshes.forEach((m,i)=>result[i].push(C.skinMatrices(m,p)));
   }return result;
  }
  return {name:'Pheony 原生测试模型',streams,frames,kind:'native'};
 }
 function synthetic(kind){
  const T=window.THREE,positions=[],normals=[],ids=[],weights=[],triangles=[];
  function append(geometry,offset,weightFunction){const p=geometry.attributes.position.array,n=geometry.attributes.normal.array,start=positions.length/3;
   for(let i=0;i<p.length;i+=3){const v=[p[i]+offset[0],p[i+1]+offset[1],p[i+2]+offset[2]],w=weightFunction(v);
    positions.push(...v);normals.push(n[i],n[i+1],n[i+2]);ids.push(0,1,2,0);weights.push(...w,0);}
   for(const i of geometry.index.array)triangles.push(start+i);geometry.dispose();
  }
  const smooth=(a,b,x)=>{const t=Math.max(0,Math.min(1,(x-a)/(b-a)));return t*t*(3-2*t);};
  let joints,parents;
  if(kind==='chain'){
   append(new T.BoxGeometry(.42,.36,.3,12,12,12),[0,.18,0],()=>[1,0,0]);
   append(new T.CylinderGeometry(.075,.085,1.5,28,45),[0,.9,0],p=>{const a=smooth(.48,.65,p[1]),b=smooth(1.05,1.2,p[1]);return[1-a,a*(1-b),a*b];});
   append(new T.SphereGeometry(.11,18,12),[0,1.65,0],()=>[0,0,1]);
   joints=[[0,0,0],[0,.56,0],[0,1.12,0]];parents=[-1,0,1];
  }else{
   append(new T.BoxGeometry(.65,.6,.17,14,14,6),[0,.9,-.09],()=>[1,0,0]);
   for(let side=0;side<2;side++)append(new T.SphereGeometry(.15,30,24),[(side?1:-1)*.17,1,.09],p=>{const w=smooth(-.045,.12,p[2]);return side?[1-w,0,w]:[1-w,w,0];});
   joints=[[0,.7,0],[-.17,1,0],[.17,1,0]];parents=[-1,0,0];
  }
  const id=C.identity(),stream={label:'合成模型流 0',visible:true,index:new Uint32Array(triangles),
   input:{position:new Float32Array(positions),normal:new Float32Array(normals),weights:new Float32Array(weights),indices:new Uint16Array(ids),boneCount:3},palette:[id,[...id],[...id]]};
  function frames(count=56){const out=[];
   for(let f=0;f<count;f++){
    const t=f/(count-1)*Math.PI*2,globals=[];
    for(let i=0;i<3;i++){
     const relative=parents[i]<0?joints[i]:R.sub(joints[i],joints[parents[i]]),a=.3*Math.sin(t+i*.3),b=.24*Math.sin(1.7*t+i*.8);
     const q=R.multiply([Math.sin(a/2),0,0,Math.cos(a/2)],[0,0,Math.sin(b/2),Math.cos(b/2)]),local=C.trs(relative,q,[1,1,1]);
     globals[i]=parents[i]<0?local:C.multiply(globals[parents[i]],local);
    }out.push(globals.map((g,i)=>C.multiply(g,C.trs(R.scale(joints[i],-1),[0,0,0,1],[1,1,1]))));
   }return[out];
  }
  return{name:kind==='chain'?'合成多段关节链':'合成双突起',streams:[stream],frames,kind};
 }
 return {load:kind=>kind==='native'?native():synthetic(kind)};
})();
