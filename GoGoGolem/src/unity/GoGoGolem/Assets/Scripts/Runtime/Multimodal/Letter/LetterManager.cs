using System;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json.Linq;
using Multimodal.AIBridge;
using Multimodal.Config;

namespace Multimodal.Letter
{
    /// <summary>
    /// 편지 응답 생성 매니저
    ///
    /// 특징:
    /// - HTTP POST 기반 비동기 처리
    /// - 사용자 ID 관리
    /// - 에러 핸들링
    /// </summary>
    public class LetterManager : MonoBehaviour
    {
        #region Inspector Fields
        [Header("User Settings")]
        [SerializeField] private string userId = ""; // TODO: user_id 구현 필요

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;
        #endregion

        #region Events
        public event Action<string> OnLetterResponse; // response_text

        public event Action<string> OnLetterSending; // user_letter

        public event Action<string> OnProcessing; // task_id

        public event Action<string, string> OnError; // error_code, error_message
        #endregion

        #region Private Fields
        private AIHttpClient _httpClient;
        private bool _isProcessing;
        private string _currentTaskId;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            // HTTP 클라이언트 초기화
            _httpClient = new AIHttpClient(ServerConfig.LetterHttpUrl);
            _httpClient.SetTimeout(60); // 편지 생성은 시간이 걸릴 수 있음

            DebugLog($"LetterManager initialized - User ID: {userId}");
        }

        private void OnDestroy()
        {
            _httpClient?.Dispose();
        }
        #endregion

        #region Public API
        /// 편지 전송 및 응답 생성 요청
        /// userLetter: 사용자가 작성한 편지 내용
        public async Task<string> SendLetterAsync(string userLetter)
        {
            if (string.IsNullOrWhiteSpace(userLetter))
            {
                var error = "Letter content cannot be empty";
                OnError?.Invoke("INVALID_INPUT", error);
                throw new ArgumentException(error);
            }

            if (_isProcessing)
            {
                var error = "Already processing a letter";
                OnError?.Invoke("BUSY", error);
                throw new InvalidOperationException(error);
            }

            try
            {
                _isProcessing = true;

                DebugLog($"Sending letter (length: {userLetter.Length} chars)...");
                OnLetterSending?.Invoke(userLetter);

                // API 요청 데이터
                var requestData = new JObject
                {
                    ["user_id"] = userId,
                    ["user_letter"] = userLetter
                };

                // POST /api/letter
                var response = await _httpClient.PostJsonAsync("/api/v1/text/generate-letter", requestData);

                // 응답 처리
                var status = response["status"]?.ToString();
                _currentTaskId = response["task_id"]?.ToString();

                DebugLog($"Letter accepted - Status: {status}, Task ID: {_currentTaskId}");

                if (status == "accepted")
                {
                    OnProcessing?.Invoke(_currentTaskId);

                    // 서버는 비동기 처리 (Firebase에 결과 저장)
                    // 클라이언트는 task_id를 받고 Firebase에서 결과 폴링 필요
                    DebugLog($"Letter request accepted. Task ID: {_currentTaskId}");
                    DebugLog("Note: Response will be saved to Firebase. Poll Firebase for results.");

                    // task_id 반환 (
                    // TODO: 실제 응답은 Firebase에서 가져와야 함
                    return _currentTaskId;
                }
                else
                {
                    throw new Exception($"Unexpected status: {status}");
                }
            }
            catch (HttpRequestException ex)
            {
                var errorMsg = $"HTTP {ex.StatusCode}: {ex.Message}";
                Debug.LogError($"[LetterManager] {errorMsg}");
                OnError?.Invoke($"HTTP_{ex.StatusCode}", ex.Message);
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LetterManager] Send letter failed: {ex.Message}");
                OnError?.Invoke("SEND_FAILED", ex.Message);
                throw;
            }
            finally
            {
                _isProcessing = false;
                _currentTaskId = null;
            }
        }

        /// 사용자 ID 설정
        public void SetUserId(string newUserId)
        {
            if (string.IsNullOrWhiteSpace(newUserId))
            {
                Debug.LogWarning("[LetterManager] Invalid user ID");
                return;
            }

            userId = newUserId;
            DebugLog($"User ID changed to: {userId}");
        }

        #endregion

        #region Debug
        private void DebugLog(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[LetterManager] {message}");
            }
        }
        #endregion

        #region Properties
        public bool IsProcessing => _isProcessing;

        public string UserId => userId;

        public string CurrentTaskId => _currentTaskId;
        #endregion
    }
}