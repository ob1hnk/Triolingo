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
  /// [안정화] 떨림         : PositionFilter (이동평균 + deadzone)
  /// [안정화] 손 꺾임      : MCP 4개 랜드마크로 palm normal 다중 평균
  /// [안정화] 화면 밖 복귀  : rest position lerp + IK weight 페이드
  /// [인덱스] 스레드 안전   : UpdateAvatar에서 Vector3 배열로 즉시 값 복사
  ///                          → LateUpdate와 MediaPipe 콜백 간 경쟁 조건 제거
  /// </summary>
  public class GolemLandmarkAnimator : MonoBehaviour
  {
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
    // 떨림 보정용 필터
    // 이동평균 버퍼 + deadzone: 미세 노이즈 무시, 큰 움직임만 반영
    // ─────────────────────────────────────────────────────────────────────────
    private class PositionFilter
    {
      private readonly int _bufferSize;
      private readonly float _deadzone;
      private readonly Vector3[] _buffer;
      private int _index;
      private int _count;
      private Vector3 _filtered;

      public PositionFilter(int bufferSize = 4, float deadzone = 0.005f)
      {
        _bufferSize = bufferSize;
        _deadzone   = deadzone;
        _buffer     = new Vector3[bufferSize];
      }

      public Vector3 Update(Vector3 raw)
      {
        _buffer[_index] = raw;
        _index = (_index + 1) % _bufferSize;
        if (_count < _bufferSize) _count++;

        Vector3 avg = Vector3.zero;
        for (int i = 0; i < _count; i++) avg += _buffer[i];
        avg /= _count;

        if (Vector3.Distance(avg, _filtered) > _deadzone)
          _filtered = avg;

        return _filtered;
      }

      public void Reset(Vector3 initial)
      {
        for (int i = 0; i < _bufferSize; i++) _buffer[i] = initial;
        _count    = _bufferSize;
        _index    = 0;
        _filtered = initial;
      }
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

    [Header("Jitter Filter Settings")]
    [Tooltip("이동평균 버퍼 크기. 클수록 부드럽지만 반응 느려짐 (권장: 3~6)")]
    [SerializeField] private int _filterBufferSize = 4;
    [Tooltip("이 거리 이하의 움직임은 무시 (Unity 월드 단위, 권장: 0.003~0.01)")]
    [SerializeField] private float _positionDeadzone = 0.005f;

    [Header("Out-of-Frame Return Settings")]
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
    private float _dataTimeout    = 1.0f;
    private float _lastDataTime;

    // 위치 필터
    private PositionFilter _leftHandPosFilter;
    private PositionFilter _rightHandPosFilter;
    private PositionFilter _leftElbowPosFilter;
    private PositionFilter _rightElbowPosFilter;

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

      // 필터 초기화
      _leftHandPosFilter   = new PositionFilter(_filterBufferSize, _positionDeadzone);
      _rightHandPosFilter  = new PositionFilter(_filterBufferSize, _positionDeadzone);
      _leftElbowPosFilter  = new PositionFilter(_filterBufferSize, _positionDeadzone);
      _rightElbowPosFilter = new PositionFilter(_filterBufferSize, _positionDeadzone);

      // rest position/rotation 저장
      if (_leftHandTarget  != null) { _leftHandRestPos  = _leftHandTarget.position;  _leftHandRestRot  = _leftHandTarget.rotation;  _leftHandPosFilter.Reset(_leftHandRestPos); }
      if (_rightHandTarget != null) { _rightHandRestPos = _rightHandTarget.position; _rightHandRestRot = _rightHandTarget.rotation; _rightHandPosFilter.Reset(_rightHandRestPos); }
      if (_leftElbowHint   != null) { _leftElbowRestPos  = _leftElbowHint.position;  _leftElbowPosFilter.Reset(_leftElbowRestPos); }
      if (_rightElbowHint  != null) { _rightElbowRestPos = _rightElbowHint.position; _rightElbowPosFilter.Reset(_rightElbowRestPos); }
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
            _fingerLayerWeight = Mathf.Lerp(_fingerLayerWeight, 0f, Time.deltaTime * _fingerLayerFadeSpeed);
            _animator.SetLayerWeight(_fingerLayerIndex, _fingerLayerWeight);
          }
        }
        else
        {
          // 화면 밖 → rest position으로 부드럽게 복귀
          ReturnTargetsToRest();

          if (_fingerLayerIndex >= 0)
          {
            _fingerLayerWeight = Mathf.Lerp(_fingerLayerWeight, 1f, Time.deltaTime * _fingerLayerFadeSpeed);
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
      float t = Time.deltaTime * _restReturnSpeed;

      if (_leftHandTarget  != null) { _leftHandTarget.position  = Vector3.Lerp(_leftHandTarget.position,   _leftHandRestPos,  t); _leftHandTarget.rotation  = Quaternion.Slerp(_leftHandTarget.rotation,  _leftHandRestRot,  t); }
      if (_rightHandTarget != null) { _rightHandTarget.position = Vector3.Lerp(_rightHandTarget.position,  _rightHandRestPos, t); _rightHandTarget.rotation = Quaternion.Slerp(_rightHandTarget.rotation, _rightHandRestRot, t); }
      if (_leftElbowHint   != null)   _leftElbowHint.position   = Vector3.Lerp(_leftElbowHint.position,   _leftElbowRestPos,  t);
      if (_rightElbowHint  != null)   _rightElbowHint.position  = Vector3.Lerp(_rightElbowHint.position,  _rightElbowRestPos, t);

      if (_leftArmIK  != null) _leftArmIK.weight  = Mathf.Lerp(_leftArmIK.weight,  0f, Time.deltaTime * _ikFadeOutSpeed);
      if (_rightArmIK != null) _rightArmIK.weight = Mathf.Lerp(_rightArmIK.weight, 0f, Time.deltaTime * _ikFadeOutSpeed);
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

      if (_leftArmIK  != null) _leftArmIK.weight  = Mathf.Lerp(_leftArmIK.weight,  1f, Time.deltaTime * 5f);
      if (_rightArmIK != null) _rightArmIK.weight = Mathf.Lerp(_rightArmIK.weight, 1f, Time.deltaTime * 5f);

      bool isHand0Left = snap.hand0Label == "Right"; // MediaPipe 거울 반전

      if (_mirrorMode)
      {
        // 미러 모드: pose index 12=오른어깨, 14=오른팔꿈치, 16=오른손목 → 골렘 왼팔
        UpdateArmIK(pose[12], pose[14], pose[16], _leftHandTarget,  _leftElbowHint,  isLeft: true);
        var leftHandMarks = isHand0Left ? hand0 : hand1;
        UpdateHandRotation(leftHandMarks, _leftHandTarget, isLeftHand: true);
        UpdateFingerTargets(leftHandMarks, _leftHandFingers, isLeftHand: true);

        UpdateArmIK(pose[11], pose[13], pose[15], _rightHandTarget, _rightElbowHint, isLeft: false);
        var rightHandMarks = isHand0Left ? hand1 : hand0;
        UpdateHandRotation(rightHandMarks, _rightHandTarget, isLeftHand: false);
        UpdateFingerTargets(rightHandMarks, _rightHandFingers, isLeftHand: false);
      }
      else
      {
        if (isHand0Left)
        {
          UpdateArmIK(pose[12], pose[14], pose[16], _rightHandTarget, _rightElbowHint, isLeft: false);
          UpdateHandRotation(hand0, _rightHandTarget, isLeftHand: false);
          UpdateFingerTargets(hand0, _rightHandFingers, isLeftHand: false);

          UpdateArmIK(pose[11], pose[13], pose[15], _leftHandTarget, _leftElbowHint, isLeft: true);
          UpdateHandRotation(hand1, _leftHandTarget, isLeftHand: true);
          UpdateFingerTargets(hand1, _leftHandFingers, isLeftHand: true);
        }
        else
        {
          UpdateArmIK(pose[11], pose[13], pose[15], _leftHandTarget, _leftElbowHint, isLeft: true);
          UpdateHandRotation(hand0, _leftHandTarget, isLeftHand: true);
          UpdateFingerTargets(hand0, _leftHandFingers, isLeftHand: true);

          UpdateArmIK(pose[12], pose[14], pose[16], _rightHandTarget, _rightElbowHint, isLeft: false);
          UpdateHandRotation(hand1, _rightHandTarget, isLeftHand: false);
          UpdateFingerTargets(hand1, _rightHandFingers, isLeftHand: false);
        }
      }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Arm IK - 어깨/팔꿈치/손목 Vector3로 IK target 위치 설정
    // PositionFilter 적용
    // ─────────────────────────────────────────────────────────────────────────
    private void UpdateArmIK(
      Vector3 shoulderPos, Vector3 elbowPos, Vector3 wristPos,
      Transform handTarget, Transform elbowHint, bool isLeft)
    {
      if (handTarget == null) return;

      Vector3 shoulderToWrist = wristPos - shoulderPos;
      shoulderToWrist.x *= _handAxisMultiplier.x;
      shoulderToWrist.y *= _handAxisMultiplier.y;
      shoulderToWrist.z *= _handAxisMultiplier.z;
      shoulderToWrist   *= _handReachMultiplier;

      // 필터로 위치 노이즈 제거
      PositionFilter handFilter  = isLeft ? _leftHandPosFilter  : _rightHandPosFilter;
      PositionFilter elbowFilter = isLeft ? _leftElbowPosFilter : _rightElbowPosFilter;

      Vector3 filteredWrist = handFilter.Update(shoulderPos + shoulderToWrist);
      handTarget.position = Vector3.Lerp(handTarget.position, filteredWrist, Time.deltaTime * _smoothing);

      if (elbowHint != null)
      {
        Vector3 shoulderToElbow = elbowPos - shoulderPos;
        shoulderToElbow.x *= _elbowXMultiplier;
        shoulderToElbow.y *= _elbowYMultiplier;
        shoulderToElbow.z  = shoulderToElbow.z * _elbowZMultiplier + _elbowForwardOffset;

        Vector3 filteredElbow = elbowFilter.Update(shoulderPos + shoulderToElbow);
        elbowHint.position = Vector3.Lerp(elbowHint.position, filteredElbow, Time.deltaTime * _smoothing);
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

      // orthonormal basis
      Vector3 right = Vector3.Cross(handDir, palmNormal).normalized;
      Vector3 up    = Vector3.Cross(right, handDir).normalized;
      if (right == Vector3.zero || up == Vector3.zero) return;

      Quaternion raw    = Quaternion.LookRotation(handDir, up);
      Quaternion final  = raw * Quaternion.Euler(_handRotationOffset) * Quaternion.Euler(isLeftHand ? _leftHandOffset : _rightHandOffset);

      handTarget.rotation = Quaternion.Slerp(handTarget.rotation, final, Time.deltaTime * _smoothing);

      if (_showPalmNormalGizmo)
      {
        if (isLeftHand) { _debugLeftPalmNormal  = palmNormal; _debugLeftWrist  = wristPos; }
        else            { _debugRightPalmNormal = palmNormal; _debugRightWrist = wristPos; }
      }
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
          Quaternion next = Quaternion.Slerp(current, target, Time.deltaTime * _fingerSmoothingSpeed);
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