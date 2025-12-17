using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NativeWebSocket;
using System.Text;
using System.Threading.Tasks;

public class SpeechWebSocketClient : MonoBehaviour
{
    private WebSocket ws;
    private string sessionId;
    private string traceId; // 분산 추적용 Trace ID
    private int chunkIndex = 0;
    private AudioClip recordingClip;
    private bool isRecording = false;
    private bool isSessionStarted = false; // 세션이 시작되었는지 여부
    private string serverUrl = "ws://localhost:8000/api/v1/ws/speech/v1";
    
    // OpenTelemetry 설정
    private string otlpEndpoint = "http://localhost:4318"; // Tempo OTLP endpoint

    // 오디오 설정
    private int sampleRate = 16000;
    private int channels = 1;
    private int chunkSizeBytes = 8192; // 약 100ms @ 16kHz, 16-bit, mono

    // VAD 설정
    private float vadThreshold = 0.0001f; // 음성 감지 임계값
    private float silenceDuration = 0f; // 현재 무음 지속 시간
    private float silenceTimeout = 1.5f; // 무음 지속 시간 제한 (초) - 이 시간 동안 무음이면 세션 종료
    private bool hasDetectedSpeech = false; // 한 번이라도 음성이 감지되었는지

    [Header("UI Controller")]
    [SerializeField] private TextUIController textUIController;

    // 테스트/디버깅 설정
    [Header("테스트 설정")]
    [SerializeField] private bool enableDebugLog = true; // 디버그 로그 활성화
    [SerializeField] private float debugLogInterval = 0.5f; // 디버그 로그 출력 간격 (초)
    [SerializeField] private bool includeWavHeader = false; // WAV 헤더 포함 여부 (서버가 순수 PCM을 원하면 false)
    private float lastDebugLogTime = 0f;

    async void Start()
    {
        await Connect();
    }

    async void OnDestroy()
    {
        await Disconnect();
    }

    void Update()
    {
        if (ws == null)
        {
            return;
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        ws.DispatchMessageQueue();
#endif
    }

    async Task Connect()
    {
        ws = new WebSocket(serverUrl);

        ws.OnOpen += () => {
            Debug.Log("WebSocket 연결됨");
        };

        ws.OnMessage += (bytes) => {
            string message = Encoding.UTF8.GetString(bytes);
            Debug.Log($"[WS<-Server] 수신 메시지 (길이: {bytes.Length} bytes):");
            Debug.Log(message);
            HandleMessage(message);
        };

        ws.OnError += (error) => {
            Debug.LogError($"WebSocket 에러: {error}");
        };

        ws.OnClose += (closeCode) => {
            Debug.Log($"WebSocket 연결 종료: {closeCode}");
        };

        await ws.Connect();
    }

    async Task Disconnect()
    {
        if (ws != null)
        {
            await ws.Close();
        }
    }

    // 녹음 시작 (시작 버튼을 누르면 호출)
    public void StartRecording()
    {
        if (isRecording) return;

        // 마이크 권한 확인
        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("마이크 장치를 찾을 수 없습니다!");
            return;
        }

        Debug.Log($"녹음 시작 - 사용 중인 마이크: {(Microphone.devices.Length > 0 ? Microphone.devices[0] : "기본 마이크")}");
        Debug.Log($"샘플 레이트: {sampleRate}Hz, 채널: {channels}");

        isRecording = true;
        chunkIndex = 0;
        sessionId = Guid.NewGuid().ToString();
        traceId = Guid.NewGuid().ToString(); // Trace ID 생성
        isSessionStarted = false;
        hasDetectedSpeech = false;
        silenceDuration = 0f;
        lastDebugLogTime = 0f;
        
        Debug.Log($"[trace_id={traceId}] Recording started");

        // 마이크에서 녹음 시작
        recordingClip = Microphone.Start(null, true, 10, sampleRate);

        if (recordingClip == null)
        {
            Debug.LogError("마이크 녹음 시작 실패!");
            isRecording = false;
            return;
        }

        Debug.Log("마이크 녹음이 성공적으로 시작되었습니다.");

        // 오디오 분석 및 전송 시작
        StartCoroutine(ProcessAudioStream());
    }

    // 녹음 중지 (수동으로 중지할 때만 사용, 일반적으로는 VAD가 자동으로 처리)
    public void StopRecording()
    {
        if (!isRecording) return;

        Debug.Log("녹음 중지 중...");

        isRecording = false;
        Microphone.End(null);

        // 세션이 시작되었다면 종료
        if (isSessionStarted)
        {
            SendSessionEnd();
        }

        Debug.Log("녹음이 중지되었습니다.");
    }

