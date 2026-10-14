// Shared by the offline page (inlined at generation time) and the inert diff reader.
export const boardRarities = {Basic:'基础',Common:'普通 · 白',Uncommon:'罕见 · 蓝',Rare:'稀有 · 金',Ancient:'先古',Token:'临时选项'};
export const boardCardTypes = {Attack:'攻击',Skill:'技能',Power:'能力'};
export const attackWindups = {Automatic:'自动（原型）',Normal:'普通 · 0.15秒',Heavy:'重击 · 0.20秒'};
export const castWindups = {Automatic:'自动（原型）',Short:'短 · 0.25秒',Long:'长 · 0.40秒'};
export const saucerShots = {Automatic:'自动（原型／小炮）',SmallCannon:'小炮',LargeCannon:'重炮',SweepingBeam:'扫荡光束',Hyperbeam:'超能光束',Laser:'激光',PriestBeam:'祭司光束'};
export const editableFields = ['name','cost','upgradedCost','role','melee','text','upgradedText','designText','notes','mark','order','hp','color','rarity','type','attackWindup','castWindup','saucerShot'];

export function mergeDescription(base = '', upgraded = '') {
  if (base === upgraded) return base;
  const a = base.split('\n').filter(Boolean), b = upgraded.split('\n').filter(Boolean);
  // Only merge numeric/energy substitutions when the surrounding sentence is identical.
  const tokens = /\d+(?:\.\d+)?|⚡+/g;
  const shape = s => s.replace(tokens, '#');
  const equalShape = (x, y) => shape(x) === shape(y);
  const dp = Array.from({length:a.length + 1}, () => Array(b.length + 1).fill(0));
  for (let i=a.length;i>=0;i--) for (let j=b.length;j>=0;j--) {
    if(i===a.length) dp[i][j]=b.length-j;
    else if(j===b.length) dp[i][j]=a.length-i;
    else dp[i][j]=Math.min(1+dp[i+1][j],1+dp[i][j+1],
      (a[i]===b[j]?0:equalShape(a[i],b[j])?0.4:1.5)+dp[i+1][j+1]);
  }
  const out=[];let i=0,j=0;
  while(i<a.length || j<b.length) {
    if(i<a.length && j<b.length) {
      const penalty=a[i]===b[j]?0:equalShape(a[i],b[j])?0.4:1.5;
      if(Math.abs(dp[i][j]-penalty-dp[i+1][j+1])<0.001) {
        if(a[i]===b[j]) out.push(a[i]);
        else if(equalShape(a[i],b[j])) {
          const next=[...b[j].matchAll(tokens)].map(m=>m[0]);let k=0;
          out.push(a[i].replace(tokens, value=>{const v=next[k++];return value===v?value:`${value}→${v}`;}));
        } else out.push(a[i],`升级后：${b[j]}`);
        i++;j++;continue;
      }
    }
    if(i<a.length && (j===b.length || dp[i][j]===1+dp[i+1][j])) out.push(`${a[i++]}（升级后移除）`);
    else out.push(`升级后：${b[j++]}`);
  }
  return out.join('\n');
}

export function descriptionFor(e) {
  return e.designText ?? (['card','effect'].includes(e.kind) ? mergeDescription(e.text,e.upgradedText) : e.text ?? '');
}

export function costFor(e) {
  // Keep exported sentinel values intact; an author-entered X is not a host enum.
  const display=v=>String(v ?? '');
  const a=display(e.cost),b=display(e.upgradedCost);
  return a===b?a:`${a}→${b}`;
}

export function readCost(value) {
  const parts=value.replace(/费/g,'').trim().split('→').map(s=>s.trim().toUpperCase());
  if(parts.length>2 || parts.some(s=>! /^(?:\d+|X|-1|-2)$/.test(s))) throw new Error('费用请填数字或 X，例如 1 或 1→0。');
  const number=s=>s==='X'?'X':Number(s);
  return {cost:number(parts[0]),upgradedCost:number(parts[1]??parts[0])};
}

export function defaultLayout(entries) {
  const rows=[];
  for(const role of ['',...entries.filter(e=>e.kind==='role').map(e=>e.role)]) {
    const cards=entries.filter(e=>e.kind==='card' && e.role===role).sort((a,b)=>a.order-b.order);
    const rarities=Object.keys(boardRarities).filter(r=>cards.some(e=>e.rarity===r) || (role?['Uncommon','Rare']:['Common']).includes(r));
    for(const rarity of rarities) {
      const list=cards.filter(e=>e.rarity===rarity),id=`row.${role||'common'}.${rarity}`;
      rows.push({id,role,rarity,slots:Array.from({length:Math.max(8,Math.ceil(list.length/8)*8)},(_,i)=>({id:`${id}.${i+1}`,entryId:list[i]?.id??null}))});
    }
  }
  return rows;
}

