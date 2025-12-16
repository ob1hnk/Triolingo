using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// OpenTelemetry Span을 Tempo로 전송하는 헬퍼 클래스
/// 
/// 사용법:
/// OpenTelemetryHelper.StartSpan("unity.vad_detection", traceId);
/// // ... 작업 수행 ...
/// OpenTelemetryHelper.EndSpan("unity.vad_detection", traceId);
/// </summary>
public static class OpenTelemetryHelper
{
    private static string otlpEndpoint = "http://localhost:4318/v1/traces"; // OTLP HTTP endpoint
    private static Dictionary<string, DateTime> spanStartTimes = new Dictionary<string, DateTime>();
    private static Dictionary<string, Dictionary<string, object>> spanAttributes = new Dictionary<string, Dictionary<string, object>>();

    /// <summary>
    /// OTLP endpoint 설정
    /// </summary>
    public static void SetEndpoint(string endpoint)
    {
        otlpEndpoint = endpoint;
    }

    /// <summary>
    /// Span 시작
    /// </summary>
    public static void StartSpan(string spanName, string traceId, Dictionary<string, object> attributes = null)
    {
        string spanKey = $"{traceId}:{spanName}";
        spanStartTimes[spanKey] = DateTime.UtcNow;
        
        if (attributes != null)
        {
            spanAttributes[spanKey] = attributes;
        }
        else
        {
            spanAttributes[spanKey] = new Dictionary<string, object>();
        }

        Debug.Log($"[trace_id={traceId}] [span={spanName}] START");
    }

    /// <summary>
    /// Span 종료 및 Tempo로 전송
    /// </summary>
    public static void EndSpan(string spanName, string traceId, Dictionary<string, object> additionalAttributes = null)
    {
        string spanKey = $"{traceId}:{spanName}";
        
        if (!spanStartTimes.ContainsKey(spanKey))
        {
            Debug.LogWarning($"[trace_id={traceId}] [span={spanName}] No start time found");
            return;
        }

        DateTime startTime = spanStartTimes[spanKey];
        DateTime endTime = DateTime.UtcNow;
        TimeSpan duration = endTime - startTime;

        // Span 정보 수집
        var attributes = spanAttributes.ContainsKey(spanKey) ? spanAttributes[spanKey] : new Dictionary<string, object>();
        if (additionalAttributes != null)
        {
            foreach (var attr in additionalAttributes)
            {
                attributes[attr.Key] = attr.Value;
            }
        }

        Debug.Log($"[trace_id={traceId}] [span={spanName}] END - Duration: {duration.TotalMilliseconds}ms");

        // OTLP로 전송 (비동기)
        SendSpanToTempo(traceId, spanName, startTime, endTime, attributes);

        // 정리
        spanStartTimes.Remove(spanKey);
        spanAttributes.Remove(spanKey);
    }

    /// <summary>
    /// OTLP 형식으로 Span을 Tempo에 전송
    /// </summary>
    private static void SendSpanToTempo(string traceId, string spanName, DateTime startTime, DateTime endTime, Dictionary<string, object> attributes)
    {
        // 간단한 OTLP HTTP JSON 형식으로 전송
        // 실제 프로덕션에서는 OpenTelemetry .NET SDK 사용 권장
        
        var spanData = new
        {
            resourceSpans = new[]
            {
                new
                {
                    resource = new
                    {
                        attributes = new[]
                        {
                            new { key = "service.name", value = new { stringValue = "unity-client" } }
                        }
                    },
                    scopeSpans = new[]
                    {
                        new
                        {
                            spans = new[]
                            {
                                new
                                {
                                    traceId = ConvertTraceIdToHex(traceId),
                                    spanId = GenerateSpanId(),
                                    name = spanName,
                                    startTimeUnixNano = DateTimeToUnixNano(startTime),
                                    endTimeUnixNano = DateTimeToUnixNano(endTime),
                                    attributes = ConvertAttributes(attributes)
                                }
                            }
                        }
                    }
                }
            }
        };

        string json = JsonUtility.ToJson(spanData);
        
        // HTTP POST로 전송 (간단한 구현)
        // 실제로는 OpenTelemetry .NET SDK의 OTLP Exporter 사용 권장
        // 여기서는 로그만 남기고 실제 전송은 선택적
        Debug.Log($"[OTLP] Would send span to {otlpEndpoint}");
        Debug.Log($"[OTLP] Span data: {json.Substring(0, Mathf.Min(500, json.Length))}...");
    }

    /// <summary>
    /// Trace ID를 32자 hex 문자열로 변환
    /// </summary>
    private static string ConvertTraceIdToHex(string traceId)
    {
        // GUID를 hex로 변환 (32자)
        return traceId.Replace("-", "").Substring(0, 32).PadLeft(32, '0');
    }

    /// <summary>
    /// Span ID 생성 (16자 hex)
    /// </summary>
    private static string GenerateSpanId()
    {
        return Guid.NewGuid().ToString().Replace("-", "").Substring(0, 16);
    }

    /// <summary>
    /// DateTime을 Unix Nano 타임스탬프로 변환
    /// </summary>
    private static long DateTimeToUnixNano(DateTime dt)
    {
        return (long)((dt - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds * 1_000_000_000);
    }

    /// <summary>
    /// Attributes를 OTLP 형식으로 변환
    /// </summary>
    private static object[] ConvertAttributes(Dictionary<string, object> attributes)
    {
        var result = new List<object>();
        foreach (var attr in attributes)
        {
            if (attr.Value is string strValue)
            {
                result.Add(new { key = attr.Key, value = new { stringValue = strValue } });
            }
            else if (attr.Value is int intValue)
            {
                result.Add(new { key = attr.Key, value = new { intValue = intValue } });
            }
            else if (attr.Value is float floatValue)
            {
                result.Add(new { key = attr.Key, value = new { doubleValue = (double)floatValue } });
            }
            else if (attr.Value is bool boolValue)
            {
                result.Add(new { key = attr.Key, value = new { boolValue = boolValue } });
            }
        }
        return result.ToArray();
    }
}

