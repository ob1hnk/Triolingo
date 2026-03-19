using UnityEngine;
using Yarn.Unity;

namespace Demo.Chapters.Prologue
{
    /// <summary>
    /// Forest 씬 전용 Yarn 커맨드 모음 (Glue 코드)
    ///
    /// Yarn 파일에서 호출 가능한 커맨드:
    ///   <<forest_choose_push>>  → 밀기 선택
    ///   <<forest_choose_lift>>  → 들기 선택
    ///
    /// DialogueRunner와 같은 GameObject에 부착
    /// </summary>
    public class ForestDialogueCommands : MonoBehaviour
    {
        // ForestEventController가 자신을 등록해줌
        private ForestEventController _controller;

        public void Register(ForestEventController controller)
        {
            _controller = controller;
        }

        [YarnCommand("forest_choose_push")]
        public void OnChoosePush()
        {
            if (_controller == null)
            {
                Debug.LogError("[ForestDialogueCommands] ForestEventController가 등록되지 않았습니다.");
                return;
            }
            _controller.OnChoicePush();
        }

        [YarnCommand("forest_choose_lift")]
        public void OnChooseLift()
        {
            if (_controller == null)
            {
                Debug.LogError("[ForestDialogueCommands] ForestEventController가 등록되지 않았습니다.");
                return;
            }
            _controller.OnChoiceLift();
        }
    }
}