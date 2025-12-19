using UnityEngine;
using UnityEngine.Animations.Rigging;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using System.Collections.Generic;

namespace Demo.GestureDetection
{
  public class AvatarLandmarkAnimator : MonoBehaviour
  {
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

    [Header("Finger Smoothing (Flickering Prevention)")]
    [SerializeField] private float _fingerSmoothingSpeed = 25f; // 손가락 전용 스무딩 속도
    [SerializeField] private float _minRotationThreshold = 1f;  // 최소 회전 임계값 (도 단위)

    [Header("Palm Orientation Control")]
    [SerializeField] private bool _usePalmConstraint = true;    // 손바닥 방향 제약 사용
    [SerializeField] private float _palmOrientationWeight = 0.7f; // 손바닥 방향 가중치 (0~1)
    [SerializeField] private bool _invertPalmNormal = false;    // 손바닥 법선 반전 (디버그용)
    [SerializeField] private bool _showPalmNormalGizmo = false; // 손바닥 방향 시각화

    [Header("Finger Bone Rotation Offset (리깅 보정)")]
    [SerializeField] private Vector3 _boneAxisCorrection = new Vector3(-90, 0, 0); // 본 로컬 축 보정 (Y축=forward)
    [SerializeField] private Vector3 _fingerRotationOffset = new Vector3(0, 0, 0);  // 추가 미세 조정
    [SerializeField] private Vector3 _thumbRotationOffset = new Vector3(0, 0, 0);   // 엄지 추가 조정

    [Header("Hand Bones (손목 본)")]
    [SerializeField] private Transform _leftHandBone;   // 왼손 손목 본
    [SerializeField] private Transform _rightHandBone;  // 오른손 손목 본

    [Header("Hand Rotation Adjustment")]
    // 아바타의 손목 본이 기본적으로 어떤 축을 향하는지에 따라 보정하는 값
    [SerializeField] private Vector3 _handRotationOffset = new Vector3(0, 0, 0); 
    // 왼손/오른손이 대칭이 아닐 경우를 대비한 추가 오프셋 (필요시 사용)
    [SerializeField] private Vector3 _leftHandOffset = new Vector3(0, 0, 0);
    [SerializeField] private Vector3 _rightHandOffset = new Vector3(0, 0, 0);

    // 손목 초기 회전 저장 (공전 계산용)
    private Quaternion _leftHandInitialRotation;
    private Quaternion _rightHandInitialRotation;
    private bool _handRotationsInitialized = false;

    private Transform[][] _leftHandFingers;
    private Transform[][] _rightHandFingers;

    // 디버그용 저장
    private Vector3 _debugLeftPalmNormal;
    private Vector3 _debugRightPalmNormal;
    private Vector3 _debugLeftWrist;
    private Vector3 _debugRightWrist;

    // 데이터를 저장해둘 변수
    private PoseLandmarkerResult _latestPoseResult;
    private HandLandmarkerResult _latestHandResult;
    private bool _hasNewData = false;

    // 손 떨림 보정
    private HandLandmarkerResult _cachedHandResult;
    private bool _hasCachedData = false;
    private float _dataTimeout = 1.0f;
    private float _lastDataTime;

    private readonly int[][] _fingerLandmarkIndices = new int[][]
    {
        new int[] { 1, 2, 3, 4 },       
        new int[] { 5, 6, 7, 8 },       
        new int[] { 9, 10, 11, 12 },    
        new int[] { 13, 14, 15, 16 },   
        new int[] { 17, 18, 19, 20 }    
    };

    // 손가락 회전 캐싱 (flickering 방지의 핵심)
    private Dictionary<Transform, Quaternion> _cachedFingerRotations = new Dictionary<Transform, Quaternion>();
    private Dictionary<Transform, Quaternion> _targetFingerRotations = new Dictionary<Transform, Quaternion>();
    
