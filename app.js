const sections = [
  { id: 'fantasy', title: '01 · Primary fantasy', description: 'What should the player feel they are becoming?', type: 'single', options: [
    ['builder', 'Build a powerful character', 'Optimize gear, abilities, stats, and long-term specialization.', true],
    ['leader', 'Lead a dangerous party', 'Prepare a team and make expedition decisions under pressure.', true],
    ['guild', 'Grow an adventurer guild', 'Recruit heroes, expand facilities, and manage an organization.', false],
    ['explorer', 'Uncover a mysterious world', 'Follow quests, discover secrets, and make narrative choices.', false]
  ]},
  { id: 'unit', title: '02 · Player unit', description: 'What is the main thing the player configures?', type: 'single', options: [
    ['hero', 'One main hero', 'Clear identity, simpler balance, and deeper personal progression.', false],
    ['party', 'A party of 3–4', 'Strong fit for autobattle, roles, equipment, and tactical preparation.', true],
    ['roster', 'A full roster', 'Large collection-building appeal, with significantly more UI and balance cost.', false]
  ]},
  { id: 'poe', title: '03 · PoE-like qualities', description: 'Select exactly three inspirations. These become our complexity budget.', type: 'multi', limit: 3, options: [
    ['branching', 'Branching progression', 'Meaningful paths through a map, passive tree, or specialization system.', true],
    ['builds', 'Build freedom', 'Multiple viable ways to configure heroes and abilities.', true],
    ['risk', 'Risk / reward routes', 'Safer paths versus dangerous routes with better opportunities.', true],
    ['loot', 'Loot variety', 'Frequent item decisions, affixes, and equipment upgrades.', false],
    ['tone', 'Dark fantasy tone', 'Bleak atmosphere, dangerous places, and morally complicated stories.', false],
    ['interactions', 'Complex skill interactions', 'Synergies, statuses, and layered build combinations.', false]
  ]},
  { id: 'agency', title: '04 · Combat intervention', description: 'How much can the player do once an encounter begins?', type: 'single', options: [
    ['observe', 'Configure and observe', 'The player sets priorities beforehand; combat executes automatically.', true],
    ['limited', 'Limited intervention', 'The player can retreat, change stance, or issue occasional emergency commands.', true],
    ['active', 'Active tactical control', 'The player can frequently interrupt and direct combat in real time.', false]
  ]},
  { id: 'art', title: '05 · Art direction', description: 'Choose the visual north star for generated and hand-authored assets.', type: 'single', options: [
    ['gothic', 'Dark gothic fantasy', 'Moody, high-contrast, weathered, and dangerous.', false],
    ['stylized', 'Colorful stylized fantasy', 'Readable silhouettes, expressive characters, and broad appeal.', true],
    ['painted', 'Grim hand-painted RPG', 'Rich materials, dramatic lighting, and illustrated environments.', false],
    ['lowpoly', 'Low-poly 2.5D', 'Clean geometry, efficient production, and strong diorama presentation.', false],
    ['pixel', 'Isometric pixel art', 'Crisp grid-based sprites with a strong retro identity.', false]
  ]},
  { id: 'forbidden', title: '06 · Explicitly out of scope', description: 'Select features we will not build in the first playable milestone.', type: 'multi', options: [
    ['pvp', 'PvP', 'No competitive combat or player-versus-player balancing at launch.', true],
    ['trading', 'Player trading', 'No player economy, market manipulation, or item ownership complexity.', true],
    ['guilds', 'Guild multiplayer', 'No shared guild systems, chat, or cooperative progression.', true],
    ['openworld', 'Fully open world', 'No seamless infinite map; the world is composed of deliberate regions.', true],
    ['classes', 'Dozens of classes', 'No huge class matrix before the core loop is proven.', true],
    ['housing', 'Player housing', 'No housing or decoration system in the first milestone.', true]
  ]}
];

const state = JSON.parse(localStorage.getItem('boundary-charter') || '{}');
let locked = localStorage.getItem('boundary-charter-locked') === 'true';
const root = document.querySelector('#sections');

