import * as pc from 'playcanvas';
import {
  circlePenetration,
  distanceXZ,
  moveTowardXZ,
  nearestAlive,
  pointSegmentDistanceXZ,
  raySphereDistance
} from './systems.js';
import {
  applyRenderQuality,
  hideRenderHierarchy,
  instantiateAnimated,
  instantiateStatic,
  loadContainer,
  loadTexture,
  prepareContainerMaterials,
  transitionModel
} from './assets.js';
import {
  ABILITIES,
  choosePriorityAbility,
  createAbilityState,
  moveAbilityUp,
  tickAbilityCooldowns
} from './ability-system.js';
import {
  createBossRewardChoices,
  createItemFromTemplate,
  deriveGearStats,
  equipInventoryItem,
  rollLootItem
} from './item-system.js';
import {
  RESOURCE_TYPES,
  createResourceStock,
  depleteResourceNode,
  gatheringDuration,
  gatheringYield,
  respawnResourceNode
} from './gathering-system.js';
import {
  BLACKSMITH_UPGRADE,
  CRAFTING_RECIPES,
  canAfford,
  createTownProgress,
  craftRecipe,
  depositResources,
  loadTownSave,
  parseTownSave,
  recordGather,
  saveTownState,
  serializeTownSave,
  upgradeBlacksmith
} from './town-system.js';
import {
  claimBossReward,
  completeExtraction,
  createExpeditionProgress,
  defeatBoss,
  defeatExpedition,
  overseerDifficulty,
  pruneExpiredRecovery,
  recoverExpeditionCache,
  returnAfterDefeat,
  updateBossAvailability
} from './expedition-system.js';
import { createEnemyStats } from './enemy-system.js';
import {
  ROUTES,
  completeActiveRoute,
  confirmRouteSelection,
  createRouteProgress,
  getAvailableRoutes,
  normalizeRouteProgress,
  selectRoute
} from './route-system.js';
import {
  COMPANION_DEFINITIONS,
  COMPANION_IDS,
  awardCompanionXp,
  createCompanionRoster,
  getActiveCompanionBonuses,
  normalizeCompanionRoster,
  swapActiveCompanion,
  unlockCompanion
} from './companion-system.js';
import {
  DISCOVERIES,
  TUTORIAL_STEPS,
  createDiscoveryProgress,
  deriveDiscoveryCompletion,
  normalizeDiscoveryProgress,
  recordEquipmentPassive,
  recordTutorialStep,
  unlockDiscovery
} from './discovery-system.js';
import { createAudioDirector } from './audio-system.js';

let randomState = 0x5f3759df;
const proceduralRuins = [];
const proceduralTrees = [];
const proceduralRocks = [];
const TERRAIN_DENSITY_MULTIPLIER = 3;
const QUATERNIUS_VILLAGE_MODELS = [
  'Wall_Plaster_Straight',
  'Wall_Plaster_Window_Wide_Round',
  'Wall_Plaster_Door_Round',
  'Corner_Exterior_Wood',
  'Roof_RoundTiles_4x4',
  'Roof_Tower_RoundTiles',
  'Door_1_Round',
  'WindowShutters_Wide_Round_Open',
  'Prop_Chimney',
  'Prop_Crate',
  'Prop_Wagon',
  'Prop_WoodenFence_Single',
  'Prop_Vine4'
];
const QUATERNIUS_NATURE_MODELS = [
  'CommonTree_1',
  'CommonTree_3',
  'TwistedTree_2',
  'TwistedTree_4',
  'DeadTree_3',
  'Pine_2',
  'Bush_Common',
  'Bush_Common_Flowers',
  'Fern_1',
  'Grass_Common_Short',
  'Grass_Common_Tall',
  'Grass_Wispy_Short',
  'Grass_Wispy_Tall',
  'Rock_Medium_1',
  'Rock_Medium_2',
  'Rock_Medium_3',
  'RockPath_Square_Wide',
  'RockPath_Square_Thin',
  'RockPath_Square_Small_1',
  'RockPath_Square_Small_2',
  'RockPath_Square_Small_3',
  'RockPath_Round_Wide',
  'Mushroom_Laetiporus',
  'Ultimate_BirchTree_1'
];

const canvas = document.querySelector('#application-canvas');
const ui = {
  objective: document.querySelector('#objective'),
  zoneLabel: document.querySelector('#zone-label'),
  zoneTitle: document.querySelector('#zone-title'),
  healthFill: document.querySelector('#health-fill'),
  healthValue: document.querySelector('#health-value'),
  focusFill: document.querySelector('#focus-fill'),
  focusValue: document.querySelector('#focus-value'),
  lootValue: document.querySelector('#loot-value'),
  resourceOre: document.querySelector('#resource-ore'),
  resourceWood: document.querySelector('#resource-wood'),
  resourceHerb: document.querySelector('#resource-herb'),
  resourceEssence: document.querySelector('#resource-essence'),
  autoToggle: document.querySelector('#auto-toggle'),
  autoLabel: document.querySelector('#auto-label'),
  abilityList: document.querySelector('#ability-list'),
  inventoryToggle: document.querySelector('#inventory-toggle'),
  inventoryCount: document.querySelector('#inventory-count'),
  inventoryPanel: document.querySelector('#inventory-panel'),
  inventoryClose: document.querySelector('#inventory-close'),
  equipmentSlots: document.querySelector('#equipment-slots'),
  inventoryGrid: document.querySelector('#inventory-grid'),
  inventoryEmpty: document.querySelector('#inventory-empty'),
  refugeToggle: document.querySelector('#refuge-toggle'),
  refugePanel: document.querySelector('#refuge-panel'),
  refugeClose: document.querySelector('#refuge-close'),
  questTitle: document.querySelector('#quest-title'),
  questCopy: document.querySelector('#quest-copy'),
  questFill: document.querySelector('#quest-fill'),
  townOre: document.querySelector('#town-ore'),
  townWood: document.querySelector('#town-wood'),
  townHerb: document.querySelector('#town-herb'),
  townEssence: document.querySelector('#town-essence'),
  blacksmithName: document.querySelector('#blacksmith-name'),
  blacksmithStatus: document.querySelector('#blacksmith-status'),
  blacksmithUpgrade: document.querySelector('#blacksmith-upgrade'),
  craftingList: document.querySelector('#crafting-list'),
  exportProgress: document.querySelector('#export-progress'),
  importProgress: document.querySelector('#import-progress'),
  saveStatus: document.querySelector('#save-status'),
  campaignMap: document.querySelector('#campaign-map'),
  mapClose: document.querySelector('#map-close'),
  mapToggle: document.querySelector('#map-toggle'),
  routeList: document.querySelector('#route-list'),
  routeDetail: document.querySelector('#route-detail'),
  routeConfirm: document.querySelector('#route-confirm'),
  soundToggle: document.querySelector('#sound-toggle'),
  soundLabel: document.querySelector('#sound-label'),
  companionList: document.querySelector('#companion-list'),
  tutorialCard: document.querySelector('#tutorial-card'),
  tutorialNext: document.querySelector('#tutorial-next'),
  tutorialProgress: document.querySelector('#tutorial-progress'),
  recoveryCard: document.querySelector('#recovery-card'),
  recoveryResources: document.querySelector('#recovery-resources'),
  recoveryTime: document.querySelector('#recovery-time'),
  recoverCache: document.querySelector('#recover-cache'),
  expeditionBanner: document.querySelector('#expedition-banner'),
  expeditionState: document.querySelector('#expedition-state'),
  expeditionCopy: document.querySelector('#expedition-copy'),
  bossRewardPanel: document.querySelector('#boss-reward-panel'),
  bossRewardList: document.querySelector('#boss-reward-list'),
  defeatPanel: document.querySelector('#defeat-panel'),
  defeatCopy: document.querySelector('#defeat-copy'),
  defeatResources: document.querySelector('#defeat-resources'),
  returnAfterDefeat: document.querySelector('#return-after-defeat'),
  targetCard: document.querySelector('#target-card'),
  targetName: document.querySelector('#target-name'),
  targetKind: document.querySelector('#target-kind'),
  enemyHealthFill: document.querySelector('#enemy-health-fill'),
  gatherCard: document.querySelector('#gather-card'),
  gatherState: document.querySelector('#gather-state'),
  gatherName: document.querySelector('#gather-name'),
  gatherFill: document.querySelector('#gather-fill'),
  hint: document.querySelector('#command-hint'),
  toast: document.querySelector('#status-toast')
};

const app = new pc.Application(canvas, {
  mouse: new pc.Mouse(canvas),
  touch: new pc.TouchDevice(canvas),
  keyboard: null,
  gamepads: null,
  graphicsDeviceOptions: {
    antialias: true,
    alpha: false,
    powerPreference: 'high-performance'
  }
});

app.setCanvasFillMode(pc.FILLMODE_FILL_WINDOW);
app.setCanvasResolution(pc.RESOLUTION_AUTO);
app.scene.ambientLight = new pc.Color(0.2, 0.23, 0.19);
app.scene.exposure = 1.06;
app.scene.gammaCorrection = pc.GAMMA_SRGB;
app.scene.toneMapping = pc.TONEMAP_ACES;
app.scene.fog.type = pc.FOG_LINEAR;
app.scene.fog.color = new pc.Color(0.055, 0.085, 0.08);
app.scene.fog.start = 24;
app.scene.fog.end = 64;
app.start();

const outlineLayer = app.scene.layers.getLayerByName('Immediate');
const enemyOutlineRenderer = new pc.OutlineRenderer(app, outlineLayer);
const enemyOutlineColor = new pc.Color(1, 0.012, 0.006);
const targetOutlineColor = new pc.Color(1, 1, 1);
configureOutlineOpacity(enemyOutlineRenderer, 0.2);
let enemyOutlineTimer = 0;

const PALETTE = {
  ground: material('ground', [0.105, 0.135, 0.12], { gloss: 0.12 }),
  groundDark: material('groundDark', [0.065, 0.08, 0.072], { gloss: 0.08 }),
  stone: material('stone', [0.19, 0.22, 0.2], { gloss: 0.18 }),
  stoneDark: material('stoneDark', [0.10, 0.12, 0.115], { gloss: 0.14 }),
  bark: material('bark', [0.17, 0.115, 0.075], { gloss: 0.08 }),
  leaves: material('leaves', [0.08, 0.20, 0.13], { gloss: 0.12 }),
  leavesPale: material('leavesPale', [0.18, 0.28, 0.18], { gloss: 0.12 }),
  heroCoat: material('heroCoat', [0.17, 0.29, 0.27], { gloss: 0.28 }),
  heroClothLight: material('heroClothLight', [0.25, 0.43, 0.39], { gloss: 0.22 }),
  heroLeather: material('heroLeather', [0.22, 0.135, 0.08], { gloss: 0.26 }),
  leatherDark: material('leatherDark', [0.105, 0.058, 0.035], { gloss: 0.24 }),
  brass: material('brass', [0.42, 0.29, 0.105], { metalness: 0.58, gloss: 0.62 }),
  skin: material('skin', [0.55, 0.34, 0.22], { gloss: 0.25 }),
  hair: material('hair', [0.12, 0.07, 0.045], { gloss: 0.16 }),
  steel: material('steel', [0.38, 0.43, 0.42], { metalness: 0.65, gloss: 0.74 }),
  ember: material('ember', [0.65, 0.14, 0.055], { emissive: [0.5, 0.055, 0.012], gloss: 0.5 }),
  enemyHide: material('enemyHide', [0.27, 0.10, 0.075], { gloss: 0.2 }),
  enemyBone: material('enemyBone', [0.48, 0.44, 0.34], { gloss: 0.12 }),
  enemyBoneLight: material('enemyBoneLight', [0.68, 0.62, 0.46], { gloss: 0.18 }),
  enemyDark: material('enemyDark', [0.085, 0.055, 0.052], { gloss: 0.1 }),
  moss: material('moss', [0.17, 0.25, 0.10], { gloss: 0.08 }),
  ring: material('ring', [0.72, 0.56, 0.20], { emissive: [0.22, 0.12, 0.018], opacity: 0.55 }),
  dangerRing: material('dangerRing', [0.58, 0.08, 0.035], { emissive: [0.18, 0.012, 0.004], opacity: 0.5 }),
  companionAura: material('companionAura', [0.28, 0.68, 0.48], { emissive: [0.05, 0.24, 0.12], opacity: 0.5 }),
  loot: material('loot', [0.18, 0.62, 0.72], { emissive: [0.03, 0.35, 0.5], gloss: 0.88 }),
  shardIce: material('shardIce', [0.9, 0.97, 1], { emissive: [0.42, 0.72, 0.92], gloss: 0.96 }),
  frostNova: material('frostNova', [0.7, 0.94, 1], { emissive: [0.24, 0.68, 0.92], opacity: 0.72, gloss: 0.9 }),
  glacialWard: material('glacialWard', [0.76, 0.95, 1], { emissive: [0.16, 0.5, 0.72], opacity: 0.3, gloss: 0.94 }),
  itemCommon: material('itemCommon', [0.72, 0.76, 0.7], { emissive: [0.08, 0.1, 0.08], gloss: 0.55 }),
  itemRare: material('itemRare', [0.36, 0.82, 0.96], { emissive: [0.08, 0.48, 0.72], gloss: 0.92 }),
  resourceOre: material('resourceOre', [0.54, 0.64, 0.68], { emissive: [0.12, 0.22, 0.26], opacity: 0.72, gloss: 0.5 }),
  resourceWood: material('resourceWood', [0.55, 0.34, 0.17], { emissive: [0.14, 0.07, 0.02], opacity: 0.72 }),
  resourceHerb: material('resourceHerb', [0.4, 0.72, 0.34], { emissive: [0.08, 0.25, 0.06], opacity: 0.72 }),
  resourceEssence: material('resourceEssence', [0.48, 0.9, 0.96], { emissive: [0.12, 0.55, 0.68], opacity: 0.75, gloss: 0.9 }),
  equipmentIce: material('equipmentIce', [0.62, 0.9, 1], { emissive: [0.08, 0.32, 0.48], gloss: 0.9 }),
  hit: material('hit', [1, 0.38, 0.09], { emissive: [1, 0.13, 0.01], opacity: 0.75 })
};

const ICE_SHARD = {
  name: 'Icy white',
  material: PALETTE.shardIce,
  light: [0.78, 0.92, 1]
};

function configureOutlineOpacity(renderer, opacity) {
  const shader = pc.ShaderUtils.createShader(app.graphicsDevice, {
    uniqueName: `EnemyOutlineBlend${Math.round(opacity * 100)}`,
    attributes: { vertex_position: pc.SEMANTIC_POSITION },
    vertexChunk: 'fullscreenQuadVS',
    fragmentGLSL: `
      varying vec2 vUv0;
      uniform sampler2D source;
      void main(void) {
        vec4 outline = texture2D(source, vUv0);
        gl_FragColor = vec4(outline.rgb, outline.a * ${opacity.toFixed(3)});
      }
    `,
    fragmentWGSL: `
      varying vUv0: vec2f;
      var source: texture_2d<f32>;
      var sourceSampler: sampler;
      @fragment
      fn fragmentMain(input: FragmentInput) -> FragmentOutput {
        var output: FragmentOutput;
        let outline = textureSample(source, sourceSampler, input.vUv0);
        output.color = vec4f(outline.rgb, outline.a * ${opacity.toFixed(3)});
        return output;
      }
    `
  });
  renderer.quadRenderer.destroy();
  renderer.shaderBlend = shader;
  renderer.quadRenderer = new pc.QuadRender(shader);
}

const world = new pc.Entity('Gloamwood Verge');
app.root.addChild(world);
const staticColliders = [];
const occluders = [];

createLighting();
createWorld();

const camera = new pc.Entity('Isometric Camera');
camera.addComponent('camera', {
  projection: pc.PROJECTION_ORTHOGRAPHIC,
  orthoHeight: window.innerWidth < 680 ? 15 : 18,
  nearClip: 0.1,
  farClip: 140,
  clearColor: new pc.Color(0.035, 0.05, 0.047)
});
app.root.addChild(camera);

const cameraFocus = new pc.Vec3(0, 0.8, 0);
const cameraOffset = new pc.Vec3(15.5, 20, 17.5);
positionCamera();

const heroRig = createHumanoid('Mara', false);
const hero = {
  entity: heroRig.root,
  rig: heroRig,
  hp: 100,
  maxHp: 100,
  focus: 100,
  maxFocus: 100,
  focusRegen: 8,
  collisionRadius: 0.42,
  speed: 5.1,
  attackRange: 7.25,
  attackDamage: 26,
  attackDuration: 0.68,
  attackCooldown: 0,
  attackElapsed: 0,
  damageApplied: false,
  targetEnemy: null,
  moveTarget: null,
  aimPoint: new pc.Vec3(0, 0, -4),
  auto: false,
  loot: 0,
  state: 'idle',
  animationTime: 0,
  invulnerable: 0,
  avoidanceSide: 1,
  abilities: createAbilityState(),
  activeAbility: null,
  requestedAbility: null,
  wardRemaining: 0,
  wardAbsorb: 0,
  inventory: [],
  equipment: { weapon: null, armor: null, helmet: null, accessory: null, gadget: null },
  gearStats: deriveGearStats([], {}),
  resources: createResourceStock(),
  gatherTarget: null,
  gatherElapsed: 0,
  gatherDuration: 0,
  refugeTarget: null
};
hero.entity.setPosition(0, 0, 1);
world.addChild(hero.entity);
createDottedRing(hero.entity, 0.72, PALETTE.ring, 10, 'Hero footing');
const wardVisual = createWardVisual(hero.entity);

const companion = {
  rig: createHumanoid('Active companion', false),
  entity: null,
  speed: 4.1,
  collisionRadius: 0.46,
  state: 'idle',
  animationTime: 0,
  attackCooldown: 0,
  attackElapsed: 0
};
companion.entity = companion.rig.root;
companion.entity.setPosition(-1.6, 0, 2.2);
companion.entity.setLocalScale(0.91, 0.91, 0.91);
world.addChild(companion.entity);
createDottedRing(companion.entity, 0.62, PALETTE.companionAura, 8, 'Companion footing');

const selector = primitive('cylinder', 'Command marker', world, [0.64, 0.018, 0.64], [0, 0.025, 0], PALETTE.ring, false);
selector.enabled = false;

const mobs = [];
const drops = [];
const resourceNodes = [];
let refugeInteraction = null;
let townProgress = createTownProgress();
let expeditionProgress = createExpeditionProgress();
let pendingBossRewards = [];
let routeProgress = createRouteProgress();
let companionRoster = createCompanionRoster();
let discoveryProgress = createDiscoveryProgress();
let soundEnabled = true;
const audio = createAudioDirector();
let overseer = null;
let overseerArena = null;
const REFUGE_POSITION = new pc.Vec3(-7.2, 0, 9.55);
const REFUGE_SAFE_RADIUS = 6.5;
const effects = [];
const projectiles = [];
const spawnZones = [
  { center: new pc.Vec3(-13, 0, -10), name: 'Ashen Hollow', count: 4, enemyType: 'gravebound' },
  { center: new pc.Vec3(13, 0, -12), name: 'Broken Causeway', count: 5, enemyType: 'stalker' },
  { center: new pc.Vec3(14, 0, 12), name: 'Old Cairns', count: 4, enemyType: 'bulwark' },
  { center: new pc.Vec3(-14, 0, 14), name: 'Rootbound Grove', count: 5, enemyType: 'stalker' }
];

for (const zone of spawnZones) {
  createSpawnMarker(zone);
  for (let i = 0; i < zone.count; i += 1) {
    const angle = (i / zone.count) * Math.PI * 2 + seededRandom() * 0.45;
    const radius = 2.1 + seededRandom() * 2.6;
    const spawn = new pc.Vec3(
      zone.center.x + Math.cos(angle) * radius,
      0,
      zone.center.z + Math.sin(angle) * radius
    );
    mobs.push(createMob(zone, spawn, i));
  }
}

const overseerZone = { center: new pc.Vec3(0, 0, -25), name: 'Overseer Hollow', count: 1 };
overseerArena = createSpawnMarker(overseerZone);
overseerArena.enabled = false;
overseer = createMob(overseerZone, overseerZone.center.clone(), 0);
overseer.name = 'Corrupted Mine Overseer';
overseer.kind = 'HOLLOW VEIN OVERSEER';
overseer.isBoss = true;
overseer.hp = 430;
overseer.maxHp = 430;
overseer.collisionRadius = 0.78;
overseer.speed = 2.05;
overseer.attackDamage = 13;
overseer.entity.enabled = false;
mobs.push(overseer);

void loadProductionAssets();

let toastTimer = 0;
let elapsed = 0;
let firstCommand = true;
let itemSerial = 0;

