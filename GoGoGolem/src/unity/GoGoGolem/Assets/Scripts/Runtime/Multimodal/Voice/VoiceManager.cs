using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Multimodal.AIBridge;
using Multimodal.Config;

namespace Multimodal.Voice
{
    /// <summary>
    /// 음성 처리 매니저 (v1, v2 - Unity VAD 방식)
    ///
    /// 특징:
    /// - Unity에서 VAD 수행 (UnityVADProcessor)
    /// - 음성 감지된 청크만 전송 (네트워크 최적화)
    /// - 세션 단위 처리 (명시적 EndSession() 호출로 종료)
    /// - 16kHz 샘플레이트
    ///
    /// 동작 흐름:
    /// 1. StartVoice() 호출
    /// 2. 마이크 시작 (16kHz)
    /// 3. 100ms마다 오디오 분석 (Unity VAD)
    /// 4. 음성 감지 → session_start
    /// 5. 음성 청크 전송 (계속)
    /// 6. EndSession() 호출 시 → session_end
    /// 7. 서버 응답 대기 (transcription + response)
    /// 8. 다음 턴 대화 (자동으로 다음 세션 시작 가능)
    ///
    /// v1 vs v2:
    /// - v1: STT + LLM (transcription 제공)
    /// - v2: Speech-to-Speech (transcription 없음, 더 빠름)
    ///
    /// 게임 사용법:
    /// ```csharp
    /// voiceManager.OnSpeechDetected += () => Debug.Log("음성 감지!");
    /// voiceManager.OnTranscript += (text) => Debug.Log($"인식: {text}");
    /// voiceManager.OnAIResponse += (text) => Debug.Log($"응답: {text}");
    ///
    /// await voiceManager.StartVoice();
    /// // 사용자 대화...
    /// voiceManager.StopVoice();
    /// ```
    /// </summary>
    public class VoiceManager : MonoBehaviour
    {
        #region Inspector Fields
        [Header("Version")]
        [SerializeField] private int version = 2; // 1 = v1, 2 = v2

        [Header("Audio Settings")]
        [SerializeField] private int sampleRate = 16000; // v1, v2는 16kHz
        [SerializeField] private float chunkIntervalSeconds = 0.1f; // 100ms마다 분석

        [Header("VAD Settings")]
        [SerializeField] private float vadThreshold = 0.0001f;
        [SerializeField] private float silenceTimeout = 1.5f;
        [SerializeField] private float minSpeechDuration = 0.3f;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;
        #endregion

        #region Events (게임이 구독)
        /// 연결 성공
        public event Action OnConnected;

        /// 연결 해제
        public event Action<string> OnDisconnected;

        /// 음성 감지 (Unity VAD)
        public event Action OnSpeechDetected;

        /// 세션 시작 (서버 확인)
        public event Action<string> OnSessionStarted; // session_id

        /// 처리 중
        public event Action<string, float> OnProcessing; // status, progress

        /// 음성 인식 결과 (v1만, v2는 null)
        public event Action<string> OnTranscript; // transcription

        /// AI 최종 응답
        public event Action<string> OnAIResponse; // response

        /// 에러 발생
        public event Action<string, string> OnError; // error_code, error_message
        #endregion

        #region Private Fields
        private VoiceWebSocketClient _wsClient;
        private MicrophoneRecorder _micRecorder;
        private UnityVADProcessor _vadProcessor;

        private bool _isVoiceActive;
        private bool _isSessionActive;
        private string _currentSessionId;
        private Coroutine _audioProcessingCoroutine;

        private List<byte[]> _sessionChunks = new List<byte[]>();
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            // 컴포넌트 초기화
            _wsClient = new VoiceWebSocketClient(ServerConfig.SpeechWsUrl, version);
            _micRecorder = new MicrophoneRecorder();
            _vadProcessor = new UnityVADProcessor
            {
                VadThreshold = vadThreshold,
                SilenceTimeout = silenceTimeout,
                MinSpeechDuration = minSpeechDuration,
                EnableDebugLogs = enableDebugLogs
            };

