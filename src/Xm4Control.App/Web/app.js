// Fenêtre principale de XM4 Control. L'appli envoie l'état complet à chaque changement (WebState côté C#) ;
// la page renvoie des commandes {cmd, value}. Les valeurs affichées viennent toujours du casque : une commande
// ne change l'affichage qu'au retour de l'état.
'use strict';

const host = window.chrome && window.chrome.webview;
let state = null;
let view = 'home';
let draggingBands = null;

const $ = (sel, root = document) => root.querySelector(sel);
const $$ = (sel, root = document) => [...root.querySelectorAll(sel)];

function send(cmd, value) {
  if (host) host.postMessage(value === undefined ? { cmd } : { cmd, value });
  else if (preview) preview(cmd, value);
}

// --- Navigation ---

function setView(name) {
  if (!document.getElementById('view-' + name)) return;
  view = name;
  for (const v of $$('.view')) v.hidden = v.id !== 'view-' + name;
  for (const b of $$('.tab')) {
    if (b.dataset.view === name) b.setAttribute('aria-current', 'page');
    else b.removeAttribute('aria-current');
  }
  $('.content').scrollTop = 0;
}

for (const b of $$('[data-view]')) b.addEventListener('click', () => setView(b.dataset.view));
setView('home');
for (const b of $$('[data-go]')) b.addEventListener('click', () => setView(b.dataset.go));

// --- Contrôles génériques ---

for (const b of $$('[data-mode]')) b.addEventListener('click', () => send('mode', b.dataset.mode));

for (const s of $$('[data-switch]')) {
  s.addEventListener('click', () => {
    if (!state) return;
    send(s.dataset.cmd || s.dataset.switch, s.getAttribute('aria-checked') !== 'true');
  });
}

// Cadran du son ambiant : boutons, molette et flèches ; réglable seulement en mode Son ambiant.
function stepAmbient(delta) {
  if (!state || state.mode !== 'ambient') return;
  const level = Math.max(1, Math.min(20, state.ambientLevel + delta));
  if (level === state.ambientLevel) return;
  state.ambientLevel = level;
  paintDial(level, true);
  send('ambientLevel', level);
}
$('#ambient-down').addEventListener('click', () => stepAmbient(-1));
$('#ambient-up').addEventListener('click', () => stepAmbient(1));
$('#dial').addEventListener('wheel', (e) => { e.preventDefault(); stepAmbient(e.deltaY < 0 ? 1 : -1); }, { passive: false });
$('#dial').addEventListener('keydown', (e) => {
  const d = { ArrowUp: 1, ArrowRight: 1, ArrowDown: -1, ArrowLeft: -1 }[e.key];
  if (d) { e.preventDefault(); stepAmbient(d); }
});

function paintDial(level, active) {
  $('#dial-fill').style.strokeDasharray = (330 * level) / 20 + ' 440';
  $('#dial-number').textContent = level;
  $('#dial').setAttribute('aria-valuenow', level);
  $('#dial').setAttribute('aria-disabled', String(!active));
}

const MODES = {
  nc: { title: 'Réduction de bruit', hint: 'Isolation maximale : le monde extérieur se tait.', tint: '#8ec5ff' },
  ambient: { title: 'Son ambiant', hint: 'Vous entendez ce qui se passe autour de vous.', tint: '#f2c27b' },
  wind: { title: 'Réduction du vent', hint: 'Coupe le souffle du vent, en extérieur.', tint: '#7fd8c8' },
  off: { title: 'Désactivé', hint: 'Son naturel, sans traitement.', tint: '#a3acb8' },
};

for (const r of $$('[data-range]')) {
  r.addEventListener('input', () => {
    paintRange(r);
    for (const el of $$('[data-text="' + r.dataset.range + '"]')) el.textContent = r.value;
    send(r.dataset.range, Number(r.value));
  });
}

for (const sel of $$('[data-select]')) {
  sel.addEventListener('change', () => send(sel.dataset.cmd, Number(sel.value)));
}

for (const b of $$('[data-cmd]:not([data-switch]):not(select)')) {
  b.addEventListener('click', () => send(b.dataset.cmd));
}

for (const a of $$('a.link')) {
  a.addEventListener('click', (e) => { e.preventDefault(); send('open', a.href); });
}

