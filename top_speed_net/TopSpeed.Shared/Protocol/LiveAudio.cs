using System;

namespace TopSpeed.Protocol
{
    public readonly struct LiveAudioProfile
    {
        public LiveAudioProfile(LiveCodec codec, ushort sampleRate, byte channels, byte frameMs)
        {
            Codec = codec;
            SampleRate = sampleRate;
            Channels = channels;
            FrameMs = frameMs;
        }

        public LiveCodec Codec { get; }
        public ushort SampleRate { get; }
        public byte Channels { get; }
        public byte FrameMs { get; }
    }

    public readonly struct LivePcmFrame
    {
        public LivePcmFrame(ushort sampleRate, byte channels, byte frameMs, short[] samples, uint timestamp)
        {
            SampleRate = sampleRate;
            Channels = channels;
            FrameMs = frameMs;
            Samples = samples ?? Array.Empty<short>();
            Timestamp = timestamp;
        }

        public ushort SampleRate { get; }
        public byte Channels { get; }
        public byte FrameMs { get; }
        public short[] Samples { get; }
        public uint Timestamp { get; }
    }

    public readonly struct LiveOpusFrame
    {
        public LiveOpusFrame(ushort sequence, uint timestamp, byte[] payload)
        {
            Sequence = sequence;
            Timestamp = timestamp;
            Payload = payload ?? Array.Empty<byte>();
        }

        public ushort Sequence { get; }
        public uint Timestamp { get; }
        public byte[] Payload { get; }
    }

    public interface ILiveEncoder
    {
        LiveAudioProfile Profile { get; }
        bool TryEncode(in LivePcmFrame input, out LiveOpusFrame output);
    }
}