    IEnumerator ProcessAudioStream()
    {
        while (isRecording)
        {
            if (recordingClip != null && Microphone.GetPosition(null) > 0)
            {
                // 최근 오디오 데이터 읽기 (예: 마지막 100ms)
                int sampleCount = sampleRate / 10; // 100ms
                float[] samples = new float[sampleCount * channels];
                int micPosition = Microphone.GetPosition(null);
                int startPos = micPosition - sampleCount;

                if (startPos < 0)
                {
                    // 순환 버퍼 처리
                    int firstPart = micPosition;
                    int secondPart = sampleCount - firstPart;

                    float[] firstSamples = new float[firstPart * channels];
                    float[] secondSamples = new float[secondPart * channels];

                    recordingClip.GetData(firstSamples, recordingClip.samples - firstPart);
                    recordingClip.GetData(secondSamples, 0);

                    Array.Copy(firstSamples, 0, samples, 0, firstPart * channels);
                    Array.Copy(secondSamples, 0, samples, firstPart * channels, secondPart * channels);
                }
                else
                {
                    recordingClip.GetData(samples, startPos);
                }

                // 오디오 레벨 및 통계 계산
                float currentEnergy = CalculateEnergy(samples);
                float maxAmplitude = GetMaxAmplitude(samples);
                float rms = CalculateRMS(samples);

                // VAD로 음성 감지
                bool isSpeech = IsSpeechDetected(samples);

                // 디버그 로그 출력 (설정된 간격마다)
                if (enableDebugLog && Time.time - lastDebugLogTime >= debugLogInterval)
                {
                    LogAudioDebugInfo(micPosition, currentEnergy, maxAmplitude, rms, isSpeech);
                    lastDebugLogTime = Time.time;
                }

                if (isSpeech)
                {
                    // 음성이 감지됨
                    hasDetectedSpeech = true;
                    silenceDuration = 0f;

                    // 세션이 시작되지 않았다면 시작
                    if (!isSessionStarted)
                    {
                        yield return StartCoroutine(SendSessionStart());
                        isSessionStarted = true;
                    }

                    // 오디오 청크 전송
                    yield return StartCoroutine(SendAudioChunk(samples));
                }
                else
                {
                    // 무음 감지
                    if (hasDetectedSpeech && isSessionStarted)
                    {
                        // 이미 음성이 감지된 적이 있고 세션이 시작되었다면
                        silenceDuration += 0.1f; // 100ms 증가

                        // 무음 지속 시간 초과 시 세션 종료
                        if (silenceDuration >= silenceTimeout)
                        {
                            Debug.Log("무음 지속 시간 초과 - 세션 자동 종료");
                            SendSessionEnd();
                            isSessionStarted = false;
                            hasDetectedSpeech = false;
                            silenceDuration = 0f;
                        }
                    }
                }
            }
            else if (recordingClip == null)
            {
                Debug.LogWarning("recordingClip이 null입니다!");
            }
            else if (Microphone.GetPosition(null) == 0)
            {
                // 마이크가 아직 데이터를 받지 못함
                if (enableDebugLog && Time.time - lastDebugLogTime >= debugLogInterval)
                {
                    Debug.Log("마이크 대기 중... (아직 오디오 데이터 수신 없음)");
                    lastDebugLogTime = Time.time;
                }
            }

            yield return new WaitForSeconds(0.1f); // 100ms마다 분석
        }
    }

    IEnumerator SendSessionStart()
    {
        // JSON 직접 구성 (JsonUtility는 anonymous type을 지원하지 않음)
        // trace_id 추가
        string json = $"{{\"type\":\"session_start\",\"trace_id\":\"{traceId}\",\"session_id\":\"{sessionId}\",\"audio_format\":\"wav\",\"sample_rate\":{sampleRate},\"channels\":{channels}}}";
        
        Debug.Log($"[trace_id={traceId}] [WS 준비] session_start 전체 JSON:");
        Debug.Log(json);
        Debug.Log($"[trace_id={traceId}] [WS 준비] session_start 상세 - trace_id: {traceId}, session_id: {sessionId}, sample_rate: {sampleRate}, channels: {channels}");

        yield return StartCoroutine(SendMessageCoroutine("session_start", json));
    }

