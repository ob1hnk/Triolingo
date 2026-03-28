using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 골렘 NavMesh 이동 제어 (재사용 가능 컴포넌트)
///
/// 역할:
///   - 평소: 플레이어를 NavMesh로 추적
///   - 이벤트 시: 추적 중단 후 지정 위치로 이동 (콜백 지원)
///
/// Inspector 세팅:
///   - Player:           추적할 플레이어 Transform
///   - Follow Distance:  플레이어와 유지할 거리
///   - Walk Param:       Animator Bool 파라미터명 (isWalking)
///
/// 사용 예시:
///   _golemFollow.StopFollowing();
///   _golemFollow.MoveToPoint(_golemStartPoint, OnGolemArrived);
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class GolemFollow : MonoBehaviour
{
    [Header("Following")]
    [Tooltip("추적할 플레이어 Transform")]
    [SerializeField] private Transform _player;
    [Tooltip("플레이어와 유지할 거리 (이 거리 안에 들어오면 정지)")]
    [SerializeField] private float _followDistance = 1.5f;
    [Tooltip("목적지 도착 판정 거리 (MoveToPoint용)")]
    [SerializeField] private float _arrivalDistance = 0.2f;

    [Header("Animation")]
    [Tooltip("Animator Bool 파라미터명")]
    [SerializeField] private string _walkParam = "isWalking";
    [SerializeField] private Animator _animator;

    // ─────────────────────────────────────────────
    // 내부 상태
    // ─────────────────────────────────────────────
    private NavMeshAgent _agent;
    private bool _isFollowing = false;
    private Coroutine _moveToPointCoroutine;

    // =============================================
    // Unity 생명주기
    // =============================================

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        if (_player == null)
            Debug.LogWarning("[GolemFollow] Player가 연결되지 않았습니다!");
        else
            StartFollowing();
    }

    private void Update()
    {
        if (!_isFollowing) return;
        FollowPlayer();
    }

    // =============================================
    // Public API
    // =============================================

    /// <summary>플레이어 추적 시작</summary>
    public void StartFollowing()
    {
        if (_player == null)
        {
            Debug.LogWarning("[GolemFollow] Player가 없어서 추적을 시작할 수 없습니다.");
            return;
        }

        StopMoveToPoint();
        _isFollowing = true;
        _agent.isStopped = false;
    }

    /// <summary>플레이어 추적 중단</summary>
    public void StopFollowing()
    {
        _isFollowing = false;
        _agent.isStopped = true;
        _agent.ResetPath();
        SetWalkAnimation(false);
    }

    /// <summary>
    /// 지정 위치로 NavMesh 이동. 도착 시 onArrived 콜백 호출.
    /// </summary>
    public void MoveToPoint(Transform target, Action onArrived = null)
    {
        if (target == null)
        {
            Debug.LogWarning("[GolemFollow] MoveToPoint: target이 null입니다.");
            onArrived?.Invoke();
            return;
        }

        StopMoveToPoint();
        _isFollowing = false;
        _agent.isStopped = false;
        _moveToPointCoroutine = StartCoroutine(MoveToPointRoutine(target, onArrived));
    }

    /// <summary>즉시 정지 (NavMesh 경로 초기화)</summary>
    public void ForceStop()
    {
        StopMoveToPoint();
        _isFollowing = false;
        _agent.isStopped = true;
        _agent.ResetPath();
        SetWalkAnimation(false);
    }

    /// <summary>
    /// Timeline 재생 전 호출 — NavMesh Agent 비활성화
    /// Agent가 켜져 있으면 Timeline의 Transform 제어와 충돌함
    /// </summary>
    public void DisableAgent()
    {
        StopMoveToPoint();
        _isFollowing = false;
        _agent.enabled = false;
        SetWalkAnimation(false);
    }

    /// <summary>
    /// Timeline 종료 후 호출 — NavMesh Agent 재활성화
    /// </summary>
    public void EnableAgent()
    {
        _agent.enabled = true;
    }

    // =============================================
    // 내부 로직
    // =============================================

    private void FollowPlayer()
    {
        float distToPlayer = Vector3.Distance(transform.position, _player.position);

        if (distToPlayer > _followDistance)
        {
            _agent.isStopped = false;
            _agent.SetDestination(_player.position);
            SetWalkAnimation(true);
        }
        else
        {
            _agent.isStopped = true;
            _agent.ResetPath();
            SetWalkAnimation(false);
        }
    }

    private IEnumerator MoveToPointRoutine(Transform target, Action onArrived)
    {
        _agent.SetDestination(target.position);
        SetWalkAnimation(true);

        // 경로 계산 대기 (1프레임)
        yield return null;

        while (true)
        {
            if (_agent.pathPending)
            {
                yield return null;
                continue;
            }

            float distToTarget = Vector3.Distance(transform.position, target.position);
            if (distToTarget <= _arrivalDistance)
                break;

            if (_agent.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                Debug.LogWarning("[GolemFollow] NavMesh 경로를 찾을 수 없습니다.");
                break;
            }

            yield return null;
        }

        _agent.isStopped = true;
        _agent.ResetPath();
        SetWalkAnimation(false);

        // 목표 방향으로 회전 맞추기
        Vector3 dir = target.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir);

        _moveToPointCoroutine = null;
        onArrived?.Invoke();
    }

    private void StopMoveToPoint()
    {
        if (_moveToPointCoroutine != null)
        {
            StopCoroutine(_moveToPointCoroutine);
            _moveToPointCoroutine = null;
        }
    }

    private void SetWalkAnimation(bool isWalking)
    {
        if (_animator == null) return;
        _animator.SetBool(_walkParam, isWalking);
    }

    // =============================================
    // Editor 디버그
    // =============================================

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_player == null) return;
        Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
        Gizmos.DrawWireSphere(_player.position, _followDistance);
    }
#endif
}