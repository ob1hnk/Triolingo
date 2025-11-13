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
from interaction.speech.domain.usecases.generate_conversation_response import (
    GenerateConversationResponseUseCase,
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

    # UseCases
    generate_conversation_response_usecase = providers.Singleton(
        GenerateConversationResponseUseCase,
        speech_to_text=speech_to_text,
        text_to_text=text_to_text,
    )
