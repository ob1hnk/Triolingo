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
    [SerializeField] private float _cameraZoomSpeed = 2f;
    [SerializeField] private float _cameraMinFOV = 20f;  // 줌 인 (더 가까이)
    [SerializeField] private float _cameraMaxFOV = 60f;  // 줌 아웃 (더 멀리)
    
    private Vector3 velocity = Vector3.zero;
    private float _currentFOV;
    
    private void Awake()
    {
    
        if (_virtualCamera != null)
        {

            // Initialize with current FOV
            _currentFOV = _virtualCamera.Lens.FieldOfView;
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
            _virtualCamera.Lens.FieldOfView = Mathf.Lerp(currentFOV, _currentFOV, _cameraZoomSpeed * Time.deltaTime);
        }
    }
    
    private void HandleCameraZoom()
    {
        if (_virtualCamera == null)
        {
            return;
        }
        
        float scrollInput = Input.mouseScrollDelta.y;
        
        if (Mathf.Abs(scrollInput) > 0.01f)
        {
            // Scroll up = zoom in (lower FOV), Scroll down = zoom out (higher FOV)
            _currentFOV -= scrollInput * _cameraZoomSpeed;
            _currentFOV = Mathf.Clamp(_currentFOV, _cameraMinFOV, _cameraMaxFOV);
        }
    }
}