using TopSpeed.Protocol;
using TopSpeed.Server.Protocol;

namespace TopSpeed.Server.Network
{
    internal sealed partial class RaceServer
    {
        private void RegisterLivePackets()
        {
            _pktReg.Add("live", Command.PlayerLiveStart, (player, payload, endPoint) =>
            {
                if (PacketSerializer.TryReadPlayerLiveStart(payload, out var start))
                    OnLiveStart(player, start);
                else
                    PacketFail(endPoint, Command.PlayerLiveStart);
            });
            _pktReg.Add("live", Command.PlayerLiveFrame, (player, payload, endPoint) =>
            {
                if (PacketSerializer.TryReadPlayerLiveFrame(payload, out var frame))
                    OnLiveFrame(player, frame);
                else
                    PacketFail(endPoint, Command.PlayerLiveFrame);
            });
            _pktReg.Add("live", Command.PlayerLiveStop, (player, payload, endPoint) =>
            {
                if (PacketSerializer.TryReadPlayerLiveStop(payload, out var stop))
                    OnLiveStop(player, stop);
                else
                    PacketFail(endPoint, Command.PlayerLiveStop);
            });
        }
    }
}