canvas.addEventListener('pointerdown', handlePointer, { passive: false });
canvas.addEventListener('pointermove', handlePointerMove, { passive: true });
ui.autoToggle.addEventListener('pointerdown', (event) => event.stopPropagation());
ui.autoToggle.addEventListener('click', () => {
  hero.auto = !hero.auto;
  ui.autoToggle.setAttribute('aria-pressed', String(hero.auto));
  ui.autoLabel.textContent = hero.auto ? 'ON' : 'OFF';
  showToast(hero.auto ? 'Auto combat engaged' : 'Direct command restored');
  if (!hero.auto && hero.targetEnemy && !hero.targetEnemy.alive) clearTarget();
});
ui.abilityList.addEventListener('pointerdown', (event) => event.stopPropagation());
ui.abilityList.addEventListener('click', handleAbilityPanelClick);
ui.inventoryToggle.addEventListener('pointerdown', (event) => event.stopPropagation());
ui.inventoryClose.addEventListener('pointerdown', (event) => event.stopPropagation());
ui.inventoryPanel.addEventListener('pointerdown', (event) => event.stopPropagation());
ui.inventoryToggle.addEventListener('click', () => setInventoryOpen(ui.inventoryPanel.hidden));
ui.inventoryClose.addEventListener('click', () => setInventoryOpen(false));
ui.inventoryGrid.addEventListener('click', handleInventoryClick);
ui.refugeToggle.addEventListener('pointerdown', (event) => event.stopPropagation());
ui.refugeClose.addEventListener('pointerdown', (event) => event.stopPropagation());
ui.refugePanel.addEventListener('pointerdown', (event) => event.stopPropagation());
ui.refugeToggle.addEventListener('click', commandRefugeReturn);
ui.refugeClose.addEventListener('click', () => setRefugeOpen(false));
ui.blacksmithUpgrade.addEventListener('click', handleBlacksmithUpgrade);
ui.craftingList.addEventListener('click', handleCraftingClick);
ui.exportProgress.addEventListener('click', exportProgressSave);
ui.importProgress.addEventListener('change', importProgressSave);
ui.recoverCache.addEventListener('click', handleRecoveryClaim);
ui.bossRewardPanel.addEventListener('pointerdown', (event) => event.stopPropagation());
ui.bossRewardList.addEventListener('click', handleBossRewardChoice);
ui.returnAfterDefeat.addEventListener('click', handleReturnAfterDefeat);
ui.mapToggle.addEventListener('pointerdown', (event) => event.stopPropagation());
ui.mapToggle.addEventListener('click', () => setCampaignMapOpen(true));
ui.mapClose.addEventListener('click', () => setCampaignMapOpen(false));
ui.routeList.addEventListener('click', handleRouteSelection);
ui.routeConfirm.addEventListener('click', handleRouteConfirmation);
ui.soundToggle.addEventListener('pointerdown', (event) => event.stopPropagation());
ui.soundToggle.addEventListener('click', toggleSound);
ui.companionList.addEventListener('click', handleCompanionSelection);
restoreProgress();
syncBossAvailability();
renderAbilityPanel();
renderInventory();
renderRefugePanel();
renderBossRewardPanel();
renderRouteMap();
renderTutorial();
setCampaignMapOpen(routeProgress.location === 'refuge' && routeProgress.completedRouteIds.length === 0);

window.addEventListener('resize', () => {
  app.resizeCanvas();
  camera.camera.orthoHeight = window.innerWidth < 680 ? 15 : 18;
});
window.addEventListener('pagehide', () => persistProgress());

app.on('update', (dt) => {
  const safeDt = Math.min(dt, 0.05);
  elapsed += safeDt;
  if (!isSimulationPaused()) {
    updateHero(safeDt);
    updateCompanion(safeDt);
    updateMobs(safeDt);
    resolveMobSeparation();
    updateResourceNodes(safeDt);
    updateRefugeVisual(safeDt);
    updateDrops(safeDt);
    updateProjectiles(safeDt);
    updateEffects(safeDt);
  }
  updateCamera(safeDt);
  updateOccluders(safeDt);
  updateEnemyOutlines(safeDt);
  updateInterface(safeDt);
});

function isSimulationPaused() {
  return !ui.campaignMap.hidden
    || expeditionProgress.rewardPending
    || expeditionProgress.state === 'defeated';
}

setTimeout(() => showToast('Tap the world to begin'), 500);
setTimeout(() => { ui.hint.style.opacity = '0.28'; }, 8500);

if ('serviceWorker' in navigator && import.meta.env.PROD) {
  window.addEventListener('load', () => {
    void navigator.serviceWorker.register(new URL('./service-worker.js', location.href));
  });
}

if (import.meta.env.DEV) {
  window.__ashenGame = {
    resourceScreens: () => resourceNodes.map((node) => {
      const screen = camera.camera.worldToScreen(node.root.getPosition());
      return { type: node.type, x: screen.x, y: screen.y, available: node.available };
    })
  };
}

function material(name, color, options = {}) {
  const result = new pc.StandardMaterial();
  result.name = name;
  result.diffuse = new pc.Color(...color);
  result.metalness = options.metalness ?? 0;
  result.gloss = options.gloss ?? 0.25;
  if (options.emissive) result.emissive = new pc.Color(...options.emissive);
  if (options.opacity !== undefined) {
    result.opacity = options.opacity;
    result.blendType = pc.BLEND_NORMAL;
    result.depthWrite = false;
  }
  result.update();
  return result;
}

function primitive(type, name, parent, scale, position, primitiveMaterial, shadows = true) {
  const entity = new pc.Entity(name);
  entity.addComponent('render', {
    type,
    castShadows: shadows,
    receiveShadows: shadows
  });
  entity.render.material = primitiveMaterial;
  entity.setLocalScale(...scale);
  entity.setLocalPosition(...position);
  parent.addChild(entity);
  return entity;
}

function createLighting() {
  const sun = new pc.Entity('Cold afternoon sun');
  sun.addComponent('light', {
    type: 'directional',
    color: new pc.Color(0.94, 0.82, 0.64),
    intensity: 1.25,
    castShadows: true,
    shadowBias: 0.18,
    normalOffsetBias: 0.12,
    shadowDistance: 55,
    shadowResolution: 2048
  });
  sun.setEulerAngles(52, -38, -18);
  app.root.addChild(sun);

  const fill = new pc.Entity('Forest fill');
  fill.addComponent('light', {
    type: 'directional',
    color: new pc.Color(0.16, 0.23, 0.18),
    intensity: 0.34,
    castShadows: false
  });
  fill.setEulerAngles(-35, 130, 0);
  app.root.addChild(fill);
}

function createWorld() {
  primitive('plane', 'Ground', world, [72, 1, 72], [0, -0.04, 0], PALETTE.ground, true);

  for (let i = 0; i < 76; i += 1) {
    const x = seededRandom() * 62 - 31;
    const z = seededRandom() * 62 - 31;
    if (Math.abs(x - Math.sin(z * 0.19) * 2.2) < 5) continue;
    if (i % 3 === 0) createRock(x, z, 0.65 + seededRandom() * 1.2);
    else createTree(x, z, 0.75 + seededRandom() * 0.65, i % 5 === 0);
  }

  createRuins(-6, -19);
  createRuins(22, 2);
  createRuins(-23, 7);

  for (let i = 0; i < 34; i += 1) {
    const x = seededRandom() * 58 - 29;
    const z = seededRandom() * 58 - 29;
    if (i % 7 === 0) createCrystalCluster(x, z, 0.7 + seededRandom() * 0.5);
    else createForestFloorCluster(x, z, 0.65 + seededRandom() * 0.55);
  }
}

function createTree(x, z, scale, pale) {
  const root = new pc.Entity('Gnarled tree');
  root.setPosition(x, 0, z);
  root.setEulerAngles(0, seededRandom() * 360, seededRandom() * 5 - 2.5);
  world.addChild(root);
  primitive('cylinder', 'Trunk', root, [0.44 * scale, 2.9 * scale, 0.44 * scale], [0, 1.42 * scale, 0], PALETTE.bark);
  primitive('cone', 'Crown low', root, [2.2 * scale, 2.8 * scale, 2.2 * scale], [0, 3.15 * scale, 0], pale ? PALETTE.leavesPale : PALETTE.leaves);
  primitive('cone', 'Crown high', root, [1.55 * scale, 2.3 * scale, 1.55 * scale], [0.1, 4.35 * scale, 0], PALETTE.leaves);
  proceduralTrees.push(root);
  registerStaticCollider(root, 0.48 * scale);
  registerOccluder(root, 1.45 * scale);
}

function createRock(x, z, scale) {
  const rock = primitive('sphere', 'Moss stone', world, [scale, scale * 0.62, scale * 0.78], [x, scale * 0.3, z], PALETTE.stone);
  rock.setLocalEulerAngles(seededRandom() * 25, seededRandom() * 180, seededRandom() * 25);
  primitive('sphere', 'Moss cap', rock, [0.72, 0.12, 0.62], [0, 0.72, 0], PALETTE.moss, false);
  proceduralRocks.push(rock);
  registerStaticCollider(rock, 0.64 * scale);
}

function createRuins(x, z) {
  const root = new pc.Entity('Ruined waystone');
  root.setPosition(x, 0, z);
  root.setEulerAngles(0, seededRandom() * 180, 0);
  world.addChild(root);
  proceduralRuins.push(root);
  primitive('box', 'Foundation', root, [4.5, 0.22, 3.2], [0, 0.1, 0], PALETTE.stoneDark);
  primitive('box', 'Standing wall', root, [3.8, 2.5, 0.38], [0, 1.25, -1.2], PALETTE.stone);
  primitive('box', 'Broken wall', root, [0.38, 1.4, 2.2], [-1.7, 0.7, -0.2], PALETTE.stone);
  primitive('box', 'Fallen block', root, [1.4, 0.55, 0.7], [1.2, 0.3, 1.3], PALETTE.stone)
    .setLocalEulerAngles(8, 22, 12);
  primitive('box', 'Moss ledge', root, [3.55, 0.10, 0.48], [0, 2.34, -1.17], PALETTE.moss, false);
  primitive('box', 'Carved pillar', root, [0.62, 3.1, 0.62], [1.48, 1.55, -1.05], PALETTE.stoneDark);
  primitive('box', 'Pillar capital', root, [0.88, 0.28, 0.88], [1.48, 3.02, -1.05], PALETTE.stone);
  registerStaticCollider(root, 1.75, [-0.3, 0, -0.75]);
}

function createForestFloorCluster(x, z, scale) {
  const root = new pc.Entity('Forest floor foliage');
  root.setPosition(x, 0, z);
  root.setEulerAngles(0, seededRandom() * 360, 0);
  world.addChild(root);
  for (let i = 0; i < 5; i += 1) {
    const angle = (i / 5) * Math.PI * 2 + seededRandom() * 0.35;
    const blade = primitive(
      'cone',
      'Fern blade',
      root,
      [0.16 * scale, (0.55 + seededRandom() * 0.35) * scale, 0.38 * scale],
      [Math.cos(angle) * 0.34 * scale, 0.25 * scale, Math.sin(angle) * 0.34 * scale],
      i % 2 ? PALETTE.leaves : PALETTE.moss,
      false
    );
    blade.setLocalEulerAngles(18 + seededRandom() * 18, -angle * pc.math.RAD_TO_DEG, seededRandom() * 15 - 7);
  }
}

function createCrystalCluster(x, z, scale) {
  const root = new pc.Entity('Blight crystal cluster');
  root.setPosition(x, 0, z);
  root.setEulerAngles(0, seededRandom() * 360, 0);
  world.addChild(root);
  primitive('sphere', 'Crystal bed', root, [0.72 * scale, 0.20 * scale, 0.62 * scale], [0, 0.08, 0], PALETTE.stoneDark);
  for (let i = 0; i < 4; i += 1) {
    const angle = (i / 4) * Math.PI * 2;
    const height = (0.65 + seededRandom() * 0.75) * scale;
    const crystal = primitive(
      'cone',
      'Cyan crystal',
      root,
      [0.22 * scale, height, 0.22 * scale],
      [Math.cos(angle) * 0.28 * scale, height * 0.48, Math.sin(angle) * 0.28 * scale],
      PALETTE.loot,
      false
    );
    crystal.setLocalEulerAngles(seededRandom() * 15 - 7, angle * pc.math.RAD_TO_DEG, seededRandom() * 12 - 6);
  }
  registerStaticCollider(root, 0.58 * scale);
}

function createHumanoid(name, enemy) {
  const root = new pc.Entity(name);
  const slots = {
    head: new pc.Entity('Head equipment slot'),
    shoulders: new pc.Entity('Shoulder equipment slot'),
    back: new pc.Entity('Back equipment slot'),
    belt: new pc.Entity('Belt equipment slot'),
    weapon: new pc.Entity('Weapon equipment slot')
  };
  const scale = enemy ? 0.9 : 1;
  root.setLocalScale(scale, scale, scale);

  const hips = primitive('box', 'Hips', root, [0.66, 0.35, 0.4], [0, 0.88, 0], enemy ? PALETTE.enemyDark : PALETTE.heroLeather);
  const torso = primitive('capsule', 'Torso', root, [0.66, 0.88, 0.48], [0, 1.48, 0], enemy ? PALETTE.enemyHide : PALETTE.heroCoat);
  const head = primitive('sphere', 'Head', root, [0.45, 0.48, 0.43], [0, 2.22, 0], enemy ? PALETTE.enemyBone : PALETTE.skin);
  primitive('sphere', 'Hair', head, [1.04, 0.48, 1.03], [0, 0.30, -0.01], enemy ? PALETTE.enemyDark : PALETTE.hair);
  slots.head.setLocalPosition(0, 2.22, 0);
  slots.shoulders.setLocalPosition(0, 1.78, 0);
  slots.back.setLocalPosition(0, 1.48, -0.32);
  slots.belt.setLocalPosition(0, 1.08, 0);
  root.addChild(slots.head);
  root.addChild(slots.shoulders);
  root.addChild(slots.back);
  root.addChild(slots.belt);

  if (enemy) {
    const eyeL = primitive('sphere', 'Eye L', head, [0.09, 0.06, 0.05], [-0.17, 0.04, 0.40], PALETTE.ember, false);
    const eyeR = primitive('sphere', 'Eye R', head, [0.09, 0.06, 0.05], [0.17, 0.04, 0.40], PALETTE.ember, false);
    eyeL.setLocalScale(0.09, 0.06, 0.05);
    eyeR.setLocalScale(0.09, 0.06, 0.05);
    primitive('box', 'Bone mask', slots.head, [0.50, 0.46, 0.15], [0, 0, 0.38], PALETTE.enemyBoneLight);
    primitive('cone', 'Mask horn L', slots.head, [0.13, 0.7, 0.13], [-0.32, 0.40, 0.06], PALETTE.enemyBoneLight)
      .setLocalEulerAngles(0, 0, 25);
    primitive('cone', 'Mask horn R', slots.head, [0.13, 0.7, 0.13], [0.32, 0.40, 0.06], PALETTE.enemyBoneLight)
      .setLocalEulerAngles(0, 0, -25);
    primitive('cone', 'Shoulder thorn L', slots.shoulders, [0.16, 0.58, 0.16], [-0.56, 0.18, 0], PALETTE.enemyBone)
      .setLocalEulerAngles(0, 0, 38);
    primitive('cone', 'Shoulder thorn R', slots.shoulders, [0.16, 0.58, 0.16], [0.56, 0.18, 0], PALETTE.enemyBone)
      .setLocalEulerAngles(0, 0, -38);
    primitive('box', 'Tattered mantle', slots.back, [0.72, 0.95, 0.10], [0, -0.35, 0], PALETTE.enemyDark)
      .setLocalEulerAngles(10, 0, 0);
  } else {
    primitive('capsule', 'Ponytail', slots.head, [0.16, 0.62, 0.16], [0, 0.08, -0.47], PALETTE.hair)
      .setLocalEulerAngles(72, 0, 0);
    primitive('cylinder', 'Wayfarer scarf', root, [0.54, 0.18, 0.54], [0, 1.92, 0], PALETTE.heroClothLight);
    primitive('box', 'Left lapel', root, [0.16, 0.62, 0.08], [-0.18, 1.52, 0.43], PALETTE.heroClothLight)
      .setLocalEulerAngles(0, 0, -12);
    primitive('box', 'Right lapel', root, [0.16, 0.62, 0.08], [0.18, 1.52, 0.43], PALETTE.heroClothLight)
      .setLocalEulerAngles(0, 0, 12);
    primitive('box', 'Shoulder guard', slots.shoulders, [0.62, 0.18, 0.56], [-0.42, 0.02, 0], PALETTE.brass)
      .setLocalEulerAngles(0, 0, -12);
    primitive('box', 'Cross belt', root, [0.11, 1.12, 0.08], [0.05, 1.50, 0.46], PALETTE.heroLeather)
      .setLocalEulerAngles(0, 0, -28);
    primitive('box', 'Waist belt', slots.belt, [0.78, 0.12, 0.46], [0, 0, 0], PALETTE.leatherDark);
    primitive('box', 'Belt buckle', slots.belt, [0.18, 0.18, 0.08], [0, 0, 0.27], PALETTE.brass);
    primitive('box', 'Utility pouch', slots.belt, [0.30, 0.42, 0.22], [0.46, -0.18, 0.02], PALETTE.heroLeather)
      .setLocalEulerAngles(0, 0, -8);
    primitive('box', 'Coat tail L', slots.back, [0.34, 1.15, 0.12], [-0.20, -0.47, 0], PALETTE.heroCoat)
      .setLocalEulerAngles(10, 0, 7);
    primitive('box', 'Coat tail R', slots.back, [0.34, 1.15, 0.12], [0.20, -0.47, 0], PALETTE.heroCoat)
      .setLocalEulerAngles(10, 0, -7);
  }

  const leftLeg = new pc.Entity('Left leg joint');
  leftLeg.setLocalPosition(-0.22, 0.8, 0);
  root.addChild(leftLeg);
  primitive('capsule', 'Left leg', leftLeg, [0.25, 0.76, 0.25], [0, -0.38, 0], enemy ? PALETTE.enemyDark : PALETTE.heroLeather);
  if (!enemy) primitive('box', 'Left boot', leftLeg, [0.30, 0.38, 0.42], [0, -0.70, 0.10], PALETTE.leatherDark);

  const rightLeg = new pc.Entity('Right leg joint');
  rightLeg.setLocalPosition(0.22, 0.8, 0);
  root.addChild(rightLeg);
  primitive('capsule', 'Right leg', rightLeg, [0.25, 0.76, 0.25], [0, -0.38, 0], enemy ? PALETTE.enemyDark : PALETTE.heroLeather);
  if (!enemy) primitive('box', 'Right boot', rightLeg, [0.30, 0.38, 0.42], [0, -0.70, 0.10], PALETTE.leatherDark);

  const leftArm = new pc.Entity('Left shoulder');
  leftArm.setLocalPosition(-0.5, 1.75, 0);
  root.addChild(leftArm);
  primitive('capsule', 'Left arm', leftArm, [0.21, 0.72, 0.21], [0, -0.35, 0], enemy ? PALETTE.enemyHide : PALETTE.heroCoat);

  const rightArm = new pc.Entity('Right shoulder');
  rightArm.setLocalPosition(0.5, 1.75, 0);
  root.addChild(rightArm);
  primitive('capsule', 'Right arm', rightArm, [0.21, 0.72, 0.21], [0, -0.35, 0], enemy ? PALETTE.enemyHide : PALETTE.heroCoat);

  const weaponPivot = new pc.Entity('Weapon grip');
  weaponPivot.setLocalPosition(0, -0.7, 0.02);
  rightArm.addChild(weaponPivot);
  slots.weapon.setLocalPosition(0, 0, 0);
  weaponPivot.addChild(slots.weapon);
  if (enemy) {
    primitive('box', 'Claw', slots.weapon, [0.18, 0.16, 0.7], [0, 0, 0.31], PALETTE.enemyBone);
    for (let i = -1; i <= 1; i += 1) {
      primitive('cone', 'Claw blade', slots.weapon, [0.07, 0.56, 0.07], [i * 0.11, -0.08, 0.72], PALETTE.enemyBoneLight)
        .setLocalEulerAngles(90, 0, 0);
    }
  } else {
    primitive('cylinder', 'Sword grip', slots.weapon, [0.09, 0.48, 0.09], [0, -0.08, 0], PALETTE.heroLeather);
    primitive('box', 'Sword crossguard', slots.weapon, [0.52, 0.08, 0.10], [0, -0.34, 0], PALETTE.brass);
    primitive('sphere', 'Sword pommel', slots.weapon, [0.13, 0.13, 0.13], [0, 0.22, 0], PALETTE.brass);
    const blade = primitive('box', 'Sword blade', slots.weapon, [0.13, 1.25, 0.06], [0, -0.94, 0], PALETTE.steel);
    blade.setLocalEulerAngles(0, 0, 0);
  }

  return { root, hips, torso, head, leftLeg, rightLeg, leftArm, rightArm, weaponPivot, slots };
}

