import worker from './PublicPvpLobby/dist/server/index.js';
let row = {revision:0,value:JSON.stringify({peers:{},rooms:{}})};
const env = {DB:{prepare(sql){return {async first(){return {...row}},bind(value,revision){return {async run(){if(row.revision!==revision)return {meta:{changes:0}};row={revision:revision+1,value};return {meta:{changes:1}}}}}}}}};
async function call(path,body){const request=new Request('https://lobby.example'+path,body?{method:'POST',body:JSON.stringify(body)}:{});const reply=await worker.fetch(request,env);return {status:reply.status,body:await reply.json()};}
function check(condition,message){if(!condition)throw Error(message)}
const host='a'.repeat(32),guests=['b','c','d','e'].map(c=>c.repeat(32));
const room=await call('/rooms',{token:host,name:'Public PvP test'});check(room.status===200,'create');
check((await call('/rooms')).body.length===1,'public discovery');
const joins=await Promise.all(guests.map(token=>call('/join',{token,id:room.body.id})));
check(joins.filter(j=>j.status===200).length===3,'atomic four-player capacity');
const joined=joins.find(j=>j.status===200);check(joined.body.host!==host,'private token disclosure');
const notices=await call('/poll?token='+host);check(notices.body.length===3,'join notifications');
const guest=guests[joins.findIndex(j=>j.status===200)];
check((await call('/signal',{token:guest,to:joined.body.host,signal:{Kind:'offer',Content:'test',Mid:''}})).status===200,'signaling');
check((await call('/poll?token='+host)).body[0].signal.Content==='test','signal delivery');
await call('/leave',{token:host});check((await call('/rooms')).body.length===0,'host departure');
check((await call('/poll?token='+guest)).body.some(m=>m.kind==='left'),'departure notification');
console.log('PASS public discovery, concurrent capacity, private tokens, signaling and departure cleanup with D1 CAS simulation.');
