using UnityEngine;

namespace Demo.GestureDetection
{
  [CreateAssetMenu(fileName = "GestureSceneConfig", menuName = "Gesture/Scene Config")]
  public class GestureSceneConfig : ScriptableObject
  {
    [Header("이 씬에서 연습할 제스처")]
    public GestureType targetGesture;
    
    // [Header("스킬 정보")]
    // public SkillData skillData; // 이펙트, 오브젝트 등
    
    [Header("인식 임계값")]
    public GestureThresholdData thresholds;
  }
}

