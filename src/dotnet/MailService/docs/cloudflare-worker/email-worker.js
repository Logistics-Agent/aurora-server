/**
 * Cloudflare Email Worker for Aurora Mail Platform
 * 
 * Handles:
 * 1. Email Event (`email(message, env, ctx)`):
 *    Intercepts inbound emails from Cloudflare Email Routing,
 *    packages raw RFC822/MIME into JSON + Base64,
 *    signs payload using HMAC-SHA256,
 *    and delivers to Aurora MailService HTTP endpoint.
 * 
 * 2. Fetch Event (`fetch(request, env, ctx)`):
 *    Handles HTTP health checks and browser GET requests safely without throwing 500 errors.
 * 
 * Environment Variables required in Cloudflare Worker (Settings > Variables):
 * - AURORA_INBOUND_URL: e.g. https://api.e-verland.site/api/v1/mail/cloudflare/inbound
 * - AURORA_WEBHOOK_SECRET: Shared secret matching CloudflareInbound:WebhookSecret in Aurora
 */

export default {
  /**
   * Handles HTTP requests (e.g. Health checks, browser visits, favicon)
   */
  async fetch(request, env, ctx) {
    const url = new URL(request.url);

    if (url.pathname === "/favicon.ico") {
      return new Response(null, { status: 204 });
    }

    if (url.pathname === "/health" || url.pathname === "/") {
      return new Response(
        JSON.stringify({
          status: "healthy",
          service: "aurora-email-worker",
          type: "cloudflare-email-routing-worker",
          timestamp: new Date().toISOString()
        }),
        {
          status: 200,
          headers: { "Content-Type": "application/json" }
        }
      );
    }

    return new Response("Not Found", { status: 404 });
  },

  /**
   * Handles Inbound Email Routing events from Cloudflare
   */
  async email(message, env, ctx) {
    try {
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

      const endpointUrl = env.AURORA_INBOUND_URL || "https://api.e-verland.site/api/v1/mail/cloudflare/inbound";

      console.log(`[Cloudflare Email Worker] Processing email From: ${message.from} To: ${message.to} (Size: ${(rawBuffer.byteLength / 1024).toFixed(2)} KB, DeliveryId: ${deliveryId})`);

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
        console.error(`[Cloudflare Email Worker] Aurora endpoint returned HTTP ${response.status}: ${errText}`);
        message.setReject(`Upstream ingestion failed with HTTP ${response.status}`);
        throw new Error(`Failed to forward email to Aurora: ${response.status} ${errText}`);
      }

      const resJson = await response.json().catch(() => ({ status: "accepted" }));
      console.log(`[Cloudflare Email Worker] Email successfully delivered to Aurora:`, resJson);
    } catch (err) {
      console.error(`[Cloudflare Email Worker] Error processing inbound email:`, err);
      throw err;
    }
  }
};

/**
 * Converts ArrayBuffer to Base64 safely even for large email payloads with attachments
 */
function arrayBufferToBase64(buffer) {
  let binary = "";
  const bytes = new Uint8Array(buffer);
  const len = bytes.byteLength;
  const chunkSize = 16384; // 16KB chunk size to avoid call stack overflow

  for (let i = 0; i < len; i += chunkSize) {
    const chunk = bytes.subarray(i, Math.min(i + chunkSize, len));
    binary += String.fromCharCode.apply(null, chunk);
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
