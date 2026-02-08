using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Firebase.Firestore;

namespace Multimodal.Letter
{
    /// <summary>
    /// 편지 응답 조회 컴포넌트 (Firebase Firestore)
    ///
    /// 온디맨드 방식: 게임에서 "편지 읽기" 이벤트 발생 시 호출
    /// 실시간 리스너가 아닌, 호출 시점에 Firestore에서 일회성 조회
    ///
    /// 사전 조건:
    /// - Firebase Unity SDK 설치 (FirebaseFirestore.unitypackage)
    /// - google-services.json 또는 GoogleService-Info.plist 배치
    ///
    /// 사용법:
    /// ```csharp
    /// var response = await letterReader.FetchLatestResponseAsync("user123");
    /// if (response != null)
    /// {
    ///     Debug.Log(response.GeneratedResponseLetter);
    /// }
    /// ```
    /// </summary>
    public class LetterReader : MonoBehaviour
    {
        #region Inspector Fields
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;
        #endregion

        #region Events
        public event Action<string, string> OnError; // error_code, error_message
        #endregion

        #region Private Fields
        private const string CollectionName = "letter_responses";
        private FirebaseFirestore _db;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            _db = FirebaseFirestore.DefaultInstance;
            DebugLog("LetterReader initialized");
        }
        #endregion

        #region Public API
        /// 가장 최근 편지 응답 1건 조회
        public async Task<LetterResponse> FetchLatestResponseAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                OnError?.Invoke("INVALID_INPUT", "User ID cannot be empty");
                return null;
            }

            try
            {
                DebugLog($"Fetching latest response for user: {userId}");

                var query = _db.Collection(CollectionName)
                    .WhereEqualTo("user_id", userId)
                    .OrderByDescending("created_at")
                    .Limit(1);

                var snapshot = await query.GetSnapshotAsync();

                if (snapshot.Count == 0)
                {
                    DebugLog("No letter response found");
                    return null;
                }

                var doc = snapshot.Documents.First();
                var response = DocumentToLetterResponse(doc);

                DebugLog($"Letter response found: {response.Id}");
                return response;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LetterReader] Fetch latest failed: {ex.Message}");
                OnError?.Invoke("FETCH_FAILED", ex.Message);
                return null;
            }
        }

        /// user_id에 해당하는 모든 편지 응답 조회
        public async Task<LetterResponse[]> FetchAllResponsesAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                OnError?.Invoke("INVALID_INPUT", "User ID cannot be empty");
                return Array.Empty<LetterResponse>();
            }

            try
            {
                DebugLog($"Fetching all responses for user: {userId}");

                var query = _db.Collection(CollectionName)
                    .WhereEqualTo("user_id", userId)
                    .OrderByDescending("created_at");

                var snapshot = await query.GetSnapshotAsync();

                var docs = snapshot.Documents.ToList();
                var responses = new LetterResponse[docs.Count];
                for (int i = 0; i < docs.Count; i++)
                {
                    responses[i] = DocumentToLetterResponse(docs[i]);
                }

                DebugLog($"Found {responses.Length} letter responses");
                return responses;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LetterReader] Fetch all failed: {ex.Message}");
                OnError?.Invoke("FETCH_FAILED", ex.Message);
                return Array.Empty<LetterResponse>();
            }
        }
        #endregion

        #region Helpers
        private LetterResponse DocumentToLetterResponse(DocumentSnapshot doc)
        {
            var dict = doc.ToDictionary();

            var response = new LetterResponse
            {
                Id = doc.Id,
                UserId = dict.ContainsKey("user_id") ? dict["user_id"]?.ToString() : "",
                UserLetter = dict.ContainsKey("user_letter") ? dict["user_letter"]?.ToString() : "",
                GeneratedResponseLetter = dict.ContainsKey("generated_response_letter")
                    ? dict["generated_response_letter"]?.ToString() : "",
            };

            if (dict.ContainsKey("created_at") && dict["created_at"] is Timestamp createdTs)
            {
                response.CreatedAt = createdTs.ToDateTime();
            }

            if (dict.ContainsKey("updated_at") && dict["updated_at"] is Timestamp updatedTs)
            {
                response.UpdatedAt = updatedTs.ToDateTime();
            }

            return response;
        }
        #endregion

        #region Debug
        private void DebugLog(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[LetterReader] {message}");
            }
        }
        #endregion
    }
}
