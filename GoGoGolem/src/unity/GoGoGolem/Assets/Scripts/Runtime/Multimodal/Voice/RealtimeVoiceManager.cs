using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using Multimodal.AIBridge;
using Multimodal.Config;

namespace Multimodal.Voice
{
    /// <summary>
    /// 음성 처리 통합 매니저 (Realtime Server VAD 방식)
    ///
    /// 특징:
    /// - OpenAI Realtime API 사용
    /// - Server VAD (Unity에서 VAD 불필요)
    /// - 실시간 TEXT_DELTA 스트리밍
    /// - 24kHz 샘플레이트
    ///
    /// 동작 흐름:
    /// 1. StartVoice() 호출
    /// 2. 마이크 시작 (24kHz)
    /// 3. 200ms마다 모든 오디오 청크 전송
    /// 4. 서버가 자동으로 발화/침묵 감지
    /// 5. TEXT_DELTA 실시간 스트리밍
    /// 6. RESPONSE_END로 응답 완료
    /// 7. 멀티턴 대화 계속 (StopVoice 호출 전까지)
    ///
    /// 게임 사용법:
    /// ```csharp
    /// voiceManager.OnSpeechDetected += () => Debug.Log("사용자 말하기 시작");
    /// voiceManager.OnTranscript += (text) => Debug.Log($"인식: {text}");
    /// voiceManager.OnStreamingText += (delta) => UpdateUI(delta); // 실시간!
    /// voiceManager.OnAIResponse += (text) => Debug.Log($"완성: {text}");
    ///
    /// await voiceManager.StartVoice();
    /// // 사용자가 자유롭게 대화...
    /// voiceManager.StopVoice();
    /// ```
    /// </summary>
    public class RealtimeVoiceManager : MonoBehaviour
    {
        #region Inspector Fields
        [Header("Audio Settings")]
        [SerializeField] private int sampleRate = 24000; // Realtime은 24kHz
        [SerializeField] private float chunkIntervalSeconds = 0.2f; // 200ms마다 전송

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;
        #endregion

        #region Events (게임이 구독)
        /// 연결 성공 시 발생
        public event Action OnConnected;

        /// 연결 해제 시 발생
        public event Action<string> OnDisconnected;

        /// 발화 감지 시 발생 (OpenAI VAD)
        public event Action OnSpeechDetected;

        /// 음성 인식 결과 수신
        public event Action<string> OnTranscript;

        /// AI 응답 텍스트 델타 (실시간 스트리밍)
        public event Action<string> OnStreamingText;

        /// AI 최종 응답 (전체 텍스트)
        public event Action<string> OnAIResponse;

        /// 에러 발생 시 발생
        public event Action<string, string> OnError; // (error_code, error_message)
        #endregion

        #region Private Fields
        private RealtimeWebSocketClient _realtimeClient;
        private MicrophoneRecorder _micRecorder;

        private bool _isVoiceActive;
        private string _currentSessionId;
        private Coroutine _audioStreamingCoroutine;

        // 실시간 응답 누적 (UI 표시용)
        private string _currentResponse = "";
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            // 컴포넌트 초기화
            _realtimeClient = new RealtimeWebSocketClient(ServerConfig.RealtimeWsUrl);
            _micRecorder = new MicrophoneRecorder();

            // WebSocket 이벤트 구독
            _realtimeClient.OnConnected += HandleConnected;
            _realtimeClient.OnDisconnected += HandleDisconnected;
            _realtimeClient.OnSpeechStarted += HandleSpeechStarted;
            _realtimeClient.OnTranscript += HandleTranscript;
            _realtimeClient.OnTextDelta += HandleTextDelta;
            _realtimeClient.OnResponseEnd += HandleResponseEnd;
            _realtimeClient.OnError += HandleError;

            // 마이크 이벤트 구독
            _micRecorder.OnError += (msg) => HandleError("MIC_ERROR", msg);

            DebugLog("RealtimeVoiceManager initialized");
        }

        private void OnDestroy()
        {
            // 정리
            StopVoice();
            _realtimeClient?.Dispose();
        }
        #endregion

        #region Public API (게임이 호출)
        /// 음성 대화 시작
        ///
        /// 1. WebSocket 연결
        /// 2. 스트리밍 시작
        /// 3. 마이크 시작
        /// 4. 오디오 전송 시작
        public async Task StartVoice(string language = "ko")
        {
            if (_isVoiceActive)
            {
                Debug.LogWarning("[RealtimeVoiceManager] Voice already active");
                return;
            }

            try
            {
                DebugLog("Starting voice...");

                // 1. WebSocket 연결
                if (!_realtimeClient.IsConnected)
                {
                    await _realtimeClient.ConnectAsync();
                }

                // 2. 스트리밍 시작
                _currentSessionId = Guid.NewGuid().ToString();
                await _realtimeClient.StartStreamAsync(_currentSessionId, language);

                // 3. 마이크 시작 (24kHz)
                if (!_micRecorder.StartRecording(sampleRate))
                {
                    throw new Exception("Failed to start microphone");
                }

                // 4. 오디오 전송 시작
                _isVoiceActive = true;
                _currentResponse = "";
                _audioStreamingCoroutine = StartCoroutine(StreamAudioCoroutine());

                DebugLog($"Voice started: session={_currentSessionId}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RealtimeVoiceManager] Start voice failed: {ex.Message}");
                OnError?.Invoke("START_FAILED", ex.Message);

                // 정리
                StopVoice();
                throw;
            }
        }

