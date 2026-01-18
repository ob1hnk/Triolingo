from abc import ABC, abstractmethod
from typing import TypeVar, Generic, Optional
from pydantic import BaseModel

InputType = TypeVar("InputType", bound=BaseModel)
OutputType = TypeVar("OutputType")


class BaseUsecase(ABC, Generic[InputType, OutputType]):
    """
    모든 유즈케이스의 기본 추상 클래스

    역할
    - 도메인 유즈케이스들이 가져야 할 공통 실행 규약을 정의
    - 상위 레이어가 유즈케이스를 일관되게 호출할 수 있도록 함
    """

    @abstractmethod
    async def __call__(
        self, input: InputType, *, ctx: Optional[dict] = None
    ) -> OutputType: ...
