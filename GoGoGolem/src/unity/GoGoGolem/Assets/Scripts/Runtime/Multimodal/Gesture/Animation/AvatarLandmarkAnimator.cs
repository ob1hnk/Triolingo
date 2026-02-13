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
    [SerializeField] private float _fingerSmoothingSpeed = 25f;
    [SerializeField] private float _minRotationThreshold = 1f;

    [Header("Palm Orientation Control")]
    [SerializeField] private bool _usePalmConstraint = true;
    [SerializeField] private float _palmOrientationWeight = 0.7f;
    [SerializeField] private bool _invertPalmNormal = false;
    [SerializeField] private bool _showPalmNormalGizmo = false;

    [Header("Finger Bone Rotation Offset")]
    [SerializeField] private Vector3 _boneAxisCorrection = new Vector3(-90, 0, 0);
    [SerializeField] private Vector3 _fingerRotationOffset = new Vector3(0, 0, 0);
    [SerializeField] private Vector3 _thumbRotationOffset = new Vector3(0, 0, 0);

    [Header("Hand Bones")]
    [SerializeField] private Transform _leftHandBone;
    [SerializeField] private Transform _rightHandBone;

    [Header("Hand Rotation Adjustment")]
    [SerializeField] private Vector3 _handRotationOffset = new Vector3(0, 0, 0); 
    [SerializeField] private Vector3 _leftHandOffset = new Vector3(0, 0, 0);
    [SerializeField] private Vector3 _rightHandOffset = new Vector3(0, 0, 0);

    private Quaternion _leftHandInitialRotation;
    private Quaternion _rightHandInitialRotation;
    private bool _handRotationsInitialized = false;

    private Transform[][] _leftHandFingers;
    private Transform[][] _rightHandFingers;

    private Vector3 _debugLeftPalmNormal;
    private Vector3 _debugRightPalmNormal;
    private Vector3 _debugLeftWrist;
    private Vector3 _debugRightWrist;

    private PoseLandmarkerResult _latestPoseResult;
    private HandLandmarkerResult _latestHandResult;
    private bool _hasNewData = false;

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

    private Dictionary<Transform, Quaternion> _cachedFingerRotations = new Dictionary<Transform, Quaternion>();
    private Dictionary<Transform, Quaternion> _targetFingerRotations = new Dictionary<Transform, Quaternion>();
    
    private int _fingerLayerIndex = -1;
    private float _fingerLayerWeight = 0f;
    private float _fingerLayerFadeSpeed = 5f;

    private void Awake()
    {
      if (_animator == null) _animator = GetComponent<Animator>();
      if (_rigBuilder == null) _rigBuilder = GetComponent<RigBuilder>();
      
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

      if (_leftHandBone == null && _animator != null)
      {
        _leftHandBone = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
      }
      if (_rightHandBone == null && _animator != null)
      {
        _rightHandBone = _animator.GetBoneTransform(HumanBodyBones.RightHand);
      }

      CacheFingerBones();
      InitializeFingerRotations();
    }

    // 데이터 검증 추가
    public void UpdateAvatar(PoseLandmarkerResult poseResult, HandLandmarkerResult handResult)
    {
        // 데이터 검증 - 잘못된 데이터는 저장하지 않음
        if (!IsValidData(poseResult, handResult))
        {
            return;
        }
        
        _latestPoseResult = poseResult;
        _latestHandResult = handResult;
        _hasNewData = true;
    }

    // [주의] 실제 움직임 처리는 Animator가 끝난 뒤인 LateUpdate에서 수행
    /* 손가락 떨림 보정 
        - 좌표 튈 때 저장된 값 활용해 튄 값 - 캐시값 사이에서 천천히 변화하도록
    */
    private void LateUpdate()
    {
      bool hasValidNewData = _hasNewData && 
                           _latestHandResult.handLandmarks != null && 
                           _latestHandResult.handLandmarks.Count > 0;
      if (hasValidNewData)
      {
        _cachedHandResult = _latestHandResult;
        _hasCachedData = true;
        _lastDataTime = Time.time;
        _hasNewData = false;
      }
        
      if (_hasCachedData && (Time.time - _lastDataTime <= _dataTimeout))
      {
        ProcessMovement(_cachedHandResult);
        
        if (_fingerLayerIndex >= 0)
        {
          _fingerLayerWeight = Mathf.Lerp(_fingerLayerWeight, 0f, Time.deltaTime * _fingerLayerFadeSpeed);
          _animator.SetLayerWeight(_fingerLayerIndex, _fingerLayerWeight);
        }
      }
      else
      {
        if (_fingerLayerIndex >= 0)
        {
          _fingerLayerWeight = Mathf.Lerp(_fingerLayerWeight, 1f, Time.deltaTime * _fingerLayerFadeSpeed);
          _animator.SetLayerWeight(_fingerLayerIndex, _fingerLayerWeight);
        }
      }
      
      ApplyCachedFingerRotations();
    }

    /* Pose Landmark의 어깨, 팔꿈치, 손목 좌표를 IK 타겟 위치로 설정
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

      Vector3 shoulderToWrist = wristPos - shoulderPos;
      shoulderToWrist.x *= _handAxisMultiplier.x;
      shoulderToWrist.y *= _handAxisMultiplier.y;
      shoulderToWrist.z *= _handAxisMultiplier.z;
      shoulderToWrist *= _handReachMultiplier;

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
      
      if (_fingerLayerIndex >= 0)
      {
        _animator.SetLayerWeight(_fingerLayerIndex, 1f);
      }
    }

    /* 손목 회전
      손바닥 평면 정의
      손바닥 방향 판단 위해 normal vector 계산 
      손 뒤틀리는 것 완화하기 위한 직교화
    */
    private void UpdateHandRotation(
        System.Collections.Generic.List<Mediapipe.Tasks.Components.Containers.NormalizedLandmark> landmarks,
        Transform handTarget,
        bool isLeftHand)
    {
        if (landmarks == null || landmarks.Count < 21 || handTarget == null)
        {
            if (landmarks != null && landmarks.Count < 21)
            {
                Debug.LogWarning($"[UpdateHandRotation] Insufficient landmarks for {(isLeftHand ? "LEFT" : "RIGHT")} hand: {landmarks.Count}/21");
            }
            return;
        }

        Vector3 wristPos = LandmarkTo3D.LandmarkToWorldPosition(landmarks[0]);
        Vector3 indexPos = LandmarkTo3D.LandmarkToWorldPosition(landmarks[5]);
        Vector3 pinkyPos = LandmarkTo3D.LandmarkToWorldPosition(landmarks[17]);

        Vector3 midFingerBase = (indexPos + pinkyPos) * 0.5f;
        Vector3 handDirection = (midFingerBase - wristPos).normalized;
        Vector3 palmWidthDirection = (pinkyPos - indexPos).normalized;

        Vector3 palmNormal;
        if (isLeftHand)
            palmNormal = Vector3.Cross(handDirection, palmWidthDirection).normalized;
        else
            palmNormal = Vector3.Cross(palmWidthDirection, handDirection).normalized;

        Vector3 fixedUp = palmNormal; 
        Vector3 fixedForward = handDirection;
        
        Vector3 fixedRight = Vector3.Cross(fixedForward, fixedUp).normalized;
        fixedUp = Vector3.Cross(fixedRight, fixedForward).normalized;

        Quaternion rawRotation = Quaternion.LookRotation(fixedForward, fixedUp);

        Quaternion offsetRot = Quaternion.Euler(_handRotationOffset);
        Quaternion specificOffset = Quaternion.Euler(isLeftHand ? _leftHandOffset : _rightHandOffset);

        Quaternion finalRotation = rawRotation * offsetRot * specificOffset;

        handTarget.rotation = Quaternion.Slerp(handTarget.rotation, finalRotation, Time.deltaTime * _smoothing);
    }

    // IK 처리
    private void ProcessMovement(HandLandmarkerResult result)
    {
      bool bothHandsDetected = _latestHandResult.handLandmarks != null && _latestHandResult.handLandmarks.Count >= 2;
      bool poseDetected = _latestPoseResult.poseLandmarks != null && _latestPoseResult.poseLandmarks.Count > 0;

      if (!bothHandsDetected || !poseDetected)
      {
        if (_leftArmIK != null) _leftArmIK.weight = Mathf.Lerp(_leftArmIK.weight, 0f, Time.deltaTime * 5f);
        if (_rightArmIK != null) _rightArmIK.weight = Mathf.Lerp(_rightArmIK.weight, 0f, Time.deltaTime * 5f);
        return;
      }

      if (_leftArmIK != null) _leftArmIK.weight = Mathf.Lerp(_leftArmIK.weight, 1f, Time.deltaTime * 5f);
      if (_rightArmIK != null) _rightArmIK.weight = Mathf.Lerp(_rightArmIK.weight, 1f, Time.deltaTime * 5f);

      var poseLandmarks = _latestPoseResult.poseLandmarks[0].landmarks;

      // Pose landmarks 개수 검증 (최소 17개 필요: 0-16 인덱스)
      if (poseLandmarks.Count < 17)
      {
        Debug.LogWarning($"[ProcessMovement] Insufficient pose landmarks: {poseLandmarks.Count}/17");
        return;
      }

      if (result.handedness != null && result.handedness.Count >= 2)
      {
          // Handedness categories 검증 (왼손/오른손 구분 정보)
          if (result.handedness[0].categories == null || result.handedness[0].categories.Count == 0 ||
              result.handedness[1].categories == null || result.handedness[1].categories.Count == 0)
          {
            Debug.LogWarning("[ProcessMovement] Handedness categories are empty - cannot determine left/right hand");
            return;
          }

          string label0 = result.handedness[0].categories[0].categoryName;

          var hand0Marks = result.handLandmarks[0].landmarks;
          var hand1Marks = result.handLandmarks[1].landmarks;

          // 각 손의 landmarks가 21개(0-20 인덱스)인지 검증
          if (hand0Marks.Count < 21 || hand1Marks.Count < 21)
          {
            Debug.LogWarning($"[ProcessMovement] Incomplete hand landmarks - Hand0: {hand0Marks.Count}/21, Hand1: {hand1Marks.Count}/21");
            return;
          }

          bool isHand0Left = label0 == "Right"; 

          if (_mirrorMode)
          {
              UpdateArmIK(poseLandmarks[12], poseLandmarks[14], poseLandmarks[16], _leftHandTarget, _leftElbowHint, true);
              
              var leftHandMarks = isHand0Left ? hand0Marks : hand1Marks;
              UpdateHandRotation(leftHandMarks, _leftHandTarget, true);
              UpdateFingerTargets(leftHandMarks, _leftHandFingers, true);

              UpdateArmIK(poseLandmarks[11], poseLandmarks[13], poseLandmarks[15], _rightHandTarget, _rightElbowHint, false);
              
              var rightHandMarks = isHand0Left ? hand1Marks : hand0Marks;
              UpdateHandRotation(rightHandMarks, _rightHandTarget, false);
              UpdateFingerTargets(rightHandMarks, _rightHandFingers, false);
          }
          else
          {
              if (isHand0Left) {
                  UpdateArmIK(poseLandmarks[12], poseLandmarks[14], poseLandmarks[16], _rightHandTarget, _rightElbowHint, false);
                  UpdateHandRotation(hand0Marks, _rightHandTarget, false);
                  UpdateFingerTargets(hand0Marks, _rightHandFingers, false);

                  UpdateArmIK(poseLandmarks[11], poseLandmarks[13], poseLandmarks[15], _leftHandTarget, _leftElbowHint, true);
                  UpdateHandRotation(hand1Marks, _leftHandTarget, true);
                  UpdateFingerTargets(hand1Marks, _leftHandFingers, true);
              } else {
                  UpdateArmIK(poseLandmarks[11], poseLandmarks[13], poseLandmarks[15], _leftHandTarget, _leftElbowHint, true);
                  UpdateHandRotation(hand0Marks, _leftHandTarget, true);
                  UpdateFingerTargets(hand0Marks, _leftHandFingers, true);

                  UpdateArmIK(poseLandmarks[12], poseLandmarks[14], poseLandmarks[16], _rightHandTarget, _rightElbowHint, false);
                  UpdateHandRotation(hand1Marks, _rightHandTarget, false);
                  UpdateFingerTargets(hand1Marks, _rightHandFingers, false);
              }
          }
      }
    }

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
    
    private void ApplyCachedFingerRotations()
    {
      foreach (var kvp in _targetFingerRotations)
      {
        Transform bone = kvp.Key;
        Quaternion targetRotWorld = kvp.Value;
        
        if (bone == null || bone.parent == null) continue;
        
        if (!_cachedFingerRotations.ContainsKey(bone))
        {
          _cachedFingerRotations[bone] = bone.rotation;
        }
        
        Quaternion currentRotWorld = _cachedFingerRotations[bone];
        
        Quaternion newRotWorld = Quaternion.Slerp(currentRotWorld, targetRotWorld, Time.deltaTime * _fingerSmoothingSpeed);
        
        if (Quaternion.Angle(currentRotWorld, targetRotWorld) > _minRotationThreshold)
        {
          _cachedFingerRotations[bone] = newRotWorld;
          
          Quaternion localRot = Quaternion.Inverse(bone.parent.rotation) * newRotWorld;
          bone.localRotation = localRot;
        }
        else
        {
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
    
    private void UpdateFingerTargets(List<Mediapipe.Tasks.Components.Containers.NormalizedLandmark> landmarks, Transform[][] handBones, bool isLeftHand)
    {
        if (handBones == null) return;

        // Landmark 개수 검증 (21개 필수: 인덱스 0-20)
        if (landmarks == null || landmarks.Count < 21)
        {
            Debug.LogWarning($"[UpdateFingerTargets] Insufficient landmarks for {(isLeftHand ? "LEFT" : "RIGHT")} hand: {landmarks?.Count ?? 0}/21");
            return;
        }

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

        Vector3 wristToMiddle = (middleMCP - wrist).normalized;
        Vector3 indexToPinky = (pinkyMCP - indexMCP).normalized;
        
        Vector3 palmOutward = Vector3.Cross(wristToMiddle, indexToPinky).normalized;
        
        Vector3 backOfHandDirection = isLeftHand ? -palmOutward : palmOutward;
        
        if (_invertPalmNormal)
        {
            backOfHandDirection = -backOfHandDirection;
        }

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

                // 인덱스 범위 체크 (방어 로직)
                if (currentIdx >= landmarks.Count || nextIdx >= landmarks.Count)
                {
                    Debug.LogWarning($"[UpdateFingerTargets] Index out of range - finger:{i}, bone:{j}, currentIdx:{currentIdx}, nextIdx:{nextIdx}, landmarksCount:{landmarks.Count}");
                    continue;
                }

                Vector3 currentPosWorld = LandmarkTo3D.LandmarkToWorldPosition(landmarks[currentIdx]);
                Vector3 nextPosWorld = LandmarkTo3D.LandmarkToWorldPosition(landmarks[nextIdx]);

                Vector3 currentPosLocal = handBone.InverseTransformPoint(currentPosWorld);
                Vector3 nextPosLocal = handBone.InverseTransformPoint(nextPosWorld);
                
                Vector3 targetDirLocal = (nextPosLocal - currentPosLocal).normalized;
                
                if (targetDirLocal == Vector3.zero) continue;
                
                Vector3 upVectorWorld = backOfHandDirection;
                Vector3 upVectorLocal = handBone.InverseTransformDirection(upVectorWorld);
                
                float parallelCheck = Mathf.Abs(Vector3.Dot(targetDirLocal.normalized, upVectorLocal.normalized));
                if (parallelCheck > 0.98f)
                {
                    Vector3 indexToPinkyLocal = handBone.InverseTransformDirection(indexToPinky);
                    upVectorLocal = Vector3.Cross(targetDirLocal, indexToPinkyLocal).normalized;
                    if (upVectorLocal == Vector3.zero)
                    {
                        upVectorLocal = Vector3.up;
                    }
                }
                
                Vector3 rightLocal = Vector3.Cross(targetDirLocal, upVectorLocal).normalized;
                if (rightLocal != Vector3.zero)
                {
                    upVectorLocal = Vector3.Cross(rightLocal, targetDirLocal).normalized;
                }
                
                Quaternion targetRotLocal = Quaternion.LookRotation(targetDirLocal, upVectorLocal);
                
                Quaternion boneAxisCorrection = Quaternion.Euler(_boneAxisCorrection);
                targetRotLocal = targetRotLocal * boneAxisCorrection;
                
                Vector3 offsetToApply = (i == 0) ? _thumbRotationOffset : _fingerRotationOffset;
                if (offsetToApply != Vector3.zero)
                {
                    Quaternion offset = Quaternion.Euler(offsetToApply);
                    targetRotLocal = targetRotLocal * offset;
                }
                
                if (_usePalmConstraint && _cachedFingerRotations.ContainsKey(bone))
                {
                    Quaternion cachedWorldRot = _cachedFingerRotations[bone];
                    Quaternion cachedLocalRot = Quaternion.Inverse(handBone.rotation) * cachedWorldRot;
                    targetRotLocal = Quaternion.Slerp(cachedLocalRot, targetRotLocal, _palmOrientationWeight);
                }
                
                Quaternion targetRotWorld = handBone.rotation * targetRotLocal;
                _targetFingerRotations[bone] = targetRotWorld;
            }
        }
    }

    // 데이터 검증 메서드
    /// <summary>
    /// Pose와 Hand 데이터 유효성 검사
    /// </summary>
    private bool IsValidData(PoseLandmarkerResult poseResult, HandLandmarkerResult handResult)
    {
      // 1. Pose 데이터 검증
      if (poseResult.poseLandmarks == null || poseResult.poseLandmarks.Count == 0)
        return false;
      
      // 2. Hand 데이터 검증
      if (handResult.handLandmarks == null)
        return false;
      
      // 3. 손이 2개 감지되었는지 확인
      if (handResult.handLandmarks.Count < 2)
        return false;
      
      // 4. 각 손의 landmarks가 21개 있는지 확인
      for (int i = 0; i < 2; i++)
      {
        var landmarks = handResult.handLandmarks[i].landmarks;
        if (landmarks == null || landmarks.Count < 21)
        {
          return false;
        }
      }
      
      return true;
    }
  }
}