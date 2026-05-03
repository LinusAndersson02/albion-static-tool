using StatisticsAnalysisTool.Network;

namespace StatisticsAnalysisTool.Tui;

public sealed class ActivityRatesPacketHandler : PacketHandler<EventPacket>
{
    private const long FixPointScale = 10_000;
    private readonly ActivityRatesService _rates;
    private readonly EntityNameService _entityNames;

    public ActivityRatesPacketHandler(ActivityRatesService rates, EntityNameService entityNames)
    {
        _rates = rates;
        _entityNames = entityNames;
    }

    protected override Task OnHandleAsync(EventPacket packet)
    {
        var now = DateTimeOffset.Now;
        switch ((AlbionEventCodes)packet.EventCode)
        {
            case AlbionEventCodes.TakeSilver:
                if (IsLocalTakeSilver(packet.Parameters))
                {
                    _rates.AddSilver(ToDisplayedValue(PacketValueReader.GetLong(packet.Parameters, 3)), now);
                }
                break;
            case AlbionEventCodes.OtherGrabbedLoot:
                if ((PacketValueReader.GetBool(packet.Parameters, 3) ?? false)
                    && _entityNames.IsLocalName(PacketValueReader.GetString(packet.Parameters, 2)))
                {
                    _rates.AddSilver(ToDisplayedValue(PacketValueReader.GetLong(packet.Parameters, 5)), now);
                }
                break;
            case AlbionEventCodes.UpdateFame:
                _rates.AddFame(GetGainedFame(packet.Parameters), now);
                break;
            case AlbionEventCodes.UpdateReSpecPoints:
                _rates.AddReSpecPoints(ToDisplayedValue(PacketValueReader.GetLong(packet.Parameters, 2)), now);
                break;
            case AlbionEventCodes.UpdateCurrency:
                _rates.AddFactionPoints(ToDisplayedValue(PacketValueReader.GetLong(packet.Parameters, 3)), now);
                break;
            case AlbionEventCodes.MightAndFavorReceivedEvent:
                _rates.AddMight(GetMight(packet.Parameters), now);
                _rates.AddFavor(GetFavor(packet.Parameters), now);
                break;
        }

        return NextAsync(packet);
    }

    private static long GetGainedFame(IReadOnlyDictionary<byte, object> parameters)
    {
        var fame = ToDisplayedValue(PacketValueReader.GetLong(parameters, 2));
        var satchel = ToDisplayedValue(PacketValueReader.GetLong(parameters, 10));
        var premiumBonus = PacketValueReader.GetBool(parameters, 5) == true
            ? (long)Math.Round(fame * 0.5d, MidpointRounding.AwayFromZero)
            : 0;

        return fame + premiumBonus + satchel;
    }

    private bool IsLocalTakeSilver(IReadOnlyDictionary<byte, object> parameters)
    {
        var objectId = PacketValueReader.GetLong(parameters, 0);
        var targetEntityId = PacketValueReader.GetLong(parameters, 2);

        return _entityNames.IsLocalEntity(objectId) || _entityNames.IsLocalEntity(targetEntityId);
    }

    private static long GetMight(IReadOnlyDictionary<byte, object> parameters)
    {
        return ToDisplayedValue(PacketValueReader.GetLong(parameters, 0))
            + ToDisplayedValue(PacketValueReader.GetLong(parameters, 2));
    }

    private static long GetFavor(IReadOnlyDictionary<byte, object> parameters)
    {
        return ToDisplayedValue(PacketValueReader.GetLong(parameters, 3))
            + ToDisplayedValue(PacketValueReader.GetLong(parameters, 5));
    }

    private static long ToDisplayedValue(long? internalValue)
    {
        return internalValue is null ? 0 : Math.Max(0, internalValue.Value / FixPointScale);
    }
}
