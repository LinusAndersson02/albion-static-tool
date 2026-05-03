using StatisticsAnalysisTool.Network;

namespace StatisticsAnalysisTool.Tui;

public sealed class EstimatedItemValuePacketHandler : PacketHandler<EventPacket>
{
    private readonly EstimatedItemValueService _values;

    public EstimatedItemValuePacketHandler(EstimatedItemValueService values)
    {
        _values = values;
    }

    protected override Task OnHandleAsync(EventPacket packet)
    {
        switch ((AlbionEventCodes)packet.EventCode)
        {
            case AlbionEventCodes.NewEquipmentItem:
            case AlbionEventCodes.NewSimpleItem:
            case AlbionEventCodes.NewFurnitureItem:
            case AlbionEventCodes.NewKillTrophyItem:
            case AlbionEventCodes.NewJournalItem:
            case AlbionEventCodes.NewLaborerItem:
            case AlbionEventCodes.NewEquipmentItemLegendarySoul:
                _values.Record(
                    PacketValueReader.GetInt(packet.Parameters, 1),
                    PacketValueReader.GetLong(packet.Parameters, 4));
                break;
        }

        return NextAsync(packet);
    }
}