const dialog = $('#confirm-power');
$('#power-off').addEventListener('click', () => dialog.showModal());
$('#confirm-cancel').addEventListener('click', () => dialog.close());
$('#confirm-ok').addEventListener('click', () => { dialog.close(); send('powerOff'); });

// Pas de menu contextuel ni de zoom de navigateur dans une fenêtre d'appli.
document.addEventListener('contextmenu', (e) => e.preventDefault());

function paintRange(r) {
  const min = Number(r.min), max = Number(r.max);
  r.style.setProperty('--fill', ((Number(r.value) - min) / (max - min)) * 100 + '%');
}

// --- Rendu de l'état ---

function render(s) {
  state = s;
  document.body.classList.toggle('connected', s.connected);
  document.body.classList.toggle('low-battery', s.battery !== null && s.battery <= 20 && !s.charging);
  document.body.dataset.mode = s.mode || '';

  for (const el of $$('[data-text]')) {
    const v = s[el.dataset.text];
    el.textContent = v === null || v === undefined ? '' : String(v);
  }

  $('#banner-disconnected').hidden = s.connected;
  $('#banner-error').hidden = !s.error;

  // Batterie.
  const hasBattery = s.battery !== null;
  for (const el of $$('[data-show="battery"]')) el.hidden = !hasBattery;
  if (hasBattery) {
    $('#battery-fill').style.width = s.battery + '%';
    $('#battery-text').textContent = s.battery + ' %' + (s.charging ? ' · en charge' : '');
  }
  $('#stat-battery').textContent = hasBattery ? s.battery + ' %' + (s.charging ? ' · en charge' : '') : '—';

  for (const b of $$('[data-mode]')) {
    b.setAttribute('aria-checked', String(s.mode === b.dataset.mode));
    b.disabled = !s.connected;
  }

  const ambient = s.mode === 'ambient';
  const mode = MODES[s.mode];
  $('#mode-title').textContent = mode ? mode.title : s.connected ? '—' : 'Casque déconnecté';
  $('#mode-hint').textContent = mode ? mode.hint : s.disconnectedHint;
  window.headsetTint = mode ? mode.tint : '#a3acb8';
  if (window.setHeadsetTint) window.setHeadsetTint(window.headsetTint);

  paintDial(s.ambientLevel, ambient);
  $('#ambient-card').classList.toggle('disabled', !ambient);
  $('#ambient-down').disabled = !ambient || s.ambientLevel <= 1;
  $('#ambient-up').disabled = !ambient || s.ambientLevel >= 20;
  $('#dial-state').textContent = ambient ? 'actif' : 'inactif';

  for (const r of $$('[data-range]')) {
    if (document.activeElement !== r) r.value = s[r.dataset.range];
    r.disabled = !ambient;
    paintRange(r);
  }

  for (const sw of $$('[data-switch]')) {
    const key = sw.dataset.switch;
    const v = s[key];
    sw.setAttribute('aria-checked', String(v === true));
    let disabled = v === null || v === undefined;
    if (key === 'focusOnVoice') disabled = !ambient;
    if (key === 'speakToChatVoiceFocus') disabled = !s.speakToChatConfig;
    sw.disabled = disabled;
    const item = sw.closest('.item');
    if (item) item.classList.toggle('disabled', disabled);
  }

  for (const sel of $$('[data-select]')) {
    fillOptions(sel, s[sel.dataset.options]);
    sel.value = String(s[sel.dataset.select]);
    sel.disabled = !s.speakToChatConfig;
  }
  $('#s2c-config').hidden = s.speakToChat !== true;

  const opt = $('#optimizer-btn');
  opt.textContent = s.optimizing ? 'Arrêter' : 'Lancer';
  opt.disabled = !s.connected;

  $('#mic-hint').textContent = s.mic === null ? 'Indisponible'
    : s.mic ? 'Profil mains libres actif' : 'Stéréo haute qualité, micro coupé';
  $('#home-preset').textContent = s.presets[s.presetIndex] || '—';
  $('#power-off').disabled = !s.connected;

  renderPresets(s);
  if (!draggingBands) drawEq(s.bands);
  drawMini(s.bands);

  if (s.page) setView(s.page);
}

function fillOptions(sel, labels) {
  const key = labels.join('\n');
  if (sel.dataset.filled === key) return;
  sel.dataset.filled = key;
  sel.replaceChildren(...labels.map((label, i) => new Option(label, String(i))));
}

