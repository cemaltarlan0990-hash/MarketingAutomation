const { test } = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const path = require('node:path');
const code = fs.readFileSync(path.join(__dirname, '../examples/web-form/altium-event-form.js'), 'utf8');

function setup(overrides = {}) {
  const button = { disabled: false, textContent: 'Kayıt Ol' };
  const messages = { textContent: '' };
  const fields = Object.fromEntries(Object.entries({
    first_name: ' Ahmet ', last_name: 'Yılmaz', email: 'ahmet@example.invalid',
    company: 'ABC', title: 'Mühendis', city: 'İSTANBUL', message: 'Etkinliğe katılmak istiyorum.',
    phone: '0555 111 22 33', _email: ''
  }).map(([name, value]) => [name, { value }]));
  fields.phone_country = { selectedOptions: [{ textContent: '+90 Türkiye' }] };
  const checks = { kvkk: { checked: true }, eio: { checked: true }, channel_email: { checked: true }, channel_sms: { checked: false }, channel_phone: { checked: false } };
  let handler, resets = 0, captchaResets = 0;
  const requests = [];
  const widget = {};
  const form = {
    elements: { namedItem: name => fields[name] },
    querySelector: selector => selector === 'button[type="submit"]' ? button : selector === '.cf-turnstile' ? widget : { value: overrides.token ?? 'captcha' },
    reportValidity: () => true, reset: () => { resets++; },
    addEventListener: (_, fn) => { handler = fn; }
  };
  const window = {
    location: { origin: 'https://altium.net', pathname: '/tr/etkinlik-kayit/test-kayittir-silmeyin' },
    confirm: () => overrides.confirm ?? true,
    turnstile: { reset: () => { captchaResets++; } },
    ...overrides.window
  };
  const document = {
    getElementById: id => id === 'reg-form-app' ? form : id === 'reg-messages' ? messages : checks[id],
    querySelector: () => ({ textContent: 'TEST Kayıttır Silmeyin' })
  };
  const fetch = async (url, options) => {
    requests.push({ url, options });
    return overrides.fetch ? overrides.fetch() : { ok: true, status: 201, json: async () => ({ success: true }) };
  };
  vm.runInNewContext(code, { window, document, fetch });
  return { fields, checks, button, messages, requests, submit: () => handler({ preventDefault() {} }), get resets() { return resets; }, get captchaResets() { return captchaResets; } };
}

test('actual Altium form fields map to Azure JSON including event and independent consents', async () => {
  const x = setup(); await x.submit();
  const r = x.requests[0]; const b = JSON.parse(r.options.body);
  assert.match(r.url, /azurewebsites\.net\/WebLead\.aspx$/);
  assert.equal(b.firstName, 'Ahmet'); assert.equal(b.lastName, 'Yılmaz');
  assert.equal(b.jobTitle, 'Mühendis'); assert.equal(b.city, 'İSTANBUL');
  assert.equal(b.phone, '+905551112233');
  assert.equal(b.consent, true); assert.equal(b.marketingConsent, true);
  assert.equal(b.emailConsent, true); assert.equal(b.smsConsent, false);
  assert.match(b.eventUrl, /\/test-kayittir-silmeyin$/);
  assert.equal(b.eventTitle, 'TEST Kayıttır Silmeyin');
  assert.deepEqual(Object.keys(r.options.headers), ['Content-Type']);
  assert.equal(x.resets, 1); assert.equal(x.button.disabled, false);
});
test('registration does not require marketing consent', async () => {
  const x = setup(); x.checks.eio.checked = false; x.checks.channel_email.checked = false;
  await x.submit(); assert.equal(JSON.parse(x.requests[0].options.body).marketingConsent, false);
});
test('cancel confirmation sends nothing and restores button', async () => {
  const x = setup({ confirm: false }); await x.submit();
  assert.equal(x.requests.length, 0); assert.equal(x.button.disabled, false);
});
test('KVKK checkbox is read rather than hard-coded', async () => {
  const x = setup(); x.checks.kvkk.checked = false; await x.submit();
  assert.equal(x.requests.length, 0); assert.match(x.messages.textContent, /onay/);
});
test('honeypot uses the actual website field', async () => {
  const x = setup(); x.fields._email.value = 'spam'; await x.submit();
  assert.equal(JSON.parse(x.requests[0].options.body).website, 'spam');
});
test('empty CAPTCHA cannot send a request', async () => {
  const x = setup({ token: '' }); await x.submit();
  assert.equal(x.requests.length, 0); assert.equal(x.button.disabled, false);
});
test('double click makes exactly one Azure request', async () => {
  let release;
  const x = setup({ fetch: () => new Promise(resolve => { release = resolve; }) });
  const first = x.submit(); await x.submit();
  release({ ok: true, status: 201, json: async () => ({ success: true }) }); await first;
  assert.equal(x.requests.length, 1);
});
test('unconfirmed result retains form without automatic retry', async () => {
  const x = setup({ fetch: async () => { throw Error('lost response'); } }); await x.submit();
  assert.equal(x.requests.length, 1); assert.equal(x.resets, 0); assert.equal(x.button.disabled, false);
});
test('provider reset error cannot keep button locked', async () => {
  const x = setup({ window: { turnstile: { reset: () => { throw Error('reset'); } } } });
  await x.submit(); assert.equal(x.button.disabled, false);
});
test('international country code is taken from selected country', async () => {
  const x = setup(); x.fields.phone_country.selectedOptions[0].textContent = '+49 Almanya';
  x.fields.phone.value = '15112345678'; await x.submit();
  assert.equal(JSON.parse(x.requests[0].options.body).phone, '+4915112345678');
});
test('Altium phone helper is used without discarding country code', async () => {
  const x = setup({ window: { AltiumPhone: { national: () => '5551112233', error: () => null } } });
  await x.submit(); assert.equal(JSON.parse(x.requests[0].options.body).phone, '+905551112233');
});
test('standard checkbox sync code must not imply a channel without selection', async () => {
  const x = setup(); x.checks.channel_email.checked = false; await x.submit();
  assert.equal(x.requests.length, 0); assert.match(x.messages.textContent, /kanalı/);
});
