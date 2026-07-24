import test from 'node:test';
import assert from 'node:assert/strict';
import {
  createBossRewardChoices,
  createItemFromTemplate,
  deriveGearStats,
  equipInventoryItem,
  rollLootItem
} from '../src3d/item-system.js';

test('boss reward choices contain three distinct rare items with stable serials', () => {
  const rewards = createBossRewardChoices(() => 0.25, 10);
  assert.equal(rewards.length, 3);
  assert.equal(new Set(rewards.map((item) => item.templateId)).size, 3);
  assert.ok(rewards.every((item) => item.rarity === 'rare'));
  assert.deepEqual(rewards.map((item) => item.id.match(/_(\d+)$/)?.[1]), ['10', '11', '12']);
});

test('named route rewards create detached item instances', () => {
  const relic = createItemFromTemplate('ossuary_reliquary', 21);
  assert.equal(relic.id, 'ossuary_reliquary_21');
  assert.equal(relic.rarity, 'rare');
  relic.stats.maxHealth = 999;
  assert.equal(createItemFromTemplate('ossuary_reliquary', 22).stats.maxHealth, 18);
  assert.equal(createItemFromTemplate('missing', 1), null);
});

test('normal loot rolls from the common pool when the rarity roll misses', () => {
  const values = [0.8, 0.1, 0.45];
  const item = rollLootItem(() => values.shift(), 4);
  assert.equal(item.rarity, 'common');
  assert.match(item.id, /_4$/);
  assert.equal(item.modifiers.length, 1);
  assert.match(item.effect, /·/);
});

test('champion loot can force a rare build-changing item', () => {
  const item = rollLootItem(() => 0.99, 9, true);
  assert.equal(item.rarity, 'rare');
});

test('equipping an inventory item fills its matching slot', () => {
  const item = rollLootItem(() => 0.1, 1, true);
  const equipment = equipInventoryItem([item], { weapon: null, armor: null, gadget: null }, item.id);
  assert.equal(equipment[item.slot], item.id);
});

test('derived stats combine modifiers from equipped items', () => {
  const inventory = [
    { id: 'weapon', slot: 'weapon', stats: { damage: 4, shardDamage: 3 } },
    { id: 'armor', slot: 'armor', stats: { maxHealth: 14 } }
  ];
  const stats = deriveGearStats(inventory, { weapon: 'weapon', armor: 'armor', gadget: null });
  assert.equal(stats.damage, 4);
  assert.equal(stats.shardDamage, 3);
  assert.equal(stats.maxHealth, 14);
});
