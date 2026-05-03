using System.Buffers.Binary;
using System.Net.Sockets;
using System.Threading;
using Libpcap;
using StatisticsAnalysisTool.Abstractions;

namespace StatisticsAnalysisTool.Capture;

public sealed class LibpcapLiveCaptureService : ILiveCaptureService, IDisposable
{
    private static readonly HashSet<ushort> PhotonUdpPorts = [5055, 5056, 5058];
    private static readonly TimeSpan StopThreadJoinTimeout = TimeSpan.FromSeconds(2);

    private readonly IPhotonReceiver _photonReceiver;
    private readonly object _sync = new();

    private PcapDispatcher? _dispatcher;
    private CancellationTokenSource? _cts;
    private Thread? _thread;
    private int _openedDeviceCount;
    private long _capturedPacketCount;
    private long _photonPayloadCount;
    private long _receiverErrorCount;
    private string? _activeDeviceName;
    private DateTimeOffset? _lastPhotonPayloadAt;
    private string? _lastErrorMessage;

    public bool IsRunning => _thread is { IsAlive: true };

    public LibpcapLiveCaptureService(IPhotonReceiver photonReceiver)
    {
        _photonReceiver = photonReceiver;
    }

    public LiveCaptureStatus Start(LiveCaptureOptions options)
    {
        lock (_sync)
        {
            if (IsRunning)
            {
                return GetStatus();
            }

            ResetCounters();

            try
            {
                _dispatcher?.Dispose();
                _dispatcher = new PcapDispatcher(Dispatch);
                _cts?.Dispose();
                _cts = new CancellationTokenSource();

                var devices = Pcap.ListDevices();
                var hasDeviceSelection = options.SelectedDeviceIdentifiers.Count > 0;
                var opened = 0;

                foreach (var device in devices)
                {
                    if (device.Flags.HasFlag(PcapDeviceFlags.Loopback) || !device.Flags.HasFlag(PcapDeviceFlags.Up))
                    {
                        continue;
                    }

                    if (hasDeviceSelection && !options.SelectedDeviceIdentifiers.Contains(device.Name ?? string.Empty))
                    {
                        continue;
                    }

                    try
                    {
                        _dispatcher.OpenDevice(device, pcap =>
                        {
                            pcap.NonBlocking = true;
                        });

                        if (!string.IsNullOrWhiteSpace(options.PacketFilter))
                        {
                            _dispatcher.Filter = options.PacketFilter;
                        }

                        opened++;
                    }
                    catch (Exception exception)
                    {
                        _lastErrorMessage = $"Open failed for {device.Name}: {exception.Message}";
                    }
                }

                if (opened == 0)
                {
                    _lastErrorMessage ??= "No active non-loopback libpcap devices could be opened.";
                    return GetStatus();
                }

                _openedDeviceCount = opened;
                _thread = new Thread(Worker)
                {
                    IsBackground = true,
                    Name = "SAT Linux libpcap capture"
                };
                _thread.Start();
            }
            catch (Exception exception)
            {
                _lastErrorMessage = exception.Message;
            }

            return GetStatus();
        }
    }

    public LiveCaptureStatus Stop()
    {
        lock (_sync)
        {
            try
            {
                _cts?.Cancel();
                _dispatcher?.Dispose();

                if (_thread is { IsAlive: true })
                {
                    _thread.Join(StopThreadJoinTimeout);
                }
            }
            finally
            {
                _thread = null;
                _dispatcher = null;
                _cts?.Dispose();
                _cts = null;
                _openedDeviceCount = 0;
                _activeDeviceName = null;
            }

            return GetStatus();
        }
    }

    public LiveCaptureStatus GetStatus()
    {
        return new LiveCaptureStatus(
            IsRunning,
            _openedDeviceCount,
            Interlocked.Read(ref _capturedPacketCount),
            Interlocked.Read(ref _photonPayloadCount),
            Interlocked.Read(ref _receiverErrorCount),
            _activeDeviceName,
            _lastPhotonPayloadAt,
            _lastErrorMessage);
    }

    private void ResetCounters()
    {
        Interlocked.Exchange(ref _capturedPacketCount, 0);
        Interlocked.Exchange(ref _photonPayloadCount, 0);
        Interlocked.Exchange(ref _receiverErrorCount, 0);
        _openedDeviceCount = 0;
        _activeDeviceName = null;
        _lastPhotonPayloadAt = null;
        _lastErrorMessage = null;
    }

