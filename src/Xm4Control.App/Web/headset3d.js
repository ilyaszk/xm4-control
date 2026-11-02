// Casque en 3D (three.js) : on le fait tourner à la souris, horizontalement et verticalement.
// Sans WebGL ou si le modèle ne charge pas, l'image fixe (headset.png) reste affichée.
import * as THREE from './vendor/three/three.module.min.js';
import { GLTFLoader } from './vendor/three/loaders/GLTFLoader.js';
import { OrbitControls } from './vendor/three/controls/OrbitControls.js';
import { RoomEnvironment } from './vendor/three/environments/RoomEnvironment.js';

const stage = document.querySelector('.stage');
const canvas = document.getElementById('headset-3d');
const fallback = document.getElementById('headset-img');

// Vue de départ : trois quarts face, comme l'image fixe.
const START_AZIMUTH = THREE.MathUtils.degToRad(35);
const START_POLAR = THREE.MathUtils.degToRad(80);
const DISTANCE = 2.6;

let renderer;
try {
  renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: true, powerPreference: 'low-power' });
} catch {
  renderer = null;
}

if (renderer) {
  renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
  renderer.setClearColor(0x000000, 0);
  renderer.toneMapping = THREE.ACESFilmicToneMapping;
  renderer.toneMappingExposure = 1.05;
  renderer.outputColorSpace = THREE.SRGBColorSpace;

  const scene = new THREE.Scene();
  const pmrem = new THREE.PMREMGenerator(renderer);
  scene.environment = pmrem.fromScene(new RoomEnvironment(), 0.04).texture;
  pmrem.dispose();

  const key = new THREE.DirectionalLight(0xffffff, 1.1);
  key.position.set(2, 3, 3);
  scene.add(key);
  // Contre-jour à la couleur du mode : le casque se détache du halo.
  const rim = new THREE.DirectionalLight(0x8ec5ff, 2.2);
  rim.position.set(-3, 1.5, -3);
  scene.add(rim);

  const camera = new THREE.PerspectiveCamera(30, 1, 0.01, 50);
  const controls = new OrbitControls(camera, canvas);
  controls.enableZoom = false;
  controls.enablePan = false;
  controls.enableDamping = true;
  controls.dampingFactor = 0.08;
  controls.rotateSpeed = 0.7;
  controls.minPolarAngle = THREE.MathUtils.degToRad(15);
  controls.maxPolarAngle = THREE.MathUtils.degToRad(165);

  function placeCamera(azimuth, polar) {
    camera.position.setFromSphericalCoords(DISTANCE, polar, azimuth);
    camera.lookAt(0, 0, 0);
    controls.update();
  }
  placeCamera(START_AZIMUTH, START_POLAR);

  // Rendu à la demande : seulement pendant un mouvement (glisser, inertie, retour à la vue de départ).
  let frame = 0;
  let reset = null;
  function tick() {
    frame = 0;
    if (reset) {
      const t = Math.min(1, (performance.now() - reset.start) / 600);
      const e = 1 - Math.pow(1 - t, 3);
      placeCamera(reset.from.theta + (reset.to.theta - reset.from.theta) * e, reset.from.phi + (reset.to.phi - reset.from.phi) * e);
      if (t >= 1) reset = null;
    }
    const moving = controls.update();
    renderer.render(scene, camera);
    if (moving || reset) frame = requestAnimationFrame(tick);
  }
  const request = () => { if (!frame) frame = requestAnimationFrame(tick); };
  controls.addEventListener('change', request);
  controls.addEventListener('start', () => stage.classList.add('grabbing'));
  controls.addEventListener('end', () => stage.classList.remove('grabbing'));

  canvas.addEventListener('dblclick', () => {
    const s = new THREE.Spherical().setFromVector3(camera.position);
    // Chemin le plus court vers l'azimut de départ.
    let to = START_AZIMUTH;
    while (to - s.theta > Math.PI) to -= 2 * Math.PI;
    while (s.theta - to > Math.PI) to += 2 * Math.PI;
    reset = { start: performance.now(), from: { theta: s.theta, phi: s.phi }, to: { theta: to, phi: START_POLAR } };
    request();
  });

  // Clavier : flèches pour tourner (le canvas est focalisable).
  canvas.addEventListener('keydown', (e) => {
    const step = THREE.MathUtils.degToRad(10);
    const s = new THREE.Spherical().setFromVector3(camera.position);
    const moves = { ArrowLeft: [-step, 0], ArrowRight: [step, 0], ArrowUp: [0, -step], ArrowDown: [0, step] };
    if (!moves[e.key]) return;
    e.preventDefault();
    const phi = THREE.MathUtils.clamp(s.phi + moves[e.key][1], controls.minPolarAngle, controls.maxPolarAngle);
    placeCamera(s.theta + moves[e.key][0], phi);
    request();
  });

  function resize() {
    const w = stage.clientWidth, h = stage.clientHeight;
    if (!w || !h) return;
    renderer.setSize(w, h, false);
    camera.aspect = w / h;
    camera.updateProjectionMatrix();
    request();
  }
  new ResizeObserver(resize).observe(stage);

  // Couleur du contre-jour, appelée par app.js à chaque changement de mode.
  window.setHeadsetTint = (hex) => {
    rim.color.set(hex);
    request();
  };

  new GLTFLoader().load('headset.glb', (gltf) => {
    const model = gltf.scene;
    // Centré sur l'origine, plus grande dimension = 1.
    const box = new THREE.Box3().setFromObject(model);
    const size = box.getSize(new THREE.Vector3());
    const center = box.getCenter(new THREE.Vector3());
    const scale = 1 / Math.max(size.x, size.y, size.z);
    model.scale.setScalar(scale);
    model.position.copy(center).multiplyScalar(-scale);
    scene.add(model);
    resize();
    canvas.hidden = false;
    fallback.hidden = true;
    stage.classList.add('is-3d');
    if (window.headsetTint) window.setHeadsetTint(window.headsetTint);
  }, undefined, () => {
    // Le modèle n'a pas chargé : on garde l'image fixe.
    renderer.dispose();
  });
}
