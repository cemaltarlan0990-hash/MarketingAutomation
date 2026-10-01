const { test } = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const path = require('node:path');
const code = fs.readFileSync(path.join(__dirname, '../examples/web-form/contact-form.js'), 'utf8');

function setup(overrides = {}) {
  const button = { disabled: false, textContent: 'Gönder' };
  const status = { textContent: '' };
  const values = { firstName: ' Ahmet ', lastName: 'Yılmaz', email: 'a@example.invalid', company: 'ABC', message: 'Talep', website: '', consent: { checked: true } };
  let handler;
  let resets = 0;
  let requests = [];
  const form = {
    dataset: { apiUrl: 'https://service.example/WebLead.aspx' },
    elements: { namedItem: (name) => typeof values[name] === 'string' ? { value: values[name] } : values[name] },
    querySelector: () => button, reportValidity: () => true,
    reset: () => { resets++; }, addEventListener: (_, callback) => { handler = callback; }
  };
  const window = { getCaptchaToken: async () => 'captcha', ...overrides.window };
  const fetch = async (url, options) => {
    requests.push({ url, options });
    return overrides.fetch ? overrides.fetch(url, options) : { ok: true, status: 201, json: async () => ({ success: true }) };
  };
  vm.runInNewContext(code, { document: { getElementById: (id) => id === 'contactForm' ? form : status }, window, fetch });
  return { button, status, values, requests, submit: () => handler({ preventDefault() {} }), get resets() { return resets; } };
}
test('success sends the exact JSON contract without secret headers and resets form', async () => {
  const x = setup(); await x.submit();
  const request = x.requests[0];
  assert.equal(request.url, 'https://service.example/WebLead.aspx');
  assert.deepEqual(Object.keys(request.options.headers), ['Content-Type']);
  const body = JSON.parse(request.options.body);
  assert.equal(body.firstName, 'Ahmet'); assert.equal(body.consent, true); assert.equal(body.phone, '');
  assert.equal(x.resets, 1); assert.equal(x.button.disabled, false);
});
test('CAPTCHA failure restores button and never posts', async () => {
  const x = setup({ window: { getCaptchaToken: async () => { throw Error('provider failed'); } } });
  await x.submit(); assert.equal(x.requests.length, 0); assert.equal(x.button.disabled, false); assert.equal(x.resets, 0);
});
test('concurrent submits produce a single request', async () => {
  let release;
  const x = setup({ window: { getCaptchaToken: () => new Promise(resolve => { release = resolve; }) } });
  const first = x.submit(); await x.submit(); release('captcha'); await first;
  assert.equal(x.requests.length, 1);
});
test('HTTP success with success=false does not clear user data', async () => {
  const x = setup({ fetch: async () => ({ ok: true, status: 200, json: async () => ({ success: false }) }) });
  await x.submit(); assert.equal(x.resets, 0); assert.equal(x.button.disabled, false);
});
test('rate limit uses a safe user message and preserves form', async () => {
  const x = setup({ fetch: async () => ({ ok: false, status: 429, json: async () => ({ message: 'SECRET INTERNAL ERROR' }) }) });
  await x.submit(); assert.equal(x.resets, 0); assert.match(x.status.textContent, /Çok fazla/); assert.doesNotMatch(x.status.textContent, /SECRET/);
});
test('network failure does not retry automatically', async () => {
  const x = setup({ fetch: async () => { throw Error('network'); } }); await x.submit();
  assert.equal(x.requests.length, 1); assert.equal(x.resets, 0); assert.equal(x.button.disabled, false);
});
