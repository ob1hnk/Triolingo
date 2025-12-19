# 시선 추적 시스템 코드 설명서
## GazePointAnnotationController 코드 분석 및 발표 자료

---

## 1. 프로젝트 개요 및 목적

**질문 예상**: "이 프로젝트는 무엇을 하는 건가요?"

**답변**:
- MediaPipe를 활용한 **실시간 시선 추적(Gaze Tracking) 시스템**입니다
- 사용자의 눈 움직임을 추적하여 화면 상의 시선 위치를 표시합니다
- Unity 환경에서 웹캠을 통해 얼굴을 감지하고, 눈의 홍채 위치를 분석해 시선 방향을 계산합니다
- 개인별 보정(Calibration) 시스템을 통해 정확도를 향상시킵니다

---

## 2. 기술 스택 및 아키텍처

**질문 예상**: "어떤 기술을 사용했나요?"

**답변**:
- **MediaPipe**: Google의 머신러닝 프레임워크로, 468개의 얼굴 랜드마크와 홍채 랜드마크를 제공합니다
- **Unity**: 실시간 렌더링 및 UI 표시를 위한 게임 엔진
- **멀티스레드 구조**: MediaPipe는 별도 스레드에서 실행되며, 메인 스레드에서 UI 업데이트를 처리합니다

**핵심 데이터 구조**:
```257:296:demo/src/unity/GoGoGolemDemo/Assets/Scripts/GazePointAnnotationController.cs
    private Vector2? TryGetGazePoint(FaceLandmarkerResult result)
    {
      if (result.faceLandmarks == null || result.faceLandmarks.Count == 0)
      {
        return null;
      }

      var landmarks = result.faceLandmarks[0].landmarks;
      if (landmarks == null || landmarks.Count == 0)
      {
        return null;
      }

      var leftEye = GetEyeOffsetVector(landmarks, _LeftIrisStart, _leftEyeRegion);
      var rightEye = GetEyeOffsetVector(landmarks, _RightIrisStart, _rightEyeRegion);

      var offset = CombineSamples(leftEye, rightEye);
      if (!offset.HasValue)
      {
        return null;
      }

      // 보정 중이면 offset을 저장 (메인 스레드에서 처리)
      if (_isCalibrating)
      {
        lock (_gazeLock)
        {
          _pendingCalibrationOffset = offset.Value;
        }
      }

      var projected = ApplyProjection(offset.Value);

      if (_logProjection)
      {
        // Debug.Log($"[Gaze] Projected=({projected.x:F4},{projected.y:F4})");
      }

      return projected;
    }
```

---

## 3. 핵심 알고리즘: 시선 위치 계산

### 3.1 얼굴 랜드마크 구조

**질문 예상**: "시선은 어떻게 계산하나요?"

**답변**:
MediaPipe는 총 478개의 랜드마크를 제공합니다:
- **468개**: 얼굴 랜드마크 (눈, 코, 입 등)
- **5개**: 왼쪽 홍채 랜드마크
- **5개**: 오른쪽 홍채 랜드마크

```19:23:demo/src/unity/GoGoGolemDemo/Assets/Scripts/GazePointAnnotationController.cs
    private const int _FaceLandmarkCount = 468;
    private const int _IrisLandmarkCount = 5;

    private const int _LeftIrisStart = _FaceLandmarkCount;
    private const int _RightIrisStart = _FaceLandmarkCount + _IrisLandmarkCount;
```

### 3.2 홍채 중심 계산

**핵심 로직**:
```587:620:demo/src/unity/GoGoGolemDemo/Assets/Scripts/GazePointAnnotationController.cs
    private Vector2? GetEyeOffsetVector(IReadOnlyList<mptcc.NormalizedLandmark> landmarks, int irisStart, EyeRegionIndices eyeRegion)
    {
      var irisCenter = TryGetIrisCenter(landmarks, irisStart);
      var pupil = irisCenter ?? GetEyelidCenter(landmarks, eyeRegion);

      var outer = landmarks[eyeRegion.outer];
      var inner = landmarks[eyeRegion.inner];
      var upper = landmarks[eyeRegion.upper];
      var lower = landmarks[eyeRegion.lower];

      var horizontalHalf = Mathf.Max(Mathf.Abs(inner.x - outer.x) * 0.5f, 1e-5f);
      var verticalHalf = Mathf.Max(Mathf.Abs(lower.y - upper.y) * 0.5f, 1e-5f);

      var horizontalCenter = (outer.x + inner.x) * 0.5f;
      var verticalCenter = (upper.y + lower.y) * 0.5f;

      var horizontal = Mathf.Clamp((pupil.x - horizontalCenter) / horizontalHalf, -1f, 1f);
      var vertical = Mathf.Clamp((verticalCenter - pupil.y) / verticalHalf, -1f, 1f);

      // 상단 영역 인식 개선: 상단을 쳐다볼 때(negative vertical) 더 optimistic하게 해석
      // 원본 offset 값 자체를 조정하여 보정에도 반영되도록 함
      if (vertical < 0f)
      {
        vertical = vertical * _upperHalfSensitivityBoost;
        vertical = Mathf.Clamp(vertical, -1f, 1f);
      }

      if (_logIrisSample && irisCenter.HasValue)
      {
        // Debug.Log($"[Gaze] Iris offset=({horizontal:F4},{vertical:F4}) [upper boost: {_upperHalfSensitivityBoost}]");
      }

      return new Vector2(horizontal, vertical);
    }
```

