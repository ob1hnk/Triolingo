"""
OpenAI Realtime API를 위한 범용 LLM 컴포넌트

WebSocket 기반의 실시간 양방향 통신을 지원합니다.
현재는 방식 A (세션 기반 일괄 처리)를 지원하며,
향후 방식 B (실시간 스트리밍)로 확장 가능합니다.
"""

import asyncio
import base64
import json
import logging
from typing import Any, AsyncGenerator, Callable, Dict, Optional

import websockets
from websockets.client import WebSocketClientProtocol

logger = logging.getLogger(__name__)


class RealtimeLLMComponent:
    """
    OpenAI Realtime API 연결을 관리하는 범용 컴포넌트

    WebSocket 기반의 OpenAI Realtime API와 직접 통신합니다.
    """

    # Realtime API 이벤트 타입
    class EventType:
        # Client -> Server
        SESSION_UPDATE = "session.update"
        INPUT_AUDIO_BUFFER_APPEND = "input_audio_buffer.append"
        INPUT_AUDIO_BUFFER_COMMIT = "input_audio_buffer.commit"
        INPUT_AUDIO_BUFFER_CLEAR = "input_audio_buffer.clear"
        RESPONSE_CREATE = "response.create"
        RESPONSE_CANCEL = "response.cancel"

        # Server -> Client
        SESSION_CREATED = "session.created"
        SESSION_UPDATED = "session.updated"
        INPUT_AUDIO_BUFFER_COMMITTED = "input_audio_buffer.committed"
        INPUT_AUDIO_BUFFER_SPEECH_STARTED = "input_audio_buffer.speech_started"
        INPUT_AUDIO_BUFFER_SPEECH_STOPPED = "input_audio_buffer.speech_stopped"
        CONVERSATION_ITEM_CREATED = "conversation.item.created"
        CONVERSATION_ITEM_TRANSCRIPTION_COMPLETED = (
            "conversation.item.input_audio_transcription.completed"
        )
        RESPONSE_CREATED = "response.created"
        RESPONSE_DONE = "response.done"
        RESPONSE_TEXT_DELTA = "response.text.delta"
        RESPONSE_TEXT_DONE = "response.text.done"
        RESPONSE_AUDIO_DELTA = "response.audio.delta"
        RESPONSE_AUDIO_DONE = "response.audio.done"
        ERROR = "error"

    def __init__(
        self,
        api_key: str,
        base_url: str = "wss://api.openai.com/v1/realtime",
        model: str = "gpt-realtime-mini",
        timeout: float = 60.0,
    ):
        """
        RealtimeLLMComponent 초기화

        Args:
            api_key: OpenAI API 키
            base_url: Realtime API WebSocket URL
            model: 사용할 Realtime 모델명 (gpt-realtime-mini 등)
            timeout: 연결 타임아웃 (초)
        """
        self.api_key = api_key
        self.base_url = base_url
        self.model = model
        self.timeout = timeout
        self.ws: Optional[WebSocketClientProtocol] = None
        self._event_handlers: Dict[str, Callable] = {}

        logger.info(
            f"RealtimeLLMComponent initialized with model: {model} "
            f"base_url: {base_url}"
        )

    async def connect(self) -> None:
        """WebSocket 연결 수립"""
        url = f"{self.base_url}?model={self.model}"

        headers = {
            "Authorization": f"Bearer {self.api_key}",
            "OpenAI-Beta": "realtime=v1",
        }

        try:
            self.ws = await asyncio.wait_for(
                websockets.connect(url, additional_headers=headers),
                timeout=self.timeout,
            )
            logger.info(f"Connected to Realtime API: {url}")

            # 세션 생성 이벤트 대기
            response = await self._receive_event()
            if response.get("type") == self.EventType.SESSION_CREATED:
                logger.info("Session created successfully")
            else:
                logger.warning(f"Unexpected initial event: {response.get('type')}")

        except asyncio.TimeoutError:
            logger.error(f"Connection timeout after {self.timeout}s")
            raise
        except Exception as e:
            logger.error(f"Failed to connect to Realtime API: {e}")
            raise

    async def configure_session(
        self,
        instructions: Optional[str] = None,
        modalities: Optional[list] = None,
        input_audio_format: str = "pcm16",
        output_audio_format: str = "pcm16",
        input_audio_transcription: Optional[Dict[str, Any]] = None,
        turn_detection: Optional[Dict[str, Any]] = None,
        temperature: float = 0.8,
        max_response_output_tokens: Optional[int] = None,
    ) -> Dict[str, Any]:
        """
        세션 설정 업데이트

        Args:
            instructions: 시스템 프롬프트/지시사항
            modalities: 출력 모달리티 ["text"] 또는 ["text", "audio"]
            input_audio_format: 입력 오디오 형식 (pcm16, g711_ulaw, g711_alaw)
            output_audio_format: 출력 오디오 형식
            input_audio_transcription: 입력 오디오 transcription 설정
                예: {"model": "gpt-4o-mini-transcribe", "language": "ko"}
            turn_detection: 턴 감지 설정
                예: {"type": "server_vad", "threshold": 0.5, "silence_duration_ms": 500}
            temperature: 온도 (0.6 ~ 1.2)
            max_response_output_tokens: 최대 출력 토큰 수

        Returns:
            session.updated 이벤트 응답
        """
        if not self.ws:
            raise RuntimeError("WebSocket not connected. Call connect() first.")

        session_config: Dict[str, Any] = {
            "input_audio_format": input_audio_format,
            "output_audio_format": output_audio_format,
            "temperature": temperature,
        }

        if modalities:
            session_config["modalities"] = modalities
        if instructions:
            session_config["instructions"] = instructions
        if input_audio_transcription:
            session_config["input_audio_transcription"] = input_audio_transcription
        if turn_detection:
            session_config["turn_detection"] = turn_detection
        if max_response_output_tokens:
            session_config["max_response_output_tokens"] = max_response_output_tokens

        event = {
            "type": self.EventType.SESSION_UPDATE,
            "session": session_config,
        }

        await self._send_event(event)
        logger.info("Session configuration sent")

        # session.updated 이벤트 대기
        response = await self._receive_event()
        if response.get("type") == self.EventType.SESSION_UPDATED:
            logger.info("Session updated successfully")
        else:
            logger.warning(f"Unexpected response: {response.get('type')}")

        return response

    async def send_audio(self, audio_bytes: bytes) -> None:
        """
        오디오 데이터 전송

        Args:
            audio_bytes: PCM16 형식의 오디오 바이트
        """
        if not self.ws:
            raise RuntimeError("WebSocket not connected")

        audio_base64 = base64.b64encode(audio_bytes).decode("utf-8")

        event = {
            "type": self.EventType.INPUT_AUDIO_BUFFER_APPEND,
            "audio": audio_base64,
        }
        await self._send_event(event)
        logger.debug(f"Audio chunk sent: {len(audio_bytes)} bytes")

    async def commit_audio(self) -> None:
        """오디오 버퍼 커밋 (입력 완료 신호)"""
        if not self.ws:
            raise RuntimeError("WebSocket not connected")

        await self._send_event({"type": self.EventType.INPUT_AUDIO_BUFFER_COMMIT})
        logger.debug("Audio buffer committed")

    async def clear_audio_buffer(self) -> None:
        """오디오 버퍼 초기화"""
        if not self.ws:
            raise RuntimeError("WebSocket not connected")

        await self._send_event({"type": self.EventType.INPUT_AUDIO_BUFFER_CLEAR})
        logger.debug("Audio buffer cleared")

    async def request_response(
        self,
        modalities: Optional[list] = None,
        instructions: Optional[str] = None,
    ) -> None:
        """
        AI 응답 생성 요청

        Args:
            modalities: 응답 모달리티 (예: ["text"] 또는 ["text", "audio"])
            instructions: 이 응답에 대한 추가 지시사항
        """
        if not self.ws:
            raise RuntimeError("WebSocket not connected")

        response_config: Dict[str, Any] = {}
        if modalities:
            response_config["modalities"] = modalities
        if instructions:
            response_config["instructions"] = instructions

        event: Dict[str, Any] = {"type": self.EventType.RESPONSE_CREATE}
        if response_config:
            event["response"] = response_config

        await self._send_event(event)
        logger.info("Response requested")

    async def receive_events(self) -> AsyncGenerator[Dict[str, Any], None]:
        """
        서버로부터 이벤트 스트림 수신

        Yields:
            각 이벤트 딕셔너리
        """
        if not self.ws:
            raise RuntimeError("WebSocket not connected")

        try:
            async for message in self.ws:
                event = json.loads(message)
                event_type = event.get("type", "")
                logger.debug(f"Received event: {event_type}")
                yield event

                # 응답 완료 또는 에러 시 종료
                if event_type in [self.EventType.RESPONSE_DONE, self.EventType.ERROR]:
                    break

        except websockets.exceptions.ConnectionClosed as e:
            logger.warning(f"WebSocket connection closed: {e}")
            raise

    async def generate_response_from_audio(
        self,
        audio_bytes: bytes,
        instructions: Optional[str] = None,
        modalities: Optional[list] = None,
    ) -> Dict[str, Any]:
        """
        오디오로부터 응답 생성 (일괄 처리 방식)

        전체 오디오를 전송하고 응답을 기다리는 방식입니다.
        실시간 스트리밍이 아닌 세션 기반 처리에 적합합니다.

        Args:
            audio_bytes: 오디오 데이터 (PCM16 형식 권장)
            instructions: 추가 지시사항
            modalities: 응답 모달리티 (기본: ["text"])

        Returns:
            {
                "transcript": str,  # 사용자 발화 transcription (있는 경우)
                "response_text": str,  # AI 응답 텍스트
                "response_audio": bytes,  # AI 응답 오디오 (있는 경우)
            }
        """
        result = {
            "transcript": "",
            "response_text": "",
            "response_audio": b"",
        }

        # 오디오 전송 및 커밋
        await self.send_audio(audio_bytes)
        await self.commit_audio()

        # 응답 요청
        await self.request_response(
            modalities=modalities or ["text"],
            instructions=instructions,
        )

        # 응답 수집
        audio_chunks = []

        async for event in self.receive_events():
            event_type = event.get("type", "")

            if event_type == self.EventType.CONVERSATION_ITEM_TRANSCRIPTION_COMPLETED:
                result["transcript"] = event.get("transcript", "")
                logger.info(f"User transcript: {result['transcript'][:50]}...")

            elif event_type == self.EventType.RESPONSE_TEXT_DELTA:
                result["response_text"] += event.get("delta", "")

            elif event_type == self.EventType.RESPONSE_TEXT_DONE:
                result["response_text"] = event.get("text", result["response_text"])

            elif event_type == self.EventType.RESPONSE_AUDIO_DELTA:
                audio_data = event.get("delta", "")
                if audio_data:
                    audio_chunks.append(base64.b64decode(audio_data))

            elif event_type == self.EventType.RESPONSE_AUDIO_DONE:
                pass  # 오디오 완료

            elif event_type == self.EventType.ERROR:
                error_msg = event.get("error", {}).get("message", "Unknown error")
                logger.error(f"Realtime API error: {error_msg}")
                raise RuntimeError(f"Realtime API error: {error_msg}")

        # 오디오 청크 합치기
        if audio_chunks:
            result["response_audio"] = b"".join(audio_chunks)

        logger.info(f"Response generated: {result['response_text'][:50]}...")
        return result

    async def close(self) -> None:
        """WebSocket 연결 종료"""
        if self.ws:
            await self.ws.close()
            self.ws = None
            logger.info("WebSocket connection closed")

    async def _send_event(self, event: Dict[str, Any]) -> None:
        """이벤트 전송"""
        if not self.ws:
            raise RuntimeError("WebSocket not connected")
        await self.ws.send(json.dumps(event))

    async def _receive_event(self) -> Dict[str, Any]:
        """단일 이벤트 수신"""
        if not self.ws:
            raise RuntimeError("WebSocket not connected")
        message = await self.ws.recv()
        return json.loads(message)

    async def __aenter__(self):
        """async context manager 진입"""
        await self.connect()
        return self

    async def __aexit__(self, exc_type, exc_val, exc_tb):
        """async context manager 종료"""
        await self.close()
