import type { Character, SkillProgress } from "../types";

const XP_COEFFICIENT = 50;
const XP_EXPONENT = 1.8;
const XP_BASE = 50;

export function xpToNext(level: number): number {
  if (level < 1) throw new Error("level must be >= 1");
  return Math.floor(XP_COEFFICIENT * Math.pow(level, XP_EXPONENT) + XP_BASE);
}

export function totalXpForLevel(level: number): number {
  if (level < 1) throw new Error("level must be >= 1");
  let sum = 0;
  for (let current = 1; current < level; current += 1) {
    sum += xpToNext(current);
  }
  return sum;
}

export interface CharacterXpResult {
  character: Character;
  xpAwarded: number;
  previousLevel: number;
  newLevel: number;
  levelsGained: number;
  freeStatPointsGained: number;
}

export function applyCharacterXp(character: Character, amount: number): CharacterXpResult {
  if (amount < 0) throw new Error("xp amount must be >= 0");

  let level = character.level;
  let xp = character.xp + amount;
  while (xp >= xpToNext(level)) {
    xp -= xpToNext(level);
    level += 1;
  }

  const levelsGained = level - character.level;
  const freeStatPoints = (character.freeStatPoints ?? 0) + levelsGained;
  const nextCharacter: Character = {
    ...character,
    level,
    xp,
    freeStatPoints
  };

  return {
    character: nextCharacter,
    xpAwarded: amount,
    previousLevel: character.level,
    newLevel: level,
    levelsGained,
    freeStatPointsGained: levelsGained
  };
}

export interface SkillXpResult {
  skills: Character["skills"];
  skillId: keyof Character["skills"];
  xpAwarded: number;
  previousLevel: number;
  newLevel: number;
  levelsGained: number;
}

export function applySkillXp(
  skills: Character["skills"],
  skillId: keyof Character["skills"],
  amount: number
): SkillXpResult {
  if (amount < 0) throw new Error("xp amount must be >= 0");

  const current: SkillProgress = skills[skillId];
  let level = current.level;
  let xp = current.xp + amount;
  while (xp >= xpToNext(level)) {
    xp -= xpToNext(level);
    level += 1;
  }

  const nextSkills: Character["skills"] = {
    ...skills,
    [skillId]: {
      level,
      xp
    }
  };

  return {
    skills: nextSkills,
    skillId,
    xpAwarded: amount,
    previousLevel: current.level,
    newLevel: level,
    levelsGained: level - current.level
  };
}
