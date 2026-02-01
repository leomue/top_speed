using System;

namespace TS.Audio
{
    internal sealed class LateReverb
    {
        private const float DefaultGain = 0.015f;

        private readonly int _sampleRate;
        private readonly Comb[] _combL;
        private readonly Comb[] _combR;
        private readonly Allpass[] _allpassL;
        private readonly Allpass[] _allpassR;
        private float _wet;
        private float _width;
        private float _wet1;
        private float _wet2;
        private float _damping;

        public LateReverb(int sampleRate)
        {
            _sampleRate = Math.Max(22050, sampleRate);
            _combL = CreateCombs(_sampleRate, 0);
            _combR = CreateCombs(_sampleRate, 7);
            _allpassL = CreateAllpasses(_sampleRate, 0);
            _allpassR = CreateAllpasses(_sampleRate, 11);

            SetWet(0f);
            SetWidth(1f);
            SetDamping(0.2f);
            SetDecaySeconds(1.5f);
        }

        public void SetWet(float wet)
        {
            _wet = Clamp01(wet);
            UpdateMix();
        }

        public void SetWidth(float width)
        {
            _width = Clamp01(width);
            UpdateMix();
        }

        public void SetDamping(float damping)
        {
            _damping = Clamp01(damping);
            for (int i = 0; i < _combL.Length; i++)
                _combL[i].SetDamping(_damping);
            for (int i = 0; i < _combR.Length; i++)
                _combR[i].SetDamping(_damping);
        }

        public void SetDecaySeconds(float seconds)
        {
            if (seconds <= 0.01f)
                return;

            for (int i = 0; i < _combL.Length; i++)
                _combL[i].SetFeedback(FeedbackFor(_combL[i].Buffer.Length, seconds));
            for (int i = 0; i < _combR.Length; i++)
                _combR[i].SetFeedback(FeedbackFor(_combR[i].Buffer.Length, seconds));
        }

        public unsafe void Process(float* input, float* outL, float* outR, int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                float sample = input[i] * DefaultGain;

                float left = 0f;
                float right = 0f;

                for (int j = 0; j < _combL.Length; j++)
                    left += _combL[j].Process(sample);
                for (int j = 0; j < _combR.Length; j++)
                    right += _combR[j].Process(sample);

                for (int j = 0; j < _allpassL.Length; j++)
                    left = _allpassL[j].Process(left);
                for (int j = 0; j < _allpassR.Length; j++)
                    right = _allpassR[j].Process(right);

                outL[i] = left * _wet1 + right * _wet2;
                outR[i] = right * _wet1 + left * _wet2;
            }
        }

        private void UpdateMix()
        {
            _wet1 = _wet * (0.5f + 0.5f * _width);
            _wet2 = _wet * (0.5f - 0.5f * _width);
        }

        private float FeedbackFor(int delaySamples, float seconds)
        {
            float delaySeconds = delaySamples / (float)_sampleRate;
            float feedback = (float)Math.Pow(10f, (-3f * delaySeconds) / seconds);
            return Clamp(feedback, 0f, 0.98f);
        }

        private static Comb[] CreateCombs(int sampleRate, int seedOffset)
        {
            float[] delaysMs = { 29.7f, 37.1f, 41.1f, 43.7f, 47.1f, 53.1f };
            var combs = new Comb[delaysMs.Length];
            for (int i = 0; i < combs.Length; i++)
            {
                int delay = Math.Max(1, (int)Math.Round(sampleRate * (delaysMs[i] + seedOffset * 0.1f) / 1000f));
                combs[i].Init(delay);
            }
            return combs;
        }

        private static Allpass[] CreateAllpasses(int sampleRate, int seedOffset)
        {
            float[] delaysMs = { 5.0f, 1.7f };
            var allpasses = new Allpass[delaysMs.Length];
            for (int i = 0; i < allpasses.Length; i++)
            {
                int delay = Math.Max(1, (int)Math.Round(sampleRate * (delaysMs[i] + seedOffset * 0.1f) / 1000f));
                allpasses[i].Init(delay);
                allpasses[i].Feedback = 0.5f;
            }
            return allpasses;
        }

        private static float Clamp01(float value)
        {
            return Clamp(value, 0f, 1f);
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private struct Comb
        {
            public float[] Buffer;
            public int Index;
            public float Feedback;
            public float Damp;
            public float Damp1;
            public float FilterStore;

            public void Init(int size)
            {
                Buffer = new float[size];
                Index = 0;
                Feedback = 0.5f;
                Damp = 0.2f;
                Damp1 = 0.8f;
                FilterStore = 0f;
            }

            public void SetFeedback(float feedback)
            {
                Feedback = feedback;
            }

            public void SetDamping(float damping)
            {
                Damp = damping;
                Damp1 = 1f - damping;
            }

            public float Process(float input)
            {
                float output = Buffer[Index];
                FilterStore = (output * Damp1) + (FilterStore * Damp);
                Buffer[Index] = input + (FilterStore * Feedback);
                Index++;
                if (Index >= Buffer.Length)
                    Index = 0;
                return output;
            }
        }

        private struct Allpass
        {
            public float[] Buffer;
            public int Index;
            public float Feedback;

            public void Init(int size)
            {
                Buffer = new float[size];
                Index = 0;
                Feedback = 0.5f;
            }

            public float Process(float input)
            {
                float bufout = Buffer[Index];
                float output = -input + bufout;
                Buffer[Index] = input + (bufout * Feedback);
                Index++;
                if (Index >= Buffer.Length)
                    Index = 0;
                return output;
            }
        }
    }
}
