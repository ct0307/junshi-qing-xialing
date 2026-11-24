// 三案共用的隔离界面演示；不运行战斗规则，不访问玩家存档。
(() => {
  'use strict';
  function fit() {
    const board = document.getElementById('artboard');
    if (!board) return;
    const scale = Math.min(innerWidth / 1280, innerHeight / 720);
    Object.assign(board.style, {transform: `scale(${scale})`, left: `${(innerWidth - 1280 * scale) / 2}px`, top: `${(innerHeight - 720 * scale) / 2}px`});
  }
  window.addEventListener('resize', fit);
  fit();
  function ready() {
    const data = window.PROPOSAL_DATA;
    const board = document.getElementById('artboard');
    const field = document.querySelector('[data-scene]');
    if (!data || !board || !field) return;
    const initial = () => ({gold: 240, selected: 'zhao_yun', phase: 'preparing', speed: 1,
      deployed: [{id: 'guan_yu', slot: 29, level: 1}, {id: 'huang_zhong', slot: 30, level: 2}],
      hint: '已选赵云，点击空阵位招募；开战前可调整布阵。', candidate: 31, seconds: 0, wave: 0});
    let state = initial();
    const hero = id => data.heroes.find(item => item.Id === id);
    const slot = id => data.level.slots.find(item => item.Id === id);
    const point = position => ({x: (position.X - 38) / 1844 * 100, y: (position.Y - 144) / 698 * 100});
    const setText = (key, value) => document.querySelectorAll(`[data-readout="${key}"]`).forEach(el => {el.textContent = value;});
    field.setAttribute('aria-label', '黄巾关布阵设计示意，32个正式阵位');
    field.innerHTML = '<img class="scene-map" src="assets/map.png" alt="沿用正式素材的黄巾关地图"><div class="scene-coverage" hidden></div><div class="scene-entry">敌军入口 →</div>';
    for (const item of data.level.slots) {
      const p = point(item.Position);
      const button = document.createElement('button');
      button.type = 'button'; button.className = 'scene-slot'; button.dataset.slot = item.Id;
      button.setAttribute('aria-label', `阵位${item.Id}`);
      Object.assign(button.style, {left: `${p.x}%`, top: `${p.y}%`});
      button.addEventListener('click', () => place(item.Id));
      field.append(button);
    }
    const overlay = document.createElement('div');
    overlay.className = 'proposal-modal'; overlay.hidden = true;
    overlay.innerHTML = '<section class="proposal-dialog" role="dialog" aria-modal="true" aria-labelledby="modal-title"><h2 id="modal-title"></h2><div data-modal-body></div><button type="button" data-modal-close>返回布阵</button></section>';
    board.append(overlay);
    let savedPhase, focusBefore;
    function closeModal() {
      overlay.hidden = true;
      if (savedPhase) {state.phase = savedPhase; savedPhase = null;}
      render();
      focusBefore?.focus();
    }
    overlay.querySelector('[data-modal-close]').addEventListener('click', closeModal);
    overlay.addEventListener('keydown', event => {
      if (event.key === 'Escape') closeModal();
      if (event.key === 'Tab') {event.preventDefault(); overlay.querySelector('[data-modal-close]').focus();}
    });
    function showModal(title, body) {
      focusBefore = document.activeElement;
      if (state.phase === 'running') {savedPhase = 'running'; state.phase = 'paused';}
      overlay.querySelector('h2').textContent = title;
      overlay.querySelector('[data-modal-body]').innerHTML = body;
      overlay.hidden = false;
      overlay.querySelector('[data-modal-close]').textContent = title === '战报示意' ? '返回设计预览' : '返回战场';
      overlay.querySelector('[data-modal-close]').focus();
      render();
    }
    function place(slotId) {
      const occupied = state.deployed.find(item => item.slot === slotId);
      if (occupied) {state.selected = occupied.id; state.candidate = slotId; state.hint = `已查看${hero(occupied.id).Name}，可查看成长状态。`; render(); return;}
      if (state.phase !== 'preparing') {state.hint = '交战中阵位固定；本页只演示界面状态。'; render(); return;}
      if (!state.selected) {state.hint = '先选择下方或侧边的一名武将，再点击空阵位。'; render(); return;}
      const selected = hero(state.selected);
      const existing = state.deployed.find(item => item.id === selected.Id);
      if (existing) {existing.slot = slotId; state.hint = `${selected.Name}已免费调位。`;}
      else if (state.gold < selected.Cost) {state.hint = `军饷不足，招募${selected.Name}还差${selected.Cost - state.gold}。`;}
      else {state.gold -= selected.Cost; state.deployed.push({id: selected.Id, slot: slotId, level: 1}); state.hint = `${selected.Name}已上阵，扣除军饷${selected.Cost}。`;}
      state.candidate = slotId;
      render();
    }
    function action(name) {
      if (name === 'toggle-run') {
        state.phase = state.phase === 'preparing' || state.phase === 'paused' ? 'running' : 'paused';
        state.hint = state.phase === 'running' ? '进军中 · 演示界面状态，未运行真实战斗。' : '已暂停 · 继续后恢复当前演示。';
        state.wave = state.wave || 1;
      } else if (name === 'speed') state.speed = state.speed % 3 + 1;
      else if (name === 'deselect') {state.selected = null; state.hint = '请选择一名武将，查看定位、军饷和成长。';}
      else if (name === 'wallet') {state.gold = state.gold === 24 ? 240 : 24; state.hint = state.gold === 24 ? '军饷不足示意 · 价格与差额明确显示。' : '军饷已恢复为常规演示状态。';}
      else if (name === 'reset') {state = initial(); savedPhase = null; overlay.hidden = true;}
      else if (name === 'rules') {showModal('守关军令', '<p>从25名武将中选至多10名，本关敌将禁选。每名武将每局最多招募一次。</p><p>备战可免费调位；开战后位置固定。Boss突破扣15城防，城防归零才失败。</p><p>进军倍率影响敌军和出兵节奏，武将攻速保持。暂停不计通关用时。</p><small>此处为设计演示；规则阅读会暂停当前预览。</small>'); return;}
      else if (name === 'result') {showModal('战报示意', '<p>黄巾演武 · 守关告捷</p><div class="proposal-summary"><div><strong>30/30</strong><span>剩余城防</span></div><div><strong>06:12</strong><span>演示用时</span></div><div><strong>0</strong><span>示意漏兵</span></div></div><p>结算样式使用示意数据，不生成真实成绩，也不写入玩家纪录。</p>'); return;}
      else if (name === 'upgrade') {state.hint = state.selected ? `已查看${hero(state.selected).Name}成长。正式数值与机制将接入现有Domain；本页不模拟升级规则。` : '先选择武将，再查看成长。';}
      else if (name === 'page') state.hint = '当前对比统一展示前五名武将；正式游戏保留五卡翻页。';
      render();
    }
    function render() {
      const selected = hero(state.selected);
      const deployed = state.deployed.find(item => item.id === state.selected);
      document.body.dataset.phase = state.phase;
      document.body.dataset.wallet = state.gold <= 24 ? 'low' : 'normal';
      document.body.classList.toggle('has-no-selection', !selected);
      setText('gold', state.gold);
      setText('state', ({preparing: '布阵中', running: '交战中', paused: '已暂停'})[state.phase]);
      setText('wave', `${state.wave} / 8`);
      setText('time', `${String(Math.floor(state.seconds / 60)).padStart(2, '0')}:${String(state.seconds % 60).padStart(2, '0')}`);
      setText('hint', state.hint);
      setText('selected-name', selected?.Name || '尚未选将');
      setText('selected-role', selected?.Role || '选择武将查看部署与成长');
      setText('selected-cost', selected ? `${selected.Cost}` : '—');
      setText('upgrade-title', selected ? `${selected.Name} · ${deployed ? `${deployed.level}阶守将` : '待上阵'}` : '选择一名武将');
      setText('upgrade-note', selected ? selected.Description : '查看武将定位与军饷，再选择空阵位。');
      document.querySelectorAll('[data-action="toggle-run"]').forEach(el => {el.textContent = state.phase === 'preparing' ? '开始守关' : state.phase === 'running' ? '暂停' : '继续守关';});
      document.querySelectorAll('[data-action="speed"]').forEach(el => {el.textContent = `进军 ${state.speed}×`;});
      document.querySelectorAll('[data-action="upgrade"]').forEach(el => {el.disabled = !selected; el.textContent = !selected ? '先选择武将' : deployed ? '查看成长' : state.gold < selected.Cost ? `还差 ${selected.Cost - state.gold} 军饷` : `选择阵位 · 招募 ${selected.Cost}`;});
      document.querySelectorAll('[data-selected-portrait],[data-selected-character]').forEach(el => {
        el.hidden = !selected;
        if (selected) el.src = `assets/${el.hasAttribute('data-selected-portrait') ? 'portraits' : 'characters'}/${selected.Id}.png`;
      });
      document.querySelectorAll('[data-hero]').forEach(button => {
        const item = hero(button.dataset.hero);
        const isDeployed = state.deployed.some(entry => entry.id === item.Id);
        button.classList.toggle('is-selected', item.Id === state.selected);
        button.classList.toggle('is-deployed', isDeployed);
        button.classList.toggle('is-unaffordable', !isDeployed && state.gold < item.Cost);
        button.setAttribute('aria-pressed', String(item.Id === state.selected));
        button.querySelectorAll('[data-card-status]').forEach(el => {el.textContent = isDeployed ? '已上阵' : state.gold < item.Cost ? `还差 ${item.Cost - state.gold}` : '可招募';});
      });
      field.querySelectorAll('.scene-unit,.scene-label,.scene-enemy').forEach(el => el.remove());
      for (const item of state.deployed) {
        const p = point(slot(item.slot).Position);
        const unit = document.createElement('div'); unit.className = 'scene-unit';
        // 上沿阵位在设计预览中缩小人物以保留完整轮廓，足点仍与正式阵位一致。
        const safeWidth = Math.min(field.clientWidth * 0.105, field.clientHeight * p.y / 100 / 0.87);
        Object.assign(unit.style, {left: `${p.x}%`, top: `${p.y}%`, width: `${safeWidth}px`});
        unit.innerHTML = `<img src="assets/characters/${item.id}.png" alt="${hero(item.id).Name}">`;
        const label = document.createElement('div'); label.className = 'scene-label';
        Object.assign(label.style, {left: `${p.x}%`, top: `${p.y + 2.5}%`});
        label.innerHTML = `${hero(item.id).Name}<span>${item.level}阶</span>`;
        field.append(unit, label);
      }
      if (state.phase !== 'preparing') {
        for (const [x, y, kind] of [[400,300,'infantry'],[510,300,'infantry'],[630,300,'archer'],[1030,300,'infantry']]) {
          const p = point({X:x,Y:y}); const enemy = document.createElement('div'); enemy.className = 'scene-enemy';
          Object.assign(enemy.style,{left:`${p.x}%`,top:`${p.y}%`});
          enemy.innerHTML=`<img src="assets/enemies/${kind}.png" alt="敌军示意">`;field.append(enemy);
        }
      }
      field.querySelectorAll('.scene-slot').forEach(button => {
        button.classList.toggle('is-occupied', state.deployed.some(item => item.slot === Number(button.dataset.slot)));
        button.classList.toggle('is-candidate', selected && !deployed && Number(button.dataset.slot) === state.candidate);
      });
      const ring = field.querySelector('.scene-coverage');
      ring.hidden = !selected;
      const anchor = point(slot(deployed?.slot || state.candidate).Position);
      Object.assign(ring.style, {left: `${anchor.x}%`, top: `${anchor.y}%`});
      board.dataset.ready = 'true';
      window.parent.postMessage({type:'proposal-state',phase:state.phase,selected:state.selected,gold:state.gold}, '*');
    }
    document.querySelectorAll('[data-hero]').forEach(button => button.addEventListener('click', () => {
      state.selected = button.dataset.hero;
      state.hint = state.deployed.some(item => item.id === state.selected) ? `已查看${hero(state.selected).Name}，备战可点空阵位调位。` : `已选${hero(state.selected).Name}，选择空阵位招募。`;
      render();
    }));
    document.querySelectorAll('[data-action]').forEach(button => button.addEventListener('click', () => action(button.dataset.action)));
    window.addEventListener('message', event => {if (event.source === window.parent && event.data?.type === 'proposal-command') action(event.data.action);});
    render(); fit();
  }
  const script = document.createElement('script');
  script.src = 'assets/data.js'; script.onload = ready;
  script.onerror = () => {const board = document.getElementById('artboard'); if (board) {const error = document.createElement('p');error.setAttribute('role','alert');error.style.cssText='position:absolute;inset:25% 20%;z-index:100;padding:30px;background:#fff0d3;color:#7d352b';error.textContent='预览资源未能加载。请保持assets文件夹与HTML在同一目录，再重新打开。';board.append(error);}};
  document.head.append(script);
})();
