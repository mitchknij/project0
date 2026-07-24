import test from 'node:test';
import assert from 'node:assert/strict';
import { AUDIO_CUES, createAudioDirector } from '../src3d/audio-system.js';

test('audio layer exposes distinct feedback for every critical expedition beat', () => {
  assert.deepEqual(Object.keys(AUDIO_CUES), [
    'command', 'cast', 'impact', 'gather', 'loot', 'boss', 'extraction', 'defeat'
  ]);
  assert.ok(new Set(Object.values(AUDIO_CUES).map((cue) => cue.frequency)).size >= 7);
});

test('audio director remains safe when Web Audio is unavailable or disabled', () => {
  const unavailable = createAudioDirector({ AudioContextClass: null });
  assert.equal(unavailable.unlock(), false);
  assert.equal(unavailable.cue('loot'), false);
  assert.equal(unavailable.cue('unknown'), false);

  const disabled = createAudioDirector({ AudioContextClass: null, enabled: false });
  assert.equal(disabled.enabled, false);
  assert.equal(disabled.setEnabled(true), true);
});
