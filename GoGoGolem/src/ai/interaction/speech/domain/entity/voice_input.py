"""
음성 입력 엔티티
"""

from dataclasses import dataclass
from typing import BinaryIO
import io


@dataclass
class VoiceInput:
    """
    음성 입력 엔티티

    사용자의 음성 입력 데이터를 bytes 또는 BinaryIO 형태로 보관합니다.
    """

    data: bytes
    format: str = "wav"
    sample_rate: int = 16000
    channels: int = 1
    name: str | None = None

    @classmethod
    def from_bytes(
        cls,
        data: bytes,
        format: str = "wav",
        sample_rate: int = 16000,
        channels: int = 1,
        name: str | None = None,
    ) -> "VoiceInput":
        """bytes로부터 VoiceInput 생성"""
        return cls(
            data=data,
            format=format,
            sample_rate=sample_rate,
            channels=channels,
            name=name,
        )

    @classmethod
    def from_binary_io(
        cls,
        binary_io: BinaryIO,
        format: str = "wav",
        sample_rate: int = 16000,
        channels: int = 1,
        name: str | None = None,
    ) -> "VoiceInput":
        """BinaryIO로부터 VoiceInput 생성"""
        if hasattr(binary_io, "tell") and hasattr(binary_io, "seek"):
            current_pos = binary_io.tell()
            binary_io.seek(0)
            data = binary_io.read()
            binary_io.seek(current_pos)
        else:
            data = binary_io.read()

        file_name = name or getattr(binary_io, "name", None)

        return cls(
            data=data,
            format=format,
            sample_rate=sample_rate,
            channels=channels,
            name=file_name,
        )

    def to_binary_io(self) -> BinaryIO:
        """BinaryIO로 변환"""
        binary_io = io.BytesIO(self.data)
        if self.name:
            binary_io.name = self.name
        return binary_io

    def __len__(self) -> int:
        """음성 데이터 크기 (bytes)"""
        return len(self.data)
