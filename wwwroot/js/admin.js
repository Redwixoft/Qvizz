(() => {
  const { LETTERS, colourOf, esc, $, face, item, optClass, fmt, ICON, move, Countdown, renderTimer, connect, toast } = Qvizz;
  const conn = connect();
  const countdown = new Countdown();
  const stage = $('#stage');
  let state = null;
  let renderedKey = null;
  const seen = new Set();   // players already seated in the lobby (only newcomers animate in)

  // ---------- fit the 1440×900 stage to any screen ----------
  function fit() {
    const s = Math.min(innerWidth / 1440, innerHeight / 900);
    stage.style.transform = `translate(-50%, -50%) scale(${s})`;
  }
  addEventListener('resize', fit);
  fit();

  // ---------- login ----------
  const PW_KEY = 'qvizz.adminPassword';
  async function login(password) {
    const ok = await conn.invoke('AdminLogin', password);
    if (ok) {
      sessionStorage.setItem(PW_KEY, password);
      $('#login').classList.add('hidden');
      $('#app').classList.remove('hidden');
      fit();
      if (state) fitNames();   // the screen may have been drawn while still hidden
    } else {
      sessionStorage.removeItem(PW_KEY);
      $('#loginError').textContent = 'That password is not right. Try again.';
    }
    return ok;
  }
  // The password can be typed before the connection to the server is up: wait for it (up to 10 s) instead of failing.
  async function connected() {
    for (let i = 0; i < 50 && conn.state !== signalR.HubConnectionState.Connected; i++) await new Promise(r => setTimeout(r, 200));
    return conn.state === signalR.HubConnectionState.Connected;
  }
  $('#login').addEventListener('submit', async e => {
    e.preventDefault();
    $('#loginError').textContent = '';
    if (!await connected()) {
      $('#loginError').textContent = 'Can\'t reach the game server yet. Check your connection and try again.';
      return;
    }
    login($('#password').value).catch(err => { $('#loginError').textContent = err.message; });
  });

  // ---------- host controls ----------
  const call = (method, ...args) => conn.invoke(method, ...args).catch(err => toast(err.message));
  document.addEventListener('click', e => {
    const kick = e.target.closest('[data-kick]');
    if (kick) {
      if (confirm(`Remove ${kick.dataset.name} from the game? They can join again under another name.`)) call('KickPlayer', kick.dataset.kick);
      return;
    }
    const b = e.target.closest('[data-act]');
    if (!b) return;
    if (b.dataset.confirm && !confirm(b.dataset.confirm)) return;
    b.disabled = true;
    call(b.dataset.act);
  });
  $('#resetBtn').onclick = () => {
    if (confirm('End this game and open a new lobby? Everyone has to join again. This game is kept in the archive.')) call('NewGame');
  };
  $('#fullBtn').onclick = () => document.fullscreenElement ? document.exitFullscreen() : document.documentElement.requestFullscreen();
  $('#playersBtn').onclick = () => { $('#playersPanel').classList.toggle('hidden'); renderPlayersPanel(); };

  // Host-only list of everyone at the table, with a Remove button (works in any phase).
  function renderPlayersPanel() {
    const panel = $('#playersPanel');
    if (!state || panel.classList.contains('hidden')) { panel.innerHTML = ''; return; }
    const rows = [...state.players].sort((a, b) => a.seat - b.seat).map(p => `
      <li>${face(p)}<span class="nm">${esc(p.nickname)}</span><span class="num">${fmt(p.score)}</span>
        <button type="button" data-kick="${p.id}" data-name="${esc(p.nickname)}">Remove</button></li>`).join('');
    panel.innerHTML = `<h2>Players (${state.players.length})</h2>${rows ? `<ul>${rows}</ul>` : '<p>Nobody has joined yet.</p>'}`;
    Qvizz.fitNames(panel);
  }

  // ---------- rendering ----------
  conn.on('adminState', s => {
    state = s;
    $('#saveNote').classList.toggle('hidden', s.persistent);
    renderPlayersPanel();
    render();
  });

  function render() {
    const s = state;
    const key = `${s.phase}:${s.questionIndex}:${s.paused}`;
    if (key === renderedKey && s.phase !== 'Lobby') {
      const meta = $('#answered');
      if (meta && s.phase === 'Question') meta.innerHTML = lockedText(s);
      return;
    }
    renderedKey = key;
    countdown.stop();
    const asked = s.phase === 'Reading' || s.phase === 'Question' || s.phase === 'Reveal';
    stage.classList.toggle('centered', asked);
    stage.classList.toggle('with-image', asked && !!s.question?.image);
    stage.classList.remove('pic-wide');
    Festive.calm(asked);
    switch (s.phase) {
      case 'Lobby': stage.innerHTML = Festive.stage('lobby') + lobby(s); break;
      case 'Countdown': countdownScreen(s); break;
      case 'Reading': readingScreen(s); break;
      case 'Question': questionScreen(s); break;
      case 'Reveal': revealScreen(s); break;
      case 'Leaderboard': stage.innerHTML = Festive.stage('standings') + standings(s); break;
      case 'Awards': stage.innerHTML = awards(s); break;
      case 'Finished': stage.innerHTML = Festive.stage('podium') + podium(s); break;
    }
    fitNames();
    // Host paused the timer: freeze the clock and say so on the big screen.
    if (s.paused && (s.phase === 'Question' || s.phase === 'Countdown' || s.phase === 'Reading')) {
      countdown.stop();
      const t = $('#timer') || $('#cd');
      if (t) { t.textContent = Math.ceil(s.remainingMs / 1000); t.classList.remove('danger'); }
      stage.insertAdjacentHTML('beforeend', '<div class="paused-overlay"><b>Paused</b><span>The host will continue in a moment</span></div>');
    }
  }

  // ----- lobby: a big QR code in the middle, players scattered around it like pieces on a table -----
  // Seats are fixed spots left and right of the centre card, filled nearest-first, each with a small
  // tilt and nudge (stable per player, so nobody jumps around when others join).
  // Up to 40 players show their drawn faces; above that everyone becomes a compact name tag.
  const CARD = { left: 470, right: 970 };          // the centre column, kept clear
  const AREA = { top: 36, bottom: 864, edge: 34 };
  function seatSpots(tileW, tileH, bottom = AREA.bottom) {
    const spots = [];
    for (const [x0, x1] of [[AREA.edge, CARD.left - 16], [CARD.right + 16, 1440 - AREA.edge]]) {
      const cols = Math.floor((x1 - x0) / tileW), rows = Math.floor((bottom - AREA.top) / tileH);
      const cw = (x1 - x0) / cols, rh = (bottom - AREA.top) / rows;
      for (let c = 0; c < cols; c++) for (let r = 0; r < rows; r++)
        spots.push({ x: x0 + cw * (c + .5), y: AREA.top + rh * (r + .5) });
    }
    // nearest to the card first, so a small group gathers around the QR code
    return spots.sort((a, b) => Math.hypot(a.x - 720, (a.y - 450) * 1.6) - Math.hypot(b.x - 720, (b.y - 450) * 1.6));
  }
  const wobble = seat => { const h = Math.sin(seat * 12.9898) * 43758.5453; return h - Math.floor(h); };

  function lobby(s) {
    const n = s.players.length;
    const newest = n > 1 ? Math.max(...s.players.map(p => p.seat)) : -1;
    const bySeat = [...s.players].sort((a, b) => a.seat - b.seat);
    const cls = p => [p.avatarUrl ? '' : 'drawing', p.connected ? '' : 'offline', p.seat === newest ? 'new' : '', seen.has(p.id) ? '' : 'fresh'].join(' ');
    // a small remove button for the host, shown on hover / keyboard focus
    const kick = p => `<button type="button" class="kick" data-kick="${p.id}" data-name="${esc(p.nickname)}" aria-label="Remove ${esc(p.nickname)}">${ICON.cross('#14213d')}</button>`;

    const tier = n <= 18 ? { w: 150, h: 152, face: 92, name: 22, tags: false }
      : n <= 40 ? { w: 112, h: 116, face: 64, name: 17, tags: false }
      : n <= 68 ? { w: 196, h: 48, name: 20, tags: true }
      : { w: 140, h: 40, name: 16, tags: true };
    // the Christmas ticker takes the bottom 58px, so seats stop above it
    const spots = seatSpots(tier.w, tier.h, Festive.on ? 828 : AREA.bottom);
    const pieces = bySeat.map((p, i) => {
      const spot = spots[i % spots.length], j = wobble(p.seat);
      const style = `left:${spot.x + (j - .5) * tier.w * .18}px;top:${spot.y + (wobble(p.seat + 99) - .5) * tier.h * .18}px;` +
        `--tilt:${(j - .5) * (tier.tags ? 6 : 10)}deg;--pc:${colourOf(p)};--face:${tier.face}px;--name:${tier.name}px;width:${tier.w - 14}px`;
      return tier.tags
        ? `<div class="piece tag ${cls(p)}" style="${style}"><i></i><span>${esc(p.nickname)}</span>${kick(p)}</div>`
        : `<div class="piece slot ${cls(p)}" style="${style};min-height:${tier.h - 14}px">${face(p)}<b>${esc(p.nickname)}</b>${p.avatarUrl ? '' : '<small>drawing…</small>'}${kick(p)}</div>`;
    }).join('');
    s.players.forEach(p => seen.add(p.id));

    const joinUrl = s.joinUrl || `${location.origin}/game`;
    const shown = joinUrl.replace(/^https?:\/\//, '');
    return `<div class="lobby">
      <div class="join-center">
        <div class="mark">Qvizz<i>.</i></div>
        <h1 class="quiz-name">${esc(s.quizTitle)}</h1>
        <div class="join">
          <h2>Scan to join</h2>
          <div class="qr">${qrSvg(joinUrl)}</div>
          <div class="url ${shown.length > 30 ? 'long' : ''}">${esc(shown).replace(/([./])/g, '$1<wbr>')}</div>
          <p class="steps">Pick a nickname, draw your face, watch this screen.</p>
        </div>
        <div class="row-actions">
          <div class="count"><b>${n}</b>player${n === 1 ? '' : 's'} at the table</div>
          <button class="btn start plastic" data-act="StartQuiz" ${n ? '' : 'disabled'}>Start the game</button>
        </div>
      </div>
      ${pieces}
    </div>`;
  }

  // Player names are always shown in full on every screen (shrunk, then wrapped if needed: Qvizz.fitNames).
  const fitNames = () => Qvizz.fitNames(stage);
  // Measurements depend on the web font: fit again once it has loaded.
  document.fonts?.ready.then(() => { if (state) fitNames(); });

  // QR code for the join address (qrcode-generator, loaded in admin.html).
  function qrSvg(url) {
    if (typeof qrcode !== 'function') return '';
    const qr = qrcode(0, 'M');
    qr.addData(url);
    qr.make();
    return qr.createSvgTag({ cellSize: 6, margin: 2, scalable: true, alt: `QR code for ${url}` });
  }

  // ----- countdown -----
  function countdownScreen(s) {
    stage.innerHTML = `<div class="lobby">
      <div class="cd-title">The fun begins now!</div>
      <div class="deal"><div class="c"></div><div class="c"></div><div class="top"><b id="cd"></b></div></div>
      <div class="cd-sub">${s.questionCount} questions · ${s.players.length} players · phones ready</div>
    </div>`;
    countdown.start(s.remainingMs, secs => {
      const el = $('#cd');
      if (el && el.textContent !== String(secs)) { el.textContent = secs; el.classList.remove('flip'); void el.offsetWidth; el.classList.add('flip'); }
    });
  }

  // Question text size by length; next to a picture the column is narrower, so one step smaller.
  const qSize = (text, pic) => pic
    ? (text.length > 90 ? 34 : text.length > 55 ? 40 : 46)
    : (text.length > 110 ? 44 : text.length > 70 ? 52 : 60);

  // Questions with a picture: the picture big on the left, like a photo card laid on the board,
  // with the question and answers stacked on the right. The picture is shown whole (never cropped);
  // a blurred copy of it fills any spare space in the frame so portrait and landscape both look full.
  // Landscape pictures get the stacked arrangement: question on top, picture in the middle, answers (2×2) at the bottom.
  window.picShape = img => {
    // Number questions keep the side-by-side layout: their answer chart needs the height.
    const stage = img.closest('.stage');
    const wide = img.naturalWidth && img.naturalHeight && img.naturalWidth / img.naturalHeight > 1.2 && !stage?.querySelector('.chart');
    stage?.classList.toggle('pic-wide', !!wide);
  };

  function layout(s, content) {
    const img = s.question?.image;
    if (!img) return content;
    const src = esc(img);
    return `<figure class="pic">
        <div class="frame"><div class="blur" style="background-image:url('${src}')"></div><img src="${src}" alt="" onload="picShape(this)"></div>
      </figure>
      <div class="q-side">${content}</div>`;
  }

  function lockedText(s) {
    const t = s.question?.type;
    const verb = t === 'Grouping' || t === 'Ordering' ? 'arranging' : t === 'ClosestNumber' ? 'guesses in' : 'locked in';
    return `<b>${s.answeredCount}</b> of ${s.players.filter(p => p.connected).length} ${verb}`;
  }

  function card(s, side, meta) {
    const q = s.question;
    return `<section class="qcard" style="--qsize:${qSize(q.text, !!q.image)}px">
      <h1>${esc(q.text)}</h1>
      <div class="meta" id="answered">${meta}</div>
      <div class="clock">${side}</div>
    </section>`;
  }

  // ----- reading: the question and picture alone for a few seconds, the answers come after -----
  // The timer token counts down the reading time; the space where the answers will land waits for them.
  function readingScreen(s) {
    const clock = `<div class="tok" id="timer"></div><span>Q ${s.questionIndex + 1} / ${s.questionCount}</span>`;
    const secs = Math.max(0, s.remainingMs) / 1000;
    const waiting = `<div class="answers-soon"><p>Read the question. The answers are coming…</p><i class="drain" style="animation-duration:${secs}s"></i></div>`;
    stage.innerHTML = layout(s, card(s, clock, 'Answers in a moment') + waiting);
    countdown.start(s.remainingMs, n => { const t = $('#timer'); if (t) t.textContent = n; });
  }

  // ----- ordering: the items beside a rail whose two ends say what top and bottom mean -----
  // While the question runs the items are in the shuffled starting order (no place numbers: it isn't an answer);
  // on the reveal they stand in the right order, numbered, each with how many players put it in its place.
  function ladder(q, items, r) {
    const rail = q.topLabel || q.bottomLabel
      ? `<div class="rail"><b>${esc(q.topLabel || '')}</b><i aria-hidden="true"></i><b>${esc(q.bottomLabel || '')}</b></div>` : '';
    const rows = items.map((it, i) => `
      <div class="it ${r ? 'ok' : ''}">${r ? `<span class="n">${i + 1}</span>` : ''}${item(it)}${r && r.answeredPlayers
        ? `<em class="hits" title="players who put it here"><tt>${r.placedRight[i]}</tt>/${r.answeredPlayers}</em>` : ''}</div>`).join('');
    return `<div class="ladder ${rail ? '' : 'no-rail'}">${rail}<div class="rungs" style="--n:${items.length}">${rows}</div></div>`;
  }

  // ----- question -----
  function questionScreen(s) {
    const q = s.question;
    const clock = `<div class="tok" id="timer"></div><span>Q ${s.questionIndex + 1} / ${s.questionCount}</span>`;
    let body = '';
    if (q.type === 'MultipleChoice') {
      body = `<div class="book">${q.options.map((o, i) => `
        <div class="row ${optClass(q.type, i)} plastic"><b class="tok">${LETTERS[i]}</b><span class="txt">${esc(o)}</span></div>`).join('')}</div>`;
    } else if (q.type === 'TrueFalse') {
      body = `<div class="tf">${q.options.map((o, i) => `
        <div class="row ${optClass(q.type, i)} plastic"><b class="tok">${LETTERS[i]}</b><span class="txt">${esc(o)}</span></div>`).join('')}</div>`;
    } else if (q.type === 'Grouping') {
      body = `<div class="cols">${q.groupLeft.map((l, i) => `
        <div class="it left"><span class="n">${i + 1}</span>${item(l)}</div><div class="it">${item(q.groupRight[i])}</div>`).join('')}</div>`;
    } else if (q.type === 'Ordering') {
      body = ladder(q, q.orderItems);
    } else {
      body = `<div class="chart"><h2>Type your guess on your phone</h2>
        <p>You can change it until the time runs out. The closest guess takes 100 points, then 90, 80…</p></div>`;
    }
    // No standings ticker while a question is running: players focus on the question.
    // Scores come back on the reveal and the standings screen.
    stage.innerHTML = layout(s, card(s, clock, lockedText(s)) + body);
    countdown.start(s.remainingMs, secs => renderTimer($('#timer'), secs));
  }

  // ----- reveal -----
  function nextButton(s) {
    const last = s.questionIndex === s.questionCount - 1;
    return `<button class="btn plastic" data-act="Next">${last ? 'Final results' : 'Standings'}</button>`;
  }

  function revealScreen(s) {
    const q = s.question, r = s.reveal;
    const firstNames = r.firstCorrect.map(p => esc(p.nickname)).join(', ');
    let body = '', meta = '';

    if (q.type === 'MultipleChoice' || q.type === 'TrueFalse') {
      meta = !r.answeredPlayers ? 'Nobody answered this one' : `<b>${r.correctPlayers}</b> of ${r.answeredPlayers} got it right${firstNames ? ` · first in: ${firstNames}` : ''}`;
      const total = Math.max(1, r.answeredPlayers);
      const rows = q.options.map((o, i) => {
        const win = i === r.correctIndex;
        const faces = win && r.firstCorrect.length
          ? `<span class="who">${r.firstCorrect.map(p => face(p)).join('')}${r.correctPlayers > r.firstCorrect.length ? `<em>+${r.correctPlayers - r.firstCorrect.length}</em>` : ''}</span>` : '';
        const tokColour = getComputedStyle(document.documentElement).getPropertyValue(q.type === 'TrueFalse' ? (i ? '--red' : '--blue') : ['--red', '--green', '--blue', '--ink'][i]);
        return `<div class="row ${optClass(q.type, i)} plastic ${win ? 'win' : 'out'}" data-v="${(r.optionCounts[i] / total) * 100}">
          <b class="tok">${win ? ICON.check(tokColour.trim()) : LETTERS[i]}</b><span class="txt">${esc(o)}</span>
          <span class="chips">${faces}<b>${r.optionCounts[i]}</b></span><i class="vol"></i></div>`;
      }).join('');
      body = `<div class="${q.type === 'TrueFalse' ? 'tf' : 'book'}">${rows}</div>`;
    } else if (q.type === 'Grouping') {
      meta = !r.answeredPlayers ? 'Nobody answered this one' : `<b>${r.correctPlayers}</b> of ${r.answeredPlayers} matched every pair${firstNames ? ` · first: ${firstNames}` : ''}`;
      body = `<div class="cols">${r.correctPairs.map((p, i) => `
        <div class="it left ok"><span class="n">${i + 1}</span>${item(p.left)}</div><div class="it">${item(p.right)}</div>`).join('')}</div>`;
    } else if (q.type === 'Ordering') {
      meta = !r.answeredPlayers ? 'Nobody answered this one' : `<b>${r.correctPlayers}</b> of ${r.answeredPlayers} got the whole order right${firstNames ? ` · first: ${firstNames}` : ''}`;
      body = ladder(q, r.correctOrder, r);
    } else {
      const best = r.closestGuesses[0];
      const winners = r.closestGuesses.filter(g => best && g.points === best.points).map(g => esc(g.nickname));
      meta = best ? `Closest: <strong>${winners.join(', ')}</strong> guessed ${fmt(best.guess)} (${offText(best.guess, r.correctNumber)}) · +${best.points}` : 'Nobody guessed this time';
      body = numberReveal(q, r);
    }
    stage.innerHTML = layout(s, card(s, nextButton(s), meta) + body);
    // grow the vote-volume bars once the rows are on screen
    requestAnimationFrame(() => requestAnimationFrame(() =>
      stage.querySelectorAll('.row[data-v] .vol').forEach(v => v.style.width = v.parentElement.dataset.v + '%')));
  }

  // How far a guess is from the right number: "3 over", "2.5 under" or "spot on".
  const offBy = (guess, correct) => Math.round((guess - correct) * 1e6) / 1e6;
  const offText = (guess, correct) => {
    const d = offBy(guess, correct);
    return d === 0 ? 'spot on' : `${fmt(Math.abs(d))} ${d > 0 ? 'over' : 'under'}`;
  };
  const offTag = (guess, correct) => {
    const d = offBy(guess, correct);
    return `<i class="off ${d === 0 ? 'exact' : ''}">${d === 0 ? 'spot on' : `<tt>${fmt(Math.abs(d))}</tt> ${d > 0 ? 'over' : 'under'}`}</i>`;
  };

  function numberReveal(q, r) {
    const values = [...r.allGuesses, r.correctNumber];
    let lo = Math.min(0, ...values), hi = Math.max(...values);
    if (hi === lo) hi = lo + 1;
    const step = niceStep((hi - lo) / 4);
    lo = Math.floor(lo / step) * step; hi = Math.ceil(hi / step) * step;
    const at = v => ((v - lo) / (hi - lo)) * 100;
    const labels = [];
    for (let v = lo; v <= hi + step / 2; v += step) labels.push(`<span class="lbl" style="left:${at(v)}%">${fmt(v)}</span>`);
    const best = r.closestGuesses[0];
    const stack = {};
    const dots = r.allGuesses.filter((g, i) => !(best && g === best.guess && i === r.allGuesses.indexOf(best.guess))).map(g => {
      const k = Math.round(at(g) / 1.5); stack[k] = (stack[k] || 0) + 1;
      return `<i class="dot" style="left:${at(g)}%;bottom:${40 + stack[k] * 16}px"></i>`;
    }).join('');
    // the winner's face sits on top of any pile of dots at its spot
    const pile = best ? Math.min(7, Math.max(0, ...[-1, 0, 1].map(o => stack[Math.round(at(best.guess) / 1.5) + o] || 0))) : 0;
    return `<div class="chart">
      <div class="answer-big"><b>${fmt(r.correctNumber)}</b><span>${esc(q.unit || '')}</span></div>
      <ol class="closest">${r.closestGuesses.map(g => `<li>${face({ ...g })}<b>${esc(g.nickname)}</b><span>${fmt(g.guess)}</span>${offTag(g.guess, r.correctNumber)}<em>+${g.points}</em></li>`).join('')}</ol>
      <div class="axis"><div class="line"></div>${labels.join('')}
        <div class="target" style="left:${at(r.correctNumber)}%"></div>${dots}
        ${best ? `<div class="pin" style="left:${at(best.guess)}%;bottom:${56 + pile * 16}px">${face({ ...best })}<span>${fmt(best.guess)}</span></div>` : ''}
      </div></div>`;
  }
  function niceStep(raw) {
    const p = Math.pow(10, Math.floor(Math.log10(raw || 1)));
    return [1, 2, 2.5, 5, 10].map(m => m * p).find(v => v >= raw) || 10 * p;
  }

  // ----- standings -----
  // 3+ questions kept in a row: a small fire and the +5 streak bonus next to the name.
  const streak = p => p.streak >= 3
    ? `<span class="streak" title="${p.streak} in a row">🔥<small>+${p.streakBonus}</small></span>` : '';
  function standings(s) {
    const lines = s.leaderboard.map((p, i) => {
      const m = move(p);
      return `<div class="line ${i === 0 ? 'first' : ''}" style="animation-delay:${i * 80}ms">
        <span class="rank">${p.rank}</span><span class="move ${m.cls}">${m.icon}${m.n}</span>${face(p)}
        <span class="nm"><span class="nm-text">${esc(p.nickname)}</span>${streak(p)}</span><span class="chip ${p.gain - p.bonus - p.streakBonus ? '' : 'zero'}">+${p.gain - p.bonus - p.streakBonus}</span><span class="chip bonus ${p.bonus ? '' : 'zero'}">+${p.bonus}</span><span class="total">${fmt(p.score)}</span></div>`;
    }).join('');
    // trade of the round: biggest climb, then biggest gain
    const climber = [...s.players].sort((a, b) => ((b.prevRank - b.rank) - (a.prevRank - a.rank)) || (b.gain - a.gain))[0];
    const climb = climber ? climber.prevRank - climber.rank : 0;
    const played = s.questionIndex + 1, left = s.questionCount - played;
    return `<div class="standings">
      <h1>Standings<span>after Q ${played} / ${s.questionCount}</span></h1>
      <div class="sheet"><div class="head"><span>Rank</span><span></span><span></span><span>Player</span><span class="r">Answer</span><span class="r">Speed bonus</span><span class="r">Total</span></div>${lines}</div>
      <div class="side">
        ${climber ? `<div class="trade"><h2>Trade of the round</h2><div class="p">${face(climber)}<div><b>${esc(climber.nickname)}</b>
          <small>${climb > 0 ? `climbed ${climb} place${climb === 1 ? '' : 's'} with +${climber.gain}` : `+${climber.gain} this round`}</small></div></div></div>` : ''}
        <div class="pile"><div class="c"></div><div class="c"></div><div class="c"><div><b>${played}</b><span>card${played === 1 ? '' : 's'} played<br>${left} to go</span></div></div></div>
        <button class="btn next plastic" data-act="Next">Next question</button>
      </div>
    </div>`;
  }

  // ----- podium -----
  function podium(s) {
    const [first, second, third] = s.leaderboard;
    // Medal, colour and block height follow the rank, so tied players share gold (or silver).
    const look = {
      1: { medal: '#ffd36b', block: '#f2b705', h: 300 },
      2: { medal: '#eef1f6', block: '#c9ced8', h: 220 },
      3: { medal: '#e9a56c', block: '#d08a4e', h: 170 }
    };
    const place = (p, cls) => {
      if (!p) return '';
      const l = look[Math.min(p.rank, 3)];
      return `<div class="place ${cls}">${face(p)}<span class="nm">${esc(p.nickname)}</span>
      <div class="block" style="height:${l.h}px;background:${l.block}">${ICON.medal(l.medal)}<span class="pts">${fmt(p.score)}</span></div></div>`;
    };
    const champions = s.leaderboard.filter(p => p.rank === 1);
    const title = !first ? 'Game over'
      : champions.length === 1 ? `${esc(first.nickname)} takes the table!`
      : champions.length === 2 ? `${esc(champions[0].nickname)} and ${esc(champions[1].nickname)} share the table!`
      : `A ${champions.length}-way tie for the table!`;
    const rest = s.players.slice(3, 7).map(p =>
      `<span class="pos"><span class="rk">${p.rank}</span>${face(p)}${esc(p.nickname)} <span class="num">${fmt(p.score)}</span></span>`).join('');
    return `<div class="lobby">
      <div class="podium-title">${title}</div>
      <div class="podium">${place(second, 'p2')}${place(first, 'p1')}${place(third, 'p3')}</div>
      <div class="rest">${rest}</div>
      <button class="btn light plastic newgame" data-act="NewGame"
        data-confirm="Start a new game? This one is saved in the archive and everyone joins again.">New game</button>
    </div>`;
  }

  // ----- awards: one trophy card per award, revealed one after another -----
  function awards(s) {
    const cards = s.awards.map((a, i) => `
      <article class="award" style="animation-delay:${300 + i * 450}ms">
        <h2>${esc(a.title)}</h2>
        <p class="blurb">${esc(a.blurb)}</p>
        ${face(a.winner)}
        <b class="nm">${esc(a.winner.nickname)}</b>
        <span class="stat">${esc(a.stat)}</span>
      </article>`).join('');
    return `<div class="lobby awards-screen">
      <h1 class="awards-title">And the awards go to…</h1>
      <div class="award-grid ${s.awards.length <= 4 ? 'few' : ''}">${cards}</div>
      <button class="btn plastic next-podium" data-act="Next">Final podium</button>
    </div>`;
  }

  // ---------- connection ----------
  conn.onreconnected(() => {
    renderedKey = null;
    const pw = sessionStorage.getItem(PW_KEY);
    if (pw) login(pw);
  });
  conn.onclose(() => setTimeout(start, 3000));
  async function start() {
    try {
      await conn.start();
      const pw = sessionStorage.getItem(PW_KEY);
      if (pw) await login(pw);
    } catch {
      setTimeout(start, 3000);
    }
  }
  start();
  // The Christmas look loads alongside the connection; redraw once it is known so hats and garland appear.
  Festive.ready.then(on => { if (on && state) { renderedKey = null; render(); } });
})();
