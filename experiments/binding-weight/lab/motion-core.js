/* JavaScript port of runtime/motion_model.hlsl, JF_StepMotion.
 * Same discrete implicit spring, filtering, release/tap impulses and wheel policy.
 * No animation or GPU pick/calibration emulation; caller supplies frozen capture.
 */
(function(root,factory){const api=factory();if(typeof module==='object'&&module.exports)module.exports=api;else root.RuntimeMotion=api;})(globalThis,function(){
 'use strict';
 const defaults={radius:.12,strength:.7,falloff:1,volumeResponse:2,dragScale:.75,holdDampingRatio:.84,holdFrequencyHz:10,releaseDampingRatio:0,releaseFrequencyHz:5,releaseImpulse:5,maxOffset:.1,targetFollowSeconds:.02,wheelDepthStep:.02,wheelMinDepth:0,wheelMaxDepth:.15};
 const fields=Object.keys(defaults),clamp=(x,a,b)=>Math.max(a,Math.min(b,x)),dot=(a,b)=>a.reduce((s,v,i)=>s+v*b[i],0),add=(a,b)=>a.map((v,i)=>v+b[i]),sub=(a,b)=>a.map((v,i)=>v-b[i]),mul=(a,s)=>a.map(v=>v*s),cross=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]],unit=(a,f=[0,0,1])=>dot(a,a)>1e-8?mul(a,1/Math.hypot(...a)):[...f];
 const limit=(a,max)=>dot(a,a)>max*max&&dot(a,a)>1e-8?mul(a,max/Math.hypot(...a)):[...a];
 function validate(p){for(const k of fields){if(!Number.isFinite(p[k]))throw Error('Invalid '+k);}
  for(const k of ['radius','falloff','dragScale','maxOffset','wheelDepthStep'])if(p[k]<=0)throw Error('Invalid '+k);
  for(const k of ['volumeResponse','holdFrequencyHz','releaseFrequencyHz','releaseImpulse','targetFollowSeconds'])if(p[k]<0)throw Error('Invalid '+k);
  for(const k of ['holdDampingRatio','releaseDampingRatio'])if(p[k]<0||p[k]>1)throw Error('Invalid '+k);
  if(p.wheelMinDepth>p.wheelMaxDepth)throw Error('Invalid wheel depth interval');return p;
 }
 class Motion {
  constructor(){this.reset();}
  reset(){this.position=[0,0,0];this.velocity=[0,0,0];this.filtered=[0,0,0];this.previous=[0,0,0];this.anchor=[0,0,0];this.right=[1,0,0];this.up=[0,1,0];this.depth=0;this.lastWheel=0;this.wasHeld=false;this.generation=0;this.sleepFrames=0;this.active=false;}
  begin(capture,input){this.reset();this.anchor=[...capture.anchor];this.right=unit(capture.right,[1,0,0]);const up=sub(capture.up,mul(this.right,dot(capture.up,this.right)));this.up=unit(up,[0,1,0]);this.lastWheel=input.wheel||0;this.generation=capture.generation;this.active=true;}
  step(input,capture,p){
   const dt=clamp(Number.isFinite(input.dt)?input.dt:1/60,1/240,.05),max=clamp(p.maxOffset,0,100),held=!!input.held&&!!capture;
   if(held&&(!this.wasHeld||this.generation!==capture.generation))this.begin(capture,input);
   if(this.wasHeld&&!held){
    const kick=clamp(p.releaseImpulse,0,10);this.velocity=add(this.velocity,mul(limit(mul(sub(this.filtered,this.previous),1/dt),max/dt),kick));
    const dist=Math.hypot(...sub(input.cursor,capture.press));
    if(capture.holdSeconds<=.2&&dist<=10){const normal=unit(capture.normal,unit(cross(this.right,this.up)));this.velocity=sub(this.velocity,mul(normal,max*kick*clamp(p.strength,0,10)/dt));this.active=true;this.sleepFrames=0;}
   }
   let target=[0,0,0];
   if(held){let steps=(input.wheel||0)-this.lastWheel;if(Math.abs(steps)>32767)steps=0;this.lastWheel=input.wheel||0;
    this.depth=clamp(this.depth+steps*clamp(p.wheelDepthStep,0,10),p.wheelMinDepth,p.wheelMaxDepth);
    const reference=Math.max(Math.min(...input.viewport.map(Math.abs)),1),delta=sub(input.cursor,capture.press).map(v=>v/reference),scale=clamp(p.dragScale,0,100),forward=unit(cross(this.right,this.up));
    target=limit(mul(add(add(mul(this.right,delta[0]*scale),mul(this.up,-delta[1]*scale)),mul(forward,this.depth)),clamp(p.strength,0,10)),max);
   }
   const follow=clamp(p.targetFollowSeconds,0,10),alpha=follow<=1e-5?1:1-Math.exp(-dt/follow);this.previous=[...this.filtered];this.filtered=add(this.filtered,mul(sub(target,this.filtered),alpha));
   const hz=clamp(held?p.holdFrequencyHz:p.releaseFrequencyHz,0,60),damping=clamp(held?p.holdDampingRatio:p.releaseDampingRatio,0,10),omega=2*Math.PI*hz,k=omega*omega,den=1+dt*2*damping*omega+dt*dt*k;
   this.velocity=mul(add(this.velocity,mul(sub(this.filtered,this.position),dt*k)),1/den);this.position=add(this.position,mul(this.velocity,dt));
   const sq=dot(this.position,this.position);if(sq>max*max&&sq>1e-8){const outward=unit(this.position);this.position=mul(outward,max);const speed=dot(this.velocity,outward);if(speed>0)this.velocity=sub(this.velocity,mul(outward,speed));}
   if(held){this.active=true;this.sleepFrames=0;}else{const asleep=dot(this.position,this.position)<=1e-8&&dot(this.velocity,this.velocity)<=1e-8&&dot(this.filtered,this.filtered)<=1e-8;this.sleepFrames=asleep?this.sleepFrames+1:0;if(this.sleepFrames>=12){this.position=[0,0,0];this.velocity=[0,0,0];this.filtered=[0,0,0];this.previous=[0,0,0];this.active=false;}}
   this.wasHeld=held;return this.position;
  }
 }
 return {defaults,fields,validate,Motion,clamp,limit};
});