function createSpawnMarker(zone) {
  const root = new pc.Entity(`${zone.name} corruption`);
  root.setPosition(zone.center);
  world.addChild(root);
  createDottedRing(root, 3.8, PALETTE.dangerRing, 14, 'Corrupted ground');
  for (let i = 0; i < 4; i += 1) {
    const angle = (i / 4) * Math.PI * 2 + 0.4;
    const shard = primitive(
      'cone',
      'Blight thorn',
      root,
      [0.18, 0.55, 0.18],
      [Math.cos(angle) * 3.25, 0.27, Math.sin(angle) * 3.25],
      PALETTE.ember,
      false
    );
    shard.setLocalEulerAngles(8, angle * pc.math.RAD_TO_DEG, 10);
  }
  return root;
}

function createDottedRing(parent, radius, ringMaterial, count, name) {
  for (let i = 0; i < count; i += 1) {
    const angle = (i / count) * Math.PI * 2;
    const dash = primitive(
      'box',
      name,
      parent,
      [0.38, 0.018, 0.08],
      [Math.cos(angle) * radius, 0.022, Math.sin(angle) * radius],
      ringMaterial,
      false
    );
    dash.setLocalEulerAngles(0, -angle * pc.math.RAD_TO_DEG, 0);
  }
}

function createWardVisual(parent) {
  const root = new pc.Entity('Glacial Ward');
  root.enabled = false;
  parent.addChild(root);
  for (let index = 0; index < 8; index += 1) {
    const angle = (index / 8) * Math.PI * 2;
    const crystal = primitive(
      'cone',
      'Ward crystal',
      root,
      [0.1, 0.42, 0.1],
      [Math.cos(angle) * 0.9, 0.7 + (index % 2) * 0.28, Math.sin(angle) * 0.9],
      PALETTE.glacialWard,
      false
    );
    crystal.setLocalEulerAngles(16, -angle * pc.math.RAD_TO_DEG, 10);
  }
  createDottedRing(root, 1.05, PALETTE.glacialWard, 12, 'Ward seal');
  return root;
}

function createMob(zone, spawn, index) {
  const enemy = createEnemyStats(zone.enemyType, index === 0);
  const rig = createHumanoid(`Gravebound ${index + 1}`, true);
  rig.root.setPosition(spawn);
  world.addChild(rig.root);
  return {
    name: enemy.name,
    kind: `${enemy.kind} · ${zone.name.toUpperCase()}`,
    enemyType: enemy.type,
    visualScale: enemy.scale,
    visualTint: enemy.tint,
    entity: rig.root,
    rig,
    spawn: spawn.clone(),
    hp: enemy.maxHp,
    maxHp: enemy.maxHp,
    baseMaxHp: enemy.maxHp,
    collisionRadius: enemy.collisionRadius,
    speed: enemy.speed + seededRandom() * 0.18,
    attackRange: enemy.attackRange,
    attackDamage: enemy.attackDamage,
    baseAttackDamage: enemy.attackDamage,
    attackCooldown: seededRandom(),
    attackElapsed: 0,
    damageApplied: false,
    alive: true,
    aggro: false,
    state: 'idle',
    animationTime: seededRandom() * 10,
    wanderTimer: 1 + seededRandom() * 3,
    wanderTarget: null,
    deathElapsed: 0,
    respawnAt: 0,
    hitFlash: 0,
    avoidanceSide: index % 2 === 0 ? 1 : -1
  };
}

function handleAbilityPanelClick(event) {
  const moveButton = event.target.closest('[data-move-up]');
  if (moveButton) {
    hero.abilities.order = moveAbilityUp(hero.abilities.order, moveButton.dataset.moveUp);
    recordTraining('priority');
    renderAbilityPanel();
    persistProgress('Ability priorities saved');
    showToast('Auto-cast priority updated');
    return;
  }

  const castButton = event.target.closest('[data-cast]');
  if (!castButton) return;
  const id = castButton.dataset.cast;
  if (hero.abilities.cooldowns[id] > 0) {
    showToast(`${ABILITIES[id].name} is recovering`);
    return;
  }
  if (hero.focus < ABILITIES[id].cost) {
    showToast(`Need ${ABILITIES[id].cost} focus for ${ABILITIES[id].name}`);
    return;
  }
  if (ABILITIES[id].requiresTarget && !hero.targetEnemy?.alive) {
    showToast(`Select an enemy for ${ABILITIES[id].name}`);
    return;
  }
  cancelGathering();
  hero.requestedAbility = id;
  if (!ABILITIES[id].requiresTarget) {
    hero.moveTarget = null;
    selector.enabled = false;
  }
}

function renderAbilityPanel() {
  const glyphs = { glacialWard: 'W', frostNova: 'N', frostLance: 'L', iceShard: 'I' };
  ui.abilityList.innerHTML = hero.abilities.order.map((id, index) => {
    const ability = ABILITIES[id];
    return `
      <div class="ability-row" data-ability="${id}">
        <button class="ability-cast" type="button" data-cast="${id}" aria-label="Cast ${ability.name}">
          <span class="ability-rank">${index + 1}</span>
          <span class="ability-glyph">${glyphs[id]}</span>
          <span class="ability-copy">
            <strong>${ability.name}</strong>
            <small>${ability.condition} · ${ability.cost} focus</small>
          </span>
          <span class="ability-time" data-cooldown="${id}">READY</span>
        </button>
        <button class="priority-up" type="button" data-move-up="${id}" aria-label="Raise ${ability.name} priority" ${index === 0 ? 'disabled' : ''}>↑</button>
      </div>
    `;
  }).join('');
}

function setInventoryOpen(open) {
  ui.inventoryPanel.hidden = !open;
  ui.inventoryToggle.setAttribute('aria-expanded', String(open));
  if (open) setRefugeOpen(false);
}

function setRefugeOpen(open) {
  ui.refugePanel.hidden = !open;
  ui.refugeToggle.setAttribute('aria-expanded', String(open));
  if (open) {
    setInventoryOpen(false);
    recordTraining('town');
    renderRefugePanel();
  } else if (townProgress.quest.completed && expeditionProgress.state === 'hunting') {
    syncBossAvailability(true);
  }
}

function openRefugeAndDeposit() {
  const carriedTotal = Object.values(hero.resources).reduce((sum, amount) => sum + amount, 0);
  if (expeditionProgress.extractionReady) {
    const extraction = completeExtraction(expeditionProgress, hero.resources);
    if (extraction.success) {
      expeditionProgress = extraction.progress;
      const deposited = depositResources(townProgress, extraction.banked);
      townProgress = deposited.progress;
      hero.resources = deposited.carried;
      hero.loot += 12;
      const routeResult = completeActiveRoute(routeProgress);
      if (routeResult.success) applyRouteCompletion(routeResult);
      const xpResult = awardCompanionXp(companionRoster, companionRoster.activeId, 120);
      if (xpResult.success) companionRoster = xpResult.roster;
      const unlockResult = unlockCompanion(companionRoster, COMPANION_IDS.SLICE_UNLOCK, {
        sliceMilestoneCompleted: true
      });
      if (unlockResult.success) companionRoster = unlockResult.roster;
      recordTraining('extraction');
      persistProgress('Extraction completed');
      showToast(`Extraction successful · ${carriedTotal} materials and 12 shards secured`);
      audio.cue('extraction');
    }
    if (getAvailableRoutes(routeProgress).length > 0) {
      setRefugeOpen(false);
      setCampaignMapOpen(true);
    } else {
      setRefugeOpen(true);
    }
    return;
  }
  if (carriedTotal > 0 && !townProgress.quest.completed) {
    const deposited = depositResources(townProgress, hero.resources);
    townProgress = deposited.progress;
    hero.resources = deposited.carried;
    showToast(`${carriedTotal} gathered materials secured`);
    persistProgress('Resources deposited');
  } else if (carriedTotal > 0 && townProgress.quest.completed) {
    showToast('Defeat the Overseer to extract carried materials');
  }
  setRefugeOpen(true);
}

function applyRouteCompletion(result) {
  routeProgress = result.progress;
  const routeDeposit = depositResources(townProgress, result.reward.resources);
  townProgress = routeDeposit.progress;
  if (result.reward.rareReward) {
    const rewardItem = createItemFromTemplate(result.reward.rareReward.replaceAll('-', '_'), ++itemSerial);
    if (rewardItem) {
      hero.inventory.push(rewardItem);
      recordEquipmentAbility(rewardItem);
    }
  }
  if (result.reward.lore.length > 0) {
    showToast(`Lore discovered · ${result.reward.lore[0]}`);
  }
  if (result.progress.completedRouteIds.includes('forgotten-ossuary')) {
    discoveryProgress = unlockDiscovery(
      discoveryProgress,
      DISCOVERIES.FOURTH_ACTIVE_ABILITY,
      'optional-lore-branch'
    );
    ensureDiscoveredAbility();
    renderAbilityPanel();
  }
  renderInventory();
  renderRouteMap();
}

function renderRefugePanel() {
  expeditionProgress = pruneExpiredRecovery(expeditionProgress, Date.now());
  const { resources, blacksmithLevel, quest } = townProgress;
  ui.townOre.textContent = String(resources.ore);
  ui.townWood.textContent = String(resources.wood);
  ui.townHerb.textContent = String(resources.herb);
  ui.townEssence.textContent = String(resources.essence);
  ui.questFill.style.width = `${Math.min(100, (quest.stage / 4) * 100)}%`;

  const questCopy = [
    {
      title: 'Provision the refuge',
      copy: `Gather ore ${Math.min(3, quest.gathered.ore)}/3 · wood ${Math.min(3, quest.gathered.wood)}/3`
    },
    { title: 'Bring the materials home', copy: 'Return to Warden Refuge and secure the gathered supplies.' },
    { title: 'Rekindle the Cold Forge', copy: 'Restore the blacksmith with 6 ore and 4 wood.' },
    { title: 'Forge a new path', copy: 'Craft your first piece of rare equipment.' },
    { title: 'The refuge endures', copy: 'The forge burns again. Continue gathering and refining your build.' }
  ][quest.stage] ?? { title: 'The refuge endures', copy: 'Continue strengthening Mara.' };
  ui.questTitle.textContent = questCopy.title;
  ui.questCopy.textContent = questCopy.copy;

  ui.blacksmithName.textContent = blacksmithLevel > 0 ? 'Cold Forge · Level I' : 'Cold Forge';
  ui.blacksmithStatus.textContent = blacksmithLevel > 0
    ? 'Restored · rare field recipes available'
    : `Dormant · requires ${BLACKSMITH_UPGRADE.cost.ore} ore and ${BLACKSMITH_UPGRADE.cost.wood} wood`;
  ui.blacksmithUpgrade.textContent = blacksmithLevel > 0 ? 'RESTORED' : 'RESTORE';
  ui.blacksmithUpgrade.disabled = blacksmithLevel > 0 || !canAfford(resources, BLACKSMITH_UPGRADE.cost);

  const recovery = expeditionProgress.recovery;
  ui.recoveryCard.hidden = !recovery;
  if (recovery) {
    ui.recoveryResources.textContent = resourceSummary(recovery.resources);
    ui.recoveryTime.textContent = `${formatRecoveryRemaining(recovery.expiresAt)} remaining`;
  }

  ui.craftingList.innerHTML = CRAFTING_RECIPES.map((recipe) => {
    const affordable = blacksmithLevel > 0 && canAfford(resources, recipe.cost) && hero.inventory.length < 18;
    const cost = Object.entries(recipe.cost).map(([type, amount]) => `${amount} ${type}`).join(' · ');
    return `
      <article class="recipe-card">
        <span class="rarity">RARE · ${recipe.item.slot.toUpperCase()}</span>
        <strong>${recipe.item.name}</strong>
        <p>${recipe.description}<br>${recipe.item.effect}</p>
        <span class="recipe-cost">${cost}</span>
        <button class="craft-button" type="button" data-craft="${recipe.id}" ${affordable ? '' : 'disabled'}>
          ${blacksmithLevel > 0 ? 'CRAFT & EQUIP' : 'FORGE LOCKED'}
        </button>
      </article>
    `;
  }).join('');
  renderCompanionRoster();
}

function setCampaignMapOpen(open) {
  if (
    !open
    && routeProgress.location !== 'route'
    && routeProgress.completedRouteIds.length === 0
  ) {
    showToast('Confirm the Hollow Vein expedition first');
    return;
  }
  ui.campaignMap.hidden = !open;
  ui.mapToggle.setAttribute('aria-expanded', String(open));
  if (open) {
    setInventoryOpen(false);
    setRefugeOpen(false);
    renderRouteMap();
  }
}

function renderRouteMap() {
  const availableIds = new Set(getAvailableRoutes(routeProgress).map((route) => route.id));
  ui.routeList.innerHTML = ROUTES.map((route) => {
    const completed = routeProgress.completedRouteIds.includes(route.id);
    const active = routeProgress.activeRouteId === route.id;
    const available = availableIds.has(route.id);
    const selected = routeProgress.selectedRouteId === route.id;
    const state = completed
      ? 'COMPLETED'
      : active
        ? 'ACTIVE EXPEDITION'
        : available
          ? route.difficulty.toUpperCase()
          : 'LOCKED';
    const description = route.kind === 'main'
      ? 'Linear campaign route · Overseer hunt'
      : route.kind === 'optional-resource'
        ? 'Bonus resources · gathering focus'
        : 'Hard encounter · lore and rare reward';
    return `
      <button class="route-node ${selected || active ? 'selected' : ''} ${completed ? 'completed' : ''} ${available || active ? '' : 'locked'}"
        type="button" data-route="${route.id}" ${available ? '' : 'disabled'}>
        <span>${state}</span>
        <strong>${route.name}</strong>
        <small>${description}</small>
      </button>
    `;
  }).join('');

  const selected = ROUTES.find((route) => route.id === routeProgress.selectedRouteId);
  if (selected) {
    const resources = resourceSummary(selected.reward.resources);
    ui.routeDetail.innerHTML = `
      <span class="eyebrow">${selected.difficulty.toUpperCase()} · ${selected.kind.replaceAll('-', ' ').toUpperCase()}</span>
      <strong>${selected.name}</strong>
      <small>First-clear rewards: ${resources}${selected.reward.rareReward ? ' · guaranteed rare relic' : ''}</small>
    `;
  } else {
    ui.routeDetail.innerHTML = `
      <span class="eyebrow">SELECT A ROUTE</span>
      <strong>${routeProgress.location === 'route' ? 'Expedition already active' : 'No expedition selected'}</strong>
      <small>Main routes unlock optional resource and lore branches.</small>
    `;
  }
  ui.routeConfirm.disabled = !selected;
}

function handleRouteSelection(event) {
  const button = event.target.closest('[data-route]');
  if (!button) return;
  const result = selectRoute(routeProgress, button.dataset.route);
  if (!result.success) return;
  routeProgress = result.progress;
  renderRouteMap();
  persistProgress('Route selected');
  audio.cue('command');
}

function handleRouteConfirmation() {
  const result = confirmRouteSelection(routeProgress);
  if (!result.success) return;
  routeProgress = result.progress;
  if (result.route.kind !== 'main') {
    discoveryProgress = recordTutorialStep(discoveryProgress, 'branch');
  }
  setCampaignMapOpen(false);
  restoreHeroAtRefuge();
  configureRouteEncounter(result.route);
  syncBossAvailability(true);
  renderTutorial();
  persistProgress(`${result.route.name} expedition started`);
  showToast(`Entering ${result.route.name}`);
  audio.cue(result.route.difficulty === 'hard' ? 'boss' : 'command');
}

function renderCompanionRoster() {
  ui.companionList.innerHTML = Object.values(COMPANION_DEFINITIONS).map((definition) => {
    const member = companionRoster.companions[definition.id];
    const active = companionRoster.activeId === definition.id;
    return `
      <button class="companion-card ${active ? 'active' : ''}" type="button"
        data-companion="${definition.id}" ${member.unlocked ? '' : 'disabled'}>
        <span>${member.unlocked ? `${definition.combatRole.toUpperCase()} · LV ${member.level}` : 'LOCKED'}</span>
        <strong>${definition.name}</strong>
        <small>${definition.buildInfluence.name} · ${definition.identity}</small>
      </button>
    `;
  }).join('');
}

function configureRouteEncounter(route) {
  const multiplier = route.difficulty === 'hard' ? 1.3 : 1;
  for (const mob of mobs) {
    if (mob.isBoss) continue;
    mob.maxHp = Math.round((mob.baseMaxHp ?? mob.maxHp) * multiplier);
    mob.attackDamage = Math.round((mob.baseAttackDamage ?? mob.attackDamage) * multiplier);
    respawnMob(mob);
  }
  for (const node of resourceNodes) {
    node.available = true;
    node.respawnAt = 0;
    node.model.enabled = true;
    node.marker.enabled = true;
  }
  for (const drop of drops.splice(0)) drop.entity.destroy();
  for (const projectile of projectiles.splice(0)) projectile.entity.destroy();
  for (const effect of effects.splice(0)) effect.root.destroy();
  hero.resources = createResourceStock();
  hero.loot = Math.max(0, hero.loot);
  if (route.kind === 'optional-resource') {
    showToast('Ironroot Grove · rich gathering nodes detected');
  }
}

function handleCompanionSelection(event) {
  const button = event.target.closest('[data-companion]');
  if (!button) return;
  const result = swapActiveCompanion(companionRoster, button.dataset.companion, {
    inRefuge: !ui.refugePanel.hidden
  });
  if (!result.success) {
    if (result.reason !== 'already-active') showToast('Companions can only be swapped in Warden Refuge');
    return;
  }
  companionRoster = result.roster;
  applyCompanionVisuals();
  renderCompanionRoster();
  persistProgress('Active companion saved');
  showToast(`${COMPANION_DEFINITIONS[companionRoster.activeId].name} joins Mara`);
}

function renderTutorial() {
  const completion = deriveDiscoveryCompletion(discoveryProgress);
  const labels = {
    town: 'Visit Warden Refuge',
    equipment: 'Equip or craft an item',
    priority: 'Change an ability priority',
    movement: 'Move through the rendered world',
    gather: 'Gather a resource node',
    combat: 'Defeat a field enemy',
    branch: 'Enter an optional route',
    boss: 'Defeat the Overseer',
    extraction: 'Complete an extraction'
  };
  const next = completion.remainingSteps[0];
  ui.tutorialCard.hidden = completion.tutorialComplete;
  ui.tutorialNext.textContent = next ? labels[next] : 'Training complete';
  ui.tutorialProgress.textContent = `${completion.completedCount} / ${completion.totalCount} learned`;
}

function ensureDiscoveredAbility() {
  if (
    discoveryProgress.discoveries.fourthActiveAbility.unlocked
    && !hero.abilities.order.includes('frostLance')
  ) {
    const defaultIndex = hero.abilities.order.indexOf('iceShard');
    hero.abilities.order.splice(defaultIndex < 0 ? hero.abilities.order.length : defaultIndex, 0, 'frostLance');
    hero.abilities.cooldowns.frostLance = 0;
  }
}

function recordEquipmentAbility(item) {
  const passiveByTemplate = {
    shard_prism: 'Prismatic Shards',
    glacial_heart: 'Nova Echo',
    frostweave_mantle: 'Deep Focus',
    veinward_plate: 'Veinward Aegis',
    crafted_rimecleaver: 'Rimecleaver Edge',
    crafted_gloamguard: 'Gloamguard Resolve',
    crafted_wayfinder_lens: 'Wayfinder Instinct'
  };
  const abilityId = passiveByTemplate[item?.templateId];
  if (!abilityId) return;
  discoveryProgress = recordEquipmentPassive(discoveryProgress, item.id, abilityId);
}

function recordTraining(step) {
  const previous = discoveryProgress;
  discoveryProgress = recordTutorialStep(discoveryProgress, step);
  if (discoveryProgress !== previous) {
    renderTutorial();
    persistProgress();
  }
}

function toggleSound() {
  soundEnabled = !soundEnabled;
  audio.setEnabled(soundEnabled);
  ui.soundToggle.setAttribute('aria-pressed', String(soundEnabled));
  ui.soundLabel.textContent = soundEnabled ? 'ON' : 'OFF';
  persistProgress('Audio preference saved');
  if (soundEnabled) audio.cue('command');
}

function handleBlacksmithUpgrade() {
  const result = upgradeBlacksmith(townProgress);
  if (!result.success) {
    showToast(result.reason);
    return;
  }
  townProgress = result.progress;
  renderRefugePanel();
  persistProgress('Blacksmith restored');
  showToast('The Cold Forge burns again');
}

