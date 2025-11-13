"""
세션별 오디오 청크를 관리하는 SessionManager
"""

import logging
from typing import Dict

logger = logging.getLogger(__name__)


class SessionManager:
    """세션별 오디오 청크를 관리하는 클래스"""

    def __init__(self):
        self.sessions: Dict[str, Dict] = {}

    def create_session(self, session_id: str, audio_format: str = "wav"):
        """새 세션 생성"""
        self.sessions[session_id] = {
            "chunks": [],
            "audio_format": audio_format,
            "complete": False,
        }
        logger.info(f"Session created: {session_id}")

    def add_chunk(self, session_id: str, chunk_index: int, audio_data: bytes):
        """세션에 오디오 청크 추가"""
        if session_id not in self.sessions:
            raise ValueError(f"Session not found: {session_id}")

        session = self.sessions[session_id]
        # chunk_index 순서대로 정렬하여 저장
        session["chunks"].append((chunk_index, audio_data))
        logger.debug(f"Chunk {chunk_index} added to session {session_id}")

    def get_audio(self, session_id: str) -> bytes:
        """세션의 모든 청크를 합쳐서 하나의 오디오 파일로 반환"""
        if session_id not in self.sessions:
            raise ValueError(f"Session not found: {session_id}")

        session = self.sessions[session_id]
        # chunk_index 순서대로 정렬
        sorted_chunks = sorted(session["chunks"], key=lambda x: x[0])
        # 모든 청크를 합침
        audio_bytes = b"".join([chunk[1] for chunk in sorted_chunks])
        return audio_bytes

    def mark_complete(self, session_id: str):
        """세션 완료 표시"""
        if session_id in self.sessions:
            self.sessions[session_id]["complete"] = True

    def remove_session(self, session_id: str):
        """세션 제거"""
        if session_id in self.sessions:
            del self.sessions[session_id]
            logger.info(f"Session removed: {session_id}")

    def has_session(self, session_id: str) -> bool:
        """세션 존재 여부 확인"""
        return session_id in self.sessions


# 전역 세션 매니저 인스턴스
session_manager = SessionManager()
