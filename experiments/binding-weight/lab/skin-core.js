(function (root, factory) {
  const api = factory();
  if (typeof module === 'object' && module.exports) module.exports = api;
  else root.SkinLabCore = api;
})(typeof globalThis === 'object' ? globalThis : this, function () {
  'use strict';
  const identity = () => [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1];
  function multiply(a, b) {
    const r = new Array(16).fill(0);
    for (let i = 0; i < 4; i++) for (let j = 0; j < 4; j++)
      for (let k = 0; k < 4; k++) r[i * 4 + j] += a[i * 4 + k] * b[k * 4 + j];
    return r;
  }
  function point(m, p) {
    return [m[0] * p[0] + m[1] * p[1] + m[2] * p[2] + m[3],
      m[4] * p[0] + m[5] * p[1] + m[6] * p[2] + m[7],
      m[8] * p[0] + m[9] * p[1] + m[10] * p[2] + m[11]];
  }
  function quaternionMultiply(a, b) {
    return [a[3] * b[0] + a[0] * b[3] + a[1] * b[2] - a[2] * b[1],
      a[3] * b[1] - a[0] * b[2] + a[1] * b[3] + a[2] * b[0],
      a[3] * b[2] + a[0] * b[1] - a[1] * b[0] + a[2] * b[3],
      a[3] * b[3] - a[0] * b[0] - a[1] * b[1] - a[2] * b[2]];
  }
  function trs(p, quaternion, s) {
    const length = Math.hypot(...quaternion);
    const [x, y, z, w] = quaternion.map(v => v / length);
    return [(1 - 2 * (y * y + z * z)) * s[0], 2 * (x * y - z * w) * s[1], 2 * (x * z + y * w) * s[2], p[0],
      2 * (x * y + z * w) * s[0], (1 - 2 * (x * x + z * z)) * s[1], 2 * (y * z - x * w) * s[2], p[1],
      2 * (x * z - y * w) * s[0], 2 * (y * z + x * w) * s[1], (1 - 2 * (x * x + y * y)) * s[2], p[2],
      0, 0, 0, 1];
  }
  function decode(encoded, Type) {
    const binary = atob(encoded), bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
    return new Type(bytes.buffer);
  }
  function decodeModel(data) {
    const meshes = data.meshes.map(m => ({...m,
      position: decode(m.position, Float32Array), normal: decode(m.normal, Float32Array),
      weight: decode(m.weight, Float32Array), skinIndex: decode(m.skinIndex, Uint16Array),
      index: decode(m.index, Uint32Array),
      inverseBindMatrices: m.boneIds.map((_, i) => m.inverseBind.slice(i * 16, (i + 1) * 16))
    }));
    return {...data, meshes};
  }
  const emptyOffset = () => ({position: [0, 0, 0], quaternion: [0, 0, 0, 1]});
  function createPose(bones) {
    return {offsets: bones.map(emptyOffset), locals: bones.map(identity), globals: bones.map(identity)};
  }
  function updatePose(bones, pose) {
    for (let i = 0; i < bones.length; i++) {
      const b = bones[i], d = pose.offsets[i];
      if (b.parent >= i) throw Error('Hierarchy must be parent-first');
      pose.locals[i] = trs(b.position.map((v, j) => v + d.position[j]),
        quaternionMultiply(b.quaternion, d.quaternion), b.scale);
      pose.globals[i] = b.parent < 0 ? pose.locals[i] : multiply(pose.globals[b.parent], pose.locals[i]);
    }
    return pose;
  }
  function descendants(bones, id) {
    const set = new Set([id]);
    for (let i = id + 1; i < bones.length; i++) if (set.has(bones[i].parent)) set.add(i);
    return set;
  }
  function skinMatrices(mesh, pose) {
    return mesh.boneIds.map((id, i) => multiply(pose.globals[id], mesh.inverseBindMatrices[i]));
  }
  function skinVertex(mesh, index, matrices) {
    const p = Array.from(mesh.position.subarray(index * 3, index * 3 + 3)), out = [0, 0, 0];
    for (let k = 0; k < 4; k++) {
      const w = mesh.weight[index * 4 + k];
      if (!w) continue;
      const q = point(matrices[mesh.skinIndex[index * 4 + k]], p);
      for (let j = 0; j < 3; j++) out[j] += w * q[j];
    }
    return out;
  }
  function vertexWeight(mesh, index, set) {
    let weight = 0;
    for (let k = 0; k < 4; k++) if (set.has(mesh.boneIds[mesh.skinIndex[index * 4 + k]]))
      weight += mesh.weight[index * 4 + k];
    return weight;
  }
  return {identity, multiply, point, trs, quaternionMultiply, decodeModel, emptyOffset,
    createPose, updatePose, descendants, skinMatrices, skinVertex, vertexWeight};
});
