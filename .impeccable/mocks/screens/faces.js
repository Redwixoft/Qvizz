// Stand-in "drawn faces" so the sketches show what player avatars look like.
// In the real app these are the PNGs players draw on their phones.
const FACES = {
  anna:  { eyes: 'dots',  mouth: 'smile', hair: '#7a4a1d', extra: 'lashes' },
  peter: { eyes: 'dots',  mouth: 'grin',  hair: '#222',    extra: 'glasses' },
  zuzka: { eyes: 'happy', mouth: 'smile', hair: '#e2483a', extra: 'freckles' },
  marek: { eyes: 'dots',  mouth: 'flat',  hair: '#3b2a1a', extra: 'beard' },
  jana:  { eyes: 'wink',  mouth: 'o',     hair: '#f2b705', extra: '' },
  tomas: { eyes: 'dots',  mouth: 'smile', hair: '#555',    extra: 'cap' }
};

// Extra generated faces ("g0", "g1", …) so crowded screens don't repeat the same six.
function generatedFace(i) {
  const pick = (arr, k) => arr[(i * k + 3) % arr.length];
  return {
    eyes: pick(['dots', 'happy', 'wink', 'dots'], 7),
    mouth: pick(['smile', 'grin', 'flat', 'o', 'smile'], 5),
    hair: pick(['#7a4a1d', '#222', '#e2483a', '#3b2a1a', '#f2b705', '#555', '#0f78b5', '#ff7eb6'], 3),
    extra: pick(['', 'glasses', 'freckles', 'beard', 'cap', 'lashes', ''], 11)
  };
}

function faceSvg(name) {
  const f = FACES[name] || (/^g\d+$/.test(name) ? generatedFace(+name.slice(1)) : FACES.anna);
  const s = 'stroke="#222" stroke-width="5" stroke-linecap="round" fill="none"';
  const eyes = {
    dots:  `<circle cx="38" cy="46" r="5" fill="#222"/><circle cx="62" cy="46" r="5" fill="#222"/>`,
    happy: `<path d="M31 47q7-8 14 0M55 47q7-8 14 0" ${s}/>`,
    wink:  `<circle cx="38" cy="46" r="5" fill="#222"/><path d="M55 47h14" ${s}/>`
  }[f.eyes];
  const mouth = {
    smile: `<path d="M36 64q14 12 28 0" ${s}/>`,
    grin:  `<path d="M34 62q16 16 32 0z" fill="#fff" stroke="#222" stroke-width="4"/>`,
    flat:  `<path d="M38 66h24" ${s}/>`,
    o:     `<circle cx="50" cy="66" r="6" ${s}/>`
  }[f.mouth];
  const extra = {
    lashes: `<path d="M31 39l-4-4M45 39l3-4M55 39l-3-4M69 39l4-4" stroke="#222" stroke-width="3" stroke-linecap="round"/>`,
    glasses:`<circle cx="38" cy="46" r="10" ${s} stroke-width="3"/><circle cx="62" cy="46" r="10" ${s} stroke-width="3"/><path d="M48 46h4" ${s} stroke-width="3"/>`,
    freckles:`<g fill="#c2703d"><circle cx="30" cy="57" r="2"/><circle cx="36" cy="59" r="2"/><circle cx="64" cy="59" r="2"/><circle cx="70" cy="57" r="2"/></g>`,
    beard:  `<path d="M24 58q4 30 26 32q22-2 26-32" fill="#3b2a1a" opacity=".85"/><path d="M38 68h24" stroke="#fff" stroke-width="4" stroke-linecap="round"/>`,
    cap:    `<path d="M18 30q32-28 64 0z" fill="#1489cc"/><path d="M70 30h18" stroke="#1489cc" stroke-width="7" stroke-linecap="round"/>`,
    '': ''
  }[f.extra];
  return `<svg viewBox="0 0 100 100" xmlns="http://www.w3.org/2000/svg">
    <ellipse cx="50" cy="54" rx="34" ry="38" fill="#fff" stroke="#333" stroke-width="3"/>
    <path d="M17 44q2-30 33-30q31 0 33 30q-10-16-33-16q-23 0-33 16z" fill="${f.hair}"/>
    ${eyes}${mouth}${extra}</svg>`;
}

document.querySelectorAll('[data-face]').forEach(el => {
  el.innerHTML = faceSvg(el.dataset.face);
});