function handleCraftingClick(event) {
  const button = event.target.closest('[data-craft]');
  if (!button) return;
  if (hero.inventory.length >= 18) {
    showToast('Field pack full');
    return;
  }
  const nextSerial = itemSerial + 1;
  const result = craftRecipe(townProgress, button.dataset.craft, nextSerial);
  if (!result.success) {
    showToast(result.reason);
    return;
  }
  itemSerial = nextSerial;
  townProgress = result.progress;
  hero.inventory.push(result.item);
  hero.equipment = equipInventoryItem(hero.inventory, hero.equipment, result.item.id);
  recordEquipmentAbility(result.item);
  recordTraining('equipment');
  refreshGearStats();
  applyEquipmentVisuals();
  renderInventory();
  renderRefugePanel();
  syncBossAvailability(true);
  persistProgress('Crafted equipment saved');
  showToast(`${result.item.name} forged and equipped`);
}

function progressPayload() {
  return {
    progress: townProgress,
    inventory: hero.inventory,
    equipment: hero.equipment,
    carried: hero.resources,
    itemSerial,
    loot: hero.loot,
    abilityOrder: hero.abilities.order,
    expedition: expeditionProgress,
    pendingBossRewards,
    routeProgress,
    companionRoster,
    discoveryProgress,
    soundEnabled
  };
}

function persistProgress(message) {
  saveTownState(progressPayload());
  if (message) ui.saveStatus.textContent = message;
}

function restoreProgress(saved = loadTownSave()) {
  townProgress = {
    resources: saved.resources,
    blacksmithLevel: saved.blacksmithLevel,
    crafted: saved.crafted,
    quest: saved.quest
  };
  if (Array.isArray(saved.inventory)) hero.inventory = saved.inventory;
  if (saved.equipment) hero.equipment = { ...hero.equipment, ...saved.equipment };
  if (saved.carried) hero.resources = { ...hero.resources, ...saved.carried };
  if (Number.isInteger(saved.itemSerial)) itemSerial = saved.itemSerial;
  if (Number.isFinite(saved.loot)) hero.loot = saved.loot;
  routeProgress = normalizeRouteProgress(saved.routeProgress);
  companionRoster = normalizeCompanionRoster(saved.companionRoster);
  discoveryProgress = normalizeDiscoveryProgress(saved.discoveryProgress);
  soundEnabled = saved.soundEnabled !== false;
  audio.setEnabled(soundEnabled);
  ui.soundToggle.setAttribute('aria-pressed', String(soundEnabled));
  ui.soundLabel.textContent = soundEnabled ? 'ON' : 'OFF';
  expeditionProgress = pruneExpiredRecovery(
    saved.expedition ?? createExpeditionProgress(),
    Date.now()
  );
  pendingBossRewards = Array.isArray(saved.pendingBossRewards)
    ? saved.pendingBossRewards
    : [];
  if (expeditionProgress.rewardPending && pendingBossRewards.length === 0) {
    pendingBossRewards = createBossRewardChoices(seededRandom, itemSerial + 1);
    itemSerial += pendingBossRewards.length;
  } else if (!expeditionProgress.rewardPending) {
    pendingBossRewards = [];
  }
  ensureDiscoveredAbility();
  if (
    Array.isArray(saved.abilityOrder)
    && saved.abilityOrder.length === hero.abilities.order.length
    && saved.abilityOrder.every((id) => ABILITIES[id])
  ) {
    hero.abilities.order = [...saved.abilityOrder];
  }
  refreshGearStats();
  applyCompanionVisuals();
  renderDefeatState();
}

function exportProgressSave() {
  const blob = new Blob([serializeTownSave(progressPayload())], { type: 'application/json' });
  const url = URL.createObjectURL(blob);
  const link = Object.assign(document.createElement('a'), {
    href: url,
    download: 'gloamwood-refuge-save.json'
  });
  link.click();
  URL.revokeObjectURL(url);
  ui.saveStatus.textContent = 'Save exported';
}

async function importProgressSave(event) {
  const [file] = event.target.files;
  if (!file) return;
  const imported = parseTownSave(await file.text());
  restoreProgress(imported);
  renderAbilityPanel();
  renderInventory();
  applyEquipmentVisuals();
  renderRefugePanel();
  syncBossAvailability();
  renderBossRewardPanel();
  renderDefeatState();
  renderRouteMap();
  renderTutorial();
  persistProgress('Imported save applied');
  showToast('Refuge progress restored');
  event.target.value = '';
}

function handleRecoveryClaim() {
  const result = recoverExpeditionCache(expeditionProgress, Date.now());
  expeditionProgress = result.progress;
  if (!result.success) {
    showToast(result.reason === 'recovery-expired' ? 'The recovery cache has expired' : 'No cache to recover');
    renderRefugePanel();
    persistProgress();
    return;
  }
  const recoveredTotal = Object.values(result.recovered).reduce((sum, amount) => sum + amount, 0);
  const deposited = depositResources(townProgress, result.recovered);
  townProgress = deposited.progress;
  renderRefugePanel();
  persistProgress('Recovery cache secured');
  showToast(`${recoveredTotal} lost materials recovered`);
}

function handleReturnAfterDefeat() {
  expeditionProgress = returnAfterDefeat(expeditionProgress);
  restoreHeroAtRefuge();
  renderDefeatState();
  setRefugeOpen(true);
  persistProgress('Returned after defeat');
  showToast('Mara returned to Warden Refuge');
}

function resourceSummary(resources) {
  const labels = { ore: 'ore', wood: 'wood', herb: 'herbs', essence: 'essence' };
  const entries = Object.entries(resources)
    .filter(([, amount]) => amount > 0)
    .map(([type, amount]) => `${amount} ${labels[type]}`);
  return entries.join(' · ') || 'No materials';
}

function formatRecoveryRemaining(expiresAt) {
  const remaining = Math.max(0, expiresAt - Date.now());
  const hours = Math.floor(remaining / 3600000);
  const minutes = Math.ceil((remaining % 3600000) / 60000);
  return hours > 0 ? `${hours}h ${minutes}m` : `${minutes}m`;
}

function handleInventoryClick(event) {
  const itemButton = event.target.closest('[data-equip-item]');
  if (!itemButton) return;
  const itemId = itemButton.dataset.equipItem;
  const item = hero.inventory.find((candidate) => candidate.id === itemId);
  if (!item) return;
  hero.equipment = equipInventoryItem(hero.inventory, hero.equipment, itemId);
  recordEquipmentAbility(item);
  recordTraining('equipment');
  refreshGearStats();
  applyEquipmentVisuals();
  renderInventory();
  persistProgress('Equipment saved');
  showToast(`${item.name} equipped`);
}

function renderInventory() {
  const slotNames = {
    weapon: 'WEAPON',
    armor: 'ARMOR',
    helmet: 'HELMET',
    accessory: 'ACCESSORY',
    gadget: 'GADGET'
  };
  ui.equipmentSlots.innerHTML = Object.keys(slotNames).map((slot) => {
    const item = hero.inventory.find((candidate) => candidate.id === hero.equipment[slot]);
    return `
      <div class="equipment-slot ${item?.rarity ?? ''}">
        <span>${slotNames[slot]}</span>
        <strong>${item?.name ?? 'Empty slot'}</strong>
        <small>${item?.effect ?? 'No modifier equipped'}</small>
      </div>
    `;
  }).join('');

  ui.inventoryGrid.innerHTML = hero.inventory.map((item) => {
    const equipped = hero.equipment[item.slot] === item.id;
    const slotGlyph = { weapon: 'W', armor: 'A', helmet: 'H', accessory: 'R', gadget: 'G' }[item.slot];
    return `
      <button class="inventory-item ${item.rarity} ${equipped ? 'equipped' : ''}" type="button" data-equip-item="${item.id}">
        <span class="item-icon">${slotGlyph}</span>
        <span>
          <strong>${item.name}</strong>
          <small>${item.effect}</small>
        </span>
        <span class="item-rarity">${equipped ? 'EQUIPPED' : item.rarity.toUpperCase()}</span>
      </button>
    `;
  }).join('');
  ui.inventoryEmpty.hidden = hero.inventory.length > 0;
  ui.inventoryCount.textContent = String(hero.inventory.length);
}

function refreshGearStats() {
  const previousMaxHp = hero.maxHp;
  const previousMaxFocus = hero.maxFocus;
  hero.gearStats = deriveGearStats(hero.inventory, hero.equipment);
  hero.maxHp = 100 + hero.gearStats.maxHealth;
  hero.maxFocus = 100 + hero.gearStats.maxFocus;
  hero.focusRegen = 8 + hero.gearStats.focusRegen;
  hero.hp = Math.min(hero.maxHp, hero.hp + Math.max(0, hero.maxHp - previousMaxHp));
  hero.focus = Math.min(hero.maxFocus, hero.focus + Math.max(0, hero.maxFocus - previousMaxFocus));
}

function handlePointer(event) {
  event.preventDefault();
  audio.unlock();
  const rect = canvas.getBoundingClientRect();
  const x = event.clientX - rect.left;
  const y = event.clientY - rect.top;
  const ray = pointerRay(x, y);
  const enemy = pickMob(ray.origin, ray.direction);
  const refuge = enemy ? null : pickRefuge(ray.origin, ray.direction);
  const resourceNode = enemy || refuge ? null : pickResourceNode(ray.origin, ray.direction);

  if (firstCommand) {
    firstCommand = false;
    ui.hint.style.opacity = '0';
  }

  if (enemy) {
    cancelGathering();
    hero.refugeTarget = null;
    hero.targetEnemy = enemy;
    hero.moveTarget = null;
    hero.aimPoint.copy(enemy.entity.getPosition());
    selector.enabled = false;
    enemy.aggro = true;
    showToast(`Engaging ${enemy.name}`);
    audio.cue('command');
    return;
  }

  if (refuge) {
    commandRefugeReturn();
    return;
  }

  if (resourceNode) {
    cancelGathering();
    hero.refugeTarget = null;
    hero.targetEnemy = null;
    hero.moveTarget = null;
    hero.gatherTarget = resourceNode;
    hero.gatherDuration = gatheringDuration(resourceNode.type, hero.gearStats.gatherSpeed);
    hero.aimPoint.copy(resourceNode.root.getPosition());
    selector.setPosition(resourceNode.root.getPosition().x, 0.025, resourceNode.root.getPosition().z);
    selector.enabled = true;
    showToast(`Moving to ${RESOURCE_TYPES[resourceNode.type].name}`);
    return;
  }

  const ground = intersectGround(ray.origin, ray.direction);
  if (!ground) return;
  ground.x = pc.math.clamp(ground.x, -32, 32);
  ground.z = pc.math.clamp(ground.z, -32, 32);
  cancelGathering();
  hero.refugeTarget = null;
  hero.targetEnemy = null;
  hero.moveTarget = ground;
  hero.aimPoint.copy(ground);
  selector.setPosition(ground.x, 0.025, ground.z);
  selector.enabled = true;
  recordTraining('movement');
  audio.cue('command');
}

function handlePointerMove(event) {
  if (event.pointerType === 'touch') return;
  const rect = canvas.getBoundingClientRect();
  const ray = pointerRay(event.clientX - rect.left, event.clientY - rect.top);
  const ground = intersectGround(ray.origin, ray.direction);
  if (!ground) return;
  ground.x = pc.math.clamp(ground.x, -32, 32);
  ground.z = pc.math.clamp(ground.z, -32, 32);
  hero.aimPoint.copy(ground);
}

function pointerRay(x, y) {
  const origin = camera.camera.screenToWorld(x, y, camera.camera.nearClip);
  const end = camera.camera.screenToWorld(x, y, camera.camera.farClip);
  return { origin, direction: end.clone().sub(origin).normalize() };
}

function pickMob(origin, direction) {
  let picked = null;
  let bestDistance = Infinity;
  for (const mob of mobs) {
    if (!mob.alive) continue;
    const center = mob.entity.getPosition().clone();
    center.y += 1.15;
    const hitDistance = raySphereDistance(origin, direction, center, 1.05);
    if (hitDistance !== null && hitDistance < bestDistance) {
      bestDistance = hitDistance;
      picked = mob;
    }
  }
  return picked;
}

function pickResourceNode(origin, direction) {
  let picked = null;
  let bestDistance = Infinity;
  for (const node of resourceNodes) {
    if (!node.available || !node.model.enabled) continue;
    const center = node.root.getPosition().clone();
    center.y += node.pickHeight;
    const hitDistance = raySphereDistance(origin, direction, center, node.pickRadius);
    if (hitDistance !== null && hitDistance < bestDistance) {
      bestDistance = hitDistance;
      picked = node;
    }
  }
  return picked;
}

function intersectGround(origin, direction) {
  if (Math.abs(direction.y) < 0.0001) return null;
  const distance = -origin.y / direction.y;
  if (distance < 0) return null;
  return origin.clone().add(direction.clone().mulScalar(distance));
}

async function loadProductionAssets() {
  try {
    const [
      heroOutfitAsset,
      enemyAsset,
      archAsset,
      wallAsset,
      statueAsset,
      groundTexture
    ] = await Promise.all([
      loadContainer(app, 'Mara modular ranger', '/assets/models/quaternius/characters/female-ranger-animated.glb'),
      loadContainer(app, 'Gravebound Warrior', '/assets/models/kaykit/skeletons/gravebound-warrior.glb'),
      loadContainer(app, 'Gothic arch', '/assets/models/environment/gothic-arch.glb'),
      loadContainer(app, 'Overgrown wall', '/assets/models/environment/overgrown-wall.glb'),
      loadContainer(app, 'Stag statue', '/assets/models/environment/stag-statue.glb'),
      loadTexture(app, 'Gloamwood ground', '/assets/textures/gloamwood-ground-v1.png')
    ]);

    const [villageKit, natureKit] = await Promise.all([
      loadNamedKit('village', QUATERNIUS_VILLAGE_MODELS),
      loadNamedKit('nature', QUATERNIUS_NATURE_MODELS)
    ]);

    prepareContainerMaterials(heroOutfitAsset, { gloss: 0.42, metalness: 0.08 });
    prepareContainerMaterials(enemyAsset, {
      gloss: 0.2,
      metalness: 0.06,
      tint: new pc.Color(0.48, 0.56, 0.58)
    });
    for (const asset of [archAsset, wallAsset, statueAsset]) {
      prepareContainerMaterials(asset, { gloss: 0.18, metalness: 0 });
    }
    for (const asset of villageKit.values()) {
      prepareContainerMaterials(asset, {
        gloss: 0.2,
        metalness: 0.015,
        tint: new pc.Color(0.62, 0.58, 0.48)
      });
    }
    for (const [name, asset] of natureKit) {
      const isTree = /(?:Tree_|Pine_|BirchTree_)/.test(name);
      const materialNames = (asset.resource.materials ?? []).map((materialAsset) => (
        materialAsset.resource?.name ?? materialAsset.name ?? ''
      ));
      const tints = isTree
        ? materialNames.map((materialName) => treeMaterialTint(name, materialName))
        : undefined;
      const treeLighting = isTree
        ? materialNames.map((materialName) => !materialName.toLowerCase().includes('leaves'))
        : undefined;
      const treeAlphaTests = isTree
        ? materialNames.map((materialName) => (
            materialName.toLowerCase().includes('leaves') ? 0.56 : 0.2
          ))
        : undefined;
      const treeDiffuseTextureIndices = isTree
        ? materialNames.map((materialName) => (
            materialName.toLowerCase().includes('leaves') ? 2 : 0
          ))
        : undefined;
      const tint = isTree
        ? undefined
        : name.startsWith('RockPath_')
          ? new pc.Color(0.42, 0.39, 0.32)
          : name.startsWith('Rock_')
            ? new pc.Color(0.38, 0.42, 0.38)
            : name.startsWith('Grass_')
              ? new pc.Color(0.22, 0.38, 0.2)
              : new pc.Color(0.28, 0.43, 0.27);
      prepareContainerMaterials(asset, {
        gloss: isTree ? 0 : 0.12,
        metalness: 0,
        specularityFactor: isTree ? 0 : 0.18,
        specular: isTree ? new pc.Color(0, 0, 0) : undefined,
        matte: isTree,
        useLightingValues: treeLighting,
        alphaTests: treeAlphaTests,
        diffuseTextureIndices: treeDiffuseTextureIndices,
        tint,
        tints
      });
    }

    installModularHero(hero.rig, heroOutfitAsset);
    installCharacterModel(companion.rig, heroOutfitAsset, false);
    makeRenderMaterialsUnique(companion.rig.productionModel);
    applyCompanionVisuals();
    for (const mob of mobs) {
      installCharacterModel(mob.rig, enemyAsset, true);
      const scale = mob.isBoss ? 1.62 : mob.visualScale ?? 1.12;
      mob.rig.productionModel.setLocalScale(scale, scale, scale);
      tintCharacterModel(
        mob.rig.productionModel,
        mob.isBoss ? [0.78, 0.3, 0.24] : mob.visualTint
      );
    }
    installGroundTexture(groundTexture);
    installEnvironmentKit({ archAsset, wallAsset, statueAsset });
    installQuaterniusWorld(villageKit, natureKit);

    ui.objective.textContent = 'Break the blightbound hunting packs';
    showToast('Production assets ready · Gloamwood awakened');
  } catch (error) {
    console.warn('Production assets could not be loaded; keeping fallback rigs.', error);
    showToast('Fallback expedition visuals active');
  }
}

function treeMaterialTint(modelName, materialName) {
  const material = materialName.toLowerCase();
  if (material.includes('bark')) {
    if (modelName.includes('Birch')) return new pc.Color(0.58, 0.5, 0.36);
    if (modelName.includes('DeadTree')) return new pc.Color(0.25, 0.18, 0.1);
    if (modelName.includes('TwistedTree')) return new pc.Color(0.29, 0.19, 0.09);
    return new pc.Color(0.34, 0.22, 0.11);
  }
  if (material.includes('leaves')) {
    if (modelName.includes('Pine')) return new pc.Color(0.55, 0.7, 0.58);
    if (modelName.includes('Birch')) return new pc.Color(0.7, 0.82, 0.58);
    if (modelName.includes('TwistedTree')) return new pc.Color(0.72, 0.62, 0.45);
    return new pc.Color(0.62, 0.78, 0.55);
  }
  return new pc.Color(0.6, 0.72, 0.5);
}

async function loadNamedKit(folder, names) {
  const loaded = await Promise.all(names.map(async (name) => [
    name,
    await loadContainer(app, name, `/assets/models/quaternius/${folder}/${name}.glb`)
  ]));
  return new Map(loaded);
}

function installGroundTexture(groundTexture) {
  PALETTE.ground.diffuse = new pc.Color(0.72, 0.78, 0.72);
  PALETTE.ground.diffuseMap = groundTexture.resource;
  PALETTE.ground.diffuseMapTiling = new pc.Vec2(9, 9);
  PALETTE.ground.diffuseTint = true;
  PALETTE.ground.update();
}

function installCharacterModel(rig, asset, enemy) {
  hideRenderHierarchy(rig.root);
  for (const render of rig.root.findComponents('render')) {
    if (render.entity.name === 'Hero footing') render.enabled = true;
  }

  const model = instantiateAnimated(asset, {
    name: enemy ? 'Gravebound production rig' : 'Mara production rig',
    scale: enemy ? 1.12 : 1.16,
    rotationY: 0,
    initial: enemy ? 'Idle_Combat' : 'Idle_Weapon',
    clipSpeeds: enemy
      ? {
          Idle_Combat: 0.84,
          Walking_D_Skeletons: 0.92,
          Running_A: 0.9,
          '1H_Melee_Attack_Slice_Diagonal': 1.18,
          Death_C_Skeletons: 1.05
        }
      : { Run_Holding: 0.94, Run: 0.94, Bow_Shoot: 1.32, Punch: 1.2 }
  });
  if (enemy) makeRenderMaterialsUnique(model);
  rig.root.addChild(model);
  applyRenderQuality(model);
  rig.productionModel = model;
  if (!enemy) {
    const lantern = new pc.Entity('Mara rim light');
    lantern.addComponent('light', {
      type: 'omni',
      color: new pc.Color(0.92, 0.68, 0.38),
      intensity: 0.72,
      range: 4.8,
      castShadows: false
    });
    lantern.setLocalPosition(-0.25, 2.25, 0.35);
    rig.root.addChild(lantern);
  }
}

function makeRenderMaterialsUnique(entity) {
  for (const render of entity.findComponents('render')) {
    for (const meshInstance of render.meshInstances) {
      const material = meshInstance.material.clone();
      material.name = `${material.name || 'material'} instance`;
      meshInstance.material = material;
    }
  }
}