        /// 음성 대화 중지
        /// 1. 오디오 전송 중지
        /// 2. 마이크 중지
        /// 3. 스트리밍 중지
        /// 4. WebSocket 연결 해제
        public void StopVoice()
        {
            if (!_isVoiceActive)
            {
                return;
            }

            try
            {
                DebugLog("Stopping voice...");

                // 1. 오디오 전송 중지
                if (_audioStreamingCoroutine != null)
                {
                    StopCoroutine(_audioStreamingCoroutine);
                    _audioStreamingCoroutine = null;
                }

                // 2. 마이크 중지
                _micRecorder.StopRecording();

                // 3. 스트리밍 중지
                if (_realtimeClient.IsStreaming)
                {
                    _ = _realtimeClient.StopStreamAsync();
                }

                // 4. WebSocket 연결 해제
                if (_realtimeClient.IsConnected)
                {
                    _ = _realtimeClient.DisconnectAsync();
                }

                _isVoiceActive = false;
                _currentSessionId = null;

                DebugLog("Voice stopped");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RealtimeVoiceManager] Stop voice failed: {ex.Message}");
            }
        }

        #endregion

        #region Audio Streaming
        /// 오디오 스트리밍 코루틴
        ///
        /// Server VAD 방식:
        /// - 모든 오디오 청크를 계속 전송
        /// - Unity에서 VAD 수행하지 않음
        /// - 서버가 자동으로 발화/침묵 감지
        private IEnumerator StreamAudioCoroutine()
        {
            var waitInterval = new WaitForSeconds(chunkIntervalSeconds);
            int chunkSize = _micRecorder.TimeToSamples(chunkIntervalSeconds);

            DebugLog($"Audio streaming started: chunk={chunkSize} samples, interval={chunkIntervalSeconds}s");

            while (_isVoiceActive)
            {
                // 최신 오디오 청크 추출 (PCM16)
                byte[] audioChunk = _micRecorder.GetLatestAudioChunkAsPCM16(chunkSize);

                if (audioChunk != null && audioChunk.Length > 0)
                {
                    // 서버로 전송 (모든 청크!)
                    _ = SendAudioChunkAsync(audioChunk);
                }

                yield return waitInterval;
            }

            DebugLog("Audio streaming stopped");
        }

        private async Task SendAudioChunkAsync(byte[] audioData)
        {
            try
            {
                await _realtimeClient.SendAudioChunkAsync(audioData);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RealtimeVoiceManager] Send audio chunk failed: {ex.Message}");
            }
        }
        #endregion

        #region Event Handlers
        private void HandleConnected()
        {
            DebugLog("WebSocket connected");
            OnConnected?.Invoke();
        }

        private void HandleDisconnected(string reason)
        {
            DebugLog($"WebSocket disconnected: {reason}");
            _isVoiceActive = false;
            OnDisconnected?.Invoke(reason);
        }

        private void HandleSpeechStarted()
        {
            DebugLog("Speech detected by OpenAI VAD");
            OnSpeechDetected?.Invoke();
        }

        private void HandleTranscript(string transcript)
        {
            DebugLog($"Transcript: {transcript}");
            OnTranscript?.Invoke(transcript);
        }

        private void HandleTextDelta(string delta)
        {
            // 실시간 스트리밍 - 응답 누적
            _currentResponse += delta;
            OnStreamingText?.Invoke(delta);

            DebugLog($"Text delta: {delta}", verbose: true);
        }

        private void HandleResponseEnd(string fullText)
        {
            DebugLog($"Response complete: {fullText}");

            // 최종 응답
            OnAIResponse?.Invoke(fullText);

            // 초기화 (다음 턴 준비)
            _currentResponse = "";
        }

        private void HandleError(string errorCode, string errorMessage)
        {
            Debug.LogError($"[RealtimeVoiceManager] Error: {errorCode} - {errorMessage}");
            OnError?.Invoke(errorCode, errorMessage);
        }
        #endregion

        #region Debug
        private void DebugLog(string message, bool verbose = false)
        {
            if (!enableDebugLogs)
            {
                return;
            }

            if (verbose && Application.isEditor)
            {
                // 에디터에서만 상세 로그
                Debug.Log($"[RealtimeVoiceManager] {message}");
            }
            else if (!verbose)
            {
                Debug.Log($"[RealtimeVoiceManager] {message}");
            }
        }
        #endregion

        #region Properties
        /// 음성 대화 활성화 여부
        public bool IsVoiceActive => _isVoiceActive;

        /// WebSocket 연결 상태
        public bool IsConnected => _realtimeClient?.IsConnected ?? false;

        /// 현재 세션 ID
        public string CurrentSessionId => _currentSessionId;

        /// 현재 누적 중인 응답 텍스트
        public string CurrentResponse => _currentResponse;

        /// 샘플레이트 (Hz)
        public int SampleRate => sampleRate;
        #endregion
    }
}
