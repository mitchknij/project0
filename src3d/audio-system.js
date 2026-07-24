export const AUDIO_CUES = Object.freeze({
  command: Object.freeze({ frequency: 310, duration: 0.05, gain: 0.025, type: 'sine' }),
  cast: Object.freeze({ frequency: 760, duration: 0.09, gain: 0.035, type: 'triangle' }),
  impact: Object.freeze({ frequency: 145, duration: 0.07, gain: 0.045, type: 'square' }),
  gather: Object.freeze({ frequency: 430, duration: 0.12, gain: 0.03, type: 'triangle' }),
  loot: Object.freeze({ frequency: 980, duration: 0.16, gain: 0.035, type: 'sine' }),
  boss: Object.freeze({ frequency: 92, duration: 0.34, gain: 0.055, type: 'sawtooth' }),
  extraction: Object.freeze({ frequency: 620, duration: 0.42, gain: 0.045, type: 'sine' }),
  defeat: Object.freeze({ frequency: 78, duration: 0.5, gain: 0.05, type: 'triangle' })
});

export function createAudioDirector({
  AudioContextClass = globalThis.AudioContext ?? globalThis.webkitAudioContext,
  enabled = true
} = {}) {
  let context = null;
  let active = enabled;

  function unlock() {
    if (!active || !AudioContextClass) return false;
    context ??= new AudioContextClass();
    if (context.state === 'suspended') void context.resume();
    return true;
  }

  function cue(name) {
    const definition = AUDIO_CUES[name];
    if (!active || !definition || !unlock()) return false;
    const oscillator = context.createOscillator();
    const gain = context.createGain();
    const now = context.currentTime;
    oscillator.type = definition.type;
    oscillator.frequency.setValueAtTime(definition.frequency, now);
    gain.gain.setValueAtTime(definition.gain, now);
    gain.gain.exponentialRampToValueAtTime(0.0001, now + definition.duration);
    oscillator.connect(gain);
    gain.connect(context.destination);
    oscillator.start(now);
    oscillator.stop(now + definition.duration);
    return true;
  }

  return {
    cue,
    unlock,
    setEnabled(value) {
      active = value === true;
      if (active && context?.state === 'suspended') void context.resume();
      return active;
    },
    get enabled() {
      return active;
    }
  };
}
