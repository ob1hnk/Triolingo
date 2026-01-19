"""
Firebase Letter Response Repository 구현

Firebase Firestore를 사용하여 편지 응답을 저장하고 조회합니다.
"""

import logging
from datetime import datetime
from typing import List, Optional

from interaction.core.infra.firebase_client import FirebaseClient
from GoGoGolem.src.ai.interaction.text.domain.entity.letter import Letter
from interaction.text.domain.repository.letter_response import (
    LetterResponseRepositoryPort,
)

logger = logging.getLogger(__name__)

COLLECTION_NAME = "letter_responses"


class FirebaseLetterResponseRepository(LetterResponseRepositoryPort):
    """Firebase를 사용한 Letter Response Repository 구현"""

    def __init__(self, firebase_client: FirebaseClient):
        """
        초기화

        Args:
            firebase_client: Firebase 클라이언트 인스턴스
        """
        self.firebase_client = firebase_client
        logger.info("FirebaseLetterResponseRepository initialized")

    async def save(self, letter_response: Letter) -> str:
        """
        편지 응답 저장

        Args:
            letter_response: 저장할 편지 응답 데이터

        Returns:
            저장된 문서의 ID
        """
        try:
            now = datetime.utcnow()
            letter_response.created_at = now
            letter_response.updated_at = now

            data = letter_response.to_dict()
            # ID는 Firebase에서 자동 생성되므로 제거
            data.pop("id", None)

            document_id = await self.firebase_client.add_document(
                collection=COLLECTION_NAME,
                data=data,
            )

            logger.info(f"Letter response saved with ID: {document_id}")
            return document_id

        except Exception as e:
            logger.error(f"Error saving letter response: {e}", exc_info=True)
            raise

    async def get_by_id(self, letter_id: str) -> Optional[Letter]:
        """
        ID로 편지 응답 조회

        Args:
            letter_id: 편지 응답 ID

        Returns:
            편지 응답 데이터 또는 None
        """
        try:
            data = await self.firebase_client.get_document(
                collection=COLLECTION_NAME,
                document_id=letter_id,
            )

            if data is None:
                logger.info(f"Letter response not found: {letter_id}")
                return None

            data["id"] = letter_id
            return Letter.from_dict(data)

        except Exception as e:
            logger.error(f"Error getting letter response {letter_id}: {e}", exc_info=True)
            raise

    async def get_by_user_id(self, user_id: str) -> List[Letter]:
        """
        사용자 ID로 편지 응답 목록 조회

        Args:
            user_id: 사용자 ID

        Returns:
            편지 응답 목록
        """
        try:
            db = self.firebase_client.db
            docs = (
                db.collection(COLLECTION_NAME)
                .where("user_id", "==", user_id)
                .order_by("created_at", direction="DESCENDING")
                .stream()
            )

            results = []
            for doc in docs:
                data = doc.to_dict()
                data["id"] = doc.id
                results.append(Letter.from_dict(data))

            logger.info(f"Found {len(results)} letter responses for user: {user_id}")
            return results

        except Exception as e:
            logger.error(
                f"Error getting letter responses for user {user_id}: {e}", exc_info=True
            )
            raise

    async def update(self, letter_id: str, letter_response: Letter) -> None:
        """
        편지 응답 업데이트

        Args:
            letter_id: 편지 응답 ID
            letter_response: 업데이트할 데이터
        """
        try:
            letter_response.updated_at = datetime.utcnow()
            data = letter_response.to_dict()
            # ID와 created_at은 업데이트하지 않음
            data.pop("id", None)
            data.pop("created_at", None)

            await self.firebase_client.update_document(
                collection=COLLECTION_NAME,
                document_id=letter_id,
                data=data,
            )

            logger.info(f"Letter response updated: {letter_id}")

        except Exception as e:
            logger.error(f"Error updating letter response {letter_id}: {e}", exc_info=True)
            raise

    async def delete(self, letter_id: str) -> None:
        """
        편지 응답 삭제

        Args:
            letter_id: 편지 응답 ID
        """
        try:
            await self.firebase_client.delete_document(
                collection=COLLECTION_NAME,
                document_id=letter_id,
            )

            logger.info(f"Letter response deleted: {letter_id}")

        except Exception as e:
            logger.error(f"Error deleting letter response {letter_id}: {e}", exc_info=True)
            raise
