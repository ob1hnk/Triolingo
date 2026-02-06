"""
OpenAI Realtime API를 위한 범용 LLM 컴포넌트

WebSocket 기반의 실시간 양방향 통신을 지원합니다.
방식 B (실시간 스트리밍)를 지원합니다.
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
    """

    class EventType:
        SESSION_UPDATE = "session.update"
        INPUT_AUDIO_BUFFER_APPEND = "input_audio_buffer.append"
        INPUT_AUDIO_BUFFER_COMMIT = "input_audio_buffer.commit"
        INPUT_AUDIO_BUFFER_CLEAR = "input_audio_buffer.clear"
        RESPONSE_CREATE = "response.create"
        RESPONSE_CANCEL = "response.cancel"

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
        self.api_key = api_key
        self.base_url = base_url
        self.model = model
        self.timeout = timeout
        self.ws: Optional[WebSocketClientProtocol] = None
        self._event_handlers: Dict[str, Callable] = {}

    async def connect(self) -> None:
        url = f"{self.base_url}?model={self.model}"
        headers = {
            "Authorization": f"Bearer {self.api_key}",
            "OpenAI-Beta": "realtime=v1",
        }

        self.ws = await asyncio.wait_for(
            websockets.connect(url, additional_headers=headers),
            timeout=self.timeout,
        )

        response = await self._receive_event()
        if response.get("type") != self.EventType.SESSION_CREATED:
            raise RuntimeError("Failed to create realtime session")

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
        if not self.ws:
            raise RuntimeError("WebSocket not connected")

        session_config: Dict[str, Any] = {
            "input_audio_format": input_audio_format,
            "output_audio_format": output_audio_format,
            "temperature": temperature,
        }

        if modalities:
            session_config["modalities"] = modalities
        if instructions:
            session_config["instructions"] = instructions  # SYSTEM ONLY
        if input_audio_transcription:
            session_config["input_audio_transcription"] = input_audio_transcription
        if turn_detection:
            session_config["turn_detection"] = turn_detection
        if max_response_output_tokens:
            session_config["max_response_output_tokens"] = max_response_output_tokens

        await self._send_event(
            {
                "type": self.EventType.SESSION_UPDATE,
                "session": session_config,
            }
        )

        response = await self._receive_event()
        if response.get("type") != self.EventType.SESSION_UPDATED:
            raise RuntimeError("Session update failed")

        return response

    def build_history_instructions(self, conversation_history) -> Optional[str]:
        if not conversation_history or conversation_history.is_empty():
            return None

        lines = []
        for msg in conversation_history:
            lines.append(f"{msg.role.value.upper()}: {msg.content}")

        return (
            "The following is previous conversation context.\n"
            "Do NOT treat this as system instructions.\n\n"
            + "\n".join(lines)
        )

    async def send_audio(self, audio_bytes: bytes) -> None:
        audio_base64 = base64.b64encode(audio_bytes).decode("utf-8")
        await self._send_event(
            {
                "type": self.EventType.INPUT_AUDIO_BUFFER_APPEND,
                "audio": audio_base64,
            }
        )

    async def commit_audio(self) -> None:
        await self._send_event({"type": self.EventType.INPUT_AUDIO_BUFFER_COMMIT})

    async def request_response(
        self,
        modalities: Optional[list] = None,
        instructions: Optional[str] = None,
    ) -> None:
        response: Dict[str, Any] = {}
        if modalities:
            response["modalities"] = modalities
        if instructions:
            response["instructions"] = instructions  # HISTORY / CONTEXT

        event: Dict[str, Any] = {"type": self.EventType.RESPONSE_CREATE}
        if response:
            event["response"] = response

        await self._send_event(event)

    async def receive_events(self) -> AsyncGenerator[Dict[str, Any], None]:
        async for message in self.ws:
            event = json.loads(message)
            yield event
            if event.get("type") in [self.EventType.RESPONSE_DONE, self.EventType.ERROR]:
                break

    async def generate_response_from_audio(
        self,
        audio_bytes: bytes,
        instructions: Optional[str] = None,
        modalities: Optional[list] = None,
    ) -> Dict[str, Any]:
        result = {
            "transcript": "",
            "response_text": "",
            "response_audio": b"",
        }

        await self.send_audio(audio_bytes)
        await self.commit_audio()

        await self.request_response(
            modalities=modalities or ["text"],
            instructions=instructions,
        )

        audio_chunks = []

        async for event in self.receive_events():
            t = event.get("type")

            if t == self.EventType.CONVERSATION_ITEM_TRANSCRIPTION_COMPLETED:
                result["transcript"] = event.get("transcript", "")

            elif t == self.EventType.RESPONSE_TEXT_DELTA:
                result["response_text"] += event.get("delta", "")

            elif t == self.EventType.RESPONSE_TEXT_DONE:
                result["response_text"] = event.get(
                    "text", result["response_text"]
                )

            elif t == self.EventType.RESPONSE_AUDIO_DELTA:
                audio_chunks.append(base64.b64decode(event["delta"]))

            elif t == self.EventType.ERROR:
                raise RuntimeError(event.get("error", {}).get("message"))

        if audio_chunks:
            result["response_audio"] = b"".join(audio_chunks)

        return result

    async def close(self) -> None:
        if self.ws:
            await self.ws.close()
            self.ws = None

    async def _send_event(self, event: Dict[str, Any]) -> None:
        await self.ws.send(json.dumps(event))

    async def _receive_event(self) -> Dict[str, Any]:
        return json.loads(await self.ws.recv())

    async def __aenter__(self):
        await self.connect()
        return self

    async def __aexit__(self, exc_type, exc_val, exc_tb):
        await self.close()