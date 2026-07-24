import Phaser from 'phaser';
import heroData from './data/hero.json';
import companions from './data/companions.json';
import routes from './data/routes.json';
import chapters from './data/chapters.json';
import enemies from './data/enemies.json';
import items from './data/items.json';
import { loadSave, parseImportedSave, saveGame, serializeSave } from './core/save.js';
import { advanceNode, completeExpedition, defeatExpedition, gatherAndAdvance, pruneExpiredRecovery, recoverCache, returnToTown, skipAndAdvance, startExpedition } from './core/expedition.js';
import { simulateEncounter } from './core/combat.js';
import { claimLoot, lootChoicesFor } from './core/loot.js';

const route = routes[0];
const enemyById = Object.fromEntries(enemies.map(enemy => [enemy.id, enemy]));
const itemById = Object.fromEntries(items.map(item => [item.id, item]));
let state = pruneExpiredRecovery(loadSave());
let gameScene;
let viewMode = state.expedition.state === 'traveling' ? 'expedition' : 'world';
let selectedChapterId = 'hollow_vein';
let primaryWorldAction = null;
let interactionLocked = false;

const ui = Object.fromEntries(['save-status','hero-name','hero-health','view-label','view-title','location-title','location-copy','objective','route-progress','town-ore','town-wood','carried-resources','equipped-weapon','recovery-card','recovery-resources','recovery-time','actions','log','loot-dialog','loot-options'].map(id => [id, document.getElementById(id)]));

function withLog(nextState, ...messages) {
  return { ...nextState, log: [...messages.filter(Boolean).reverse(), ...nextState.log].slice(0, 30) };
}
function currentNode() { return state.expedition.state === 'traveling' ? route.nodes[state.expedition.nodeIndex] : null; }
function selectedChapter() { return chapters.find(chapter => chapter.id === selectedChapterId) ?? chapters[0]; }
function objectiveText() {
  if (state.quest.completed) return 'The Hollow Vein is broken — improve the refuge';
  if (state.expedition.state === 'defeated') return 'Return to Blackfen Refuge';
  const node = currentNode();
  return node ? node.name : selectedChapter().objective;
}
function locationText() {
  const node = currentNode();
  if (state.expedition.state === 'defeated') return ['Expedition Lost', 'Mara was forced from the mine. Any gathered materials now wait in a recoverable cache.'];
  if (node) return [node.name, node.description];
  if (viewMode === 'world') {
    const chapter = selectedChapter();
    if (chapter.status === 'locked') return [chapter.name, chapter.description];
    if (state.quest.completed) return [chapter.name, 'The mine is quiet for now. Repeat the chapter for equipment and resources, or inspect the road ahead.'];
    return [chapter.name, chapter.description];
  }
  return ['Blackfen Refuge', 'The last lanterns burn behind barred windows. Prepare the party, then follow the Ashen Road into the abandoned settlement.'];
}
function formatRemaining(expiresAt) {
  const ms = Math.max(0, expiresAt - Date.now());
  const hours = Math.floor(ms / 3600000), minutes = Math.ceil((ms % 3600000) / 60000);
  return `${hours}h ${minutes}m remaining`;
}
function persist(message = 'Progress autosaved') { saveGame(state); ui['save-status'].textContent = message; }