function installModularHero(rig, outfitAsset) {
  hideRenderHierarchy(rig.root);
  for (const render of rig.root.findComponents('render')) {
    if (render.entity.name === 'Hero footing') render.enabled = true;
  }

  const model = instantiateAnimated(outfitAsset, {
    name: 'Mara modular animation rig',
    scale: 1.62,
    rotationY: 0,
    initial: 'Idle_Loop',
    stripAnimationRoot: 'Armature',
    clipSpeeds: {
      Idle_Loop: 0.82,
      Walk_Loop: 0.9,
      Jog_Fwd_Loop: 0.94,
      Spell_Simple_Shoot: 1.55,
      Sword_Attack: 1.35
    }
  });
  applyRenderQuality(model);

  rig.root.addChild(model);
  rig.productionModel = model;
  rig.castHand = model.findByName('hand_l');
  rig.equipmentAnchors = {
    weapon: model.findByName('hand_r'),
    gadget: model.findByName('spine_03')
  };
  rig.modularParts = {
    pauldrons: model.findByName('Female_Ranger_Acc_Pauldrons'),
    bracers: model.findByName('Female_Ranger_Arms_Bracer'),
    beltOne: model.findByName('Female_Ranger_Body_Belt_1'),
    beltTwo: model.findByName('Female_Ranger_Body_Belt_2'),
    hood: model.findByName('Female_Ranger_Head_Hood')
  };
  if (rig.modularParts.pauldrons) rig.modularParts.pauldrons.enabled = false;
  if (rig.modularParts.beltTwo) rig.modularParts.beltTwo.enabled = false;
  applyEquipmentVisuals();

  const lantern = new pc.Entity('Mara rim light');
  lantern.addComponent('light', {
    type: 'omni',
    color: new pc.Color(0.92, 0.55, 0.25),
    intensity: 0.82,
    range: 5.2,
    castShadows: false
  });
  lantern.setLocalPosition(-0.3, 2.25, 0.35);
  rig.root.addChild(lantern);
}

function applyEquipmentVisuals() {
  const rig = hero.rig;
  if (!rig.productionModel) return;
  rig.weaponVisual?.destroy();
  rig.gadgetVisual?.destroy();
  rig.weaponVisual = null;
  rig.gadgetVisual = null;

  const weapon = hero.inventory.find((item) => item.id === hero.equipment.weapon);
  const armor = hero.inventory.find((item) => item.id === hero.equipment.armor);
  const helmet = hero.inventory.find((item) => item.id === hero.equipment.helmet);
  const gadget = hero.inventory.find((item) => item.id === hero.equipment.gadget);
  const parts = rig.modularParts;

  if (parts?.pauldrons) parts.pauldrons.enabled = armor?.visual === 'plate' || armor?.visual === 'mantle';
  if (parts?.bracers) parts.bracers.enabled = Boolean(armor);
  if (parts?.beltOne) parts.beltOne.enabled = armor?.visual !== 'mantle';
  if (parts?.beltTwo) parts.beltTwo.enabled = Boolean(armor);
  if (parts?.hood) parts.hood.enabled = helmet?.visual === 'hood' || armor?.visual === 'mantle';

  if (weapon && rig.equipmentAnchors.weapon) {
    const root = new pc.Entity(`${weapon.name} equipped`);
    rig.equipmentAnchors.weapon.addChild(root);
    root.setLocalPosition(0.02, -0.04, 0.02);
    root.setLocalEulerAngles(0, 0, 8);
    const icy = weapon.visual === 'rimefang';
    const weaponMaterial = icy ? PALETTE.equipmentIce : PALETTE.steel;
    if (weapon.visual === 'wand') {
      primitive('cylinder', 'Wand shaft', root, [0.035, 0.42, 0.035], [0, -0.24, 0], PALETTE.heroLeather);
      primitive('sphere', 'Wand focus', root, [0.13, 0.16, 0.13], [0, -0.68, 0], PALETTE.equipmentIce, false);
    } else {
      primitive('cylinder', 'Weapon grip', root, [0.045, 0.22, 0.045], [0, 0.02, 0], PALETTE.heroLeather);
      primitive('box', 'Weapon guard', root, [0.28, 0.045, 0.07], [0, -0.19, 0], PALETTE.brass);
      primitive('box', 'Weapon blade', root, [icy ? 0.1 : 0.075, icy ? 0.82 : 0.64, 0.04], [0, -0.68, 0], weaponMaterial);
      if (icy) {
        primitive('cone', 'Rimefang point', root, [0.11, 0.3, 0.07], [0, -1.17, 0], PALETTE.equipmentIce, false);
      }
    }
    rig.weaponVisual = root;
  }

  if (gadget && rig.equipmentAnchors.gadget) {
    const root = new pc.Entity(`${gadget.name} equipped`);
    rig.equipmentAnchors.gadget.addChild(root);
    root.setLocalPosition(0.34, 0.02, -0.18);
    if (gadget.visual === 'lens') {
      primitive('cylinder', 'Surveyor lens', root, [0.15, 0.05, 0.15], [0, 0, 0], PALETTE.brass, false)
        .setLocalEulerAngles(90, 0, 0);
    } else if (gadget.visual === 'prism') {
      primitive('cone', 'Shard prism', root, [0.16, 0.36, 0.16], [0, 0, 0], PALETTE.equipmentIce, false);
    } else {
      primitive('sphere', 'Glacial heart', root, [0.17, 0.2, 0.14], [0, 0, 0], PALETTE.itemRare, false);
    }
    rig.gadgetVisual = root;
  }
}

function installEnvironmentKit({ archAsset, wallAsset, statueAsset }) {
  for (const ruin of proceduralRuins) ruin.enabled = false;

  const placements = [
    [archAsset, { name: 'Gloamwood gate', position: [-6, 0, -19], rotation: [0, 24, 0], scale: 1.35 }],
    [wallAsset, { name: 'Eastern overgrown ruin', position: [22, 0, 2], rotation: [0, -62, 0], scale: 1.4 }],
    [wallAsset, { name: 'Western overgrown ruin', position: [-23, 0, 7], rotation: [0, 106, 0], scale: 1.28 }],
    [statueAsset, { name: 'Stag ward', position: [-3.8, 0, -17.3], rotation: [0, 142, 0], scale: 1.15 }]
  ];

  for (const [asset, options] of placements) {
    const entity = instantiateStatic(asset, options);
    world.addChild(entity);
    applyRenderQuality(entity);
    if (options.name === 'Gloamwood gate') {
      registerStaticCollider(entity, 0.58 * options.scale, [-1.15, 0, 0]);
      registerStaticCollider(entity, 0.58 * options.scale, [1.15, 0, 0]);
    } else if (options.name.includes('overgrown ruin')) {
      for (const offset of [-1.2, 0, 1.2]) {
        registerStaticCollider(entity, 0.72 * options.scale, [offset, 0, 0]);
      }
    } else {
      registerStaticCollider(entity, 0.62 * options.scale);
    }
  }

  createAccentLight('Gate crystal light', [-4.7, 2.1, -18.2], [0.08, 0.72, 0.86], 1.6, 7);
  createAccentLight('Blight grove light', [13.5, 1.2, 11.5], [0.13, 0.48, 0.62], 1.05, 6);
}

function installStoneCauseway(natureKit) {
  const mainSections = [
    'RockPath_Square_Wide',
    'RockPath_Square_Wide',
    'RockPath_Square_Thin',
    'RockPath_Round_Wide'
  ];
  const edgeSections = [
    'RockPath_Square_Small_1',
    'RockPath_Square_Small_2',
    'RockPath_Square_Small_3'
  ];

  let sectionIndex = 0;
  for (let z = -31; z <= 31; z += 1.86) {
    const bend = Math.sin(z * 0.19) * 2.2;
    const heading = Math.atan(0.418 * Math.cos(z * 0.19)) * pc.math.RAD_TO_DEG;

    for (const lane of [-1, 1]) {
      if ((sectionIndex + lane + 7) % 13 === 0) continue;
      const name = mainSections[(sectionIndex + (lane > 0 ? 1 : 0)) % mainSections.length];
      const lateralJitter = Math.sin(sectionIndex * 1.73 + lane) * 0.12;
      const depthJitter = Math.cos(sectionIndex * 1.31 + lane) * 0.1;
      addKitModel(
        world,
        natureKit.get(name),
        `Raised stone route ${sectionIndex + 1}`,
        [bend + lane * 0.9 + lateralJitter, 0.012, z + depthJitter],
        [0, heading + Math.sin(sectionIndex * 0.83 + lane) * 4.5, 0],
        0.96 + ((sectionIndex + lane + 2) % 3) * 0.025
      );
    }

    if (sectionIndex % 2 === 0) {
      const side = sectionIndex % 4 === 0 ? -1 : 1;
      const name = edgeSections[(sectionIndex / 2) % edgeSections.length];
      addKitModel(
        world,
        natureKit.get(name),
        `Broken route edge ${sectionIndex + 1}`,
        [bend + side * (1.82 + (sectionIndex % 3) * 0.11), 0.01, z + 0.54],
        [0, heading + side * (12 + (sectionIndex % 5) * 3), 0],
        0.78 + (sectionIndex % 3) * 0.07
      );
    }

    sectionIndex += 1;
  }
}

function installQuaterniusWorld(villageKit, natureKit) {
  for (const tree of proceduralTrees) tree.enabled = false;
  for (const rock of proceduralRocks) rock.enabled = false;
  installStoneCauseway(natureKit);

  const refuge = new pc.Entity('Warden refuge');
  refuge.setPosition(-7.2, 0, 5.8);
  refuge.setEulerAngles(0, 24, 0);
  world.addChild(refuge);

  const building = [
    ['Wall_Plaster_Door_Round', [0, 0, 2], [0, 0, 0], 0.78],
    ['Wall_Plaster_Window_Wide_Round', [-2, 0, 2], [0, 0, 0], 0.78],
    ['Wall_Plaster_Window_Wide_Round', [2, 0, 2], [0, 0, 0], 0.78],
    ['Wall_Plaster_Straight', [-2, 0, -2], [0, 180, 0], 0.78],
    ['Wall_Plaster_Straight', [0, 0, -2], [0, 180, 0], 0.78],
    ['Wall_Plaster_Straight', [2, 0, -2], [0, 180, 0], 0.78],
    ['Corner_Exterior_Wood', [-3, 0, 0], [0, 90, 0], 0.78],
    ['Corner_Exterior_Wood', [3, 0, 0], [0, -90, 0], 0.78],
    ['Roof_RoundTiles_4x4', [0, 2.05, 0], [0, 0, 0], 0.78],
    ['Prop_Chimney', [-1.4, 2.8, -0.6], [0, 0, 0], 0.76],
    ['Door_1_Round', [0, 0, 2.04], [0, 0, 0], 0.78],
    ['Prop_Vine4', [1.9, 0.05, 2.08], [0, 0, 0], 0.82]
  ];
  for (const [name, position, rotation, scale] of building) {
    addKitModel(refuge, villageKit.get(name), `Refuge ${name}`, position, rotation, scale);
  }

  const courtyard = [
    ['Prop_Wagon', [-2.8, 0, 5.1], [0, 38, 0], 0.72],
    ['Prop_Crate', [2.5, 0, 4.4], [0, -18, 0], 0.82],
    ['Prop_WoodenFence_Single', [-4.5, 0, 3.3], [0, 82, 0], 0.82],
    ['Prop_WoodenFence_Single', [4.3, 0, 3.1], [0, 98, 0], 0.82]
  ];
  for (const [name, position, rotation, scale] of courtyard) {
    addKitModel(refuge, villageKit.get(name), `Courtyard ${name}`, position, rotation, scale);
  }
  installRefugeInteraction();

  const treeNames = [
    'CommonTree_1',
    'CommonTree_3',
    'Pine_2',
    'CommonTree_1',
    'DeadTree_3',
    'Pine_2',
    'TwistedTree_2',
    'CommonTree_3',
    'Ultimate_BirchTree_1',
    'Pine_2',
    'TwistedTree_4',
    'CommonTree_1'
  ];
  const treePlacements = [
    [-27, -24], [-21, -27], [-13, -28], [17, -27], [25, -22], [29, -12],
    [28, 7], [25, 23], [16, 28], [-15, 28], [-25, 22], [-29, 10],
    [-19, -12], [19, 15], [-18, 17], [17, -17]
  ];

  for (let index = 0; index < 52; index += 1) {
    const angle = index * 2.39996 + 0.38;
    const radius = 11.5 + (index % 11) * 1.78;
    const x = Math.cos(angle) * radius;
    const z = Math.sin(angle) * radius;
    const roadCenter = Math.sin(z * 0.19) * 2.2;
    const refugeDistance = Math.hypot(x + 7.2, z - 5.8);
    if (Math.abs(x - roadCenter) < 5.4 || refugeDistance < 8.5) continue;
    treePlacements.push([x, z]);
  }

  treePlacements.forEach(([x, z], index) => {
    const name = treeNames[index % treeNames.length];
    addKitModel(
      world,
      natureKit.get(name),
      `Gloamwood ${name}`,
      [x, 0, z],
      [0, (index * 71) % 360, 0],
      0.72 + (index % 5) * 0.075
    );
  });

  const undergrowthNames = [
    'Bush_Common', 'Bush_Common_Flowers', 'Fern_1', 'Grass_Common_Short',
    'Grass_Wispy_Tall', 'Rock_Medium_1', 'Rock_Medium_3', 'Mushroom_Laetiporus'
  ];
  for (let index = 0; index < 44 * TERRAIN_DENSITY_MULTIPLIER; index += 1) {
    const angle = index * 2.39996;
    const radius = 9 + (index % 9) * 2.35;
    const x = Math.cos(angle) * radius;
    const z = Math.sin(angle) * radius;
    if (Math.abs(x - Math.sin(z * 0.19) * 2.2) < 4.8) continue;
    const name = undergrowthNames[index % undergrowthNames.length];
    addKitModel(
      world,
      natureKit.get(name),
      `Forest detail ${name}`,
      [x, 0.02, z],
      [0, (index * 53) % 360, 0],
      0.62 + (index % 5) * 0.11
    );
  }

  const grassNames = [
    'Grass_Common_Short',
    'Grass_Common_Tall',
    'Grass_Wispy_Short',
    'Grass_Wispy_Tall'
  ];
  const grassPatchCenters = [
    [-25, -18], [-17, -24], [-10, -19], [10, -25], [21, -20], [26, -7],
    [24, 12], [17, 22], [6, 26], [-10, 24], [-22, 17], [-26, 5],
    [-16, -7], [14, 9]
  ];
  grassPatchCenters.forEach(([centerX, centerZ], patchIndex) => {
    const bladeCount = (4 + (patchIndex % 3)) * TERRAIN_DENSITY_MULTIPLIER;
    for (let bladeIndex = 0; bladeIndex < bladeCount; bladeIndex += 1) {
      const angle = bladeIndex * 2.39996 + patchIndex * 0.71;
      const radius = 0.38 + (bladeIndex % 5) * 0.43 + Math.floor(bladeIndex / 5) * 0.16;
      const name = grassNames[(patchIndex + bladeIndex) % grassNames.length];
      addKitModel(
        world,
        natureKit.get(name),
        `Gloamwood grass patch ${patchIndex + 1}`,
        [centerX + Math.cos(angle) * radius, 0.018, centerZ + Math.sin(angle) * radius],
        [0, (patchIndex * 47 + bladeIndex * 83) % 360, 0],
        0.58 + ((patchIndex + bladeIndex) % 4) * 0.1
      );
    }
  });

  const rockNames = ['Rock_Medium_1', 'Rock_Medium_2', 'Rock_Medium_3'];
  const rockClusterCenters = [
    [-24, -13], [-15, -26], [14, -25], [26, -12], [25, 18],
    [10, 27], [-18, 25], [-27, 9], [15, 13]
  ];
  rockClusterCenters.forEach(([centerX, centerZ], clusterIndex) => {
    const rockCount = (2 + (clusterIndex % 2)) * TERRAIN_DENSITY_MULTIPLIER;
    for (let rockIndex = 0; rockIndex < rockCount; rockIndex += 1) {
      const angle = rockIndex * 2.7 + clusterIndex * 0.93;
      const radius = rockIndex === 0
        ? 0
        : 0.52 + (rockIndex % 3) * 0.46 + Math.floor(rockIndex / 3) * 0.34;
      const name = rockNames[(clusterIndex + rockIndex) % rockNames.length];
      addKitModel(
        world,
        natureKit.get(name),
        `Gloamwood rock cluster ${clusterIndex + 1}`,
        [centerX + Math.cos(angle) * radius, 0.01, centerZ + Math.sin(angle) * radius],
        [0, (clusterIndex * 67 + rockIndex * 109) % 360, 0],
        0.68 + ((clusterIndex + rockIndex) % 4) * 0.13
      );
    }
  });

  installResourceNodes(natureKit);
  createAccentLight('Refuge hearth', [-7.2, 1.3, 9.1], [1, 0.34, 0.08], 1.5, 7.5);
}

function tintCharacterModel(entity, tint) {
  if (!tint) return;
  for (const render of entity.findComponents('render')) {
    for (const meshInstance of render.meshInstances) {
      const material = meshInstance.material;
      material.diffuse = new pc.Color(
        material.diffuse.r * tint[0],
        material.diffuse.g * tint[1],
        material.diffuse.b * tint[2]
      );
      material.update();
    }
  }
}

function applyCompanionVisuals() {
  const model = companion.rig.productionModel;
  if (!model) return;
  const tintById = {
    brann: [0.78, 0.46, 0.25],
    nyra: [0.48, 0.35, 0.82],
    elowen: [0.34, 0.72, 0.42]
  };
  const tint = tintById[companionRoster.activeId] ?? tintById.brann;
  for (const render of model.findComponents('render')) {
    for (const meshInstance of render.meshInstances) {
      const material = meshInstance.material;
      material._companionBaseDiffuse ??= material.diffuse.clone();
      const base = material._companionBaseDiffuse;
      material.diffuse = new pc.Color(
        base.r * tint[0],
        base.g * tint[1],
        base.b * tint[2]
      );
      material.update();
    }
  }
}

function pickRefuge(origin, direction) {
  if (!refugeInteraction) return null;
  const center = refugeInteraction.root.getPosition().clone();
  center.y += 0.75;
  return raySphereDistance(origin, direction, center, refugeInteraction.radius) !== null
    ? refugeInteraction
    : null;
}

function installRefugeInteraction() {
  if (refugeInteraction) return;
  const root = new pc.Entity('Warden refuge forge interaction');
  root.setPosition(REFUGE_POSITION);
  world.addChild(root);
  createDottedRing(root, 1.05, PALETTE.ring, 12, 'Refuge return ring');
  const anvil = primitive('box', 'Cold forge anvil', root, [0.46, 0.18, 0.25], [0, 0.48, 0], PALETTE.steel);
  primitive('box', 'Cold forge horn', anvil, [0.42, 0.38, 0.7], [0, 0, 0.34], PALETTE.steel);
  primitive('cylinder', 'Cold forge base', root, [0.22, 0.54, 0.22], [0, 0.22, 0], PALETTE.stoneDark);
  const beacon = primitive('cone', 'Refuge beacon', root, [0.13, 0.48, 0.13], [0, 1.45, 0], PALETTE.ring, false);
  beacon.setLocalEulerAngles(0, 0, 180);
  refugeInteraction = { root, beacon, radius: 1.5, interactRange: 3.8 };
}

function installResourceNodes(natureKit) {
  if (resourceNodes.length) return;
  const definitions = [
    ['ore', 'Rock_Medium_1', [4.2, 0.02, -1.8], 0.82],
    ['wood', 'DeadTree_3', [-3.6, 0, -3.4], 0.54],
    ['herb', 'Bush_Common_Flowers', [2.6, 0.02, 4.2], 0.66],
    ['essence', 'Mushroom_Laetiporus', [-2.8, 0.02, 2.8], 0.8],
    ['ore', 'Rock_Medium_2', [-21.5, 0.02, -16.5], 0.92],
    ['ore', 'Rock_Medium_1', [20.5, 0.02, -18.5], 0.96],
    ['ore', 'Rock_Medium_3', [22.5, 0.02, 15.5], 0.9],
    ['ore', 'Rock_Medium_2', [-21.5, 0.02, 19], 1],
    ['wood', 'DeadTree_3', [-16.5, 0, -20.5], 0.62],
    ['wood', 'TwistedTree_2', [17.5, 0, -15.5], 0.58],
    ['wood', 'DeadTree_3', [18.5, 0, 20.5], 0.64],
    ['wood', 'TwistedTree_4', [-17.5, 0, 18], 0.58],
    ['herb', 'Bush_Common_Flowers', [-9.5, 0.02, -13.5], 0.72],
    ['herb', 'Mushroom_Laetiporus', [8.5, 0.02, -16], 0.82],
    ['herb', 'Fern_1', [10.5, 0.02, 13.5], 0.78],
    ['herb', 'Bush_Common_Flowers', [-12, 0.02, 14.5], 0.7],
    ['essence', 'Mushroom_Laetiporus', [-5.5, 0.02, -23], 0.9],
    ['essence', 'Rock_Medium_1', [24, 0.02, 3.5], 0.68],
    ['essence', 'Mushroom_Laetiporus', [5.5, 0.02, 23.5], 0.9],
    ['essence', 'Rock_Medium_3', [-24.5, 0.02, 4.5], 0.66]
  ];

  definitions.forEach(([type, modelName, position, scale], index) => {
    const root = new pc.Entity(`${RESOURCE_TYPES[type].name} node ${index + 1}`);
    root.setPosition(...position);
    root.setEulerAngles(0, (index * 83) % 360, 0);
    world.addChild(root);

    const model = addKitModel(
      root,
      natureKit.get(modelName),
      `Harvestable ${modelName}`,
      [0, 0, 0],
      [0, 0, 0],
      scale
    );
    if (!model) {
      root.destroy();
      return;
    }

    const marker = new pc.Entity(`${type} gathering marker`);
    root.addChild(marker);
    const markerMaterial = PALETTE[`resource${type[0].toUpperCase()}${type.slice(1)}`];
    createDottedRing(marker, type === 'wood' ? 1.05 : 0.8, markerMaterial, 10, `${type} node ring`);
    const beacon = primitive(
      type === 'essence' ? 'cone' : 'sphere',
      `${type} node beacon`,
      marker,
      type === 'essence' ? [0.14, 0.48, 0.14] : [0.11, 0.11, 0.11],
      [0, type === 'wood' ? 2.3 : 1.18, 0],
      markerMaterial,
      false
    );
    if (type === 'essence') beacon.setLocalEulerAngles(0, 0, 180);

    resourceNodes.push({
      type,
      root,
      model,
      marker,
      beacon,
      available: true,
      respawnAt: 0,
      interactRange: type === 'wood' ? 2.4 : 2.05,
      pickRadius: type === 'wood' ? 1.35 : 1.1,
      pickHeight: type === 'wood' ? 1.45 : 0.72,
      phase: index * 0.7
    });
  });
}

