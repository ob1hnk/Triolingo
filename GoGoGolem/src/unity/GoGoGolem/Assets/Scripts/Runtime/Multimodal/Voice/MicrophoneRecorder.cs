using System;
using UnityEngine;

namespace Multimodal.Voice
{
    /// <summary>
    /// 마이크 녹음 공통 로직
    /// 
    /// - Unity Microphone API 활용
    /// - 순환 버퍼 방식으로 최신 오디오 추출
    /// - Float → PCM16 변환 지원
    /// - 샘플레이트 동적 설정 (16kHz, 24kHz)
    /// 
    /// 사용법:
    /// 1. StartRecording(sampleRate)
    /// 2. GetLatestAudioChunk(chunkSize) 반복 호출
    /// 3. StopRecording()
    /// </summary>
    public class MicrophoneRecorder
    {
        #region Fields
        private AudioClip _recordingClip;
        private string _deviceName;
        private int _sampleRate;
        private int _lastReadPosition;
        private bool _isRecording;

        // 오디오 설정
        private const int RecordingLength = 10; // 10초 순환 버퍼
        private const int Channels = 1; // Mono
        #endregion

        #region Events
        /// 녹음 시작 시 발생
        public event Action OnRecordingStarted;

        /// 녹음 중지 시 발생
        public event Action OnRecordingStopped;

        /// 마이크 에러 발생 시 발생
        public event Action<string> OnError;
        #endregion

        #region Recording Control
        /// 
        /// 녹음 시작
        /// sampleRate: 16000 또는 24000, deviceName = 마이크 디바이스 이름 (null = 기본 마이크)
        public bool StartRecording(int sampleRate = 24000, string deviceName = null)
        {
            if (_isRecording)
            {
                Debug.LogWarning("[MicRecorder] Already recording");
                return false;
            }

            try
            {
                // 마이크 디바이스 확인
                if (Microphone.devices.Length == 0)
                {
                    var errorMsg = "No microphone devices found";
                    Debug.LogError($"[MicRecorder] {errorMsg}");
                    OnError?.Invoke(errorMsg);
                    return false;
                }

                _deviceName = deviceName ?? (Microphone.devices.Length > 0 ? Microphone.devices[0] : null);
                _sampleRate = sampleRate;
                _lastReadPosition = 0;

                // AudioClip 생성 및 녹음 시작
                _recordingClip = Microphone.Start(
                    _deviceName,
                    loop: true,
                    RecordingLength,
                    _sampleRate
                );

                if (_recordingClip == null)
                {
                    var errorMsg = "Failed to create AudioClip";
                    Debug.LogError($"[MicRecorder] {errorMsg}");
                    OnError?.Invoke(errorMsg);
                    return false;
                }

                _isRecording = true;

                Debug.Log($"[MicRecorder] Recording started: {_deviceName}, {_sampleRate}Hz");
                OnRecordingStarted?.Invoke();

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MicRecorder] Start recording failed: {ex.Message}");
                OnError?.Invoke(ex.Message);
                return false;
            }
        }

