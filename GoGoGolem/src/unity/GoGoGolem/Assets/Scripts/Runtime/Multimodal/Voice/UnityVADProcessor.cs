using UnityEngine;

namespace Multimodal.Voice
{
    /// <summary>
    /// Unity VAD (Voice Activity Detection) 프로세서
    /// 
    /// 특징:
    /// - 에너지 기반 음성 감지
    /// - 무음 지속 시간 추적
    /// - 세션 시작/종료 판단
    /// 
    /// 사용법:
    /// 1. ProcessAudio(samples) 호출하여 음성 감지
    /// 2. IsSpeechActive로 현재 상태 확인
    /// 3. ShouldStartSession으로 세션 시작 판단
    /// 4. ShouldEndSession으로 세션 종료 판단
    /// </summary>
    public class UnityVADProcessor
    {
        #region Configuration
        /// 음성 감지 임계값 (RMS 기준)
        public float VadThreshold { get; set; } = 0.0001f;

        /// 무음으로 판단하기까지 걸리는 시간 (초)
        public float SilenceTimeout { get; set; } = 1.5f;

        /// 세션 시작을 위한 최소 음성 지속 시간 (초)
        public float MinSpeechDuration { get; set; } = 0.3f;

        /// 디버그 로그 활성화
        public bool EnableDebugLogs { get; set; } = false;
        #endregion

        #region State
        private bool _isSpeechActive;
        private bool _hasDetectedSpeech;
        private float _silenceDuration;
        private float _speechDuration;
        private float _lastProcessTime;
        #endregion

        #region Constructor
        public UnityVADProcessor()
        {
            Reset();
        }
        #endregion

        #region Public API
        /// 
        /// 오디오 샘플을 분석하여 음성 활동 감지
        public bool ProcessAudio(float[] samples, float deltaTime)
        {
            if (samples == null || samples.Length == 0)
            {
                return _isSpeechActive;
            }

            // 오디오 에너지 계산
            float energy = MicrophoneRecorder.CalculateRMS(samples);

            // 음성 감지 판단
            bool isSpeechDetected = energy > VadThreshold;

            if (isSpeechDetected)
            {
                // 음성 감지됨
                if (!_isSpeechActive)
                {
                    _isSpeechActive = true;
                    _speechDuration = 0f;
                    DebugLog($"Speech started (energy: {energy:F6})");
                }

                _speechDuration += deltaTime;
                _silenceDuration = 0f;
                _hasDetectedSpeech = true;
            }
            else
            {
                // 무음 감지됨
                if (_isSpeechActive)
                {
                    _silenceDuration += deltaTime;

                    // 무음 지속 시간이 임계값 초과 시 음성 종료
                    if (_silenceDuration >= SilenceTimeout)
                    {
                        _isSpeechActive = false;
                        DebugLog($"Speech stopped (silence: {_silenceDuration:F2}s)");
                    }
                }
            }

            _lastProcessTime = Time.time;
            return _isSpeechActive;
        }

        /// 세션을 시작해야 하는지 판단
        /// (음성이 일정 시간 이상 지속되면 true)
        public bool ShouldStartSession()
        {
            return _isSpeechActive && 
                   _speechDuration >= MinSpeechDuration && 
                   !_hasDetectedSpeech; // 이미 세션 시작했으면 false
        }

        /// 세션을 종료해야 하는지 판단
        /// (무음이 일정 시간 이상 지속되면 true)
        public bool ShouldEndSession()
        {
            return _hasDetectedSpeech && 
                   !_isSpeechActive && 
                   _silenceDuration >= SilenceTimeout;
        }

        /// VAD 상태 초기화
        public void Reset()
        {
            _isSpeechActive = false;
            _hasDetectedSpeech = false;
            _silenceDuration = 0f;
            _speechDuration = 0f;
            _lastProcessTime = Time.time;

            DebugLog("VAD reset");
        }

        /// 세션 시작 확인 (외부에서 호출)
        public void MarkSessionStarted()
        {
            _hasDetectedSpeech = true;
            DebugLog("Session marked as started");
        }

        /// 세션 종료 확인 (외부에서 호출)
        public void MarkSessionEnded()
        {
            Reset();
            DebugLog("Session marked as ended");
        }
        #endregion

        #region Audio Analysis (Static Helpers)
        /// 오디오 샘플의 에너지 레벨 계산 (0.0 ~ 1.0)
        public static float CalculateEnergyLevel(float[] samples)
        {
            if (samples == null || samples.Length == 0)
            {
                return 0f;
            }

            float rms = MicrophoneRecorder.CalculateRMS(samples);
            
            // 일반적인 음성 범위로 정규화 (0.0 ~ 0.1)
            // 0.1 이상은 큰 소리로 간주
            return Mathf.Clamp01(rms * 10f);
        }

        /// 음성 감지 여부 (간단 버전)
        public static bool IsSpeechDetected(float[] samples, float threshold = 0.0001f)
        {
            if (samples == null || samples.Length == 0)
            {
                return false;
            }

            float rms = MicrophoneRecorder.CalculateRMS(samples);
            return rms > threshold;
        }
        #endregion

        #region Debug
        private void DebugLog(string message)
        {
            if (EnableDebugLogs)
            {
                Debug.Log($"[UnityVAD] {message}");
            }
        }
        #endregion

        #region Properties
        /// 현재 음성 활동 중인지 여부
        public bool IsSpeechActive => _isSpeechActive;

        /// 세션이 시작되었는지 여부
        public bool HasDetectedSpeech => _hasDetectedSpeech;

        /// 현재 무음 지속 시간 (초)
        public float SilenceDuration => _silenceDuration;

        /// 현재 음성 지속 시간 (초)
        public float SpeechDuration => _speechDuration;
        #endregion
    }
}