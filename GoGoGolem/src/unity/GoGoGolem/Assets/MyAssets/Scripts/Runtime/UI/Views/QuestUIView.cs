using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using MyAssets.Runtime.Data.Quest;

public class QuestUIView : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private RectTransform collapsedPanel;
    [SerializeField] private RectTransform expandedPanel;
    
    [Header("Collapsed Panel Elements")]
    [SerializeField] private Image questIcon;
    [SerializeField] private GameObject newIndicator;
    [SerializeField] private Button collapsedPanelButton;
    
    [Header("Expanded Panel Elements")]
    [SerializeField] private Button collapseIconButton;
    [SerializeField] private ScrollRect questScrollView;
    [SerializeField] private Transform questContentParent;
    
    [Header("Prefabs")]
    [SerializeField] private GameObject questItemPrefab;
    
    [Header("Animation Settings")]
    [SerializeField] private float animationDuration = 0.3f;
    // [SerializeField] private Ease slideEase = Ease.OutQuad;
    
    // Collapsed/Expanded 위치
    private Vector2 collapsedPosition = new Vector2(20, -20);
    private Vector2 expandedPosition = new Vector2(20, -20);
    
    // 상태
    private bool isExpanded = false;
    private Dictionary<string, QuestItemView> questItemViews = new Dictionary<string, QuestItemView>();
    
    // 이벤트
    public event Action OnExpandRequested;
    public event Action OnCollapseRequested;
    
    private void Awake()
    {
        // 초기 상태 설정
        collapsedPanel.gameObject.SetActive(true);
        expandedPanel.gameObject.SetActive(false);
        newIndicator.SetActive(false);
        
        // 버튼 이벤트 등록
        collapsedPanelButton.onClick.AddListener(() => OnExpandRequested?.Invoke());
        collapseIconButton.onClick.AddListener(() => OnCollapseRequested?.Invoke());
    }
    
    private void Update()
    {
        // Tab 키 입력 처리
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (isExpanded)
                OnCollapseRequested?.Invoke();
            else
                OnExpandRequested?.Invoke();
        }
    }
    
    /// <summary>
    /// 퀘스트 창 펼치기
    /// </summary>
    public void Expand()
    {
        if (isExpanded) return;
        
        isExpanded = true;
        
        // New Indicator 숨기기
        newIndicator.SetActive(false);
        
        // Collapsed Panel 숨기고 Expanded Panel 표시
        collapsedPanel.gameObject.SetActive(false);
        expandedPanel.gameObject.SetActive(true);
        
        // 슬라이드 애니메이션 (왼쪽에서 들어옴)
        expandedPanel.anchoredPosition = expandedPosition + Vector2.right * 370; // 패널 너비만큼 오른쪽에서 시작
        //expandedPanel.DOAnchorPos(expandedPosition, animationDuration).SetEase(slideEase);
    }
    
    /// <summary>
    /// 퀘스트 창 접기
    /// </summary>
    public void Collapse()
    {
        if (!isExpanded) return;
        
        isExpanded = false;
        
        // 슬라이드 애니메이션 (오른쪽으로 나감)
        // expandedPanel.DOAnchorPos(expandedPosition + Vector2.right * 370, animationDuration)
        //     .SetEase(slideEase)
        //     .OnComplete(() =>
        //     {
        //         expandedPanel.gameObject.SetActive(false);
        //         collapsedPanel.gameObject.SetActive(true);
        //     });
    }
    
    /// <summary>
    /// New Indicator 표시
    /// </summary>
    public void ShowNewIndicator()
    {
        if (!isExpanded)
        {
            newIndicator.SetActive(true);
        }
    }
    
    /// <summary>
    /// New Indicator 숨기기
    /// </summary>
    public void HideNewIndicator()
    {
        newIndicator.SetActive(false);
    }
    
    /// <summary>
    /// 퀘스트 아이템 추가
    /// </summary>
        public void AddQuestItem(string questId, QuestType questType, string questName, bool isMain)
    {
        if (questItemViews.ContainsKey(questId))
        {
            Debug.LogWarning($"Quest {questId} already exists in UI.");
            return;
        }
        
        GameObject questItemObj = Instantiate(questItemPrefab, questContentParent);
        QuestItemView questItemView = questItemObj.GetComponent<QuestItemView>();
        
        if (questItemView != null)
        {
            questItemView.Initialize(questId, questType, questName);
            questItemViews.Add(questId, questItemView);
            
            // 메인 퀘스트를 상단에 배치
            if (questType == QuestType.MainQuest) // QuestType.Main → QuestType.MainQuest
            {
                questItemView.transform.SetAsFirstSibling();
            }
            
            // 스크롤을 상단으로 (새 퀘스트가 보이도록)
            Canvas.ForceUpdateCanvases();
            questScrollView.verticalNormalizedPosition = 1f;
        }
    }
    
    /// <summary>
    /// 퀘스트 아이템 제거
    /// </summary>
    public void RemoveQuestItem(string questId)
    {
        if (questItemViews.TryGetValue(questId, out QuestItemView questItemView))
        {
            Destroy(questItemView.gameObject);
            questItemViews.Remove(questId);
        }
    }
    
    /// <summary>
    /// 목표 추가
    /// </summary>
    public void AddObjective(string questId, string objectiveId, string objectiveText)
    {
        if (questItemViews.TryGetValue(questId, out QuestItemView questItemView))
        {
            questItemView.AddObjective(objectiveId, objectiveText);
        }
    }
    
    /// <summary>
    /// 목표 완료 처리
    /// </summary>
    public void CompleteObjective(string questId, string objectiveId)
    {
        if (questItemViews.TryGetValue(questId, out QuestItemView questItemView))
        {
            questItemView.CompleteObjective(objectiveId);
        }
    }
    
    /// <summary>
    /// 퀘스트 제거 (완료 시)
    /// </summary>
    public void RemoveQuest(string questId)
    {
        RemoveQuestItem(questId);
    }
    
    /// <summary>
    /// 모든 퀘스트 클리어
    /// </summary>
    public void ClearAllQuests()
    {
        foreach (var questItemView in questItemViews.Values)
        {
            if (questItemView != null)
                Destroy(questItemView.gameObject);
        }
        questItemViews.Clear();
    }
    
    private void OnDestroy()
    {
        // 이벤트 해제
        collapsedPanelButton.onClick.RemoveAllListeners();
        collapseIconButton.onClick.RemoveAllListeners();
        
        // DOTween 정리
        // DOTween.Kill(expandedPanel);
    }
}