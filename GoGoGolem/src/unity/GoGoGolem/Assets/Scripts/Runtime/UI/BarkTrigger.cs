using System;
using UnityEngine;
using UI.Presenters;

/// <summary>
/// Inspector에서 대사 시퀀스를 설정하고 다양한 방식으로 Bark를 발화시키는 헬퍼.
///
/// 사용 시나리오:
///   1. 트리거 볼륨: BoxCollider(isTrigger=true) + 이 컴포넌트에 triggerOnEnter=true
///   2. 타임라인 Signal Receiver: Fire() 메소드를 시그널로 호출
///   3. 다른 스크립트/UnityEvent에서 Fire() 직접 호출
/// </summary>
[DisallowMultipleComponent]
public class BarkTrigger : MonoBehaviour
{
    [Header("Content")]
    [Tooltip("순차적으로 출력할 대사 목록")]
    [SerializeField] private BarkLine[] lines;

    [Header("Behavior")]
    [Tooltip("한 번만 발화할지 여부")]
    [SerializeField] private bool triggerOnce = true;

    [Header("Trigger Volume (선택)")]
    [Tooltip("OnTriggerEnter에서 자동 발화")]
    [SerializeField] private bool triggerOnEnter = false;
    [SerializeField] private string playerTag = "Player";

    private bool _fired;

    /// <summary>Bark 발화. 시그널 리시버/UnityEvent/코드에서 호출 가능.</summary>
    public void Fire() => Fire(null);

    /// <summary>
    /// Bark 발화 + 완료 콜백. 발화가 스킵되는 경우(이미 발화됨/Presenter 없음/lines 비어있음)에도
    /// onComplete가 즉시 호출되어 호출측 흐름이 이어지도록 보장한다.
    /// </summary>
    public void Fire(Action onComplete)
    {
        if (triggerOnce && _fired)
        {
            onComplete?.Invoke();
            return;
        }

        if (BarkPresenter.Instance == null)
        {
            Debug.LogWarning("[BarkTrigger] BarkPresenter.Instance가 없습니다.");
            onComplete?.Invoke();
            return;
        }

        if (lines == null || lines.Length == 0)
        {
            Debug.LogWarning($"[BarkTrigger] '{name}'에 lines가 비어있습니다.");
            onComplete?.Invoke();
            return;
        }

        BarkPresenter.Instance.Bark(lines, onComplete);
        _fired = true;
    }

    /// <summary>발화 이력 초기화 (다시 발화 가능 상태로)</summary>
    public void ResetTrigger() => _fired = false;

    private void OnTriggerEnter(Collider other)
    {
        if (!triggerOnEnter) return;
        if (!string.IsNullOrEmpty(playerTag) && !other.CompareTag(playerTag)) return;
        Fire();
    }
}