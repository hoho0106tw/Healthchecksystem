const tabs = await (await fetch('http://127.0.0.1:9227/json')).json();
const ws = new WebSocket(tabs.find(t=>t.type==='page').webSocketDebuggerUrl);
await new Promise(r=>ws.addEventListener('open',r,{once:true}));
let serial=0; const pending=new Map(); const errors=[];
ws.addEventListener('message',e=>{const m=JSON.parse(e.data); if(m.id){const p=pending.get(m.id);pending.delete(m.id);m.error?p.reject(m.error):p.resolve(m.result);} if(m.method==='Runtime.exceptionThrown') errors.push(m.params.exceptionDetails.text);});
function call(method,params={}){return new Promise((resolve,reject)=>{const id=++serial; pending.set(id,{resolve,reject});ws.send(JSON.stringify({id,method,params}));});}
await call('Page.enable');await call('Runtime.enable');
await call('Page.navigate',{url:'http://127.0.0.1:5097/Home/Index?reservationDateStart=2026-01-01&reservationDateEnd=2026-12-31'});
for(let i=0;i<40;i++){await new Promise(r=>setTimeout(r,250));const r=await call('Runtime.evaluate',{expression:'document.readyState === "complete" && !!document.querySelector(".dashboard-list-filters")',returnByValue:true});if(r.result.value)break;}
for(const width of [1920,1366,768,390]){
await call('Emulation.setDeviceMetricsOverride',{width,height:900,deviceScaleFactor:1,mobile:false});await new Promise(r=>setTimeout(r,200));
const result=await call('Runtime.evaluate',{expression:`JSON.stringify((()=>{const panel=document.querySelector('.examinee-panel');const filter=panel.querySelector('form');const wrap=panel.querySelector('.table-wrap');const footer=panel.querySelector('.table-footer');const r=e=>{const b=e.getBoundingClientRect();return {x:b.x,y:b.y,w:b.width,h:b.height,bottom:b.bottom}};return {width:innerWidth,pageWidth:document.documentElement.scrollWidth,panel:r(panel),filters:r(filter),table:r(wrap),footer:r(footer),rows:document.querySelectorAll('.examinee-table tbody tr').length,summary:document.querySelector('[data-page-summary]').textContent,inputs:[...filter.querySelectorAll('input')].map(r)}})())`,returnByValue:true});console.log(result.result.value);
}
console.log(JSON.stringify({errors}));await call('Browser.close');ws.close();