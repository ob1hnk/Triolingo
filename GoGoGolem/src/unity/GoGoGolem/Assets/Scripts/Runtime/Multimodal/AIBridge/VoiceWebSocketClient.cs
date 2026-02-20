using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Multimodal.AIBridge
{
    /// <summary>
    /// 음성 처리 WebSocket 클라이언트 (v1, v2 - Unity VAD 방식)
    /// 
    /// 특징:
    /// - Unity에서 VAD 수행 (UnityVADProcessor 사용)
    /// - 음성 감지된 청크만 전송 (네트워크 최적화)
    /// 
    /// 프로토콜:
    /// 1. SESSION_START: 음성 감지 시 세션 시작
    /// 2. AUDIO_CHUNK: 음성 청크 전송 (여러 번)
    /// 3. SESSION_END: 무음 1.5초 지속 시 세션 종료
    /// 
    /// 응답:
    /// - ACK: 수신 확인
    /// - PROCESSING: 처리 중
    /// - RESULT: 최종 결과 (transcription + response)
    /// - ERROR: 에러
    /// 
    /// v1 vs v2:
    /// - v1: STT → LLM (2단계 파이프라인, transcription 제공)
    /// - v2: Speech-to-Speech API (1단계, transcription 없음)
    /// </summary>
    public class VoiceWebSocketClient : IDisposable
    {
        #region Message Types
        private static class MessageType
        {
            // Client -> Server
            public const string SESSION_START = "session_start";
            public const string AUDIO_CHUNK = "audio_chunk";
            public const string SESSION_END = "session_end";

            // Server -> Client
            public const string ACK = "ack";
            public const string PROCESSING = "processing";
            public const string RESULT = "result";
            public const string ERROR = "error";
        }
        #endregion

        #region Events
        public event Action OnConnected;

        public event Action<string> OnDisconnected;

        public event Action<string> OnSessionStarted; // session_id

        public event Action<string, int> OnChunkAcknowledged; // session_id, chunk_index

        public event Action<string, float> OnProcessing; // status, progress

        public event Action<string, string> OnResult; // transcription, response

        public event Action<string, string> OnError; // error_code, error_message
        #endregion

        #region Fields
        private NativeWebSocket.WebSocket _webSocket;
        private readonly string _serverUrl;
        private readonly string _endpoint;
        private bool _isConnected;
        private CancellationTokenSource _cts;

        private string _currentSessionId;
        private int _chunkIndex;
        #endregion

        #region Constructor
        /// <summary>
        /// WebSocket 클라이언트 생성
        /// </summary>
        /// serverUrl: 서버 URL, version: 1 or 2
        public VoiceWebSocketClient(string serverUrl, int version = 2)
        {
            _serverUrl = serverUrl.TrimEnd('/');
            _endpoint = $"/ws/speech/v{version}";
        }
        #endregion

        #region Connection Management
        /// WebSocket 연결 수립
        public async Task ConnectAsync()
        {
            if (_isConnected)
            {
                Debug.LogWarning("[VoiceWS] Already connected");
                return;
            }

            try
            {
                _cts = new CancellationTokenSource();

                // WebSocket 생성
                _webSocket = new NativeWebSocket.WebSocket($"{_serverUrl}{_endpoint}");

                // 이벤트 핸들러 등록
                _webSocket.OnOpen += HandleOpen;
                _webSocket.OnMessage += HandleMessage;
                _webSocket.OnError += HandleError;
                _webSocket.OnClose += HandleClose;

                // 연결 시도
                await _webSocket.Connect();

                // 메시지 처리 루프 시작
                _ = ProcessMessagesAsync(_cts.Token);

                Debug.Log($"[VoiceWS] Connecting to {_endpoint}...");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VoiceWS] Connection failed: {ex.Message}");
                OnError?.Invoke("CONNECTION_FAILED", ex.Message);
                throw;
            }
        }

        /// WebSocket 연결 해제
        public async Task DisconnectAsync()
        {
            if (!_isConnected)
            {
                return;
            }

            try
            {
                _cts?.Cancel();

                if (_webSocket != null && _webSocket.State == NativeWebSocket.WebSocketState.Open)
                {
                    await _webSocket.Close();
                }

                _isConnected = false;
                Debug.Log("[VoiceWS] Disconnected");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VoiceWS] Disconnect error: {ex.Message}");
            }
        }
        #endregion

        #region Session Management
        /// 세션 시작 (음성 감지 시)
        /// sessionId: 세션 ID, audioFormat: 오디오 포맷(wav), sampleRate, 16000, channels: 1
        public async Task StartSessionAsync(
            string sessionId,
            string audioFormat = "wav",
            int sampleRate = 16000,
            int channels = 1)
        {
            if (!_isConnected)
            {
                throw new InvalidOperationException("Not connected to WebSocket");
            }

            _currentSessionId = sessionId;
            _chunkIndex = 0;

            var message = new JObject
            {
                ["type"] = MessageType.SESSION_START,
                ["session_id"] = sessionId,
                ["audio_format"] = audioFormat,
                ["sample_rate"] = sampleRate,
                ["channels"] = channels
            };

            await SendJsonAsync(message);

            Debug.Log($"[VoiceWS] Session started: {sessionId}, {sampleRate}Hz");
        }

        /// 오디오 청크 전송
        /// audioData: PCM16 오디오 바이트 배열, isLastChunk: 마지막 청크 여부
        public async Task SendAudioChunkAsync(byte[] audioData, bool isLastChunk = false)
        {
            if (string.IsNullOrEmpty(_currentSessionId))
            {
                throw new InvalidOperationException("Session not started");
            }

            var base64Audio = Convert.ToBase64String(audioData);

            var message = new JObject
            {
                ["type"] = MessageType.AUDIO_CHUNK,
                ["session_id"] = _currentSessionId,
                ["chunk_index"] = _chunkIndex,
                ["audio_data"] = base64Audio,
                ["is_last_chunk"] = isLastChunk
            };

            await SendJsonAsync(message);

            _chunkIndex++;
        }

        /// 세션 종료 (무음 지속 시)
        public async Task EndSessionAsync()
        {
            if (string.IsNullOrEmpty(_currentSessionId))
            {
                Debug.LogWarning("[VoiceWS] No active session to end");
                return;
            }

            var message = new JObject
            {
                ["type"] = MessageType.SESSION_END,
                ["session_id"] = _currentSessionId
            };

            await SendJsonAsync(message);

            Debug.Log($"[VoiceWS] Session ended: {_currentSessionId}, chunks: {_chunkIndex}");
        }
        #endregion

        #region Message Handling
        private void HandleOpen()
        {
            _isConnected = true;
            Debug.Log("[VoiceWS] WebSocket connected");
            OnConnected?.Invoke();
        }

        private void HandleMessage(byte[] data)
        {
            try
            {
                var json = Encoding.UTF8.GetString(data);
                var message = JObject.Parse(json);
                var type = message["type"]?.ToString();

                Debug.Log($"[VoiceWS] Received: {type}");

                switch (type)
                {
                    case MessageType.ACK:
                        HandleAck(message);
                        break;

                    case MessageType.PROCESSING:
                        HandleProcessing(message);
                        break;

                    case MessageType.RESULT:
                        HandleResult(message);
                        break;

                    case MessageType.ERROR:
                        HandleErrorMessage(message);
                        break;

                    default:
                        Debug.LogWarning($"[VoiceWS] Unknown message type: {type}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VoiceWS] Message handling error: {ex.Message}");
            }
        }

        private void HandleAck(JObject message)
        {
            var sessionId = message["session_id"]?.ToString();
            var ackMessage = message["message"]?.ToString();
            var chunkIndex = message["chunk_index"]?.ToObject<int?>();

            if (chunkIndex.HasValue)
            {
                // 청크 ACK
                Debug.Log($"[VoiceWS] Chunk {chunkIndex.Value} acknowledged");
                OnChunkAcknowledged?.Invoke(sessionId, chunkIndex.Value);
            }
            else
            {
                // 세션 시작 ACK
                Debug.Log($"[VoiceWS] ACK: {ackMessage}");
                OnSessionStarted?.Invoke(sessionId);
            }
        }

        private void HandleProcessing(JObject message)
        {
            var status = message["status"]?.ToString() ?? "Processing...";
            var progress = message["progress"]?.ToObject<float?>() ?? 0.5f;

            Debug.Log($"[VoiceWS] Processing: {status} ({progress:P0})");
            OnProcessing?.Invoke(status, progress);
        }

        private void HandleResult(JObject message)
        {
            var transcription = message["transcription"]?.ToString();
            var response = message["text"]?.ToString();

            Debug.Log($"[VoiceWS] Result - Transcription: {transcription}");
            Debug.Log($"[VoiceWS] Result - Response: {response}");

            OnResult?.Invoke(transcription, response);

            // 세션 초기화 (다음 세션 준비)
            _currentSessionId = null;
            _chunkIndex = 0;
        }

        private void HandleErrorMessage(JObject message)
        {
            var errorCode = message["error_code"]?.ToString() ?? "UNKNOWN";
            var errorMessage = message["error_message"]?.ToString() ?? "Unknown error";

            Debug.LogError($"[VoiceWS] Error: {errorCode} - {errorMessage}");
            OnError?.Invoke(errorCode, errorMessage);

            // 에러 시 세션 초기화
            _currentSessionId = null;
            _chunkIndex = 0;
        }

        private void HandleError(string error)
        {
            Debug.LogError($"[VoiceWS] WebSocket error: {error}");
            OnError?.Invoke("WEBSOCKET_ERROR", error);
        }

        private void HandleClose(NativeWebSocket.WebSocketCloseCode code)
        {
            _isConnected = false;
            _currentSessionId = null;
            _chunkIndex = 0;

            var reason = $"Code: {code}";
            Debug.Log($"[VoiceWS] WebSocket closed: {reason}");
            OnDisconnected?.Invoke(reason);
        }
        #endregion

        #region Helper Methods
        private async Task SendJsonAsync(JObject message)
        {
            if (_webSocket == null || _webSocket.State != NativeWebSocket.WebSocketState.Open)
            {
                throw new InvalidOperationException("WebSocket not connected");
            }

            var json = message.ToString(Formatting.None);
            await _webSocket.SendText(json);
        }

        private async Task ProcessMessagesAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && _webSocket != null)
                {
                    #if !UNITY_WEBGL || UNITY_EDITOR
                    _webSocket.DispatchMessageQueue();
                    #endif

                    await Task.Delay(10, ct);
                }
            }
            catch (OperationCanceledException)
            {
                // 정상 종료
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VoiceWS] Message processing error: {ex.Message}");
            }
        }
        #endregion

        #region IDisposable
        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _webSocket?.CancelConnection();
            _webSocket = null;
        }
        #endregion

        #region Properties
        public bool IsConnected => _isConnected;
        public string CurrentSessionId => _currentSessionId;
        public int ChunkIndex => _chunkIndex;
        public string Endpoint => _endpoint;
        #endregion
    }
}