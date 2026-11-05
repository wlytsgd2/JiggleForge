'use strict';
const fs=require('node:fs'),path=require('node:path'),{execFileSync}=require('node:child_process');
const dir=path.resolve(__dirname,'../../artifacts/weight-size-r1/validation'),report={};
for(const driver of ['warp','hardware'])report[driver]=JSON.parse(execFileSync(path.join(dir,'weight_size_gpu.exe'),[dir,driver],{encoding:'utf8',timeout:180000,windowsHide:true}));
fs.writeFileSync(path.join(dir,'gpu.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report,null,2));
