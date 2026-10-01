// Replace the existing reg-form-app submit handler with this script.
// Keep the website's phone helper and EIO/channel synchronisation code.
(() => {
  'use strict';
  const form = document.getElementById('reg-form-app');
  if (!form) return;
  const button = form.querySelector('button[type="submit"]');
  const messages = document.getElementById('reg-messages');
  if (!button || !messages) return;
  const endpoint = 'https://alttr-marketingautomation-f9dmgackdme4gueu.westeurope-01.azurewebsites.net/WebLead.aspx';
  const label = button.textContent;
  let sending = false;
  const text = name => form.elements.namedItem(name)?.value.trim() || '';
  const checked = id => Boolean(document.getElementById(id)?.checked);
  const say = message => { messages.textContent = message; };
  const eventTitle = () => document.querySelector('main h1')?.textContent.trim() || '';

  // Match server field limits and show errors before consuming CAPTCHA.
  const limits = { first_name: 50, last_name: 50, email: 100, company: 100, title: 100, message: 1000 };
  for (const [name, maximum] of Object.entries(limits)) {
    const field = form.elements.namedItem(name);
    if (field) field.maxLength = maximum;
  }

  function phoneNumber() {
    const field = form.elements.namedItem('phone');
    if (!field?.value.trim()) return '';
    const selection = form.elements.namedItem('phone_country')?.selectedOptions?.[0];
    const dialCode = selection?.textContent.trim().match(/^\+\d+/)?.[0];
    if (!dialCode) throw new Error('Telefon ülke kodunu seçin.');
    // AltiumPhone.national is the same helper used by the current event form.
    const national = window.AltiumPhone ? window.AltiumPhone.national(field) : field.value;
    if (!window.AltiumPhone && national.trim().startsWith('+')) return '+' + national.replace(/\D/g, '');
    let digits = national.replace(/\D/g, '');
    if (dialCode === '+90' && digits.startsWith('0')) digits = digits.slice(1);
    return dialCode + digits;
  }

  form.addEventListener('submit', async event => {
    event.preventDefault();
    if (sending || !form.reportValidity()) return;
    sending = true; // Also covers the registration confirmation step.
    button.disabled = true;
    try {
      const title = eventTitle();
      if (!window.confirm(title + ' etkinliğine kayıt olmak istiyor musunuz?')) return;
      button.textContent = 'Gönderiliyor…';
      say('');
      if (!checked('kvkk')) { say('Lütfen form onayını işaretleyin.'); return; }
      const emailConsent = checked('channel_email');
      const smsConsent = checked('channel_sms');
      const phoneConsent = checked('channel_phone');
      const marketingConsent = checked('eio') || emailConsent || smsConsent || phoneConsent;
      if (marketingConsent && !(emailConsent || smsConsent || phoneConsent)) {
        say('Ticari ileti onayı için en az bir iletişim kanalı seçin.'); return;
      }
      const phoneField = form.elements.namedItem('phone');
      const phoneError = window.AltiumPhone && phoneField ? window.AltiumPhone.error(phoneField, false) : null;
      if (phoneError) { say(phoneError); return; }
      let phone;
      try { phone = phoneNumber(); } catch (error) { say(error.message); return; }
      if (phone && !/^\+[1-9]\d{6,14}$/.test(phone)) {
        say('Telefon numarasını ve ülke kodunu kontrol edin.'); return;
      }
      const captchaToken = form.querySelector('[name="cf-turnstile-response"]')?.value || '';
      if (!captchaToken) { say('Lütfen güvenlik doğrulamasını tamamlayın.'); return; }
      const payload = {
        firstName: text('first_name'), lastName: text('last_name'), email: text('email'),
        company: text('company'), phone, jobTitle: text('title'), city: text('city'),
        message: text('message'), consent: checked('kvkk'),
        marketingConsent, emailConsent, smsConsent, phoneConsent,
        website: text('_email'), captchaToken,
        eventTitle: title, eventUrl: window.location.origin + window.location.pathname
      };
      const response = await fetch(endpoint, {
        method: 'POST', credentials: 'omit',
        headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload)
      });
      const result = await response.json();
      const reference = typeof result.correlationId === 'string' && /^[a-f0-9]{32}$/i.test(result.correlationId)
        ? ' Referans: ' + result.correlationId : '';
      if (response.ok && result.success === true) {
        form.reset();
        say('Etkinlik talebiniz CRM’e kaydedildi, teşekkürler.');
      } else if (response.status === 429) {
        say('Çok fazla gönderim yapıldı. Bir süre sonra tekrar deneyin.');
      } else if (response.status === 400 || response.status === 413) {
        say('Form bilgilerini ve alan uzunluklarını kontrol edin.');
      } else if (response.status === 403) {
        say('Güvenlik doğrulaması başarısız. Doğrulamayı yenileyin.' + reference);
      } else if (response.status === 503) {
        say('İstek Azure’a ulaştı, ancak servis ayarları henüz tamamlanmamış. Site yöneticisine bildirin.' + reference);
      } else if (response.status === 502) {
        say('İstek Azure’a ulaştı, ancak CRM kayıt işlemi tamamlanamadı. Site yöneticisine bildirin.' + reference);
      } else {
        say('Kayıt işlemi tamamlanamadı. Lütfen daha sonra tekrar deneyin.' + reference);
      }
    } catch {
      // A lost response may follow a CRM creation. Never retry automatically.
      say('Gönderim sonucu doğrulanamadı. Tekrar göndermeden önce bağlantınızı kontrol edin.');
    } finally {
      try {
        const widget = form.querySelector('.cf-turnstile');
        if (widget && window.turnstile) window.turnstile.reset(widget);
      } catch { /* Keep button recovery independent of the CAPTCHA library. */ }
      button.disabled = false;
      button.textContent = label;
      sending = false;
    }
  });
})();
