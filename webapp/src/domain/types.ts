export type ActivityKind = "Idle" | "Fighting" | "Mining" | "Chopping" | "Gathering";

export interface EfficiencySnapshot {
  kind: ActivityKind;
  targetId: string | null;
  actionsPerHour: number;
  xpPerAction: number;
  coinsPerAction: number;
  snapshotAt: number;
  mapDensity: number;
  travelOverheadMs: number;
  survivalFactor: number;
}

export interface ItemStack {
  itemId: string;
  qty: number;
}

export interface DropEntry {
  itemId: string;
  chance: number;
  min: number;
  max: number;
}

export interface DropItem {
  itemId: string;
  min: number;
  max: number;
}

export interface WeightedSlot {
  weight: number;
  nothing?: boolean;
  itemId?: string;
  min?: number;
  max?: number;
}

export interface WeightedTable {
  rolls: number;
  slots: WeightedSlot[];
}

export interface DropTable {
  always: DropItem[];
  main: WeightedTable | null;
  tertiary: DropEntry[];
}

export interface MonsterDef {
  id: string;
  name: string;
  xp: number;
  coinsMin: number;
  coinsMax: number;
  drops: DropEntry[];
  dropTable?: DropTable;
}

export interface ResourceNodeDef {
  id: string;
  name: string;
  skill: Exclude<ActivityKind, "Idle" | "Fighting">;
  xp: number;
  drops: DropEntry[];
}

export interface SkillProgress {
  level: number;
  xp: number;
}

export interface Character {
  id: string;
  name: string;
  classId: "Beginner" | "Warrior" | "Archer" | "Mage";
  level: number;
  xp: number;
  mapId: string;
  inventory: ItemStack[];
  maxInventorySlots: number;
  activity: {
    kind: ActivityKind;
    targetId: string | null;
    startedAt: number;
  };
  efficiency: EfficiencySnapshot | null;
  freeStatPoints?: number;
  skills: {
    Combat: SkillProgress;
    Mining: SkillProgress;
    Chopping: SkillProgress;
    Gathering: SkillProgress;
  };
}

export interface Bank {
  coins: number;
  slots: ItemStack[];
  maxSlots: number;
}

export interface Account {
  id: string;
  name: string;
  lastSeenAt: number;
  autoLoot: boolean;
  autoCombatDisabled: boolean;
  characters: Character[];
  bank: Bank;
}

export interface OfflineCharacterReport {
  characterId: string;
  characterName: string;
  kind: ActivityKind;
  targetId: string | null;
  actions: number;
  xpGained: number;
  levelsGained: number;
  coinsGained: number;
  loot: ItemStack[];
}

export interface OfflineReport {
  elapsedMs: number;
  cappedMs: number;
  characters: OfflineCharacterReport[];
}

export interface ContentBundle {
  monsters: Record<string, MonsterDef>;
  nodes: Record<string, ResourceNodeDef>;
}

export interface OfflineBalanceConfig {
  rate: number;
  capMs: number;
  minimumDurationMs: number;
}

export type CombatCommandKind = "SelectTarget" | "TriggerSkill" | "MoveIntent";

export interface CombatCommand {
  kind: CombatCommandKind;
  targetId?: string;
  skillId?: string;
}

export interface CombatSkillDef {
  id: string;
  name: string;
  cooldownMs: number;
  damageMultiplier: number;
  manaCost: number;
  range: number;
  autoEnabled: boolean;
  targeting?:
    | "HostileActor"
    | "Self"
    | "CircleAroundSource"
    | "CircleAroundTarget"
    | "GroundPoint"
    | "Direction"
    | "TilePatternAroundSource"
    | "TilePatternAroundTarget";
  tilePattern?: TilePatternDef;
  timing?: "Immediate" | "ScheduledImpact";
  impactDelayTicks?: number;
  minimumAutoTargets?: number;
  modifier?: {
    property: "Defense" | "Damage" | "AttackSpeed" | "CritChance";
    operation: "FlatAdd" | "AdditivePercent" | "MultiplicativePercent" | "Override";
    magnitude: number;
    durationTicks: number;
  };
  inflicts?: Array<{
    kind: "Burn" | "Poison" | "Chill" | "Stun";
    magnitude: number;
    durationTicks: number;
    intervalTicks: number;
  }>;
}

