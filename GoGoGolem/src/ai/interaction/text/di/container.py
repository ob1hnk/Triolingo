"""
Text 모듈의 의존성 주입 컨테이너
"""

from dependency_injector import containers, providers

from interaction.core.di.container import CoreContainer
from interaction.text.components.text_to_text.llm_text_to_text_v1 import LLMTextToTextV1
from interaction.text.repository.firebase.letter_response import (
    FirebaseLetterResponseRepository,
)
from interaction.text.domain.usecases.generate_letter_response import (
    GenerateLetterResponseUseCase,
)


class TextContainer(containers.DeclarativeContainer):
    """Text 모듈의 의존성 주입 컨테이너"""

    # Core container
    core_container: CoreContainer = providers.Container(
        CoreContainer,
    )

    config = providers.Configuration()

    # Port Implementations
    text_to_text = providers.Singleton(
        LLMTextToTextV1,
        router=core_container.model_router,
    )

    # Repository Implementations
    letter_repository = providers.Singleton(
        FirebaseLetterResponseRepository,
        firebase_client=core_container.firebase_client,
    )

    # UseCases
    generate_letter_response_usecase = providers.Singleton(
        GenerateLetterResponseUseCase,
        text_to_text=text_to_text,
        letter_repository=letter_repository,
    )
