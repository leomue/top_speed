using System;
using System.Collections.Generic;
using System.Numerics;
using Concentus.Structs;
using TopSpeed.Audio;
using TopSpeed.Input;
using TopSpeed.Protocol;
using TS.Audio;

namespace TopSpeed.Vehicles.Live
{
    internal sealed class LiveRadio : IDisposable
    {
        private const int MaxBufferedFrames = 12;

        private readonly AudioManager _audio;
        private readonly RaceSettings _settings;
        private readonly object _lock = new object();
        private readonly Queue<float[]> _frames;

        private AudioSourceHandle? _source;
        private OpusDecoder? _decoder;
        private short[] _decodeBuffer;
        private float[]? _activeFrame;
        private int _activeFrameOffset;
        private int _volumePercent;
        private bool _desiredPlaying;
        private bool _pausedByGame;
        private uint _streamId;
        private ushort _sampleRate;
        private byte _channels;
        private byte _frameMs;
        private Vector3 _position;
        private Vector3 _velocity;
        private long _receivedFrames;
        private long _decodedFrames;
        private long _droppedFrames;
        private long _decodeErrors;
        private long _underruns;
        private long _lastFrameUtcTicks;

        public LiveRadio(AudioManager audio, RaceSettings settings)
        {
            _audio = audio ?? throw new ArgumentNullException(nameof(audio));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _frames = new Queue<float[]>(MaxBufferedFrames);
            _decodeBuffer = Array.Empty<short>();
            _volumePercent = 100;
        }

        public bool IsActive
        {
            get
            {
                lock (_lock)
                    return _streamId != 0 && _decoder != null;
            }
        }

        public uint StreamId
        {
            get
            {
                lock (_lock)
                    return _streamId;
            }
        }

        public long ReceivedFrames
        {
            get
            {
                lock (_lock)
                    return _receivedFrames;
            }
        }

        public long DecodedFrames
        {
            get
            {
                lock (_lock)
                    return _decodedFrames;
            }
        }

        public long DroppedFrames
        {
            get
            {
                lock (_lock)
                    return _droppedFrames;
            }
        }

        public long DecodeErrors
        {
            get
            {
                lock (_lock)
                    return _decodeErrors;
            }
        }

        public long Underruns
        {
            get
            {
                lock (_lock)
                    return _underruns;
            }
        }

        public long LastFrameUtcTicks
        {
            get
            {
                lock (_lock)
                    return _lastFrameUtcTicks;
            }
        }

        public bool Start(uint streamId, LiveCodec codec, ushort sampleRate, byte channels, byte frameMs)
        {
            if (streamId == 0)
                return false;
            if (codec != LiveCodec.Opus)
                return false;
            if (channels < ProtocolConstants.LiveChannelsMin || channels > ProtocolConstants.LiveChannelsMax)
                return false;
            if (sampleRate != ProtocolConstants.LiveSampleRate)
                return false;
            if (frameMs != ProtocolConstants.LiveFrameMs)
                return false;

            lock (_lock)
            {
                if (_streamId == streamId && _decoder != null)
                {
                    UpdatePlaybackLocked();
                    return true;
                }

                StopLocked();
                _decoder = OpusDecoder.Create(sampleRate, channels);
                _streamId = streamId;
                _sampleRate = sampleRate;
                _channels = channels;
                _frameMs = frameMs;
                var samplesPerChannel = (_sampleRate * _frameMs) / 1000;
                _decodeBuffer = new short[Math.Max(1, samplesPerChannel * _channels)];
                _source = _audio.CreateProceduralSource(OnRender, _channels, _sampleRate, useHrtf: true);
                _source.SetDopplerFactor(0f);
                _source.SetPosition(_position);
                _source.SetVelocity(_velocity);
                _source.SetVolumePercent(_settings, AudioVolumeCategory.Radio, _volumePercent);
                UpdatePlaybackLocked();
                return true;
            }
        }