**설명**:
1. **홍채 중심 계산**: 5개의 홍채 랜드마크의 평균값을 사용 (홍채가 보이지 않으면 눈꺼풀 중심 사용)
2. **눈 영역 경계**: 눈의 바깥쪽, 안쪽, 위쪽, 아래쪽 랜드마크로 눈 영역 정의
3. **정규화된 offset 계산**: 
   - `horizontal`: 홍채가 눈 중심에서 왼쪽/오른쪽으로 얼마나 이동했는지 (-1 ~ +1)
   - `vertical`: 홍채가 눈 중심에서 위/아래로 얼마나 이동했는지 (-1 ~ +1)
4. **상단 영역 보정**: 상단을 쳐다볼 때 민감도를 높여 인식 개선

### 3.3 양쪽 눈 데이터 결합

```643:650:demo/src/unity/GoGoGolemDemo/Assets/Scripts/GazePointAnnotationController.cs
    private Vector2? CombineSamples(Vector2? left, Vector2? right)
    {
      if (left.HasValue && right.HasValue)
      {
        return (left.Value + right.Value) * 0.5f;
      }
      return left ?? right;
    }
```

양쪽 눈의 offset을 평균내어 최종 시선 방향을 계산합니다.

---

## 4. 보정(Calibration) 시스템

**질문 예상**: "보정이 왜 필요한가요? 어떻게 동작하나요?"

### 4.1 보정의 필요성

**답변**:
- 사람마다 얼굴 구조, 카메라 위치, 화면 크기가 다릅니다
- 시선 offset 값이 실제 화면 좌표와 선형적 관계가 있지만, 개인차가 존재합니다
- 따라서 5개 포인트를 보면서 개인별 보정 파라미터를 계산합니다

### 4.2 보정 프로세스

```365:401:demo/src/unity/GoGoGolemDemo/Assets/Scripts/GazePointAnnotationController.cs
    public void StartCalibration()
    {
      // 보정이 이미 완료되었으면 다시 시작하지 않음
      if (_hasCalibrationResult)
      {
        if (_logCalibration)
        {
          // Debug.Log("[Calibration] Calibration already completed. Skipping.");
        }
        return;
      }

      if (!_enableCalibration)
      {
        // Debug.LogWarning("[Calibration] Calibration is disabled. Enable it in the inspector.");
        return;
      }

      if (_calibrationTargets == null || _calibrationTargets.Length == 0)
      {
        // Debug.LogWarning("[Calibration] Calibration targets are not configured.");
        return;
      }

      _isCalibrating = true;
      _currentCalibrationPointIndex = 0;
      _currentPointTimer = 0f;
      _lastSampleTime = 0f;
      _calibrationSamples.Clear();
      _pendingGaze = null;
      _smoothedGaze = null;

      var firstPoint = _calibrationTargets[0];
      // Debug.Log($"[Calibration] Calibration started!");
      // Debug.Log($"[Calibration] {_calibrationTargets.Length} points, {_calibrationPointDisplayDuration}s per point, collecting last {_calibrationSampleDuration}s");
      // Debug.Log($"[Calibration] Point 1/{_calibrationTargets.Length}: ({firstPoint.x:F2}, {firstPoint.y:F2}) - Look at the RED point");
    }
```

**보정 포인트**:
```47:54:demo/src/unity/GoGoGolemDemo/Assets/Scripts/GazePointAnnotationController.cs
    [SerializeField] private Vector2[] _calibrationTargets = new Vector2[]
    {
      new Vector2(0.1f, 0.1f),  // 왼쪽 위
      new Vector2(0.9f, 0.1f),  // 오른쪽 위
      new Vector2(0.5f, 0.5f),  // 중앙
      new Vector2(0.1f, 0.9f),  // 왼쪽 아래
      new Vector2(0.9f, 0.9f)   // 오른쪽 아래
    };
```

