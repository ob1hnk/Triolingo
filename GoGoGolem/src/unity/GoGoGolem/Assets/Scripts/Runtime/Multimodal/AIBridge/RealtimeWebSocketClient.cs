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
    /// Realtime API WebSocket 클라이언트 (Server VAD 방식)
    /// 
    /// - Unity에서 VAD 수행하지 않음
    /// - 모든 오디오 청크를 서버로 전송
    /// - OpenAI가 Server VAD로 발화/침묵 자동 감지
    /// - 실시간 TEXT_DELTA 스트리밍 지원
    /// 
    /// 프로토콜:
    /// 1. STREAM_START: 스트리밍 시작
    /// 2. STREAM_AUDIO: 오디오 청크 전송 (계속)
    /// 3. STREAM_STOP: 스트리밍 종료
    /// 
    /// 응답:
    /// - STREAM_ACK: 연결 확인
    /// - SPEECH_STARTED: 발화 감지
    /// - TRANSCRIPT: 음성 인식 결과
    /// - TEXT_DELTA: 응답 텍스트 스트리밍
    /// - RESPONSE_END: 응답 완료
    /// - STREAM_ERROR: 에러
    /// </summary>
    public class RealtimeWebSocketClient : IDisposable
    {
        #region Message Types
        private static class MessageType
        {
            // Client -> Server
            public const string STREAM_START = "STREAM_START";
            public const string STREAM_AUDIO = "STREAM_AUDIO";
            public const string STREAM_STOP = "STREAM_STOP";

            // Server -> Client
            public const string STREAM_ACK = "STREAM_ACK";
            public const string SPEECH_STARTED = "SPEECH_STARTED";
            public const string TRANSCRIPT = "TRANSCRIPT";
            public const string TEXT_DELTA = "TEXT_DELTA";
            public const string RESPONSE_END = "RESPONSE_END";
            public const string STREAM_ERROR = "STREAM_ERROR";
        }
        #endregion

        #region Events
        public event Action OnConnected;

        public event Action<string> OnDisconnected;

        public event Action OnSpeechStarted;

        public event Action<string> OnTranscript;

        public event Action<string> OnTextDelta;

        public event Action<string> OnResponseEnd;

        public event Action<string, string> OnError; // (error_code, error_message)
        #endregion

        #region Fields
        private NativeWebSocket.WebSocket _webSocket;
        private readonly string _serverUrl;
        private string _sessionId;
        private bool _isConnected;
        private bool _isStreaming;
        private CancellationTokenSource _cts;
        private TaskCompletionSource<bool> _connectTcs;

        private readonly Queue<byte[]> _sendQueue = new Queue<byte[]>();
        private readonly object _queueLock = new object();
        #endregion

        #region Constructor
        public RealtimeWebSocketClient(string serverUrl)
        {
            _serverUrl = serverUrl.TrimEnd('/');
        }
        #endregion

        #region Connection Management
        /// WebSocket 연결 수립
        public async Task ConnectAsync()
        {
            if (_isConnected)
            {
                Debug.LogWarning("[Realtime] Already connected");
                return;
            }

            try
            {
                _cts = new CancellationTokenSource();
                _connectTcs = new TaskCompletionSource<bool>();

                // WebSocket 생성
                _webSocket = new NativeWebSocket.WebSocket($"{_serverUrl}/ws/realtime/v1");

                // 이벤트 핸들러 등록
                _webSocket.OnOpen += HandleOpen;
                _webSocket.OnMessage += HandleMessage;
                _webSocket.OnError += HandleError;
                _webSocket.OnClose += HandleClose;

                Debug.Log("[Realtime] Connecting to WebSocket...");

                // 연결 시도 (Connect는 연결이 끊길 때까지 반환되지 않으므로 fire-and-forget)
                _ = _webSocket.Connect();

                // 메시지 처리 루프 시작 (OnOpen 콜백을 받기 위해 필요)
                _ = ProcessMessagesAsync(_cts.Token);

                // OnOpen 콜백이 호출될 때까지 대기 (최대 10초)
                var timeoutTask = Task.Delay(10000);
                var completedTask = await Task.WhenAny(_connectTcs.Task, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    throw new Exception("Connection timeout - OnOpen not received");
                }

                Debug.Log("[Realtime] Connection established successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Realtime] Connection failed: {ex.Message}");
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
                // 스트리밍 중이면 중지
                if (_isStreaming)
                {
                    await StopStreamAsync();
                }

                _cts?.Cancel();

                if (_webSocket != null && _webSocket.State == NativeWebSocket.WebSocketState.Open)
                {
                    await _webSocket.Close();
                }

                _isConnected = false;
                Debug.Log("[Realtime] Disconnected");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Realtime] Disconnect error: {ex.Message}");
            }
        }
        #endregion

        #region Streaming Control
        /// 스트리밍 시작
        /// sessionId: 세션 ID, language: 언어 코드 (기본: ko)
        public async Task StartStreamAsync(string sessionId, string language = "ko")
        {
            if (!_isConnected)
            {
                throw new InvalidOperationException("Not connected to WebSocket");
            }

            if (_isStreaming)
            {
                Debug.LogWarning("[Realtime] Stream already started");
                return;
            }

            _sessionId = sessionId;

            var message = new JObject
            {
                ["type"] = MessageType.STREAM_START,
                ["session_id"] = sessionId,
                ["language"] = language
            };

            await SendJsonAsync(message);
            _isStreaming = true;

            Debug.Log($"[Realtime] Stream started: {sessionId}, language: {language}");
        }

        /// 오디오 청크 전송 (Base64 인코딩)
        /// audioData: PCM16 오디오 바이트 배열
        public async Task SendAudioChunkAsync(byte[] audioData)
        {
            if (!_isStreaming)
            {
                Debug.LogWarning("[Realtime] Stream not started");
                return;
            }

            var base64Audio = Convert.ToBase64String(audioData);

            var message = new JObject
            {
                ["type"] = MessageType.STREAM_AUDIO,
                ["session_id"] = _sessionId,
                ["audio_data"] = base64Audio
            };

            await SendJsonAsync(message);
        }

        /// 스트리밍 중지
        public async Task StopStreamAsync()
        {
            if (!_isStreaming)
            {
                return;
            }

            var message = new JObject
            {
                ["type"] = MessageType.STREAM_STOP,
                ["session_id"] = _sessionId
            };

            await SendJsonAsync(message);
            _isStreaming = false;

            Debug.Log($"[Realtime] Stream stopped: {_sessionId}");
        }
        #endregion

        #region Message Handling
        private void HandleOpen()
        {
            _isConnected = true;
            Debug.Log("[Realtime] WebSocket connected");

            // ConnectAsync에서 대기 중인 TaskCompletionSource 완료
            _connectTcs?.TrySetResult(true);

            OnConnected?.Invoke();
        }

        private void HandleMessage(byte[] data)
        {
            try
            {
                var json = Encoding.UTF8.GetString(data);
                var message = JObject.Parse(json);
                var type = message["type"]?.ToString();

                Debug.Log($"[Realtime] Received: {type}");

                switch (type)
                {
                    case MessageType.STREAM_ACK:
                        HandleStreamAck(message);
                        break;

                    case MessageType.SPEECH_STARTED:
                        HandleSpeechStarted(message);
                        break;

                    case MessageType.TRANSCRIPT:
                        HandleTranscript(message);
                        break;

                    case MessageType.TEXT_DELTA:
                        HandleTextDelta(message);
                        break;

                    case MessageType.RESPONSE_END:
                        HandleResponseEnd(message);
                        break;

                    case MessageType.STREAM_ERROR:
                        HandleStreamError(message);
                        break;

                    default:
                        Debug.LogWarning($"[Realtime] Unknown message type: {type}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Realtime] Message handling error: {ex.Message}");
            }
        }

        private void HandleStreamAck(JObject message)
        {
            var ackMessage = message["message"]?.ToString();
            Debug.Log($"[Realtime] ACK: {ackMessage}");
        }

        private void HandleSpeechStarted(JObject message)
        {
            Debug.Log("[Realtime] Speech detected by OpenAI VAD");
            OnSpeechStarted?.Invoke();
        }

        private void HandleTranscript(JObject message)
        {
            var transcript = message["transcript"]?.ToString();
            if (!string.IsNullOrEmpty(transcript))
            {
                Debug.Log($"[Realtime] Transcript: {transcript}");
                OnTranscript?.Invoke(transcript);
            }
        }

        private void HandleTextDelta(JObject message)
        {
            var delta = message["delta"]?.ToString();
            if (!string.IsNullOrEmpty(delta))
            {
                // 실시간 스트리밍 - UI 업데이트
                OnTextDelta?.Invoke(delta);
            }
        }

        private void HandleResponseEnd(JObject message)
        {
            var fullText = message["full_text"]?.ToString();
            Debug.Log($"[Realtime] Response complete: {fullText}");
            OnResponseEnd?.Invoke(fullText);
        }

        private void HandleStreamError(JObject message)
        {
            var errorCode = message["error_code"]?.ToString() ?? "UNKNOWN";
            var errorMessage = message["error_message"]?.ToString() ?? "Unknown error";

            Debug.LogError($"[Realtime] Error: {errorCode} - {errorMessage}");
            OnError?.Invoke(errorCode, errorMessage);
        }

        private void HandleError(string error)
        {
            Debug.LogError($"[Realtime] WebSocket error: {error}");

            // 연결 중이었다면 TaskCompletionSource 실패 처리
            _connectTcs?.TrySetException(new Exception(error));

            OnError?.Invoke("WEBSOCKET_ERROR", error);
        }

        private void HandleClose(NativeWebSocket.WebSocketCloseCode code)
        {
            _isConnected = false;
            _isStreaming = false;

            var reason = $"Code: {code}";
            Debug.Log($"[Realtime] WebSocket closed: {reason}");

            // 연결 중이었다면 TaskCompletionSource 실패 처리
            _connectTcs?.TrySetException(new Exception($"Connection closed: {reason}"));

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
            var bytes = Encoding.UTF8.GetBytes(json);

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
                Debug.LogError($"[Realtime] Message processing error: {ex.Message}");
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
        public bool IsStreaming => _isStreaming;
        public string SessionId => _sessionId;
        #endregion
    }
}