export function normalizeBoard(input) {
  validateBoard(input);
  const data=structuredClone(input);
  data.schema=2;
  data.layout ??= defaultLayout(data.entries);
  for(const e of data.entries.filter(e=>e.kind==='card')) {
    e.attackWindup ??= 'Automatic';e.castWindup ??= 'Automatic';e.saucerShot ??= 'Automatic';
  }
  validateBoard(data);
  return data;
}

export function validateBoard(data) {
  const check=(condition,message)=>{if(!condition)throw new Error(message);};
  const safeId=id=>typeof id==='string' && /^[\w.-]{1,150}$/.test(id);
  const keys=(obj,allowed)=>Object.keys(obj).every(k=>allowed.includes(k));
  check(data && [1,2].includes(data.schema),'Unknown board schema');
  check(data.project==='DohnaDohna','Wrong project');
  check(typeof data.baselineHash==='string' && /^[a-f0-9]{64}$/.test(data.baselineHash),'Invalid baseline hash');
  check(Array.isArray(data.entries) && data.entries.length<2000,'Invalid entries');
  const ids=new Map();
  for(const e of data.entries) {
    check(e && safeId(e.id) && !ids.has(e.id),'Invalid or duplicate stable ID');ids.set(e.id,e);
    check(['card','role','relic','effect','power'].includes(e.kind),'Unknown entry kind');
    check(keys(e,['id','kind','source','type',...editableFields]),'Unknown entry field');
    for(const value of Object.values(e)) check(value===null || (['string','number','boolean'].includes(typeof value) && (typeof value!=='string' || value.length<20000) && (typeof value!=='number' || Number.isFinite(value))),'Invalid field value');
    check(typeof e.name==='string' && typeof e.role==='string' && Number.isFinite(e.order),'Missing name/role/order');
    for(const key of ['name','role','text','upgradedText','designText','notes','mark','color']) check(e[key]===undefined || typeof e[key]==='string',`Invalid ${key}`);
    check(e.kind!=='card' || Object.hasOwn(boardRarities,e.rarity),'Unknown card rarity');
    check(e.kind==='card' ? Object.hasOwn(boardCardTypes,e.type) : e.type===undefined,'Unknown or non-card type');
    check(e.melee===undefined || typeof e.melee==='boolean','Invalid melee flag');
    for(const [key,values] of [['attackWindup',attackWindups],['castWindup',castWindups],['saucerShot',saucerShots]])
      check(e[key]===undefined || (e.kind==='card' && Object.hasOwn(values,e[key])),`Invalid ${key}`);
  }
  const roleEntries=data.entries.filter(e=>e.kind==='role');
  check(roleEntries.every(e=>safeId(e.role) && Number.isFinite(e.hp) && /^#[0-9a-f]{6}$/i.test(e.color)),'Invalid role definition');
  check(new Set(roleEntries.map(e=>e.role)).size===roleEntries.length,'Duplicate role');
  const roles=new Set(['',...data.entries.filter(e=>e.kind==='role').map(e=>e.role)]);
  for(const e of data.entries) check(roles.has(e.role),'Unknown role');
  if(data.schema===1) {check(data.layout===undefined,'Legacy layout must be absent');return;}
  check(Array.isArray(data.layout) && data.layout.length<500,'Invalid layout');
  const rows=new Set(),slots=new Set(),placed=new Set();
  for(const row of data.layout) {
    check(row && keys(row,['id','role','rarity','slots']) && safeId(row.id) && !rows.has(row.id),'Invalid/duplicate row');rows.add(row.id);
    check(roles.has(row.role) && Object.hasOwn(boardRarities,row.rarity),'Invalid row role/rarity');
    check(Array.isArray(row.slots) && row.slots.length<=2000,'Invalid slots');
    for(const slot of row.slots) {
      check(slot && keys(slot,['id','entryId']) && safeId(slot.id) && !slots.has(slot.id),'Invalid/duplicate slot');slots.add(slot.id);
      check(slots.size<=6000,'Too many slots');
      if(slot.entryId===null)continue;
      const e=ids.get(slot.entryId);
      check(e?.kind==='card' && !placed.has(e.id),'Invalid/duplicate slot occupant');
      check(e.role===row.role && e.rarity===row.rarity,'Slot and card ownership disagree');placed.add(e.id);
    }
  }
  check(placed.size===data.entries.filter(e=>e.kind==='card').length,'Unplaced card');
}
