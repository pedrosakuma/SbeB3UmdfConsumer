using B3.Umdf.FixConflated;

namespace B3.Umdf.FixConflated.Tests;

public sealed class SecurityStatusMessageBuilderTests
{
    [Fact]
    public void Build_Produces_Compact_ProductionLike_SecurityStatus()
    {
        var message = SecurityStatusMessageBuilder.Build(new FixSecurityStatusDefinition
        {
            Instrument = new FixInstrumentReference
            {
                Symbol = "PETR4",
                SecurityId = "12345",
                SecurityIdSource = "8",
                SecurityExchange = "BVMF",
                InstrumentId = 99,
                Product = 4,
                CfiCode = "ESVUFR",
                SecurityGroup = "EQUITY",
                SecurityType = "CS",
                SecuritySubType = "PN",
                ContractMultiplier = 1m,
                SecurityDescription = "PETROBRAS PN"
            },
            SecurityTradingStatus = 2,
            SourceTimestampNanoseconds = 1_786_544_116_789_000_000,
            SecurityStatusReqId = "req-1",
            UnsolicitedIndicator = true,
            TradSesOpenTimeNanoseconds = 1_786_543_200_000_000_000,
            SecurityTradingEvent = 4,
            HaltReason = 'X',
            BuyVolume = 1000.5m,
            SellVolume = 2000.25m,
            HighPrice = 32.15m,
            LowPrice = 30.05m,
            LastPrice = 31.55m,
            Text = "Trading resumed",
            TradingSessionId = "1",
            TradingSessionSubId = "18"
        });

        Assert.Equal(FixApplicationMsgTypes.SecurityStatus, FixApplicationMessageTestHelpers.GetRequired(message, FixTags.MsgType));
        Assert.Equal("PETR4", FixApplicationMessageTestHelpers.GetRequired(message, FixApplicationTags.Symbol));
        Assert.Equal("12345", FixApplicationMessageTestHelpers.GetRequired(message, FixApplicationTags.SecurityId));
        Assert.Equal("99", FixApplicationMessageTestHelpers.GetRequired(message, FixApplicationTags.InstrumentId));
        Assert.Equal("4", FixApplicationMessageTestHelpers.GetRequired(message, FixApplicationTags.Product));
        Assert.Equal("ESVUFR", FixApplicationMessageTestHelpers.GetRequired(message, FixApplicationTags.CfiCode));
        Assert.Equal("20260812", FixApplicationMessageTestHelpers.GetRequired(message, FixApplicationTags.TradeDate));
        Assert.Equal("20260812-14:15:16.789", FixApplicationMessageTestHelpers.GetRequired(message, FixApplicationTags.TransactTime));
        Assert.Equal("req-1", FixApplicationMessageTestHelpers.GetRequired(message, FixApplicationTags.SecurityStatusReqId));
        Assert.Equal("1", FixApplicationMessageTestHelpers.GetRequired(message, FixApplicationTags.TradingSessionId));
        Assert.Equal("18", FixApplicationMessageTestHelpers.GetRequired(message, FixApplicationTags.TradingSessionSubId));
        Assert.Equal("Y", FixApplicationMessageTestHelpers.GetRequired(message, FixApplicationTags.UnsolicitedIndicator));
        Assert.Equal("20260812-14:00:00.000", FixApplicationMessageTestHelpers.GetRequired(message, FixApplicationTags.TradSesOpenTime));
        Assert.Equal("EQUITY", FixApplicationMessageTestHelpers.GetRequired(message, FixApplicationTags.SecurityGroup));
        Assert.Equal("2", FixApplicationMessageTestHelpers.GetRequired(message, FixApplicationTags.SecurityTradingStatus));
        Assert.Equal("X", FixApplicationMessageTestHelpers.GetRequired(message, FixApplicationTags.HaltReason));
        Assert.Equal("1000.5", FixApplicationMessageTestHelpers.GetRequired(message, FixApplicationTags.BuyVolume));
        Assert.Equal("2000.25", FixApplicationMessageTestHelpers.GetRequired(message, FixApplicationTags.SellVolume));
        Assert.Equal("32.15", FixApplicationMessageTestHelpers.GetRequired(message, FixApplicationTags.HighPrice));
        Assert.Equal("30.05", FixApplicationMessageTestHelpers.GetRequired(message, FixApplicationTags.LowPrice));
        Assert.Equal("31.55", FixApplicationMessageTestHelpers.GetRequired(message, FixApplicationTags.LastPx));
        Assert.Equal("Trading resumed", FixApplicationMessageTestHelpers.GetRequired(message, FixApplicationTags.Text));
        Assert.Equal("4", FixApplicationMessageTestHelpers.GetRequired(message, FixApplicationTags.SecurityTradingEvent));

        FixMessage decoded = FixApplicationMessageTestHelpers.RoundTrip(message);
        Assert.Equal("2", FixApplicationMessageTestHelpers.GetRequired(decoded, FixApplicationTags.SecurityTradingStatus));
        Assert.Equal("Trading resumed", FixApplicationMessageTestHelpers.GetRequired(decoded, FixApplicationTags.Text));
    }
}
