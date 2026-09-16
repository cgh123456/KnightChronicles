const menuList = document.querySelector('#menuList');
const dialog = document.querySelector('#confirmDialog');
const dialogTitle = document.querySelector('#dialogTitle');
const dialogBody = document.querySelector('#dialogBody');
const dialogConfirm = document.querySelector('#dialogConfirm');
const dialogCancel = document.querySelector('#dialogCancel');
const recordCard = document.querySelector('#recordCard');
const warning = document.querySelector('#saveWarning');
const toast = document.querySelector('#toast');

const state = {
  activeRun: false,
  loading: false,
  saveFailed: false,
  focusedIndex: 0,
  transitionLocked: false,
};

const routes = [
  { id: 'expedition', title: '开始远征', firstRunTitle: '开始新游戏', subtitle: '选择角色与出战武器', action: 'expedition' },
  { id: 'meta', title: '局外成长', subtitle: '解锁角色、武器与概率成长', dot: true, route: '局外成长中心' },
  { id: 'archive', title: '图鉴与档案', subtitle: '查看发现、记录与成就', dot: true, route: '图鉴与档案' },
  { id: 'settings', title: '设置', subtitle: '画面、音频与操作', route: '设置' },
  { id: 'help', title: '帮助', subtitle: '了解远征规则', recommend: true, route: '帮助与教程' },
  { id: 'exit', title: '退出游戏', subtitle: '保存当前局外进度后退出', action: 'exit' },
];

function activeRoutes() {
  if (!state.activeRun) return routes;
  return [{
    id: 'continue', title: '继续游戏', subtitle: '灰烬圣堂 · 第 2 层 · 3 分钟前的安全点', action: 'continue', continue: true,
  }, ...routes];
}

function renderMenu() {
  const items = activeRoutes();
  if (state.focusedIndex >= items.length) state.focusedIndex = 0;
  menuList.innerHTML = items.map((item, index) => `
    <button class="menu-button ${item.continue ? 'menu-button--continue' : ''} ${index === state.focusedIndex ? 'is-focused' : ''}"
      type="button" data-index="${index}" data-id="${item.id}">
      <span class="menu-button__index">${String(index + 1).padStart(2, '0')}</span>
      <span class="menu-button__content"><strong>${item.title}</strong><small>${item.subtitle}</small></span>
      <span class="menu-button__right">${item.dot ? '<i class="notification-dot" aria-label="有可用解锁"></i>' : ''}${item.recommend ? '<i class="recommend">推荐查看</i>' : ''}<i class="menu-button__arrow" aria-hidden="true">›</i></span>
    </button>`).join('');
}

function setFocus(index, shouldFocus = false) {
  const items = activeRoutes();
  state.focusedIndex = (index + items.length) % items.length;
  renderMenu();
  if (shouldFocus) menuList.querySelector(`[data-index="${state.focusedIndex}"]`)?.focus();
}

function showToast(message) {
  toast.textContent = message;
  toast.classList.add('is-visible');
  window.clearTimeout(showToast.timeout);
  showToast.timeout = window.setTimeout(() => toast.classList.remove('is-visible'), 2800);
}

function lockTransition(message) {
  if (state.transitionLocked) return;
  state.transitionLocked = true;
  document.querySelector('.home-shell').animate([{ opacity: 1 }, { opacity: .88 }, { opacity: 1 }], { duration: 300 });
  showToast(message);
  window.setTimeout(() => { state.transitionLocked = false; }, 300);
}

function openDangerDialog(kind) {
  const copy = kind === 'new-run'
    ? { title: '放弃未完成远征？', body: '开始新远征将放弃当前未完成远征，且无法继续。局外宝石与解锁进度不会受影响。', button: '放弃并开始' }
    : { title: '确定退出《骑士异闻录》？', body: '将先保存当前局外进度。未完成远征会保留在最近的安全点，供下次继续。', button: '确认退出' };
  dialogTitle.textContent = copy.title;
  dialogBody.textContent = copy.body;
  dialogConfirm.textContent = copy.button;
  dialog.dataset.kind = kind;
  dialog.showModal();
  dialogCancel.focus();
}

function activate(item) {
  if (state.transitionLocked || state.loading) return;
  if (item.action === 'continue') {
    lockTransition('正在恢复「灰烬圣堂」的最近安全点…');
  } else if (item.action === 'expedition') {
    if (state.activeRun) openDangerDialog('new-run');
    else lockTransition('已进入角色与出战配置页。');
  } else if (item.action === 'exit') {
    openDangerDialog('exit');
  } else {
    lockTransition(`已进入「${item.route}」（首页原型中以提示模拟）。`);
  }
}

menuList.addEventListener('click', (event) => {
  const button = event.target.closest('.menu-button');
  if (!button) return;
  const index = Number(button.dataset.index);
  setFocus(index);
  activate(activeRoutes()[index]);
});

menuList.addEventListener('focusin', (event) => {
  const button = event.target.closest('.menu-button');
  if (button) setFocus(Number(button.dataset.index));
});

document.addEventListener('keydown', (event) => {
  if (dialog.open) return;
  if (event.key === 'ArrowDown') { event.preventDefault(); setFocus(state.focusedIndex + 1, true); }
  if (event.key === 'ArrowUp') { event.preventDefault(); setFocus(state.focusedIndex - 1, true); }
  if (event.key === 'Enter' || event.key === ' ') {
    const isMenuFocus = document.activeElement?.closest?.('#menuList');
    if (isMenuFocus || document.activeElement === document.body) { event.preventDefault(); activate(activeRoutes()[state.focusedIndex]); }
  }
  if (event.key === 'ArrowLeft' || event.key === 'ArrowRight') {
    const direction = event.key === 'ArrowLeft' ? '角色展示区' : '游戏入口';
    showToast(`焦点区域：${direction}`);
  }
});

dialogCancel.addEventListener('click', () => dialog.close());
dialog.addEventListener('cancel', (event) => { event.preventDefault(); dialog.close(); });
dialogConfirm.addEventListener('click', () => {
  const kind = dialog.dataset.kind;
  dialog.close();
  if (kind === 'new-run') {
    state.activeRun = false;
    state.focusedIndex = 0;
    renderMenu();
    lockTransition('已放弃未完成远征，进入角色与出战配置页。');
  } else {
    showToast('局外进度已保存。原型环境不会真正退出窗口。');
  }
});

document.querySelector('#toggleRun').addEventListener('click', () => {
  state.activeRun = !state.activeRun;
  state.focusedIndex = 0;
  renderMenu();
  showToast(state.activeRun ? '已模拟有效安全点：显示「继续游戏」。' : '已移除未完成远征：隐藏「继续游戏」。');
});
document.querySelector('#toggleLoading').addEventListener('click', () => {
  state.loading = !state.loading;
  recordCard.classList.toggle('is-loading', state.loading);
  showToast(state.loading ? '战绩数据读取中：显示骨架屏。' : '战绩读取完成。');
});
document.querySelector('#toggleSaveError').addEventListener('click', () => {
  state.saveFailed = !state.saveFailed;
  warning.hidden = !state.saveFailed;
});
document.querySelector('#retrySave').addEventListener('click', () => {
  state.saveFailed = false;
  warning.hidden = true;
  showToast('已成功写入局外档案。');
});
document.querySelectorAll('[data-route]').forEach((button) => button.addEventListener('click', () => lockTransition(`已进入「${button.dataset.route}」（首页原型中以提示模拟）。`)));

renderMenu();
