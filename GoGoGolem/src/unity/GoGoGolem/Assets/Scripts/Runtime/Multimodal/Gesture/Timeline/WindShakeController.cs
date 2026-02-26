using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.Playables;

namespace Demo.GestureDetection
{
    public class WindShakeController : MonoBehaviour
    {
        [Header("Shake Settings")]
        [SerializeField] private float _shakeSpeed = 3f;
        [SerializeField] private float _shakeAmount = 5f;
        [SerializeField] private float _fadeSpeed = 2f;

        private Quaternion _originRotation;
        private float _currentIntensity = 0f;
        private bool _isShaking = false;

        private void Start()
        {
            _originRotation = transform.localRotation;
        }

        private void Update()
        {
            float targetIntensity = _isShaking ? 1f : 0f;
            _currentIntensity = Mathf.MoveTowards(
                _currentIntensity, targetIntensity, _fadeSpeed * Time.deltaTime);

            if (_currentIntensity > 0f)
            {
                float angle = Mathf.Sin(Time.time * _shakeSpeed) * _shakeAmount * _currentIntensity;
                transform.localRotation = _originRotation * Quaternion.Euler(0f, 0f, angle);
            }
            else
            {
                transform.localRotation = _originRotation;
            }
        }

        public void StartShake()
        {
            Debug.Log("StartShake called!");
            _isShaking = true;
        }

        public void StopShake()
        {
            Debug.Log("StopShake called!");
            _isShaking = false;
        }
    }
}