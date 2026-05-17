"""
OpenAI Realtime API (GA) 를 위한 범용 LLM 컴포넌트

WebSocket 기반의 실시간 양방향 통신을 지원합니다.
GA Realtime API 스펙 기준으로 작성되었습니다.
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
    OpenAI Realtime API (GA) 연결을 관리하는 범용 컴포넌트
    """

    class EventType:
        # 클라이언트 → 서버
        SESSION_UPDATE = "session.update"
        INPUT_AUDIO_BUFFER_APPEND = "input_audio_buffer.append"
        INPUT_AUDIO_BUFFER_COMMIT = "input_audio_buffer.commit"
        INPUT_AUDIO_BUFFER_CLEAR = "input_audio_buffer.clear"
        RESPONSE_CREATE = "response.create"
        RESPONSE_CANCEL = "response.cancel"

        # 서버 → 클라이언트
        SESSION_CREATED = "session.created"
        SESSION_UPDATED = "session.updated"
        CONVERSATION_CREATED = "conversation.created"
        INPUT_AUDIO_BUFFER_COMMITTED = "input_audio_buffer.committed"
        INPUT_AUDIO_BUFFER_SPEECH_STARTED = "input_audio_buffer.speech_started"
        INPUT_AUDIO_BUFFER_SPEECH_STOPPED = "input_audio_buffer.speech_stopped"
        # GA API: conversation.item.added (Beta: conversation.item.created)
        CONVERSATION_ITEM_ADDED = "conversation.item.added"
        CONVERSATION_ITEM_CREATED = "conversation.item.created"
        CONVERSATION_ITEM_TRANSCRIPTION_COMPLETED = (
            "conversation.item.input_audio_transcription.completed"
        )
        RESPONSE_CREATED = "response.created"
        RESPONSE_DONE = "response.done"
        # GA API 이벤트 이름 변경: response.text.* → response.output_text.*
        RESPONSE_TEXT_DELTA = "response.output_text.delta"
        RESPONSE_TEXT_DONE = "response.output_text.done"
        # GA API 이벤트 이름 변경: response.audio.* → response.output_audio.*
        RESPONSE_AUDIO_DELTA = "response.output_audio.delta"
        RESPONSE_AUDIO_DONE = "response.output_audio.done"
        RESPONSE_FUNCTION_CALL_ARGUMENTS_DELTA = "response.function_call_arguments.delta"
        RESPONSE_FUNCTION_CALL_ARGUMENTS_DONE = "response.function_call_arguments.done"
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
        }

        self.ws = await asyncio.wait_for(
            websockets.connect(url, additional_headers=headers),
            timeout=self.timeout,
        )

        # GA API는 session.created 후 conversation.created 등 추가 이벤트를 보낼 수 있음
        # session.created 이벤트가 올 때까지 대기
        while True:
            response = await self._receive_event()
            event_type = response.get("type")
            if event_type == self.EventType.SESSION_CREATED:
                return
            if event_type == self.EventType.ERROR:
                logger.error(f"OpenAI error during connect: {response}")
                raise RuntimeError(
                    f"Failed to create realtime session: {response.get('error', {}).get('message', 'unknown')}"
                )
            logger.debug(f"Skipping event during connect: {event_type}")

    async def configure_session(
        self,
        instructions: Optional[str] = None,
        input_audio_transcription: Optional[Dict[str, Any]] = None,
        turn_detection: Optional[Dict[str, Any]] = None,
        tools: Optional[list] = None,
        tool_choice: Optional[str] = None,
    ) -> Dict[str, Any]:
        """
        GA Realtime API session.update 이벤트 전송

        GA API 구조:
        - instructions: session 최상위
        - turn_detection → audio.input.turn_detection
        - input_audio_transcription → audio.input.transcription
        - temperature, modalities, input/output_audio_format: 제거됨
        """
        if not self.ws:
            raise RuntimeError("WebSocket not connected")

        session_config: Dict[str, Any] = {
            "type": "realtime",
            "model": self.model,
        }

        if instructions:
            session_config["instructions"] = instructions

        # GA API: turn_detection과 transcription은 audio.input 하위로 이동
        audio_input: Dict[str, Any] = {}
        if turn_detection:
            audio_input["turn_detection"] = turn_detection
        if input_audio_transcription:
            audio_input["transcription"] = input_audio_transcription
        if audio_input:
            session_config["audio"] = {"input": audio_input}

        if tools:
            session_config["tools"] = tools
        if tool_choice:
            session_config["tool_choice"] = tool_choice

        await self._send_event(
            {
                "type": self.EventType.SESSION_UPDATE,
                "session": session_config,
            }
        )

        while True:
            response = await self._receive_event()
            event_type = response.get("type")
            if event_type == self.EventType.SESSION_UPDATED:
                return response
            if event_type == self.EventType.ERROR:
                error_msg = response.get("error", {}).get("message", "unknown")
                logger.error(f"OpenAI error during session update: {response}")
                raise RuntimeError(f"Session update failed: {error_msg}")
            logger.debug(f"Skipping intermediate event during configure_session: {event_type}")

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
        output_modalities: Optional[list] = None,
    ) -> None:
        event: Dict[str, Any] = {"type": self.EventType.RESPONSE_CREATE}
        if output_modalities:
            event["response"] = {"output_modalities": output_modalities}
        await self._send_event(event)

    async def receive_events(
        self, break_on_response_done: bool = True
    ) -> AsyncGenerator[Dict[str, Any], None]:
        """
        OpenAI Realtime API에서 이벤트를 수신하는 비동기 제너레이터

        Args:
            break_on_response_done: True면 RESPONSE_DONE에서 중단 (단일 응답용)
                                   False면 ERROR에서만 중단 (Server VAD 멀티턴용)
        """
        async for message in self.ws:
            event = json.loads(message)
            yield event
            event_type = event.get("type")
            if event_type == self.EventType.ERROR:
                break
            if break_on_response_done and event_type == self.EventType.RESPONSE_DONE:
                break

    async def generate_response_from_audio(
        self,
        audio_bytes: bytes,
        output_modalities: Optional[list] = None,
    ) -> Dict[str, Any]:
        result = {
            "transcript": "",
            "response_text": "",
            "response_audio": b"",
        }

        await self.send_audio(audio_bytes)
        await self.commit_audio()
        await self.request_response(output_modalities=output_modalities or ["text"])

        audio_chunks = []

        async for event in self.receive_events():
            t = event.get("type")

            if t == self.EventType.CONVERSATION_ITEM_TRANSCRIPTION_COMPLETED:
                result["transcript"] = event.get("transcript", "")

            elif t == self.EventType.RESPONSE_TEXT_DELTA:
                result["response_text"] += event.get("delta", "")

            elif t == self.EventType.RESPONSE_TEXT_DONE:
                result["response_text"] = event.get("text", result["response_text"])

            elif t == self.EventType.RESPONSE_AUDIO_DELTA:
                audio_chunks.append(base64.b64decode(event["delta"]))

            elif t == self.EventType.ERROR:
                error_data = event.get("error", {})
                error_type = error_data.get("type", "unknown_error")
                error_code = error_data.get("code", "unknown")
                error_msg = error_data.get("message", "Unknown error")
                raise RuntimeError(f"[{error_type}:{error_code}] {error_msg}")

        if audio_chunks:
            result["response_audio"] = b"".join(audio_chunks)

        return result

    async def send_function_call_output(self, call_id: str, output: str) -> None:
        await self._send_event({
            "type": "conversation.item.create",
            "item": {
                "type": "function_call_output",
                "call_id": call_id,
                "output": output,
            },
        })

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