function render() {
  root.innerHTML = sections.map(section => `
    <section class="workshop-section" data-section="${section.id}">
      <div class="section-top"><div><p class="eyebrow">DECISION</p><h2>${section.title}</h2><p>${section.description}</p></div><span class="count" id="count-${section.id}"></span></div>
      <div class="options">${section.options.map(([id, title, desc, recommended]) => `
        <label class="option ${section.type === 'multi' ? 'multi' : ''}">
          <input type="${section.type === 'multi' ? 'checkbox' : 'radio'}" name="${section.id}" value="${id}" ${selected(section.id, id) ? 'checked' : ''} ${locked ? 'disabled' : ''} />
          <span class="option-card"><span class="option-title"><span class="mark"></span>${title}</span><p class="option-desc">${desc}</p>${recommended ? '<span class="recommendation">Recommended starting point</span>' : ''}</span>
        </label>`).join('')}</div>
    </section>`).join('');
  root.querySelectorAll('input').forEach(input => input.addEventListener('change', onChange));
  updateUI();
}
function selected(section, id) { return Array.isArray(state[section]) ? state[section].includes(id) : state[section] === id; }
function onChange(event) {
  const input = event.target, section = sections.find(s => s.id === input.name);
  if (section.type === 'multi') {
    const values = [...document.querySelectorAll(`input[name="${section.id}"]:checked`)].map(x => x.value);
    if (section.limit && values.length > section.limit) { input.checked = false; return; }
    state[section.id] = values;
  } else state[section.id] = input.value;
  localStorage.setItem('boundary-charter', JSON.stringify(state)); updateUI();
}
function updateUI() {
  const complete = sections.filter(s => (Array.isArray(state[s.id]) ? state[s.id].length > 0 : Boolean(state[s.id]))).length;
  document.querySelector('#progressLabel').textContent = `${complete} / ${sections.length} complete`;
  document.querySelector('#progressBar').style.width = `${complete / sections.length * 100}%`;
  sections.forEach(s => { const values = Array.isArray(state[s.id]) ? state[s.id] : (state[s.id] ? [state[s.id]] : []); document.querySelector(`#count-${s.id}`).textContent = s.limit ? `${values.length} / ${s.limit} selected` : values.length ? 'Selected' : 'Not selected'; });
  const labels = []; sections.forEach(s => { const values = Array.isArray(state[s.id]) ? state[s.id] : (state[s.id] ? [state[s.id]] : []); values.forEach(value => { const item = s.options.find(x => x[0] === value); if (item) labels.push(`<div class="summary-item"><strong>${s.title.replace(/^\d+ · /, '')}</strong><span>${item[1]}</span></div>`); }); });
  document.querySelector('#summary').innerHTML = labels.length ? `<div class="summary-list">${labels.join('')}</div>` : '<div class="summary-empty">Make your first selection to start shaping the charter.</div>';
  document.querySelector('#summaryState').textContent = locked ? 'Locked' : 'Draft'; document.querySelector('#summaryState').classList.toggle('locked', locked);
  document.querySelector('#lockButton').textContent = locked ? 'Unlock decisions' : 'Lock decisions';
}
document.querySelector('#lockButton').addEventListener('click', () => { if (!locked && Object.keys(state).length < sections.length) { alert('Complete all six decisions before locking the charter.'); return; } locked = !locked; localStorage.setItem('boundary-charter-locked', locked); render(); });
document.querySelector('#resetButton').addEventListener('click', () => { if (confirm('Reset all workshop decisions?')) { Object.keys(state).forEach(key => delete state[key]); locked = false; localStorage.removeItem('boundary-charter'); localStorage.removeItem('boundary-charter-locked'); render(); } });
document.querySelector('#exportButton').addEventListener('click', () => { const output = ['# Boundary Charter', '', `Status: ${locked ? 'Locked' : 'Draft'}`, '']; sections.forEach(s => { const values = Array.isArray(state[s.id]) ? state[s.id] : (state[s.id] ? [state[s.id]] : []); output.push(`## ${s.title.replace(/^\d+ · /, '')}`); output.push(values.length ? values.map(v => `- ${s.options.find(x => x[0] === v)?.[1] || v}`).join('\n') : '- Not decided'); output.push(''); }); const blob = new Blob([output.join('\n')], {type: 'text/markdown'}); const link = Object.assign(document.createElement('a'), {href: URL.createObjectURL(blob), download: 'boundary-charter.md'}); link.click(); URL.revokeObjectURL(link.href); });
render();
