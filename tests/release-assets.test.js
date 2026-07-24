import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

test('release includes an installable landscape PWA manifest and offline worker', async () => {
  const manifest = JSON.parse(await readFile(
    new URL('../public/manifest.webmanifest', import.meta.url),
    'utf8'
  ));
  const worker = await readFile(
    new URL('../public/service-worker.js', import.meta.url),
    'utf8'
  );

  assert.equal(manifest.start_url, '/game.html');
  assert.equal(manifest.display, 'standalone');
  assert.equal(manifest.orientation, 'landscape');
  assert.ok(manifest.icons.length > 0);
  assert.match(worker, /caches\.open/);
  assert.match(worker, /fetch/);
});
