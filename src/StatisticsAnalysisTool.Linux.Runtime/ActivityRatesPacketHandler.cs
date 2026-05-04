using StatisticsAnalysisTool.Network;
using StatisticsAnalysisTool.Network.Legacy;

namespace StatisticsAnalysisTool.Linux.Runtime;

public sealed class ActivityRatesPacketHandler : PacketHandler<EventPacket>
{
    private const long FixPointScale = 10_000;
    private readonly ActivityRatesService _rates;
    private readonly EntityNameService _entityNames;
    private double _lastLocalEntityGuildTaxInPercent;
    private double _lastLocalEntityClusterTaxInPercent;

    public ActivityRatesPacketHandler(ActivityRatesService rates, EntityNameService entityNames)
    {
        _rates = rates;
        _entityNames = entityNames;
    }

    protected override Task OnHandleAsync(EventPacket packet)
    {
        var now = DateTimeOffset.Now;
        switch ((LegacyAlbionEventCode)packet.EventCode)
        {
            case LegacyAlbionEventCode.TakeSilver:
                RecordTakeSilver(LegacyTakeSilverEvent.FromParameters(packet.Parameters), now);
                break;
            case LegacyAlbionEventCode.UpdateFame:
                _rates.AddFame(GetGainedFame(LegacyUpdateFameEvent.FromParameters(packet.Parameters)), now);
                break;
            case LegacyAlbionEventCode.UpdateReSpecPoints:
                RecordReSpec(LegacyUpdateReSpecPointsEvent.FromParameters(packet.Parameters), now);
                break;
            case LegacyAlbionEventCode.UpdateCurrency:
                _rates.AddFactionPoints(ToDisplayedValue(LegacyUpdateCurrencyEvent.FromParameters(packet.Parameters).GainedFactionCoinsInternal), now);
                break;
            case LegacyAlbionEventCode.UpdateFactionStanding:
                var factionStanding = LegacyUpdateFactionStandingEvent.FromParameters(packet.Parameters);
                _rates.AddFactionStanding(ToDisplayedValue(factionStanding.GainedFactionFlagPointsInternal), now);
                break;
            case LegacyAlbionEventCode.MightAndFavorReceivedEvent:
                var mightAndFavor = LegacyMightAndFavorReceivedEvent.FromParameters(packet.Parameters);
                _rates.AddMight(ToDisplayedValue(mightAndFavor.MightInternal), now);
                _rates.AddFavor(ToDisplayedValue(mightAndFavor.FavorInternal), now);
                break;
        }

        return NextAsync(packet);
    }

    private void RecordTakeSilver(LegacyTakeSilverEvent value, DateTimeOffset now)
    {
        var isObjectLocalEntity = value.ObjectId is not null && _entityNames.IsLocalEntity(value.ObjectId);
        var isObjectPartyEntityAndNotTargetEntity = value.ObjectId is not null
            && _entityNames.IsPartyEntity(value.ObjectId)
            && value.ObjectId != value.TargetEntityId;
        var isObjectLocalEntityAndTargetEntity = value.ObjectId is not null
            && _entityNames.IsLocalEntity(value.ObjectId)
            && value.ObjectId == value.TargetEntityId;

        if (!isObjectLocalEntity && !isObjectPartyEntityAndNotTargetEntity && !isObjectLocalEntityAndTargetEntity)
        {
            return;
        }

        var silverInternal = value.YieldPreTaxInternal - value.GuildTaxInternal;
        if (isObjectLocalEntity && !isObjectLocalEntityAndTargetEntity && value.YieldPreTaxInternal > 0)
        {
            _lastLocalEntityGuildTaxInPercent = 100d / value.YieldPreTaxInternal * value.GuildTaxInternal;
            _lastLocalEntityClusterTaxInPercent = 100d / value.YieldPreTaxInternal * value.ClusterTaxInternal;
        }

        if (isObjectPartyEntityAndNotTargetEntity && !isObjectLocalEntity)
        {
            var guildTax = value.YieldPreTaxInternal / 100d * _lastLocalEntityGuildTaxInPercent;
            var yieldAfterGuildTax = value.YieldPreTaxInternal - guildTax;
            var clusterTax = yieldAfterGuildTax / 100d * _lastLocalEntityClusterTaxInPercent;
            silverInternal = (long)Math.Round(yieldAfterGuildTax - clusterTax, MidpointRounding.AwayFromZero);
        }

        _rates.AddSilver(ToDisplayedValue(silverInternal), now);
    }

    private void RecordReSpec(LegacyUpdateReSpecPointsEvent value, DateTimeOffset now)
    {
        if (!value.HasCurrentTotalReSpecPoints)
        {
            return;
        }

        _rates.AddReSpecPoints(ToDisplayedValue(value.GainedReSpecPointsInternal), now);
        _rates.AddPaidSilverForReSpec(ToDisplayedValue(value.PaidSilverInternal), now);
    }

    private static long GetGainedFame(LegacyUpdateFameEvent value)
    {
        var fame = ToDisplayedValue(value.FameWithZoneMultiplierInternal);
        var satchel = ToDisplayedValue(value.SatchelFameInternal);
        var premiumBonus = value.IsPremiumBonus
            ? fame * 0.5d
            : 0d;

        return (long)Math.Round((fame + premiumBonus + satchel) * value.BonusFactor, MidpointRounding.AwayFromZero);
    }

    private static long ToDisplayedValue(long internalValue)
    {
        return Math.Max(0, internalValue / FixPointScale);
    }
}