            // WebSocket 이벤트 구독
            _wsClient.OnConnected += HandleConnected;
            _wsClient.OnDisconnected += HandleDisconnected;
            _wsClient.OnSessionStarted += HandleSessionStarted;
            _wsClient.OnChunkAcknowledged += HandleChunkAcknowledged;
            _wsClient.OnProcessing += HandleProcessing;
            _wsClient.OnResult += HandleResult;
            _wsClient.OnError += HandleError;

            // 마이크 이벤트 구독
            _micRecorder.OnError += (msg) => HandleError("MIC_ERROR", msg);

            DebugLog($"VoiceManager initialized (v{version})");
        }

        private void OnDestroy()
        {
            StopVoice();
            _wsClient?.Dispose();
        }
        #endregion

        #region Public API (게임이 호출)
        /// 음성 대화 시작
        public async Task StartVoice()
        {
            if (_isVoiceActive)
            {
                Debug.LogWarning("[VoiceManager] Voice already active");
                return;
            }

            try
            {
                DebugLog("Starting voice...");

                // 1. WebSocket 연결
                if (!_wsClient.IsConnected)
                {
                    await _wsClient.ConnectAsync();
                }

                // 2. 마이크 시작 (16kHz)
                if (!_micRecorder.StartRecording(sampleRate))
                {
                    throw new Exception("Failed to start microphone");
                }

                // 3. VAD 초기화
                _vadProcessor.Reset();

                // 4. 오디오 처리 시작
                _isVoiceActive = true;
                _isSessionActive = false;
                _sessionChunks.Clear();
                _audioProcessingCoroutine = StartCoroutine(ProcessAudioCoroutine());

                DebugLog("Voice started");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VoiceManager] Start voice failed: {ex.Message}");
                OnError?.Invoke("START_FAILED", ex.Message);

                StopVoice();
                throw;
            }
        }

        /// 음성 대화 중지
        public void StopVoice()
        {
            if (!_isVoiceActive)
            {
                return;
            }

            try
            {
                DebugLog("Stopping voice...");

                // 1. 오디오 처리 중지
                if (_audioProcessingCoroutine != null)
                {
                    StopCoroutine(_audioProcessingCoroutine);
                    _audioProcessingCoroutine = null;
                }

                // 2. 마이크 중지
                _micRecorder.StopRecording();

                // 3. 활성 세션이 있으면 종료
                if (_isSessionActive)
                {
                    _ = EndCurrentSessionAsync();
                }

                // 4. WebSocket 연결 해제
                if (_wsClient.IsConnected)
                {
                    _ = _wsClient.DisconnectAsync();
                }

                _isVoiceActive = false;
                _isSessionActive = false;

                DebugLog("Voice stopped");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VoiceManager] Stop voice failed: {ex.Message}");
            }
        }

        /// 현재 세션 종료 (게임에서 명시적으로 호출)
        /// session_end를 서버에 전송하여 응답을 요청
        public async Task EndSession()
        {
            if (!_isSessionActive)
            {
                Debug.LogWarning("[VoiceManager] No active session to end");
                return;
            }

            await EndCurrentSessionAsync();
        }

        #endregion

        #region Audio Processing (Unity VAD)
        /// 오디오 처리 코루틴 (Unity VAD)
        private IEnumerator ProcessAudioCoroutine()
        {
            var waitInterval = new WaitForSeconds(chunkIntervalSeconds);
            int chunkSize = _micRecorder.TimeToSamples(chunkIntervalSeconds);

            DebugLog($"Audio processing started: chunk={chunkSize} samples, interval={chunkIntervalSeconds}s");

            while (_isVoiceActive)
            {
                // 최신 오디오 청크 추출
                float[] audioSamples = _micRecorder.GetLatestAudioChunk(chunkSize);

                if (audioSamples != null && audioSamples.Length > 0)
                {
                    // Unity VAD 처리
                    bool isSpeechActive = _vadProcessor.ProcessAudio(audioSamples, chunkIntervalSeconds);

                    // 세션 시작 판단
                    if (!_isSessionActive && _vadProcessor.ShouldStartSession())
                    {
                        OnSpeechDetected?.Invoke();
                        _ = StartNewSessionAsync();
                    }

                    // 음성 활동 중이면 청크 전송
                    if (_isSessionActive && isSpeechActive)
                    {
                        byte[] pcm16 = MicrophoneRecorder.ConvertToPCM16(audioSamples);
                        _sessionChunks.Add(pcm16);
                        _ = SendAudioChunkAsync(pcm16);
                    }
                }

                yield return waitInterval;
            }

            DebugLog("Audio processing stopped");
        }

        private async Task StartNewSessionAsync()
        {
            try
            {
                _currentSessionId = Guid.NewGuid().ToString();
                _sessionChunks.Clear();
                _isSessionActive = true;

                await _wsClient.StartSessionAsync(
                    _currentSessionId,
                    audioFormat: "wav",
                    sampleRate: sampleRate,
                    channels: 1
                );

                _vadProcessor.MarkSessionStarted();
                DebugLog($"Session started: {_currentSessionId}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VoiceManager] Start session failed: {ex.Message}");
                _isSessionActive = false;
            }
        }

        private async Task SendAudioChunkAsync(byte[] audioData)
        {
            try
            {
                await _wsClient.SendAudioChunkAsync(audioData);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VoiceManager] Send chunk failed: {ex.Message}");
            }
        }

        private async Task EndCurrentSessionAsync()
        {
            try
            {
                await _wsClient.EndSessionAsync();

                _isSessionActive = false;
                _vadProcessor.MarkSessionEnded();

                DebugLog($"Session ended: {_currentSessionId}, chunks: {_sessionChunks.Count}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VoiceManager] End session failed: {ex.Message}");
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
            _isSessionActive = false;
            OnDisconnected?.Invoke(reason);
        }

        private void HandleSessionStarted(string sessionId)
        {
            DebugLog($"Session started confirmed: {sessionId}");
            OnSessionStarted?.Invoke(sessionId);
        }

        private void HandleChunkAcknowledged(string sessionId, int chunkIndex)
        {
            // 청크 ACK는 로그만 (필요시 이벤트 추가 가능)
            DebugLog($"Chunk {chunkIndex} acknowledged", verbose: true);
        }

        private void HandleProcessing(string status, float progress)
        {
            DebugLog($"Processing: {status} ({progress:P0})");
            OnProcessing?.Invoke(status, progress);
        }

        private void HandleResult(string transcription, string response)
        {
            DebugLog($"Result - Transcription: {transcription}");
            DebugLog($"Result - Response: {response}");

            // v1은 transcription 제공, v2는 null
            if (!string.IsNullOrEmpty(transcription))
            {
                OnTranscript?.Invoke(transcription);
            }

            // 최종 응답
            OnAIResponse?.Invoke(response);

            // 세션 초기화 (다음 세션 준비)
            _sessionChunks.Clear();
        }

        private void HandleError(string errorCode, string errorMessage)
        {
            Debug.LogError($"[VoiceManager] Error: {errorCode} - {errorMessage}");
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
                Debug.Log($"[VoiceManager] {message}");
            }
            else if (!verbose)
            {
                Debug.Log($"[VoiceManager] {message}");
            }
        }
        #endregion

        #region Properties
        public bool IsVoiceActive => _isVoiceActive;
        public bool IsSessionActive => _isSessionActive;
        public bool IsConnected => _wsClient?.IsConnected ?? false;
        public string CurrentSessionId => _currentSessionId;
        public int Version => version;
        public int SampleRate => sampleRate;
        public bool IsSpeechActive => _vadProcessor?.IsSpeechActive ?? false;
        #endregion
    }
}