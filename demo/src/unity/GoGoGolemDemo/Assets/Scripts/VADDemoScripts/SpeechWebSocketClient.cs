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
    private int chunkIndex = 0;
    private AudioClip recordingClip;
    private bool isRecording = false;
    private bool isSessionStarted = false; // 세션이 시작되었는지 여부
    private string serverUrl = "ws://44.210.134.73:8000/api/v1/ws/speech/v1";

    // 오디오 설정
    private int sampleRate = 16000;
    private int channels = 1;
    private int chunkSizeBytes = 8192; // 약 100ms @ 16kHz, 16-bit, mono

    // VAD 설정
    private float vadThreshold = 0.0001f; // 음성 감지 임계값
    private float silenceDuration = 0f; // 현재 무음 지속 시간
    private float silenceTimeout = 1.5f; // 무음 지속 시간 제한 (초) - 이 시간 동안 무음이면 세션 종료
    private bool hasDetectedSpeech = false; // 한 번이라도 음성이 감지되었는지

    // 테스트/디버깅 설정
    [Header("테스트 설정")]
    [SerializeField] private bool enableDebugLog = true; // 디버그 로그 활성화
    [SerializeField] private float debugLogInterval = 0.5f; // 디버그 로그 출력 간격 (초)
    private float lastDebugLogTime = 0f;

    async void Start()
    {
        await Connect();
    }

    async void OnDestroy()
    {
        await Disconnect();
    }

    async Task Connect()
    {
        ws = new WebSocket(serverUrl);

        ws.OnOpen += () => {
            Debug.Log("WebSocket 연결됨");
        };

        ws.OnMessage += (bytes) => {
            HandleMessage(Encoding.UTF8.GetString(bytes));
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
        isSessionStarted = false;
        hasDetectedSpeech = false;
        silenceDuration = 0f;
        lastDebugLogTime = 0f;

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
        var startMsg = new {
            type = "session_start",
            session_id = sessionId,
            audio_format = "wav",
            sample_rate = sampleRate,
            channels = channels
        };

        string json = JsonUtility.ToJson(startMsg);
        Debug.Log($"[WS 준비] session_start payload: {json}");

        yield return StartCoroutine(SendMessageCoroutine("session_start", json));
    }

    IEnumerator SendAudioChunk(float[] samples)
    {
        // WAV 형식으로 변환 (PCM 데이터)
        byte[] pcmData = ConvertToPCM16(samples);

        // Base64 인코딩
        string base64Audio = Convert.ToBase64String(pcmData);
        int currentChunkIndex = chunkIndex++;

        // 청크 전송
        var chunkMsg = new {
            type = "audio_chunk",
            session_id = sessionId,
            chunk_index = currentChunkIndex,
            audio_data = base64Audio,
            is_last_chunk = false
        };

        string json = JsonUtility.ToJson(chunkMsg);
        Debug.Log($"[WS 준비] audio_chunk #{currentChunkIndex} | PCM bytes: {pcmData.Length} | Base64 length: {base64Audio.Length}");

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
        var endMsg = new {
            type = "session_end",
            session_id = sessionId
        };

        string json = JsonUtility.ToJson(endMsg);
        Debug.Log($"[WS 준비] session_end payload: {json}");

        yield return StartCoroutine(SendMessageCoroutine("session_end", json));
    }

    void HandleMessage(string message)
    {
        var response = JsonUtility.FromJson<WebSocketResponse>(message);

        switch (response.type)
        {
            case "ack":
                Debug.Log($"ACK 수신: {response.message}");
                break;

            case "processing":
                Debug.Log($"처리 중: {response.status}");
                break;

            case "result":
                Debug.Log($"결과 수신:");
                Debug.Log($"  인식: {response.transcription}");
                Debug.Log($"  응답: {response.text}");
                OnResultReceived(response.text, response.transcription);
                break;

            case "error":
                Debug.LogError($"에러: {response.error_code} - {response.error_message}");
                OnErrorReceived(response.error_code, response.error_message);
                break;
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
            Debug.Log($"[WS->Server] {messageLabel} 전송 완료");
        }
    }

    // Float 배열을 16-bit PCM 바이트 배열로 변환
    byte[] ConvertToPCM16(float[] samples)
    {
        byte[] pcmData = new byte[samples.Length * 2];

        for (int i = 0; i < samples.Length; i++)
        {
            // -1.0 ~ 1.0 범위를 -32768 ~ 32767로 변환
            short sample = (short)(samples[i] * 32767f);
            pcmData[i * 2] = (byte)(sample & 0xFF);
            pcmData[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
        }

        return pcmData;
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