function addKitModel(parent, asset, name, position, rotation, scale) {
  if (!asset) return null;
  const entity = instantiateStatic(asset, { name, position, rotation, scale });
  parent.addChild(entity);
  applyRenderQuality(entity);
  registerKitCollision(entity, name, scale);
  return entity;
}

function registerKitCollision(entity, name, scale) {
  if (name.includes('RockPath_')) return;

  if (/(?:Tree_|Pine_|BirchTree_)/.test(name)) {
    registerStaticCollider(entity, 0.52 * scale);
    registerOccluder(entity, 1.55 * scale);
    return;
  }
  if (name.includes('Rock_Medium_')) {
    registerStaticCollider(entity, 0.62 * scale);
    return;
  }
  if (/Refuge (?:Wall_|Corner_|Door_)/.test(name)) {
    registerStaticCollider(entity, 0.82 * scale);
    return;
  }
  if (name.includes('Prop_Wagon')) {
    registerStaticCollider(entity, 1.28 * scale);
    return;
  }
  if (name.includes('Prop_Crate')) {
    registerStaticCollider(entity, 0.52 * scale);
    return;
  }
  if (name.includes('Prop_WoodenFence')) {
    registerStaticCollider(entity, 0.9 * scale);
  }
}

function registerStaticCollider(entity, radius, localOffset = [0, 0, 0]) {
  staticColliders.push({
    entity,
    radius,
    localOffset: new pc.Vec3(...localOffset),
    meshInstances: entity.findComponents('render').flatMap((render) => render.meshInstances ?? [])
  });
}

function colliderWorldPosition(collider) {
  return collider.entity.getWorldTransform().transformPoint(collider.localOffset);
}

function registerOccluder(entity, radius) {
  const materials = [];
  for (const render of entity.findComponents('render')) {
    for (const meshInstance of render.meshInstances) {
      const material = meshInstance.material.clone();
      material.name = `${material.name || 'foliage'} occluder instance`;
      meshInstance.material = material;
      materials.push(material);
    }
  }
  occluders.push({ entity, radius, materials, opacity: 1 });
}

function createAccentLight(name, position, color, intensity, range) {
  const light = new pc.Entity(name);
  light.addComponent('light', {
    type: 'omni',
    color: new pc.Color(...color),
    intensity,
    range,
    castShadows: false
  });
  light.setPosition(...position);
  world.addChild(light);
}

function updateHero(dt) {
  hero.animationTime += dt;
  tickAbilityCooldowns(hero.abilities.cooldowns, dt);
  hero.focus = Math.min(hero.maxFocus, hero.focus + hero.focusRegen * dt);
  hero.invulnerable = Math.max(0, hero.invulnerable - dt);
  hero.wardRemaining = Math.max(0, hero.wardRemaining - dt);
  if (hero.wardRemaining <= 0 || hero.wardAbsorb <= 0) {
    hero.wardRemaining = 0;
    hero.wardAbsorb = 0;
    wardVisual.enabled = false;
  } else {
    wardVisual.enabled = true;
    wardVisual.rotateLocal(0, 34 * dt, 0);
    const pulse = 1 + Math.sin(elapsed * 5.2) * 0.035;
    wardVisual.setLocalScale(pulse, pulse, pulse);
  }

  if (hero.hp <= 0) {
    handleHeroDefeat();
    return;
  }
  if (expeditionProgress.rewardPending) {
    hero.state = 'idle';
    return;
  }
  if (expeditionProgress.state === 'defeated') {
    return;
  }

  if (hero.targetEnemy && !hero.targetEnemy.alive) clearTarget();
  if (hero.auto && !hero.targetEnemy && !hero.gatherTarget && !hero.refugeTarget) {
    hero.targetEnemy = nearestAlive(hero.entity.getPosition(), mobs, 19);
    if (hero.targetEnemy) hero.targetEnemy.aggro = true;
  }

  if (hero.attackElapsed > 0) {
    updateHeroAttack(dt);
  } else if (hero.gatherTarget) {
    updateHeroGathering(dt);
  } else if (hero.refugeTarget) {
    updateHeroRefuge(dt);
  } else if (hero.targetEnemy || hero.requestedAbility) {
    const abilityId = nextHeroAbility();
    const ability = abilityId ? ABILITIES[abilityId] : null;
    if (ability?.requiresTarget && hero.targetEnemy?.alive) {
      const enemyPosition = hero.targetEnemy.entity.getPosition();
      const distance = distanceXZ(hero.entity.getPosition(), enemyPosition);
      faceToward(hero.entity, enemyPosition);
      if (distance > ability.range) {
        moveActor(hero, enemyPosition, dt, ability.range * 0.86);
      } else {
        beginHeroAbility(abilityId);
      }
    } else if (ability) {
      beginHeroAbility(abilityId);
    } else {
      hero.state = 'idle';
    }
  } else if (hero.moveTarget) {
    const arrived = moveActor(hero, hero.moveTarget, dt, 0.18);
    if (arrived) {
      hero.moveTarget = null;
      hero.state = 'idle';
      selector.enabled = false;
    }
  } else {
    hero.state = 'idle';
    faceToward(hero.entity, hero.aimPoint);
  }

  const activeCastTime = hero.activeAbility ? ABILITIES[hero.activeAbility].castTime : hero.attackDuration;
  animateRig(
    hero.rig,
    hero.state,
    hero.animationTime,
    hero.attackElapsed / activeCastTime,
    false,
    hero.activeAbility
  );
}

function updateHeroGathering(dt) {
  const node = hero.gatherTarget;
  if (!node?.available) {
    cancelGathering();
    return;
  }
  const nodePosition = node.root.getPosition();
  faceToward(hero.entity, nodePosition);
  if (distanceXZ(hero.entity.getPosition(), nodePosition) > node.interactRange) {
    hero.gatherElapsed = 0;
    moveActor(hero, nodePosition, dt, node.interactRange, 'walk');
    return;
  }

  hero.state = 'gather';
  hero.gatherElapsed += dt;
  selector.enabled = false;
  if (hero.gatherElapsed < hero.gatherDuration) return;

  const baseAmount = gatheringYield(node.type, seededRandom, hero.gearStats);
  const companionYield = getActiveCompanionBonuses(companionRoster).modifiers.resourceYield ?? 0;
  const amount = Math.max(1, Math.round(baseAmount * (1 + companionYield)));
  const resource = RESOURCE_TYPES[node.type];
  hero.resources[node.type] += amount;
  townProgress = recordGather(townProgress, node.type, amount);
  depleteResourceNode(node, elapsed);
  node.model.enabled = false;
  node.marker.enabled = false;
  spawnGatherEffect(node.root.getPosition(), PALETTE[`resource${node.type[0].toUpperCase()}${node.type.slice(1)}`]);
  recordTraining('gather');
  audio.cue('gather');
  showToast(`Gathered ${amount} ${resource.shortName.toLowerCase()}`);
  persistProgress('Gathering progress saved');
  cancelGathering();
}

function updateCompanion(dt) {
  companion.animationTime += dt;
  companion.attackCooldown = Math.max(0, companion.attackCooldown - dt);
  const heroPosition = hero.entity.getPosition();
  const desired = new pc.Vec3(heroPosition.x - 1.35, 0, heroPosition.z + 1.25);
  const target = hero.targetEnemy?.alive ? hero.targetEnemy : null;

  if (distanceXZ(companion.entity.getPosition(), heroPosition) > 8) {
    companion.entity.setPosition(desired);
  }

  if (target && distanceXZ(companion.entity.getPosition(), target.entity.getPosition()) <= 8.5) {
    faceToward(companion.entity, target.entity.getPosition());
    if (companion.attackCooldown <= 0) {
      const member = companionRoster.companions[companionRoster.activeId];
      const bonus = getActiveCompanionBonuses(companionRoster).modifiers;
      const afflictedMultiplier = target.slowRemaining > 0
        ? 1 + (bonus.damageToAfflicted ?? 0)
        : 1;
      damageMob(target, Math.round((7 + member.level * 2) * afflictedMultiplier));
      companion.attackCooldown = 1.35;
      companion.attackElapsed = 0.001;
      companion.state = 'attack';
    }
  } else if (distanceXZ(companion.entity.getPosition(), desired) > 1.05) {
    moveActor(companion, desired, dt, 0.5, 'run');
  } else {
    companion.state = 'idle';
    faceToward(companion.entity, hero.aimPoint);
  }

  if (companion.attackElapsed > 0) {
    companion.attackElapsed += dt;
    if (companion.attackElapsed > 0.5) companion.attackElapsed = 0;
  }
  animateRig(
    companion.rig,
    companion.attackElapsed > 0 ? 'attack' : companion.state,
    companion.animationTime,
    companion.attackElapsed / 0.5,
    false,
    'iceShard'
  );
}

function commandRefugeReturn() {
  if (!refugeInteraction) {
    showToast('The refuge is still taking shape');
    return;
  }
  setRefugeOpen(false);
  cancelGathering();
  clearTarget();
  hero.moveTarget = null;
  hero.refugeTarget = refugeInteraction;
  hero.auto = false;
  ui.autoToggle.setAttribute('aria-pressed', 'false');
  ui.autoLabel.textContent = 'OFF';
  const position = refugeInteraction.root.getPosition();
  hero.aimPoint.copy(position);
  selector.setPosition(position.x, 0.025, position.z);
  selector.enabled = true;
  showToast('Returning to Warden Refuge');
}

function updateHeroRefuge(dt) {
  const position = hero.refugeTarget.root.getPosition();
  faceToward(hero.entity, position);
  if (distanceXZ(hero.entity.getPosition(), position) > hero.refugeTarget.interactRange) {
    moveActor(hero, position, dt, hero.refugeTarget.interactRange, 'walk');
    return;
  }
  hero.refugeTarget = null;
  hero.state = 'idle';
  selector.enabled = false;
  openRefugeAndDeposit();
}

function updateRefugeVisual(dt) {
  if (!refugeInteraction) return;
  refugeInteraction.root.rotateLocal(0, 8 * dt, 0);
  refugeInteraction.beacon.setLocalPosition(0, 1.45 + Math.sin(elapsed * 2.8) * 0.1, 0);
}

function cancelGathering() {
  hero.gatherTarget = null;
  hero.gatherElapsed = 0;
  hero.gatherDuration = 0;
}

function updateResourceNodes(dt) {
  for (const node of resourceNodes) {
    if (!node.available) {
      if (respawnResourceNode(node, elapsed)) {
        node.model.enabled = true;
        node.marker.enabled = true;
      }
      continue;
    }
    node.marker.rotateLocal(0, 16 * dt, 0);
    const bob = Math.sin(elapsed * 2.4 + node.phase) * 0.08;
    node.beacon.setLocalPosition(0, (node.type === 'wood' ? 2.3 : 1.18) + bob, 0);
  }
}

function spawnGatherEffect(position, effectMaterial) {
  const root = new pc.Entity('Gather burst');
  root.setPosition(position.x, 0.08, position.z);
  world.addChild(root);
  for (let index = 0; index < 7; index += 1) {
    const angle = (index / 7) * Math.PI * 2;
    const mote = primitive(
      'sphere',
      'Gather mote',
      root,
      [0.07, 0.07, 0.07],
      [Math.cos(angle) * 0.55, 0.18 + (index % 3) * 0.16, Math.sin(angle) * 0.55],
      effectMaterial,
      false
    );
    mote._velocity = new pc.Vec3(Math.cos(angle) * 1.2, 1.15 + (index % 3) * 0.22, Math.sin(angle) * 1.2);
  }
  effects.push({ root, age: 0, duration: 0.65, kind: 'gather' });
}

function nextHeroAbility() {
  if (hero.requestedAbility) {
    const requested = hero.requestedAbility;
    if (hero.abilities.cooldowns[requested] <= 0) return requested;
    hero.requestedAbility = null;
  }

  const heroPosition = hero.entity.getPosition();
  const novaRadius = ABILITIES.frostNova.radius + hero.gearStats.novaRadius;
  const nearbyEnemyCount = mobs.filter((mob) => (
    mob.alive
    && mob.entity.enabled
    && distanceXZ(heroPosition, mob.entity.getPosition()) <= novaRadius
  )).length;
  return choosePriorityAbility({
    order: hero.abilities.order,
    cooldowns: hero.abilities.cooldowns,
    health: hero.hp,
    maxHealth: hero.maxHp,
    resource: hero.focus,
    nearbyEnemyCount,
    hasTarget: Boolean(hero.targetEnemy?.alive)
  });
}

function beginHeroAbility(id) {
  const ability = ABILITIES[id];
  if (hero.focus < ability.cost) {
    hero.requestedAbility = null;
    return;
  }
  hero.focus -= ability.cost;
  hero.activeAbility = id;
  hero.requestedAbility = null;
  hero.attackElapsed = 0.0001;
  hero.damageApplied = false;
  hero.state = 'attack';
  hero.moveTarget = null;
  selector.enabled = false;
  audio.cue('cast');
}

function updateHeroAttack(dt) {
  const ability = ABILITIES[hero.activeAbility];
  if (!ability) {
    hero.attackElapsed = 0;
    return;
  }
  const enemy = hero.targetEnemy;
  if (ability.requiresTarget && (!enemy || !enemy.alive)) {
    hero.attackElapsed = 0;
    hero.activeAbility = null;
    return;
  }
  hero.attackElapsed += dt;
  const progress = hero.attackElapsed / ability.castTime;
  if (enemy?.alive) faceToward(hero.entity, enemy.entity.getPosition());
  hero.state = 'attack';

  if (progress >= ability.trigger && !hero.damageApplied) {
    hero.damageApplied = true;
    if (ability.id === 'iceShard' || ability.id === 'frostLance') {
      launchElementalShard(
        enemy,
        ability.damage + hero.gearStats.damage + hero.gearStats.shardDamage,
        ability.id === 'frostLance' ? 1.55 : 1
      );
    }
    else if (ability.id === 'frostNova') castFrostNova(ability);
    else if (ability.id === 'glacialWard') castGlacialWard(ability);
  }
  if (progress >= 1) {
    hero.abilities.cooldowns[ability.id] = effectiveAbilityCooldown(ability);
    hero.attackElapsed = 0;
    hero.activeAbility = null;
    hero.damageApplied = false;
  }
}

function effectiveAbilityCooldown(ability) {
  if (ability.id === 'frostNova') {
    return Math.max(1, ability.cooldown - hero.gearStats.novaCooldownReduction);
  }
  return ability.cooldown;
}

function moveActor(actor, destination, dt, stopDistance = 0, movementState = 'run') {
  const position = actor.entity.getPosition();
  const distance = distanceXZ(position, destination);
  if (distance <= stopDistance) return true;
  const speedMultiplier = actor.slowRemaining > 0 ? 0.48 : 1;
  const maxStep = Math.min(actor.speed * speedMultiplier * dt, Math.max(0, distance - stopDistance));
  const obstacle = firstBlockingStaticCollider(actor, position, destination);
  const steeringTarget = obstacle
    ? obstacleWaypoint(actor, position, destination, obstacle)
    : destination;
  const next = moveTowardXZ(position, steeringTarget, maxStep);
  const dx = next.x - position.x;
  const dz = next.z - position.z;
  const heading = Math.atan2(dz, dx);
  const side = actor.avoidanceSide ?? 1;
  const angleOffsets = obstacle
    ? [0, side * 24, side * 48, side * 72, -side * 32, -side * 64, side * 96, -side * 96]
    : [0, side * 22, -side * 22, side * 45, -side * 45, side * 72, -side * 72, 90, -90];
  const candidates = angleOffsets.map((offset) => {
    const angle = heading + offset * pc.math.DEG_TO_RAD;
    return {
      x: position.x + Math.cos(angle) * maxStep,
      z: position.z + Math.sin(angle) * maxStep,
      offset
    };
  });
  let chosen = null;
  let chosenScore = Infinity;
  for (const candidate of candidates) {
    if (isCandidateBlocked(actor, position, candidate)) continue;
    const score = distanceXZ(candidate, steeringTarget)
      + Math.abs(candidate.offset) * 0.0015
      + (Math.sign(candidate.offset) === -side ? 0.035 : 0);
    if (score < chosenScore) {
      chosen = candidate;
      chosenScore = score;
    }
  }

  if (!chosen) {
    actor.state = 'idle';
    actor.blockedElapsed = (actor.blockedElapsed ?? 0) + dt;
    if (actor.blockedElapsed > 0.24) actor.avoidanceSide = -(actor.avoidanceSide ?? 1);
    return false;
  }

  actor.blockedElapsed = 0;
  actor.entity.setPosition(chosen.x, 0, chosen.z);
  faceToward(actor.entity, steeringTarget);
  actor.state = movementState;
  return distanceXZ(chosen, destination) <= stopDistance + 0.02;
}

function firstBlockingStaticCollider(actor, start, destination) {
  const actorRadius = actor.collisionRadius ?? 0.45;
  let first = null;
  let firstT = Infinity;
  for (const collider of staticColliders) {
    if (!collider.entity.enabled) continue;
    const center = colliderWorldPosition(collider);
    const intersection = pointSegmentDistanceXZ(center, start, destination);
    const clearance = actorRadius + collider.radius + 0.16;
    if (intersection.t <= 0.015 || intersection.t >= 0.985 || intersection.distance >= clearance) continue;
    if (intersection.t < firstT) {
      first = { collider, center, clearance };
      firstT = intersection.t;
    }
  }
  return first;
}

function obstacleWaypoint(actor, start, destination, obstacle) {
  const dx = destination.x - start.x;
  const dz = destination.z - start.z;
  const length = Math.max(0.001, Math.hypot(dx, dz));
  const forwardX = dx / length;
  const forwardZ = dz / length;
  const side = actor.avoidanceSide ?? 1;
  const detour = obstacle.clearance * 1.42 + 0.18;
  const forward = Math.min(obstacle.clearance * 0.5, length * 0.2);
  return {
    x: obstacle.center.x - forwardZ * detour * side + forwardX * forward,
    z: obstacle.center.z + forwardX * detour * side + forwardZ * forward
  };
}

function isCandidateBlocked(actor, current, candidate) {
  const actorRadius = actor.collisionRadius ?? 0.45;
  for (const collider of staticColliders) {
    if (!collider.entity.enabled) continue;
    const center = colliderWorldPosition(collider);
    const currentPenetration = circlePenetration(current, actorRadius, center, collider.radius);
    const nextPenetration = circlePenetration(candidate, actorRadius, center, collider.radius);
    if (nextPenetration > 0.015 && nextPenetration > currentPenetration + 0.001) return true;
  }

  const actors = actor === hero ? mobs : [hero, ...mobs];
  for (const other of actors) {
    if (other === actor || other.hp <= 0 || other.alive === false || !other.entity.enabled) continue;
    const otherPosition = other.entity.getPosition();
    const currentPenetration = circlePenetration(
      current,
      actorRadius,
      otherPosition,
      other.collisionRadius ?? 0.45
    );
    const nextPenetration = circlePenetration(
      candidate,
      actorRadius,
      otherPosition,
      other.collisionRadius ?? 0.45
    );
    if (nextPenetration > 0.01 && nextPenetration > currentPenetration + 0.001) return true;
  }
  return false;
}