    IEnumerator SendAudioChunk(float[] samples)
    {
        // PCM 데이터 변환
        byte[] pcmData = ConvertToPCM16(samples);
        
        // WAV 헤더 포함 여부에 따라 데이터 준비
        byte[] audioData;
        if (includeWavHeader)
        {
            // WAV 헤더 + PCM 데이터
            byte[] wavHeader = CreateWavHeader(pcmData.Length, sampleRate, channels);
            audioData = new byte[wavHeader.Length + pcmData.Length];
            Array.Copy(wavHeader, 0, audioData, 0, wavHeader.Length);
            Array.Copy(pcmData, 0, audioData, wavHeader.Length, pcmData.Length);
            Debug.Log($"[오디오] WAV 헤더 포함 - 헤더: {wavHeader.Length} bytes, PCM: {pcmData.Length} bytes, 총: {audioData.Length} bytes");
        }
        else
        {
            // 순수 PCM 데이터만
            audioData = pcmData;
            Debug.Log($"[오디오] 순수 PCM 데이터 - {pcmData.Length} bytes");
        }

        // Base64 인코딩
        string base64Audio = Convert.ToBase64String(audioData);
        int currentChunkIndex = chunkIndex++;

        // JSON 직접 구성 (JsonUtility는 anonymous type을 지원하지 않음)
        // Base64 문자열에 특수문자가 있을 수 있으므로 이스케이프 처리
        string escapedBase64 = base64Audio.Replace("\\", "\\\\").Replace("\"", "\\\"");
        // trace_id 추가
        string json = $"{{\"type\":\"audio_chunk\",\"trace_id\":\"{traceId}\",\"session_id\":\"{sessionId}\",\"chunk_index\":{currentChunkIndex},\"audio_data\":\"{escapedBase64}\",\"is_last_chunk\":false}}";
        
        Debug.Log($"[trace_id={traceId}] [WS 준비] audio_chunk #{currentChunkIndex} 상세:");
        Debug.Log($"  - trace_id: {traceId}");
        Debug.Log($"  - session_id: {sessionId}");
        Debug.Log($"  - chunk_index: {currentChunkIndex}");
        Debug.Log($"  - 샘플 수: {samples.Length}");
        Debug.Log($"  - PCM bytes: {pcmData.Length}");
        Debug.Log($"  - 오디오 데이터 bytes: {audioData.Length} (WAV 헤더 포함: {includeWavHeader})");
        Debug.Log($"  - Base64 length: {base64Audio.Length}");
        Debug.Log($"  - 샘플 레이트: {sampleRate}Hz, 채널: {channels}");
        
        // PCM 데이터 검증 (처음 몇 바이트 확인)
        if (pcmData.Length >= 4)
        {
            Debug.Log($"  - PCM 데이터 시작 (hex): {pcmData[0]:X2} {pcmData[1]:X2} {pcmData[2]:X2} {pcmData[3]:X2}");
        }
        
        Debug.Log($"[WS 준비] audio_chunk #{currentChunkIndex} 전체 JSON (처음 500자):");
        Debug.Log(json.Substring(0, Mathf.Min(500, json.Length)) + (json.Length > 500 ? "..." : ""));

        yield return StartCoroutine(SendMessageCoroutine($"audio_chunk #{currentChunkIndex}", json));
    }

    // 간단한 VAD 구현 (에너지 기반)
    bool IsSpeechDetected(float[] samples)
    {
        float energy = CalculateEnergy(samples);
        return energy > vadThreshold;
    }

    float CalculateEnergy(float[] samples)
    {
        float sum = 0f;
        foreach (float sample in samples)
        {
            sum += sample * sample;
        }
        return sum / samples.Length;
    }

    // 최대 진폭 계산
    float GetMaxAmplitude(float[] samples)
    {
        float max = 0f;
        foreach (float sample in samples)
        {
            float abs = Mathf.Abs(sample);
            if (abs > max)
            {
                max = abs;
            }
        }
        return max;
    }

    // RMS (Root Mean Square) 계산
    float CalculateRMS(float[] samples)
    {
        float sum = 0f;
        foreach (float sample in samples)
        {
            sum += sample * sample;
        }
        return Mathf.Sqrt(sum / samples.Length);
    }

    // 오디오 디버그 정보 로그 출력
    void LogAudioDebugInfo(int micPosition, float energy, float maxAmplitude, float rms, bool isSpeech)
    {
        string status = isSpeech ? "음성 감지됨 ✓" : "무음";
        string speechIndicator = isSpeech ? "🔊" : "🔇";
        
        Debug.Log($"[마이크 테스트] {speechIndicator} {status} | " +
                  $"위치: {micPosition} | " +
                  $"에너지: {energy:F6} | " +
                  $"RMS: {rms:F6} | " +
                  $"최대 진폭: {maxAmplitude:F6} | " +
                  $"임계값: {vadThreshold:F6} | " +
                  $"세션 시작됨: {isSessionStarted}");
    }

