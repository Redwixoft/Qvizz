// Host remote: run the game from your phone while the big screen shows /admin.
// Same password as the presenter screen; it joins the same admin channel, so both stay in sync.
(() => {
  const { esc, $, face, fmt, Countdown, renderTimer, connect, toast } = Qvizz;
  const conn = connect();
  const countdown = new Countdown();
  const app = $('#app');
  const PW_KEY = 'qvizz.adminPassword';
  let state = null;
  let showPlayers = false;

  // ---------- login ----------
  async function login(password) {
    const ok = await conn.invoke('AdminLogin', password);
    if (ok) {
      sessionStorage.setItem(PW_KEY, password);
      $('#login').classList.add('hidden');
      app.classList.remove('hidden');
    } else {
      sessionStorage.removeItem(PW_KEY);
      $('#loginError').textContent = 'That password is not right. Try again.';
    }
    return ok;
  }
  $('#login').addEventListener('submit', e => { e.preventDefault(); login($('#password').value); });

  // ---------- actions ----------
  const call = (method, ...args) => conn.invoke(method, ...args).catch(err => toast(err.message));
  app.addEventListener('click', e => {
    const kick = e.target.closest('[data-kick]');
    if (kick) {
      if (confirm(`Remove ${kick.dataset.name} from the game? They can join again under another name.`)) call('KickPlayer', kick.dataset.kick);
      return;
    }
    if (e.target.closest('#togglePlayers')) { showPlayers = !showPlayers; render(); return; }
    const b = e.target.closest('[data-act]');
    if (!b) return;
    if (b.dataset.confirm && !confirm(b.dataset.confirm)) return;
    b.disabled = true;
    call(b.dataset.act);
  });

  conn.on('adminState', s => { state = s; render(); });

  // What the big button does in each phase.
  function primary(s) {
    const last = s.questionIndex === s.questionCount - 1;
    switch (s.phase) {
      case 'Lobby': return { act: 'StartQuiz', label: 'Start the game', disabled: !s.players.length };
      case 'Countdown':
      case 'Question': return s.paused ? { act: 'Resume', label: 'Resume' } : { act: 'Pause', label: 'Pause', cls: 'light' };
      case 'Reveal': return { act: 'Next', label: last ? 'Final results' : 'Show standings' };
      case 'Leaderboard': return { act: 'Next', label: 'Next question' };
      case 'Awards': return { act: 'Next', label: 'Final podium' };
      case 'Finished': return { act: 'NewGame', label: 'New game', confirm: 'Start a new game? This one is saved in the archive and everyone joins again.' };
    }
  }

  function status(s) {
    const q = `Question ${s.questionIndex + 1} of ${s.questionCount}`;
    return {
      Lobby: `Lobby · ${s.players.length} player${s.players.length === 1 ? '' : 's'}`,
      Countdown: 'Countdown to question 1',
      Question: q,
      Reveal: `${q} · answer shown`,
      Leaderboard: `Standings after question ${s.questionIndex + 1}`,
      Awards: 'Awards',
      Finished: 'Game over · podium'
    }[s.phase];
  }

  function detail(s) {
    if (s.phase === 'Question' || s.phase === 'Countdown') {
      const connected = s.players.filter(p => p.connected).length;
      return `${s.question ? `<p class="q">${esc(s.question.text)}</p>` : ''}
        <div class="timer-row"><b class="tok ${s.paused ? 'paused' : ''}" id="timer"></b>
          <span>${s.paused ? 'Paused' : s.phase === 'Question' ? `<b class="num">${s.answeredCount}</b> of ${connected} answered` : 'Get ready…'}</span></div>`;
    }
    if (s.phase === 'Reveal' && s.reveal) {
      const r = s.reveal;
      return `<p class="q">${esc(s.question.text)}</p><p class="sub">${r.answeredPlayers ? `${r.correctPlayers} of ${r.answeredPlayers} got it right` : 'Nobody answered'}</p>`;
    }
    if (s.phase === 'Leaderboard' || s.phase === 'Finished') {
      return `<ol class="top">${s.leaderboard.slice(0, 3).map(p => `<li>${face(p)}<span>${esc(p.nickname)}${p.streak >= 3 ? ' <span class="fire">🔥</span>' : ''}</span><b class="num">${fmt(p.score)}</b></li>`).join('')}</ol>`;
    }
    if (s.phase === 'Awards') return `<p class="sub">${s.awards.length} award${s.awards.length === 1 ? '' : 's'} on the big screen</p>`;
    return `<p class="sub">Players are joining. Start when everyone is in.</p>`;
  }

  function render() {
    const s = state;
    if (!s) return;
    countdown.stop();
    const p = primary(s);
    const players = [...s.players].sort((a, b) => a.seat - b.seat);
    app.innerHTML = `
      <header class="remote-head"><span class="mark-sm">Qvizz<i>.</i></span><span class="quiz">${esc(s.quizTitle)}</span></header>
      <section class="panel remote-card">
        <h2>${esc(status(s))}</h2>
        ${detail(s)}
      </section>
      <button class="btn plastic big ${p.cls || ''}" data-act="${p.act}" ${p.disabled ? 'disabled' : ''} ${p.confirm ? `data-confirm="${esc(p.confirm)}"` : ''}>${p.label}</button>
      <button class="btn light plastic" id="togglePlayers" type="button">${showPlayers ? 'Hide players' : `Players (${players.length})`}</button>
      ${showPlayers ? `<ul class="remote-players">${players.map(pl => `
        <li class="${pl.connected ? '' : 'offline'}">${face(pl)}<span class="nm">${esc(pl.nickname)}</span><span class="num">${fmt(pl.score)}</span>
          <button type="button" data-kick="${pl.id}" data-name="${esc(pl.nickname)}">Remove</button></li>`).join('') || '<li>Nobody has joined yet.</li>'}</ul>` : ''}
      ${s.phase !== 'Finished' && s.phase !== 'Lobby' ? `<button class="btn light plastic quiet" data-act="NewGame"
        data-confirm="End this game and open a new lobby? Everyone has to join again. This game is kept in the archive.">End game &amp; new lobby</button>` : ''}`;

    if (s.phase === 'Countdown' || s.phase === 'Question') {
      if (s.paused) { const t = $('#timer'); if (t) t.textContent = Math.ceil(s.remainingMs / 1000); }
      else if (s.phase === 'Countdown') countdown.start(s.remainingMs, secs => { const t = $('#timer'); if (t) t.textContent = secs; });
      else countdown.start(s.remainingMs, secs => renderTimer($('#timer'), secs));
    }
  }

  // ---------- connection (with a visible banner while it's down) ----------
  const banner = $('#conn');
  const showBanner = text => { banner.textContent = text; banner.classList.remove('hidden'); };
  conn.onreconnecting(() => showBanner('Reconnecting…'));
  conn.onreconnected(() => {
    banner.classList.add('hidden');
    const pw = sessionStorage.getItem(PW_KEY);
    if (pw) login(pw);
  });
  conn.onclose(() => { showBanner('Connection lost. Trying again…'); setTimeout(start, 3000); });
  async function start() {
    try {
      await conn.start();
      banner.classList.add('hidden');
      const pw = sessionStorage.getItem(PW_KEY);
      if (pw) await login(pw);
    } catch {
      showBanner('Connection lost. Trying again…');
      setTimeout(start, 3000);
    }
  }
  start();
})();