        /// 녹음 중지
        public void StopRecording()
        {
            if (!_isRecording)
            {
                return;
            }

            try
            {
                if (Microphone.IsRecording(_deviceName))
                {
                    Microphone.End(_deviceName);
                }

                _isRecording = false;
                _recordingClip = null;
                _lastReadPosition = 0;

                Debug.Log("[MicRecorder] Recording stopped");
                OnRecordingStopped?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MicRecorder] Stop recording failed: {ex.Message}");
                OnError?.Invoke(ex.Message);
            }
        }
        #endregion

        #region Audio Data Extraction
        /// 최신 오디오 청크 추출 (Float 배열)
        /// 순환 버퍼에서 마지막 읽은 위치 이후의 새로운 샘플들을 반환합니다.
        /// chunkSizeInSamples= 추출할 샘플 수 (0 = 모든 새로운 샘플)
        public float[] GetLatestAudioChunk(int chunkSizeInSamples = 0)
        {
            if (!_isRecording || _recordingClip == null)
            {
                return null;
            }

            try
            {
                // 현재 마이크 위치
                int currentPosition = Microphone.GetPosition(_deviceName);

                if (currentPosition < 0)
                {
                    Debug.LogWarning("[MicRecorder] Invalid microphone position");
                    return null;
                }

                // 읽을 샘플 수 계산
                int samplesToRead;

                if (chunkSizeInSamples > 0)
                {
                    // 고정 크기
                    samplesToRead = chunkSizeInSamples;
                }
                else
                {
                    // 새로운 샘플만
                    if (currentPosition == _lastReadPosition)
                    {
                        return null; // 새로운 데이터 없음
                    }

                    // 순환 버퍼 처리
                    if (currentPosition > _lastReadPosition)
                    {
                        samplesToRead = currentPosition - _lastReadPosition;
                    }
                    else
                    {
                        // 버퍼가 순환한 경우
                        samplesToRead = (_recordingClip.samples - _lastReadPosition) + currentPosition;
                    }
                }

                // 버퍼 크기 제한
                samplesToRead = Mathf.Min(samplesToRead, _recordingClip.samples);

                if (samplesToRead <= 0)
                {
                    return null;
                }

                // 샘플 추출
                float[] samples = new float[samplesToRead];
                _recordingClip.GetData(samples, _lastReadPosition);

                // 읽기 위치 업데이트
                _lastReadPosition = (currentPosition) % _recordingClip.samples;

                return samples;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MicRecorder] Get audio chunk failed: {ex.Message}");
                OnError?.Invoke(ex.Message);
                return null;
            }
        }

        /// 최신 오디오 청크를 PCM16 바이트 배열로 반환
        /// chunkSizeInSamples = PCM16 바이트 배열
        public byte[] GetLatestAudioChunkAsPCM16(int chunkSizeInSamples = 0)
        {
            var samples = GetLatestAudioChunk(chunkSizeInSamples);

            if (samples == null || samples.Length == 0)
            {
                return null;
            }

            return ConvertToPCM16(samples);
        }
        #endregion

        #region Audio Conversion
        /// Float 샘플을 PCM16 바이트 배열로 변환
        public static byte[] ConvertToPCM16(float[] samples)
        {
            if (samples == null || samples.Length == 0)
            {
                return Array.Empty<byte>();
            }

            byte[] pcm16 = new byte[samples.Length * 2]; // 각 샘플 = 2 bytes (16-bit)

            for (int i = 0; i < samples.Length; i++)
            {
                // Float (-1.0 ~ 1.0) -> Int16 (-32768 ~ 32767)
                float clamped = Mathf.Clamp(samples[i], -1.0f, 1.0f);
                short sample16 = (short)(clamped * short.MaxValue);

                // Little Endian 바이트 순서
                pcm16[i * 2] = (byte)(sample16 & 0xFF);
                pcm16[i * 2 + 1] = (byte)((sample16 >> 8) & 0xFF);
            }

            return pcm16;
        }

        /// 시간(초)을 샘플 수로 변환
        public int TimeToSamples(float seconds)
        {
            return Mathf.RoundToInt(seconds * _sampleRate);
        }

        /// 샘플 수를 시간(초)으로 변환
        public float SamplesToTime(int samples)
        {
            return (float)samples / _sampleRate;
        }
        #endregion

        #region Audio Analysis (VAD용)
        /// 오디오 에너지 계산 (RMS)
        public static float CalculateRMS(float[] samples)
        {
            if (samples == null || samples.Length == 0)
            {
                return 0f;
            }

            float sum = 0f;
            foreach (var sample in samples)
            {
                sum += sample * sample;
            }

            return Mathf.Sqrt(sum / samples.Length);
        }

        /// 오디오 에너지 계산 (평균 절대값)
        public static float CalculateEnergy(float[] samples)
        {
            if (samples == null || samples.Length == 0)
            {
                return 0f;
            }

            float sum = 0f;
            foreach (var sample in samples)
            {
                sum += Mathf.Abs(sample);
            }

            return sum / samples.Length;
        }
        #endregion

        #region Properties
        /// 녹음 중 여부
        public bool IsRecording => _isRecording;

        /// 샘플레이트 (Hz)
        public int SampleRate => _sampleRate;

        /// 사용 중인 마이크 디바이스 이름
        public string DeviceName => _deviceName;

        /// 녹음 중인 AudioClip
        public AudioClip RecordingClip => _recordingClip;

        /// 사용 가능한 마이크 디바이스 목록
        public static string[] AvailableDevices => Microphone.devices;
        #endregion
    }
}