<?php
declare(strict_types=1);

/**
 * Website SERVER integration example, not browser code or a standalone public endpoint.
 * Call only after the existing handler validates the form, CSRF and CAPTCHA.
 * Supply the key from the website's server configuration, never from form input.
 * Supply the title and URL from the server's event record, never from form input.
 * Requires PHP 7.3+ and cURL. The actual website handler must include and call this function.
 */
function forwardEventToCrm(
    array $validated,
    string $integrationKey,
    string $eventTitle,
    string $eventUrl
): array {
    if (trim($integrationKey) === '') {
        throw new RuntimeException('CRM entegrasyon anahtarı yapılandırılmamış.');
    }
    $flag = static function (string $name) use ($validated): bool {
        $value = $validated[$name] ?? false;
        if (in_array($value, [true, 1, '1'], true)) {
            return true;
        }
        if (in_array($value, [false, 0, '0'], true)) {
            return false;
        }
        throw new InvalidArgumentException('Geçersiz onay alanı: ' . $name);
    };
    if (!$flag('kvkk_consent')) {
        throw new InvalidArgumentException('Form onayı gereklidir.');
    }
    $payload = [
        'firstName' => $validated['first_name'] ?? '',
        'lastName' => $validated['last_name'] ?? '',
        'email' => $validated['email'] ?? '',
        'phone' => $validated['phone'] ?? '',
        'company' => $validated['company'] ?? '',
        'jobTitle' => $validated['title'] ?? '',
        'city' => $validated['city'] ?? '',
        'message' => $validated['message'] ?? '',
        'consent' => true,
        'marketingConsent' => $flag('eio_consent'),
        'emailConsent' => $flag('channel_email'),
        'smsConsent' => $flag('channel_sms'),
        'phoneConsent' => $flag('channel_phone'),
        'website' => $validated['_email'] ?? '',
        'eventTitle' => $eventTitle,
        'eventUrl' => $eventUrl,
    ];

    $json = json_encode($payload, JSON_UNESCAPED_UNICODE | JSON_THROW_ON_ERROR);
    $handle = curl_init('https://alttr-marketingautomation-f9dmgackdme4gueu.westeurope-01.azurewebsites.net/CreateCrmRegistration.aspx');
    if ($handle === false) {
        throw new RuntimeException('CRM isteği başlatılamadı.');
    }
    try {
        curl_setopt_array($handle, [
            CURLOPT_POST => true,
            CURLOPT_POSTFIELDS => $json,
            CURLOPT_HTTPHEADER => [
                'Content-Type: application/json; charset=utf-8',
                'Accept: application/json',
                'X-Integration-Key: ' . $integrationKey,
            ],
            CURLOPT_RETURNTRANSFER => true,
            CURLOPT_FOLLOWLOCATION => false,
            CURLOPT_CONNECTTIMEOUT => 10,
            CURLOPT_TIMEOUT => 30,
            CURLOPT_SSL_VERIFYPEER => true,
            CURLOPT_SSL_VERIFYHOST => 2,
        ]);
        $response = curl_exec($handle);
        $status = (int) curl_getinfo($handle, CURLINFO_HTTP_CODE);
        if ($response === false || $status !== 200) {
            // Do not log the request body, key or raw CRM response.
            throw new RuntimeException('CRM aktarımı tamamlanamadı. HTTP: ' . $status);
        }
        $result = json_decode($response, true, 8, JSON_THROW_ON_ERROR);
        if (!is_array($result) || ($result['success'] ?? false) !== true || empty($result['crmId'])) {
            throw new RuntimeException('CRM kaydı doğrulanamadı.');
        }
        return ['crmId' => $result['crmId'], 'correlationId' => $result['correlationId'] ?? null];
    } finally {
        curl_close($handle);
    }
}