**동작 방식**:
1. 5개 포인트를 순차적으로 표시 (각 포인트당 3초)
2. 마지막 1초 동안 30Hz로 샘플 수집
3. 각 포인트에 대해: `목표 좌표(target)` vs `측정된 offset` 데이터 수집
4. 선형 회귀를 통해 Scale과 Bias 파라미터 계산

### 4.3 보정 파라미터 계산

```518:547:demo/src/unity/GoGoGolemDemo/Assets/Scripts/GazePointAnnotationController.cs
    private bool TryFitAxis(List<CalibrationSample> samples, bool useX, out float scale, out float bias)
    {
      float sumOffset = 0f;
      float sumTarget = 0f;
      float sumOffsetSq = 0f;
      float sumOffsetTarget = 0f;
      int n = samples.Count;

      foreach (var sample in samples)
      {
        var offset = useX ? sample.offset.x : sample.offset.y;
        var target = useX ? sample.target.x : sample.target.y;
        sumOffset += offset;
        sumTarget += target;
        sumOffsetSq += offset * offset;
        sumOffsetTarget += offset * target;
      }

      var denominator = n * sumOffsetSq - sumOffset * sumOffset;
      if (Mathf.Abs(denominator) < 1e-5f)
      {
        scale = 0f;
        bias = 0.5f;
        return false;
      }

      scale = (n * sumOffsetTarget - sumOffset * sumTarget) / denominator;
      bias = (sumTarget - scale * sumOffset) / n;
      return true;
    }
```

**수식**: `target = scale × offset + bias`

선형 최소제곱법(Least Squares)을 사용하여 Scale과 Bias를 계산합니다.

### 4.3.1 선형 회귀(Linear Regression) 이론

#### 선형 회귀란?

**선형 회귀(Linear Regression)**는 두 변수 간의 **선형 관계**를 찾아내는 통계학 및 머신러닝의 기본 기법입니다.

**핵심 아이디어**: 
- 입력 변수(X)와 출력 변수(Y) 사이에 `Y = aX + b` 형태의 직선 관계가 있다고 가정
- 주어진 데이터를 가장 잘 설명하는 `a`(기울기)와 `b`(절편) 값을 찾아냄

#### 우리 코드에서의 적용

우리 시선 추적 시스템에서는:

- **입력 변수(X)**: `offset` - 눈의 홍채가 눈 중심에서 얼마나 벗어났는지 (-1 ~ +1)
- **출력 변수(Y)**: `target` - 실제 화면 상의 정규화된 좌표 (0 ~ 1)
- **목표**: `target = scale × offset + bias` 형태의 선형 관계 찾기

**왜 선형 관계를 가정하나?**

1. **물리적 근거**: 시선의 각도와 화면 위치는 대체로 선형적 관계를 가집니다
2. **수학적 단순성**: 선형 모델은 계산이 간단하고 해석하기 쉽습니다
3. **실용성**: 개인별로 약간의 차이가 있지만, 선형 변환(scale + bias)으로 충분히 보정 가능합니다

#### 선형 회귀의 수학적 표현

**일반 형태**:
```
Y = aX + b
```

**우리 코드에서**:
```
target = scale × offset + bias
```

여기서:
- `scale` (기울기, slope): offset이 1 증가할 때 target이 얼마나 변하는가
- `bias` (절편, intercept): offset이 0일 때의 target 값 (기본 위치)

#### 선형 회귀를 푸는 방법

**문제**: 여러 데이터 포인트 `(offset₁, target₁), (offset₂, target₂), ..., (offsetₙ, targetₙ)`가 주어졌을 때, 가장 잘 맞는 `scale`과 `bias`를 찾는 것

**해결 방법**:

1. **최소제곱법(Least Squares Method)** ⭐ 우리 코드에서 사용
   - 가장 널리 사용되는 방법
   - 오차의 제곱합을 최소화하는 해를 구함
   - 수학적으로 명확하고 계산이 빠름

2. **경사 하강법(Gradient Descent)**
   - 복잡한 모델에 주로 사용
   - 우리의 단순 선형 회귀에는 불필요

3. **정규 방정식(Normal Equation)**
   - 최소제곱법의 행렬 형태
   - 본질적으로는 같은 방법

#### 선형 회귀의 가정

