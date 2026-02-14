using UnityEngine;

namespace Demo.GestureDetection.Tutorial
{
    /// <summary>
    /// 처음 배우는 스킬인지 기록 관리 (PlayerPrefs)
    /// - GestureSceneController에서 Timeline 재생 여부 판단에만 사용
    /// </summary>
    public class GestureTutorialManager : MonoBehaviour
    {
        private const string KEY_PREFIX = "SkillLearned_";

        public bool IsFirstTime(GestureType gestureType)
        {
            return !PlayerPrefs.HasKey(KEY_PREFIX + gestureType);
        }

        public void MarkAsLearned(GestureType gestureType)
        {
            PlayerPrefs.SetInt(KEY_PREFIX + gestureType, 1);
            PlayerPrefs.Save();
            Debug.Log($"[TutorialManager] Marked as learned: {gestureType}");
        }

        [ContextMenu("Reset All Learned Records")]
        public void ResetAllLearned()
        {
            foreach (GestureType type in System.Enum.GetValues(typeof(GestureType)))
                PlayerPrefs.DeleteKey(KEY_PREFIX + type);

            PlayerPrefs.Save();
            Debug.Log("[TutorialManager] All records reset");
        }
    }
}