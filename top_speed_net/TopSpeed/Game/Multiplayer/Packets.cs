using TopSpeed.Network;

namespace TopSpeed.Game
{
    internal sealed partial class Game
    {
        private void ProcessMultiplayerPackets()
        {
            while (_queuedMultiplayerPackets.TryDequeue(out var queued))
            {
                if (!ReferenceEquals(_session, queued.Session))
                    continue;

                _mpPktReg.TryDispatch(queued.Packet);
                if (!ReferenceEquals(_session, queued.Session))
                    return;
            }
        }

        private void EnqueueMultiplayerPacket(MultiplayerSession session, IncomingPacket packet)
        {
            _queuedMultiplayerPackets.Enqueue(new QueuedIncomingPacket(session, packet));
        }

        private void ClearQueuedMultiplayerPackets()
        {
            while (_queuedMultiplayerPackets.TryDequeue(out _))
            {
            }
        }

        private void RegisterMultiplayerPacketHandlers()
        {
            RegisterMultiplayerControlPacketHandlers();
            RegisterMultiplayerRoomPacketHandlers();
            RegisterMultiplayerRaceStatePacketHandlers();
            RegisterMultiplayerRaceEventPacketHandlers();
            RegisterMultiplayerMediaPacketHandlers();
            RegisterMultiplayerLivePacketHandlers();
            RegisterMultiplayerChatPacketHandlers();
        }
    }
}
