using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 개인정보처리방침 동의 팝업 Presenter.
/// 4개 필수 동의 항목을 모두 체크해야 확인 버튼이 활성화된다.
/// 동의 상태는 PlayerPrefs에 저장되어 이후 실행 시 다시 묻지 않는다.
/// </summary>
public class PrivacyConsentPresenter : MonoBehaviour
{
    private const string PrefKeyConsented = "Privacy_Consented";

    [Header("Panel")]
    [SerializeField] private GameObject panel;

    [Header("Checkboxes (필수 동의)")]
    [SerializeField] private Toggle checkAll;
    [SerializeField] private Toggle checkCollection;     // 개인정보 수집·이용
    [SerializeField] private Toggle checkSensitive;      // 민감정보(음성·영상)
    [SerializeField] private Toggle checkOverseas;        // 국외 이전
    [SerializeField] private Toggle checkGuardian;        // 법정대리인 동의

    [Header("Buttons")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button closeButton;

    [Header("Policy Text (ScrollRect 안의 TMP)")]
    [SerializeField] private TMP_Text policyText;

    private Toggle[] _requiredToggles;
    private bool _updatingAll;

    public event System.Action OnConsented;

    /// <summary>
    /// 이전에 동의한 적이 있는지 확인.
    /// </summary>
    public static bool HasConsented => PlayerPrefs.GetInt(PrefKeyConsented, 0) == 1;

    private void Awake()
    {
        _requiredToggles = new[] { checkCollection, checkSensitive, checkOverseas, checkGuardian };

        if (panel != null) panel.SetActive(false);

        if (policyText != null)
            policyText.text = GetPolicyContent();
    }

    private void OnEnable()
    {
        if (checkAll != null)
            checkAll.onValueChanged.AddListener(OnCheckAllChanged);

        foreach (var t in _requiredToggles)
        {
            if (t != null)
                t.onValueChanged.AddListener(OnIndividualChanged);
        }

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);

        UpdateConfirmButton();
    }

    private void OnDisable()
    {
        if (checkAll != null)
            checkAll.onValueChanged.RemoveListener(OnCheckAllChanged);

        foreach (var t in _requiredToggles)
        {
            if (t != null)
                t.onValueChanged.RemoveListener(OnIndividualChanged);
        }

        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(OnConfirm);

        if (closeButton != null)
            closeButton.onClick.RemoveListener(Hide);
    }

    public void Show()
    {
        // 모든 체크 해제 상태로 초기화
        _updatingAll = true;
        if (checkAll != null) checkAll.isOn = false;
        foreach (var t in _requiredToggles)
        {
            if (t != null) t.isOn = false;
        }
        _updatingAll = false;

        UpdateConfirmButton();
        if (panel != null) panel.SetActive(true);
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }

    // ─── 전체 동의 ───

    private void OnCheckAllChanged(bool isOn)
    {
        if (_updatingAll) return;

        _updatingAll = true;
        foreach (var t in _requiredToggles)
        {
            if (t != null) t.isOn = isOn;
        }
        _updatingAll = false;

        UpdateConfirmButton();
    }

    // ─── 개별 체크 변경 ───

    private void OnIndividualChanged(bool _)
    {
        if (_updatingAll) return;

        bool allOn = true;
        foreach (var t in _requiredToggles)
        {
            if (t != null && !t.isOn) { allOn = false; break; }
        }

        _updatingAll = true;
        if (checkAll != null) checkAll.isOn = allOn;
        _updatingAll = false;

        UpdateConfirmButton();
    }

    // ─── 확인 버튼 활성화 ───

    private void UpdateConfirmButton()
    {
        if (confirmButton == null) return;

        bool allChecked = true;
        foreach (var t in _requiredToggles)
        {
            if (t != null && !t.isOn) { allChecked = false; break; }
        }

        confirmButton.interactable = allChecked;
    }

    // ─── 동의 확인 ───

    private void OnConfirm()
    {
        PlayerPrefs.SetInt(PrefKeyConsented, 1);
        PlayerPrefs.Save();
        Hide();
        OnConsented?.Invoke();
    }

    // ─── 약관 본문 ───

    private static string GetPolicyContent()
    {
        return @"<size=28><b>GoGo Golem! 개인정보처리방침</b></size>

<size=22><b>1. 수집하는 개인정보 항목</b></size>

<b>필수 수집 항목:</b>
 - 음성 데이터: 마이크를 통한 음성 입력 (AI 대화에 사용)
 - 영상/신체 데이터: 웹캠을 통한 동작 인식 (모션 인식에 사용)
 - 텍스트 데이터: 편지 등 자유 서술 입력
 - 사용자 식별정보: 이름
 - 행동 로그: 퀘스트 진행, 상호작용 기록

<b>민감정보 고지:</b> 음성 데이터는 「개인정보 보호법」상 바이오 인증정보(민감정보)에 해당하며, 웹캠 영상/동작 데이터는 개인식별정보로 보호됩니다.

<size=22><b>2. 수집 및 이용 목적</b></size>

 - AI 캐릭터와의 실시간 음성 대화 제공
 - 동작 인식 기반 게임 인터랙션
 - 편지 작성 및 AI 답장 생성
 - 게임 진행 상태 저장 및 관리
 - 서비스 품질 개선

<size=22><b>3. 보관 기간 및 파기</b></size>

 - 게임 이용 기간 중 보관하며, 목적 달성 또는 이용 종료 시 지체 없이 파기합니다.
 - 보관 기간: 서비스 이용 종료일로부터 30일 이내 파기
 - 파기 방법: 전자적 파일은 복구 불가능한 방법으로 삭제

<size=22><b>4. 법정대리인(부모/보호자) 동의</b></size>

본 서비스는 만 14세 미만 아동(6~12세)을 대상으로 하므로, 「개인정보 보호법」 제22조 및 미국 COPPA에 따라 법정대리인의 동의가 반드시 필요합니다.

 - 게임 최초 실행 시 법정대리인 동의 절차를 진행합니다.
 - 법정대리인은 아동의 개인정보 열람, 정정, 삭제, 처리 중단을 요청할 수 있습니다.

<size=22><b>5. 개인정보의 국외 이전</b></size>

 - 이전받는 자: OpenAI (미국)
 - 이전 목적: 음성 인식 및 AI 대화 생성
 - 이전 항목: 음성 데이터, 텍스트 데이터
 - 이전 국가: 미국
 - 보관 기간: API 처리 즉시 후 미보관 (OpenAI API 정책에 따름)

<size=22><b>6. 음성 및 영상 처리 고지</b></size>

 - 마이크: 음성은 실시간으로 수집되어 AI 서버로 전송, 처리됩니다.
 - 웹캠: 동작 인식을 위해 영상을 수집하며, 모션 데이터는 로컬에서 처리·저장됩니다. 원본 영상은 서버로 전송되지 않습니다.
 - 음성·영상 데이터는 민감정보로서 별도 동의를 받아 처리합니다.

<size=22><b>7. 정보주체의 권리</b></size>

이용자(또는 법정대리인)는 다음 권리를 행사할 수 있습니다.

 - 개인정보 열람 요청
 - 개인정보 정정·삭제 요청
 - 개인정보 처리 중단 요청
 - 동의 철회

<size=22><b>8. 안전성 확보 조치</b></size>

 - API 키는 서버에서만 관리하며 클라이언트에 노출하지 않습니다.
 - AI 출력에 유해 콘텐츠 필터를 적용합니다.
 - 데이터 전송 시 암호화(HTTPS)를 적용합니다.";
    }
}
