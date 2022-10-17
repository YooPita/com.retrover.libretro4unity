﻿/* MIT License

 * Copyright (c) 2021-2022 Skurdt
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:

 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.

 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE. */

using SK.Libretro.Header;
using System;

namespace SK.Libretro
{
    internal sealed class AudioHandler : IDisposable
    {
        public bool Enabled { get; set; }

        private const float GAIN            = 1f;
        private const float NORMALIZED_GAIN = GAIN / 0x8000;

        private readonly Wrapper _wrapper;
        private readonly retro_audio_sample_t _sampleCallback;
        private readonly retro_audio_sample_batch_t _sampleBatchCallback;

        private IAudioProcessor _processor;

        private retro_audio_callback_t _audioCallback;
        private retro_audio_set_state_callback_t _audioCallbackSetState;
        private retro_audio_buffer_status_callback_t _audioBufferStatusCallback;
        private uint _minimumLatency;

        public AudioHandler(Wrapper wrapper) => (_wrapper, _sampleCallback, _sampleBatchCallback) = (wrapper, SampleCallback, SampleBatchCallback);

        public void Init(IAudioProcessor audioProcessor)
        {
            _processor = audioProcessor ?? new NullAudioProcessor();
            _processor.Init(_wrapper.Game.SystemAVInfo.SampleRate);
        }

        public void Dispose() => _processor?.Dispose();

        public void SetCoreCallbacks(retro_set_audio_sample_t setAudioSample, retro_set_audio_sample_batch_t setAudioSampleBatch)
        {
            setAudioSample(_sampleCallback);
            setAudioSampleBatch(_sampleBatchCallback);
        }

        public bool SetAudioCallback(IntPtr data)
        {
            if (data.IsNull())
                return false;

            retro_audio_callback callback = data.ToStructure<retro_audio_callback>();
            _audioCallback         = callback.callback.GetDelegate<retro_audio_callback_t>();
            _audioCallbackSetState = callback.set_state.GetDelegate<retro_audio_set_state_callback_t>();
            return true;
        }

        public bool SetAudioBufferStatusCallback(IntPtr data)
        {
            if (data.IsNull())
                return false;

            retro_audio_buffer_status_callback bufferStatusCallback = data.ToStructure<retro_audio_buffer_status_callback>();
            _audioBufferStatusCallback = bufferStatusCallback.callback.GetDelegate<retro_audio_buffer_status_callback_t>();
            return true;
        }

        public bool SetMinimumAudioLatency(IntPtr data)
        {
            if (data.IsNull())
                return false;

            _minimumLatency = data.ReadUInt32();
            return true;
        }

        private void SampleCallback(short left, short right)
        {
            if (!Enabled)
                return;

            float[] floatBuffer = new float[]
            {
                left  * NORMALIZED_GAIN,
                right * NORMALIZED_GAIN
            };

            _processor.ProcessSamples(floatBuffer);
        }

        private unsafe nuint SampleBatchCallback(IntPtr data, nuint frames)
        {
            if (Enabled)
            {
                short* dataPtr = (short*)data;
                ulong numSamples = frames * 2;
                float[] floatBuffer = new float[numSamples];
                for (ulong i = 0; i < numSamples; ++i)
                    floatBuffer[i] = dataPtr[i] * NORMALIZED_GAIN;

                _processor.ProcessSamples(floatBuffer);
            }
            return frames;
        }
    }
}
