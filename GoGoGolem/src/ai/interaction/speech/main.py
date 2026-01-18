import asyncio
import argparse
import logging
from pathlib import Path
from interaction.core.di.config import CoreConfig
from dotenv import load_dotenv
from interaction.speech.di.container import SpeechContainer

# logger info 레벨로 설정
logging.basicConfig(
    level=logging.INFO, format="%(asctime)s - %(name)s - %(levelname)s - %(message)s"
)
logger = logging.getLogger(__name__)


def init_container() -> SpeechContainer:
    """컨테이너를 초기화합니다."""
    # 1. 환경 변수 로드
    load_dotenv(override=True)

    # 2. Core 설정 로드
    core_config = CoreConfig()

    # 3. SpeechContainer 생성
    container = SpeechContainer()

    # 4. Core container 설정 주입 (중요: model_router가 제대로 초기화되려면 필요)
    container.core_container().config.from_pydantic(core_config)

    return container


async def run_speech_to_text_process(
    container: SpeechContainer,
    audio_file_path: str,
    language: str = "ko",
):
    """음성을 텍스트로 변환하고 응답을 생성하는 프로세스를 실행합니다."""
    # UseCase 가져오기
    usecase = container.generate_conversation_response_usecase()

    # 음성 파일 읽기
    audio_path = Path(audio_file_path)
    with audio_path.open("rb") as f:
        result = await usecase.execute(f, language=language)

    # 결과 출력
    logger.info("Process completed successfully")
    print(f"Transcription: {result.get('transcription')}")
    print(f"Response: {result.get('response')}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Run speech-to-text process")
    parser.add_argument(
        "--audio-file", required=True, help="Path to audio file (e.g. speech.wav)"
    )
    parser.add_argument("--language", default="ko", help="Language code (default: ko)")
    args = parser.parse_args()

    # 컨테이너 초기화
    main_container = init_container()

    # 프로세스 실행
    asyncio.run(
        run_speech_to_text_process(
            main_container,
            args.audio_file,
            args.language,
        )
    )
