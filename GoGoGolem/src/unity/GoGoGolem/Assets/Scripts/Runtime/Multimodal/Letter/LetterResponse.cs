using System;

namespace Multimodal.Letter
{
    /// <summary>
    /// 편지 응답 데이터 모델
    ///
    /// 서버 Firestore 컬렉션: letter_responses
    /// </summary>
    public class LetterResponse
    {
        public string Id { get; set; }
        public string UserId { get; set; }
        public string UserLetter { get; set; }
        public string GeneratedResponseLetter { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