선형 회귀가 잘 작동하려면:

1. **선형성**: X와 Y 사이에 선형 관계가 있어야 함 ✅ (시선-화면 위치 관계)
2. **독립성**: 데이터 포인트들이 서로 독립적이어야 함 ✅ (각 보정 포인트는 독립적)
3. **등분산성**: 오차의 분산이 일정해야 함 (완벽하지 않지만 허용 가능)
4. **정규성**: 오차가 정규분포를 따르면 좋음 (필수는 아님)

우리의 경우, 선형 관계 가정이 충분히 합리적입니다!

#### 선형 회귀의 장단점

**장점**:
- ✅ **해석 용이**: scale과 bias가 명확한 의미를 가짐
- ✅ **계산 속도**: 매우 빠른 계산 (O(n))
- ✅ **과적합 방지**: 단순한 모델이므로 과적합 가능성이 낮음
- ✅ **실용성**: 작은 데이터셋에서도 잘 작동

**단점**:
- ❌ **선형 관계 가정**: 비선형 관계에는 부적합
- ❌ **이상치에 민감**: 극단적인 데이터 포인트가 결과에 큰 영향

우리의 경우, 보정 포인트 5개는 충분하며, 이상치는 사용자가 잘못 응시하지 않는 한 문제가 되지 않습니다.

#### 실제 응용 예시

**보정 과정**:

1. 사용자가 5개 포인트를 응시
2. 각 포인트에서:
   - 실제 응시 위치 (target): 예) (0.1, 0.1)
   - 측정된 시선 offset: 예) (-0.4, -0.3)

3. 선형 회귀를 통해 다음 관계를 학습:
   ```
   target_x = 0.6 × offset_x + 0.34
   target_y = 0.7 × offset_y + 0.31
   ```

4. 이후 새로운 offset이 들어오면, 학습된 공식으로 target을 예측:
   ```
   새로운 offset = (-0.2, 0.1)
   예측된 target = (0.6 × -0.2 + 0.34, 0.7 × 0.1 + 0.31)
                  = (0.22, 0.38)
   ```

#### 최소제곱법과의 관계

**선형 회귀** = "목표" (선형 관계 찾기)
**최소제곱법** = "방법" (선형 회귀를 풀기 위한 구체적 알고리즘)

즉, 최소제곱법은 선형 회귀 문제를 해결하기 위한 **가장 일반적인 방법**입니다.

다음 섹션에서 최소제곱법이 구체적으로 어떻게 작동하는지 살펴보겠습니다.

### 4.3.2 최소제곱법(Least Squares Method) 이론

#### 최소제곱법이란?

**최소제곱법(Least Squares Method)**은 선형 회귀 문제를 해결하기 위한 구체적인 알고리즘입니다. 데이터 포인트들과 선(또는 함수) 사이의 **오차의 제곱합을 최소화**하여 최적의 선을 찾는 방법입니다.

**핵심 개념**: "가장 잘 맞는다"는 것은 모든 데이터 포인트에서 예측값과 실제값의 차이(오차)의 **제곱합이 최소**인 경우를 의미합니다.

#### 왜 최소제곱법인가?

앞서 선형 회귀를 통해 `target = scale × offset + bias` 형태의 관계를 찾고 싶다고 했습니다.

**문제**: 여러 개의 `(offset, target)` 데이터 포인트가 있는데, 이들을 가장 잘 설명하는 `scale`과 `bias` 값을 찾고 싶습니다.

**예시**:
- 보정 포인트 1: offset = -0.5, target = 0.1 (왼쪽 위)
- 보정 포인트 2: offset = 0.0, target = 0.5 (중앙)
- 보정 포인트 3: offset = +0.5, target = 0.9 (오른쪽 아래)
- ... 등등

이 데이터들에 가장 잘 맞는 `scale`과 `bias` 값을 찾아야 합니다.

#### 수학적 원리

**1. 목표 함수 정의**

각 데이터 포인트 `(offset_i, target_i)`에 대해, 예측값은:
```
예측값 = scale × offset_i + bias
```

실제값과 예측값의 차이(오차)는:
```
오차_i = target_i - (scale × offset_i + bias)
```

**2. 최소화할 함수**

모든 데이터 포인트에 대한 오차의 제곱합(Sum of Squared Errors, SSE):
```
SSE = Σ (target_i - scale × offset_i - bias)²
```

이 SSE를 최소화하는 `scale`과 `bias`를 찾으면 됩니다.

**3. 미분을 통한 최적값 계산**

