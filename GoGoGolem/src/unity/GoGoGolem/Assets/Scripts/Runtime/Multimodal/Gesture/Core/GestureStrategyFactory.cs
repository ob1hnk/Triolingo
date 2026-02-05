using System;
using UnityEngine;

namespace Demo.GestureDetection
{
  /// <summary>
  /// 제스처 Strategy 생성 팩토리
  /// 제스처 타입에 따라 적절한 Strategy 인스턴스 생성
  /// </summary>
  public static class GestureStrategyFactory
  {
    /// <summary>
    /// 제스처 타입에 맞는 Strategy 생성 및 초기화
    /// </summary>
    /// <param name="type">생성할 제스처 타입</param>
    /// <param name="thresholds">임계값 데이터</param>
    /// <returns>초기화된 Strategy 인스턴스</returns>
    public static IGestureStrategy Create(GestureType type, GestureThresholdData thresholds)
    {
      if (thresholds == null)
      {
        Debug.LogWarning("[GestureStrategyFactory] Thresholds is null, using default values");
        thresholds = GestureThresholdData.Default();
      }

      IGestureStrategy strategy = type switch
      {
        GestureType.Wind => new WindGestureStrategy(),
        GestureType.Lift => new LiftGestureStrategy(),
        GestureType.None => throw new ArgumentException("Cannot create strategy for GestureType.None"),
        _ => throw new ArgumentException($"Unknown gesture type: {type}")
      };
      
      strategy.Initialize(thresholds);
      Debug.Log($"[GestureStrategyFactory] Created strategy for {type}");
      
      return strategy;
    }

    /// <summary>
    /// 제스처별 최적화된 Threshold로 생성 (선택적)
    /// </summary>
    public static IGestureStrategy CreateWithOptimizedThresholds(GestureType type)
    {
      GestureThresholdData thresholds = type switch
      {
        GestureType.Wind => GestureThresholdData.ForWind(),
        GestureType.Lift => GestureThresholdData.ForLift(),
        _ => GestureThresholdData.Default()
      };

      return Create(type, thresholds);
    }
  }
}

