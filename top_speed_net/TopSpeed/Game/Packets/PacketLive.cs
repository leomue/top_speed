using TopSpeed.Network;
using TopSpeed.Protocol;

namespace TopSpeed.Game
{
    internal sealed partial class Game
    {
        private void RegisterMultiplayerLivePacketHandlers()
        {
            _mpPktReg.Add("live", Command.PlayerLiveStart, HandleMpPlayerLiveStartPacket);
            _mpPktReg.Add("live", Command.PlayerLiveFrame, HandleMpPlayerLiveFramePacket);
            _mpPktReg.Add("live", Command.PlayerLiveStop, HandleMpPlayerLiveStopPacket);
        }

        private bool HandleMpPlayerLiveStartPacket(IncomingPacket packet)
        {
            if (_multiplayerRace == null)
                return true;

            if (ClientPacketSerializer.TryReadPlayerLiveStart(packet.Payload, out var start))
                _multiplayerRace.ApplyRemoteLiveStart(start, packet.ReceivedUtcTicks);
            return true;
        }

        private bool HandleMpPlayerLiveFramePacket(IncomingPacket packet)
        {
            if (_multiplayerRace == null)
                return true;

            if (ClientPacketSerializer.TryReadPlayerLiveFrame(packet.Payload, out var frame))
                _multiplayerRace.ApplyRemoteLiveFrame(frame, packet.ReceivedUtcTicks);
            return true;
        }

        private bool HandleMpPlayerLiveStopPacket(IncomingPacket packet)
        {
            if (_multiplayerRace == null)
                return true;

            if (ClientPacketSerializer.TryReadPlayerLiveStop(packet.Payload, out var stop))
                _multiplayerRace.ApplyRemoteLiveStop(stop);
            return true;
        }
    }
}