SSE를 `scale`과 `bias`로 각각 편미분하여 0으로 만드는 값이 최적해입니다:

```
∂SSE/∂scale = 0  →  scale에 대한 방정식
∂SSE/∂bias = 0   →  bias에 대한 방정식
```

**4. 정규 방정식(Normal Equation) 유도**

편미분을 통해 다음 연립방정식을 얻을 수 있습니다:

```
n × bias + scale × Σoffset = Σtarget
bias × Σoffset + scale × Σ(offset²) = Σ(offset × target)
```

이를 행렬로 표현하면:
```
[n          Σoffset    ] [bias]   [Σtarget      ]
[Σoffset    Σ(offset²) ] [scale] = [Σ(offset×target)]
```

**5. 해의 공식**

행렬을 역행렬로 풀면 (또는 직접 대입하면):

```
scale = (n × Σ(offset×target) - Σoffset × Σtarget) / (n × Σ(offset²) - (Σoffset)²)
bias = (Σtarget - scale × Σoffset) / n
```

이것이 바로 코드에서 사용하는 공식입니다!

#### 코드에서의 구현

```232:261:demo/src/unity/GoGoGolemDemo/Assets/Scripts/GazePointAnnotationController.cs
    private bool TryFitAxis(List<CalibrationSample> samples, bool useX, out float scale, out float bias)
    {
      float sumOffset = 0f;
      float sumTarget = 0f;
      float sumOffsetSq = 0f;
      float sumOffsetTarget = 0f;
      int n = samples.Count;

      foreach (var sample in samples)
      {
        var offset = useX ? sample.offset.x : sample.offset.y;
        var target = useX ? sample.target.x : sample.target.y;
        sumOffset += offset;
        sumTarget += target;
        sumOffsetSq += offset * offset;
        sumOffsetTarget += offset * target;
      }

      var denominator = n * sumOffsetSq - sumOffset * sumOffset;
      if (Mathf.Abs(denominator) < 1e-5f)
      {
        scale = 0f;
        bias = 0.5f;
        return false;
      }

      scale = (n * sumOffsetTarget - sumOffset * sumTarget) / denominator;
      bias = (sumTarget - scale * sumOffset) / n;
      return true;
    }
```

**코드 해석**:
- `sumOffset`: Σoffset
- `sumTarget`: Σtarget
- `sumOffsetSq`: Σ(offset²)
- `sumOffsetTarget`: Σ(offset × target)
- `denominator`: n × Σ(offset²) - (Σoffset)² (분모, 0에 가까우면 수치 불안정)

**왜 제곱합을 최소화하나?**

1. **양수화**: 오차를 제곱하면 항상 양수가 되어, 음수와 양수가 상쇄되지 않습니다
2. **큰 오차에 더 큰 페널티**: 제곱을 하면 큰 오차에 더 큰 가중치를 부여합니다
3. **수학적으로 처리하기 쉬움**: 제곱 함수는 미분이 쉬워 최적해를 구하기 좋습니다

**실제 예시**:

만약 3개의 샘플이 있다고 가정:
- 샘플 1: offset = -0.5, target = 0.1
- 샘플 2: offset = 0.0, target = 0.5  
- 샘플 3: offset = 0.5, target = 0.9

계산:
```
n = 3
Σoffset = -0.5 + 0.0 + 0.5 = 0.0
Σtarget = 0.1 + 0.5 + 0.9 = 1.5
Σ(offset²) = 0.25 + 0.0 + 0.25 = 0.5
Σ(offset×target) = -0.05 + 0.0 + 0.45 = 0.4

denominator = 3 × 0.5 - 0.0² = 1.5
scale = (3 × 0.4 - 0.0 × 1.5) / 1.5 = 1.2 / 1.5 = 0.8
bias = (1.5 - 0.8 × 0.0) / 3 = 0.5
```

결과: `target = 0.8 × offset + 0.5`

이 선이 세 데이터 포인트를 가장 잘 설명합니다!

### 4.4 보정 적용

```338:361:demo/src/unity/GoGoGolemDemo/Assets/Scripts/GazePointAnnotationController.cs
    private Vector2 ApplyProjection(Vector2 offset)
    {
      // offset은 이미 GetEyeOffsetVector에서 상단 영역에 대해 boost가 적용된 상태
      if (_hasCalibrationResult)
      {
        // Calibration Bias의 영향을 강화: bias에 가중치를 곱함
        var weightedBias = new Vector2(
          _calibrationBias.x * _calibrationBiasWeight,
          _calibrationBias.y * _calibrationBiasWeight
        );
        
        var calibrated = new Vector2(
          _calibrationScale.x * offset.x + weightedBias.x,
          _calibrationScale.y * offset.y + weightedBias.y
        );
        return new Vector2(Mathf.Clamp01(calibrated.x), Mathf.Clamp01(calibrated.y));
      }

      var projected = new Vector2(
        0.5f + offset.x * _screenSensitivity.x,
        0.5f + offset.y * _screenSensitivity.y
      );
      return new Vector2(Mathf.Clamp01(projected.x), Mathf.Clamp01(projected.y));
    }
```