    void SendSessionEnd()
    {
        StartCoroutine(SendSessionEndRoutine());
    }

    IEnumerator SendSessionEndRoutine()
    {
        // JSON 직접 구성 (JsonUtility는 anonymous type을 지원하지 않음)
        // trace_id 추가
        string json = $"{{\"type\":\"session_end\",\"trace_id\":\"{traceId}\",\"session_id\":\"{sessionId}\"}}";
        
        Debug.Log($"[trace_id={traceId}] [WS 준비] session_end 전체 JSON:");
        Debug.Log(json);
        Debug.Log($"[trace_id={traceId}] [WS 준비] session_end 상세 - trace_id: {traceId}, session_id: {sessionId}");

        yield return StartCoroutine(SendMessageCoroutine("session_end", json));
    }

    void HandleMessage(string message)
    {
        try
        {
            var response = JsonUtility.FromJson<WebSocketResponse>(message);

            if (string.IsNullOrEmpty(response.type))
            {
                Debug.LogWarning($"[WS<-Server] 메시지 타입이 없습니다. 원본: {message}");
                return;
            }

            Debug.Log($"[WS<-Server] 메시지 타입: {response.type}");

            switch (response.type)
            {
                case "ack":
                    Debug.Log($"[WS<-Server] ✅ ACK 수신");
                    Debug.Log($"  - session_id: {response.session_id ?? "없음"}");
                    Debug.Log($"  - message: {response.message ?? "없음"}");
                    break;

                case "processing":
                    Debug.Log($"[WS<-Server] ⚙️ 처리 중");
                    Debug.Log($"  - session_id: {response.session_id ?? "없음"}");
                    Debug.Log($"  - status: {response.status ?? "없음"}");
                    if (response.progress.HasValue)
                    {
                        Debug.Log($"  - progress: {response.progress.Value * 100:F1}%");
                    }
                    break;

                case "result":
                    Debug.Log($"[WS<-Server] ✅ 결과 수신");
                    Debug.Log($"  - session_id: {response.session_id ?? "없음"}");
                    Debug.Log($"  - 인식(transcription): {response.transcription ?? "없음"}");
                    Debug.Log($"  - 응답(text): {response.text ?? "없음"}");

                    // UI 업데이트
                    if (textUIController != null && !string.IsNullOrEmpty(response.text))
                    {
                        textUIController.UpdateText(response.text);
                    }

                    // 이벤트 null 체크
                    if (OnResultReceived != null)
                    {
                        OnResultReceived(response.text, response.transcription);
                    }
                    break;

                case "error":
                    Debug.LogError($"[WS<-Server] ❌ 에러 수신");
                    Debug.LogError($"  - session_id: {response.session_id ?? "없음"}");
                    Debug.LogError($"  - error_code: {response.error_code ?? "없음"}");
                    Debug.LogError($"  - error_message: {response.error_message ?? "없음"}");
                    
                    // 이벤트 null 체크
                    if (OnErrorReceived != null)
                    {
                        OnErrorReceived(response.error_code, response.error_message);
                    }
                    break;

                default:
                    Debug.LogWarning($"[WS<-Server] 알 수 없는 메시지 타입: {response.type}");
                    Debug.LogWarning($"  원본 메시지: {message}");
                    break;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[WS<-Server] 메시지 파싱 오류: {e.Message}");
            Debug.LogError($"  원본 메시지: {message}");
            Debug.LogError($"  스택 트레이스: {e.StackTrace}");
        }
    }

    IEnumerator SendMessageCoroutine(string messageLabel, string json)
    {
        if (ws == null)
        {
            Debug.LogError($"[WS 오류] {messageLabel} 전송 실패 - WebSocket 인스턴스가 null 입니다.");
            yield break;
        }

        if (ws.State != WebSocketState.Open)
        {
            Debug.LogWarning($"[WS 경고] {messageLabel} 전송 실패 - 현재 상태: {ws.State}");
            yield break;
        }

        Debug.Log($"[WS->Server] {messageLabel} 전송 시작");
        Debug.Log($"[WS->Server] {messageLabel} 전송할 JSON 길이: {json.Length} bytes");
        Debug.Log($"[WS->Server] {messageLabel} 전송할 전체 JSON:");
        Debug.Log(json);

        var sendTask = ws.SendText(json);
        while (!sendTask.IsCompleted)
        {
            yield return null;
        }

        if (sendTask.IsFaulted)
        {
            Debug.LogError($"[WS 오류] {messageLabel} 전송 중 예외: {sendTask.Exception}");
        }
        else if (sendTask.IsCanceled)
        {
            Debug.LogWarning($"[WS 경고] {messageLabel} 전송이 취소되었습니다.");
        }
        else
        {
            Debug.Log($"[WS->Server] {messageLabel} 전송 완료 (상태: {ws.State})");
        }
    }

    // Float 배열을 16-bit PCM 바이트 배열로 변환 (Little-endian)
    byte[] ConvertToPCM16(float[] samples)
    {
        byte[] pcmData = new byte[samples.Length * 2];

        for (int i = 0; i < samples.Length; i++)
        {
            // -1.0 ~ 1.0 범위를 클리핑하고 -32768 ~ 32767로 변환
            float clampedSample = Mathf.Clamp(samples[i], -1.0f, 1.0f);
            short sample = (short)(clampedSample * 32767f);
            
            // Little-endian으로 변환 (낮은 바이트가 먼저)
            pcmData[i * 2] = (byte)(sample & 0xFF);
            pcmData[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
        }

        return pcmData;
    }

    // WAV 헤더 생성 (44 bytes)
    byte[] CreateWavHeader(int dataSize, int sampleRate, int channels, int bitsPerSample = 16)
    {
        byte[] header = new byte[44];
        int byteRate = sampleRate * channels * bitsPerSample / 8;
        int blockAlign = channels * bitsPerSample / 8;

        // RIFF 헤더
        header[0] = (byte)'R';
        header[1] = (byte)'I';
        header[2] = (byte)'F';
        header[3] = (byte)'F';
        
        // 파일 크기 - 8 (RIFF 헤더 크기)
        int fileSize = 36 + dataSize;
        header[4] = (byte)(fileSize & 0xFF);
        header[5] = (byte)((fileSize >> 8) & 0xFF);
        header[6] = (byte)((fileSize >> 16) & 0xFF);
        header[7] = (byte)((fileSize >> 24) & 0xFF);
        
        // WAVE
        header[8] = (byte)'W';
        header[9] = (byte)'A';
        header[10] = (byte)'V';
        header[11] = (byte)'E';
        
        // fmt 청크
        header[12] = (byte)'f';
        header[13] = (byte)'m';
        header[14] = (byte)'t';
        header[15] = (byte)' ';
        
        // fmt 청크 크기 (16)
        header[16] = 16;
        header[17] = 0;
        header[18] = 0;
        header[19] = 0;
        
        // 오디오 포맷 (1 = PCM)
        header[20] = 1;
        header[21] = 0;
        
        // 채널 수
        header[22] = (byte)channels;
        header[23] = 0;
        
        // 샘플 레이트
        header[24] = (byte)(sampleRate & 0xFF);
        header[25] = (byte)((sampleRate >> 8) & 0xFF);
        header[26] = (byte)((sampleRate >> 16) & 0xFF);
        header[27] = (byte)((sampleRate >> 24) & 0xFF);
        
        // 바이트 레이트
        header[28] = (byte)(byteRate & 0xFF);
        header[29] = (byte)((byteRate >> 8) & 0xFF);
        header[30] = (byte)((byteRate >> 16) & 0xFF);
        header[31] = (byte)((byteRate >> 24) & 0xFF);
        
        // 블록 정렬
        header[32] = (byte)(blockAlign & 0xFF);
        header[33] = (byte)((blockAlign >> 8) & 0xFF);
        
        // 비트당 샘플
        header[34] = (byte)(bitsPerSample & 0xFF);
        header[35] = (byte)((bitsPerSample >> 8) & 0xFF);
        
        // data 청크
        header[36] = (byte)'d';
        header[37] = (byte)'a';
        header[38] = (byte)'t';
        header[39] = (byte)'a';
        
        // 데이터 크기
        header[40] = (byte)(dataSize & 0xFF);
        header[41] = (byte)((dataSize >> 8) & 0xFF);
        header[42] = (byte)((dataSize >> 16) & 0xFF);
        header[43] = (byte)((dataSize >> 24) & 0xFF);

        return header;
    }

    // 결과 수신 콜백
    public event Action<string, string> OnResultReceived;

    // 에러 수신 콜백
    public event Action<string, string> OnErrorReceived;
}

// 응답 메시지 구조체
[Serializable]
public class WebSocketResponse
{
    public string type;
    public string session_id;
    public string message;
    public int? chunk_index;
    public string status;
    public float? progress;
    public string text;
    public string transcription;
    public string error_code;
    public string error_message;
}
