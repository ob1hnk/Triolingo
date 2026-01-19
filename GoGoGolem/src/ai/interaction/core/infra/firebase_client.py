"""
Firebase 클라이언트

Firebase Firestore에 접근하기 위한 클라이언트입니다.
"""

import logging
from typing import Any, Dict, Optional

import firebase_admin
from firebase_admin import credentials, firestore
from google.cloud.firestore import AsyncClient

logger = logging.getLogger(__name__)


class FirebaseClient:
    """Firebase 클라이언트 - Firestore 접근을 위한 래퍼"""

    _instance: Optional["FirebaseClient"] = None
    _initialized: bool = False

    def __init__(
        self,
        credentials_path: Optional[str] = None,
    ):
        """
        Firebase 클라이언트 초기화

        Args:
            credentials_path: Firebase 서비스 계정 키 파일 경로
        """
        self.credentials_path = credentials_path
        self._db: Optional[AsyncClient] = None

        self._initialize()

    def _initialize(self) -> None:
        """Firebase Admin SDK 초기화"""
        if FirebaseClient._initialized:
            logger.info("Firebase already initialized, reusing existing app")
            self._db = firestore.client()
            return

        try:
            if self.credentials_path:
                cred = credentials.Certificate(self.credentials_path)
                options = {}

                firebase_admin.initialize_app(cred, options)
                logger.info(
                    f"Firebase initialized with credentials from: {self.credentials_path}"
                )
            else:
                # 환경 변수에서 자동으로 인증 정보를 가져오는 경우
                firebase_admin.initialize_app()
                logger.info("Firebase initialized with default credentials")

            self._db = firestore.client()
            FirebaseClient._initialized = True

        except Exception as e:
            logger.error(f"Failed to initialize Firebase: {e}")
            raise

    @property
    def db(self):
        """Firestore 클라이언트 반환"""
        if self._db is None:
            raise RuntimeError("Firebase client not initialized")
        return self._db

    async def get_document(
        self, collection: str, document_id: str
    ) -> Optional[Dict[str, Any]]:
        """
        문서 조회

        Args:
            collection: 컬렉션 이름
            document_id: 문서 ID

        Returns:
            문서 데이터 또는 None
        """
        try:
            doc_ref = self.db.collection(collection).document(document_id)
            doc = doc_ref.get()
            if doc.exists:
                return doc.to_dict()
            return None
        except Exception as e:
            logger.error(f"Error getting document {collection}/{document_id}: {e}")
            raise

    async def set_document(
        self,
        collection: str,
        document_id: str,
        data: Dict[str, Any],
        merge: bool = False,
    ) -> None:
        """
        문서 저장/업데이트

        Args:
            collection: 컬렉션 이름
            document_id: 문서 ID
            data: 저장할 데이터
            merge: True면 기존 데이터와 병합, False면 덮어쓰기
        """
        try:
            doc_ref = self.db.collection(collection).document(document_id)
            doc_ref.set(data, merge=merge)
            logger.info(f"Document saved: {collection}/{document_id}")
        except Exception as e:
            logger.error(f"Error setting document {collection}/{document_id}: {e}")
            raise

    async def add_document(
        self,
        collection: str,
        data: Dict[str, Any],
    ) -> str:
        """
        새 문서 추가 (자동 ID 생성)

        Args:
            collection: 컬렉션 이름
            data: 저장할 데이터

        Returns:
            생성된 문서 ID
        """
        try:
            doc_ref = self.db.collection(collection).add(data)
            document_id = doc_ref[1].id
            logger.info(f"Document added: {collection}/{document_id}")
            return document_id
        except Exception as e:
            logger.error(f"Error adding document to {collection}: {e}")
            raise

    async def update_document(
        self,
        collection: str,
        document_id: str,
        data: Dict[str, Any],
    ) -> None:
        """
        문서 업데이트 (기존 필드 유지)

        Args:
            collection: 컬렉션 이름
            document_id: 문서 ID
            data: 업데이트할 데이터
        """
        try:
            doc_ref = self.db.collection(collection).document(document_id)
            doc_ref.update(data)
            logger.info(f"Document updated: {collection}/{document_id}")
        except Exception as e:
            logger.error(f"Error updating document {collection}/{document_id}: {e}")
            raise

    async def delete_document(self, collection: str, document_id: str) -> None:
        """
        문서 삭제

        Args:
            collection: 컬렉션 이름
            document_id: 문서 ID
        """
        try:
            doc_ref = self.db.collection(collection).document(document_id)
            doc_ref.delete()
            logger.info(f"Document deleted: {collection}/{document_id}")
        except Exception as e:
            logger.error(f"Error deleting document {collection}/{document_id}: {e}")
            raise


def create_firebase_client(credentials_path: Optional[str] = None) -> FirebaseClient:
    """
    Firebase 클라이언트 팩토리 함수

    Args:
        credentials_path: Firebase 서비스 계정 키 파일 경로

    Returns:
        FirebaseClient 인스턴스
    """
    return FirebaseClient(credentials_path=credentials_path)