    // Animator 레이어 컨트롤
    private int _fingerLayerIndex = -1;
    private float _fingerLayerWeight = 0f;
    private float _fingerLayerFadeSpeed = 5f;

    private void Awake()
    {
      if (_animator == null) _animator = GetComponent<Animator>();
      if (_rigBuilder == null) _rigBuilder = GetComponent<RigBuilder>();
      
      // Finger 레이어 찾기 (있다면)
      for (int i = 0; i < _animator.layerCount; i++)
      {
        if (_animator.GetLayerName(i).Contains("Finger") || _animator.GetLayerName(i).Contains("Hand"))
        {
          _fingerLayerIndex = i;
          break;
        }
      }
    }

    private void Start()
    {
      if (_leftArmIK != null) _leftArmIK.weight = 0f;
      if (_rightArmIK != null) _rightArmIK.weight = 0f;

      // 손목 본 자동 찾기
      if (_leftHandBone == null && _animator != null)
      {
        _leftHandBone = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
      }
      if (_rightHandBone == null && _animator != null)
      {
        _rightHandBone = _animator.GetBoneTransform(HumanBodyBones.RightHand);
      }

      CacheFingerBones();
      InitializeFingerRotations(); // 초기 회전값 캐싱
    }

    // 외부(Runner)에서는 이 함수를 통해 데이터만 넣음.
    public void UpdateAvatar(PoseLandmarkerResult poseResult, HandLandmarkerResult handResult)
    {
        _latestPoseResult = poseResult;
        _latestHandResult = handResult;
        _hasNewData = true;
    }

    // [주의] 실제 움직임 처리는 Animator가 끝난 뒤인 LateUpdate에서 수행
    /* ⭐ 손가락 떨림 보정 
        - 좌표 튈 때 저장된 값 활용해 튄 값 - 캐시값 사이에서 천천히 변화하도록
    */
    private void LateUpdate()
    {
      // 1. 데이터 갱신 - 새 데이터가 들어왔다면 캐시(백업) 업데이트
      bool hasValidNewData = _hasNewData && 
                           _latestHandResult.handLandmarks != null && 
                           _latestHandResult.handLandmarks.Count > 0;
      if (hasValidNewData)
      {
        _cachedHandResult = _latestHandResult;
        _hasCachedData = true;
        _lastDataTime = Time.time;
        _hasNewData = false; // 플래그 리셋
      }
        
      // 2. 움직임 적용 - 백업 데이터가 유효하고, 타임아웃 안 지났으면 적용
      if (_hasCachedData && (Time.time - _lastDataTime <= _dataTimeout))
      {
        ProcessMovement(_cachedHandResult);
        
        // Animator 레이어 weight 조절 (손가락 기본 애니메이션 억제)
        if (_fingerLayerIndex >= 0)
        {
          _fingerLayerWeight = Mathf.Lerp(_fingerLayerWeight, 0f, Time.deltaTime * _fingerLayerFadeSpeed);
          _animator.SetLayerWeight(_fingerLayerIndex, _fingerLayerWeight);
        }
      }
      else
      {
        // 데이터 없음 - Animator 레이어 복구
        if (_fingerLayerIndex >= 0)
        {
          _fingerLayerWeight = Mathf.Lerp(_fingerLayerWeight, 1f, Time.deltaTime * _fingerLayerFadeSpeed);
          _animator.SetLayerWeight(_fingerLayerIndex, _fingerLayerWeight);
        }
      }
      
      // 3. 캐시된 회전값으로 부드럽게 보간 (flickering 방지)
      ApplyCachedFingerRotations();
    }

