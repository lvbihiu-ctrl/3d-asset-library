const test = require('node:test');
const assert = require('node:assert/strict');
const { createUpdateFlow } = require('./bridge.js');

test('no native Windows host reports unsupported; never claims latest', async () => {
  const flow = createUpdateFlow({});
  const result = await flow.check();
  assert.equal(result.status, 'error');
  assert.match(result.message, /Windows/);
  assert.equal(result.canInstall, false);
});

test('a trusted newer native manifest enables installing, without installing yet', async () => {
  const calls = [];
  const flow = createUpdateFlow({
    callNative: async action => {
      calls.push(action);
      return { verified: true, currentVersion: '0.1.7', latestVersion: '0.1.8', available: true };
    },
    confirmInstall: async () => false
  });
  const state = await flow.check();
  assert.equal(state.status, 'available');
  assert.equal(state.canInstall, true);
  assert.deepEqual(calls, ['checkForUpdates']);
  assert.equal((await flow.install()).status, 'cancelled');
  assert.deepEqual(calls, ['checkForUpdates']);
});

test('a not independently verified or malformed manifest never enables installation', async () => {
  for (const payload of [
    { verified: false, available: true, currentVersion: '0.1.7', latestVersion: '0.1.8' },
    { verified: true, available: true, currentVersion: '0.1.7', latestVersion: 'invalid' },
    { verified: true, available: true, currentVersion: '0.1.8', latestVersion: '0.1.7' }
  ]) {
    const flow = createUpdateFlow({ callNative: async () => payload });
    const result = await flow.check();
    assert.equal(result.status, 'error');
    assert.equal(result.canInstall, false);
  }
});

test('a verified same-version manifest can say already latest', async () => {
  const flow = createUpdateFlow({callNative: async () => ({
    verified: true, available: false, currentVersion: '0.1.7', latestVersion: '0.1.7'
  })});
  assert.equal((await flow.check()).status, 'latest');
  assert.equal(flow.getState().canInstall, false);
});

test('when native check fails it shows failure, not no-update', async () => {
  const flow = createUpdateFlow({callNative: async () => { throw new Error('Raw 与 API 都失败'); }});
  const result = await flow.check();
  assert.equal(result.status, 'error');
  assert.match(result.message, /Raw 与 API 都失败/);
  assert.equal(result.canInstall, false);
});

test('installation is gated by confirmed availability and explicit user approval', async () => {
  const calls = [];
  const flow = createUpdateFlow({
    callNative: async action => {
      calls.push(action);
      if (action === 'checkForUpdates') return {verified:true,available:true,currentVersion:'0.1.7',latestVersion:'0.1.8'};
      return {restarting:true};
    },
    confirmInstall: async () => true
  });
  assert.equal((await flow.install()).status, 'blocked');
  await flow.check();
  assert.equal((await flow.install()).status, 'restarting');
  assert.deepEqual(calls, ['checkForUpdates', 'installUpdate']);
});

test('failed native installation never reports updated or restarting', async () => {
  const flow = createUpdateFlow({
    callNative: async action => {
      if (action === 'checkForUpdates') return {verified:true,available:true,currentVersion:'0.1.7',latestVersion:'0.1.8'};
      throw new Error('SHA256 校验失败');
    },
    confirmInstall: async () => true
  });
  await flow.check();
  const result = await flow.install();
  assert.equal(result.status, 'error');
  assert.match(result.message, /SHA256 校验失败/);
  assert.equal(result.canInstall, false);
});
