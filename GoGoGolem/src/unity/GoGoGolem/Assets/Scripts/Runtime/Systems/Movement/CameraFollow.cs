using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.InputSystem;

public class CameraFollow : MonoBehaviour
{

    public Transform target;
    public float smoothTime = 0.1f;
    public Vector3 offset = new Vector3(0f, 1f, -3f);
    public Vector3 rotation = new Vector3(0f, 0f, 0f);
    
    [SerializeField] private CinemachineCamera _virtualCamera;
    [SerializeField] private float _cameraZoomSpeed = 1f;
    [SerializeField] private float _cameraMinFOV = 20f;  // 줌 인 (더 가까이)
    [SerializeField] private float _cameraMaxFOV = 60f;  // 줌 아웃 (더 멀리)
    
    private Vector3 velocity = Vector3.zero;
    private float _targetFOV;
    
    private void Awake()
    {
    
        if (_virtualCamera != null)
        {

            // Initialize with current FOV
            _targetFOV = _virtualCamera.Lens.FieldOfView;
        }
    }
    
    private void Start()
    {
        // Set initial rotation
        transform.rotation = Quaternion.Euler(rotation);
    }
    
    // Update is called once per frame
    void Update()
    {
        if (target != null)
        {
            Vector3 targetPosition = target.position + offset;
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);
        }
        
        HandleCameraZoom();
    }
    
    private void LateUpdate()
    {
        // Apply FOV changes smoothly
        if (_virtualCamera != null)
        {
            // Use smooth damping for FOV (similar to SmoothDamp)
            float currentFOV = _virtualCamera.Lens.FieldOfView;
            _virtualCamera.Lens.FieldOfView = Mathf.Lerp(currentFOV, _targetFOV, _cameraZoomSpeed * Time.deltaTime);
        }
    }
    
    private void HandleCameraZoom()
    {
        if (_virtualCamera == null) return;
        
        // Zoom based on mouse scroll wheel
        float scrollInput = Input.mouseScrollDelta.y;
        
        if (Mathf.Abs(scrollInput) > 0.01f)
        {
            _targetFOV -= scrollInput * _cameraZoomSpeed;
        }

        // Zoom based on keyboard input
        if (Keyboard.current != null)
        {
            // '=' 키를 누르면 가장 가까운 줌(Min FOV)으로 이동
            if (Keyboard.current.equalsKey.wasPressedThisFrame)
            {
                _targetFOV = _cameraMinFOV;
            }
            // '-' 키를 누르면 가장 먼 줌(Max FOV)으로 이동
            if (Keyboard.current.minusKey.wasPressedThisFrame)
            {
                _targetFOV = _cameraMaxFOV;
            }
        }

        // 3. 최종 값 제한 (Clamp)
        _targetFOV = Mathf.Clamp(_targetFOV, _cameraMinFOV, _cameraMaxFOV);
    }
}