    /* ⭐ Pose Landmark의 어깨, 팔꿈치, 손목 좌표를 IK 타겟 위치로 설정
    */
    private void UpdateArmIK(
      Mediapipe.Tasks.Components.Containers.NormalizedLandmark shoulder,
      Mediapipe.Tasks.Components.Containers.NormalizedLandmark elbow,
      Mediapipe.Tasks.Components.Containers.NormalizedLandmark wrist,
      Transform handTarget,
      Transform elbowHint,
      bool isLeftArm)
    {
      if (handTarget == null) return;

      Vector3 shoulderPos = LandmarkTo3D.LandmarkToWorldPosition(shoulder);
      Vector3 elbowPos = LandmarkTo3D.LandmarkToWorldPosition(elbow);
      Vector3 wristPos = LandmarkTo3D.LandmarkToWorldPosition(wrist);

      // 어깨→손목 벡터 계산 및 스케일 조정
      Vector3 shoulderToWrist = wristPos - shoulderPos;
      shoulderToWrist.x *= _handAxisMultiplier.x;
      shoulderToWrist.y *= _handAxisMultiplier.y;
      shoulderToWrist.z *= _handAxisMultiplier.z;
      shoulderToWrist *= _handReachMultiplier;

      // 자연스러운 움직임 - 보간 적용
      Vector3 finalWristPos = shoulderPos + shoulderToWrist;
      handTarget.position = Vector3.Lerp(handTarget.position, finalWristPos, Time.deltaTime * _smoothing);

      if (elbowHint != null)
      {
        Vector3 shoulderToElbow = elbowPos - shoulderPos;
        shoulderToElbow.x *= _elbowXMultiplier;
        shoulderToElbow.y *= _elbowYMultiplier;
        shoulderToElbow.z *= _elbowZMultiplier;
        shoulderToElbow.z += _elbowForwardOffset; 
    
        Vector3 finalElbowPos = shoulderPos + shoulderToElbow;
        elbowHint.position = Vector3.Lerp(elbowHint.position, finalElbowPos, Time.deltaTime * _smoothing);
      }
    }

    public void ResetToIdle()
    {
      if (_leftArmIK != null) _leftArmIK.weight = 0f;
      if (_rightArmIK != null) _rightArmIK.weight = 0f;
      
      // Animator 레이어 복구
      if (_fingerLayerIndex >= 0)
      {
        _animator.SetLayerWeight(_fingerLayerIndex, 1f);
      }
    }

    /* ⭐ 손목 회전
          손바닥 평면 정의
          손바닥 방향 판단 위해 normal vector 계산
          손 뒤틀리는 것 완화하기 위한 직교화
    */
    private void UpdateHandRotation(
        System.Collections.Generic.List<Mediapipe.Tasks.Components.Containers.NormalizedLandmark> landmarks,
        Transform handTarget,
        bool isLeftHand)
    {
        if (landmarks == null || landmarks.Count <= 17 || handTarget == null) return;

        // 1. 핵심 랜드마크 추출 (0:손목, 5:검지MCP, 17:새끼MCP)
        Vector3 wristPos = LandmarkTo3D.LandmarkToWorldPosition(landmarks[0]);
        Vector3 indexPos = LandmarkTo3D.LandmarkToWorldPosition(landmarks[5]);
        Vector3 pinkyPos = LandmarkTo3D.LandmarkToWorldPosition(landmarks[17]);

        // 2. 손바닥 평면 정의
        Vector3 midFingerBase = (indexPos + pinkyPos) * 0.5f;
        Vector3 handDirection = (midFingerBase - wristPos).normalized;
        Vector3 palmWidthDirection = (pinkyPos - indexPos).normalized;

        // 3. 손바닥 Normal vector 계산 (손바닥이 보는 방향)
        // 왼손/오른손에 따라 외적 순서 차이
        Vector3 palmNormal;
        if (isLeftHand)
            palmNormal = Vector3.Cross(handDirection, palmWidthDirection).normalized; // 왼손
        else
            palmNormal = Vector3.Cross(palmWidthDirection, handDirection).normalized; // 오른손

        // 4. 직교화 - 특정 각도에서 손 뒤틀리는 것 완화
        Vector3 fixedUp = palmNormal; 
        Vector3 fixedForward = handDirection;
        
        // 회전 보정 - Up 벡터와 Forward 벡터를 서로 수직이 되게 재조정
        Vector3 fixedRight = Vector3.Cross(fixedForward, fixedUp).normalized;
        fixedUp = Vector3.Cross(fixedRight, fixedForward).normalized;

        // 5. 회전 생성
        Quaternion rawRotation = Quaternion.LookRotation(fixedForward, fixedUp);

        // 6. 오프셋 적용 (아바타 리깅에 맞게 축 보정)
        Quaternion offsetRot = Quaternion.Euler(_handRotationOffset);
        Quaternion specificOffset = Quaternion.Euler(isLeftHand ? _leftHandOffset : _rightHandOffset);

        Quaternion finalRotation = rawRotation * offsetRot * specificOffset;

        // 7. 적용
        handTarget.rotation = Quaternion.Slerp(handTarget.rotation, finalRotation, Time.deltaTime * _smoothing);
    }

