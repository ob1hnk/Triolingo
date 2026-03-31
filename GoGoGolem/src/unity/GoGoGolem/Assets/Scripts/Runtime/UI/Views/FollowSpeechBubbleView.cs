using System;
using TMPro;
using UnityEngine;

/// <summary>
/// 특정 Transform을 따라다니는 말풍선 UI.
/// 씬 루트에 배치하고 followTarget을 지정하면
/// 부모 회전에 영향받지 않고 항상 머리 위 고정 위치에 표시된다.
/// 카메라 거리에 따라 Scale을 동적으로 조절해 항상 일정한 화면 크기를 유지한다.
/// VCam별로 offset/scale을 다르게 설정할 수 있다.
///
/// 세팅:
///   1. SpeechBubbleCanvas를 씬 루트에 배치 (플레이어 자식 X)
///   2. Follow Target → 플레이어 루트 Transform 연결
///   3. Default Offset → 기본 머리 위 높이
///   4. Vcam Overrides → VCam GameObject 연결 + 개별 offset/scale 설정
/// </summary>
public class FollowSpeechBubbleView : MonoBehaviour
{
    [SerializeField] private GameObject bubbleRoot;
    [SerializeField] private TMP_Text textField;

    [Header("Follow")]
    [Tooltip("따라다닐 Transform (플레이어 루트)")]
    [SerializeField] private Transform followTarget;
    [Tooltip("기본 오프셋 (머리 위 높이 등)")]
    [SerializeField] private Vector3 defaultOffset = new Vector3(0f, 2.6f, 0f);

    [Header("Scale by Distance")]
    [Tooltip("기준 카메라 거리 (이 거리일 때 Base Scale 적용)")]
    [SerializeField] private float referenceDistance = 10f;
    [Tooltip("기준 거리에서의 Scale 값")]
    [SerializeField] private float baseScale = 0.01f;

    [Header("VCam Overrides")]
    [Tooltip("VCam별 offset/scale 개별 설정. 활성화된 VCam GameObject 순서로 매칭.")]
    [SerializeField] private VCamOffsetOverride[] vcamOverrides;

    [Serializable]
    public class VCamOffsetOverride
    {
        [Tooltip("Virtual Camera GameObject")]
        public GameObject vcam;
        [Tooltip("이 VCam 활성화 시 적용할 오프셋")]
        public Vector3 offset = new Vector3(0f, 2.6f, 0f);
        [Tooltip("이 VCam 활성화 시 적용할 Base Scale (0이면 기본값 사용)")]
        public float baseScaleOverride = 0f;
    }

    private Camera _mainCamera;

    private void Awake()
    {
        _mainCamera = Camera.main;
        Hide();
    }

    private void LateUpdate()
    {
        _mainCamera = Camera.main;
        if (_mainCamera == null) return;

        // 현재 활성 VCam에 맞는 override 찾기
        Vector3 currentOffset = defaultOffset;
        float currentBaseScale = baseScale;

        if (vcamOverrides != null)
        {
            foreach (var o in vcamOverrides)
            {
                if (o.vcam != null && o.vcam.activeInHierarchy)
                {
                    currentOffset = o.offset;
                    if (o.baseScaleOverride > 0f)
                        currentBaseScale = o.baseScaleOverride;
                    break;
                }
            }
        }

        if (followTarget != null)
            transform.position = followTarget.position + currentOffset;

        // 빌보드
        transform.rotation = _mainCamera.transform.rotation;

        // 거리 기반 Scale
        float distance = Vector3.Distance(transform.position, _mainCamera.transform.position);
        float scale = currentBaseScale * (distance / referenceDistance);
        transform.localScale = Vector3.one * scale;
        // Debug.Log($"distance: {distance}, scale: {scale}");
    }

    public void Show(string text)
    {
        if (textField != null) textField.text = text;
        if (bubbleRoot != null) bubbleRoot.SetActive(true);
    }

    public void Hide()
    {
        if (bubbleRoot != null) bubbleRoot.SetActive(false);
    }
}