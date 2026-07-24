import * as pc from 'playcanvas';

const NON_LOOPING_CLIPS = new Set([
  'Bow_Draw',
  'Bow_Shoot',
  'Death',
  'Death_A',
  'Death_B',
  'Death_C_Skeletons',
  'HitReact',
  'Hit_A',
  'Hit_B',
  '1H_Melee_Attack_Chop',
  '1H_Melee_Attack_Slice_Diagonal',
  '1H_Melee_Attack_Slice_Horizontal',
  '1H_Melee_Attack_Stab',
  'Punch',
  'Punch_Cross',
  'RecieveHit',
  'RecieveHit_2',
  'Interact',
  'Spell_Simple_Enter',
  'Spell_Simple_Exit',
  'Spell_Simple_Shoot',
  'Sword_Attack',
  'Weapon'
]);

export function loadContainer(app, name, url) {
  return new Promise((resolve, reject) => {
    app.assets.loadFromUrl(url, 'container', (error, asset) => {
      if (error) reject(new Error(`${name}: ${error}`));
      else resolve(asset);
    });
  });
}

export function loadTexture(app, name, url) {
  return new Promise((resolve, reject) => {
    app.assets.loadFromUrl(url, 'texture', (error, asset) => {
      if (error) reject(new Error(`${name}: ${error}`));
      else {
        asset.resource.addressU = pc.ADDRESS_REPEAT;
        asset.resource.addressV = pc.ADDRESS_REPEAT;
        asset.resource.anisotropy = 8;
        resolve(asset);
      }
    });
  });
}

export function prepareContainerMaterials(asset, options = {}) {
  const materials = asset.resource.materials ?? [];
  for (let index = 0; index < materials.length; index += 1) {
    const materialAsset = materials[index];
    const material = materialAsset.resource;
    if (!material) continue;
    material.useLighting = options.useLightingValues?.[index] ?? options.useLighting ?? true;
    if (options.alphaTests || options.alphaTest !== undefined) {
      material.alphaTest = options.alphaTests?.[index] ?? options.alphaTest;
    }
    material.useFog = true;
    material.ambientTint = true;
    material.gloss = options.glosses?.[index] ?? options.gloss ?? 0.34;
    material.metalness = options.metalness ?? 0.02;
    if (options.matte) {
      material.gloss = 0;
      material.metalness = 0;
      material.specularityFactor = 0;
      if (material.specular) {
        material.specular.set(0, 0, 0);
        material.specularTint = true;
      }
      material.specularMap = null;
      material.glossMap = null;
      material.metalnessMap = null;
      material.clearCoat = 0;
      material.clearCoatGloss = 0;
      material.clearCoatMap = null;
      material.clearCoatGlossMap = null;
      material.sheenGloss = 0;
      material.iridescence = 0;
    }
    if (options.specularityFactors || options.specularityFactor !== undefined) {
      material.specularityFactor = options.specularityFactors?.[index] ?? options.specularityFactor;
    }
    const specular = options.speculars?.[index] ?? options.specular;
    if (specular && material.specular) {
      material.specular.copy(specular);
      material.specularTint = true;
    }
    const textureCount = asset.resource.textures?.length ?? 0;
    const forcedTextureIndex = options.diffuseTextureIndices?.[index];
    const textureIndex = forcedTextureIndex ?? Math.min(index, textureCount - 1);
    const textureAsset = textureCount ? asset.resource.textures[textureIndex] : null;
    if ((forcedTextureIndex !== undefined || !material.diffuseMap) && textureAsset?.resource) {
      material.diffuseMap = textureAsset.resource;
      material.diffuseMapChannel = 'rgb';
    }
    const tint = options.tints?.[index] ?? options.tint;
    if (tint && material.diffuse) {
      material.diffuse.copy(tint);
      material.diffuseTint = true;
    }
    material.update?.();
  }
}

export function instantiateAnimated(asset, options = {}) {
  const entity = asset.resource.instantiateRenderEntity({
    castShadows: true,
    receiveShadows: true
  });
  entity.name = options.name ?? asset.name;
  const scale = options.scale ?? 1;
  entity.setLocalScale(scale, scale, scale);
  entity.setLocalEulerAngles(0, options.rotationY ?? 0, 0);

  entity.addComponent('anim', {
    activate: true,
    speed: options.speed ?? 1
  });

  const clips = new Set();
  const animationSource = options.animationAsset ?? asset;
  for (const animationAsset of animationSource.resource.animations ?? []) {
    const track = animationAsset.resource;
    const clipName = track?.name;
    if (!track || !clipName) continue;
    if (options.stripAnimationRoot) {
      for (const curve of track._curves ?? []) {
        for (const path of curve._paths ?? []) {
          if (path.entityPath?.[0] === options.stripAnimationRoot) {
            path.entityPath = path.entityPath.slice(1);
          }
        }
      }
    }
    clips.add(clipName);
    entity.anim.assignAnimation(
      clipName,
      track,
      undefined,
      options.clipSpeeds?.[clipName] ?? 1,
      !NON_LOOPING_CLIPS.has(clipName)
    );
  }

  const initial = options.initial ?? (clips.has('Idle_Weapon') ? 'Idle_Weapon' : 'Idle');
  if (clips.has(initial)) entity.anim.baseLayer.transition(initial, 0);
  entity._productionClips = clips;
  entity._activeProductionClip = initial;
  return entity;
}

export function instantiateStatic(asset, options = {}) {
  const entity = asset.resource.instantiateRenderEntity({
    castShadows: true,
    receiveShadows: true
  });
  entity.name = options.name ?? asset.name;
  const scale = options.scale ?? 1;
  entity.setLocalScale(scale, scale, scale);
  entity.setLocalPosition(...(options.position ?? [0, 0, 0]));
  entity.setLocalEulerAngles(...(options.rotation ?? [0, 0, 0]));
  return entity;
}

export function transitionModel(model, clip, blendTime = 0.14, restart = false) {
  if (!model?.anim || !model._productionClips?.has(clip)) return false;
  if (!restart && model._activeProductionClip === clip) return true;
  model.anim.baseLayer.transition(clip, blendTime, restart ? 0 : undefined);
  model._activeProductionClip = clip;
  return true;
}

export function hideRenderHierarchy(root) {
  for (const render of root.findComponents('render')) render.enabled = false;
}

export function applyRenderQuality(root) {
  for (const render of root.findComponents('render')) {
    render.castShadows = true;
    render.receiveShadows = true;
    for (const meshInstance of render.meshInstances) {
      meshInstance.castShadow = true;
      meshInstance.receiveShadow = true;
    }
  }
}

export { pc };