function updateMobs(dt) {
  const now = performance.now();
  for (const mob of mobs) {
    if (!mob.alive) {
      if (mob.state === 'dying') {
        mob.deathElapsed += dt;
        if (!mob.rig.productionModel) {
          mob.entity.setEulerAngles(
            pc.math.lerp(0, 82, Math.min(1, mob.deathElapsed / 0.52)),
            mob.entity.getEulerAngles().y,
            0
          );
        }
        if (mob.deathElapsed > (mob.rig.productionModel ? 1.15 : 0.7)) {
          mob.entity.enabled = false;
          mob.state = 'dead';
        }
      } else if (!mob.isBoss && now >= mob.respawnAt) {
        respawnMob(mob);
      }
      continue;
    }

    mob.animationTime += dt;
    mob.attackCooldown = Math.max(0, mob.attackCooldown - dt);
    mob.slowRemaining = Math.max(0, (mob.slowRemaining ?? 0) - dt);
    mob.hitFlash = Math.max(0, mob.hitFlash - dt);
    const mobPosition = mob.entity.getPosition();
    const heroPosition = hero.entity.getPosition();
    const heroDistance = distanceXZ(mobPosition, heroPosition);
    const heroInRefuge = distanceXZ(heroPosition, REFUGE_POSITION) < REFUGE_SAFE_RADIUS;
    const mobInRefuge = distanceXZ(mobPosition, REFUGE_POSITION) < REFUGE_SAFE_RADIUS;
    if (heroInRefuge) mob.aggro = false;
    else if (heroDistance < 5.2) mob.aggro = true;
    if (heroDistance > 14) mob.aggro = false;

    if (mobInRefuge) {
      mob.aggro = false;
      const away = mobPosition.clone().sub(REFUGE_POSITION);
      if (away.lengthSq() < 0.01) away.set(1, 0, 0);
      away.normalize().mulScalar(REFUGE_SAFE_RADIUS + 1.2).add(REFUGE_POSITION);
      moveActor(mob, away, dt, 0.2, 'run');
    } else if (mob.attackElapsed > 0) {
      updateMobAttack(mob, dt);
    } else if (mob.aggro && hero.hp > 0) {
      faceToward(mob.entity, hero.entity.getPosition());
      if (heroDistance > mob.attackRange) {
        moveActor(mob, hero.entity.getPosition(), dt, mob.attackRange * 0.88);
      } else if (mob.attackCooldown <= 0) {
        mob.attackElapsed = 0.0001;
        mob.damageApplied = false;
        mob.state = 'attack';
      } else {
        mob.state = 'idle';
      }
    } else {
      updateWander(mob, dt);
    }

    animateRig(mob.rig, mob.state, mob.animationTime, mob.attackElapsed / 0.9, true);
  }
}

function resolveMobSeparation() {
  for (let firstIndex = 0; firstIndex < mobs.length; firstIndex += 1) {
    const first = mobs[firstIndex];
    if (!first.alive || !first.entity.enabled) continue;
    for (let secondIndex = firstIndex + 1; secondIndex < mobs.length; secondIndex += 1) {
      const second = mobs[secondIndex];
      if (!second.alive || !second.entity.enabled) continue;
      const firstPosition = first.entity.getPosition();
      const secondPosition = second.entity.getPosition();
      const penetration = circlePenetration(
        firstPosition,
        first.collisionRadius,
        secondPosition,
        second.collisionRadius
      );
      if (penetration <= 0) continue;

      let dx = firstPosition.x - secondPosition.x;
      let dz = firstPosition.z - secondPosition.z;
      let length = Math.hypot(dx, dz);
      if (length < 0.0001) {
        dx = firstIndex % 2 ? 1 : -1;
        dz = secondIndex % 2 ? 0.5 : -0.5;
        length = Math.hypot(dx, dz);
      }
      const push = Math.min(0.12, penetration * 0.52);
      const nx = dx / length;
      const nz = dz / length;
      const firstCandidate = { x: firstPosition.x + nx * push, z: firstPosition.z + nz * push };
      const secondCandidate = { x: secondPosition.x - nx * push, z: secondPosition.z - nz * push };
      if (!isCandidateBlocked(first, firstPosition, firstCandidate)) {
        first.entity.setPosition(firstCandidate.x, 0, firstCandidate.z);
      }
      if (!isCandidateBlocked(second, secondPosition, secondCandidate)) {
        second.entity.setPosition(secondCandidate.x, 0, secondCandidate.z);
      }
    }
  }
}

function updateWander(mob, dt) {
  mob.wanderTimer -= dt;
  if (mob.wanderTimer <= 0) {
    mob.wanderTimer = 2.5 + seededRandom() * 3.5;
    const angle = seededRandom() * Math.PI * 2;
    const radius = 1.5 + seededRandom() * 3.5;
    mob.wanderTarget = new pc.Vec3(
      mob.spawn.x + Math.cos(angle) * radius,
      0,
      mob.spawn.z + Math.sin(angle) * radius
    );
  }
  if (mob.wanderTarget && !moveActor(mob, mob.wanderTarget, dt * 0.42, 0.2, 'walk')) return;
  mob.wanderTarget = null;
  mob.state = 'idle';
}

function updateMobAttack(mob, dt) {
  mob.attackElapsed += dt;
  const progress = mob.attackElapsed / 0.9;
  mob.state = 'attack';
  faceToward(mob.entity, hero.entity.getPosition());
  if (progress >= 0.55 && !mob.damageApplied && distanceXZ(mob.entity.getPosition(), hero.entity.getPosition()) < 2.1) {
    mob.damageApplied = true;
    damageHero(mob.attackDamage ?? 7);
  }
  if (progress >= 1) {
    mob.attackElapsed = 0;
    mob.attackCooldown = 0.62;
  }
}

function damageMob(mob, amount) {
  mob.hp = Math.max(0, mob.hp - amount);
  mob.aggro = true;
  mob.hitFlash = 0.18;
  spawnHitEffect(mob.entity.getPosition(), false);
  audio.cue('impact');
  if (mob.hp <= 0) killMob(mob);
}

function killMob(mob) {
  mob.alive = false;
  mob.state = 'dying';
  mob.deathElapsed = 0;
  mob.respawnAt = mob.isBoss ? Infinity : performance.now() + 8000;
  const deathClip = firstAvailable(
    mob.rig.productionModel?._productionClips,
    ['Death_C_Skeletons', 'Death_A', 'Death_B', 'Death']
  );
  if (deathClip) transitionModel(mob.rig.productionModel, deathClip, 0.08, true);
  if (mob.isBoss) {
    const result = defeatBoss(expeditionProgress);
    if (result.success) {
      expeditionProgress = result.progress;
      if (overseerArena) overseerArena.enabled = false;
      pendingBossRewards = createBossRewardChoices(seededRandom, itemSerial + 1);
      itemSerial += pendingBossRewards.length;
      hero.loot += 8;
      discoveryProgress = unlockDiscovery(
        discoveryProgress,
        DISCOVERIES.FOURTH_ACTIVE_ABILITY,
        'first-boss'
      );
      recordTraining('boss');
      ensureDiscoveredAbility();
      renderAbilityPanel();
      renderBossRewardPanel();
      persistProgress('Overseer defeated · reward choice saved');
      showToast('Overseer defeated · choose one rare reward');
      audio.cue('boss');
    }
  } else {
    spawnDrop(mob.entity.getPosition(), mob);
    recordTraining('combat');
  }
  if (hero.targetEnemy === mob) clearTarget();
  if (!mob.isBoss) showToast(`${mob.name} felled · essence dropped`);
}

function respawnMob(mob) {
  const angle = seededRandom() * Math.PI * 2;
  mob.entity.enabled = true;
  mob.entity.setPosition(
    mob.spawn.x + Math.cos(angle) * 1.2,
    0,
    mob.spawn.z + Math.sin(angle) * 1.2
  );
  mob.entity.setEulerAngles(0, seededRandom() * 360, 0);
  mob.hp = mob.maxHp;
  mob.alive = true;
  mob.aggro = false;
  mob.state = 'idle';
  mob.deathElapsed = 0;
  mob.attackElapsed = 0;
  mob.slowRemaining = 0;
  mob.wanderTarget = null;
  const idleClip = firstAvailable(
    mob.rig.productionModel?._productionClips,
    ['Idle_Combat', 'Idle', 'Idle_Attacking']
  );
  if (idleClip) transitionModel(mob.rig.productionModel, idleClip, 0, true);
}

function damageHero(amount) {
  if (hero.invulnerable > 0 || expeditionProgress.rewardPending) return;
  const damageReduction = getActiveCompanionBonuses(companionRoster).modifiers.heroDamageReduction ?? 0;
  amount = Math.max(1, Math.round(amount * (1 - damageReduction)));
  if (hero.wardRemaining > 0 && hero.wardAbsorb > 0) {
    const absorbed = Math.min(amount, hero.wardAbsorb);
    hero.wardAbsorb -= absorbed;
    amount -= absorbed;
    showToast(`Glacial Ward absorbed ${absorbed}`);
    if (amount <= 0) {
      hero.invulnerable = 0.1;
      return;
    }
  }
  cancelGathering();
  selector.enabled = false;
  hero.hp = Math.max(0, hero.hp - amount);
  hero.invulnerable = 0.16;
  spawnHitEffect(hero.entity.getPosition(), true);
  audio.cue('impact');
  canvas.animate(
    [{ filter: 'brightness(1)' }, { filter: 'brightness(1.35) sepia(.3)' }, { filter: 'brightness(1)' }],
    { duration: 180 }
  );
}

function handleHeroDefeat() {
  if (expeditionProgress.state === 'defeated') return;
  const lostResources = { ...hero.resources };
  expeditionProgress = defeatExpedition(expeditionProgress, lostResources, Date.now());
  routeProgress = normalizeRouteProgress({
    ...routeProgress,
    location: 'refuge',
    selectedRouteId: null,
    activeRouteId: null
  });
  pendingBossRewards = [];
  hero.resources = createResourceStock();
  hero.entity.enabled = false;
  hero.attackElapsed = 0;
  hero.activeAbility = null;
  hero.requestedAbility = null;
  hero.moveTarget = null;
  hero.targetEnemy = null;
  hero.refugeTarget = null;
  cancelGathering();
  hero.auto = false;
  selector.enabled = false;
  wardVisual.enabled = false;
  ui.autoToggle.setAttribute('aria-pressed', 'false');
  ui.autoLabel.textContent = 'OFF';
  for (const mob of mobs) mob.aggro = false;
  if (overseerArena) overseerArena.enabled = false;
  renderDefeatState();
  audio.cue('defeat');
  persistProgress('Expedition defeat saved');
}

function restoreHeroAtRefuge() {
  hero.hp = hero.maxHp;
  hero.focus = hero.maxFocus;
  hero.entity.enabled = true;
  companion.entity.enabled = true;
  hero.entity.setPosition(REFUGE_POSITION.x + 1.8, 0, REFUGE_POSITION.z + 0.5);
  hero.entity.setEulerAngles(0, 180, 0);
  hero.attackElapsed = 0;
  hero.activeAbility = null;
  hero.requestedAbility = null;
  hero.wardRemaining = 0;
  hero.wardAbsorb = 0;
  wardVisual.enabled = false;
  hero.moveTarget = null;
  hero.targetEnemy = null;
  cancelGathering();
  hero.auto = false;
  ui.autoToggle.setAttribute('aria-pressed', 'false');
  ui.autoLabel.textContent = 'OFF';
  for (const mob of mobs) mob.aggro = false;
}

function resetOverseer() {
  if (!overseer) return;
  const difficulty = overseerDifficulty(expeditionProgress);
  const activeRoute = ROUTES.find((route) => route.id === routeProgress.activeRouteId);
  const routeMultiplier = activeRoute?.difficulty === 'hard' ? 1.25 : 1;
  overseer.maxHp = Math.round(difficulty.maxHp * routeMultiplier);
  overseer.attackDamage = Math.round(difficulty.attackDamage * routeMultiplier);
  overseer.kind = `HOLLOW VEIN OVERSEER · TIER ${difficulty.tier}`;
  overseer.entity.setPosition(overseer.spawn);
  overseer.entity.setEulerAngles(0, 180, 0);
  overseer.hp = overseer.maxHp;
  overseer.alive = true;
  overseer.aggro = false;
  overseer.state = 'idle';
  overseer.deathElapsed = 0;
  overseer.attackElapsed = 0;
  overseer.attackCooldown = 0;
  overseer.respawnAt = Infinity;
  overseer.wanderTarget = null;
  overseer.entity.enabled = true;
  const idleClip = firstAvailable(
    overseer.rig.productionModel?._productionClips,
    ['Idle_Combat', 'Idle', 'Idle_Attacking']
  );
  if (idleClip) transitionModel(overseer.rig.productionModel, idleClip, 0, true);
}

function handleBossRewardChoice(event) {
  const button = event.target.closest('[data-boss-reward]');
  if (!button || !expeditionProgress.rewardPending) return;
  const item = pendingBossRewards.find((candidate) => candidate.id === button.dataset.bossReward);
  if (!item) return;
  const result = claimBossReward(expeditionProgress);
  if (!result.success) return;

  expeditionProgress = result.progress;
  hero.inventory.push(item);
  hero.equipment = equipInventoryItem(hero.inventory, hero.equipment, item.id);
  recordEquipmentAbility(item);
  recordTraining('equipment');
  pendingBossRewards = [];
  refreshGearStats();
  applyEquipmentVisuals();
  renderInventory();
  renderBossRewardPanel();
  persistProgress('Overseer reward claimed · extraction ready');
  showToast(`${item.name} equipped · return to Warden Refuge`);
}

function renderBossRewardPanel() {
  const show = expeditionProgress.rewardPending && pendingBossRewards.length > 0;
  ui.bossRewardPanel.hidden = !show;
  if (!show) {
    ui.bossRewardList.innerHTML = '';
    return;
  }
  ui.bossRewardList.innerHTML = pendingBossRewards.map((item) => `
    <button class="boss-reward" type="button" data-boss-reward="${item.id}">
      <span class="reward-slot">RARE · ${item.slot.toUpperCase()}</span>
      <strong>${item.name}</strong>
      <small>${item.effect}</small>
    </button>
  `).join('');
}

function syncBossAvailability(announce = false) {
  const wasAvailable = expeditionProgress.bossState === 'available';
  const routeActive = routeProgress.location === 'route' && Boolean(routeProgress.activeRouteId);
  expeditionProgress = updateBossAvailability(
    expeditionProgress,
    townProgress.quest.completed && routeActive
  );
  const isAvailable = (
    expeditionProgress.state === 'hunting'
    && expeditionProgress.bossState === 'available'
  );
  if (isAvailable && (!wasAvailable || !overseer.alive)) resetOverseer();
  if (overseer) overseer.entity.enabled = isAvailable && overseer.alive;
  if (overseerArena) overseerArena.enabled = isAvailable;
  if (announce && isAvailable && !wasAvailable) {
    showToast('The Corrupted Mine Overseer stirs in the northern hollow');
  }
}

function renderDefeatState() {
  const defeated = expeditionProgress.state === 'defeated';
  ui.defeatPanel.hidden = !defeated;
  hero.entity.enabled = !defeated;
  companion.entity.enabled = !defeated;
  if (!defeated) return;
  const recovery = expeditionProgress.recovery;
  ui.defeatCopy.textContent = recovery
    ? 'The expedition ended immediately. Your gathered cache can be reclaimed from Warden Refuge for 24 hours.'
    : 'The expedition ended immediately. No gathered materials were lost.';
  ui.defeatResources.textContent = recovery
    ? resourceSummary(recovery.resources)
    : 'No materials lost';
}

function animateRig(rig, state, time, attackProgress, enemy, abilityId = null) {
  if (rig.productionModel) {
    const clips = rig.productionModel._productionClips;
    let clip;
    if (state === 'walk') {
      clip = firstAvailable(clips, ['Walking_D_Skeletons', 'Walking_A', 'Walk_Loop', 'Walk', 'Running_A', 'Run']);
    } else if (state === 'run') {
      clip = firstAvailable(
        clips,
        enemy
          ? ['Running_A', 'Running_B', 'Walking_D_Skeletons', 'Run', 'Walk']
          : ['Jog_Fwd_Loop', 'Sprint_Loop', 'Run_Holding', 'Run', 'Walk_Loop', 'Walk']
      );
    } else if (state === 'attack') {
      const abilityClips = {
        glacialWard: ['Spell_Simple_Enter', 'Interact', 'Spell_Simple_Shoot'],
        frostNova: ['Spell_Simple_Exit', 'Punch_Cross', 'Spell_Simple_Shoot'],
        frostLance: ['Spell_Simple_Enter', 'Bow_Shoot', 'Spell_Simple_Shoot'],
        iceShard: ['Spell_Simple_Shoot', 'Pistol_Shoot', 'Punch_Jab']
      };
      clip = firstAvailable(
        clips,
        enemy
          ? ['1H_Melee_Attack_Slice_Diagonal', '1H_Melee_Attack_Chop', 'Weapon', 'Punch']
          : abilityClips[abilityId] ?? ['Spell_Simple_Shoot', 'Sword_Attack', 'Bow_Shoot', 'Punch_Cross', 'Punch']
      );
    } else if (state === 'gather') {
      clip = firstAvailable(clips, ['Interact', 'PickUp_Table', 'Fixing_Kneeling', 'Spell_Simple_Enter']);
    } else {
      clip = firstAvailable(
        clips,
        enemy
          ? ['Idle_Combat', 'Idle_B', 'Idle', 'Idle_Attacking']
          : ['Idle_Loop', 'Sword_Idle', 'Idle_Weapon', 'Idle_Attacking', 'Idle']
      );
    }
    if (clip) transitionModel(rig.productionModel, clip, state === 'attack' || state === 'gather' ? 0.08 : 0.16);
    return;
  }

  const locomoting = state === 'walk' || state === 'run';
  const strideRate = state === 'run' ? (enemy ? 8 : 10) : 5.2;
  const walk = locomoting ? Math.sin(time * strideRate) : 0;
  const idle = Math.sin(time * 2.2);
  const stride = state === 'run' ? 1 : 0.58;
  let leftArm = locomoting ? walk * 34 * stride : idle * 3;
  let rightArm = locomoting ? -walk * 34 * stride : -idle * 3;
  let leftLeg = locomoting ? -walk * 38 * stride : 0;
  let rightLeg = locomoting ? walk * 38 * stride : 0;
  let torsoZ = idle * 1.2;

  if (state === 'attack') {
    const windup = Math.min(1, attackProgress / 0.36);
    const strike = attackProgress < 0.36 ? windup : 1 - Math.min(1, (attackProgress - 0.36) / 0.42);
    if (!enemy && abilityId === 'glacialWard') {
      leftArm = pc.math.lerp(-20, -108, windup);
      rightArm = pc.math.lerp(20, -108, windup);
      torsoZ = pc.math.lerp(0, -7, windup);
    } else if (!enemy && abilityId === 'frostNova') {
      leftArm = pc.math.lerp(15, -82, strike);
      rightArm = pc.math.lerp(-15, 82, strike);
      torsoZ = pc.math.lerp(-12, 16, strike);
    } else if (!enemy && (abilityId === 'iceShard' || abilityId === 'frostLance')) {
      leftArm = pc.math.lerp(-92, 65, strike);
      rightArm = -18;
      torsoZ = pc.math.lerp(-8, 10, strike);
    } else {
      rightArm = pc.math.lerp(-92, 65, strike);
      leftArm = enemy ? pc.math.lerp(10, -35, strike) : -20;
      torsoZ = pc.math.lerp(-8, 13, strike);
    }
  } else if (state === 'gather') {
    const gatherMotion = (Math.sin(time * 8) + 1) * 0.5;
    leftArm = pc.math.lerp(-35, -92, gatherMotion);
    rightArm = pc.math.lerp(-28, -78, gatherMotion);
    torsoZ = pc.math.lerp(-8, -18, gatherMotion);
  }

  rig.leftArm.setLocalEulerAngles(leftArm, 0, enemy ? -10 : 7);
  rig.rightArm.setLocalEulerAngles(rightArm, 0, enemy ? 10 : -7);
  rig.leftLeg.setLocalEulerAngles(leftLeg, 0, 0);
  rig.rightLeg.setLocalEulerAngles(rightLeg, 0, 0);
  rig.torso.setLocalEulerAngles(0, 0, torsoZ);
  rig.hips.setLocalPosition(0, 0.88 + (locomoting ? Math.abs(walk) * 0.045 * stride : idle * 0.012), 0);
  rig.slots.back.setLocalEulerAngles(
    locomoting ? 7 + Math.abs(walk) * 7 * stride : 4 + idle * 1.5,
    0,
    locomoting ? walk * 2.5 * stride : 0
  );
  rig.slots.head.setLocalEulerAngles(state === 'attack' ? -4 : idle * 1.2, 0, locomoting ? walk * 1.4 * stride : 0);
}

