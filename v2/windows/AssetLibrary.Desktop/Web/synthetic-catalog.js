'use strict';

(() => {
  const palettes = [
    ['#355c7d', '#6c5b7b'], ['#246b78', '#e8b96b'], ['#315c4c', '#a7c957'],
    ['#495867', '#bdd5ea'], ['#5d4e6d', '#c9ada7'], ['#37474f', '#90a4ae'],
  ];
  const names = [
    '弧形休闲椅', '桌面音箱', '玻璃边几', '摄影棚灯', '模块沙发', '金属台灯',
    '展示底座', '盆栽组合', '木质餐椅', '复古相机', '产品展台', '户外长椅',
    '收纳柜', '落地灯', '办公桌', '布艺单椅', '咖啡机', '移动推车',
  ];
  function preview(index, name) {
    const [from, to] = palettes[index % palettes.length];
    const width = 720;
    const height = 440 + (index % 4) * 90;
    const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}" viewBox="0 0 ${width} ${height}">
      <defs><linearGradient id="g" x1="0" y1="0" x2="1" y2="1"><stop stop-color="${from}"/><stop offset="1" stop-color="${to}"/></linearGradient></defs>
      <rect width="100%" height="100%" fill="url(#g)"/><circle cx="${180 + index * 13 % 320}" cy="${150 + index * 17 % 180}" r="110" fill="rgba(255,255,255,.15)"/>
      <rect x="80" y="${height - 150}" width="560" height="80" rx="24" fill="rgba(10,20,30,.24)"/>
      <text x="360" y="${height - 98}" text-anchor="middle" fill="white" font-family="Segoe UI, sans-serif" font-size="34" font-weight="700">${name}</text>
    </svg>`;
    return `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svg)}`;
  }
  const assets = names.map((name, index) => ({
    id: `synthetic-${index + 1}`,
    name,
    rel: `合成素材/分类-${index % 5 + 1}/${name}.c4d`,
    full: `D:\\SyntheticAssetLibrary\\分类-${index % 5 + 1}\\${name}.c4d`,
    kind: '模型',
    ext: 'c4d',
    model: true,
    hasPreview: true,
    preview: preview(index, name),
    category: ['家具', '数码产品', '桌面办公', '灯具', '户外'][index % 5],
    changed: '合成演示数据',
  }));
  window.__ASSET_CATALOG__ = {
    summary: {
      root: '合成演示数据（未读取用户素材）',
      scannedAt: 'Issue #14 Windows 宿主预览',
      reusedPreviews: 18,
      durationSeconds: 0,
    },
    assets,
  };
})();