function action(label, handler, className = '') {
  const button = document.createElement('button'); button.type = 'button'; button.textContent = label; button.className = className; button.addEventListener('click', handler); ui.actions.append(button);
}
function contextualAction(label, node, handler, className = 'primary') {
  const wrapped = () => {
    if (interactionLocked) return;
    interactionLocked = true;
    ui.actions.querySelectorAll('button').forEach(button => { button.disabled = true; });
    ui['save-status'].textContent = `Moving to ${node.name}`;
    if (gameScene) gameScene.performContextAction(node, () => { interactionLocked = false; handler(); });
    else { interactionLocked = false; handler(); }
  };
  primaryWorldAction = wrapped;
  action(label, wrapped, className);
}
function renderActions() {
  ui.actions.replaceChildren();
  primaryWorldAction = null;
  if (state.pendingLoot.length) { action('Choose expedition reward', showLoot, 'primary'); return; }
  if (state.expedition.state === 'town') {
    const chapter = selectedChapter();
    if (chapter.status === 'available') action(state.quest.completed ? 'Confirm repeat expedition' : 'Confirm chapter expedition', enterSelectedChapter, 'primary');
    else {
      const locked = document.createElement('button'); locked.type = 'button'; locked.disabled = true; locked.textContent = 'Chapter locked'; locked.className = 'skip primary'; ui.actions.append(locked);
    }
    if (state.recovery) action('Recover dropped cache', () => commit(withLog(recoverCache(state), 'The lost cache was recovered before its trail went cold.')), 'danger');
    if (state.quest.completed) action('Equip best weapon', equipBestWeapon);
    return;
  }
  if (state.expedition.state === 'defeated') { action('Return to world map', returnAfterDefeat, 'primary'); return; }
  const node = currentNode();
  if (!node) { action('Return to the world map', finishChapter, 'primary'); return; }
  if (node.type === 'story') contextualAction('Move through the gate', node, () => commit(withLog(advanceNode(state), 'Mara crossed the Ashen Gate.')));
  if (node.type === 'resource') contextualAction(`Gather ${node.amount} ${node.resource}`, node, () => commit(withLog(gatherAndAdvance(state, node.resource, node.amount), `${node.amount} ${node.resource} gathered and packed for the return journey.`)));
  if (node.type === 'combat' || node.type === 'boss') contextualAction(node.type === 'boss' ? 'Approach the overseer' : 'Approach and autobattle', node, () => runCombat(node));
  if (node.type === 'optional') {
    contextualAction('Break the shrine seal', node, () => runCombat(node), 'optional');
    action('Leave the seal untouched', () => commit(withLog(skipAndAdvance(state, node.id), 'The party passed the sealed shrine without entering.')), 'skip');
  }
  if (node.type === 'camp') contextualAction('Walk to camp and rest', node, () => commit(withLog(advanceNode({ ...state, hero: { ...state.hero, health: state.hero.maxHealth } }), 'Mara rested and returned to full health.')));
}
function enterSelectedChapter() {
  const chapter = selectedChapter();
  if (chapter.status !== 'available') return;
  viewMode = 'expedition';
  commit(withLog(startExpedition(state, chapter.routeId), `Chapter confirmed: ${chapter.name}.`, `The party entered ${route.name}.`), 'Expedition started');
}
function finishChapter() {
  viewMode = 'world';
  commit(withLog(completeExpedition(state), 'The corrupted overseer fell. The blacksmith reopened in Blackfen Refuge.', 'The Scavenger joined the companion roster.'), 'Chapter completed');
}
function returnAfterDefeat() {
  viewMode = 'world';
  commit(withLog(returnToTown(state), 'Mara returned to Blackfen Refuge to recover.'), 'Returned to town');
}
function selectChapter(chapterId) {
  if (state.expedition.state !== 'town') return;
  selectedChapterId = chapterId;
  render();
}
function render() {
  state = pruneExpiredRecovery(state);
  const [title, copy] = locationText();
  ui['hero-name'].textContent = heroData.name;
  ui['view-label'].textContent = viewMode === 'world' ? 'OVERWORLD' : 'EXPEDITION';
  ui['view-title'].textContent = viewMode === 'world' ? 'The Ashen Road' : route.name;
  ui['hero-health'].textContent = `${state.hero.health} / ${state.hero.maxHealth} HP`;
  ui['location-title'].textContent = title; ui['location-copy'].textContent = copy;
  ui.objective.textContent = objectiveText();
  const progress = state.quest.completed ? 100 : state.expedition.state === 'traveling' ? (state.expedition.nodeIndex / route.nodes.length) * 100 : 0;
  ui['route-progress'].style.width = `${progress}%`;
  ui['town-ore'].textContent = state.town.resources.ore; ui['town-wood'].textContent = state.town.resources.wood;
  const carried = state.expedition.carried; ui['carried-resources'].textContent = carried.ore || carried.wood ? `${carried.ore} ore · ${carried.wood} wood` : '—';
  ui['equipped-weapon'].textContent = itemById[state.hero.equipped.weapon]?.name ?? 'Unarmed';
  ui['recovery-card'].hidden = !state.recovery;
  if (state.recovery) { ui['recovery-resources'].textContent = `${state.recovery.resources.ore} ore · ${state.recovery.resources.wood} wood`; ui['recovery-time'].textContent = formatRemaining(state.recovery.expiresAt); }
  ui.log.innerHTML = state.log.map(entry => `<li>${entry}</li>`).join('');
  renderActions();
  gameScene?.syncState(state);
}
function commit(nextState, message) { state = nextState; persist(message); render(); }
function runCombat(node) {
  ui.actions.querySelectorAll('button').forEach(button => { button.disabled = true; });
  ui['save-status'].textContent = 'Autobattle in progress';
  const enemy = enemyById[node.enemyId];
  const outcome = simulateEncounter(state, enemy, items);
  if (gameScene) gameScene.playCombat(outcome, () => resolveCombat(node, outcome));
  else resolveCombat(node, outcome);
}
function resolveCombat(node, outcome) {
  const enemy = enemyById[node.enemyId];
  let next = withLog(outcome.state, ...outcome.log, `${enemy.name} confronted Mara.`);
  if (outcome.result === 'defeat') next = withLog(defeatExpedition(next), 'The expedition ended in defeat. Gathered resources were dropped.');
  else { next = { ...next, pendingLoot: lootChoicesFor(enemy, items).map(item => item.id) }; next = withLog(next, `${enemy.name} was defeated.`); }
  commit(next, outcome.result === 'victory' ? 'Victory autosaved' : 'Defeat autosaved');
}
function showLoot() {
  ui['loot-options'].replaceChildren();
  state.pendingLoot.map(id => itemById[id]).filter(Boolean).forEach(item => {
    const button = document.createElement('button'); button.type = 'button'; button.className = 'loot-option';
    button.innerHTML = `<span class="rarity">${item.rarity.toUpperCase()} · ${item.slot.toUpperCase()}</span><strong>${item.name}</strong><span>Power +${item.power}</span><span>${item.effect}</span>`;
    button.addEventListener('click', () => chooseLoot(item)); ui['loot-options'].append(button);
  });
  ui['loot-dialog'].showModal();
}
function chooseLoot(item) {
  let next = claimLoot(state, item.id); next = advanceNode(next);
  next = withLog(next, `${item.name} was added to the inventory.`);
  state = next; ui['loot-dialog'].close();
  persist('Reward saved'); render();
}
function equipBestWeapon() {
  const weapons = state.inventory.map(id => itemById[id]).filter(item => item?.slot === 'weapon').sort((a,b) => b.power - a.power);
  if (!weapons.length) return;
  commit(withLog({ ...state, hero: { ...state.hero, equipped: { ...state.hero.equipped, weapon: weapons[0].id } } }, `${weapons[0].name} equipped.`), 'Equipment saved');
}