function firstAvailable(clips, candidates) {
  return candidates.find((name) => clips?.has(name));
}

function faceToward(entity, destination) {
  const position = entity.getPosition();
  const dx = destination.x - position.x;
  const dz = destination.z - position.z;
  if (Math.abs(dx) + Math.abs(dz) < 0.001) return;
  entity.setEulerAngles(0, Math.atan2(dx, dz) * pc.math.RAD_TO_DEG, 0);
}

function clearTarget() {
  hero.targetEnemy = null;
  if (hero.activeAbility === 'iceShard' || hero.activeAbility === 'frostLance') {
    hero.attackElapsed = 0;
    hero.activeAbility = null;
    hero.damageApplied = false;
  }
}

function spawnDrop(position, mob) {
  const entity = new pc.Entity('Blight essence');
  entity.setPosition(position.x, 0, position.z);
  world.addChild(entity);
  const gem = primitive('sphere', 'Essence crystal', entity, [0.24, 0.42, 0.24], [0, 0.45, 0], PALETTE.loot, false);
  gem.setLocalEulerAngles(0, 0, 45);
  drops.push({ kind: 'essence', entity, gem, born: elapsed, value: 1 + Math.floor(seededRandom() * 3) });

  const champion = mob?.isBoss || mob?.name.includes('Champion');
  if (champion || seededRandom() < 0.42) {
    const item = rollLootItem(seededRandom, ++itemSerial, champion);
    spawnItemDrop(position, item);
  }
}

function spawnItemDrop(position, item) {
  const root = new pc.Entity(`${item.name} drop`);
  root.setPosition(position.x + 0.6, 0, position.z + 0.28);
  world.addChild(root);
  const dropMaterial = item.rarity === 'rare' ? PALETTE.itemRare : PALETTE.itemCommon;
  const gem = primitive('box', `${item.name} token`, root, [0.24, 0.34, 0.24], [0, 0.52, 0], dropMaterial, false);
  gem.setLocalEulerAngles(28, 45, 28);
  const beam = primitive(
    'cylinder',
    `${item.rarity} loot beam`,
    root,
    [0.025, item.rarity === 'rare' ? 3.6 : 2.2, 0.025],
    [0, item.rarity === 'rare' ? 1.9 : 1.2, 0],
    dropMaterial,
    false
  );
  drops.push({ kind: 'item', entity: root, gem, beam, item, born: elapsed });
}

function updateDrops(dt) {
  for (let index = drops.length - 1; index >= 0; index -= 1) {
    const drop = drops[index];
    drop.gem.setLocalPosition(0, 0.48 + Math.sin((elapsed - drop.born) * 4) * 0.12, 0);
    drop.gem.rotateLocal(0, 95 * dt, 0);
    if (drop.beam) {
      const pulse = 0.75 + Math.sin((elapsed - drop.born) * 5) * 0.18;
      drop.beam.setLocalScale(0.025 * pulse, drop.item.rarity === 'rare' ? 3.6 : 2.2, 0.025 * pulse);
    }
    if (distanceXZ(hero.entity.getPosition(), drop.entity.getPosition()) < 1.25) {
      if (drop.kind === 'item') {
        if (hero.inventory.length >= 18) {
          if (!drop.packWarningShown) {
            drop.packWarningShown = true;
            showToast('Field pack full');
          }
          continue;
        }
        hero.inventory.push(drop.item);
        recordEquipmentAbility(drop.item);
        renderInventory();
        persistProgress('Recovered equipment saved');
        showToast(`${drop.item.rarity === 'rare' ? 'Rare · ' : ''}${drop.item.name} recovered`);
        audio.cue('loot');
      } else {
        hero.loot += drop.value;
        persistProgress('Recovered essence saved');
        showToast(`Recovered ${drop.value} blight essence`);
        audio.cue('loot');
      }
      drop.entity.destroy();
      drops.splice(index, 1);
    }
  }
}

function spawnHitEffect(position, onHero) {
  const root = new pc.Entity('Impact');
  root.setPosition(position.x, 1.15, position.z);
  world.addChild(root);
  for (let i = 0; i < 5; i += 1) {
    const spark = primitive('sphere', 'Spark', root, [0.08, 0.08, 0.08], [0, 0, 0], onHero ? PALETTE.ember : PALETTE.hit, false);
    const angle = (i / 5) * Math.PI * 2;
    spark._velocity = new pc.Vec3(Math.cos(angle) * 2.4, 1.5 + seededRandom(), Math.sin(angle) * 2.4);
  }
  effects.push({ root, age: 0 });
}

function castGlacialWard(ability) {
  hero.wardRemaining = ability.duration;
  hero.wardAbsorb = ability.absorb + hero.gearStats.wardAbsorb;
  wardVisual.enabled = true;
  wardVisual.setLocalScale(0.72, 0.72, 0.72);
  showToast(`Glacial Ward · ${hero.wardAbsorb} damage barrier`);
}

function castFrostNova(ability) {
  const heroPosition = hero.entity.getPosition();
  const radius = ability.radius + hero.gearStats.novaRadius;
  const damage = ability.damage + hero.gearStats.damage;
  const root = new pc.Entity('Frost Nova');
  root.setPosition(heroPosition.x, 0.18, heroPosition.z);
  world.addChild(root);

  for (let index = 0; index < 18; index += 1) {
    const angle = (index / 18) * Math.PI * 2;
    const shard = primitive(
      'cone',
      'Nova ice fragment',
      root,
      [0.09, 0.32, 0.09],
      [Math.cos(angle) * 0.4, 0.1, Math.sin(angle) * 0.4],
      PALETTE.frostNova,
      false
    );
    shard.setLocalEulerAngles(78, -angle * pc.math.RAD_TO_DEG, 0);
    shard._velocity = new pc.Vec3(Math.cos(angle) * 5.4, 0.5, Math.sin(angle) * 5.4);
  }
  effects.push({ root, age: 0, duration: 0.72, kind: 'nova' });

  let struck = 0;
  for (const mob of mobs) {
    if (!mob.alive || !mob.entity.enabled) continue;
    if (distanceXZ(heroPosition, mob.entity.getPosition()) > radius) continue;
    mob.slowRemaining = Math.max(mob.slowRemaining ?? 0, ability.slowDuration);
    damageMob(mob, damage);
    struck += 1;
  }
  showToast(struck ? `Frost Nova struck ${struck}` : 'Frost Nova released');
}

function launchElementalShard(target, damage, scale = 1) {
  const element = ICE_SHARD;
  const root = new pc.Entity(`${element.name} elemental shard`);
  const targetPosition = target.entity.getPosition().clone();
  targetPosition.y = 1.05;
  const start = hero.rig.castHand
    ? hero.rig.castHand.getPosition().clone()
    : hero.entity.getWorldTransform().transformPoint(new pc.Vec3(0.42, 1.48, 0.34));
  const palmDirection = targetPosition.clone().sub(start).normalize();
  start.add(palmDirection.mulScalar(0.16));
  root.setPosition(start);
  root.setLocalScale(scale, scale, scale);
  world.addChild(root);
  primitive('box', `${element.name} shard core`, root, [0.14, 0.14, 0.52], [0, 0, 0], element.material, false)
    .setLocalEulerAngles(0, 0, 45);
  primitive('cone', `${element.name} shard point`, root, [0.14, 0.3, 0.14], [0, 0, -0.4], element.material, false)
    .setLocalEulerAngles(90, 0, 0);
  primitive('cone', `${element.name} shard tail`, root, [0.1, 0.2, 0.1], [0, 0, 0.34], element.material, false)
    .setLocalEulerAngles(-90, 0, 0);
  const glow = new pc.Entity(`${element.name} shard glow`);
  glow.addComponent('light', {
    type: 'omni',
    color: new pc.Color(...element.light),
    intensity: 0.7,
    range: 2.5,
    castShadows: false
  });
  root.addChild(glow);
  root.lookAt(targetPosition);
  projectiles.push({ entity: root, target, damage, speed: scale > 1 ? 13.5 : 11.5, element });
}

function updateProjectiles(dt) {
  for (let index = projectiles.length - 1; index >= 0; index -= 1) {
    const projectile = projectiles[index];
    if (!projectile.target.alive) {
      projectile.entity.destroy();
      projectiles.splice(index, 1);
      continue;
    }
    const position = projectile.entity.getPosition();
    const targetPosition = projectile.target.entity.getPosition().clone();
    targetPosition.y = 1.05;
    const distance = position.distance(targetPosition);
    if (distance < 0.48) {
      damageMob(projectile.target, projectile.damage);
      spawnElementalImpact(targetPosition, projectile.element);
      projectile.entity.destroy();
      projectiles.splice(index, 1);
      continue;
    }
    const step = targetPosition.clone().sub(position).normalize().mulScalar(Math.min(distance, projectile.speed * dt));
    projectile.entity.translate(step);
    projectile.entity.lookAt(targetPosition);
    projectile.entity.rotateLocal(0, 0, 720 * dt);
  }
}

function spawnElementalImpact(position, element) {
  const root = new pc.Entity(`${element.name} shard impact`);
  root.setPosition(position);
  world.addChild(root);
  for (let index = 0; index < 7; index += 1) {
    const angle = (index / 7) * Math.PI * 2;
    const spark = primitive(
      'cone',
      `${element.name} fragment`,
      root,
      [0.055, 0.2, 0.055],
      [0, 0, 0],
      element.material,
      false
    );
    spark._velocity = new pc.Vec3(
      Math.cos(angle) * (1.8 + seededRandom()),
      1.2 + seededRandom() * 1.4,
      Math.sin(angle) * (1.8 + seededRandom())
    );
  }
  effects.push({ root, age: 0 });
}

function updateEffects(dt) {
  for (let index = effects.length - 1; index >= 0; index -= 1) {
    const effect = effects[index];
    effect.age += dt;
    for (const spark of effect.root.children) {
      spark.translate(spark._velocity.x * dt, spark._velocity.y * dt, spark._velocity.z * dt);
      if (effect.kind === 'nova') {
        spark._velocity.y -= 1.2 * dt;
        spark.rotateLocal(0, 0, 420 * dt);
        const scale = Math.max(0.02, 1 - effect.age / effect.duration);
        spark.setLocalScale(0.09 * scale, 0.32 * scale, 0.09 * scale);
      } else {
        spark._velocity.y -= 5 * dt;
        const scale = Math.max(0.02, 0.08 * (1 - effect.age / 0.5));
        spark.setLocalScale(scale, scale, scale);
      }
    }
    if (effect.age > (effect.duration ?? 0.5)) {
      effect.root.destroy();
      effects.splice(index, 1);
    }
  }
}

function updateCamera(dt) {
  const heroPosition = hero.entity.getPosition();
  const smoothing = 1 - Math.exp(-dt * 4.8);
  cameraFocus.lerp(cameraFocus, new pc.Vec3(heroPosition.x, 0.9, heroPosition.z), smoothing);
  positionCamera();
}

function updateOccluders(dt) {
  const cameraPosition = camera.getPosition();
  const heroPosition = hero.entity.getPosition();
  const visibleEnemies = mobs.filter((mob) => (
    mob.alive
    && mob.entity.enabled
    && (mob === hero.targetEnemy || mob.aggro)
    && distanceXZ(heroPosition, mob.entity.getPosition()) < 26
  ));
  const smoothing = 1 - Math.exp(-dt * 10);

  for (const occluder of occluders) {
    let shouldFade = false;
    if (occluder.entity.enabled) {
      const occluderPosition = occluder.entity.getPosition();
      for (const mob of visibleEnemies) {
        const enemyPosition = mob.entity.getPosition();
        const line = pointSegmentDistanceXZ(occluderPosition, cameraPosition, enemyPosition);
        if (line.t > 0.08 && line.t < 0.96 && line.distance < occluder.radius) {
          shouldFade = true;
          break;
        }
      }
    }

    const targetOpacity = shouldFade ? 0.32 : 1;
    const nextOpacity = pc.math.lerp(occluder.opacity, targetOpacity, smoothing);
    if (Math.abs(nextOpacity - occluder.opacity) < 0.002) continue;
    occluder.opacity = nextOpacity;

    const transparent = nextOpacity < 0.985;
    for (const material of occluder.materials) {
      material.opacity = nextOpacity;
      material.blendType = transparent ? pc.BLEND_NORMAL : pc.BLEND_NONE;
      material.depthWrite = !transparent;
      material.update();
    }
  }
}

function updateEnemyOutlines(dt) {
  enemyOutlineRenderer.frameUpdate(camera, outlineLayer, true);
  enemyOutlineTimer -= dt;
  if (enemyOutlineTimer > 0) return;
  enemyOutlineTimer = 0.08;

  for (const mob of mobs) {
    const outlineEntity = mob.rig.productionModel;
    const isTargeted = Boolean(
      outlineEntity
      && mob.alive
      && mob.entity.enabled
      && mob === hero.targetEnemy
    );
    const isHiddenNearby = Boolean(
      outlineEntity
      && mob.alive
      && mob.entity.enabled
      && distanceXZ(hero.entity.getPosition(), mob.entity.getPosition()) <= 10
      && isMobCameraOccluded(mob)
    );
    const desiredOutline = isTargeted ? 'target' : isHiddenNearby ? 'hidden' : null;

    if (mob.outlineEntity && mob.outlineEntity !== outlineEntity) {
      enemyOutlineRenderer.removeEntity(mob.outlineEntity);
      mob.outlineKind = null;
    }
    mob.outlineEntity = outlineEntity;

    if (mob.outlineKind && mob.outlineKind !== desiredOutline) {
      enemyOutlineRenderer.removeEntity(outlineEntity);
      mob.outlineKind = null;
    }
    if (desiredOutline && !mob.outlineKind) {
      enemyOutlineRenderer.addEntity(
        outlineEntity,
        desiredOutline === 'target' ? targetOutlineColor : enemyOutlineColor
      );
      mob.outlineKind = desiredOutline;
    }
  }
}

function isMobCameraOccluded(mob) {
  const origin = camera.getPosition();
  const target = mob.entity.getPosition().clone();
  target.y = 1.15;
  const direction = target.clone().sub(origin);
  const targetDistance = direction.length();
  if (targetDistance < 0.5) return false;
  direction.mulScalar(1 / targetDistance);
  const ray = new pc.Ray(origin, direction);
  const hitPoint = new pc.Vec3();

  for (const collider of staticColliders) {
    if (!collider.entity.enabled || !collider.meshInstances.length) continue;
    const center = colliderWorldPosition(collider);
    const broadPhase = pointSegmentDistanceXZ(center, origin, target);
    if (broadPhase.t <= 0.04 || broadPhase.t >= 0.97) continue;
    if (broadPhase.distance > collider.radius * 2.4 + 0.8) continue;

    for (const meshInstance of collider.meshInstances) {
      if (!meshInstance.visible || !meshInstance.aabb.intersectsRay(ray, hitPoint)) continue;
      if (origin.distance(hitPoint) < targetDistance - 0.38) return true;
    }
  }
  return false;
}

function positionCamera() {
  camera.setPosition(
    cameraFocus.x + cameraOffset.x,
    cameraFocus.y + cameraOffset.y,
    cameraFocus.z + cameraOffset.z
  );
  camera.lookAt(cameraFocus);
}

function updateInterface(dt) {
  const activeRoute = ROUTES.find((route) => route.id === routeProgress.activeRouteId);
  ui.zoneLabel.textContent = activeRoute?.kind === 'main'
    ? 'CHAPTER I · THE HOLLOW VEIN'
    : activeRoute
      ? `OPTIONAL ROUTE · ${activeRoute.difficulty.toUpperCase()}`
      : 'THE ASHEN ROAD · REFUGE';
  ui.zoneTitle.textContent = activeRoute?.name ?? 'Warden Refuge';
  ui.objective.textContent = questObjectiveText();
  const healthPercent = (hero.hp / hero.maxHp) * 100;
  ui.healthFill.style.width = `${healthPercent}%`;
  ui.healthValue.textContent = `${hero.hp} / ${hero.maxHp}`;
  ui.focusFill.style.width = `${(hero.focus / hero.maxFocus) * 100}%`;
  ui.focusValue.textContent = `${Math.floor(hero.focus)} focus`;
  ui.lootValue.textContent = `${hero.loot} shard${hero.loot === 1 ? '' : 's'}`;
  ui.resourceOre.textContent = String(hero.resources.ore);
  ui.resourceWood.textContent = String(hero.resources.wood);
  ui.resourceHerb.textContent = String(hero.resources.herb);
  ui.resourceEssence.textContent = String(hero.resources.essence);

  const bossAvailable = expeditionProgress.bossState === 'available';
  ui.expeditionBanner.hidden = !bossAvailable && !expeditionProgress.extractionReady;
  if (expeditionProgress.extractionReady) {
    ui.expeditionState.textContent = 'EXTRACTION READY';
    ui.expeditionCopy.textContent = 'The Overseer has fallen · return to Warden Refuge to secure the expedition';
  } else if (bossAvailable) {
    ui.expeditionState.textContent = 'OVERSEER REVEALED';
    ui.expeditionCopy.textContent = 'Corrupted Mine Overseer · northern hollow';
  }

  if (!ui.refugePanel.hidden && expeditionProgress.recovery) {
    ui.recoveryTime.textContent = `${formatRecoveryRemaining(expeditionProgress.recovery.expiresAt)} remaining`;
  }

  if (hero.targetEnemy?.alive) {
    ui.targetCard.hidden = false;
    ui.targetName.textContent = hero.targetEnemy.name;
    ui.targetKind.textContent = hero.targetEnemy.kind;
    ui.enemyHealthFill.style.width = `${(hero.targetEnemy.hp / hero.targetEnemy.maxHp) * 100}%`;
  } else {
    ui.targetCard.hidden = true;
  }

  if (hero.gatherTarget?.available) {
    const node = hero.gatherTarget;
    const inRange = distanceXZ(hero.entity.getPosition(), node.root.getPosition()) <= node.interactRange;
    ui.gatherCard.hidden = false;
    ui.gatherState.textContent = inRange ? 'GATHERING' : 'APPROACHING';
    ui.gatherName.textContent = RESOURCE_TYPES[node.type].name;
    ui.gatherFill.style.width = `${inRange ? Math.min(100, (hero.gatherElapsed / hero.gatherDuration) * 100) : 0}%`;
  } else {
    ui.gatherCard.hidden = true;
  }

  for (const id of hero.abilities.order) {
    const ability = ABILITIES[id];
    const cooldown = hero.abilities.cooldowns[id];
    const row = ui.abilityList.querySelector(`[data-ability="${id}"]`);
    const cooldownLabel = row?.querySelector(`[data-cooldown="${id}"]`);
    if (!row || !cooldownLabel) continue;
    const effectiveCooldown = effectiveAbilityCooldown(ability);
    const cooldownPercent = effectiveCooldown ? (cooldown / effectiveCooldown) * 100 : 0;
    row.style.setProperty('--cooldown', `${cooldownPercent}%`);
    row.classList.toggle('ready', cooldown <= 0);
    row.classList.toggle('active', hero.activeAbility === id);
    cooldownLabel.textContent = cooldown > 0 ? cooldown.toFixed(cooldown < 1 ? 1 : 0) : 'READY';
  }

  if (toastTimer > 0) {
    toastTimer -= dt;
    if (toastTimer <= 0) ui.toast.classList.remove('visible');
  }
}

function questObjectiveText() {
  const { stage, gathered } = townProgress.quest;
  if (stage === 0) {
    return `Provision the refuge · ore ${Math.min(3, gathered.ore)}/3 · wood ${Math.min(3, gathered.wood)}/3`;
  }
  if (stage === 1) return 'Return gathered materials to Warden Refuge';
  if (stage === 2) return 'Restore the Cold Forge · 6 ore · 4 wood';
  if (stage === 3) return 'Craft and equip your first rare item';
  if (routeProgress.location !== 'route') return 'Choose the next expedition from the Ashen Road map';
  if (expeditionProgress.state === 'defeated') return 'Expedition lost · return to Warden Refuge';
  if (expeditionProgress.rewardPending) return 'Choose one rare Overseer reward';
  if (expeditionProgress.extractionReady) return 'Extraction ready · return to Warden Refuge';
  if (expeditionProgress.bossState === 'available') {
    return 'Defeat the Corrupted Mine Overseer · northern hollow';
  }
  return 'Depart Warden Refuge to begin the next Overseer hunt';
}

function showToast(message) {
  ui.toast.textContent = message;
  ui.toast.classList.remove('visible');
  requestAnimationFrame(() => ui.toast.classList.add('visible'));
  toastTimer = 2.2;
}

function seededRandom() {
  randomState = (randomState * 1664525 + 1013904223) >>> 0;
  return randomState / 4294967296;
}