    // ⭐ IK 처리
    private void ProcessMovement(HandLandmarkerResult result)
    {
      bool bothHandsDetected = _latestHandResult.handLandmarks != null && _latestHandResult.handLandmarks.Count >= 2;
      bool poseDetected = _latestPoseResult.poseLandmarks != null && _latestPoseResult.poseLandmarks.Count > 0;

      // 1. IK 가중치 처리 (손이 감지되지 않으면 IK를 부드럽게 끔)
      if (!bothHandsDetected || !poseDetected)
      {
        if (_leftArmIK != null) _leftArmIK.weight = Mathf.Lerp(_leftArmIK.weight, 0f, Time.deltaTime * 5f);
        if (_rightArmIK != null) _rightArmIK.weight = Mathf.Lerp(_rightArmIK.weight, 0f, Time.deltaTime * 5f);
        return;
      }

      // 감지됨: IK 가중치 활성화
      if (_leftArmIK != null) _leftArmIK.weight = Mathf.Lerp(_leftArmIK.weight, 1f, Time.deltaTime * 5f);
      if (_rightArmIK != null) _rightArmIK.weight = Mathf.Lerp(_rightArmIK.weight, 1f, Time.deltaTime * 5f);

      // Pose 랜드마크 (팔 위치 계산용)
      var poseLandmarks = _latestPoseResult.poseLandmarks[0].landmarks;

      // 2. 위치 및 회전 업데이트
      if (result.handedness != null && result.handedness.Count >= 2)
      {
          // MediaPipe의 Handedness 라벨 확인 (Left/Right)
          string label0 = result.handedness[0].categories[0].categoryName; 
          
          var hand0Marks = result.handLandmarks[0].landmarks; 
          var hand1Marks = result.handLandmarks[1].landmarks;

          // MediaPipe "Right" 라벨 = 실제 사용자 왼손 (거울 모드 등 고려 필요)
          // label0 == "Right"이면 hand0이 왼손 데이터라고 가정
          bool isHand0Left = label0 == "Right"; 

          if (_mirrorMode)
          {
              // 왼손
              // 1. 팔 위치 (Pose 기반)
              UpdateArmIK(poseLandmarks[12], poseLandmarks[14], poseLandmarks[16], _leftHandTarget, _leftElbowHint, true);
              
              // 2. 손목 회전 (Hand 기반)
              // 사용 데이터: hand0 (만약 hand0이 왼손이라면)
              var leftHandMarks = isHand0Left ? hand0Marks : hand1Marks;
              UpdateHandRotation(leftHandMarks, _leftHandTarget, true);

              // 3. 손가락 각도 (Hand 기반)
              UpdateFingerTargets(leftHandMarks, _leftHandFingers, true);

              // 오른손
              // 1. 팔 위치
              UpdateArmIK(poseLandmarks[11], poseLandmarks[13], poseLandmarks[15], _rightHandTarget, _rightElbowHint, false);
              
              // 2. 손목 회전
              var rightHandMarks = isHand0Left ? hand1Marks : hand0Marks;
              UpdateHandRotation(rightHandMarks, _rightHandTarget, false);

              // 3. 손가락 각도
              UpdateFingerTargets(rightHandMarks, _rightHandFingers, false);
          }
          else
          {
              // 거울 모드 설정 따라 왼손, 오른손 타겟 데이터 반전
              if (isHand0Left) {
                  // Hand0(Left) -> Right Target
                  UpdateArmIK(poseLandmarks[12], poseLandmarks[14], poseLandmarks[16], _rightHandTarget, _rightElbowHint, false);
                  UpdateHandRotation(hand0Marks, _rightHandTarget, false);
                  UpdateFingerTargets(hand0Marks, _rightHandFingers, false);

                  // Hand1(Right) -> Left Target
                  UpdateArmIK(poseLandmarks[11], poseLandmarks[13], poseLandmarks[15], _leftHandTarget, _leftElbowHint, true);
                  UpdateHandRotation(hand1Marks, _leftHandTarget, true);
                  UpdateFingerTargets(hand1Marks, _leftHandFingers, true);
              } else {
                  // Hand0(Right) -> Left Target
                  UpdateArmIK(poseLandmarks[11], poseLandmarks[13], poseLandmarks[15], _leftHandTarget, _leftElbowHint, true);
                  UpdateHandRotation(hand0Marks, _leftHandTarget, true);
                  UpdateFingerTargets(hand0Marks, _leftHandFingers, true);

                  // Hand1(Left) -> Right Target
                  UpdateArmIK(poseLandmarks[12], poseLandmarks[14], poseLandmarks[16], _rightHandTarget, _rightElbowHint, false);
                  UpdateHandRotation(hand1Marks, _rightHandTarget, false);
                  UpdateFingerTargets(hand1Marks, _rightHandFingers, false);
              }
          }
      }
    }

