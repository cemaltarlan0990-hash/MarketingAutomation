(() => {
  'use strict';
  const form = document.getElementById('contactForm');
  if (!form) return;
  const button = form.querySelector('button[type="submit"]');
  const status = document.getElementById('contactFormStatus');
  const initialButtonText = button.textContent;
  let sending = false;

  const showMessage = (message) => {
    if (status) status.textContent = message;
    else window.alert(message);
  };

  // Supports the existing getCaptchaToken() function or an implicit Turnstile widget.
  const getToken = async () => {
    if (typeof window.getCaptchaToken === 'function') return await window.getCaptchaToken();
    const input = form.querySelector('[name="cf-turnstile-response"]');
    return input ? input.value : '';
  };

  form.addEventListener('submit', async (event) => {
    event.preventDefault();
    if (sending || !form.reportValidity()) return;
    sending = true;
    button.disabled = true;
    button.textContent = 'Gönderiliyor…';
    showMessage('');
    try {
      const fields = form.elements;
      const text = (name) => fields.namedItem(name)?.value.trim() || '';
      if (!fields.namedItem('consent')?.checked) {
        showMessage('Form onayını işaretleyin.');
        return;
      }
      // CAPTCHA retrieval is inside try/finally so errors cannot leave the button locked.
      const captchaToken = await getToken();
      if (!captchaToken) {
        showMessage('Lütfen güvenlik doğrulamasını tamamlayın.');
        return;
      }
      const payload = {
        firstName: text('firstName'), lastName: text('lastName'),
        email: text('email'), phone: text('phone'), company: text('company'),
        message: text('message'), consent: fields.namedItem('consent').checked,
        captchaToken, website: text('website')
      };
      const response = await fetch(form.dataset.apiUrl, {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        credentials: 'omit', body: JSON.stringify(payload)
      });
      const result = await response.json();
      if (response.ok && result.success === true) {
        form.reset();
        showMessage('Talebiniz alındı, teşekkürler.');
      } else if (response.status === 429) {
        showMessage('Çok fazla gönderim yapıldı. Lütfen bir süre sonra tekrar deneyin.');
      } else if (response.status === 403) {
        showMessage('Güvenlik doğrulaması başarısız. Doğrulamayı yenileyin.');
      } else if (response.status === 400 || response.status === 413) {
        showMessage('Form bilgilerini ve alan uzunluklarını kontrol edin.');
      } else {
        showMessage('Talebiniz tamamlanamadı. Lütfen daha sonra tekrar deneyin.');
      }
    } catch {
      // No automatic retry: a lost response can follow a successful CRM creation.
      showMessage('Gönderim sonucu doğrulanamadı. Tekrar göndermeden önce bağlantınızı kontrol edin.');
    } finally {
      try {
        if (window.turnstile) {
          const widget = form.querySelector('.cf-turnstile');
          if (widget) window.turnstile.reset(widget);
        }
      } catch { /* Button must be restored even if the widget reset fails. */ }
      sending = false;
      button.disabled = false;
      button.textContent = initialButtonText;
    }
  });
})();
