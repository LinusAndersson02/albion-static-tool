using StatisticsAnalysisTool.Network;
using StatisticsAnalysisTool.Network.Legacy;

namespace StatisticsAnalysisTool.Linux.Runtime;

public sealed class EstimatedItemValuePacketHandler : PacketHandler<EventPacket>
{
    private readonly EstimatedItemValueService _values;

    public EstimatedItemValuePacketHandler(EstimatedItemValueService values)
    {
        _values = values;
    }

    protected override Task OnHandleAsync(EventPacket packet)
    {
        switch ((LegacyAlbionEventCode)packet.EventCode)
        {
            case LegacyAlbionEventCode.NewEquipmentItem:
            case LegacyAlbionEventCode.NewSimpleItem:
            case LegacyAlbionEventCode.NewFurnitureItem:
            case LegacyAlbionEventCode.NewKillTrophyItem:
            case LegacyAlbionEventCode.NewJournalItem:
            case LegacyAlbionEventCode.NewLaborerItem:
            case LegacyAlbionEventCode.NewEquipmentItemLegendarySoul:
                var item = LegacyDiscoveredItem.FromItemEventParameters(packet.Parameters);
                _values.Record(item?.ItemIndex, item?.EstimatedMarketValue, item?.Quality);
                break;
        }

        return NextAsync(packet);
    }
}