function renderPresets(s) {
  const box = $('#presets');
  const key = s.presets.join('\n');
  if (box.dataset.filled !== key) {
    box.dataset.filled = key;
    box.replaceChildren(...s.presets.map((label, i) => {
      const b = document.createElement('button');
      b.className = 'pill';
      b.setAttribute('role', 'radio');
      b.textContent = label;
      b.addEventListener('click', () => send('preset', i));
      return b;
    }));
  }
  [...box.children].forEach((b, i) => {
    b.setAttribute('aria-checked', String(i === s.presetIndex));
    b.disabled = !s.connected;
  });
}

// --- Égaliseur ---

const EQ = { w: 1000, padX: 80, top: 56, bottom: 290, min: -10, max: 10 };
const svgNs = 'http://www.w3.org/2000/svg';
const eqX = (i, n) => EQ.padX + (i * (EQ.w - 2 * EQ.padX)) / (n - 1);
const eqY = (v) => EQ.top + ((EQ.max - v) / (EQ.max - EQ.min)) * (EQ.bottom - EQ.top);

// Courbe de Catmull-Rom passant par les points, convertie en courbes de Bézier.
function smoothPath(pts) {
  let d = 'M' + pts[0][0] + ' ' + pts[0][1];
  for (let i = 0; i < pts.length - 1; i++) {
    const p0 = pts[i - 1] || pts[i], p1 = pts[i], p2 = pts[i + 1], p3 = pts[i + 2] || p2;
    const c1 = [p1[0] + (p2[0] - p0[0]) / 6, p1[1] + (p2[1] - p0[1]) / 6];
    const c2 = [p2[0] - (p3[0] - p1[0]) / 6, p2[1] - (p3[1] - p1[1]) / 6];
    d += ' C' + c1.join(' ') + ', ' + c2.join(' ') + ', ' + p2.join(' ');
  }
  return d;
}

function el(name, attrs, text) {
  const e = document.createElementNS(svgNs, name);
  for (const [k, v] of Object.entries(attrs)) e.setAttribute(k, v);
  if (text !== undefined) e.textContent = text;
  return e;
}

function drawEq(bands) {
  const n = bands.length;
  const grid = $('#eq-grid');
  if (!grid.childElementCount) {
    for (const v of [10, 5, -5, -10]) grid.append(el('path', { class: 'eq-grid-line', d: 'M' + EQ.padX / 2 + ' ' + eqY(v) + ' H' + (EQ.w - EQ.padX / 2) }));
    grid.append(el('path', { class: 'eq-zero', d: 'M' + EQ.padX / 2 + ' ' + eqY(0) + ' H' + (EQ.w - EQ.padX / 2) }));
    for (let i = 0; i < n; i++) {
      grid.append(el('path', { class: 'eq-guide', d: 'M' + eqX(i, n) + ' ' + EQ.top + ' V' + EQ.bottom }));
      grid.append(el('text', { class: 'eq-label', x: eqX(i, n), y: EQ.bottom + 44 }, state.bandLabels[i]));
    }
  }
  const pts = bands.map((v, i) => [eqX(i, n), eqY(v)]);
  const line = smoothPath(pts);
  $('#eq-line').setAttribute('d', line);
  $('#eq-area').setAttribute('d', line + ' V' + EQ.bottom + ' H' + pts[0][0] + ' Z');

  const g = $('#eq-points');
  if (g.childElementCount !== n * 2) {
    g.replaceChildren();
    for (let i = 0; i < n; i++) {
      const h = el('circle', { class: 'eq-handle', r: 12, 'data-band': i });
      h.addEventListener('pointerdown', (e) => startDrag(e, i));
      h.addEventListener('wheel', (e) => wheelBand(e, i), { passive: false });
      g.append(el('text', { class: 'eq-value', 'data-value': i }), h);
    }
  }
  bands.forEach((v, i) => {
    const h = g.querySelector('[data-band="' + i + '"]');
    h.setAttribute('cx', pts[i][0]);
    h.setAttribute('cy', pts[i][1]);
    const t = g.querySelector('[data-value="' + i + '"]');
    t.setAttribute('x', pts[i][0]);
    t.setAttribute('y', pts[i][1] - 24);
    t.textContent = v > 0 ? '+' + v : String(v);
  });
}

