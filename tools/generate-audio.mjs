/** Original, deterministic PCM sound design. No samples, network, API or paid service. */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createHash } from 'node:crypto';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const out = path.join(root, 'art', 'audio');
const rate = 44100;
const tau = Math.PI * 2;
const check = process.argv.includes('--check');
const definitions = [];
function random(seed) {
  let state = seed >>> 0;
  return () => { state = (1664525 * state + 1013904223) >>> 0; return state / 4294967296 * 2 - 1; };
}
const fade = (t, duration, attack = .006, release = .045) => Math.min(1, t / attack) * Math.min(1, (duration - t) / release);
const tone = (frequency, t, decay = 7) => Math.sin(tau * frequency * t) * Math.exp(-decay * t);
function add(name, duration, description, sample, gain = .5, loop = false) {
  const count = Math.round(duration * rate);
  const values = new Float64Array(count);
  let peak = 0, square = 0;
  for (let i = 0; i < count; i++) {
    const t = i / rate;
    values[i] = sample(t, i) * gain * (loop ? 1 : fade(t, duration));
    peak = Math.max(peak, Math.abs(values[i]));
    square += values[i] ** 2;
  }
  if (peak >= .96) throw new Error(`${name}: unexpectedly high peak ${peak}`);
  const wave = Buffer.alloc(44 + count * 2);
  wave.write('RIFF', 0); wave.writeUInt32LE(wave.length - 8, 4); wave.write('WAVEfmt ', 8);
  wave.writeUInt32LE(16, 16); wave.writeUInt16LE(1, 20); wave.writeUInt16LE(1, 22);
  wave.writeUInt32LE(rate, 24); wave.writeUInt32LE(rate * 2, 28);
  wave.writeUInt16LE(2, 32); wave.writeUInt16LE(16, 34); wave.write('data', 36);
  wave.writeUInt32LE(count * 2, 40);
  for (let i = 0; i < count; i++) wave.writeInt16LE(Math.round(values[i] * 32767), 44 + i * 2);
  definitions.push({ name, duration, description, loop, wave, peak: +peak.toFixed(6), rms: +Math.sqrt(square / count).toFixed(6) });
}

add('ui-hover', .08, 'Quiet, rounded wooden touch for optional hover feedback.', t => tone(720, t, 60) + .2 * tone(1440, t, 80), .13);
add('ui-select', .18, 'Soft two-part pluck for selection and opening a card.', t => tone(587.33, t, 25) + .25 * tone(1174.66, t, 40), .3);
for (const [name, seed, pitch] of [['footstep-a', 41, 95], ['footstep-b', 83, 103]]) {
  const rng = random(seed); let low = 0;
  add(name, .19, 'Muted gravel/oak footstep; alternate A and B, tied to actual movement.', t => {
    low += .065 * (rng() - low);
    return low * 2.8 * Math.exp(-t * 23) + .24 * tone(pitch, t, 32);
  }, .38);
}
{
  const rng = random(229); let low = 0;
  add('build-place', .44, 'Warm construction impact: low body, short wood knock and a restrained grain.', t => {
    low += .1 * (rng() - low);
    return .65 * Math.sin(tau * (155 * t - 90 * t * t)) * Math.exp(-t * 16)
      + .19 * tone(410, t, 33) + .35 * low * Math.exp(-t * 32);
  }, .62);
}
add('quest-complete', .95, 'Three gentle ascending notes for a completed learning task.', t =>
  [523.25, 659.25, 783.99].reduce((v, f, i) => { const s = t - i * .12; return v + (s >= 0 ? tone(f, s, 6) * Math.min(1, s / .008) : 0); }, 0), .23);
add('milestone', 1.45, 'Short warm major-sixth resolution; reserve for meaningful milestones.', t =>
  [261.63, 329.63, 392, 440].reduce((v, f, i) => { const s = t - i * .09; return v + (s >= 0 ? (tone(f, s, 3.8) + .12 * tone(f * 2, s, 7)) * Math.min(1, s / .012) : 0); }, 0), .2);
add('ui-unavailable', .28, 'Quiet descending cue; no buzzer or alarm.', t =>
  tone(392, t, 20) * .6 + (t >= .085 ? tone(349.23, t - .085, 24) * .45 * Math.min(1, (t - .085) / .007) : 0), .28);

// Every component makes an integer number of cycles in 16 s, including modulation.
// The first and wrapped sample follow the same continuous function: no splice click.
add('courtyard-ambience', 16, 'Subtle seamless tonal courtyard bed. Start only on opt-in; loop quietly.', t => {
  const breath = .7 + .3 * Math.cos(tau * t / 16);
  const body = .45 * Math.sin(tau * 110 * t) + .21 * Math.sin(tau * 146.875 * t)
    + .13 * Math.sin(tau * 220 * t) + .06 * Math.sin(tau * 293.75 * t);
  return breath * body;
}, .085, true);

const manifest = {
  schema: 'kompetenzhaus.audio', version: 1,
  provenance: 'Original deterministic synthesis by tools/generate-audio.mjs. No recordings, external assets, network calls or billable services.',
  format: { codec: 'PCM', sampleRate: rate, channels: 1, bitsPerSample: 16 },
  integration: {
    master: 'Respect the sound toggle; do not autoplay before the player enables sound.',
    ambience: 'Optional. Suggested playback volume 0.35; use 0.5 s fades when starting/stopping.',
    footsteps: 'Alternate A/B only while the avatar moves; do not trigger every render frame.',
    hover: 'Optional, disabled by default to avoid auditory clutter.',
    signals: 'All feedback must also have a visible equivalent.'
  },
  sounds: definitions.map(({ wave, ...item }) => ({ ...item, file: `${item.name}.wav`, bytes: wave.length, sha256: createHash('sha256').update(wave).digest('hex') }))
};
const files = [...definitions.map(d => [`${d.name}.wav`, d.wave]), ['manifest.json', Buffer.from(JSON.stringify(manifest, null, 2) + '\n')]];
if (!check) fs.mkdirSync(out, { recursive: true });
for (const [name, contents] of files) {
  const destination = path.join(out, name);
  if (check) {
    if (!fs.existsSync(destination) || !fs.readFileSync(destination).equals(contents)) throw new Error(`Generated audio differs: ${name}`);
  } else fs.writeFileSync(destination, contents);
}
console.log(`${check ? 'Verified' : 'Generated'} ${definitions.length} WAV files; ${files.reduce((n, [, b]) => n + b.length, 0)} bytes; no external services.`);
