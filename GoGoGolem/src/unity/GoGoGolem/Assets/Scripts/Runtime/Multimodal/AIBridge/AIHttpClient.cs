using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Multimodal.AIBridge
{
    /// <summary>
    /// HTTP 클라이언트 (REST API 통신)
    /// 
    /// - UnityWebRequest 기반
    /// - JSON 직렬화/역직렬화
    /// - 에러 핸들링
    /// - 타임아웃 설정
    /// </summary>
    public class AIHttpClient : IDisposable
    {
        #region Fields
        private readonly string _baseUrl;
        private int _timeout = 30; // 기본 30초

        // 요청 헤더
        private const string ContentTypeJson = "application/json";
        #endregion

        #region Constructor
        public AIHttpClient(string baseUrl)
        {
            _baseUrl = baseUrl.TrimEnd('/');
        }
        #endregion

        #region Configuration
        public void SetTimeout(int seconds)
        {
            _timeout = Mathf.Max(1, seconds);
        }
        #endregion

        #region HTTP Methods
        /// GET 요청
        public async Task<JObject> GetAsync(string endpoint)
        {
            var url = $"{_baseUrl}{endpoint}";

            using (var request = UnityWebRequest.Get(url))
            {
                request.timeout = _timeout;

                var operation = request.SendWebRequest();

                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                return HandleResponse(request);
            }
        }

        /// POST 요청 (JSON)
        public async Task<JObject> PostJsonAsync(string endpoint, object data)
        {
            var url = $"{_baseUrl}{endpoint}";
            var json = SerializeData(data);
            var bodyRaw = Encoding.UTF8.GetBytes(json);

            using (var request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", ContentTypeJson);
                request.timeout = _timeout;

                var operation = request.SendWebRequest();

                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                return HandleResponse(request);
            }
        }

        #endregion

        #region Response Handling
        private JObject HandleResponse(UnityWebRequest request)
        {
            // 네트워크 에러 체크
            if (request.result == UnityWebRequest.Result.ConnectionError ||
                request.result == UnityWebRequest.Result.ProtocolError)
            {
                var errorMessage = $"HTTP Error {request.responseCode}: {request.error}";
                
                // 서버가 JSON 에러를 반환한 경우
                if (!string.IsNullOrEmpty(request.downloadHandler?.text))
                {
                    try
                    {
                        var errorJson = JObject.Parse(request.downloadHandler.text);
                        errorMessage = errorJson["detail"]?.ToString() ?? 
                                     errorJson["error_message"]?.ToString() ?? 
                                     errorMessage;
                    }
                    catch
                    {
                        // JSON 파싱 실패 시 원본 에러 메시지 사용
                    }
                }

                throw new HttpRequestException(
                    (int)request.responseCode,
                    errorMessage
                );
            }

            // 응답 파싱
            var responseText = request.downloadHandler.text;

            if (string.IsNullOrEmpty(responseText))
            {
                return new JObject
                {
                    ["success"] = true,
                    ["status_code"] = request.responseCode
                };
            }

            try
            {
                return JObject.Parse(responseText);
            }
            catch (Exception ex)
            {
                throw new HttpRequestException(
                    (int)request.responseCode,
                    $"Failed to parse response: {ex.Message}"
                );
            }
        }

        private string SerializeData(object data)
        {
            if (data is string str)
            {
                return str;
            }

            if (data is JObject jobj)
            {
                return jobj.ToString(Formatting.None);
            }

            return JsonConvert.SerializeObject(data);
        }
        #endregion

        #region IDisposable
        public void Dispose()
        {
            // UnityWebRequest는 using 블록에서 자동 정리됨
        }
        #endregion

        #region Properties
        public string BaseUrl => _baseUrl;
        public int Timeout => _timeout;
        #endregion
    }

    #region Exception
    /// HTTP 요청 예외
    public class HttpRequestException : Exception
    {
        public int StatusCode { get; }

        public HttpRequestException(int statusCode, string message) 
            : base(message)
        {
            StatusCode = statusCode;
        }
    }
    #endregion
}