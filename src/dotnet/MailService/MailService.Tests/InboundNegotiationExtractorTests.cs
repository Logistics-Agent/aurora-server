using MailService.Domain.Enums;
using MailService.Infrastructure.Negotiation;
using Xunit;

namespace MailService.Tests;

public class InboundNegotiationExtractorTests
{
    [Fact]
    public void TryExtractOffer_ExtractsShipmentIdAndDollarAmount_ForQuotationEmail()
    {
        bool ok = InboundNegotiationExtractor.TryExtractOffer(
            "Re: Quotation for SHP-2026-00128",
            "Hi team, our counter offer is $1,200.50 for this shipment.",
            EmailCategory.Quotation,
            out string shipmentId,
            out double offerPrice);

        Assert.True(ok);
        Assert.Equal("SHP-2026-00128", shipmentId);
        Assert.Equal(1200.50, offerPrice);
    }

    [Fact]
    public void TryExtractOffer_ExtractsUsdSuffix_WhenIntentKeywordPresent()
    {
        bool ok = InboundNegotiationExtractor.TryExtractOffer(
            "Proposal SHP-VN-900",
            "We propose rate 1500 USD.",
            EmailCategory.Unknown,
            out string shipmentId,
            out double offerPrice);

        Assert.True(ok);
        Assert.Equal("SHP-VN-900", shipmentId);
        Assert.Equal(1500, offerPrice);
    }

    [Fact]
    public void TryExtractOffer_ReturnsFalse_WhenNoShipmentOrNoNegotiationIntent()
    {
        bool ok = InboundNegotiationExtractor.TryExtractOffer(
            "Hello",
            "Please send invoice $500 for last month.",
            EmailCategory.Unknown,
            out _,
            out _);

        Assert.False(ok);
    }
}
