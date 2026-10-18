'use strict';

// Preview-only UI controller. The Windows host must validate source URLs, no-cache
// manifests, pinned commit, SHA256, backup and restart before replying.
function createUpdateFlow({callNative, confirmInstall} = {}) {
  let state = {status: 'idle', message: '', canInstall: false};
  const view = () => ({...state});
  function set(status, message, canInstall = false) {
    state = {status, message, canInstall};
    return view();
  }
  function compareVersions(current, latest) {
    const pattern = /^\d+(?:\.\d+){1,3}$/;
    if (!pattern.test(current) || !pattern.test(latest)) throw Error('Windows 返回了无效版本号');
    const a = current.split('.').map(Number), b = latest.split('.').map(Number);
    for (let i = 0; i < Math.max(a.length, b.length); i++) {
      if ((b[i] || 0) > (a[i] || 0)) return 1;
      if ((b[i] || 0) < (a[i] || 0)) return -1;
    }
    return 0;
  }
  async function check() {
    if (typeof callNative !== 'function') {
      return set('error', '仅 Windows 桌面客户端支持检查与安装更新；网页预览不能更新程序。');
    }
    set('checking', '正在通过 Windows 原生服务检查版本…');
    try {
      const result = await callNative('checkForUpdates');
      if (!result || result.verified !== true) throw Error('Windows 未能独立核验更新清单');
      const cmp = compareVersions(String(result.currentVersion ?? ''), String(result.latestVersion ?? ''));
      if (typeof result.available !== 'boolean' || result.available !== (cmp > 0)) {
        throw Error('Windows 返回的版本状态不一致');
      }
      if (cmp > 0) {
        return set('available', '已验证新版本 ' + result.latestVersion + '（当前 ' + result.currentVersion + '）', true);
      }
      return set('latest', '已核验，当前已是最新版 ' + result.currentVersion);
    } catch (err) {
      return set('error', '检查更新失败：' + (err.message || String(err)));
    }
  }
  async function install() {
    if (state.status !== 'available' || !state.canInstall) {
      return set('blocked', '没有经过验证的可安装更新；请先检查版本。');
    }
    if (typeof confirmInstall !== 'function') {
      return set('blocked', '必须先取得用户明确的安装确认。');
    }
    let approved = false;
    try { approved = await confirmInstall(); }
    catch (err) { return set('error', '安装确认失败：' + (err.message || String(err))); }
    if (approved !== true) return set('cancelled', '用户取消了安装。');
    set('installing', 'Windows 正在下载、校验和备份更新…');
    try {
      const result = await callNative('installUpdate');
      if (!result || result.restarting !== true) {
        throw Error('Windows 尚未确认重新启动；未能证明安装完成');
      }
      return set('restarting', '桌面程序已确认更新准备就绪，正在重新启动…');
    } catch (err) {
      return set('error', '安装更新失败：' + (err.message || String(err)) + '。旧版本应由原生更新器保留。');
    }
  }
  return {check, install, getState: view};
}
if (typeof module !== 'undefined' && module.exports) module.exports = {createUpdateFlow};
if (typeof window !== 'undefined') window.NativeUpdateFlow = {createUpdateFlow};
