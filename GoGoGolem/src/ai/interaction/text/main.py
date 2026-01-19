"""
Text 모듈 메인 엔트리포인트

편지 응답 생성 프로세스를 실행하는 데모/테스트용 스크립트입니다.
"""

import asyncio
import argparse
import logging

from dotenv import load_dotenv

from interaction.core.di.config import CoreConfig
from interaction.text.di.container import TextContainer

# logger info 레벨로 설정
logging.basicConfig(
    level=logging.INFO, format="%(asctime)s - %(name)s - %(levelname)s - %(message)s"
)
logger = logging.getLogger(__name__)


def init_container() -> TextContainer:
    """컨테이너를 초기화합니다."""
    # 1. 환경 변수 로드
    load_dotenv(override=True)

    # 2. Core 설정 로드
    core_config = CoreConfig()

    # 3. TextContainer 생성
    container = TextContainer()

    # 4. Core container 설정 주입
    container.core_container().config.from_pydantic(core_config)

    return container


async def generate_letter_response(
    container: TextContainer,
    user_id: str,
    user_letter: str,
):
    """
    편지 응답 생성 프로세스를 실행합니다.

    Args:
        container: TextContainer 인스턴스
        user_id: 사용자 ID
        user_letter: 사용자가 작성한 편지 내용
    """
    # UseCase 가져오기
    usecase = container.generate_letter_response_usecase()

    # 입력 데이터 생성
    input_data = {
        "user_id": user_id,
        "user_letter": user_letter,
    }

    # 실행
    result = await usecase.execute(input_data)

    # 결과 출력
    logger.info("Letter response generated successfully")
    print("User Letter:")
    print(result["user_letter"])
    print("Parent Response:")
    print(result["generated_response_letter"])
    print(f"Letter ID: {result['letter_id']}")

    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Generate letter response")
    parser.add_argument("--user-id", default="demo_user_001", help="User ID")
    parser.add_argument("--letter", help="Letter content (for generate mode)")
    args = parser.parse_args()

    # 컨테이너 초기화
    main_container = init_container()

    asyncio.run(generate_letter_response(main_container, args.user_id, args.letter))