export interface CombatTileCoordinate {
  x: number;
  y: number;
}

export interface TilePatternDef {
  patternKind: "SingleTile" | "Cross" | "SquareRadius" | "CustomOffsets";
  size?: number;
  customOffsets?: CombatTileCoordinate[];
  floorPolicy: "SameFloor";
  /** Zero means unlimited. */
  maxTargets?: number;
}

export interface CombatSpatialSnapshot {
  actorId: string;
  definitionId: string;
  tile: CombatTileCoordinate;
  floor: number;
  faction: "Player" | "Hostile" | "Neutral";
  alive?: boolean;
  targetable?: boolean;
}

export interface TransientCombatModifier {
  instanceId: string;
  definitionId: string;
  sourceActorId: string;
  targetActorId: string;
  property: "Defense" | "Damage" | "AttackSpeed" | "CritChance";
  operation: "FlatAdd" | "AdditivePercent" | "MultiplicativePercent" | "Override";
  magnitude: number;
  startTick: number;
  endTick: number;
  applicationSequenceId: number;
}

export interface ActiveCombatStatus {
  instanceId: string;
  definitionId: string;
  sourceActorId: string;
  targetActorId: string;
  kind: "Burn" | "Poison" | "Chill" | "Stun";
  magnitude: number;
  startTick: number;
  endTick: number;
  nextTick: number;
  intervalTicks: number;
  applicationSequenceId: number;
}

export interface ScheduledCombatEffect {
  sequenceId: number;
  executeTick: number;
  kind?: "ResolveSkillImpact" | "ExpireModifier" | "TickStatus";
  referenceId?: string;
  sourceActorId: string;
  targetActorId: string | null;
  skillId: string;
  commandSequenceId?: number;
  actionSequenceId?: number;
}

export interface CombatEnemy {
  id: string;
  name: string;
  hp: number;
  attack: number;
}

export interface CombatActorStats {
  hpMax: number;
  manaMax: number;
  baseAttack: number;
  critChance: number;
  critMultiplier: number;
}

export interface ActiveCombatState {
  timestamp: number;
  simulationTick: number;
  playerHp: number;
  playerMana: number;
  enemyHp: number;
  targetId: string | null;
  autoCombatStopped: boolean;
  autoCombatStoppedAt: number;
  playerNextAttackAt: number;
  skillNextReadyAt: Record<string, number>;
  scheduledEffects: ScheduledCombatEffect[];
  lastCommandSequenceId: number;
  lastScheduledEffectSequenceId: number;
  lastActionSequenceId: number;
  activeModifiers: TransientCombatModifier[];
  activeStatuses: ActiveCombatStatus[];
  lastAutoSkillId: string | null;
  lastAutoSkillFallbackReason: string | null;
}

export type CombatEventKind =
  | "TargetSelected"
  | "MovementRequested"
  | "ActionRejected"
  | "SkillCastStarted"
  | "CombatEffectScheduled"
  | "CooldownStarted"
  | "AttackResolved"
  | "SkillResolved"
  | "CombatEffectCancelled"
  | "TransientModifierApplied"
  | "TransientModifierExpired"
  | "StatusApplied"
  | "StatusTicked"
  | "StatusExpired"
  | "EnemyDefeated"
  | "PlayerDefeated"
  | "AutoCombatResumed";

export interface CombatEvent {
  kind: CombatEventKind;
  reason?: string;
  amount?: number;
  durationTicks?: number;
  targetId?: string;
  skillId?: string;
  critical?: boolean;
  commandSequenceId?: number;
  actionSequenceId?: number;
}

export interface ActiveCombatInput {
  timestamp: number;
  commandQueue: CombatCommand[];
  canFight: boolean;
  targetAvailable: boolean;
  activeEnemyId: string;
  state: ActiveCombatState;
  stats: CombatActorStats;
  enemy: CombatEnemy;
  skills: CombatSkillDef[];
  autoResumeGraceMs: number;
}

export interface ActiveCombatResult {
  state: ActiveCombatState;
  events: CombatEvent[];
  enemyKilled: boolean;
  playerDefeated: boolean;
}
