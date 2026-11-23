(() => {
  const { LETTERS, esc, $, face, item, optClass, fmt, ICON, move, Countdown, renderTimer, connect, toast } = Qvizz;
  const ID_KEY = 'qvizz.playerId';
  const conn = connect();
  const countdown = new Countdown();
  const app = $('#app');

  let state = null;        // last PlayerView from the server
  let drawing = false;     // local "draw your face" screen
  let renderedKey = null;
  let grouping = null;     // { qi, order: [...], selected }

  // ================= rendering =================
  conn.on('playerState', s => {
    state = s;
    if (s.phase !== 'Lobby') drawing = false;
    else if (!s.me.avatarUrl && (renderedKey === null || renderedKey === 'join')) drawing = true; // just joined: draw a face
    render();
    syncPause(s);
  });

  conn.on('reset', () => {
    localStorage.removeItem(ID_KEY);
    state = null; drawing = false; renderedKey = null;
    showJoin('A new game is starting. Take a seat again!');
  });

  conn.on('kicked', () => {
    localStorage.removeItem(ID_KEY);
    state = null; drawing = false; renderedKey = null;
    showJoin('The host removed you from the game. You can join again with a different nickname.');
  });

  // ---------- keep the screen awake while the game page is open ----------
  // Uses the Screen Wake Lock API (needs HTTPS; supported by current Chrome, Edge, Safari 16.4+, Samsung Internet).
  // The browser drops the lock when the tab is hidden, so take it again whenever the page comes back.
  let wakeLock = null;
  async function keepAwake() {
    if (!('wakeLock' in navigator) || document.visibilityState !== 'visible' || wakeLock) return;
    try {
      wakeLock = await navigator.wakeLock.request('screen');
      wakeLock.addEventListener('release', () => { wakeLock = null; });
    } catch { /* not allowed right now (e.g. battery saver) — the game still works */ }
  }
  document.addEventListener('visibilitychange', keepAwake);
  document.addEventListener('pointerdown', keepAwake);   // some browsers only grant it after a tap
  keepAwake();

  function render() {
    const s = state;
    if (!s) return;
    // During a question the screen is built once; later pushes only refresh the "saved" status,
    // so typing / arranging is never interrupted.
    const key = drawing ? 'draw'
      : s.phase === 'Question' ? `Question:${s.questionIndex}`
      : `${s.phase}:${s.questionIndex}:${s.me.avatarUrl}:${s.playerCount}`;
    if (key === renderedKey) {
      if (s.phase === 'Question') updateQuestion(s);
      return;
    }
    renderedKey = key;
    countdown.stop();

    if (drawing) return showDraw();
    switch (s.phase) {
      case 'Lobby': return showLobby(s);
      case 'Countdown': return showCountdown(s);
      case 'Reading': return showReading(s);
      case 'Question': return showQuestion(s);
      case 'Reveal':
      case 'Leaderboard': return showResult(s);
      case 'Awards': return showAwards(s);
      case 'Finished': return showFinished(s);
    }
  }

  const meChip = s => `<div class="me">${face(s.me)}<span class="n">${esc(s.me.nickname)}${s.me.streak >= 3 ? ' <span class="fire" title="On a streak">🔥</span>' : ''}</span><span class="s">${fmt(s.me.score)}</span><span class="chip">#${s.me.rank}</span></div>`;

  // ---------- join ----------
  function showJoin(message) {
    renderedKey = 'join';
    countdown.stop();
    app.innerHTML = `
      <div class="center">
        <div class="p-mark">Qvizz<i>.</i></div>
        <p class="soft">${esc(message || 'Take a seat at the table')}</p>
        <form class="panel" id="joinForm">
          <label for="nick">Your nickname</label>
          <input id="nick" maxlength="20" autocomplete="nickname" autocapitalize="words" enterkeyhint="go" required>
          <div class="error" id="joinError" role="alert"></div>
          <button class="btn plastic" type="submit">Join the game</button>
        </form>
      </div>`;
    $('#joinForm').onsubmit = async e => {
      e.preventDefault();
      const btn = e.submitter; if (btn) btn.disabled = true;
      const res = await conn.invoke('Join', $('#nick').value).catch(err => ({ error: err.message }));
      if (btn) btn.disabled = false;
      if (res.error) { $('#joinError').textContent = res.error; return; }
      localStorage.setItem(ID_KEY, res.playerId);
    };
  }

  // ---------- lobby ----------
  function showLobby(s) {
    app.innerHTML = `
      <div class="center">
        ${face(s.me, 'pawn')}
        <h2 class="on-board" style="font-size:40px">${esc(s.me.nickname)}</h2>
        <p class="soft">You're in. Keep an eye on the big screen; the game starts any moment.</p>
        <button class="btn light plastic" id="redraw" type="button">${s.me.avatarUrl ? 'Redraw my face' : 'Draw my face'}</button>
      </div>`;
    $('#redraw').onclick = () => { drawing = true; render(); };
  }

  // ---------- face drawing ----------
  function showDraw() {
    app.innerHTML = `
      <h2 class="on-board" style="font-size:28px;text-align:center;margin-top:4px">Draw your face</h2>
      <p class="soft" style="text-align:center;align-self:center;margin-top:-6px">Eyes, nose, mouth, hair, glasses… go wild.</p>
      <div class="draw-wrap"><canvas id="canvas" width="300" height="300" aria-label="Drawing area"></canvas></div>
      <div class="pal" id="palette"></div>
      <div class="tools">
        <button class="btn light plastic sel" id="thin" type="button">Thin</button>
        <button class="btn light plastic" id="thick" type="button">Thick</button>
        <button class="btn light plastic" id="undo" type="button">Undo</button>
        <button class="btn light plastic" id="clear" type="button">Clear</button>
      </div>
      <button class="btn plastic" id="done" type="button" style="margin-top:auto">That's me!</button>`;

    const canvas = $('#canvas'), ctx = canvas.getContext('2d');
    const colours = ['#222222', '#7a4a1d', '#d93a28', '#0f78b5', '#23843a', '#f2b705', '#ff7eb6', '#ffffff'];
    let colour = colours[0], size = 5, strokes = [], current = null;

    const palette = $('#palette');
    palette.innerHTML = colours.map(c => `<button class="sw" type="button" style="background:${c}" data-c="${c}" aria-label="Colour ${c}"></button>`).join('');
    const markColour = () => palette.querySelectorAll('.sw').forEach(b => b.classList.toggle('on', b.dataset.c === colour));
    palette.onclick = e => { if (e.target.dataset.c) { colour = e.target.dataset.c; markColour(); } };
    markColour();
    const setSize = (n, id) => { size = n; ['thin', 'thick'].forEach(x => $('#' + x).classList.toggle('sel', x === id)); };

    // a white face on white: just the outline to draw into
    function faceOutline() {
      ctx.fillStyle = '#ffffff'; ctx.fillRect(0, 0, 300, 300);
      ctx.beginPath(); ctx.ellipse(150, 155, 105, 125, 0, 0, Math.PI * 2);
      ctx.lineWidth = 4; ctx.strokeStyle = '#333'; ctx.stroke();
    }
    function redraw() {
      faceOutline();
      ctx.lineCap = 'round'; ctx.lineJoin = 'round';
      for (const st of strokes) {
        ctx.strokeStyle = st.colour; ctx.lineWidth = st.size;
        ctx.beginPath();
        st.points.forEach(([x, y], i) => i ? ctx.lineTo(x, y) : ctx.moveTo(x, y));
        if (st.points.length === 1) ctx.lineTo(st.points[0][0] + 0.1, st.points[0][1]);
        ctx.stroke();
      }
    }
    const pos = e => {
      const r = canvas.getBoundingClientRect();
      return [(e.clientX - r.left) * 300 / r.width, (e.clientY - r.top) * 300 / r.height];
    };
    canvas.onpointerdown = e => {
      try { canvas.setPointerCapture(e.pointerId); } catch { /* synthetic events */ }
      current = { colour, size, points: [pos(e)] };
      strokes.push(current); redraw();
    };
    canvas.onpointermove = e => { if (current) { current.points.push(pos(e)); redraw(); } };
    canvas.onpointerup = canvas.onpointercancel = () => current = null;

    $('#thin').onclick = () => setSize(5, 'thin');
    $('#thick').onclick = () => setSize(12, 'thick');
    $('#undo').onclick = () => { strokes.pop(); redraw(); };
    $('#clear').onclick = () => { strokes = []; redraw(); };
    $('#done').onclick = async () => {
      $('#done').disabled = true;
      const err = await conn.invoke('SetAvatar', canvas.toDataURL('image/png')).catch(e => e.message);
      if (err) { toast(err); $('#done').disabled = false; return; }
      drawing = false;
      render();
    };
    redraw();
  }

  // ---------- countdown ----------
  function showCountdown(s) {
    app.innerHTML = `${meChip(s)}<div class="center"><h2 class="on-board" style="font-size:40px">Get ready!</h2><div id="cd" class="num on-board" style="font-size:140px;line-height:1"></div></div>`;
    countdown.start(s.remainingMs, secs => $('#cd').textContent = secs);
  }

  // ---------- reading: the question alone, answers follow on their own ----------
  function showReading(s) {
    app.innerHTML = `<div class="panel read"><small>Question ${s.questionIndex + 1} of ${s.questionCount}</small><h2>${esc(s.question.text)}</h2></div>
      <div class="center"><p class="soft">Answers appear in</p><div id="cd" class="num on-board read-cd"></div></div>${meChip(s)}`;
    countdown.start(s.remainingMs, secs => { const t = $('#cd'); if (t) t.textContent = secs; });
  }

  // ---------- question ----------
  const topbar = s => `<div class="pq"><p>${esc(s.question.text)}</p><b class="tok" id="timer"></b></div>`;

  async function send(method, ...args) {
    const err = await conn.invoke(method, ...args).catch(e => e.message);
    if (err) toast(err);
  }

  // Answers can be changed until the time runs out. Matching and number answers are
  // saved on every change, so whatever is on screen at the end counts.
  function showQuestion(s) {
    const q = s.question;
    let body = '';
    if (q.type === 'MultipleChoice' || q.type === 'TrueFalse') {
      body = `<div class="answers ${q.type === 'TrueFalse' ? 'tf' : ''}">${q.options.map((o, i) => `
        <button class="abtn ${optClass(q.type, i)} plastic" data-i="${i}" type="button"><b class="tok">${LETTERS[i]}</b><span class="t">${esc(o)}</span></button>`).join('')}</div>`;
    } else if (q.type === 'Grouping') {
      if (!grouping || grouping.qi !== s.questionIndex)
        grouping = { qi: s.questionIndex, order: s.myGrouping ? [...s.myGrouping] : q.groupRight.map((_, i) => i), selected: null };
      body = `<p class="pstatus">Tap two answers on the right to swap them.</p><div class="pairs" id="pairs"></div>`;
    } else {
      body = `<form class="panel" id="numForm">
          <label for="num">Your guess</label>
          <div class="field"><input id="num" inputmode="decimal" autocomplete="off" enterkeyhint="done">${q.unit ? `<span>${esc(q.unit)}</span>` : ''}</div>
          <div class="saved" id="savedNote" hidden>Saved</div>
        </form>`;
    }
    const spacer = q.type === 'ClosestNumber' ? '<div style="flex:1"></div>' : '';
    app.innerHTML = topbar(s) + body + `<div id="answerStatus" class="pstatus"></div>` + spacer + meChip(s);
    countdown.start(s.remainingMs, secs => renderTimer($('#timer'), secs));

    app.querySelectorAll('.abtn').forEach(b => b.onclick = () => {
      markChoice(+b.dataset.i);
      send('AnswerChoice', +b.dataset.i);
    });
    if (q.type === 'Grouping') renderPairs(q);
    if (q.type === 'ClosestNumber') {
      const input = $('#num');
      if (s.myNumber != null) input.value = s.myNumber;
      let debounce = null;
      const save = () => {
        clearTimeout(debounce);
        const raw = input.value.trim().replace(/\s/g, '').replace(',', '.');
        const v = raw === '' ? null : parseFloat(raw);
        if (v === null || Number.isFinite(v)) send('AnswerNumber', v);
        else $('#answerStatus').textContent = 'Numbers only, please.';
      };
      input.oninput = () => { clearTimeout(debounce); debounce = setTimeout(save, 300); };
      $('#numForm').onsubmit = e => { e.preventDefault(); save(); input.blur(); };
    }
    updateQuestion(s);
  }

  // Refreshes selection and "saved" status from the server without rebuilding the screen.
  function updateQuestion(s) {
    const q = s.question;
    if (q.type === 'MultipleChoice' || q.type === 'TrueFalse') markChoice(s.myChoice);
    const status = $('#answerStatus');
    const note = $('#savedNote'); if (note) note.hidden = !s.answered;
    if (!status) return;
    if (!s.answered) {
      status.textContent = q.type === 'Grouping' ? 'Your arrangement saves automatically.'
        : q.type === 'ClosestNumber' ? 'Type your guess; it saves automatically.'
        : 'Tap your answer.';
    } else {
      status.textContent = q.type === 'Grouping' || q.type === 'ClosestNumber'
        ? 'Saved. You can change it until the time runs out.'
        : 'Answer saved. Tap another to change it.';
    }
  }

  function markChoice(choice) {
    app.querySelectorAll('.abtn').forEach(b => {
      const on = choice != null && +b.dataset.i === choice;
      b.classList.toggle('on', on);
      b.classList.toggle('dim', choice != null && !on);
      b.setAttribute('aria-pressed', on);
      const tick = b.querySelector('.tick');
      if (on && !tick) b.insertAdjacentHTML('beforeend', `<span class="tick">${ICON.check('#23843a')}</span>`);
      if (!on && tick) tick.remove();
    });
  }

  function renderPairs(q) {
    $('#pairs').innerHTML = q.groupLeft.map((l, row) => `
      <div class="pair pair-${row % 6} plastic">
        <span class="cell l">${item(l)}</span>
        <button type="button" class="cell m ${grouping.selected === row ? 'sel' : ''}" data-row="${row}">${item(q.groupRight[grouping.order[row]])}</button>
      </div>`).join('');
    $('#pairs').querySelectorAll('.m').forEach(b => b.onclick = () => {
      const row = +b.dataset.row;
      if (grouping.selected === null) { grouping.selected = row; renderPairs(q); return; }
      const o = grouping.order, a = grouping.selected;
      grouping.selected = null;
      if (a === row) { renderPairs(q); return; }
      // remember where both answers were, swap, redraw, then slide each one in from its old spot
      const cells = () => [...$('#pairs').querySelectorAll('.m')];
      const before = cells().map(c => c.getBoundingClientRect());
      [o[a], o[row]] = [o[row], o[a]];
      send('AnswerGrouping', [...o]);
      renderPairs(q);
      animateSwap(cells(), before, a, row);
    });
  }

  // The two swapped answers glide past each other into their new rows and flash gold,
  // so it's obvious which two traded places.
  function animateSwap(cells, before, a, b) {
    if (matchMedia('(prefers-reduced-motion: reduce)').matches) return;
    for (const [to, from] of [[a, b], [b, a]]) {
      const el = cells[to];
      const dx = before[from].left - before[to].left, dy = before[from].top - before[to].top;
      el.animate([
        { transform: `translate(${dx}px, ${dy}px) scale(1.06)`, boxShadow: '0 0 0 4px #ffd36b, 0 10px 18px rgba(0,0,0,.3)', zIndex: 2 },
        { transform: 'translate(0, 0) scale(1)', boxShadow: '0 0 0 4px #ffd36b, 0 4px 0 rgba(0,0,0,.2)', offset: .75 },
        { transform: 'none', boxShadow: '0 4px 0 rgba(0,0,0,.2)' }
      ], { duration: 520, easing: 'cubic-bezier(.16, 1, .3, 1)' });
    }
  }

  // ---------- results ----------
  function showResult(s) {
    const r = s.result || { correct: false, points: 0, bonus: 0, detail: '' };
    const noAnswer = !s.result || r.detail.startsWith('No answer');
    const [badge, title] = noAnswer ? ['none', "Time's up"]
      : r.correct ? ['good', 'Correct!']
      : r.points > 0 ? ['part', 'Nice one!']
      : ['bad', 'Not this time'];
    const icon = badge === 'good' || badge === 'part' ? ICON.check(badge === 'part' ? '#14213d' : '#fff') : ICON.cross('#fff');
    const base = r.points - r.bonus - (r.streakBonus || 0);
    const how = r.bonus > 0 ? `${base} for the answer, +${r.bonus} speed bonus` : '';
    const detail = r.detail && !['Correct!', 'Not this time.'].includes(r.detail) ? r.detail : '';
    const m = move(s.me);
    app.innerHTML = `
      <div class="res">
        <div class="badge ${badge}">${icon}</div>
        <h2>${title}</h2>
        <div class="chip pts ${r.points ? '' : 'zero'}">+${r.points}</div>
        ${how ? `<div class="sub">${how}</div>` : ''}
        ${r.streakBonus ? `<div class="streak-line">🔥 ${r.streak} in a row: +${r.streakBonus} streak bonus</div>` : ''}
        ${detail ? `<div class="sub">${esc(detail)}</div>` : ''}
        ${s.correctAnswer ? `<div class="sub">Answer: ${answerHtml(s.correctAnswer)}</div>` : ''}
      </div>
      <div class="rankline">You are #${s.me.rank} of ${s.playerCount}${m.n ? `<span class="mv ${m.cls}">${m.icon}${m.n}</span>` : ''}</div>
      ${s.boost ? `<div class="boost">${s.boostRival ? face(s.boostRival) : ''}<span>${esc(s.boost)}</span></div>` : ''}
      <div style="flex:1"></div>
      ${meChip(s)}`;
  }

  // "B: Canberra" -> the same coloured letter piece as on the big screen, then the text.
  function answerHtml(text) {
    const m = /^([A-F]): (.*)$/.exec(text);
    if (!m) return `<b>${esc(text)}</b>`;
    const i = LETTERS.indexOf(m[1]);
    const tf = (m[2] === 'True' || m[2] === 'False') && i < 2;
    return `<span class="ans-tok ${optClass(tf ? 'TrueFalse' : 'MultipleChoice', i)}">${m[1]}</span> <b>${esc(m[2])}</b>`;
  }

  const myAwards = s => s.myAwards.length ? `<div class="my-awards">${s.myAwards.map(a =>
    `<div class="my-award"><b>${esc(a.title)}</b><span>${esc(a.stat)}</span></div>`).join('')}</div>` : '';

  function showAwards(s) {
    app.innerHTML = `
      <div class="center">
        <h2 class="on-board" style="font-size:36px">Award time!</h2>
        ${s.myAwards.length
          ? `<p class="soft">You won ${s.myAwards.length === 1 ? 'an award' : `${s.myAwards.length} awards`}. Take a bow!</p>${myAwards(s)}`
          : '<p class="soft">Watch the big screen to see who takes home the awards.</p>'}
      </div>
      ${meChip(s)}`;
  }

  // ---------- host pause ----------
  // The question screen isn't rebuilt on pause (it would wipe a half-typed guess),
  // so freeze or restart the clock in place and show a notice.
  let wasPaused = false;
  function syncPause(s) {
    const timed = s.phase === 'Question' || s.phase === 'Countdown' || s.phase === 'Reading';
    const paused = timed && s.paused;
    $('#pauseNote').classList.toggle('hidden', !paused);
    const t = $('#timer') || $('#cd');
    if (paused) {
      countdown.stop();
      if (t) { t.textContent = Math.ceil(s.remainingMs / 1000); t.classList.remove('danger'); }
    } else if (wasPaused && timed && t) {
      countdown.start(s.remainingMs, secs => t.id === 'timer' ? renderTimer(t, secs) : (t.textContent = secs));
    }
    wasPaused = paused;
  }

  // ---------- personal recap ----------
  function recap(s) {
    if (!s.recap?.length) return '';
    const mark = r => !r.answered ? '' : r.correct ? ICON.check('#23843a') : r.points > 0 ? ICON.check('#8a6400') : ICON.cross('#d93a28');
    return `<section class="recap" aria-label="Your answers">
      <h3>Your answers</h3>
      <ol>${s.recap.map(r => `
        <li class="${r.correct ? 'ok' : r.points ? 'part' : 'miss'}">
          <span class="rq"><b class="num">${r.number}</b>${esc(r.question)}</span>
          <span class="ra"><i>${mark(r)}</i>You: <b>${esc(r.yourAnswer)}</b></span>
          ${r.correctAnswer && !r.correct ? `<span class="ra right">Answer: <b>${esc(r.correctAnswer)}</b></span>` : ''}
          <span class="chip ${r.points ? '' : 'zero'}">+${r.points}</span>
        </li>`).join('')}</ol>
    </section>`;
  }

  function showFinished(s) {
    const rank = s.me.rank;
    const fills = ['#ffd36b', '#eef1f6', '#e9a56c'];
    const titles = ['Gold', 'Silver', 'Bronze'];
    const podium = rank <= 3;
    app.innerHTML = `
      <div class="center">
        ${podium ? `<div class="medal-big">${ICON.medal(fills[rank - 1], rank)}</div>` : ''}
        <h2 class="on-board" style="font-size:40px;letter-spacing:-.02em">${podium ? `${titles[rank - 1]} for ${esc(s.me.nickname)}!` : `You finished #${rank} of ${s.playerCount}`}</h2>
        <div class="chip final-pts">${fmt(s.me.score)} pts</div>
        ${face(s.me, 'pawn')}
        <p class="soft">${rank === 1 ? 'Champion of the table. Take a bow!' : podium ? 'On the podium. What a game!' : 'Thanks for playing. Rematch next time?'}</p>
        ${myAwards(s)}
        ${recap(s)}
      </div>`;
  }

  // ================= connection =================
  async function identify() {
    renderedKey = null;
    const id = localStorage.getItem(ID_KEY);
    if (id && await conn.invoke('Rejoin', id)) return;
    localStorage.removeItem(ID_KEY);
    showJoin();
  }
  // A banner while the connection is down, so players wait instead of reloading.
  const banner = $('#conn');
  const showBanner = text => { banner.textContent = text; banner.classList.remove('hidden'); };
  conn.onreconnecting(() => showBanner('Reconnecting… hang on, your answers are safe.'));
  conn.onreconnected(() => { banner.classList.add('hidden'); identify(); });
  conn.onclose(() => { showBanner('Connection lost. Trying again…'); setTimeout(start, 2000); });
  async function start() {
    try { await conn.start(); banner.classList.add('hidden'); await identify(); }
    catch { showBanner('Connection lost. Trying again…'); setTimeout(start, 2000); }
  }
  start();
  // The Christmas look loads alongside the connection; redraw once it is known (never mid-question, to keep a typed guess).
  Festive.ready.then(on => { if (on && state && state.phase !== 'Question') { renderedKey = null; render(); } });
})();
