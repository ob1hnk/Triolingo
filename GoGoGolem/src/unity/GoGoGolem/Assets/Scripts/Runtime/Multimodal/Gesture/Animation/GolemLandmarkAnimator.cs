#if GESTURE_METRICS
using System;                 // DateTime (CSV 파일명)
using System.IO;              // Path/File/Directory (CSV 저장)
using System.Globalization;   // CultureInfo (CSV 숫자 포맷)
#endif
using UnityEngine;
using UnityEngine.Animations.Rigging;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using System.Collections.Generic;

namespace Demo.GestureDetection
{
  /// <summary>
  /// 골렘 팔/손 IK 애니메이션 컨트롤러 (Generic 리그 전용)
  /// HumanBodyBones 의존성 제거 - 모든 bone을 Inspector에서 직접 할당
  ///
  /// [안정화] 떨림         : OneEuroFilter (속도 적응형 low-pass)
  /// [안정화] 손 꺾임      : MCP 4개 랜드마크로 palm normal 다중 평균
  /// [안정화] 화면 밖 복귀  : rest position lerp + IK weight 페이드
  /// [안정화] 핸드니스 플립  : 프레임 카운트 hysteresis
  /// [스레드] 스레드 안전   : UpdateAvatar에서 Vector3 배열로 즉시 값 복사
  ///                          → LateUpdate와 MediaPipe 콜백 간 경쟁 조건 제거
  /// </summary>
  public class GolemLandmarkAnimator : MonoBehaviour
  {
    // 떨림 보정 필터 종류 (평가/비교 측정용)
    public enum JitterFilterMode { None, MovingAverage, OneEuro }

    // CSV로 기록할 손 (golem 기준 좌/우 hand target)
    public enum RecordJoint { RightHand, LeftHand }

    // ─────────────────────────────────────────────────────────────────────────
    // 스레드 안전 스냅샷
    // MediaPipe 콜백은 백그라운드 스레드에서 실행.
    // List<NormalizedLandmark> 참조를 그대로 저장하면 LateUpdate 사이에
    // 내부 데이터가 교체되어 Count 체크 통과 후 인덱스 접근 시 오류 발생.
    // → UpdateAvatar 호출 시 Vector3 배열로 즉시 값 복사해 스냅샷으로 보관.
    // ─────────────────────────────────────────────────────────────────────────
    private struct LandmarkSnapshot
    {
      public Vector3[] pose;       // Pose landmarks [0..N], 월드 좌표로 변환 완료
      public Vector3[] hand0;      // Hand 0 landmarks [0..20]
      public Vector3[] hand1;      // Hand 1 landmarks [0..20]
      public string    hand0Label; // "Left" or "Right" (MediaPipe 기준)
      public bool      valid;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 위치 필터 공통 인터페이스 (런타임에 종류 교체 → 평가 비교)
    // maxDelta(이상치 제거)는 세 모드 모두 동일 적용 → 스무딩 거동만 비교됨
    // ─────────────────────────────────────────────────────────────────────────
    private interface IPositionFilter
    {
      Vector3 Update(Vector3 raw, float dt, Vector3 maxDelta);
      void Reset(Vector3 v);
    }

    // 스무딩 없음: 이상치 클램프만 적용 (필터 off 기준선)
    private class NoFilter : IPositionFilter
    {
      private Vector3 _prev;
      private bool    _init;

      public Vector3 Update(Vector3 raw, float dt, Vector3 maxDelta)
      {
        if (!_init) { _prev = raw; _init = true; return raw; }
        raw = ClampDelta(raw, _prev, maxDelta);
        _prev = raw;
        return raw;
      }

      public void Reset(Vector3 v) { _prev = v; _init = true; }
    }

    // 이전 구현: 이동평균 버퍼 + deadzone (미세 노이즈 무시, 큰 움직임만 반영)
    private class MovingAverageFilter : IPositionFilter
    {
      private readonly int       _bufferSize;
      private readonly float     _deadzone;
      private readonly Vector3[] _buffer;
      private int     _index;
      private int     _count;
      private Vector3 _filtered;
      private bool    _init;

      public MovingAverageFilter(int bufferSize, float deadzone)
      {
        _bufferSize = Mathf.Max(1, bufferSize);
        _deadzone   = deadzone;
        _buffer     = new Vector3[_bufferSize];
      }

      public Vector3 Update(Vector3 raw, float dt, Vector3 maxDelta)
      {
        if (_init) raw = ClampDelta(raw, _filtered, maxDelta);

        _buffer[_index] = raw;
        _index = (_index + 1) % _bufferSize;
        if (_count < _bufferSize) _count++;

        Vector3 avg = Vector3.zero;
        for (int i = 0; i < _count; i++) avg += _buffer[i];
        avg /= _count;

        if (!_init || Vector3.Distance(avg, _filtered) > _deadzone)
          _filtered = avg;

        _init = true;
        return _filtered;
      }

      public void Reset(Vector3 v)
      {
        for (int i = 0; i < _bufferSize; i++) _buffer[i] = v;
        _count = _bufferSize; _index = 0; _filtered = v; _init = true;
      }
    }

