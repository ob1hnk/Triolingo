"""
Speech 모듈의 의존성 주입 컨테이너
"""

from dependency_injector import containers, providers

from interaction.core.di.container import CoreContainer
from interaction.speech.components.speech_to_text.llm_speech_to_text_v1 import (
    LLMSpeechToTextV1,
)
from interaction.speech.components.text_to_text.llm_text_to_text_v1 import (
    LLMTextToTextV1,
)
from interaction.speech.components.speech_to_speech.llm_speech_to_speech_v1 import (
    LLMSpeechToSpeechV1,
)
from interaction.speech.domain.usecases.generate_conversation_response import (
    GenerateConversationResponseUseCase,
)
from interaction.speech.domain.usecases.generate_conversation_response_v2 import (
    GenerateConversationResponseUseCaseV2,
)


class SpeechContainer(containers.DeclarativeContainer):
    """Speech 모듈의 의존성 주입 컨테이너"""

    # Core container
    core_container: CoreContainer = providers.Container(
        CoreContainer,
    )

    config = providers.Configuration()

    # Port Implementations
    speech_to_text = providers.Singleton(
        LLMSpeechToTextV1,
        router=core_container.model_router,
    )

    text_to_text = providers.Singleton(
        LLMTextToTextV1,
        router=core_container.model_router,
    )

    # Speech-to-Speech (v2 - 단일 API 호출로 응답 생성)
    speech_to_speech = providers.Singleton(
        LLMSpeechToSpeechV1,
        router=core_container.model_router,
    )

    # UseCases
    # v1: 2단계 파이프라인 (음성→텍스트→응답)
    generate_conversation_response_usecase = providers.Singleton(
        GenerateConversationResponseUseCase,
        speech_to_text=speech_to_text,
        text_to_text=text_to_text,
    )

    # v2: 단일 단계 (음성→응답) - 레이턴시 감소
    generate_conversation_response_usecase_v2 = providers.Singleton(
        GenerateConversationResponseUseCaseV2,
        speech_to_speech=speech_to_speech,
    )
