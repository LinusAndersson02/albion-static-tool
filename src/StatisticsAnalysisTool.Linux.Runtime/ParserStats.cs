using StatisticsAnalysisTool.Network;

namespace StatisticsAnalysisTool.Linux.Runtime;

public sealed class ParserStats
{
    private long _eventCount;
    private long _requestCount;
    private long _responseCount;

    public void RegisterHandlers(ReceiverBuilder builder)
    {
        builder.AddHandler(new CountingPacketHandler<EventPacket>(() => Interlocked.Increment(ref _eventCount)));
        builder.AddHandler(new CountingPacketHandler<RequestPacket>(() => Interlocked.Increment(ref _requestCount)));
        builder.AddHandler(new CountingPacketHandler<ResponsePacket>(() => Interlocked.Increment(ref _responseCount)));
    }

    public ParserStatsSnapshot GetSnapshot()
    {
        return new ParserStatsSnapshot(
            Interlocked.Read(ref _eventCount),
            Interlocked.Read(ref _requestCount),
            Interlocked.Read(ref _responseCount));
    }

    private sealed class CountingPacketHandler<TPacket> : PacketHandler<TPacket>
    {
        private readonly Action _increment;

        public CountingPacketHandler(Action increment)
        {
            _increment = increment;
        }

        protected override Task OnHandleAsync(TPacket packet)
        {
            _increment();
            return Task.CompletedTask;
        }
    }
}
