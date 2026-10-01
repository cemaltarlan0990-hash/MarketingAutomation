{{-- Altium websitesinin gerçek etkinlik formu şablonuna uygulanacak parça.
     1. Mevcut Turnstile widget satırını aşağıdaki div ile değiştirin.
        İkinci bir widget oluşturmayın; mevcut api.js yüklemesi kalsın.
     2. reg-form-app için eski submit listener bloğunu tamamen kaldırın.
        Telefon yardımcıları ve EIO/kanal eşitleme kodu kalsın.
     3. Aşağıdaki script etiketini mevcut formdan sonra ekleyin.
     Bu dosya Azure deploy ile Altium sitesine otomatik kurulmaz. --}}

<div class="cf-turnstile"
     data-sitekey="0x4AAAAAAD1BpWdw4ch_-oeB"
     data-action="web-lead"
     style="margin:8px 0;"></div>

<script defer src="https://alttr-marketingautomation-f9dmgackdme4gueu.westeurope-01.azurewebsites.net/integrations/altium-event-form.js?v=direct-azure-2"></script>
