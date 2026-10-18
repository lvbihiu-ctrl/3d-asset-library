'use strict';

(() => {
  const transport = window.chrome && window.chrome.webview;
  const pending = new Map();
  let sequence = 0;

  function request(action, payload = {}) {
    if (!transport) return Promise.reject(new Error('当前页面没有 Windows 原生宿主。'));
    const id = `page-${Date.now()}-${++sequence}`;
    return new Promise((resolve, reject) => {
      // Folder scans can legitimately exceed five minutes on large or slow disks.
      // Keep the request pending so the page cannot diverge from the native session.
      const timeoutMs = ['importFolder', 'searchSimilarClipboard', 'setCoverFromClipboard'].includes(action) ? null : 5000;
      const timer = timeoutMs === null ? null : window.setTimeout(() => {
        pending.delete(id);
        reject(new Error('Windows 宿主响应超时。'));
      }, timeoutMs);
      pending.set(id, { resolve, reject, timer });
      transport.postMessage({ id, action, payload });
    });
  }

  if (transport) {
    transport.addEventListener('message', event => {
      const response = event.data;
      if (!response) return;
      if (typeof response.eventName === 'string') {
        window.dispatchEvent(new CustomEvent(`assetlibrary:${response.eventName}`, {
          detail: response.detail || {},
        }));
        return;
      }
      if (typeof response.id !== 'string') return;
      const waiting = pending.get(response.id);
      if (!waiting) return;
      if (waiting.timer !== null) window.clearTimeout(waiting.timer);
      pending.delete(response.id);
      if (response.ok === true) waiting.resolve(response.result);
      else waiting.reject(new Error(response.error?.message || 'Windows 宿主拒绝了请求。'));
    });
  }

  Object.defineProperty(window, 'assetLibraryHost', {
    configurable: false,
    writable: false,
    value: Object.freeze({ request }),
  });

  document.addEventListener('DOMContentLoaded', async () => {
    try {
      const info = await request('getHostInfo', {});
      document.documentElement.dataset.nativeHost = 'connected';
      window.dispatchEvent(new CustomEvent('assetlibrary:hostinfo', { detail: info }));
    } catch (error) {
      document.documentElement.dataset.nativeHost = 'error';
      window.dispatchEvent(new CustomEvent('assetlibrary:hosterror', {
        detail: { message: error.message },
      }));
    }
  });
})();
