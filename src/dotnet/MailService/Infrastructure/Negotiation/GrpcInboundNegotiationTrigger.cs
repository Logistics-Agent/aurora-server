using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using MailService.Application.Interfaces.Negotiation;
using MailService.Domain.Enums;
using Microsoft.Extensions.Logging;
using Negotiation.Grpc;

namespace MailService.Infrastructure.Negotiation;

public static partial class InboundNegotiationExtractor
{
    private static readonly Regex ShipmentIdRegex = new(
        @"\b(SHP-[A-Z0-9-]{3,})\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex NegotiationIntentRegex = new(
        @"\b(offer|counter[- ]?offer|quote|quotation|price|rate|budget|propose|proposal|trả giá|đề xuất|báo giá|giá)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CurrencyAmountRegex = new(
        @"(?:\$\s*([\d,]+(?:\.\d+)?)|USD\s*([\d,]+(?:\.\d+)?)|([\d,]+(?:\.\d+)?)\s*USD)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool TryExtractOffer(
        string? subject,
        string? bodyText,
        EmailCategory category,
        out string shipmentId,
        out double offerPrice)
    {
        shipmentId = string.Empty;
        offerPrice = 0;

        string combined = $"{subject ?? string.Empty}\n{bodyText ?? string.Empty}";
        var shpMatch = ShipmentIdRegex.Match(combined);
        if (!shpMatch.Success)
        {
            return false;
        }

        if (category != EmailCategory.Quotation && !NegotiationIntentRegex.IsMatch(combined))
        {
            return false;
        }

        var priceMatch = CurrencyAmountRegex.Match(combined);
        if (!priceMatch.Success)
        {
            return false;
        }

        string rawAmount = priceMatch.Groups[1].Success
            ? priceMatch.Groups[1].Value
            : priceMatch.Groups[2].Success
                ? priceMatch.Groups[2].Value
                : priceMatch.Groups[3].Value;

        string normalized = rawAmount.Replace(",", string.Empty);
        if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) || parsed <= 0)
        {
            return false;
        }

        shipmentId = shpMatch.Groups[1].Value.ToUpperInvariant();
        offerPrice = parsed;
        return true;
    }
}

public sealed class GrpcInboundNegotiationTrigger : IInboundNegotiationTrigger
{
    private readonly NegotiationService.NegotiationServiceClient _client;
    private readonly ILogger<GrpcInboundNegotiationTrigger> _logger;

    public GrpcInboundNegotiationTrigger(
        NegotiationService.NegotiationServiceClient client,
        ILogger<GrpcInboundNegotiationTrigger> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task TryTriggerAsync(InboundNegotiationContext context, CancellationToken cancellationToken = default)
    {
        if (!InboundNegotiationExtractor.TryExtractOffer(
                context.Subject,
                context.BodyText,
                context.Category,
                out string shipmentId,
                out double offerPrice))
        {
            return;
        }

        try
        {
            var headers = new Metadata
            {
                { "x-tenant-id", context.TenantId.ToString() }
            };

            var response = await _client.SubmitOfferAsync(
                new SubmitOfferRequest
                {
                    ShipmentId = shipmentId,
                    CustomerId = context.SenderAddress,
                    OfferPrice = offerPrice,
                    CustomerTier = "STANDARD",
                    SourceMessageId = context.MessageId,
                    SourceThreadId = context.ThreadId.ToString()
                },
                headers,
                deadline: DateTime.UtcNow.AddSeconds(5),
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Inbound mail {MessageId} triggered Negotiation {SessionId} for {ShipmentId} (Offer: {OfferPrice}, Decision: {Decision})",
                context.MessageId, response.SessionId, shipmentId, offerPrice, response.Decision);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to trigger NegotiationService.SubmitOffer for inbound message {MessageId} (Shipment {ShipmentId})",
                context.MessageId, shipmentId);
        }
    }
}