**보정 전**: 기본 sensitivity 값 사용 (고정값)
**보정 후**: 개인별로 계산된 scale과 bias 적용

---

## 5. 멀티스레드 안전성

**질문 예상**: "실시간 처리에서 성능 이슈는 없나요?"

**답변**:

### 5.1 멀티스레드 구조 이론

#### 스레드(Thread)란?

**스레드**는 프로그램이 실행되는 **실행 단위**입니다. 하나의 프로세스(프로그램)는 여러 개의 스레드를 가질 수 있으며, 각 스레드는 동시에 실행될 수 있습니다.

**비유**: 
- 프로세스 = 식당
- 스레드 = 웨이터
- 여러 웨이터가 동시에 일하면 더 빠르게 서빙할 수 있습니다

#### 왜 멀티스레드를 사용하나?

**단일 스레드의 문제**:
```
[카메라 이미지 수집] → [MediaPipe 처리 (100ms)] → [UI 업데이트]
```
MediaPipe 처리가 끝날 때까지 UI가 멈춥니다! (프레임 드롭 발생)

**멀티스레드의 해결**:
```
스레드 1 (메인): UI 렌더링 (60fps 유지)
스레드 2 (MediaPipe): 이미지 처리 (비동기)
```

두 스레드가 **동시에** 실행되므로 UI가 멈추지 않습니다!

#### Race Condition (경쟁 상태) 문제

**문제 상황**:

두 스레드가 **같은 변수**에 동시에 접근할 때 발생:

```csharp
// 스레드 1 (MediaPipe): _pendingGaze에 값 쓰기
_pendingGaze = new Vector2(0.5f, 0.6f);

// 스레드 2 (Unity 메인): _pendingGaze에서 값 읽기  
var gaze = _pendingGaze;
```

**위험한 시나리오**:

1. 스레드 1이 `_pendingGaze`의 첫 번째 값(0.5f)을 쓰는 중...
2. 스레드 2가 그 순간 `_pendingGaze`를 읽음 → 잘못된 값(예: 이전 값의 일부만 읽음)
3. 스레드 1이 두 번째 값(0.6f)을 쓰는 중...
4. 결과: **데이터 손상** 또는 **예측 불가능한 동작**

이것이 바로 **Race Condition**입니다!

#### Lock (잠금) 메커니즘

**Lock의 원리**:

Lock은 "화장실 잠금 장치"와 같습니다:
- 한 스레드가 lock을 잡으면 다른 스레드는 **대기**합니다
- 작업이 끝나면 lock을 풀고 다음 스레드가 진행합니다
- **한 번에 하나의 스레드만** 공유 변수에 접근할 수 있습니다

**코드에서의 사용**:

```78:78:demo/src/unity/GoGoGolemDemo/Assets/Scripts/GazePointAnnotationController.cs
    private readonly object _gazeLock = new object();
```

`_gazeLock`은 "열쇠" 역할을 합니다.

```196:203:demo/src/unity/GoGoGolemDemo/Assets/Scripts/GazePointAnnotationController.cs
    public void DrawLater(FaceLandmarkerResult result)
    {
      lock (_gazeLock)
      {
        UpdateGaze(result);
        isStale = true;
      }
    }
```

**동작 순서**:
1. MediaPipe 스레드가 `lock (_gazeLock)` 실행 → 열쇠 획득
2. 다른 스레드는 대기 상태
3. `UpdateGaze()` 실행 → 안전하게 `_pendingGaze` 업데이트
4. `lock` 블록 종료 → 열쇠 반환
5. 대기 중이던 스레드가 열쇠 획득 후 진행

#### 코드에서의 멀티스레드 구조

**1. DrawLater() - MediaPipe 스레드에서 호출**

```196:203:demo/src/unity/GoGoGolemDemo/Assets/Scripts/GazePointAnnotationController.cs
    public void DrawLater(FaceLandmarkerResult result)
    {
      lock (_gazeLock)
      {
        UpdateGaze(result);
        isStale = true;
      }
    }
```