document.getElementById('export-save').addEventListener('click', () => {
  const blob = new Blob([serializeSave(state)], { type:'application/json' }); const url = URL.createObjectURL(blob); const link = Object.assign(document.createElement('a'), { href:url, download:'ashen-road-save.json' }); link.click(); URL.revokeObjectURL(url); ui['save-status'].textContent = 'Save exported';
});
document.getElementById('import-save').addEventListener('change', async event => {
  try { state = parseImportedSave(await event.target.files[0].text()); persist('Save imported'); render(); } catch (error) { ui['save-status'].textContent = error.message; } event.target.value = '';
});
document.getElementById('clear-log').addEventListener('click', () => commit({ ...state, log: [] }, 'Log cleared'));

class ExpeditionScene extends Phaser.Scene {
  constructor(){ super('ExpeditionScene'); this.viewObjects=[]; }
  preload(){
    this.load.image('overworld-art','/assets/generated/ashen-road-overworld.png');
    this.load.image('mine-arena-art','/assets/generated/mine-arena.png');
    this.load.image('character-lineup','/assets/generated/character-lineup.png');
  }
  create(){
    this.cameras.main.setBackgroundColor('#0d1519');
    const source=this.textures.get('character-lineup').getSourceImage();
    const frameBounds=[[0,.19],[.17,.40],[.38,.61],[.575,.75],[.73,1]];
    frameBounds.forEach(([start,end],index)=>this.textures.get('character-lineup').add(`unit-${index}`,0,Math.floor(source.width*start),0,Math.floor(source.width*(end-start)),source.height));
    this.input.on('pointermove', pointer => { if(pointer.isDown && viewMode === 'expedition'){ this.cameras.main.scrollX -= pointer.velocity.x/12; this.cameras.main.scrollY -= pointer.velocity.y/12; } });
    gameScene=this; this.syncState(state);
  }
  keep(object){ this.viewObjects.push(object); return object; }
  clearView(){ this.tweens.killAll(); this.viewObjects.forEach(object => object.destroy()); this.viewObjects=[]; this.cameras.main.setScroll(0,0); }
  diamond(graphics,x,y,w,h,color){
    graphics.fillStyle(color,1); graphics.beginPath(); graphics.moveTo(x,y-h/2); graphics.lineTo(x+w/2,y); graphics.lineTo(x,y+h/2); graphics.lineTo(x-w/2,y); graphics.closePath(); graphics.fillPath(); graphics.lineStyle(1,0x516059,.38); graphics.strokePath();
  }
  mapText(x,y,text,size='12px',color='#dfe7df'){ return this.keep(this.add.text(x,y,text,{fontFamily:'system-ui',fontSize:size,color,backgroundColor:'#0b1117dd',padding:{x:5,y:3}}).setOrigin(.5)); }
  syncState(next){ this.clearView(); if(viewMode === 'world') this.drawOverworld(); else this.drawExpedition(next); }
  drawOverworld(){
    this.keep(this.add.image(480,270,'overworld-art').setDisplaySize(960,540));
    const positions=[[190,357],[422,243],[625,190]];
    chapters.forEach((chapter,index)=>{
      const [x,y]=positions[index], selected=chapter.id===selectedChapterId, locked=chapter.status==='locked';
      const halo=this.keep(this.add.circle(x,y,selected ? 34 : 27,selected ? 0xd4aa62 : 0x10171c,selected ? .2 : .72).setStrokeStyle(selected ? 3 : 2,locked ? 0x525b5e : 0xd4aa62));
      const node=this.keep(this.add.circle(x,y,16,locked?0x465056:index===0?0x9a543f:0x5d877d).setStrokeStyle(2,0x10171c).setInteractive({useHandCursor:true}));
      this.keep(this.add.text(x,y,locked?'×':String(index+1),{fontFamily:'Georgia',fontSize:'13px',fontStyle:'bold',color:'#fff'}).setOrigin(.5));
      this.mapText(x,y+43,`${chapter.chapter.toUpperCase()}\n${chapter.name}`,chapter.id===selectedChapterId?'13px':'11px',locked?'#78827e':'#edf1e8').setAlign('center').setShadow(0,2,'#000',4);
      node.on('pointerup',()=>selectChapter(chapter.id)); halo.setInteractive({useHandCursor:true}).on('pointerup',()=>selectChapter(chapter.id));
    });
    this.keep(this.add.text(28,485,'SELECT A CHAPTER NODE',{fontFamily:'system-ui',fontSize:'11px',fontStyle:'bold',color:'#f0d39a',backgroundColor:'#091015c9',padding:{x:9,y:6}}));
  }
  drawExpedition(next){
    const node=currentNode();
    this.keep(this.add.image(480,270,'mine-arena-art').setDisplaySize(960,540));
    const g=this.keep(this.add.graphics());
    this.drawEnvironment(g,node);
    this.drawParty(next);
    if(node && ['combat','optional','boss'].includes(node.type) && !next.pendingLoot.length) this.drawEnemies(node);
    if(next.pendingLoot.length) this.drawDrops(next.pendingLoot);
    else if(node) this.drawObjectiveMarker(node);
    if(next.expedition.state==='defeated') this.keep(this.add.rectangle(480,270,960,540,0x210d0d,.48));
    this.keep(this.add.text(32,476,node?.type==='boss'?'BOSS ENCOUNTER':node?.name?.toUpperCase() ?? 'ROUTE COMPLETE',{fontFamily:'Georgia',fontSize:'16px',fontStyle:'bold',color:'#f0d39a',backgroundColor:'#091015c9',padding:{x:9,y:6}}));
  }
  drawEnvironment(g,node){
    if(node?.resource==='ore') for(const [x,y] of [[500,382],[540,400],[568,370]]){ this.keep(this.add.polygon(x,y,[0,-18,17,-4,11,15,-12,14,-19,-3],0x718c91).setStrokeStyle(2,0xc2d0cb)); }
    if(node?.resource==='wood') for(const [x,y] of [[495,392],[540,410],[575,382]]){ this.keep(this.add.rectangle(x,y,68,14,0x7a553a).setRotation(-.3).setStrokeStyle(2,0x35261d)); }
    if(node?.type==='camp'){
      this.keep(this.add.rectangle(548,422,54,8,0x6e4933).setRotation(.35));
      this.keep(this.add.rectangle(548,422,54,8,0x6e4933).setRotation(-.35));
      this.keep(this.add.circle(548,404,11,0xf28b49));
      const fireGlow=this.keep(this.add.circle(548,405,28,0xe6773f,.14));
      this.tweens.add({targets:fireGlow,scale:{from:.85,to:1.12},alpha:{from:.1,to:.2},duration:540,yoyo:true,repeat:-1});
    }
  }
  drawParty(next){
    this.heroShadow=this.keep(this.add.ellipse(295,430,92,24,0x030608,.66));
    this.heroSprite=this.keep(this.add.image(295,432,'character-lineup','unit-0').setScale(.285).setOrigin(.5,1));
    this.heroHealthBack=this.keep(this.add.rectangle(224,275,142,9,0x170d0d).setOrigin(0,.5).setStrokeStyle(1,0xd9c7a0));
    this.heroHealthFill=this.keep(this.add.rectangle(226,275,138*(next.hero.health/next.hero.maxHealth),5,0xb95143).setOrigin(0,.5));
    this.mapText(295,255,'MARA','11px','#f2d59e');
    if(next.expedition.state==='defeated') this.heroSprite.setAlpha(.35);
    else this.tweens.add({targets:this.heroSprite,angle:{from:-.35,to:.35},duration:1150,yoyo:true,repeat:-1,ease:'Sine.inOut'});
  }
  drawEnemies(node){
    const enemy=enemyById[node.enemyId],boss=Boolean(enemy.boss),x=610,y=430;
    this.keep(this.add.ellipse(x,y, boss ? 118 : 82,boss ? 30 : 22,0x030608,.68));
    this.enemySprite=this.keep(this.add.image(x,y,'character-lineup',boss ? 'unit-4' : 'unit-3').setScale(boss ? .32 : .255).setOrigin(.5,1));
    this.enemyHealthBack=this.keep(this.add.rectangle(530,275,160,9,0x170d0d).setOrigin(0,.5).setStrokeStyle(1,boss?0xe38a70:0xc0c9c3));
    this.enemyHealthFill=this.keep(this.add.rectangle(532,275,156,5,boss?0xc74d3e:0x7a9b8d).setOrigin(0,.5));
    this.mapText(610,255,enemy.name.toUpperCase(),boss?'12px':'11px',boss?'#e5947f':'#dfe7df');
    this.tweens.add({targets:this.enemySprite,angle:{from:.3,to:-.3},duration:1320,yoyo:true,repeat:-1,ease:'Sine.inOut'});
  }
  drawDrops(ids){
    ids.forEach((id,index)=>{ const item=itemById[id],x=485+index*72,y=355+(index%2)*24; const halo=this.keep(this.add.circle(x,y,29,0xd4aa62,.16).setInteractive({useHandCursor:true})); this.keep(this.add.circle(x,y,11,item.rarity==='epic'?0x926fb1:0xd4aa62).setStrokeStyle(2,0xf5ddaa)); this.keep(this.add.text(x,y,'◆',{fontFamily:'Georgia',fontSize:'13px',color:'#fff'}).setOrigin(.5)); this.mapText(x,y+35,item.name,'9px','#f1d49b'); this.tweens.add({targets:halo,scale:{from:.88,to:1.12},alpha:{from:.1,to:.24},duration:850,yoyo:true,repeat:-1}); halo.on('pointerup',()=>showLoot()); });
  }
  objectivePlacement(node){
    if(node.type==='story') return {markerX:360,markerY:285,destX:345,destY:340};
    if(node.type==='resource') return {markerX:535,markerY:350,destX:440,destY:425};
    if(node.type==='camp') return {markerX:548,markerY:380,destX:450,destY:425};
    return {markerX:610,markerY:335,destX:420,destY:430};
  }
  drawObjectiveMarker(node){
    const spot=this.objectivePlacement(node);
    const halo=this.keep(this.add.circle(spot.markerX,spot.markerY,28,0xd4aa62,.13).setStrokeStyle(2,0xf0cf8d,.65).setInteractive({useHandCursor:true}));
    this.keep(this.add.circle(spot.markerX,spot.markerY,7,0xf0cf8d));
    this.tweens.add({targets:halo,scale:{from:.8,to:1.22},alpha:{from:.08,to:.22},duration:900,yoyo:true,repeat:-1});
    halo.on('pointerup',()=>{ if(primaryWorldAction) primaryWorldAction(); });
  }
  performContextAction(node,onComplete){
    const spot=this.objectivePlacement(node),duration=780;
    this.tweens.add({targets:this.heroSprite,x:spot.destX,y:spot.destY+2,duration,ease:'Sine.inOut'});
    this.tweens.add({targets:this.heroShadow,x:spot.destX,y:spot.destY,duration,ease:'Sine.inOut'});
    this.tweens.add({targets:this.heroSprite,angle:{from:-1.5,to:1.5},duration:130,yoyo:true,repeat:5});
    this.time.delayedCall(duration+40,()=>{
      if(node.type==='resource'){
        let strikes=0;
        const strike=()=>{
          this.tweens.add({targets:this.heroSprite,angle:-8,duration:100,yoyo:true});
          const shard=this.keep(this.add.circle(520+strikes*10,370-strikes*5,4,node.resource==='ore'?0xbfd0ce:0xb8895e));
          this.tweens.add({targets:shard,y:shard.y-35,x:shard.x+18,alpha:0,duration:420});
          strikes+=1; if(strikes<3)this.time.delayedCall(240,strike); else this.time.delayedCall(440,onComplete);
        };
        strike();
      } else if(node.type==='camp'){
        const restGlow=this.keep(this.add.circle(spot.destX,spot.destY-30,55,0xe49a58,.18));
        this.tweens.add({targets:restGlow,scale:1.5,alpha:0,duration:650,onComplete});
        this.time.delayedCall(700,onComplete);
      } else this.time.delayedCall(node.type==='combat'||node.type==='optional'||node.type==='boss'?120:360,onComplete);
    });
  }
  floatingDamage(x,y,damage,color){
    const text=this.keep(this.add.text(x,y,`-${damage}`,{fontFamily:'Georgia',fontSize:'20px',fontStyle:'bold',color,stroke:'#130a08',strokeThickness:4}).setOrigin(.5));
    this.tweens.add({targets:text,y:y-38,alpha:0,duration:560,ease:'Quad.out'});
  }
  playCombat(outcome,onComplete){
    if(!this.heroSprite || !this.enemySprite || !outcome.events.length){ onComplete(); return; }
    const heroX=this.heroSprite.x,enemyX=this.enemySprite.x;
    let index=0;
    const nextEvent=()=>{
      if(index>=outcome.events.length){ this.time.delayedCall(520,onComplete); return; }
      const event=outcome.events[index++];
      if(event.type==='hero-attack'){
        const finishHit=()=>{
          this.enemyHealthFill.displayWidth=156*(event.remainingHealth/event.maxHealth);
          this.enemySprite.setTintFill(0xffd4c7);
          this.tweens.add({targets:this.enemySprite,x:enemyX+18,duration:80,yoyo:true,repeat:1,onComplete:()=>this.enemySprite.clearTint()});
          this.floatingDamage(enemyX,295,event.damage,'#ffd29c');
          this.time.delayedCall(360,nextEvent);
        };
        if(event.ability==='Ember Bolt'){
          const ember=this.keep(this.add.circle(heroX+35,335,9,0xf28b49).setStrokeStyle(3,0xffd6a0));
          this.keep(this.add.circle(heroX+35,335,22,0xf28b49,.18));
          this.tweens.add({targets:ember,x:enemyX-20,y:330,duration:330,ease:'Quad.in',onComplete:finishHit});
        } else {
          this.tweens.add({targets:this.heroSprite,x:heroX+125,duration:180,ease:'Quad.in',yoyo:true,onYoyo:finishHit});
        }
      } else {
        this.tweens.add({targets:this.enemySprite,x:enemyX-105,duration:180,ease:'Quad.in',yoyo:true,onYoyo:()=>{
          this.heroHealthFill.displayWidth=138*(event.remainingHealth/event.maxHealth);
          this.heroSprite.setTintFill(0xffb2a6);
          this.cameras.main.shake(90,.006);
          this.floatingDamage(heroX,295,event.damage,'#ff8e7d');
          this.time.delayedCall(360,()=>{ this.heroSprite.clearTint(); nextEvent(); });
        }});
      }
    };
    this.time.delayedCall(280,nextEvent);
  }
}

new Phaser.Game({ type:Phaser.AUTO,parent:'game-container',width:960,height:540,backgroundColor:'#0d1519',scale:{mode:Phaser.Scale.FIT,autoCenter:Phaser.Scale.CENTER_BOTH},input:{activePointers:3},scene:[ExpeditionScene] });
render();