function bandFromPointer(e) {
  const svg = $('#eq');
  const p = new DOMPoint(e.clientX, e.clientY).matrixTransform(svg.getScreenCTM().inverse());
  const v = EQ.max - ((p.y - EQ.top) / (EQ.bottom - EQ.top)) * (EQ.max - EQ.min);
  return Math.max(EQ.min, Math.min(EQ.max, Math.round(v)));
}

function startDrag(e, i) {
  if (!state || !state.connected) return;
  e.preventDefault();
  const handle = e.currentTarget;
  handle.setPointerCapture(e.pointerId);
  handle.classList.add('drag');
  draggingBands = state.bands.slice();
  const move = (ev) => {
    const v = bandFromPointer(ev);
    if (v === draggingBands[i]) return;
    draggingBands[i] = v;
    drawEq(draggingBands);
  };
  const up = () => {
    handle.classList.remove('drag');
    handle.removeEventListener('pointermove', move);
    handle.removeEventListener('pointerup', up);
    handle.removeEventListener('lostpointercapture', up);
    const bands = draggingBands;
    draggingBands = null;
    if (bands.some((v, k) => v !== state.bands[k])) send('bands', bands);
    else drawEq(state.bands);
  };
  handle.addEventListener('pointermove', move);
  handle.addEventListener('pointerup', up);
  handle.addEventListener('lostpointercapture', up);
}

let wheelTimer = 0;
let wheelBands = null;
function wheelBand(e, i) {
  if (!state || !state.connected) return;
  e.preventDefault();
  wheelBands = wheelBands || state.bands.slice();
  wheelBands[i] = Math.max(EQ.min, Math.min(EQ.max, wheelBands[i] + (e.deltaY < 0 ? 1 : -1)));
  draggingBands = wheelBands;
  drawEq(wheelBands);
  clearTimeout(wheelTimer);
  wheelTimer = setTimeout(() => {
    const bands = wheelBands;
    wheelBands = null;
    draggingBands = null;
    send('bands', bands);
  }, 400);
}

function drawMini(bands) {
  const n = bands.length;
  const pts = bands.map((v, i) => [(i * 300) / (n - 1), 27 - v * 2.2]);
  const line = smoothPath(pts);
  $('#mini-line').setAttribute('d', line);
}

// --- Liaison avec l'appli ---

let preview = null;
if (host) {
  host.addEventListener('message', (e) => render(e.data));
  send('ready');
} else {
  // Aperçu hors de l'appli (navigateur) : état d'exemple, les commandes ne modifient que cet état local.
  const s = {
    connected: true, deviceName: 'WH-1000XM4', statusText: 'Connecté · AAC', disconnectedHint: '', error: '',
    battery: 80, charging: false, codec: 'AAC', firmware: '—', mode: 'nc', ambientLevel: 12, focusOnVoice: false,
    optimizing: false, optimizerStatus: '', mic: true,
    presets: ['Désactivé', 'Bright', 'Excited', 'Mellow', 'Relaxed', 'Vocal', 'Treble Boost', 'Bass Boost', 'Speech', 'Manuel', 'Personnalisé 1', 'Personnalisé 2'],
    presetIndex: 7, bands: [6, 2, -1, 0, 1, 2], bandLabels: ['Clear Bass', '400', '1k', '2,5k', '6,3k', '16k'],
    dsee: false, speakToChat: true, speakToChatConfig: true, sensitivities: ['Auto', 'Élevée', 'Basse'], sensitivityIndex: 0,
    timeouts: ['Court', 'Normal', 'Long', 'Jamais'], timeoutIndex: 1, speakToChatVoiceFocus: false,
    pauseWhenTakenOff: true, autoPowerOff: true, touchSensor: true, voiceGuidance: false,
    startWithWindows: true, hotkeyEnabled: true, hotkeyError: '', lowBatteryAlert: true, version: 'aperçu', page: null,
  };
  const keys = { ambientLevel: 'ambientLevel', preset: 'presetIndex', sensitivity: 'sensitivityIndex', timeout: 'timeoutIndex', hotkey: 'hotkeyEnabled' };
  preview = (cmd, value) => {
    if (cmd === 'mode') s.mode = value;
    else if (cmd === 'bands') { s.bands = value; s.presetIndex = 9; }
    else if (cmd in s || cmd in keys) s[keys[cmd] || cmd] = value;
    render(s);
  };
  render(s);
}