    // _prev 기준 ±maxDelta 클램프 (축별, float.MaxValue=비활성)
    private static Vector3 ClampDelta(Vector3 raw, Vector3 prev, Vector3 maxDelta)
    {
      return new Vector3(
        maxDelta.x >= float.MaxValue ? raw.x : Mathf.Clamp(raw.x, prev.x - maxDelta.x, prev.x + maxDelta.x),
        maxDelta.y >= float.MaxValue ? raw.y : Mathf.Clamp(raw.y, prev.y - maxDelta.y, prev.y + maxDelta.y),
        maxDelta.z >= float.MaxValue ? raw.z : Mathf.Clamp(raw.z, prev.z - maxDelta.z, prev.z + maxDelta.z));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 떨림 보정용 필터 (One Euro Filter)
    // 속도가 낮을 때 강하게, 높을 때 약하게 필터링 → lag 없이 떨림 제거
    // minCutoff: 정지 시 필터 강도 (낮을수록 떨림 더 제거)
    // beta:      속도 민감도 (높을수록 빠른 동작에 더 즉각 반응)
    // ─────────────────────────────────────────────────────────────────────────
    private class OneEuroFilter1D
    {
      private readonly float _dCutoff = 1.0f;
      private float _minCutoff;
      private float _beta;
      private float _prev;
      private float _dPrev;
      private bool  _initialized;

      public OneEuroFilter1D(float minCutoff, float beta)
      {
        _minCutoff = minCutoff;
        _beta      = beta;
      }

      private float Alpha(float cutoff, float dt)
      {
        float tau = 1.0f / (2.0f * Mathf.PI * cutoff);
        return 1.0f / (1.0f + tau / dt);
      }

      public float Update(float raw, float dt, float maxDelta = float.MaxValue)
      {
        if (dt <= 0f) return _initialized ? _prev : raw;
        if (!_initialized) { _prev = raw; _dPrev = 0f; _initialized = true; return raw; }

        // 이상치 제거: _prev 기준 maxDelta 초과 시 입력 클램프
        if (maxDelta < float.MaxValue)
          raw = Mathf.Clamp(raw, _prev - maxDelta, _prev + maxDelta);

        float dAlpha    = Alpha(_dCutoff, dt);
        float dRaw      = (raw - _prev) / dt;
        float dFiltered = dAlpha * dRaw + (1f - dAlpha) * _dPrev;

        float cutoff    = _minCutoff + _beta * Mathf.Abs(dFiltered);
        float alpha     = Alpha(cutoff, dt);
        float filtered  = alpha * raw + (1f - alpha) * _prev;

        _prev  = filtered;
        _dPrev = dFiltered;
        return filtered;
      }

      public void Reset(float value) { _prev = value; _dPrev = 0f; _initialized = true; }
    }

    private class OneEuroFilter : IPositionFilter
    {
      private readonly OneEuroFilter1D _x, _y, _z;

      public OneEuroFilter(float minCutoff, float beta)
      {
        _x = new OneEuroFilter1D(minCutoff, beta);
        _y = new OneEuroFilter1D(minCutoff, beta);
        _z = new OneEuroFilter1D(minCutoff, beta);
      }

      public Vector3 Update(Vector3 raw, float dt) =>
        new Vector3(_x.Update(raw.x, dt), _y.Update(raw.y, dt), _z.Update(raw.z, dt));

      public Vector3 Update(Vector3 raw, float dt, Vector3 maxDelta) =>
        new Vector3(
          _x.Update(raw.x, dt, maxDelta.x),
          _y.Update(raw.y, dt, maxDelta.y),
          _z.Update(raw.z, dt, maxDelta.z));

      public void Reset(Vector3 v) { _x.Reset(v.x); _y.Reset(v.y); _z.Reset(v.z); }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector 필드
    // ─────────────────────────────────────────────────────────────────────────
    [Header("Avatar")]
    [SerializeField] private Animator _animator;
    [SerializeField] private RigBuilder _rigBuilder;

    [Header("IK Targets")]
    [SerializeField] private Transform _leftHandTarget;
    [SerializeField] private Transform _rightHandTarget;
    [SerializeField] private Transform _leftElbowHint;
    [SerializeField] private Transform _rightElbowHint;

    [Header("Two Bone IK Constraints")]
    [SerializeField] private TwoBoneIKConstraint _leftArmIK;
    [SerializeField] private TwoBoneIKConstraint _rightArmIK;

    [Header("Settings")]
    [SerializeField] private bool _mirrorMode = true;
    [SerializeField] private float _smoothing = 25f;
    [Tooltip("손목 회전 전용 스무딩. _smoothing보다 낮게 설정해 MCP 노이즈로 인한 회전 떨림 감소 (권장: 8~15)")]
    [SerializeField] private float _handRotationSmoothing = 10f;

    [Header("Hand Movement Amplification")]
    [SerializeField] private float _handReachMultiplier = 2.0f;
    [SerializeField] private Vector3 _handAxisMultiplier = new Vector3(1.5f, 1.5f, 1.0f);

    [Header("Elbow Hint Settings")]
    [SerializeField] private float _elbowForwardOffset = 0.07f;
    [SerializeField] private float _elbowXMultiplier = 1.0f;
    [SerializeField] private float _elbowYMultiplier = 1.0f;
    [SerializeField] private float _elbowZMultiplier = 1.0f;

    [Header("Finger Smoothing")]
    [SerializeField] private float _fingerSmoothingSpeed = 25f;
    [SerializeField] private float _minRotationThreshold = 1f;

    [Header("Palm Orientation Control")]
    [SerializeField] private bool _usePalmConstraint = true;
    [SerializeField] private float _palmOrientationWeight = 0.7f;
    [SerializeField] private bool _invertPalmNormal = false;
    [SerializeField] private bool _showPalmNormalGizmo = false;
    [Tooltip("palm normal 부호가 이 프레임 수 이상 연속으로 뒤집힐 때만 손 뒤집기로 인정 (손바닥 정면 시 깜빡임 방지)")]
    [SerializeField] private int _palmNormalFlipFrames = 6;

    [Header("Finger Bone Rotation Offset")]
    [SerializeField] private Vector3 _boneAxisCorrection = new Vector3(90, 0, 0);
    [SerializeField] private Vector3 _leftFingerRotationOffset  = Vector3.zero;
    [SerializeField] private Vector3 _rightFingerRotationOffset = Vector3.zero;
    [SerializeField] private Vector3 _leftThumbRotationOffset   = Vector3.zero;
    [SerializeField] private Vector3 _rightThumbRotationOffset  = Vector3.zero;

    [Header("Hand Root Bones (골렘 손목 bone)")]
    [SerializeField] private Transform _leftHandBone;
    [SerializeField] private Transform _rightHandBone;

    [Header("Left Hand Finger Bones (엄지→소지 순서, 각 2마디)")]
    [Tooltip("[0]=첫마디, [1]=끝마디. 없으면 비워두면 됨")]
    [SerializeField] private Transform[] _leftThumb  = new Transform[2];
    [SerializeField] private Transform[] _leftIndex  = new Transform[2];
    [SerializeField] private Transform[] _leftMiddle = new Transform[2];
    [SerializeField] private Transform[] _leftRing   = new Transform[2];
    [SerializeField] private Transform[] _leftLittle = new Transform[2];

    [Header("Right Hand Finger Bones (엄지→소지 순서, 각 2마디)")]
    [SerializeField] private Transform[] _rightThumb  = new Transform[2];
    [SerializeField] private Transform[] _rightIndex  = new Transform[2];
    [SerializeField] private Transform[] _rightMiddle = new Transform[2];
    [SerializeField] private Transform[] _rightRing   = new Transform[2];
    [SerializeField] private Transform[] _rightLittle = new Transform[2];

    [Header("Hand Rotation Adjustment")]
    [SerializeField] private Vector3 _handRotationOffset = Vector3.zero;
    [SerializeField] private Vector3 _leftHandOffset  = Vector3.zero;
    [SerializeField] private Vector3 _rightHandOffset = Vector3.zero;

    [Header("Jitter Filter Mode (평가/비교용)")]
    [Tooltip("떨림 보정 필터 선택. 같은 씬·동작에서 필터만 바꿔 비교 측정. 런타임 변경 즉시 반영")]
    [SerializeField] private JitterFilterMode _filterMode = JitterFilterMode.OneEuro;
    [Tooltip("이동평균 버퍼 크기 (MovingAverage 모드 전용). 클수록 부드럽지만 반응 느려짐 (권장: 3~6)")]
    [SerializeField] private int _maBufferSize = 4;
    [Tooltip("이 거리 이하 움직임 무시 (MovingAverage 모드 전용, Unity 월드 단위, 권장: 0.003~0.01)")]
    [SerializeField] private float _maDeadzone = 0.005f;

    [Header("Jitter Filter Settings (One Euro Filter)")]
    [Tooltip("정지 시 필터 강도. 낮을수록 떨림 더 제거 (권장: 0.5~3.0)")]
    [SerializeField] private float _minCutoff = 2.0f;
    [Tooltip("속도 민감도. 높을수록 빠른 동작에 더 즉각 반응 (권장: 0.01~0.3)")]
    [SerializeField] private float _beta = 2.0f;

#if GESTURE_METRICS
    [Header("Metrics Recording (CSV, 평가용 — GESTURE_METRICS define 시에만 컴파일)")]
    [Tooltip("체크하면 raw/필터 좌표를 매 프레임 기록, 해제하면 CSV 파일로 저장")]
    [SerializeField] private bool _recordMetrics = false;
    [Tooltip("기록 대상 손. 측정 시 해당 손을 가만히(떨림) 또는 좌우로(지연) 움직일 것")]
    [SerializeField] private RecordJoint _recordJoint = RecordJoint.RightHand;
#endif

    [Header("Arm Outlier Rejection")]
    [Tooltip("프레임당 팔/팔꿈치 최대 이동량 (XYZ). Z를 작게 설정해 제스처 시 깊이 튐 방지. 0=비활성")]
    [SerializeField] private Vector3 _maxArmJumpPerFrame = new Vector3(0.5f, 0.5f, 0.08f);

    [Header("Arm Reach Settings")]
    [Tooltip("바디 스케일(어깨 너비) 기준으로 팔 뻗음을 정규화. 카메라 거리 무관하게 동일한 팔 움직임 보장")]
    [SerializeField] private bool _normalizeByBodyScale = true;
    [Tooltip("어깨 기준 손 target의 Z(깊이) 뻗음 허용 범위 (min, max). 손목을 모을 때 pose 깊이 추정이 튀어 손이 카메라로 돌진/잘리는 현상 방지. 증폭 적용 후 값 기준.")]
    [SerializeField] private Vector2 _handReachZClamp = new Vector2(-2f, 2f);

    [Header("Handedness Stability")]
    [Tooltip("MediaPipe 핸드니스 라벨이 이 프레임 수 이상 연속으로 바뀔 때만 좌우 할당 전환 (순간 flip 방지)")]
    [SerializeField] private int _handednessFlipFrames = 8;

    [Header("Out-of-Frame Return Settings")]
    [Tooltip("데이터 수신 중단 후 rest position 복귀를 시작하기까지의 유예 시간 (초)")]
    [SerializeField] private float _dataTimeout = 1.0f;
    [Tooltip("화면 밖 시 IK weight 감소 속도")]
    [SerializeField] private float _ikFadeOutSpeed = 3f;
    [Tooltip("화면 밖 시 hand target을 rest position으로 이동시키는 속도")]
    [SerializeField] private float _restReturnSpeed = 5f;

    // ─────────────────────────────────────────────────────────────────────────
    // Private 런타임 필드
    // ─────────────────────────────────────────────────────────────────────────
    private Transform[][] _leftHandFingers;
    private Transform[][] _rightHandFingers;

    // 스냅샷 & lock
    private LandmarkSnapshot _snapshot;
    private readonly object  _snapshotLock = new object();
    private bool  _hasNewSnapshot = false;
    private bool  _hasCachedData  = false;
    private float _lastDataTime;

    // 위치 필터 (런타임에 _filterMode로 교체 가능)
    private IPositionFilter _leftHandPosFilter;
    private IPositionFilter _rightHandPosFilter;
    private IPositionFilter _leftElbowPosFilter;
    private IPositionFilter _rightElbowPosFilter;
    private JitterFilterMode _activeFilterMode;

#if GESTURE_METRICS
    // 메트릭 CSV 기록 상태
    private List<string> _csvRows;
    private bool         _wasRecording;
    private float        _recordStartTime;
#endif

    // handedness 안정화
    private bool _lastIsHand0Left      = true;
    private int  _handednessDisagreeCount = 0;

    // palm normal 부호 안정화 (좌/우손 각각)
    private Vector3 _lastLeftPalmNormal  = Vector3.zero;
    private Vector3 _lastRightPalmNormal = Vector3.zero;
    private int     _leftPalmFlipCount   = 0;
    private int     _rightPalmFlipCount  = 0;

    // rest position
    private Vector3    _leftHandRestPos;
    private Vector3    _rightHandRestPos;
    private Quaternion _leftHandRestRot;
    private Quaternion _rightHandRestRot;
    private Vector3    _leftElbowRestPos;
    private Vector3    _rightElbowRestPos;

    // 디버그 기즈모
    private Vector3 _debugLeftPalmNormal;
    private Vector3 _debugRightPalmNormal;
    private Vector3 _debugLeftWrist;
    private Vector3 _debugRightWrist;

    // 손가락 2마디 기준: j=0→첫마디, j=1→끝마디
    private readonly int[][] _fingerLandmarkIndices = new int[][]
    {
      new int[] { 1, 2, 3 },     // 엄지
      new int[] { 5, 6, 7 },     // 검지
      new int[] { 9, 10, 11 },   // 중지
      new int[] { 13, 14, 15 },  // 약지
      new int[] { 17, 18, 19 }   // 소지
    };

    private Dictionary<Transform, Quaternion> _cachedFingerRotations = new Dictionary<Transform, Quaternion>();
    private Dictionary<Transform, Quaternion> _targetFingerRotations = new Dictionary<Transform, Quaternion>();

    private int   _fingerLayerIndex  = -1;
    private float _fingerLayerWeight = 0f;
    private float _fingerLayerFadeSpeed = 5f;

    // 성공 timeline 멈춤 제어용
    private bool _isFrozen = false;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity lifecycle
    // ─────────────────────────────────────────────────────────────────────────
    private void Awake()
    {
      if (_animator == null)   _animator   = GetComponent<Animator>();
      if (_rigBuilder == null) _rigBuilder = GetComponent<RigBuilder>();

      for (int i = 0; i < _animator.layerCount; i++)
      {
        string n = _animator.GetLayerName(i);
        if (n.Contains("Finger") || n.Contains("Hand"))
        {
          _fingerLayerIndex = i;
          break;
        }
      }
    }

    private void Start()
    {
      if (_leftArmIK  != null) _leftArmIK.weight  = 0f;
      if (_rightArmIK != null) _rightArmIK.weight = 0f;

      if (_leftHandBone  == null) Debug.LogWarning("[GolemLandmarkAnimator] Left Hand Bone 미할당.");
      if (_rightHandBone == null) Debug.LogWarning("[GolemLandmarkAnimator] Right Hand Bone 미할당.");

      CacheFingerBones();
      InitializeFingerRotations();

      // 필터 초기화 (_filterMode에 따라 생성)
      RebuildFilters();

      // rest position/rotation 저장. 필터는 Reset하지 않고 _initialized=false 유지.
      // 첫 실제 데이터 프레임에서 outlier rejection 없이 즉시 그 위치로 초기화됨.
      if (_leftHandTarget  != null) { _leftHandRestPos  = _leftHandTarget.position;  _leftHandRestRot  = _leftHandTarget.rotation; }
      if (_rightHandTarget != null) { _rightHandRestPos = _rightHandTarget.position; _rightHandRestRot = _rightHandTarget.rotation; }
      if (_leftElbowHint   != null) { _leftElbowRestPos  = _leftElbowHint.position; }
      if (_rightElbowHint  != null) { _rightElbowRestPos = _rightElbowHint.position; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UpdateAvatar: 백그라운드 스레드에서 호출될 수 있음
    // → 모든 landmark를 Vector3 배열로 즉시 값 복사해 스냅샷 저장
    // ─────────────────────────────────────────────────────────────────────────
    public void UpdateAvatar(PoseLandmarkerResult poseResult, HandLandmarkerResult handResult)
    {
      if (!IsValidData(poseResult, handResult)) return;

      // 유효성 재확인 후 값 복사 (IReadOnlyList로 즉시 변환 → Vector3 배열)
      var poseMarks  = poseResult.poseLandmarks[0].landmarks;
      var hand0Marks = handResult.handLandmarks[0].landmarks;
      var hand1Marks = handResult.handLandmarks[1].landmarks;
      var label0     = handResult.handedness[0].categories[0].categoryName;

      var snap = new LandmarkSnapshot
      {
        pose       = ToVector3Array(poseMarks),
        hand0      = ToVector3Array(hand0Marks),
        hand1      = ToVector3Array(hand1Marks),
        hand0Label = label0,
        valid      = true,
      };

      lock (_snapshotLock)
      {
        _snapshot      = snap;
        _hasNewSnapshot = true;
      }
    }

    // NormalizedLandmark 리스트를 월드 좌표 Vector3 배열로 값 복사
    private static Vector3[] ToVector3Array(
        IReadOnlyList<Mediapipe.Tasks.Components.Containers.NormalizedLandmark> src)
    {
      var arr = new Vector3[src.Count];
      for (int i = 0; i < src.Count; i++)
        arr[i] = LandmarkTo3D.LandmarkToWorldPosition(src[i]);
      return arr;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // LateUpdate: 실제 움직임 처리는 Animator가 끝난 뒤 수행
    // ─────────────────────────────────────────────────────────────────────────
    private void LateUpdate()
    {
      // 필터 모드가 Inspector에서 바뀌면 즉시 재생성
      if (_filterMode != _activeFilterMode) RebuildFilters();

#if GESTURE_METRICS
      // 기록 체크박스 on/off 엣지 처리 (on→버퍼 시작, off→CSV 저장)
      HandleRecordingEdge();
#endif

      // lock으로 스냅샷을 안전하게 읽어옴 (메인 스레드에서만 사용)
      LandmarkSnapshot snap;
      bool hasNew;
      lock (_snapshotLock)
      {
        snap        = _snapshot;
        hasNew      = _hasNewSnapshot;
        _hasNewSnapshot = false;
      }

      if (hasNew && snap.valid)
      {
        _hasCachedData = true;
        _lastDataTime  = Time.time;
      }

      if (!_isFrozen)
      {
        if (_hasCachedData && (Time.time - _lastDataTime <= _dataTimeout))
        {
          ProcessMovement(snap);

          if (_fingerLayerIndex >= 0)
          {
            _fingerLayerWeight = Mathf.Lerp(_fingerLayerWeight, 0f, 1f - Mathf.Exp(-_fingerLayerFadeSpeed * Time.deltaTime));
            _animator.SetLayerWeight(_fingerLayerIndex, _fingerLayerWeight);
          }
        }
        else
        {
          // 화면 밖 → rest position으로 부드럽게 복귀
          ReturnTargetsToRest();

          if (_fingerLayerIndex >= 0)
          {
            _fingerLayerWeight = Mathf.Lerp(_fingerLayerWeight, 1f, 1f - Mathf.Exp(-_fingerLayerFadeSpeed * Time.deltaTime));
            _animator.SetLayerWeight(_fingerLayerIndex, _fingerLayerWeight);
          }
        }
      }

      ApplyCachedFingerRotations();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 화면 밖 복귀
    // ─────────────────────────────────────────────────────────────────────────
    private void ReturnTargetsToRest()
    {
      float t = 1f - Mathf.Exp(-_restReturnSpeed * Time.deltaTime);

      if (_leftHandTarget  != null) { _leftHandTarget.position  = Vector3.Lerp(_leftHandTarget.position,   _leftHandRestPos,  t); _leftHandTarget.rotation  = Quaternion.Slerp(_leftHandTarget.rotation,  _leftHandRestRot,  t); }
      if (_rightHandTarget != null) { _rightHandTarget.position = Vector3.Lerp(_rightHandTarget.position,  _rightHandRestPos, t); _rightHandTarget.rotation = Quaternion.Slerp(_rightHandTarget.rotation, _rightHandRestRot, t); }
      if (_leftElbowHint   != null)   _leftElbowHint.position   = Vector3.Lerp(_leftElbowHint.position,   _leftElbowRestPos,  t);
      if (_rightElbowHint  != null)   _rightElbowHint.position  = Vector3.Lerp(_rightElbowHint.position,  _rightElbowRestPos, t);

      float ikFadeT = 1f - Mathf.Exp(-_ikFadeOutSpeed * Time.deltaTime);
      if (_leftArmIK  != null) _leftArmIK.weight  = Mathf.Lerp(_leftArmIK.weight,  0f, ikFadeT);
      if (_rightArmIK != null) _rightArmIK.weight = Mathf.Lerp(_rightArmIK.weight, 0f, ikFadeT);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // IK 처리 - LandmarkSnapshot(Vector3[])을 받으므로 스레드 안전
    // ─────────────────────────────────────────────────────────────────────────
    private void ProcessMovement(LandmarkSnapshot snap)
    {
      if (!snap.valid) { ReturnTargetsToRest(); return; }

      Vector3[] pose  = snap.pose;
      Vector3[] hand0 = snap.hand0;
      Vector3[] hand1 = snap.hand1;

      // 배열 길이 검사 (List.Count 대신 Array.Length → 변경 불가능)
      if (pose == null || pose.Length < 17)
      {
        Debug.LogWarning($"[ProcessMovement] Insufficient pose landmarks: {pose?.Length ?? 0}/17");
        ReturnTargetsToRest();
        return;
      }
      if (hand0 == null || hand0.Length < 21 || hand1 == null || hand1.Length < 21)
      {
        Debug.LogWarning($"[ProcessMovement] Incomplete hand landmarks - Hand0:{hand0?.Length ?? 0}, Hand1:{hand1?.Length ?? 0}");
        ReturnTargetsToRest();
        return;
      }
      if (string.IsNullOrEmpty(snap.hand0Label)) { Debug.LogWarning("[ProcessMovement] hand0Label empty"); return; }

      float ikFadeInT = 1f - Mathf.Exp(-5f * Time.deltaTime);
      if (_leftArmIK  != null) _leftArmIK.weight  = Mathf.Lerp(_leftArmIK.weight,  1f, ikFadeInT);
      if (_rightArmIK != null) _rightArmIK.weight = Mathf.Lerp(_rightArmIK.weight, 1f, ikFadeInT);

      // 핸드니스: 라벨이 _handednessFlipFrames 프레임 이상 연속으로 달라질 때만 전환
      bool labelIsHand0Left = snap.hand0Label == "Right"; // MediaPipe 거울 반전
      if (labelIsHand0Left == _lastIsHand0Left)
      {
        _handednessDisagreeCount = 0;
      }
      else if (++_handednessDisagreeCount >= _handednessFlipFrames)
      {
        _lastIsHand0Left = labelIsHand0Left;
        _handednessDisagreeCount = 0;
      }
      bool isHand0Left = _lastIsHand0Left;

      // 어깨 너비: 바디 스케일 정규화 기준. 너무 작으면 팔 위치가 극단적으로 증폭됨.
      float shoulderWidth = _normalizeByBodyScale
        ? Mathf.Clamp(Vector3.Distance(pose[11], pose[12]), 0.05f, 0.6f)
        : 1f;

      if (_mirrorMode)
      {
        // 미러 모드: pose index 12=오른어깨, 14=오른팔꿈치, 16=오른손목 → 골렘 왼팔
        UpdateArmIK(pose[12], pose[14], pose[16], shoulderWidth, _leftHandTarget,  _leftElbowHint,  isLeft: true);
        var leftHandMarks = isHand0Left ? hand0 : hand1;
        UpdateHandRotation(leftHandMarks, _leftHandTarget, isLeftHand: true);
        UpdateFingerTargets(leftHandMarks, _leftHandFingers, isLeftHand: true);

        UpdateArmIK(pose[11], pose[13], pose[15], shoulderWidth, _rightHandTarget, _rightElbowHint, isLeft: false);
        var rightHandMarks = isHand0Left ? hand1 : hand0;
        UpdateHandRotation(rightHandMarks, _rightHandTarget, isLeftHand: false);
        UpdateFingerTargets(rightHandMarks, _rightHandFingers, isLeftHand: false);
      }
      else
      {
        if (isHand0Left)
        {
          UpdateArmIK(pose[12], pose[14], pose[16], shoulderWidth, _rightHandTarget, _rightElbowHint, isLeft: false);
          UpdateHandRotation(hand0, _rightHandTarget, isLeftHand: false);
          UpdateFingerTargets(hand0, _rightHandFingers, isLeftHand: false);

          UpdateArmIK(pose[11], pose[13], pose[15], shoulderWidth, _leftHandTarget, _leftElbowHint, isLeft: true);
          UpdateHandRotation(hand1, _leftHandTarget, isLeftHand: true);
          UpdateFingerTargets(hand1, _leftHandFingers, isLeftHand: true);
        }
        else
        {
          UpdateArmIK(pose[11], pose[13], pose[15], shoulderWidth, _leftHandTarget, _leftElbowHint, isLeft: true);
          UpdateHandRotation(hand0, _leftHandTarget, isLeftHand: true);
          UpdateFingerTargets(hand0, _leftHandFingers, isLeftHand: true);

          UpdateArmIK(pose[12], pose[14], pose[16], shoulderWidth, _rightHandTarget, _rightElbowHint, isLeft: false);
          UpdateHandRotation(hand1, _rightHandTarget, isLeftHand: false);
          UpdateFingerTargets(hand1, _rightHandFingers, isLeftHand: false);
        }
      }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Arm IK - 어깨/팔꿈치/손목 Vector3로 IK target 위치 설정
    // OneEuroFilter 적용
    // ─────────────────────────────────────────────────────────────────────────
    private void UpdateArmIK(
      Vector3 shoulderPos, Vector3 elbowPos, Vector3 wristPos,
      float shoulderWidth,
      Transform handTarget, Transform elbowHint, bool isLeft)
    {
      if (handTarget == null) return;

      // 바디 스케일 정규화: 어깨 너비로 나눠 카메라 거리 무관한 팔 뻗음
      Vector3 shoulderToWrist = (wristPos - shoulderPos) / shoulderWidth;
      shoulderToWrist.x *= _handAxisMultiplier.x;
      shoulderToWrist.y *= _handAxisMultiplier.y;
      shoulderToWrist.z *= _handAxisMultiplier.z;
      shoulderToWrist   *= _handReachMultiplier;

      // 손목을 모을 때 pose Z 추정이 튀어 손이 카메라로 돌진 → 깊이 뻗음에 절대 한계 적용
      shoulderToWrist.z = Mathf.Clamp(shoulderToWrist.z, _handReachZClamp.x, _handReachZClamp.y);

      IPositionFilter handFilter  = isLeft ? _leftHandPosFilter  : _rightHandPosFilter;
      IPositionFilter elbowFilter = isLeft ? _leftElbowPosFilter : _rightElbowPosFilter;

      // 각 축 독립적으로 0이면 비활성 (float.MaxValue = 제한 없음)
      Vector3 maxJump = new Vector3(
        _maxArmJumpPerFrame.x > 0f ? _maxArmJumpPerFrame.x : float.MaxValue,
        _maxArmJumpPerFrame.y > 0f ? _maxArmJumpPerFrame.y : float.MaxValue,
        _maxArmJumpPerFrame.z > 0f ? _maxArmJumpPerFrame.z : float.MaxValue);
      float smoothT = 1f - Mathf.Exp(-_smoothing * Time.deltaTime);
      Vector3 rawWrist      = shoulderPos + shoulderToWrist;
      Vector3 filteredWrist = handFilter.Update(rawWrist, Time.deltaTime, maxJump);
      handTarget.position = Vector3.Lerp(handTarget.position, filteredWrist, smoothT);

#if GESTURE_METRICS
      // 평가용: 선택한 손의 raw(필터 입력) vs filtered(필터 출력) 기록
      if (_recordMetrics && isLeft == (_recordJoint == RecordJoint.LeftHand))
        RecordRow(rawWrist, filteredWrist);
#endif

      if (elbowHint != null)
      {
        Vector3 shoulderToElbow = (elbowPos - shoulderPos) / shoulderWidth;
        shoulderToElbow.x *= _elbowXMultiplier;
        shoulderToElbow.y *= _elbowYMultiplier;
        shoulderToElbow.z  = shoulderToElbow.z * _elbowZMultiplier + _elbowForwardOffset;

        Vector3 filteredElbow = elbowFilter.Update(shoulderPos + shoulderToElbow, Time.deltaTime, maxJump);
        elbowHint.position = Vector3.Lerp(elbowHint.position, filteredElbow, smoothT);
      }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 손목 회전 계산
    // MCP 4개(5,9,13,17) 기반 palm normal 다중 평균
    // ─────────────────────────────────────────────────────────────────────────
    private void UpdateHandRotation(Vector3[] landmarks, Transform handTarget, bool isLeftHand)
    {
      if (landmarks == null || landmarks.Length < 21 || handTarget == null) return;

      Vector3 wristPos  = landmarks[0];
      Vector3 indexMCP  = landmarks[5];
      Vector3 middleMCP = landmarks[9];
      Vector3 ringMCP   = landmarks[13];
      Vector3 pinkyMCP  = landmarks[17];

      // hand direction: wrist → middle MCP
      Vector3 handDir = (middleMCP - wristPos).normalized;
      if (handDir == Vector3.zero) return;

      // 인접 MCP 쌍 삼각형들의 normal 평균
      Vector3[] mcpList = { indexMCP, middleMCP, ringMCP, pinkyMCP };
      Vector3 normalSum = Vector3.zero;
      int normalCount   = 0;
      for (int i = 0; i < mcpList.Length - 1; i++)
      {
        Vector3 v1 = (mcpList[i]     - wristPos).normalized;
        Vector3 v2 = (mcpList[i + 1] - wristPos).normalized;
        Vector3 n  = Vector3.Cross(v1, v2);
        if (n.sqrMagnitude > 0.0001f) { normalSum += n.normalized; normalCount++; }
      }
      if (normalCount == 0) return;

      Vector3 palmNormal = (normalSum / normalCount).normalized;
      if (!isLeftHand)      palmNormal = -palmNormal;
      if (_invertPalmNormal) palmNormal = -palmNormal;

      // 손바닥 정면 시 z-노이즈로 인한 부호 플립 억제
      palmNormal = StabilizePalmNormal(palmNormal, isLeftHand);

      // orthonormal basis
      Vector3 right = Vector3.Cross(handDir, palmNormal).normalized;
      Vector3 up    = Vector3.Cross(right, handDir).normalized;
      if (right == Vector3.zero || up == Vector3.zero) return;

      Quaternion raw    = Quaternion.LookRotation(handDir, up);
      Quaternion final  = raw * Quaternion.Euler(_handRotationOffset) * Quaternion.Euler(isLeftHand ? _leftHandOffset : _rightHandOffset);

      handTarget.rotation = Quaternion.Slerp(handTarget.rotation, final, 1f - Mathf.Exp(-_handRotationSmoothing * Time.deltaTime));

      if (_showPalmNormalGizmo)
      {
        if (isLeftHand) { _debugLeftPalmNormal  = palmNormal; _debugLeftWrist  = wristPos; }
        else            { _debugRightPalmNormal = palmNormal; _debugRightWrist = wristPos; }
      }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // palm normal 부호 안정화
    // 손바닥이 카메라에 평평할 때 MCP가 거의 한 평면에 놓여, MediaPipe의 불안정한
    // z(깊이) 노이즈가 외적 normal의 부호를 매 프레임 뒤집는다 → 손이 손날을 거쳐 회전.
    // 직전 normal과 90° 이상 벌어지면(부호 플립) 즉시 받지 않고, _palmNormalFlipFrames
    // 프레임 연속 뒤집혔을 때만 전환 (handedness 플립 억제와 동일한 hysteresis).
    // ─────────────────────────────────────────────────────────────────────────
    private Vector3 StabilizePalmNormal(Vector3 palmNormal, bool isLeftHand)
    {
      Vector3 last      = isLeftHand ? _lastLeftPalmNormal : _lastRightPalmNormal;
      int     flipCount = isLeftHand ? _leftPalmFlipCount  : _rightPalmFlipCount;

      // 첫 프레임: 비교 대상 없음 → 그대로 채택
      if (last == Vector3.zero)
      {
        if (isLeftHand) _lastLeftPalmNormal = palmNormal;
        else            _lastRightPalmNormal = palmNormal;
        return palmNormal;
      }

      if (Vector3.Dot(palmNormal, last) >= 0f)
      {
        // 부호 일치 → 카운터 리셋, 최신값 추적 (점진적 회전은 정상 반영)
        flipCount = 0;
        last      = palmNormal;
      }
      else if (++flipCount >= _palmNormalFlipFrames)
      {
        // 충분히 오래 뒤집힘 → 실제 손 뒤집기로 인정
        flipCount = 0;
        last      = palmNormal;
      }
      else
      {
        // 일시적 플립 → 무시하고 직전 부호 유지
        palmNormal = -palmNormal;
      }

      if (isLeftHand) { _lastLeftPalmNormal = last;  _leftPalmFlipCount  = flipCount; }
      else            { _lastRightPalmNormal = last; _rightPalmFlipCount = flipCount; }

      return palmNormal;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 손가락 rotation 계산
    // ─────────────────────────────────────────────────────────────────────────
    private void UpdateFingerTargets(Vector3[] landmarks, Transform[][] handBones, bool isLeftHand)
    {
      if (handBones == null || landmarks == null || landmarks.Length < 21) return;

      Transform handBone = isLeftHand ? _leftHandBone : _rightHandBone;
      if (handBone == null) return;

      Vector3 wristPos  = landmarks[0];
      Vector3 indexMCP  = landmarks[5];
      Vector3 pinkyMCP  = landmarks[17];
      Vector3 middleMCP = landmarks[9];

      Vector3 wristToMiddle       = (middleMCP - wristPos).normalized;
      Vector3 indexToPinky        = (pinkyMCP - indexMCP).normalized;
      Vector3 palmOutward         = Vector3.Cross(wristToMiddle, indexToPinky).normalized;
      Vector3 backOfHandDirection = isLeftHand ? -palmOutward : palmOutward;
      if (_invertPalmNormal) backOfHandDirection = -backOfHandDirection;

      Vector3 thumbUpWorld = isLeftHand ? indexToPinky : -indexToPinky;

      if (_showPalmNormalGizmo)
      {
        if (isLeftHand) { _debugLeftPalmNormal  = backOfHandDirection; _debugLeftWrist  = wristPos; }
        else            { _debugRightPalmNormal = backOfHandDirection; _debugRightWrist = wristPos; }
      }

      for (int i = 0; i < 5; i++)
      {
        if (handBones[i] == null) continue;
        for (int j = 0; j < 2; j++)
        {
          Transform bone = handBones[i][j];
          if (bone == null) continue;

          int currentIdx = _fingerLandmarkIndices[i][j];
          int nextIdx    = _fingerLandmarkIndices[i][j + 1];
          if (currentIdx >= landmarks.Length || nextIdx >= landmarks.Length) continue;

          Vector3 curWorld  = landmarks[currentIdx];
          Vector3 nextWorld = landmarks[nextIdx];
          Vector3 curLocal  = handBone.InverseTransformPoint(curWorld);
          Vector3 nextLocal = handBone.InverseTransformPoint(nextWorld);
          Vector3 dirLocal  = (nextLocal - curLocal).normalized;
          if (dirLocal == Vector3.zero) continue;

          Vector3 upWorld = (i == 0) ? thumbUpWorld : backOfHandDirection;
          Vector3 upLocal = handBone.InverseTransformDirection(upWorld);

          if (Mathf.Abs(Vector3.Dot(dirLocal, upLocal.normalized)) > 0.98f)
          {
            Vector3 itpLocal = handBone.InverseTransformDirection(indexToPinky);
            upLocal = Vector3.Cross(dirLocal, itpLocal).normalized;
            if (upLocal == Vector3.zero) upLocal = Vector3.up;
          }

          Vector3 rightLocal = Vector3.Cross(dirLocal, upLocal).normalized;
          if (rightLocal != Vector3.zero) upLocal = Vector3.Cross(rightLocal, dirLocal).normalized;

          Quaternion targetLocal = Quaternion.LookRotation(dirLocal, upLocal) * Quaternion.Euler(_boneAxisCorrection);

          Vector3 offset = (i == 0)
            ? (isLeftHand ? _leftThumbRotationOffset  : _rightThumbRotationOffset)
            : (isLeftHand ? _leftFingerRotationOffset : _rightFingerRotationOffset);
          if (offset != Vector3.zero) targetLocal = targetLocal * Quaternion.Euler(offset);

          if (_usePalmConstraint && _cachedFingerRotations.ContainsKey(bone))
          {
            Quaternion cachedLocal = Quaternion.Inverse(handBone.rotation) * _cachedFingerRotations[bone];
            targetLocal = Quaternion.Slerp(cachedLocal, targetLocal, _palmOrientationWeight);
          }

          _targetFingerRotations[bone] = handBone.rotation * targetLocal;
        }
      }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 공통 유틸
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 성공 타임라인 진입 시 현재 손 위치를 고정
    /// </summary>
    public void FreezePosition()
    {
      _isFrozen = true;
      Debug.Log("[GolemLandmarkAnimator] Position FROZEN");
    }

    public void ResetToIdle()
    {
      _isFrozen = false;
      if (_leftArmIK  != null) _leftArmIK.weight  = 0f;
      if (_rightArmIK != null) _rightArmIK.weight = 0f;
      if (_fingerLayerIndex >= 0) _animator.SetLayerWeight(_fingerLayerIndex, 1f);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 필터 종류 교체 (평가/비교용)
    // ─────────────────────────────────────────────────────────────────────────
    private IPositionFilter CreateFilter()
    {
      switch (_filterMode)
      {
        case JitterFilterMode.None:          return new NoFilter();
        case JitterFilterMode.MovingAverage: return new MovingAverageFilter(_maBufferSize, _maDeadzone);
        default:                             return new OneEuroFilter(_minCutoff, _beta);
      }
    }

    private void RebuildFilters()
    {
      _leftHandPosFilter   = CreateFilter();
      _rightHandPosFilter  = CreateFilter();
      _leftElbowPosFilter  = CreateFilter();
      _rightElbowPosFilter = CreateFilter();
      _activeFilterMode    = _filterMode;
      Debug.Log($"[GolemLandmarkAnimator] Filter mode = {_filterMode}");
    }

#if GESTURE_METRICS
    // ─────────────────────────────────────────────────────────────────────────
    // 메트릭 CSV 기록: _recordMetrics 체크 on→기록 시작, off→파일 저장
    // 컬럼: time_s, dt_s, fps, filter_mode, raw_xyz, filt_xyz
    //   떨림 = 손 정지 시 filt_xyz의 표준편차
    //   지연 = 손 흔들 때 raw_xyz vs filt_xyz의 교차상관 offset
    //   FPS  = fps 컬럼 평균
    // ─────────────────────────────────────────────────────────────────────────
    private void HandleRecordingEdge()
    {
      if (_recordMetrics && !_wasRecording)
      {
        _csvRows = new List<string>
        {
          "time_s,dt_s,fps,filter_mode,raw_x,raw_y,raw_z,filt_x,filt_y,filt_z"
        };
        _recordStartTime = Time.time;
        Debug.Log($"[GestureMetrics] Recording START (mode={_filterMode}, joint={_recordJoint})");
      }
      else if (!_recordMetrics && _wasRecording)
      {
        WriteCsv();
      }
      _wasRecording = _recordMetrics;
    }

    private void RecordRow(Vector3 raw, Vector3 filtered)
    {
      if (_csvRows == null) return;
      float dt  = Time.deltaTime;
      float fps = dt > 0f ? 1f / dt : 0f;
      var   c   = CultureInfo.InvariantCulture;
      _csvRows.Add(string.Format(c,
        "{0:F4},{1:F5},{2:F1},{3},{4:F5},{5:F5},{6:F5},{7:F5},{8:F5},{9:F5}",
        Time.time - _recordStartTime, dt, fps, _filterMode,
        raw.x, raw.y, raw.z, filtered.x, filtered.y, filtered.z));
    }

    private void WriteCsv()
    {
      if (_csvRows == null || _csvRows.Count <= 1)
      {
        Debug.LogWarning("[GestureMetrics] 기록된 데이터 없음 (손이 화면에 잡혔는지 확인)");
        _csvRows = null;
        return;
      }

      string dir = Path.Combine(Application.dataPath, "..", "GestureMetrics");
      Directory.CreateDirectory(dir);
      string file = Path.Combine(dir,
        $"gesture_{_filterMode}_{_recordJoint}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
      File.WriteAllLines(file, _csvRows);
      Debug.Log($"[GestureMetrics] {_csvRows.Count - 1} rows 저장 → {Path.GetFullPath(file)}");
      _csvRows = null;
    }

    private void OnDisable()
    {
      // 기록 중 플레이 종료/비활성 시 데이터 유실 방지
      if (_wasRecording) WriteCsv();
      _wasRecording = false;
    }
#endif

    private bool IsValidData(PoseLandmarkerResult poseResult, HandLandmarkerResult handResult)
    {
      if (poseResult.poseLandmarks == null || poseResult.poseLandmarks.Count == 0) return false;
      if (handResult.handLandmarks == null || handResult.handLandmarks.Count < 2)  return false;
      if (handResult.handedness    == null || handResult.handedness.Count    < 2)  return false;
      for (int i = 0; i < 2; i++)
      {
        var lm = handResult.handLandmarks[i].landmarks;
        if (lm == null || lm.Count < 21) return false;
        var hd = handResult.handedness[i].categories;
        if (hd == null || hd.Count == 0) return false;
      }
      return true;
    }

    private void CacheFingerBones()
    {
      _leftHandFingers  = new Transform[][] { _leftThumb,  _leftIndex,  _leftMiddle,  _leftRing,  _leftLittle };
      _rightHandFingers = new Transform[][] { _rightThumb, _rightIndex, _rightMiddle, _rightRing, _rightLittle };
      int lc = CountAssignedBones(_leftHandFingers);
      int rc = CountAssignedBones(_rightHandFingers);
      Debug.Log($"[GolemLandmarkAnimator] Finger bones - 왼손:{lc}개, 오른손:{rc}개");
      if (lc == 0 && rc == 0)
        Debug.LogWarning("[GolemLandmarkAnimator] 손가락 bone이 하나도 할당되지 않았습니다.");
    }

    private int CountAssignedBones(Transform[][] fingers)
    {
      int count = 0;
      if (fingers == null) return 0;
      foreach (var f in fingers) { if (f == null) continue; foreach (var b in f) if (b != null) count++; }
      return count;
    }

    private void InitializeFingerRotations()
    {
      _cachedFingerRotations.Clear();
      _targetFingerRotations.Clear();
      foreach (var fingers in new Transform[][][] { _leftHandFingers, _rightHandFingers })
      {
        if (fingers == null) continue;
        foreach (var f in fingers) { if (f == null) continue; foreach (var b in f) { if (b != null) { _cachedFingerRotations[b] = b.rotation; _targetFingerRotations[b] = b.rotation; } } }
      }
    }

    private void ApplyCachedFingerRotations()
    {
      foreach (var kvp in _targetFingerRotations)
      {
        Transform  bone   = kvp.Key;
        Quaternion target = kvp.Value;
        if (bone == null || bone.parent == null) continue;

        if (!_cachedFingerRotations.ContainsKey(bone))
          _cachedFingerRotations[bone] = bone.rotation;

        Quaternion current = _cachedFingerRotations[bone];
        if (Quaternion.Angle(current, target) > _minRotationThreshold)
        {
          Quaternion next = Quaternion.Slerp(current, target, 1f - Mathf.Exp(-_fingerSmoothingSpeed * Time.deltaTime));
          _cachedFingerRotations[bone] = next;
          bone.localRotation = Quaternion.Inverse(bone.parent.rotation) * next;
        }
      }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
      if (!_showPalmNormalGizmo) return;
      Gizmos.color = Color.blue; Gizmos.DrawRay(_debugLeftWrist,  _debugLeftPalmNormal  * 0.15f);
      Gizmos.color = Color.red;  Gizmos.DrawRay(_debugRightWrist, _debugRightPalmNormal * 0.15f);
    }
#endif
  }
}