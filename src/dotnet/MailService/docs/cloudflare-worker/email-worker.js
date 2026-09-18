/**
 * Cloudflare Email Worker for Aurora Mail Platform
 * 
 * Intercepts inbound emails from Cloudflare Email Routing,
 * packages raw RFC822/MIME into JSON + Base64,
 * signs payload using HMAC-SHA256,
 * and delivers to Aurora MailService HTTP endpoint.
 * 
 * Environment Variables required in Cloudflare Worker:
 * - AURORA_INBOUND_URL: e.g. https://api.humanak.cyou/api/v1/mail/inbound/cloudflare
 * - AURORA_WEBHOOK_SECRET: Shared secret matching CloudflareInbound:WebhookSecret in Aurora
 */

export default {
  async email(message, env, ctx) {
    const rawBuffer = await new Response(message.raw).arrayBuffer();
    const rawBase64 = arrayBufferToBase64(rawBuffer);

    const deliveryId = crypto.randomUUID();
    const timestamp = Math.floor(Date.now() / 1000);

    const payload = {
      deliveryId: deliveryId,
      timestamp: timestamp,
      from: message.from,
      to: [message.to],
      rawEmailBase64: rawBase64
    };

    const rawBody = JSON.stringify(payload);

    // Generate HMAC-SHA256 signature over `${timestamp}.${rawBody}`
    let signatureHex = "";
    if (env.AURORA_WEBHOOK_SECRET) {
      signatureHex = await generateHmacSha256(env.AURORA_WEBHOOK_SECRET, `${timestamp}.${rawBody}`);
    }

    // Keep a production-safe default so a missing Worker variable does not
    // silently route mail to the retired e-verland.site endpoint. The
    // variable remains configurable for other environments.
    const endpointUrl = (env.AURORA_INBOUND_URL || "https://api.humanak.cyou/api/v1/mail/inbound/cloudflare").trim();

    if (!endpointUrl) {
      console.error("[Cloudflare Email Worker] AURORA_INBOUND_URL is empty");
      message.setReject("Aurora inbound endpoint is not configured");
      throw new Error("Aurora inbound endpoint is not configured");
    }

    console.log(`[Cloudflare Email Worker] Forwarding email From: ${message.from} To: ${message.to} DeliveryId: ${deliveryId} Endpoint: ${endpointUrl}`);

    const headers = {
      "Content-Type": "application/json",
      "X-Aurora-Timestamp": timestamp.toString(),
      "X-Aurora-Delivery-Id": deliveryId,
      "X-Aurora-Signature": signatureHex
    };

    if (env.AURORA_WEBHOOK_SECRET) {
      headers["X-Aurora-Webhook-Secret"] = env.AURORA_WEBHOOK_SECRET;
    }

    const response = await fetch(endpointUrl, {
      method: "POST",
      headers: headers,
      body: rawBody
    });

    if (!response.ok) {
      const errText = await response.text().catch(() => "");
      console.error(`[Cloudflare Email Worker] Aurora endpoint returned error ${response.status}: ${errText}`);
      message.setReject(`Upstream ingestion error (${response.status})`);
      throw new Error(`Failed to forward email to Aurora: ${response.status} ${errText}`);
    }

    const resJson = await response.json().catch(() => ({ status: "ok" }));
    console.log(`[Cloudflare Email Worker] Inbound successfully processed by Aurora:`, resJson);
  }
};

/**
 * Converts ArrayBuffer to Base64 string
 */
function arrayBufferToBase64(buffer) {
  let binary = "";
  const bytes = new Uint8Array(buffer);
  const chunkSize = 0x8000;
  for (let i = 0; i < bytes.length; i += chunkSize) {
    binary += String.fromCharCode.apply(null, bytes.subarray(i, i + chunkSize));
  }
  return btoa(binary);
}

/**
 * Computes HMAC-SHA256 signature in hexadecimal using Web Crypto API
 */
async function generateHmacSha256(secret, messageText) {
  const encoder = new TextEncoder();
  const keyData = encoder.encode(secret);
  const messageData = encoder.encode(messageText);

  const cryptoKey = await crypto.subtle.importKey(
    "raw",
    keyData,
    { name: "HMAC", hash: "SHA-256" },
    false,
    ["sign"]
  );

  const signatureBuffer = await crypto.subtle.sign("HMAC", cryptoKey, messageData);
  const signatureBytes = new Uint8Array(signatureBuffer);

  return Array.from(signatureBytes)
    .map(b => b.toString(16).padStart(2, "0"))
    .join("");
}