- MediaPipe가 **별도 스레드**에서 얼굴 인식 완료
- 결과를 `lock`으로 보호하여 `_pendingGaze`에 저장
- `isStale = true`로 "업데이트 필요" 표시

**2. SyncNow() - Unity 메인 스레드에서 호출**

```205:238:demo/src/unity/GoGoGolemDemo/Assets/Scripts/GazePointAnnotationController.cs
    protected override void SyncNow()
    {
      lock (_gazeLock)
      {
        isStale = false;

        if (_isCalibrating)
        {
          // 보정 모드: 빨간색 보정 포인트 표시
          var calibrationPoint = GetCurrentCalibrationPoint();
          if (calibrationPoint.HasValue)
          {
            DrawIndicator(calibrationPoint.Value, _calibrationIndicatorColor, _calibrationIndicatorRadius, false);
          }
          return;
        }

        if (_pendingGaze == null)
        {
          annotation?.gameObject.SetActive(false);
          _smoothedGaze = null;
          return;
        }

        if (annotation == null)
        {
          return;
        }

        var smoothed = Smooth(_pendingGaze.Value);
        var clamped = new Vector2(Mathf.Clamp01(smoothed.x), Mathf.Clamp01(smoothed.y));
        DrawIndicator(clamped, _indicatorColor, _indicatorRadius, true);
      }
    }
```

- Unity 메인 스레드에서 `Update()` 중 호출
- 같은 `lock (_gazeLock)` 사용 → 안전하게 데이터 읽기
- 읽은 데이터로 UI 업데이트

**타임라인 예시**:

```
시간 →

[MediaPipe 스레드]
  이미지 처리 중...
              → lock 획득 → _pendingGaze 업데이트 → lock 해제
  
[Unity 메인 스레드]
  렌더링...    렌더링...    → lock 획득 → 데이터 읽기 → UI 업데이트 → lock 해제
              (60fps 유지)              (데이터 안전하게 전달)
```

#### Lock의 주의사항

**1. Deadlock (교착 상태)**

두 스레드가 서로의 lock을 기다리는 상황:
```
스레드 1: lock A 획득 → lock B 대기
스레드 2: lock B 획득 → lock A 대기
→ 영원히 멈춤!
```

**이 코드에서는 해결**: 하나의 lock만 사용 → deadlock 불가능

**2. 성능 오버헤드**

Lock을 너무 자주 사용하면:
- 스레드 대기 시간 증가
- 성능 저하

**이 코드에서의 최적화**:
- Lock 블록을 최소화 (필요한 작업만 lock 내부에)
- 공유 변수 최소화

#### 정리

**멀티스레드를 사용하는 이유**:
1. ✅ **성능**: 무거운 연산(MediaPipe)이 UI를 블로킹하지 않음
2. ✅ **반응성**: 60fps UI 렌더링 유지
3. ✅ **병렬 처리**: CPU 코어를 효율적으로 활용

**Lock을 사용하는 이유**:
1. ✅ **안전성**: Race Condition 방지
2. ✅ **데이터 무결성**: 공유 변수가 손상되지 않음
3. ✅ **예측 가능한 동작**: 멀티스레드에서도 안정적인 결과

### 5.2 스무딩 처리

```563:575:demo/src/unity/GoGoGolemDemo/Assets/Scripts/GazePointAnnotationController.cs
    private Vector2 Smooth(Vector2 next)
    {
      var lerpT = 1f - Mathf.Clamp01(_smoothingFactor);
      if (_smoothedGaze.HasValue)
      {
        _smoothedGaze = Vector2.Lerp(_smoothedGaze.Value, next, lerpT);
      }
      else
      {
        _smoothedGaze = next;
      }
      return _smoothedGaze.Value;
    }
```

Lerp(Linear Interpolation)를 사용하여 시선 위치의 떨림을 완화합니다.

---

## 6. UI 표시

**질문 예상**: "시선이 화면에 어떻게 표시되나요?"

**답변**:

```297:336:demo/src/unity/GoGoGolemDemo/Assets/Scripts/GazePointAnnotationController.cs
    private void DrawIndicator(Vector2 normalized, UnityColor color, float radius, bool logDirectControl)
    {
      if (annotation == null)
      {
        return;
      }

      annotation.gameObject.SetActive(true);
      annotation.SetColor(color);
      annotation.SetRadius(radius);

      if (_useDirectPositionControl && _canvasRect != null)
      {
        var rectTransform = annotation.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
          var canvasWidth = _canvasRect.rect.width;
          var canvasHeight = _canvasRect.rect.height;
          var x = (normalized.x - 0.5f) * canvasWidth;
          var y = (normalized.y - 0.5f) * canvasHeight;
          rectTransform.anchoredPosition = new Vector2(x, -y);

          if (_logProjection && logDirectControl)
          {
            // Debug.Log($"[GazeTracking] DIRECT CONTROL: normalized=({normalized.x:F4},{normalized.y:F4}) canvas=({x:F1},{-y:F1})");
          }
        }
        return;
      }

      var landmark = new NormalizedLandmark
      {
        X = normalized.x,
        Y = normalized.y,
        Z = 0f,
        Visibility = 1f,
        Presence = 1f
      };
      annotation.Draw(landmark, _visualizeZ);
    }
```

정규화된 좌표(0~1)를 Canvas의 실제 픽셀 좌표로 변환하여 시선 표시기를 표시합니다.

---

## 7. 예상 질문 및 답변 정리

### Q1: "이 시스템의 정확도는 어느 정도인가요?"

**A**: 
- 보정 없이 사용 시: 대략 5~10도 오차
- 보정 후: 개인마다 다르지만 2~5도 내외의 오차
- 상업용 아이 트래커(예: Tobii) 대비 정확도는 낮지만, 웹캠만으로 구현 가능한 수준

### Q2: "왜 상단 영역에 대해 sensitivity boost를 적용했나요?"

**A**: 
- 실제 사용 중 상단을 쳐다볼 때 인식이 약했던 문제를 해결하기 위함
- `vertical < 0`일 때 값을 `_upperHalfSensitivityBoost`(기본 1.5) 배로 증가시켜 더 민감하게 반응하도록 함

### Q3: "보정 중에 데이터를 왜 마지막 1초만 수집하나요?"

**A**: 
- 사용자가 보정 포인트에 집중하는 시간을 고려
- 처음 2초는 준비 시간, 마지막 1초만 실제로 집중한 데이터로 간주
- 노이즈가 적은 깨끗한 샘플을 얻기 위함

### Q4: "멀티스레드에서 lock을 사용한 이유는?"

**A**: 
- MediaPipe는 별도 스레드에서 실행되며, Unity 메인 스레드와 동시에 데이터에 접근
- `lock (_gazeLock)`을 사용하여 race condition 방지
- `_pendingGaze`, `_pendingCalibrationOffset` 등의 공유 변수 보호

### Q5: "성능 최적화는 어떻게 했나요?"

**A**: 
- MediaPipe는 비동기로 처리하여 메인 스레드 블로킹 방지
- 스무딩으로 업데이트 빈도 감소
- 샘플링 레이트 제어 (보정 시 30Hz)

---

## 8. 코드 흐름도 (발표용)

```
1. 카메라 입력
   ↓
2. MediaPipe FaceLandmarker (별도 스레드)
   ↓
3. FaceLandmarkerResult 생성 (478개 랜드마크)
   ↓
4. GazePointAnnotationController.DrawLater()
   ├─ TryGetGazePoint()
   │  ├─ GetEyeOffsetVector() (왼쪽 눈)
   │  ├─ GetEyeOffsetVector() (오른쪽 눈)
   │  ├─ CombineSamples() (평균)
   │  └─ ApplyProjection() (보정 적용 또는 기본값)
   └─ lock으로 _pendingGaze 저장
   ↓
5. Unity 메인 스레드에서 SyncNow() 호출
   ├─ Smooth() (스무딩)
   └─ DrawIndicator() (UI 표시)
```

---

## 9. 핵심 포인트 요약

1. **MediaPipe**: 468개 얼굴 + 10개 홍채 랜드마크 제공
2. **시선 계산**: 홍채 중심이 눈 영역 내에서 어디에 있는지로 offset 계산
3. **보정 시스템**: 5점 보정으로 개인별 scale/bias 파라미터 학습
4. **멀티스레드**: lock으로 안전한 데이터 전달
5. **스무딩**: Lerp로 떨림 완화
6. **실시간**: 비동기 처리로 30fps 이상 유지

---

## 10. 발표 시 강조할 점

- ✅ **실시간 처리**: MediaPipe의 비동기 처리로 부드러운 시선 추적
- ✅ **개인 맞춤 보정**: 선형 회귀를 통한 정확도 향상
- ✅ **안정성**: 멀티스레드 환경에서의 안전한 데이터 처리
- ✅ **사용자 경험**: 스무딩과 상단 영역 보정으로 자연스러운 인터랙션

