const { test } = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const path = require('node:path');
const code = fs.readFileSync(path.join(__dirname, '../examples/web-form/altium-event-form.js'), 'utf8');

function setup(overrides = {}) {
  const button = { disabled: false, textContent: 'Kayıt Ol', focus() {} };
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
    dataset: { ...overrides.eventMetadata },
    elements: { namedItem: name => fields[name] },
    querySelector: selector => selector === 'button[type="submit"]' ? button : selector === '.cf-turnstile' ? widget : { value: overrides.token ?? 'captcha' },
    reportValidity: () => true, reset: () => { resets++; },
    addEventListener: (_, fn) => { handler = fn; }
  };
  const window = {
    location: { origin: 'https://altium.net', pathname: '/tr/etkinlik-kayit/test-kayittir-silmeyin', assign: url => { redirects.push(url); } },
    confirm: () => overrides.confirm ?? true,
    turnstile: { reset: () => { captchaResets++; } },
    ...overrides.window
  };
  const redirects = [], nodes = [], domReadyListeners = [];
  const createElement = tag => {
    const listeners = {};
    const node = { tag, style: {}, children: [], textContent: '',
      append(...items) { this.children.push(...items); },
      setAttribute() {}, remove() { this.removed = true; },
      focus() { document.activeElement = this; },
      addEventListener(type, fn) { listeners[type] = fn; },
      trigger(type, event = {}) { listeners[type]?.(event); }
    };
    nodes.push(node); return node;
  };
  const document = {
    readyState: overrides.readyState ?? 'complete', createElement,
    body: { append() {
      if (overrides.manualConfirm) return;
      nodes.findLast(n => n.textContent === ((overrides.confirm ?? true) ? 'Onayla' : 'Vazgeç')).trigger('click');
    } },
    addEventListener: (event, listener) => { if (event === 'DOMContentLoaded') domReadyListeners.push(listener); },
    getElementById: id => id === 'reg-form-app' ? form : id === 'reg-messages' ? messages : checks[id],
    querySelector: () => ({ textContent: 'TEST Kayıttır Silmeyin' })
  };
  const fetch = async (url, options) => {
    requests.push({ url, options });
    return overrides.fetch ? overrides.fetch() : { ok: true, status: 201, json: async () => ({ success: true }) };
  };
  let bindings = 0;
  const originalAdd = form.addEventListener;
  form.addEventListener = (...args) => { bindings++; originalAdd(...args); };
  const load = () => vm.runInNewContext(code, { window, document, fetch, URL });
  load();
  return { fields, checks, button, messages, requests, nodes, redirects, form, load,
    ready: () => domReadyListeners.forEach(fn => fn()), get bindings() { return bindings; },
    submit: () => handler({ preventDefault() {} }), get resets() { return resets; }, get captchaResets() { return captchaResets; } };
}

test('loading the integration asset twice binds only one submit listener', async () => {
  const x = setup(); x.load();
  assert.equal(x.bindings, 1);
  await x.submit(); assert.equal(x.requests.length, 1);
});

test('early script loading waits for DOM before binding', async () => {
  const x = setup({ readyState: 'loading' });
  assert.equal(x.bindings, 0); x.ready(); assert.equal(x.bindings, 1);
  await x.submit(); assert.equal(x.requests.length, 1);
});

test('the supplied confirmation popup blocks submission until accepted', async () => {
  const x = setup({ manualConfirm: true }); const pending = x.submit();
  assert.equal(x.requests.length, 0); await x.submit();
  assert.equal(x.nodes.filter(n => n.textContent === 'Onayla').length, 1);
  x.nodes.find(n => n.textContent === 'Onayla').trigger('click');
  await pending; assert.equal(x.requests.length, 1);
});

test('same-site thank-you redirect happens only after confirmed CRM success', async () => {
  const x = setup({ eventMetadata: { thankYouUrl: '/tr/tesekkur-ederiz' } });
  await x.submit(); assert.deepEqual(x.redirects, ['https://altium.net/tr/tesekkur-ederiz']);
  const failed = setup({ eventMetadata: { thankYouUrl: '/tr/tesekkur-ederiz' }, fetch: async () => ({ok:false, status:503, json:async () => ({success:false})}) });
  await failed.submit(); assert.equal(failed.redirects.length, 0);
});

test('external and malformed optional redirect URLs do not replace successful result', async () => {
  for (const thankYouUrl of ['https://example.invalid/redirect', 'http://[broken']) {
    const x = setup({ eventMetadata: { thankYouUrl } }); await x.submit();
    assert.equal(x.redirects.length, 0); assert.match(x.messages.textContent, /CRM’e kaydedildi/);
  }
});

test('event campaign and lead source preserve exact Unicode and punctuation in JSON', async () => {
  const eventMetadata = { sourceCampaign: 'İstanbul "Tasarım & Üretim" 2026', leadSource: 'CO | Web Formu' };
  const x = setup({ eventMetadata }); await x.submit();
  const b = JSON.parse(x.requests[0].options.body);
  assert.equal(b.sourceCampaign, eventMetadata.sourceCampaign);
  assert.equal(b.leadSource, eventMetadata.leadSource);
  assert.equal(Object.hasOwn(b, 'campaignid'), false);
  assert.equal(Object.hasOwn(b, 'leadsourcecode'), false);
});

test('missing event metadata sends empty strings without inventing CRM values', async () => {
  const x = setup(); await x.submit();
  const b = JSON.parse(x.requests[0].options.body);
  assert.equal(b.sourceCampaign, ''); assert.equal(b.leadSource, '');
});

test('campaign and lead source come from event attributes, not visitor fields', async () => {
  const x = setup({ eventMetadata: { sourceCampaign: 'Kampanya A', leadSource: 'Etkinlik' } });
  x.fields.sourceCampaign = { value: 'Visitor campaign' };
  x.fields.leadSource = { value: 'Visitor source' };
  await x.submit(); const b = JSON.parse(x.requests[0].options.body);
  assert.equal(b.sourceCampaign, 'Kampanya A'); assert.equal(b.leadSource, 'Etkinlik');
});

test('actual Altium form fields map to Azure JSON including event and independent consents', async () => {
  const x = setup(); await x.submit();
  const r = x.requests[0]; const b = JSON.parse(r.options.body);
  assert.equal(r.url, 'https://alttr-marketingautomation-f9dmgackdme4gueu.westeurope-01.azurewebsites.net/WebLead.aspx');
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
  const first = x.submit(); await Promise.resolve(); await x.submit();
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

for (const [status, expectedMessage] of [[503, /servis ayarları/], [502, /CRM kayıt işlemi/]]) {
  test('Azure HTTP ' + status + ' retains fields and reports the failure with a reference', async () => {
    const correlationId = '0123456789abcdef0123456789abcdef';
    const x = setup({ fetch: async () => ({ ok: false, status, json: async () => ({ success: false, correlationId }) }) });
    await x.submit();
    assert.equal(x.requests.length, 1);
    assert.equal(x.resets, 0);
    assert.equal(x.button.disabled, false);
    assert.match(x.messages.textContent, /İstek Azure’a ulaştı/);
    assert.match(x.messages.textContent, expectedMessage);
    assert.ok(x.messages.textContent.includes(correlationId));
  });
}