    private void Worker()
    {
        var dispatcher = _dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        try
        {
            while (_cts is { IsCancellationRequested: false })
            {
                try
                {
                    var dispatched = dispatcher.Dispatch(50);
                    if (dispatched <= 0)
                    {
                        _cts.Token.WaitHandle.WaitOne(25);
                    }
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (InvalidOperationException)
                {
                    break;
                }
                catch (PcapException exception)
                {
                    _lastErrorMessage = exception.Message;
                    _cts?.Token.WaitHandle.WaitOne(250);
                }
            }
        }
        catch (Exception exception)
        {
            _lastErrorMessage = exception.Message;
        }
    }

    private void Dispatch(Pcap pcap, ref Packet packet)
    {
        Interlocked.Increment(ref _capturedPacketCount);

        if (!TryGetEthernetUdpPayload(packet.Data, out var udpPayload, out var sourcePort, out var destinationPort))
        {
            return;
        }

        if (!PhotonUdpPorts.Contains(sourcePort) && !PhotonUdpPorts.Contains(destinationPort) && !LooksLikePhoton(udpPayload))
        {
            return;
        }

        Interlocked.Increment(ref _photonPayloadCount);
        _activeDeviceName = pcap.Name;
        _lastPhotonPayloadAt = DateTimeOffset.Now;

        try
        {
            _photonReceiver.ReceivePacket(udpPayload);
        }
        catch (Exception exception)
        {
            Interlocked.Increment(ref _receiverErrorCount);
            _lastErrorMessage = $"Photon parser failed: {exception.Message}";
        }
    }

    private static bool TryGetEthernetUdpPayload(
        ReadOnlySpan<byte> ethernetFrame,
        out ReadOnlySpan<byte> payload,
        out ushort sourcePort,
        out ushort destinationPort)
    {
        payload = default;
        sourcePort = 0;
        destinationPort = 0;

        if (ethernetFrame.Length < 14)
        {
            return false;
        }

        var etherType = BinaryPrimitives.ReadUInt16BigEndian(ethernetFrame[12..14]);
        var packet = ethernetFrame[14..];

        return etherType switch
        {
            0x0800 => TryGetIPv4UdpPayload(packet, out payload, out sourcePort, out destinationPort),
            0x86DD => TryGetIPv6UdpPayload(packet, out payload, out sourcePort, out destinationPort),
            _ => false
        };
    }

    private static bool TryGetIPv4UdpPayload(
        ReadOnlySpan<byte> packet,
        out ReadOnlySpan<byte> payload,
        out ushort sourcePort,
        out ushort destinationPort)
    {
        payload = default;
        sourcePort = 0;
        destinationPort = 0;

        if (packet.Length < 20 || (packet[0] >> 4) != 4)
        {
            return false;
        }

        var headerLength = (packet[0] & 0x0F) * 4;
        if (headerLength < 20 || packet.Length < headerLength + 8)
        {
            return false;
        }

        var flagsAndFragmentOffset = BinaryPrimitives.ReadUInt16BigEndian(packet[6..8]);
        var hasMoreFragments = (flagsAndFragmentOffset & 0x2000) != 0;
        var fragmentOffset = (flagsAndFragmentOffset & 0x1FFF) * 8;
        if (hasMoreFragments || fragmentOffset != 0 || (ProtocolType)packet[9] != ProtocolType.Udp)
        {
            return false;
        }

        return TryGetUdpDatagramPayload(packet[headerLength..], out payload, out sourcePort, out destinationPort);
    }

    private static bool TryGetIPv6UdpPayload(
        ReadOnlySpan<byte> packet,
        out ReadOnlySpan<byte> payload,
        out ushort sourcePort,
        out ushort destinationPort)
    {
        payload = default;
        sourcePort = 0;
        destinationPort = 0;

        if (packet.Length < 48 || (packet[0] >> 4) != 6 || (ProtocolType)packet[6] != ProtocolType.Udp)
        {
            return false;
        }

        return TryGetUdpDatagramPayload(packet[40..], out payload, out sourcePort, out destinationPort);
    }

    private static bool TryGetUdpDatagramPayload(
        ReadOnlySpan<byte> udpPacket,
        out ReadOnlySpan<byte> payload,
        out ushort sourcePort,
        out ushort destinationPort)
    {
        payload = default;
        sourcePort = 0;
        destinationPort = 0;

        if (udpPacket.Length < 8)
        {
            return false;
        }

        sourcePort = BinaryPrimitives.ReadUInt16BigEndian(udpPacket[..2]);
        destinationPort = BinaryPrimitives.ReadUInt16BigEndian(udpPacket[2..4]);
        var udpLength = BinaryPrimitives.ReadUInt16BigEndian(udpPacket[4..6]);
        var payloadLength = Math.Min(udpPacket.Length - 8, Math.Max(0, udpLength - 8));
        if (payloadLength <= 0)
        {
            return false;
        }

        payload = udpPacket.Slice(8, payloadLength);
        return true;
    }

    private static bool LooksLikePhoton(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 3)
        {
            return false;
        }

        return payload[0] is 0xF1 or 0xF2 or 0xFE;
    }

    public void Dispose()
    {
        Stop();
    }
}