    // 초기 손가락 회전값 설정
    private void InitializeFingerRotations()
    {
      _cachedFingerRotations.Clear();
      _targetFingerRotations.Clear();
      
      if (_leftHandFingers != null)
      {
        foreach (var finger in _leftHandFingers)
        {
          foreach (var bone in finger)
          {
            if (bone != null)
            {
              _cachedFingerRotations[bone] = bone.rotation;
              _targetFingerRotations[bone] = bone.rotation;
            }
          }
        }
      }
      
      if (_rightHandFingers != null)
      {
        foreach (var finger in _rightHandFingers)
        {
          foreach (var bone in finger)
          {
            if (bone != null)
            {
              _cachedFingerRotations[bone] = bone.rotation;
              _targetFingerRotations[bone] = bone.rotation;
            }
          }
        }
      }
    }
    
    // 설정된 회전값으로 부드럽게 적용 (flickering 방지)
    private void ApplyCachedFingerRotations()
    {
      foreach (var kvp in _targetFingerRotations)
      {
        Transform bone = kvp.Key;
        Quaternion targetRotWorld = kvp.Value;
        
        if (bone == null || bone.parent == null) continue;
        
        // 캐시된 현재 회전값 (월드 회전)
        if (!_cachedFingerRotations.ContainsKey(bone))
        {
          _cachedFingerRotations[bone] = bone.rotation;
        }
        
        Quaternion currentRotWorld = _cachedFingerRotations[bone];
        
        // 부드럽게 보간 (월드 회전)
        Quaternion newRotWorld = Quaternion.Slerp(currentRotWorld, targetRotWorld, Time.deltaTime * _fingerSmoothingSpeed);
        
        // 너무 작은 변화는 무시 (떨림 방지)
        if (Quaternion.Angle(currentRotWorld, targetRotWorld) > _minRotationThreshold)
        {
          _cachedFingerRotations[bone] = newRotWorld;
          
          // 월드 회전 → 로컬 회전으로 변환하여 적용 (부모 회전 자동 적용)
          Quaternion localRot = Quaternion.Inverse(bone.parent.rotation) * newRotWorld;
          bone.localRotation = localRot;
        }
        else
        {
          // 변화가 작으면 현재 회전 유지
          Quaternion localRot = Quaternion.Inverse(bone.parent.rotation) * currentRotWorld;
          bone.localRotation = localRot;
        }
      }
    }