        public bool PushFrame(uint streamId, byte[] payload, uint _timestamp)
        {
            if (payload == null || payload.Length == 0)
                return false;
            if (payload.Length > ProtocolConstants.MaxLiveFrameBytes)
                return false;

            lock (_lock)
            {
                if (_decoder == null || _streamId == 0 || streamId != _streamId)
                    return false;

                var samplesPerChannel = (_sampleRate * _frameMs) / 1000;
                if (samplesPerChannel <= 0)
                    return false;

                int decodedPerChannel;
                try
                {
                    decodedPerChannel = _decoder.Decode(payload, 0, payload.Length, _decodeBuffer, 0, samplesPerChannel, false);
                }
                catch
                {
                    _decodeErrors++;
                    return false;
                }

                if (decodedPerChannel <= 0)
                {
                    _decodeErrors++;
                    return false;
                }

                var sampleCount = decodedPerChannel * _channels;
                var frame = new float[sampleCount];
                for (var i = 0; i < sampleCount; i++)
                    frame[i] = _decodeBuffer[i] / 32768f;

                if (_frames.Count >= MaxBufferedFrames)
                {
                    _frames.Dequeue();
                    _droppedFrames++;
                }

                _frames.Enqueue(frame);
                _receivedFrames++;
                _decodedFrames++;
                _lastFrameUtcTicks = DateTime.UtcNow.Ticks;

                if (_source != null && _desiredPlaying && !_pausedByGame && !_source.IsPlaying)
                    _source.Play(loop: true);

                return true;
            }
        }

        public void Stop(uint streamId)
        {
            lock (_lock)
            {
                if (_streamId == 0)
                    return;
                if (streamId != 0 && streamId != _streamId)
                    return;
                StopLocked();
            }
        }

        public void SetPlayback(bool playing)
        {
            lock (_lock)
            {
                _desiredPlaying = playing;
                UpdatePlaybackLocked();
            }
        }

        public void PauseForGame()
        {
            lock (_lock)
            {
                _pausedByGame = true;
                UpdatePlaybackLocked();
            }
        }

        public void ResumeFromGame()
        {
            lock (_lock)
            {
                _pausedByGame = false;
                UpdatePlaybackLocked();
            }
        }

        public void SetVolumePercent(int volumePercent)
        {
            if (volumePercent < 0)
                volumePercent = 0;
            if (volumePercent > 100)
                volumePercent = 100;

            lock (_lock)
            {
                _volumePercent = volumePercent;
                _source?.SetVolumePercent(_settings, AudioVolumeCategory.Radio, _volumePercent);
            }
        }

        public void UpdateSpatial(Vector3 position, Vector3 velocity)
        {
            lock (_lock)
            {
                _position = position;
                _velocity = velocity;
                _source?.SetPosition(position);
                _source?.SetVelocity(velocity);
            }
        }

        public void Dispose()
        {
            lock (_lock)
                StopLocked();
        }

        private void UpdatePlaybackLocked()
        {
            if (_source == null)
                return;

            if (_desiredPlaying && !_pausedByGame)
            {
                if (!_source.IsPlaying)
                    _source.Play(loop: true);
            }
            else if (_source.IsPlaying)
            {
                _source.Stop();
            }
        }

        private void StopLocked()
        {
            if (_source != null)
            {
                _source.Stop();
                _source.Dispose();
                _source = null;
            }

            _decoder = null;
            _streamId = 0;
            _sampleRate = 0;
            _channels = 0;
            _frameMs = 0;
            _decodeBuffer = Array.Empty<short>();
            _activeFrame = null;
            _activeFrameOffset = 0;
            _frames.Clear();
        }

        private void OnRender(float[] buffer, int frames, int channels, ref ulong frameIndex)
        {
            if (buffer == null || frames <= 0 || channels <= 0)
                return;

            lock (_lock)
            {
                var sampleCount = frames * channels;
                if (sampleCount <= 0 || sampleCount > buffer.Length)
                    return;

                var cursor = 0;
                for (var frame = 0; frame < frames; frame++)
                {
                    if (_activeFrame == null || _activeFrameOffset + channels > _activeFrame.Length)
                    {
                        if (_frames.Count > 0)
                        {
                            _activeFrame = _frames.Dequeue();
                            _activeFrameOffset = 0;
                        }
                        else
                        {
                            _activeFrame = null;
                            _underruns++;
                        }
                    }

                    if (_activeFrame == null)
                    {
                        for (var ch = 0; ch < channels; ch++)
                            buffer[cursor++] = 0f;
                        continue;
                    }

                    for (var ch = 0; ch < channels; ch++)
                        buffer[cursor++] = _activeFrame[_activeFrameOffset++];
                }

                if (cursor < sampleCount)
                {
                    for (var i = cursor; i < sampleCount; i++)
                        buffer[i] = 0f;
                }
            }
        }
    }
}
