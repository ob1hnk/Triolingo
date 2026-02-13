namespace Multimodal.Config
{
    /// <summary>
    /// AI 서버 설정 (하드코딩 + 전처리기)
    /// 
    /// - 에디터 / Development Build → Local 환경
    /// - Release Build → Production 환경
    /// </summary>
    public static class ServerConfig
    {
        #region Base URLs
        
        #if UNITY_EDITOR || DEVELOPMENT_BUILD
                public const string HttpBaseUrl = "http://localhost:8000";
                public const string WsBaseUrl = "ws://localhost:8000";
        #else
                public const string HttpBaseUrl = "http://44.210.134.73:8000";
                public const string WsBaseUrl = "ws://44.210.134.73:8000";
        #endif
                
        #endregion

        #region API Endpoints
        
        private const string RealtimePrefix = "/api/realtime";
        private const string SpeechPrefix = "/api/v1";
        private const string LetterPrefix = "";
        
        /// <summary>Realtime API WebSocket URL (Server VAD)</summary>
        public static string RealtimeWsUrl => $"{WsBaseUrl}{RealtimePrefix}";
        
        /// <summary>Speech API WebSocket URL (Unity VAD)</summary>
        public static string SpeechWsUrl => $"{WsBaseUrl}{SpeechPrefix}";
        
        /// <summary>Letter API HTTP URL</summary>
        public static string LetterHttpUrl => $"{HttpBaseUrl}{LetterPrefix}";
        
        #endregion

        #region Debug Helper
        
#if UNITY_EDITOR
        public static bool IsLocal => true;
#elif DEVELOPMENT_BUILD
        public static bool IsLocal => true;
#else
        public static bool IsLocal => false;
#endif
        
        #endregion
    }
}