    private void CacheFingerBones()
    {
        _leftHandFingers = new Transform[5][];
        _rightHandFingers = new Transform[5][];

        var leftBones = new HumanBodyBones[][] {
            new[] { HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbDistal },
            new[] { HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal },
            new[] { HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal },
            new[] { HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal },
            new[] { HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal }
        };

        var rightBones = new HumanBodyBones[][] {
            new[] { HumanBodyBones.RightThumbProximal, HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal },
            new[] { HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal },
            new[] { HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal },
            new[] { HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingDistal },
            new[] { HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal }
        };

        for (int i = 0; i < 5; i++)
        {
            _leftHandFingers[i] = new Transform[3];
            _rightHandFingers[i] = new Transform[3];
            for (int j = 0; j < 3; j++)
            {
                _leftHandFingers[i][j] = _animator.GetBoneTransform(leftBones[i][j]);
                _rightHandFingers[i][j] = _animator.GetBoneTransform(rightBones[i][j]);
            }
        }
    }
    
    // 타겟 회전값만 업데이트
    private void UpdateFingerTargets(List<Mediapipe.Tasks.Components.Containers.NormalizedLandmark> landmarks, Transform[][] handBones, bool isLeftHand)
    {
        if (handBones == null) return;

        // 손목 본 가져오기 (로컬 좌표계 기준으로 계산)
        Transform handBone = isLeftHand ? _leftHandBone : _rightHandBone;
        if (handBone == null)
        {
            Debug.LogWarning($"[UpdateFingerTargets] Hand bone not found for {(isLeftHand ? "LEFT" : "RIGHT")} hand!");
            return;
        }

        Vector3 wrist = LandmarkTo3D.LandmarkToWorldPosition(landmarks[0]);
        Vector3 indexMCP = LandmarkTo3D.LandmarkToWorldPosition(landmarks[5]);
        Vector3 pinkyMCP = LandmarkTo3D.LandmarkToWorldPosition(landmarks[17]);
        Vector3 middleMCP = LandmarkTo3D.LandmarkToWorldPosition(landmarks[9]);

        // 손등 방향 계산 (카메라를 향하는 쪽)
        // 1. 손바닥 평면의 두 벡터
        Vector3 wristToMiddle = (middleMCP - wrist).normalized;  // 손가락 방향
        Vector3 indexToPinky = (pinkyMCP - indexMCP).normalized;  // 검지→새끼 방향
        
        // 2. 외적으로 손바닥에서 나오는 법선 계산
        Vector3 palmOutward = Vector3.Cross(wristToMiddle, indexToPinky).normalized;
        
        // 3. 손등 방향 = 손바닥 반대 방향 (카메라를 향하는 쪽)
        // MediaPipe: 오른손 라벨 = 사용자 왼손
        Vector3 backOfHandDirection = isLeftHand ? -palmOutward : palmOutward;
        
        // 4. 디버그: 방향 반전 옵션
        if (_invertPalmNormal)
        {
            backOfHandDirection = -backOfHandDirection;
        }

        // 5. 디버그: Gizmo용 저장
        if (_showPalmNormalGizmo)
        {
            if (isLeftHand)
            {
                _debugLeftPalmNormal = backOfHandDirection;
                _debugLeftWrist = wrist;
            }
            else
            {
                _debugRightPalmNormal = backOfHandDirection;
                _debugRightWrist = wrist;
            }
        }

        for (int i = 0; i < 5; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                Transform bone = handBones[i][j];
                if (bone == null) continue;

                int currentIdx = _fingerLandmarkIndices[i][j];
                int nextIdx = _fingerLandmarkIndices[i][j + 1];

                Vector3 currentPosWorld = LandmarkTo3D.LandmarkToWorldPosition(landmarks[currentIdx]);
                Vector3 nextPosWorld = LandmarkTo3D.LandmarkToWorldPosition(landmarks[nextIdx]);

                // 월드 좌표 → 손목 로컬 좌표로 변환 (위치 변환!)
                // 이렇게 해야 손목 회전 시 손가락도 공전함!
                Vector3 currentPosLocal = handBone.InverseTransformPoint(currentPosWorld);
                Vector3 nextPosLocal = handBone.InverseTransformPoint(nextPosWorld);
                
                // 로컬 좌표에서 방향 계산
                Vector3 targetDirLocal = (nextPosLocal - currentPosLocal).normalized;
                
                if (targetDirLocal == Vector3.zero) continue;
                
                // upVector도 손목 로컬 좌표로 변환
                Vector3 upVectorWorld = backOfHandDirection;
                Vector3 upVectorLocal = handBone.InverseTransformDirection(upVectorWorld);
                
                // targetDir과 upVector가 평행하면 문제 발생 - 대체 벡터 사용
                float parallelCheck = Mathf.Abs(Vector3.Dot(targetDirLocal.normalized, upVectorLocal.normalized));
                if (parallelCheck > 0.98f)
                {
                    // 평행하거나 거의 평행 - 대체 벡터 사용
                    Vector3 indexToPinkyLocal = handBone.InverseTransformDirection(indexToPinky);
                    upVectorLocal = Vector3.Cross(targetDirLocal, indexToPinkyLocal).normalized;
                    if (upVectorLocal == Vector3.zero)
                    {
                        upVectorLocal = Vector3.up; // 최후의 수단
                    }
                }
                
                // targetDir과 직교하는 upVector 생성 (Gram-Schmidt)
                Vector3 rightLocal = Vector3.Cross(targetDirLocal, upVectorLocal).normalized;
                if (rightLocal != Vector3.zero)
                {
                    upVectorLocal = Vector3.Cross(rightLocal, targetDirLocal).normalized;
                }
                
                // 로컬 좌표계에서 회전 계산
                Quaternion targetRotLocal = Quaternion.LookRotation(targetDirLocal, upVectorLocal);
                
                // 본의 로컬 축 변환 (설정 가능)
                // 기본값: (-90, 0, 0) = Y축이 forward
                Quaternion boneAxisCorrection = Quaternion.Euler(_boneAxisCorrection);
                targetRotLocal = targetRotLocal * boneAxisCorrection;
                
                // 손가락 본 회전 오프셋 적용 (추가 미세 조정)
                Vector3 offsetToApply = (i == 0) ? _thumbRotationOffset : _fingerRotationOffset;
                if (offsetToApply != Vector3.zero)
                {
                    Quaternion offset = Quaternion.Euler(offsetToApply);
                    targetRotLocal = targetRotLocal * offset;
                }
                
                // 손바닥 방향 제약 적용 (inspector 설정)
                // 로컬 회전에 적용하므로 이전 로컬 회전과 비교
                if (_usePalmConstraint && _cachedFingerRotations.ContainsKey(bone))
                {
                    // 설정된 월드 회전 → 로컬 회전 변환
                    Quaternion cachedWorldRot = _cachedFingerRotations[bone];
                    Quaternion cachedLocalRot = Quaternion.Inverse(handBone.rotation) * cachedWorldRot;
                    // 자연스러운 로컬 회전
                    targetRotLocal = Quaternion.Slerp(cachedLocalRot, targetRotLocal, _palmOrientationWeight);
                }
                
                // 로컬 회전을 타겟으로 저장 (월드 회전으로 변환하여 저장)
                Quaternion targetRotWorld = handBone.rotation * targetRotLocal;
                _targetFingerRotations[bone] = targetRotWorld; // 타겟만 저장
            }
        }
    }

    
  }
}