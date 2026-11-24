// Shared helpers for the presenter and player pages.
const Qvizz = (() => {
  const LETTERS = ['A', 'B', 'C', 'D', 'E', 'F'];

  // Each player keeps one pawn colour everywhere, picked by join order.
  const PLAYER_COLOURS = ['#d93a28', '#23843a', '#0f78b5', '#f2b705', '#864cbf', '#0aa3a3', '#e86fa6', '#7a4a1d', '#3a3a3a', '#ff8c1a'];
  const colourOf = p => PLAYER_COLOURS[((p?.seat || 1) - 1) % PLAYER_COLOURS.length];

  const esc = s => String(s ?? '').replace(/[&<>"']/g, c =>
    ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

  const $ = sel => document.querySelector(sel);

  /** A player's drawn face, ringed in their colour (or their initial until they've drawn). */
  function face(p, cls = '') {
    const style = `style="--pc:${colourOf(p)}"`;
    const el = p.avatarUrl
      ? `<img class="face ${cls}" ${style} src="${esc(p.avatarUrl)}" alt="">`
      : `<span class="face blank ${cls}" ${style}><b>${esc((p.nickname || '?')[0].toUpperCase())}</b></span>`;
    // Christmas look: every face wears a Santa hat, tilted a little differently per player.
    return typeof Festive !== 'undefined' && Festive.on ?`<span class="hatted">${el}${Festive.hat(p.seat || 1)}</span>` : el;
  }

  function item(it) {
    if (!it) return '';
    if (it.image) return `<img src="${esc(it.image)}" alt="${esc(it.text || '')}">` + (it.text ? `<span>${esc(it.text)}</span>` : '');
    return esc(it.text);
  }

  /** Colour class for an answer piece: A-D red/green/blue/yellow; true/false blue/red. */
  const optClass = (type, i) => type === 'TrueFalse' ? `tf-${i}` : `opt-${i}`;

  const fmt = n => Number(n).toLocaleString('en-US', { maximumFractionDigits: 2 }).replace(/,/g, ' ');

  const ICON = {
    check: colour => `<svg viewBox="0 0 20 20" aria-hidden="true"><path d="M4 10.5l4 4 8-9" fill="none" stroke="${colour}" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"/></svg>`,
    cross: colour => `<svg viewBox="0 0 20 20" aria-hidden="true"><path d="M5 5l10 10M15 5L5 15" fill="none" stroke="${colour}" stroke-width="3" stroke-linecap="round"/></svg>`,
    up: '<svg viewBox="0 0 20 20" aria-hidden="true"><path d="M10 3l7 11H3z" fill="currentColor"/></svg>',
    down: '<svg viewBox="0 0 20 20" aria-hidden="true"><path d="M10 17L3 6h14z" fill="currentColor"/></svg>',
    same: '<svg viewBox="0 0 20 20" aria-hidden="true"><rect x="3" y="8" width="14" height="4" rx="2" fill="currentColor"/></svg>',
    medal: (fill, label = '') => `<svg class="medal" viewBox="0 0 92 110" aria-hidden="true">
      <path d="M22 0h20l12 34H34zM50 0h20L58 34H38z" fill="#1d3e93"/>
      <circle cx="46" cy="70" r="36" fill="${fill}" stroke="#14213d" stroke-width="5"/>
      ${label ? `<text x="46" y="82" text-anchor="middle" font-family="JetBrains Mono, monospace" font-weight="800" font-size="34" fill="#14213d">${label}</text>`
              : '<circle cx="46" cy="70" r="24" fill="none" stroke="#14213d" stroke-width="3" opacity=".35"/>'}
    </svg>`
  };

  /** Rank movement since the previous question. */
  function move(p) {
    const d = (p.prevRank || p.rank) - p.rank;
    if (d > 0) return { cls: 'up', icon: ICON.up, n: d };
    if (d < 0) return { cls: 'down', icon: ICON.down, n: -d };
    return { cls: 'same', icon: ICON.same, n: '' };
  }

  // Counts down locally from the server-provided remaining time.
  class Countdown {
    constructor() { this.handle = null; }
    start(remainingMs, onTick) {
      this.stop();
      const deadline = Date.now() + remainingMs;
      const tick = () => {
        const ms = Math.max(0, deadline - Date.now());
        onTick(Math.ceil(ms / 1000), ms);
        if (ms <= 0) this.stop();
      };
      tick();
      this.handle = setInterval(tick, 100);
    }
    stop() { if (this.handle) clearInterval(this.handle); this.handle = null; }
  }

  /** Timer token: the last 5 seconds grow and turn red. */
  function renderTimer(el, secs) {
    if (!el) return;
    el.textContent = secs;
    el.classList.toggle('danger', secs <= 5 && secs > 0);
  }

  function connect() {
    return new signalR.HubConnectionBuilder()
      .withUrl('/hub')
      .withAutomaticReconnect([0, 1000, 2000, 3000, 5000, 5000, 10000, 10000, 10000])
      .build();
  }

  function toast(msg) {
    const t = document.createElement('div');
    t.className = 'toast';
    t.setAttribute('role', 'status');
    t.textContent = msg;
    document.body.appendChild(t);
    setTimeout(() => t.remove(), 2600);
  }

  /**
   * Player names are never cut off. A name that doesn't fit its box gets a smaller font, only as much as needed;
   * one that still doesn't fit at 65% of its size wraps between words instead. A single word too long even for its
   * own line shrinks a little more (to 45%), and only then may break inside the word.
   * The elements must be single-line with overflow hidden (see "player names" in app.css) so overflow can be measured.
   */
  const NAMES = '.piece.slot > b, .piece.tag > span, .sheet .nm-text, .place .nm, .award .nm, .trade .p b, .closest b, .players-panel .nm, .me .n, .remote-players .nm';
  const fitted = new Set();   // every screen area that shows names, re-fitted when fonts load or the window changes
  let refitQueued = false;
  function refitAll() {
    if (refitQueued) return;
    refitQueued = true;
    requestAnimationFrame(() => { refitQueued = false; fitted.forEach(r => r.isConnected ? fitNames(r) : fitted.delete(r)); });
  }
  // Names are measured in the font they are shown in. The web font usually arrives after the first screen is drawn
  // (and is narrower or wider than the fallback), so measure again every time a font finishes loading.
  document.fonts?.addEventListener('loadingdone', refitAll);
  addEventListener('resize', refitAll);

  function fitNames(root) {
    if (!root) return;
    fitted.add(root);
    if (document.fonts?.status === 'loading') document.fonts.ready.then(refitAll);
    if (!root.getClientRects().length) return;   // hidden: nothing to measure yet
    const over = el => el.scrollWidth > el.clientWidth;
    for (const el of root.querySelectorAll(NAMES)) {
      el.style.fontSize = '';
      el.classList.remove('nm-wrap', 'nm-break');
      if (!over(el)) continue;
      const base = parseFloat(getComputedStyle(el).fontSize);
      let min = Math.round(base * .65);
      let size = Math.max(min, Math.floor(base * el.clientWidth / el.scrollWidth));
      el.style.fontSize = size + 'px';
      while (over(el) && size > min) el.style.fontSize = --size + 'px';
      if (!over(el)) continue;
      el.classList.add('nm-wrap');                   // wrap between words
      min = Math.round(base * .45);
      while (over(el) && size > min) el.style.fontSize = --size + 'px';
      if (over(el)) el.classList.add('nm-break');   // one very long word: break inside it rather than cut it
    }
  }

  return { LETTERS, PLAYER_COLOURS, colourOf, esc, $, face, item, optClass, fmt, ICON, move, Countdown, renderTimer, connect, toast, fitNames };
})();
