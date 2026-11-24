// Christmas look ("festive": true in Quiz/quiz.json): falling block-chain snowflakes with the odd
// bitcoin or ethereum coin among them, Santa hats on faces, wordmarks and the timer, a block garland,
// a stack of numbered gifts and a holiday-market ticker in the lobby.
// Everything here is decoration: it sits behind the game and never takes clicks.
const Festive = (() => {
  const api = { on: false, ready: null, hat, stage, calm };

  const SPRITE = `<svg width="0" height="0" style="position:absolute" aria-hidden="true"><defs>
    <symbol id="fx-flake" viewBox="-50 -50 100 100"><g fill="none" stroke="currentColor" stroke-width="6.5" stroke-linecap="round">
      ${[0, 60, 120, 180, 240, 300].map(a => `<g transform="rotate(${a})"><path d="M0 -10 V-36 M0 -24 L-9 -32 M0 -24 L9 -32"/><rect x="-6" y="-48" width="12" height="12" rx="2.5" transform="rotate(45 0 -42)" fill="currentColor" stroke="none"/></g>`).join('')}
      <path d="M0 -9 L7.8 -4.5 V4.5 L0 9 L-7.8 4.5 V-4.5 Z"/></g></symbol>
    <symbol id="fx-hat" viewBox="0 0 120 96" overflow="visible">
      <path d="M14 70 C18 34 44 8 76 10 C96 12 108 30 104 52 C98 40 88 34 80 36 C88 46 92 58 92 70 Z" fill="#d93a28"/>
      <path d="M80 36 C88 46 92 58 92 70 L70 70 C72 56 76 44 80 36Z" fill="#000" opacity=".14"/>
      <circle cx="104" cy="56" r="12" fill="#fff"/><rect x="4" y="62" width="98" height="24" rx="12" fill="#fff"/>
      <path d="M12 80 Q52 88 96 80" fill="none" stroke="#dbe4fb" stroke-width="4" stroke-linecap="round"/></symbol>
    <symbol id="fx-cube" viewBox="-50 -50 100 100"><path d="M0 -40 L36 -20 V20 L0 40 L-36 20 V-20 Z" fill="currentColor"/><path d="M-36 -20 L0 0 L36 -20 L0 -40Z" fill="#fff" opacity=".35"/><path d="M0 0 L36 -20 V20 L0 40Z" fill="#000" opacity=".18"/><path d="M-22 -18 L-6 -9" stroke="#fff" stroke-width="5" stroke-linecap="round" opacity=".7"/></symbol>
    <symbol id="fx-btc" viewBox="-50 -50 100 100"><circle r="46" fill="#f7931a"/><circle r="46" fill="none" stroke="#000" stroke-opacity=".14" stroke-width="6"/><circle cx="-14" cy="-18" r="7" fill="#fff" opacity=".45"/>
      <g transform="rotate(14)" fill="none" stroke="#fff" stroke-width="7" stroke-linejoin="round" stroke-linecap="round">
        <path d="M-12 -20 H5 A9.5 9.5 0 0 1 5 -1 H-12 Z M-12 -1 H8 A10.5 10.5 0 0 1 8 20 H-12 Z"/><path d="M-4 -28 V-20 M5 -28 V-20 M-4 20 V28 M5 20 V28"/></g></symbol>
    <symbol id="fx-eth" viewBox="-50 -50 100 100"><circle r="46" fill="#627eea"/><circle r="46" fill="none" stroke="#000" stroke-opacity=".16" stroke-width="6"/><circle cx="-16" cy="-20" r="6" fill="#fff" opacity=".4"/>
      <path d="M0 -33 L19 1 L0 12 L-19 1 Z" fill="#fff"/><path d="M0 -33 L19 1 L0 12 Z" fill="#c9d3f8"/><path d="M0 -8 L19 1 L0 12 L-19 1 Z" fill="#000" opacity=".1"/>
      <path d="M-19 6 L0 17 L19 6 L0 33 Z" fill="#fff"/><path d="M0 17 L19 6 L0 33 Z" fill="#c9d3f8"/></symbol>
  </defs></svg>`;

  let seed = 7;
  const rnd = () => (seed = (seed * 9301 + 49297) % 233280) / 233280;

  /** A Santa hat to put on a face (sized and placed by CSS: .hatted .hat). */
  // Every player wears their hat a little differently: on top, slid to the left or right, tilted, or mirrored.
  // dx: sideways shift and y: height, both as % of the face; r: tilt; f: -1 mirrors the hat (its tip then droops left).
  const POSES = [
    { dx: -27, y: -40, r: -16, f: 1 },   // tilted, on the left
    { dx: 0, y: -48, r: -3, f: 1 },      // straight on top
    { dx: 27, y: -40, r: 16, f: -1 },    // tilted, on the right
    { dx: -14, y: -45, r: -8, f: -1 },   // slightly left, mirrored
    { dx: 0, y: -48, r: 4, f: -1 },      // straight on top, mirrored
    { dx: 14, y: -45, r: 9, f: 1 },      // slightly right
    { dx: -22, y: -42, r: -22, f: -1 },  // jaunty, on the left, mirrored
  ];
  /** A Santa hat for a face; the pose follows the player's seat, so it stays the same for them all game. */
  function hat(seat = 1) {
    const p = POSES[(Math.max(1, seat) - 1) % POSES.length];
    return `<svg class="hat" viewBox="0 0 120 96" style="--hdx:${p.dx}%;--hy:${p.y}%;--hr:${p.r}deg;--hf:${p.f}" aria-hidden="true"><use href="#fx-hat"/></svg>`;
  }

  // ----- snow over the whole window: mostly flakes, now and then a bitcoin or an ethereum coin -----
  function snow(big) {
    const layer = document.createElement('div');
    layer.className = 'fx-snow';
    layer.setAttribute('aria-hidden', 'true');
    let html = '';
    const flakes = big ? 46 : 16;
    for (let i = 0; i < flakes; i++) {
      const size = big ? 16 + rnd() * 28 : 16 + rnd() * 18;
      html += `<svg class="fl" viewBox="0 0 100 100" style="left:${(rnd() * 100).toFixed(2)}%;--y:${(rnd() * 96).toFixed(1)}vh;width:${size.toFixed(0)}px;height:${size.toFixed(0)}px;` +
        `color:${rnd() < .5 ? '#7f9be0' : '#a9bdf0'};opacity:${(.35 + rnd() * .4).toFixed(2)};--dx:${(rnd() * 120 - 60).toFixed(0)}px;--rot:${(rnd() * 240 - 120).toFixed(0)}deg;` +
        `animation-duration:${(14 + rnd() * 10).toFixed(1)}s;animation-delay:${(-rnd() * 24).toFixed(1)}s"><use href="#fx-flake"/></svg>`;
    }
    // Coins fall rarely: each one crosses the screen in the first part of a long cycle, then waits off-screen.
    const coins = big ? [['fx-btc', 40], ['fx-eth', 40], ['fx-btc', 40], ['fx-eth', 40]] : [['fx-btc', 28], ['fx-eth', 28]];
    coins.forEach(([id, size], i) => {
      const cycle = 70;
      html += `<div class="coin" style="left:${(8 + rnd() * 84).toFixed(1)}%;width:${size}px;height:${size}px;--dx:${(rnd() * 80 - 40).toFixed(0)}px;` +
        `animation-duration:${cycle}s;animation-delay:${(-(i * cycle) / coins.length * 1.7 - rnd() * 8).toFixed(1)}s">` +
        `<svg viewBox="0 0 100 100" style="animation-duration:${(3 + rnd() * 2).toFixed(1)}s"><use href="#${id}"/></svg></div>`;
    });
    layer.innerHTML = html;
    document.body.prepend(layer);
  }

  /** Fewer flakes while a question is on screen. */
  function calm(on) {
    document.querySelector('.fx-snow')?.classList.toggle('calm', !!on);
  }

  // ----- presenter decorations, drawn in the 1440×900 stage coordinates -----
  const PIECES = ['#d93a28', '#23843a', '#0f78b5', '#f2b705'];

  // A chain swag with glass blocks that light up one after another, like blocks being confirmed.
  function garland(x1, x2, sag) {
    const y = t => -6 + sag * 4 * t * (1 - t);
    let links = '';
    for (let i = 0; i < 40; i++) {
      const t0 = i / 40, t1 = (i + 1) / 40;
      const ax = x1 + (x2 - x1) * t0, bx = x1 + (x2 - x1) * t1, ay = y(t0), by = y(t1);
      const a = Math.atan2(by - ay, bx - ax) * 180 / Math.PI, cx = (ax + bx) / 2, cy = (ay + by) / 2;
      links += `<rect x="${(cx - 7).toFixed(1)}" y="${(cy - 3.5).toFixed(1)}" width="14" height="7" rx="3.5" transform="rotate(${a.toFixed(1)} ${cx.toFixed(1)} ${cy.toFixed(1)})"${i % 2 ? ' opacity=".7"' : ''}/>`;
    }
    let blocks = '';
    const n = 7;
    for (let k = 1; k <= n; k++) {
      const t = k / (n + 1), x = x1 + (x2 - x1) * t;
      blocks += `<svg class="blk" viewBox="0 0 100 100" style="left:${(x - 21).toFixed(0)}px;top:${(y(t) + 4).toFixed(0)}px;color:${PIECES[k % 4]};animation-delay:${(k * .6).toFixed(1)}s"><use href="#fx-cube"/></svg>`;
    }
    return `<svg class="chain" viewBox="0 0 1440 900">${links}</svg>${blocks}`;
  }

  // Presents stacked in a corner, tied together with ribbon and numbered like blocks.
  function gifts(side) {
    const boxes = [[0, 110, 96, 80, '#d93a28', 1], [92, 120, 74, 70, '#0f78b5', 2], [30, 44, 80, 66, '#23843a', 3]];
    const flip = side === 'right';
    let s = '';
    for (const [bx, by, w, h, c, n] of boxes) {
      const tag = flip ? `translate(${w - 8} ${h - 26}) scale(-1 1)` : `translate(8 ${h - 26})`;
      s += `<g transform="translate(${bx} ${by})"><rect width="${w}" height="${h}" rx="6" fill="${c}"/><rect y="${h - 10}" width="${w}" height="10" rx="4" fill="#000" opacity=".18"/>
        <rect x="${w / 2 - 6}" width="12" height="${h}" fill="#fff" opacity=".9"/><rect y="${h * .3}" width="${w}" height="10" fill="#fff" opacity=".9"/>
        <path d="M${w / 2} 0 c-18 -22 -34 -4 -14 2 Z M${w / 2} 0 c18 -22 34 -4 14 2 Z" fill="#fff"/>
        <g transform="${tag}"><rect x="-2" y="-2" width="34" height="20" rx="5" fill="#14213d"/><text x="15" y="13" text-anchor="middle" font-family="JetBrains Mono, monospace" font-weight="800" font-size="13" fill="#fff">#${n}</text></g></g>`;
    }
    s += `<path d="M48 110 C48 100 70 112 70 110 M128 120 C128 100 96 110 94 110" fill="none" stroke="#a9bdf0" stroke-width="4" stroke-dasharray="6 5" stroke-linecap="round"/>`;
    return `<svg class="gifts ${side}" viewBox="-20 0 200 190">${s}</svg>`;
  }

  // The festive market, lobby only. Clearly silly prices, so nobody takes them for real ones.
  function ticker() {
    const items = [['QVZ', 1, '99.9'], ['SNOW', 1, '12.4'], ['COCOA', 1, '3.1'], ['SOCKS', 0, '2.0'], ['SLEIGH', 1, '24.0'], ['REINDEER', 1, '8.8'],
      ['MINCE PIE', 0, '0.7'], ['TINSEL', 1, '5.5'], ['GINGERBREAD', 1, '4.2'], ['ELF', 1, '1.2']];
    const run = items.map(([n, up, p]) => `<span class="it"><b>${n}</b><span class="${up ? 'up' : 'down'}">${up ? Qvizz.ICON.up : Qvizz.ICON.down}${p}%</span></span>`).join('');
    return `<div class="fx-ticker" aria-hidden="true"><div class="lbl">Holiday market</div><div class="run" style="animation-delay:${-(Date.now() / 1000 % 36).toFixed(2)}s">${run}${run}</div></div>`;
  }

  /** Decoration for a presenter screen; empty when the Christmas look is off. */
  function stage(kind) {
    if (!api.on) return '';
    seed = 7;
    if (kind === 'lobby') return `<div class="fx-stage">${garland(-20, 430, 70)}${garland(1010, 1460, 70)}${gifts('left')}${gifts('right')}</div>${ticker()}`;
    if (kind === 'standings') return `<div class="fx-stage">${garland(1060, 1460, 70)}</div>`;
    if (kind === 'podium') return `<div class="fx-stage">${garland(-20, 430, 70)}${garland(1010, 1460, 70)}${gifts('left')}</div>`;
    return '';
  }

  api.ready = fetch('/look.json', { cache: 'no-store' })
    .then(r => r.ok ? r.json() : {})
    .catch(() => ({}))
    .then(look => {
      api.on = !!look.festive;
      if (api.on) {
        document.documentElement.classList.add('festive');
        document.body.insertAdjacentHTML('afterbegin', SPRITE);
        snow(document.body.classList.contains('admin-body'));
      }
      return api.on;
    });

  return api;
})();
