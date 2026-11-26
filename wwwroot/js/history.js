// Presenter: "Full standings" (this game, everyone) and "Old games" (every played game: standings, each answer,
// questions with everyone's answers, awards). Opened from the final screen; shown in a panel over the stage.
const QvizzHistory = (() => {
  const { LETTERS, esc, face, fmt } = Qvizz;
  let conn = null, getState = () => null;
  let view = null;           // { kind: 'full' } | { kind: 'list', games } | { kind: 'game', game, tab, q }
  const panel = () => document.getElementById('history');

  const TYPE = { MultipleChoice: 'Multiple choice', TrueFalse: 'True or false', Grouping: 'Matching', ClosestNumber: 'Closest number', Ordering: 'Ordering' };
  const when = iso => iso ? new Date(iso).toLocaleString('en-GB', { day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' }) : '';
  const pts = n => `${n > 0 ? '+' : ''}${fmt(n)}`;
  const part = x => (x.leftImage ? `<img src="${esc(x.leftImage)}" alt=""> ` : '') + esc(x.text) + (x.image ? ` <img src="${esc(x.image)}" alt="">` : '');
  const secs = ms => ms == null ? '' : `${(ms / 1000).toFixed(1)} s`;

  function init(connection, stateFn) {
    conn = connection;
    getState = stateFn;
    document.addEventListener('click', onClick);
    document.addEventListener('keydown', e => { if (e.key === 'Escape' && view) close(); });
  }

  async function open(kind) {
    if (kind === 'full') { view = { kind: 'full' }; render(); return; }
    view = { kind: 'list', games: null }; render();
    try { view.games = await conn.invoke('ListGames'); }
    catch (e) { view.error = e.message; }
    if (view?.kind === 'list') render();
  }

  async function openGame(id) {
    view = { kind: 'game', game: null, tab: 'standings', q: 0 }; render();
    try { view.game = await conn.invoke('GetGame', id); if (!view.game) view.error = 'That game could not be found.'; }
    catch (e) { view.error = e.message; }
    if (view?.kind === 'game') render();
  }

  // A test run or a game nobody needs: gone for good, after a confirmation.
  async function removeGame(id) {
    const g = view?.games?.find(x => x.id === id);
    if (!g) return;
    const label = `${g.title} from ${when(g.startedAt || g.createdAt)} (${g.players} player${g.players === 1 ? '' : 's'})`;
    if (!confirm(`Delete ${label}?\n\nIts standings, answers and players' drawings are removed for good. This can't be undone.`)) return;
    try {
      const err = await conn.invoke('DeleteGame', id);
      if (err) { Qvizz.toast(err); return; }
      view.games = view.games.filter(x => x.id !== id);
      render();
      Qvizz.toast('Game deleted.');
    } catch (e) { Qvizz.toast(e.message); }
  }

  function close() { view = null; const p = panel(); p.classList.add('hidden'); p.innerHTML = ''; }

  /** The full standings follow the live game, so they close when the game moves on (e.g. a new game starts). */
  function onState(s) {
    if (view?.kind === 'full') { if (s.phase !== 'Finished') close(); else render(); }
  }

  function onClick(e) {
    const t = e.target.closest('[data-hist]');
    if (!t) return;
    const [what, arg] = t.dataset.hist.split(':');
    if (what === 'open') open(arg);
    else if (what === 'close') close();
    else if (what === 'list') open('list');
    else if (what === 'game') openGame(arg);
    else if (what === 'del') removeGame(arg);
    else if (what === 'tab') { view.tab = arg; render(); }
    else if (what === 'q') { view.tab = 'questions'; view.q = +arg; render(); }
  }

  // ---------- rendering ----------
  function render() {
    const p = panel();
    p.classList.remove('hidden');
    const body = view.kind === 'full' ? fullStandings()
      : view.kind === 'list' ? gameList()
      : gameDetail();
    p.innerHTML = `<div class="hist-card" role="dialog" aria-modal="true">${body}</div>`;
    p.scrollTop = 0;
    Qvizz.fitNames(p);
  }

  const closeBtn = '<button type="button" class="hist-x" data-hist="close" aria-label="Close">Close ✕</button>';
  const loading = () => view.error ? `<p class="hist-empty">${esc(view.error)}</p>` : '<p class="hist-empty">Loading…</p>';

  function ranking(players, extra = () => '') {
    return `<ol class="hist-rank">${players.map(p => `
      <li><span class="rk">${p.rank}</span>${face(p)}<span class="nm">${esc(p.nickname)}</span>${extra(p)}<b class="sc">${fmt(p.score)}</b></li>`).join('')}</ol>`;
  }

  function fullStandings() {
    const s = getState();
    const players = [...(s?.players || [])].sort((a, b) => a.rank - b.rank || b.score - a.score || a.seat - b.seat);
    return `<header class="hist-head"><div><h2>Full standings</h2><p>${esc(s?.quizTitle || '')} · ${players.length} players</p></div>${closeBtn}</header>
      ${players.length ? ranking(players) : '<p class="hist-empty">Nobody played this game.</p>'}`;
  }

  function gameList() {
    const head = `<header class="hist-head"><div><h2>Old games</h2><p>Every played game, newest first. Open one to see its standings and everyone's answers.</p></div>${closeBtn}</header>`;
    if (!view.games) return head + loading();
    if (!view.games.length) return head + '<p class="hist-empty">No games have been played yet.</p>';
    const rows = view.games.map(g => `
      <div class="hist-row">
      <button type="button" class="hist-game" data-hist="game:${g.id}">
        <span class="when">${esc(when(g.startedAt || g.createdAt))}${g.current ? ' <em>this game</em>' : ''}</span>
        <span class="ttl">${esc(g.title)}</span>
        <span class="meta">${g.players} player${g.players === 1 ? '' : 's'} · ${g.questionsPlayed}${g.questionCount ? ` of ${g.questionCount}` : ''} questions${g.finishedAt ? '' : ' · not finished'}</span>
        <span class="win">${g.winner ? `🏆 ${esc(g.winner)} <b>${fmt(g.winnerScore)}</b>` : ''}</span>
      </button>
      ${g.current ? '<span class="hist-del" aria-hidden="true" style="visibility:hidden">Delete</span>' : `<button type="button" class="hist-del" data-hist="del:${g.id}" title="Delete this game" aria-label="Delete this game">Delete</button>`}
      </div>`).join('');
    return head + `<div class="hist-games">${rows}</div>`;
  }

  function gameDetail() {
    const back = '<button type="button" class="hist-back" data-hist="list">← All games</button>';
    const g = view.game;
    if (!g) return `<header class="hist-head"><div>${back}</div>${closeBtn}</header>` + loading();
    const s = g.game;
    const tabs = [['standings', 'Standings'], ['answers', 'Answers'], ['questions', 'Questions'], ...(g.awards.length ? [['awards', 'Awards']] : [])]
      .map(([k, label]) => `<button type="button" role="tab" aria-selected="${view.tab === k}" class="${view.tab === k ? 'on' : ''}" data-hist="tab:${k}">${label}</button>`).join('');
    const note = g.questionsFromCurrentQuiz
      ? '<p class="hist-note">This game was saved before games kept their own copy of the questions, so the questions come from the current quiz file and may have changed since.</p>' : '';
    const content = { standings, answers: answerGrid, questions: questionView, awards: awardList }[view.tab](g);
    return `<header class="hist-head"><div>${back}<h2>${esc(s.title)}</h2>
        <p>${esc(when(s.startedAt || s.createdAt))} · ${s.players} player${s.players === 1 ? '' : 's'} · ${s.questionsPlayed}${s.questionCount ? ` of ${s.questionCount}` : ''} questions${s.finishedAt ? '' : ' · not finished'}</p></div>${closeBtn}</header>
      ${note}<nav class="hist-tabs" role="tablist">${tabs}</nav>${content}`;
  }

  const played = g => g.players[0]?.answers.length ?? 0;

  function standings(g) {
    if (!g.players.length) return '<p class="hist-empty">Nobody played this game.</p>';
    const n = played(g);
    return ranking(g.players, p => `<span class="right">${p.correctCount} of ${n} right</span>`);
  }

  // Every player's answer to every question: the letter / number / pieces right, coloured by result, with the points.
  function answerGrid(g) {
    const n = played(g);
    if (!g.players.length || !n) return '<p class="hist-empty">No questions were scored in this game.</p>';
    const head = g.questions.slice(0, n).map((q, i) =>
      `<th><button type="button" data-hist="q:${i}" title="${esc(q.text)}">Q${i + 1}</button></th>`).join('');
    const rows = g.players.map(p => `<tr><th class="pl"><span class="rk">${p.rank}</span>${face(p)}<span class="nm">${esc(p.nickname)}</span></th>
      ${p.answers.map(a => `<td class="r-${a.result}" title="${esc(a.text)}"><b>${esc(a.short)}</b><small>${a.points ? pts(a.points) : '0'}</small></td>`).join('')}
      <td class="tot">${fmt(p.score)}</td></tr>`).join('');
    return `<p class="hist-hint">Green: right · yellow: some points · red: wrong · grey: no answer. Click a question to see it with everyone's answers.</p>
      <div class="hist-grid-wrap"><table class="hist-grid"><thead><tr><th class="pl">Player</th>${head}<th class="tot">Total</th></tr></thead><tbody>${rows}</tbody></table></div>`;
  }

  function questionView(g) {
    const n = g.questions.length;
    if (!n) return '<p class="hist-empty">No questions.</p>';
    const i = Math.min(view.q, n - 1), q = g.questions[i];
    const pick = g.questions.map((x, k) => `<button type="button" class="${k === i ? 'on' : ''}" data-hist="q:${k}">${k + 1}</button>`).join('');
    const correct = q.correct ? `<p class="ok-ans">Answer: <b>${esc(q.correct)}</b></p>`
      : q.correctParts.length ? `<div class="ok-ans">Answer:<ul class="parts">${q.correctParts.map(x => `<li>${part(x)}</li>`).join('')}</ul></div>` : '';
    const scored = i < played(g);
    const people = scored ? [...g.players].sort((a, b) => b.answers[i].points - a.answers[i].points || a.rank - b.rank).map(p => {
      const a = p.answers[i];
      const parts = a.parts.length ? `<ul class="parts">${a.parts.map(x => `<li class="${x.ok ? 'ok' : 'no'}">${part(x)}</li>`).join('')}</ul>` : '';
      const how = [a.responseMs != null && a.result !== 'none' ? `in ${secs(a.responseMs)}` : '', a.changes > 1 ? `changed ${a.changes - 1}×` : '',
        a.bonus ? `+${a.bonus} speed` : '', a.streakBonus ? `+${a.streakBonus} streak` : ''].filter(Boolean).join(' · ');
      return `<tr class="r-${a.result}"><th class="pl">${face(p)}<span class="nm">${esc(p.nickname)}</span></th>
        <td class="ans">${esc(a.text)}${parts}</td><td class="how">${how}</td><td class="pt">${pts(a.points)}</td></tr>`;
    }).join('') : '';
    return `<nav class="hist-qs" aria-label="Questions">${pick}</nav>
      <section class="hist-q">
        ${q.image ? `<img src="${esc(q.image)}" alt="">` : ''}
        <div><small>Question ${i + 1} · ${TYPE[q.type] || q.type}</small><h3>${esc(q.text)}</h3>${correct}${q.explanation ? `<p class="hist-why">${esc(q.explanation)}</p>` : ''}</div>
      </section>
      ${scored ? `<table class="hist-ans"><tbody>${people}</tbody></table>` : '<p class="hist-empty">This question was not played.</p>'}`;
  }

  function awardList(g) {
    const byId = Object.fromEntries(g.players.map(p => [p.id, p]));
    return `<div class="hist-awards">${g.awards.map(a => {
      const w = byId[a.winnerId];
      return `<article><h3>${esc(a.title)}</h3><p>${esc(a.blurb)}</p><div class="w">${w ? face(w) : ''}<b class="nm">${esc(w?.nickname || '')}</b></div><span>${esc(a.stat)}</span></article>`;
    }).join('')}</div>`;
  }

  return { init, onState, open, close };
})();
