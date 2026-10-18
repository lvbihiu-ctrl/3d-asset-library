(function () {
'use strict';

const $ = id => document.getElementById(id);
const xss = value => String(value ?? '').replace(/[&<>"']/g, character => ({
  '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
}[character]));
const icon = name => `<svg class="icon" aria-hidden="true"><use href="#i-${xss(name)}"></use></svg>`;
const syntheticCategories = ['产品渲染', '生活场景', '工业设计', '灯具道具', '材质样本', '未分类'];
let cats = syntheticCategories.slice();
const paths = ['资产盘 / 家具', '资产盘 / 产品', '项目盘 / 练习素材', '资产盘 / 灯具'];
const shapes = ['立方体', '几何球', '机械环', '展示台', '桌椅组合', '工作灯', '玻璃瓶', '工业零件', '渲染场景', '材质球'];

function syntheticCover(id) {
  const n = Number(id);
  const h = (n * 37) % 360;
  const shade = (h + 50) % 360;
  const variant = n % 3;
  const art = variant === 0
    ? '<ellipse cx="135" cy="214" rx="95" ry="20" fill="#1a2a4350"/><path d="M83 116 155 75 220 111 147 157Z" fill="url(#front)"/><path d="M83 116 147 157 147 242 83 201Z" fill="url(#side)"/><path d="M147 157 220 111 220 198 147 242Z" fill="#ffffff99"/>'
    : variant === 1
      ? '<ellipse cx="150" cy="213" rx="81" ry="20" fill="#1a2a4350"/><circle cx="150" cy="149" r="82" fill="url(#front)"/><circle cx="132" cy="123" r="53" fill="#ffffff55"/><ellipse cx="150" cy="223" rx="68" ry="11" fill="#ffffff80"/>'
      : '<ellipse cx="147" cy="211" rx="100" ry="18" fill="#1a2a4350"/><path d="M85 183c0-87 120-129 140-49 9 39-51 88-95 74-24-8-20-42-4-60 16-18 50-30 64-10 12 17-20 32-35 31" fill="none" stroke="url(#front)" stroke-width="34" stroke-linecap="round"/><path d="M108 232h80" stroke="#ffffffc0" stroke-width="8"/>';
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="360" height="300" viewBox="0 0 300 260"><defs><linearGradient id="bg" x1="0" y1="0" x2="1" y2="1"><stop stop-color="hsl(${h} 35% 93%)"/><stop offset="1" stop-color="hsl(${shade} 30% 82%)"/></linearGradient><linearGradient id="front" x1="0" y1="0" x2="1" y2="1"><stop stop-color="hsl(${h} 52% 78%)"/><stop offset="1" stop-color="hsl(${h} 35% 48%)"/></linearGradient><linearGradient id="side" x1="0" y1="0" x2="1" y2="1"><stop stop-color="hsl(${shade} 36% 55%)"/><stop offset="1" stop-color="hsl(${shade} 28% 37%)"/></linearGradient></defs><rect width="300" height="260" fill="url(#bg)"/><circle cx="255" cy="38" r="48" fill="#fff" fill-opacity=".2"/>${art}<text x="12" y="246" font-family="Arial" font-size="12" fill="#526174">SYNTHETIC · ${String(n).padStart(3, '0')}</text></svg>`;
  return 'data:image/svg+xml;charset=UTF-8,' + encodeURIComponent(svg);
}

function missingCover(extension) {
  const label = xss(extension || '3D');
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="720" height="520" viewBox="0 0 720 520"><defs><linearGradient id="g" x1="0" y1="0" x2="1" y2="1"><stop stop-color="#edf2f7"/><stop offset="1" stop-color="#d8e1ec"/></linearGradient></defs><rect width="720" height="520" fill="url(#g)"/><path d="m360 126 116 65v138l-116 65-116-65V191Z" fill="none" stroke="#8291a5" stroke-width="18"/><path d="m244 191 116 67 116-67M360 258v136" fill="none" stroke="#8291a5" stroke-width="14"/><text x="360" y="470" text-anchor="middle" fill="#66768b" font-family="Segoe UI, sans-serif" font-size="28">${label} · 无同名封面</text></svg>`;
  return 'data:image/svg+xml;charset=UTF-8,' + encodeURIComponent(svg);
}

const stress = Number(window.__syntheticCount || new URLSearchParams(location.search).get('synthetic'));
const count = [100, 1000].includes(stress) ? stress : 15;
let items = Array.from({ length: count }, (_, index) => ({
  id: String(index + 1),
  name: shapes[index % shapes.length] + ' · 合成资产 ' + String(index + 1).padStart(3, '0'),
  category: cats[index % cats.length],
  folder: paths[index % paths.length],
  tag: index % 2 ? '练习' : '展示',
  source: 'synthetic_' + (index + 1) + '.svg',
  extension: 'SVG',
  ratio: [1.32, 1.15, 1.42, 1.22][index % 4],
  src: syntheticCover(index + 1),
  fav: false,
  hasCover: true,
  real: false,
}));

const state = {
  layout: 'board', filter: 'all', chip: '全部', query: '', selected: items[0]?.id ?? null,
  checked: new Set(), dark: false,
};
const cardSizeKey = 'assetlibrary.card-size.v1';
let cardSize = 260;
try {
  const savedSize = Number(localStorage.getItem(cardSizeKey));
  if (Number.isFinite(savedSize) && savedSize >= 160 && savedSize <= 480) cardSize = savedSize;
} catch { /* Storage may be unavailable in standalone previews. */ }
$('app').style.setProperty('--card-size', cardSize + 'px');
document.querySelector('.main').title = 'Ctrl + 滚轮：缩放素材卡片';
let toastTimeout;
let realMode = false;
let librarySummary = null;
let hostInfo = null;
let scanning = false;
let clipboardBusy = false;
let imageResults = null;
let masonryFrame = 0;
let dragPending = false;
let dragCandidate = null;
let suppressClickUntil = 0;

function scheduleMasonry() {
  if (masonryFrame) return;
  masonryFrame = requestAnimationFrame(() => {
    masonryFrame = 0;
    const area = $('assets');
    const cards = [...area.querySelectorAll('article.asset')];
    area.style.height = '';
    if (state.layout !== 'board' || !cards.length) return;
    const gap = 14;
    const width = Math.min(cardSize, area.clientWidth);
    const columns = Math.min(cards.length, Math.max(1, Math.floor((area.clientWidth + gap) / (width + gap))));
    const bottoms = Array(columns).fill(0);
    cards.forEach(card => { card.style.width = width + 'px'; });
    const heights = cards.map(card => card.offsetHeight);
    cards.forEach((card, index) => {
      const column = bottoms.indexOf(Math.min(...bottoms));
      card.style.left = column * (width + gap) + 'px';
      card.style.top = bottoms[column] + 'px';
      bottoms[column] += heights[index] + gap;
    });
    area.style.height = Math.max(0, Math.max(...bottoms) - gap) + 'px';
  });
}

document.querySelector('.main').addEventListener('wheel', event => {
  if (!event.ctrlKey || !event.deltaY) return;
  event.preventDefault();
  if (dragPending) return;
  cardSize = Math.max(160, Math.min(480, cardSize + (event.deltaY < 0 ? 24 : -24)));
  $('app').style.setProperty('--card-size', cardSize + 'px');
  try { localStorage.setItem(cardSizeKey, String(cardSize)); } catch { /* Nonfatal preference write. */ }
  scheduleMasonry();
}, { passive: false });

function canDragAsset() {
  return realMode && !scanning && !clipboardBusy && hostInfo?.capabilities?.includes('beginAssetDrag');
}
document.addEventListener('pointermove', async event => {
  if (!dragCandidate || dragPending || !(event.buttons & 1)) return;
  if (Math.hypot(event.clientX - dragCandidate.x, event.clientY - dragCandidate.y) < 8) return;
  const assetId = dragCandidate.assetId;
  dragCandidate = null;
  if (!canDragAsset()) return;
  dragPending = true;
  document.documentElement.dataset.dragState = 'dragging';
  try {
    const result = await host('beginAssetDrag', { assetId });
    document.documentElement.dataset.dragState = result.dropped ? 'dropped' : 'cancelled';
  } catch (error) {
    document.documentElement.dataset.dragState = 'error';
    toast(error.message, 'error');
  } finally {
    dragPending = false;
    suppressClickUntil = performance.now() + 250;
  }
});
document.addEventListener('pointerup', () => { dragCandidate = null; });
document.addEventListener('pointercancel', () => { dragCandidate = null; });
window.addEventListener('blur', () => { dragCandidate = null; });
// Never let Chromium turn a cover into a URL or image-file drag payload.
document.addEventListener('dragstart', event => {
  if (event.target.closest?.('.asset,.inspectCover')) event.preventDefault();
});

function toast(message, type = 'info') {
  $('toast').textContent = message;
  $('toast').className = 'toast show' + (type === 'error' ? ' error' : '');
  clearTimeout(toastTimeout);
  toastTimeout = setTimeout(() => { $('toast').className = 'toast'; }, 3200);
}

function matchesFilters(asset) {
  const query = state.query.toLocaleLowerCase().trim();
  const position = items.indexOf(asset);
  const filterMatches = state.filter === 'all'
    || (state.filter === 'favorite' && asset.fav)
    || (state.filter === 'recent' && position >= Math.max(0, items.length - 5))
    || (state.filter === 'unclassified' && asset.category === '未分类')
    || (state.filter.startsWith('cat:') && asset.category === state.filter.slice(4))
    || (state.filter.startsWith('root:') && asset.folder.startsWith(state.filter.slice(5)));
  const chipMatches = state.chip === '全部' || asset.category === state.chip || asset.tag === state.chip;
  const searchMatches = !query || [asset.name, asset.category, asset.folder, asset.tag, asset.source, asset.extension]
    .some(value => String(value).toLocaleLowerCase().includes(query));
  return filterMatches && chipMatches && searchMatches && (!imageResults || imageResults.has(asset.id));
}

function navButton(key, iconName, label, number) {
  const active = state.filter === key;
  return `<button type="button" data-filter="${xss(key)}" class="${active ? 'active' : ''}" aria-pressed="${active}"><span class="left">${icon(iconName)}<span>${xss(label)}</span></span><span class="num">${number}</span></button>`;
}

function renderNavigation() {
  const definitions = [
    ['all', 'library', '全部素材', items.length],
    ['recent', 'clock', '最近扫描', Math.min(5, items.length)],
  ];
  if (!realMode) {
    definitions.splice(1, 0, ['favorite', 'heart', '我的收藏', items.filter(asset => asset.fav).length]);
    definitions.push(['unclassified', 'diamond', '未分类', items.filter(asset => asset.category === '未分类').length]);
  }
  $('special-nav').innerHTML = definitions.map(definition => navButton(...definition)).join('');
  $('nav-label').textContent = realMode ? '格式 / 素材文件夹' : '虚拟分类 / 素材文件夹';
  const roots = [...new Set(items.map(asset => asset.folder.split(/[\\/]/)[0]).filter(Boolean))];
  $('category-nav').innerHTML = cats.map(category => navButton(
    'cat:' + category, 'layers', category, items.filter(asset => asset.category === category).length
  )).join('') + `<div class="navtitle" style="padding-top:17px">素材文件夹${realMode ? '（只读）' : '（合成）'}</div>`
    + roots.map(root => navButton(
      'root:' + root, 'folder', root, items.filter(asset => asset.folder.startsWith(root)).length
    )).join('');
  document.querySelectorAll('.nav [data-filter]').forEach(element => {
    element.onclick = () => {
      state.filter = element.dataset.filter;
      state.chip = '全部';
      render();
    };
  });
}

function renderChips() {
  $('chips').innerHTML = ['全部', ...cats].map(category => {
    const active = state.chip === category;
    return `<button type="button" class="chip ${active ? 'active' : ''}" data-chip="${xss(category)}" aria-pressed="${active}">${xss(category)}</button>`;
  }).join('');
  $('chips').querySelectorAll('button').forEach(element => {
    element.onclick = () => {
      state.chip = element.dataset.chip;
      render();
    };
  });
}

function resetFilters() {
  imageResults = null;
  state.filter = 'all';
  state.query = '';
  state.chip = '全部';
  $('search').value = '';
  render();
}

function renderCards() {
  const visible = items.filter(matchesFilters);
  if (imageResults) visible.sort((a, b) => imageResults.get(a.id) - imageResults.get(b.id));
  const focusId = visible.some(asset => asset.id === state.selected) ? state.selected : visible[0]?.id;
  const filterTitle = state.filter === 'all' ? '全部素材'
    : state.filter === 'favorite' ? '我的收藏'
      : state.filter === 'recent' ? '最近扫描'
        : state.filter === 'unclassified' ? '未分类'
          : state.filter.slice(state.filter.indexOf(':') + 1);
  $('pageName').textContent = imageResults ? '以图搜图 · 基础相似度' : filterTitle;
  $('pageCount').textContent = realMode
    ? `${visible.length} / ${items.length} 个真实模型 · ${librarySummary?.rootName || '本地素材库'}`
    : `${visible.length} 条合成测试记录 · 未读取本机模型`;
  if (realMode) {
    const errors = Number(librarySummary?.errorCount || 0);
    $('hintline').innerHTML = imageResults
      ? `${icon('info')}<span><b>dHash 基础相似度排序：</b>差异越小越靠前，不代表语义识别。点击“全部素材”退出搜图。</span>`
      : `${icon('info')}<span><b>真实素材只读索引：</b>${xss(librarySummary?.root || '')} · ${items.length} 个模型 · ${librarySummary?.rootAvailable === false ? '磁盘未连接：保留索引和缓存封面，请重新连接后定位' : errors ? `${errors} 个目录无法读取` : '索引已载入'}。选中素材后 Ctrl+V 设置封面；以图搜图请用专门按钮。C4D 拖入尚未完成。</span>`;
  } else {
    $('hintline').innerHTML = `${icon('info')}<span>当前仅用于验证界面与交互；<b>未连接真实素材库，C4D 拖入尚未完成。</b></span>`;
  }

  const emptyMessage = realMode && items.length === 0
    ? '<div class="empty"><strong>没有扫描到支持的模型</strong><span>当前只读扫描 .c4d、.fbx 和 .obj。可以重新选择其他素材文件夹。</span></div>'
    : '<div class="empty"><strong>没有找到匹配素材</strong><span>可以清除搜索和分类筛选后重新查看。</span><button type="button" class="toolbtn" id="clearFiltersEmpty">' + icon('refresh') + '清除筛选</button></div>';
  $('assets').innerHTML = visible.length ? visible.map(asset => {
    const syntheticControls = realMode ? '' : `<input class="multi" type="checkbox" data-check="${xss(asset.id)}" ${state.checked.has(asset.id) ? 'checked' : ''} aria-label="批量选择 ${xss(asset.name)}">`;
    const favorite = realMode ? '' : `<button type="button" class="heart ${asset.fav ? 'on' : ''}" data-fav="${xss(asset.id)}" aria-label="${asset.fav ? '取消收藏' : '收藏'} ${xss(asset.name)}" aria-pressed="${asset.fav}">${icon('heart')}</button>`;
    return `<article role="listitem" aria-current="${asset.id === state.selected}" class="asset ${asset.id === state.selected ? 'selected' : ''}" data-id="${xss(asset.id)}" tabindex="${asset.id === focusId ? '0' : '-1'}" style="--ar:${asset.ratio}"><div class="cover"><img src="${xss(asset.src)}" alt="${xss(asset.name)}" loading="lazy"><span class="mark">${imageResults ? '差异 ' + imageResults.get(asset.id) + '/64' : realMode ? (asset.hasCover ? '真实封面' : '暂无封面') : '合成封面 · 未关联模型'}</span>${syntheticControls}</div><div class="assetinfo"><div class="aname" title="${xss(asset.name)}">${xss(asset.name)}</div><div class="origin">${realMode ? `${xss(asset.extension)} · ${xss(asset.folder)}` : '合成数据 · SVG'}</div><div class="tags"><span class="tag">${xss(asset.category)}</span><span class="tag">${xss(asset.tag)}</span></div><div class="assetfoot"><span class="model-state">${icon('box')}${realMode ? '原模型只读' : '无实际模型'}</span>${favorite}</div></div></article>`;
  }).join('') : emptyMessage;

  if (realMode && canDragAsset() && !imageResults && librarySummary?.rootAvailable !== false) {
    $('hintline').innerHTML = `${icon('info')}<span>真实素材只读索引 · 拖动卡片可拖出原模型；选中后 Ctrl+V 设置封面。</span>`;
  }
  const cardElements = [...$('assets').querySelectorAll('article.asset')];
  cardElements.forEach((element, index) => {
    const image = element.querySelector('img');
    image.draggable = false;
    const measureImage = () => {
      const asset = visible[index];
      if (!asset.real || !image.naturalWidth || !image.naturalHeight) return;
      asset.ratio = image.naturalWidth / image.naturalHeight;
      element.style.setProperty('--ar', asset.ratio);
      scheduleMasonry();
    };
    image.addEventListener('load', measureImage);
    if (image.complete) measureImage();
    element.addEventListener('pointerdown', event => {
      if (event.button !== 0 || event.target.closest('button,input') || !canDragAsset()) return;
      state.selected = element.dataset.id;
      cardElements.forEach(card => {
        const selected = card === element;
        card.classList.toggle('selected', selected);
        card.setAttribute('aria-current', String(selected));
        card.tabIndex = selected ? 0 : -1;
      });
      renderDetail();
      updateCapabilityControls();
      dragCandidate = { assetId: element.dataset.id, x: event.clientX, y: event.clientY };
    });
    const select = () => {
      state.selected = element.dataset.id;
      renderCards();
      renderDetail();
      updateCapabilityControls();
    };
    element.addEventListener('click', event => {
      if (!dragPending && performance.now() > suppressClickUntil && !event.target.closest('button,input')) select();
    });
    element.addEventListener('keydown', event => {
      if (event.key === 'Enter' || event.key === ' ') {
        event.preventDefault();
        select();
        return;
      }
      const moves = { ArrowRight: 1, ArrowDown: 1, ArrowLeft: -1, ArrowUp: -1, Home: -index, End: cardElements.length - 1 - index };
      if (!(event.key in moves)) return;
      event.preventDefault();
      const next = cardElements[Math.max(0, Math.min(cardElements.length - 1, index + moves[event.key]))];
      cardElements.forEach(card => { card.tabIndex = card === next ? 0 : -1; });
      next.focus();
    });
  });
  $('assets').querySelectorAll('[data-fav]').forEach(element => {
    element.onclick = event => {
      event.stopPropagation();
      const asset = items.find(item => item.id === element.dataset.fav);
      asset.fav = !asset.fav;
      render();
      toast('仅修改合成测试状态，不写入本机素材库');
    };
  });
  $('assets').querySelectorAll('[data-check]').forEach(element => {
    element.onchange = () => {
      const id = element.dataset.check;
      if (element.checked) state.checked.add(id); else state.checked.delete(id);
      renderBatch();
    };
  });
  const clear = $('clearFiltersEmpty');
  if (clear) clear.onclick = resetFilters;
  renderBatch();
  scheduleMasonry();
}

function renderBatch() {
  const ids = [...state.checked];
  $('batchbar').style.display = !realMode && ids.length ? 'flex' : 'none';
  if (realMode || !ids.length) return;
  $('batchbar').innerHTML = `<strong>已选 ${ids.length} 条</strong><span>批量分类（临时）</span><select id="batchcat">${cats.map(category => `<option>${xss(category)}</option>`).join('')}</select><button id="batchapply">应用</button><button id="batchclear">取消</button>`;
  $('batchapply').onclick = () => {
    const category = $('batchcat').value;
    items.forEach(asset => { if (state.checked.has(asset.id)) asset.category = category; });
    state.checked.clear();
    render();
    toast('仅改变当前合成页面的分类');
  };
  $('batchclear').onclick = () => { state.checked.clear(); renderCards(); };
}

function formatBytes(bytes) {
  const value = Number(bytes || 0);
  if (value < 1024) return `${value} B`;
  if (value < 1024 * 1024) return `${(value / 1024).toFixed(1)} KB`;
  return `${(value / 1024 / 1024).toFixed(1)} MB`;
}

function renderDetail() {
  const asset = items.find(item => item.id === state.selected) || items[0];
  if (!asset) {
    $('detail').innerHTML = '<div class="empty"><strong>暂无素材</strong><span>点击“导入素材文件夹”选择一个真实素材根目录。</span></div>';
    return;
  }
  if (realMode) {
    const modified = asset.modifiedUtc ? new Date(asset.modifiedUtc).toLocaleString() : '未知';
    $('detail').innerHTML = `<div class="inspectCover"><img src="${xss(asset.src)}" alt="${xss(asset.name)}"></div><div class="detailName">${xss(asset.name)}</div><div class="small">${asset.hasCover ? '已关联真实封面 · Ctrl+V 可更换' : '选中后 Ctrl+V 粘贴图片设置封面'}</div><div class="rule"></div><div class="fieldLabel">原文件（只读）</div><div class="small">${xss(asset.relativePath)}</div><div class="rule"></div><div class="small">格式：<b>${xss(asset.extension)}</b><br>大小：${xss(formatBytes(asset.sizeBytes))}<br>修改时间：${xss(modified)}</div><div class="rule"></div><button type="button" class="toolbtn primary" id="detailLocate">${icon('locate')}在资源管理器中定位</button><div class="unavailable-callout">${icon('info')}<span><b>C4D 拖入尚未接入</b><br>Issue #15 仍待实机验证。</span></div><div class="inspectNote">封面和索引保存于独立缓存，不修改原素材。</div>`;
    $('detailLocate').onclick = locateSelected;
    $('detail').querySelector('img').draggable = false;
    if (canDragAsset()) $('detail').querySelector('.unavailable-callout').innerHTML = `${icon('info')}<span><b>拖动左侧卡片，拖出原模型</b><br>在 C4D 中按住 Shift 拖入可尝试合并；接收结果以 C4D 为准。</span>`;
    return;
  }
  $('detail').innerHTML = `<div class="inspectCover"><img src="${xss(asset.src)}" alt="${xss(asset.name)}"></div><div class="detailName">${xss(asset.name)}</div><div class="small">预览来源：程序合成 SVG</div><div class="rule"></div><div class="fieldLabel">素材文件夹（合成）</div><div class="small">${xss(asset.folder)}</div><div class="rule"></div><div class="fieldLabel">虚拟分类（仅当前会话）</div><select id="setcat" aria-label="虚拟分类">${cats.map(category => `<option ${asset.category === category ? 'selected' : ''}>${xss(category)}</option>`).join('')}</select><div class="rule"></div><div class="small">模型文件：<b>未关联</b></div><div class="unavailable-callout">${icon('info')}<span><b>C4D 拖入尚未接入</b><br>Issue #15 仍待实机验证。</span></div><div class="inspectNote">真实文件必须由 C# 从本地素材 ID 校验后发出，网页不会合成路径冒充模型。</div>`;
  $('setcat').onchange = event => {
    asset.category = event.target.value;
    render();
    toast('合成样本已分类，真实数据未修改');
  };
}

function render() {
  $('app').className = 'app ' + (state.layout === 'list' ? 'listmode' : state.layout === 'board' ? 'boardmode' : 'gridmode');
  for (const [id, value] of [['gridBtn', 'grid'], ['listBtn', 'list'], ['boardBtn', 'board']]) {
    const active = state.layout === value;
    $(id).classList.toggle('active', active);
    $(id).setAttribute('aria-pressed', String(active));
  }
  renderNavigation();
  renderChips();
  renderCards();
  renderDetail();
  updateCapabilityControls();
}

const nativeHost = window.assetLibraryHost;
const capabilityControls = {
  pasteCover: 'setCoverFromClipboard',
  findImage: 'searchSimilarClipboard',
  locateFile: 'locateAsset',
  importFolder: 'importFolder',
  checkUpdate: 'checkForUpdates',
};

function setNativeStatus(stateName, message) {
  const mode = $('nativeMode');
  mode.dataset.state = stateName;
  const text = mode.querySelector('.mode-text');
  if (text) text.textContent = message; else mode.textContent = message;
}

function updateCapabilityControls() {
  const capabilities = new Set(Array.isArray(hostInfo?.capabilities) ? hostInfo.capabilities : []);
  for (const [id, capability] of Object.entries(capabilityControls)) {
    const control = $(id);
    let enabled = capabilities.has(capability);
    if (id === 'checkUpdate') enabled = enabled && capabilities.has('installUpdate');
    if (id === 'locateFile') enabled = enabled && realMode && Boolean(state.selected);
    if (id === 'importFolder') enabled = enabled && !scanning;
    if (id === 'pasteCover') enabled = enabled && realMode && Boolean(state.selected);
    if (id === 'findImage') enabled = enabled && realMode && items.length > 0;
    if (['pasteCover', 'findImage', 'importFolder'].includes(id)) enabled = enabled && !clipboardBusy && !scanning;
    control.disabled = !enabled;
    control.title = enabled ? '' : id === 'checkUpdate' ? '更新服务尚未接入' : id === 'locateFile' ? '请先选择真实素材' : '该本地能力尚未接入';
  }
}

function applyCapabilities(info) {
  hostInfo = info;
  updateCapabilityControls();
  const capabilities = new Set(Array.isArray(info?.capabilities) ? info.capabilities : []);
  const realCount = [...capabilities].filter(value => value !== 'getHostInfo').length;
  setNativeStatus(realCount ? 'connected' : 'limited', realCount
    ? `Windows 客户端已连接 · ${realCount} 项本地能力可用`
    : 'Windows 客户端已连接 · 未连接真实素材库');
}

async function connectNativeHost() {
  if (!nativeHost) {
    setNativeStatus('offline', '合成数据 · 未连接真实素材库');
    return;
  }
  setNativeStatus('connecting', '正在连接 Windows 客户端…');
  try {
    const info = await nativeHost.request('getHostInfo', {});
    applyCapabilities(info);
    if (info.capabilities?.includes('getLibrary')) {
      const saved = await host('getLibrary');
      if (saved?.catalog) {
        applyRealCatalog(saved.catalog);
        setNativeStatus('connected', `真实素材库已恢复 · ${items.length} 条记录`);
      }
    }
  } catch (error) {
    setNativeStatus('error', `Windows 客户端连接失败：${error.message}`);
  } finally {
    document.documentElement.dataset.libraryReady = 'true';
  }
}

function host(action, payload = {}) {
  if (!nativeHost) return Promise.reject(Error('当前是 HTML 预览，或尚未连接支持此功能的 Windows 原生服务。'));
  return nativeHost.request(action, payload);
}

const flow = window.NativeUpdateFlow.createUpdateFlow({
  callNative: action => host(action),
  confirmInstall: async () => window.confirm('是否确认安装已校验的新版本并重启桌面程序？'),
});

function showUpdate(updateState) {
  $('updateStatus').textContent = updateState.message;
  $('nativeUpdate').disabled = !updateState.canInstall;
  $('checkUpdate').disabled = updateState.status === 'checking';
}

$('checkUpdate').onclick = async () => {
  $('updateDialog').classList.add('open');
  showUpdate(await flow.check());
};
$('nativeUpdate').onclick = async () => showUpdate(await flow.install());
$('closeUpdate').onclick = () => $('updateDialog').classList.remove('open');
$('updateDialog').addEventListener('click', event => {
  if (event.target === $('updateDialog')) $('updateDialog').classList.remove('open');
});
document.addEventListener('keydown', event => {
  if (event.key === 'Escape') $('updateDialog').classList.remove('open');
});

async function nativeOperation(action, payload = {}) {
  try {
    return await host(action, payload);
  } catch (error) {
    toast(error.message, 'error');
    return null;
  }
}

function applyRealCatalog(catalog) {
  imageResults = null;
  realMode = true;
  librarySummary = catalog;
  cats = [...new Set(catalog.assets.map(asset => asset.extension))].sort();
  items = catalog.assets.map(asset => ({
    ...asset,
    id: String(asset.id),
    category: asset.extension,
    tag: asset.hasCover ? '有封面' : '缺封面',
    source: asset.fileName,
    ratio: 1.34,
    src: asset.hasCover ? `https://assetlibrary-covers.local/${encodeURIComponent(asset.id)}` : missingCover(asset.extension),
    fav: false,
    real: true,
  }));
  state.filter = 'all';
  state.chip = '全部';
  state.query = '';
  state.selected = items[0]?.id ?? null;
  state.checked.clear();
  $('search').value = '';
  $('assets').setAttribute('aria-label', '真实素材');
  const navFoot = $('navFoot');
  if (navFoot) navFoot.innerHTML = `只读扫描：${xss(catalog.rootName)}<br>不会移动、重命名或修改原文件。`;
  render();
}

async function importFolder() {
  if (scanning) return;
  scanning = true;
  updateCapabilityControls();
  setNativeStatus('connecting', '请选择素材文件夹；确认后开始只读扫描…');
  $('hintline').innerHTML = `${icon('info')}<span><b>等待选择并扫描素材文件夹…</b> 原文件不会被修改。</span>`;
  const result = await nativeOperation('importFolder');
  scanning = false;
  updateCapabilityControls();
  if (!result) {
    setNativeStatus('error', '素材扫描失败；请检查磁盘连接和读取权限');
    return;
  }
  if (result.cancelled) {
    setNativeStatus('connected', realMode ? '已保留当前真实素材库' : '已取消选择 · 仍显示合成数据');
    render();
    return;
  }
  applyRealCatalog(result.catalog);
  setNativeStatus('connected', `真实素材库已连接 · ${items.length} 个模型`);
  toast(`只读扫描完成：${items.length} 个模型`);
}

async function locateSelected() {
  if (!realMode || !state.selected) {
    toast('请先选择真实素材文件夹和素材卡片', 'error');
    return;
  }
  const result = await nativeOperation('locateAsset', { assetId: state.selected });
  if (result?.located) toast('已在资源管理器中定位原文件');
}

window.addEventListener('assetlibrary:scanProgress', event => {
  if (!scanning) return;
  const progress = event.detail || {};
  const directories = Number(progress.directoryCount || 0);
  const assets = Number(progress.assetCount || 0);
  const errors = Number(progress.errorCount || 0);
  const folder = String(progress.currentFolder || '.');
  setNativeStatus('connecting', `正在只读扫描 · ${assets} 个模型 · ${directories} 个文件夹`);
  $('hintline').innerHTML = `${icon('info')}<span><b>扫描进行中：</b>${assets} 个模型 · ${directories} 个文件夹 · ${errors} 个读取错误<br><span class="small">${xss(folder)}</span></span>`;
});

$('pasteCover').onclick = pasteCover;
$('findImage').onclick = searchImage;
async function pasteCover() {
  if (clipboardBusy || scanning || !realMode || !state.selected) return;
  const selectedId = state.selected;
  clipboardBusy = true;
  document.documentElement.dataset.coverState = 'busy';
  updateCapabilityControls();
  try {
    const result = await host('setCoverFromClipboard', { assetId: selectedId });
    const asset = items.find(item => item.id === result.asset.id);
    if (!asset) throw Error('素材选择已变化，请重新选择。');
    Object.assign(asset, result.asset, { tag: '有封面', src: `https://assetlibrary-covers.local/${asset.id}?revision=${result.revision}` });
    imageResults = null;
    render();
    document.documentElement.dataset.coverState = 'saved';
    toast('封面已保存，重启后仍会保留；原模型未修改');
  } catch (error) {
    document.documentElement.dataset.coverState = 'error';
    toast(error.message, 'error');
  } finally {
    clipboardBusy = false;
    updateCapabilityControls();
  }
}
async function searchImage() {
  if (clipboardBusy || scanning || !realMode) return;
  clipboardBusy = true;
  document.documentElement.dataset.imageSearchState = 'busy';
  updateCapabilityControls();
  toast('正在比较素材封面…');
  try {
    const result = await host('searchSimilarClipboard');
    imageResults = new Map(result.matches.map(match => [match.assetId, match.distance]));
    state.query = ''; state.filter = 'all'; state.chip = '全部';
    $('search').value = '';
    state.selected = result.matches[0]?.assetId ?? null;
    render();
    document.documentElement.dataset.imageSearchState = 'complete';
    toast(result.matches.length ? `已按基础相似度排序 ${result.matches.length} 条；${result.skipped} 条无可读封面` : '没有可比较的封面，请先给素材设置封面', result.matches.length ? 'info' : 'error');
  } catch (error) {
    document.documentElement.dataset.imageSearchState = 'error';
    toast(error.message, 'error');
  } finally {
    clipboardBusy = false;
    updateCapabilityControls();
  }
}
document.addEventListener('keydown', event => {
  if (!(event.ctrlKey || event.metaKey) || event.key.toLowerCase() !== 'v' || event.altKey || event.shiftKey) return;
  if (event.target.closest?.('input,textarea,[contenteditable="true"],[contenteditable=""]')) return;
  if ($('pasteCover').disabled) return;
  event.preventDefault();
  pasteCover();
});
$('showAll').onclick = resetFilters;
$('locateFile').onclick = locateSelected;
$('importFolder').onclick = importFolder;
$('gridBtn').onclick = () => { state.layout = 'grid'; render(); };
$('listBtn').onclick = () => { state.layout = 'list'; render(); };
$('boardBtn').onclick = () => { state.layout = 'board'; render(); };
$('search').oninput = event => { state.query = event.target.value; renderCards(); renderDetail(); };
$('theme').onclick = () => {
  state.dark = !state.dark;
  document.body.classList.toggle('dark', state.dark);
  $('theme').setAttribute('aria-pressed', String(state.dark));
  $('theme').querySelector('use').setAttribute('href', state.dark ? '#i-sun' : '#i-moon');
  $('theme').querySelector('.button-label').textContent = state.dark ? '浅色' : '深色';
};

let lastMasonryWidth = -1;
new ResizeObserver(entries => {
  const width = entries[0].contentRect.width;
  if (width !== lastMasonryWidth) { lastMasonryWidth = width; scheduleMasonry(); }
}).observe($('assets'));
render();
connectNativeHost();
})();
