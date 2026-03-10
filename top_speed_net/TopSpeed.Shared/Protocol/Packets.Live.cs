using System;

namespace TopSpeed.Protocol
{
    public sealed class PacketPlayerLiveStart
    {
        public uint PlayerId;
        public byte PlayerNumber;
        public uint StreamId;
        public LiveCodec Codec;
        public ushort SampleRate;
        public byte Channels;
        public byte FrameMs;
    }

    public sealed class PacketPlayerLiveFrame
    {
        public uint PlayerId;
        public byte PlayerNumber;
        public uint StreamId;
        public ushort Sequence;
        public uint Timestamp;
        public byte[] Data = Array.Empty<byte>();
    }

    public sealed class PacketPlayerLiveStop
    {
        public uint PlayerId;
        public byte PlayerNumber;
        public uint StreamId;
    }
}
