'use strict';
// Interpret the generated INI schedule and shared project hook verbatim.
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const ini=fs.readFileSync(path.join(process.argv[2],'JiggleForge.ini'),'utf8').replace(/\r/g,'');
const body=n=>ini.match(new RegExp('\\['+n+'\\]\\n([\\s\\S]*?)(?=\\n\\[|$)'))[1];
let v={runtimeEnabled:1,mouseDown:0,demandSession:0},builtins={time:0,cursor_showing:0,cursor_x:.5,cursor_y:.5},events=[],pool=new Map();
function expression(s){s=s.replace(/\$PoolDemandProjectSession\[@cs-u0\]/g,'projectSession()').replace(/@cs-u0/g,'901').replace(/\$(\w+)/g,(_,k)=>`(v.${k}||0)`).replace(/\b(time|cursor_showing|cursor_x|cursor_y)\b/g,(_,k)=>`builtins.${k}`);return Function('v','builtins','projectSession',`return (${s});`)(v,builtins,()=>pool.get(901)??-1);}
function execute(name){let stack=[];for(let line of body(name).split('\n')){line=line.split(';')[0].trim();if(!line)continue;const active=()=>stack.at(-1)?.active??true;
 if(line.startsWith('if ')){let parent=active(),yes=parent&&!!expression(line.slice(3));stack.push({parent,active:yes,taken:yes});continue;}
 if(line.startsWith('else if ')){let b=stack.at(-1),yes=b.parent&&!b.taken&&!!expression(line.slice(8));b.active=yes;b.taken=b.taken||yes;continue;}
 if(line==='else'){let b=stack.at(-1);b.active=b.parent&&!b.taken;b.taken=true;continue;}
 if(line==='endif'){stack.pop();continue;}if(!active())continue;
 let m=line.match(/^(?:local )?\$(\w+) = (.+)$/);if(m){v[m[1]]=expression(m[2]);continue;}
 m=line.match(/^([xyzw]\d+) = (.+)$/);if(m){v[m[1]]=expression(m[2]);continue;}
 if(line.startsWith('$PoolDemandProjectSession[')){pool.set(901,v.demandSession);continue;}
 m=line.match(/^run = (CommandList\w+)$/);if(m){events.push(m[1]);if(['CommandListDemandClearPick','CommandListDemandSleep'].includes(m[1]))execute(m[1]);continue;}
 if(/^(?:post )?(?:clear|[a-z]+-t\d+|[a-z]+-u\d+|dispatch|Resource\w+|Pool\w+\[|\$Pool\w+\[)/.test(line)){events.push(line);continue;}
 if(line.startsWith('cs = '))continue;throw Error('Unhandled executed INI: '+line);
 }assert.equal(stack.length,0);}
function step(t,patch={},cursor=undefined){Object.assign(v,patch);builtins.time=t;if(cursor!==undefined)builtins.cursor_showing=cursor;events=[];execute('CommandListDemandSchedule');return [...events];}
for(let i=0;i<240;i++){assert.equal(step(i/60).length,0);assert.equal(v.demandAwake,0);}
// Repeated combat clicks / X and even Alt must not wake or allocate/copy.
for(let i=0;i<480;i++){assert.equal(step(5+i/60,{mouseDown:i%2,demandAltHeld:1}).length,0);assert.equal(v.demandAwake,0);assert.equal(v.demandDragHeld,0);assert.equal(v.demandMotionActive,0);assert.equal(v.demandRetryFrames,0);events=[];execute('CustomShaderUpdateProjectMotion');assert(!events.some(e=>e.startsWith('dispatch')||e.startsWith('clear')));}
step(14,{},1);assert.equal(v.demandAwake,1);assert.equal(v.demandPickActive,1);assert.equal(v.demandMotionActive,0);assert.equal(v.demandWarmFrames,2);assert.equal(v.demandSession,1);assert.equal(v.demandDragHeld,0,'Held combat click cannot carry into a visible menu');
step(14.02);step(14.04);assert.equal(v.demandDragHeld,0);assert.equal(v.demandMotionActive,0);
step(14.06,{mouseDown:0});step(14.08,{mouseDown:1});assert.equal(v.demandDragHeld,1);assert.equal(v.demandMotionActive,1);assert.equal(v.demandGain,1);assert.equal(v.demandRetryFrames,12);
execute('CustomShaderUpdateProjectMotion');assert(events.includes('dispatch = 16, 1, 1'));assert(events.includes('clear = cs-u0 0.0'));
events=[];execute('CustomShaderUpdateProjectMotion');assert(!events.includes('clear = cs-u0 0.0'));
step(14.2,{mouseDown:0});step(15);assert.equal(v.demandMotionActive,1);assert.equal(v.demandGain,1); // visible cursor preserves normal release
step(15.02,{mouseDown:1});
const end=step(15.04,{},0);assert(end.includes('CommandListDemandSleep'));assert(end.includes('clear = ResourceCapturedPick 0.0'));assert.equal(v.demandAwake,0);assert.equal(v.demandMotionActive,0);assert.equal(v.demandDragHeld,0);assert.equal(v.demandGain,0);
for(let i=0;i<480;i++){assert.equal(step(16+i/60,{mouseDown:i%2,demandAltHeld:1}).length,0);events=[];execute('CustomShaderUpdateProjectMotion');assert(!events.some(e=>e.startsWith('dispatch')||e.startsWith('clear')));}
step(25,{},1);assert.equal(v.demandPickActive,1);assert.equal(v.demandSession,2);assert.equal(v.demandDragHeld,0);assert.equal(v.demandMotionActive,0);
events=[];execute('CustomShaderUpdateProjectMotion');assert(events.includes('clear = cs-u0 0.0'));assert(!events.includes('dispatch = 16, 1, 1'));
step(25.02,{mouseDown:0});step(25.04,{mouseDown:1});assert.equal(v.demandMotionActive,1);
// Outside the game window is also non-interactive, even with visible cursor.
builtins.cursor_x=1.01;step(25.06);assert.equal(v.demandAwake,0);assert.equal(v.demandDragHeld,0);
builtins.cursor_x=.5;step(25.08);assert.equal(v.demandSession,3);assert.equal(v.demandMotionActive,0);assert.equal(v.demandDragHeld,0);
step(25.1,{mouseDown:0});step(25.12,{mouseDown:1});assert.equal(v.demandMotionActive,1);
step(25.14,{runtimeEnabled:0});assert.equal(v.demandAwake,0);assert.equal(v.demandGain,0);assert.equal(v.demandDragHeld,0);
step(25.16,{runtimeEnabled:1});assert.equal(v.demandAwake,1);assert.equal(v.demandMotionActive,0);assert.equal(v.demandDragHeld,0);
step(25.18,{mouseDown:0});step(25.2,{mouseDown:1});assert.equal(v.demandMotionActive,1);
const skin=body('CommandListBindingEndSkin');assert(!/\bcopy\b|run =|cs-t\d/.test(skin),'Sleeping registration cannot copy/probe/dispatch');
const render=body('CommandListBindingBeginDraw');assert(render.includes('if $demandMotionActive == 1 && $boneWeightEnabled == 1\n        run = CommandListBindingPrepareViews'));
const present=body('Present');assert(present.includes('if $runtimeEnabled == 1 && $demandAwake == 1'));assert(present.includes('else if $runtimeEnabled == 0 && $runtimeEnabledPrevious == 1'));
assert(!body('CommandListDemandSleep').includes('clear = ResourceInputController'));
assert(body('CommandListRuntimeStep').includes('x81 = $demandDragHeld'));assert(!body('CommandListRuntimeStep').includes('x81 = $mouseDown'));
assert(present.includes('if $demandDragHeld == 1'));assert(!present.includes('if $mouseDown == 1'));
assert(!body('CommandListDemandSchedule').includes('$demandAltHeld'));assert(!body('CommandListDemandSchedule').includes('$demandTail'));
assert(body('ShaderOverrideBindingDiagnosticSkin').includes('$demandAwake == 1'));
console.log(JSON.stringify({pass:true,hiddenFramesWithoutGpuCommands:1200,hiddenCombatClickFrames:960,visibleCursorOnlyWake:true,hiddenAltCannotWake:true,heldCombatClickRequiresRelease:true,hiddenCancelsImmediately:true,rawGrabInputFiltered:true,privateProjectDispatchGated:true,privateProjectResetOncePerSession:true,oneShotSleepCleanup:true,noIdleCopiesOrProbes:true,f7Preserved:true}));
