import { readFile, writeFile } from 'node:fs/promises';

const [, , modelPath, animationPath, outputPath] = process.argv;
if (!modelPath || !animationPath || !outputPath) {
  throw new Error('Usage: node merge-compatible-animation-glb.mjs <model.glb> <animations.glb> <output.glb>');
}

function parseGlb(bytes) {
  if (bytes.subarray(0, 4).toString('ascii') !== 'glTF') throw new Error('Input is not a GLB');
  const jsonLength = bytes.readUInt32LE(12);
  const jsonType = bytes.readUInt32LE(16);
  if (jsonType !== 0x4e4f534a) throw new Error('First GLB chunk is not JSON');
  const json = JSON.parse(bytes.subarray(20, 20 + jsonLength).toString('utf8').trim());
  const binaryHeader = 20 + jsonLength;
  const binaryLength = bytes.readUInt32LE(binaryHeader);
  const binaryType = bytes.readUInt32LE(binaryHeader + 4);
  if (binaryType !== 0x004e4942) throw new Error('Second GLB chunk is not BIN');
  return {
    json,
    binary: bytes.subarray(binaryHeader + 8, binaryHeader + 8 + binaryLength)
  };
}

function padded(buffer, byte = 0) {
  const padding = (4 - (buffer.length % 4)) % 4;
  return padding ? Buffer.concat([buffer, Buffer.alloc(padding, byte)]) : buffer;
}

function writeGlb(json, binary) {
  const jsonChunk = padded(Buffer.from(JSON.stringify(json)), 0x20);
  const binaryChunk = padded(binary);
  const totalLength = 12 + 8 + jsonChunk.length + 8 + binaryChunk.length;
  const header = Buffer.alloc(12);
  header.write('glTF', 0, 'ascii');
  header.writeUInt32LE(2, 4);
  header.writeUInt32LE(totalLength, 8);
  const jsonHeader = Buffer.alloc(8);
  jsonHeader.writeUInt32LE(jsonChunk.length, 0);
  jsonHeader.writeUInt32LE(0x4e4f534a, 4);
  const binaryHeader = Buffer.alloc(8);
  binaryHeader.writeUInt32LE(binaryChunk.length, 0);
  binaryHeader.writeUInt32LE(0x004e4942, 4);
  return Buffer.concat([header, jsonHeader, jsonChunk, binaryHeader, binaryChunk]);
}

const model = parseGlb(await readFile(modelPath));
const library = parseGlb(await readFile(animationPath));
const modelNodeNames = model.json.nodes.map(({ name }) => name);
const libraryNodeNames = library.json.nodes.map(({ name }) => name);

for (const animation of library.json.animations ?? []) {
  for (const channel of animation.channels) {
    const targetName = libraryNodeNames[channel.target.node];
    const targetNode = modelNodeNames.indexOf(targetName);
    if (targetNode < 0) throw new Error(`Animation target is missing from model: ${targetName}`);
    channel.target.node = targetNode;
  }
}

const accessorOffset = model.json.accessors?.length ?? 0;
const bufferViewOffset = model.json.bufferViews?.length ?? 0;
const binaryOffset = padded(model.binary).length;

const appendedViews = (library.json.bufferViews ?? []).map((view) => ({
  ...view,
  buffer: 0,
  byteOffset: (view.byteOffset ?? 0) + binaryOffset
}));
const appendedAccessors = (library.json.accessors ?? []).map((accessor) => ({
  ...accessor,
  bufferView: accessor.bufferView === undefined ? undefined : accessor.bufferView + bufferViewOffset
}));
const animations = (library.json.animations ?? []).map((animation) => ({
  ...animation,
  samplers: animation.samplers.map((sampler) => ({
    ...sampler,
    input: sampler.input + accessorOffset,
    output: sampler.output + accessorOffset
  }))
}));

model.json.bufferViews = [...(model.json.bufferViews ?? []), ...appendedViews];
model.json.accessors = [...(model.json.accessors ?? []), ...appendedAccessors];
model.json.animations = animations;
const combinedBinary = Buffer.concat([padded(model.binary), library.binary]);
model.json.buffers[0].byteLength = combinedBinary.length;

await writeFile(outputPath, writeGlb(model.json, combinedBinary));
