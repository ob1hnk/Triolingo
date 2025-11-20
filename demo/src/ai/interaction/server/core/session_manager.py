"""
세션별 오디오 청크를 관리하는 SessionManager
"""

import logging
import io
import wave
from typing import Dict

logger = logging.getLogger(__name__)


class SessionManager:
    """세션별 오디오 청크를 관리하는 클래스"""

    def __init__(self):
        self.sessions: Dict[str, Dict] = {}

    def create_session(
        self,
        session_id: str,
        audio_format: str = "wav",
        sample_rate: int = 16000,
        channels: int = 1,
    ):
        """새 세션 생성"""
        self.sessions[session_id] = {
            "chunks": [],
            "audio_format": audio_format,
            "sample_rate": sample_rate,
            "channels": channels,
            "complete": False,
        }
        logger.info(
            f"Session created: {session_id} (format: {audio_format}, "
            f"sample_rate: {sample_rate}Hz, channels: {channels})"
        )

    def add_chunk(self, session_id: str, chunk_index: int, audio_data: bytes):
        """세션에 오디오 청크 추가"""
        if session_id not in self.sessions:
            raise ValueError(f"Session not found: {session_id}")

        session = self.sessions[session_id]
        # chunk_index 순서대로 정렬하여 저장
        session["chunks"].append((chunk_index, audio_data))
        logger.debug(f"Chunk {chunk_index} added to session {session_id}")

    def _merge_pcm_chunks(
        self,
        chunks: list[tuple[int, bytes]],
        sample_rate: int,
        channels: int,
        sampwidth: int = 2,  # 16-bit = 2 bytes
    ) -> bytes:
        """
        PCM 데이터 청크들을 합쳐서 WAV 파일로 변환

        Args:
            chunks: (chunk_index, audio_data) 튜플 리스트
            sample_rate: 샘플 레이트 (Hz)
            channels: 채널 수 (1=mono, 2=stereo)
            sampwidth: 샘플 너비 (바이트, 기본값: 2 = 16-bit)

        Returns:
            합쳐진 WAV 파일의 bytes
        """
        if not chunks:
            raise ValueError("No chunks to merge")

        # chunk_index 순서대로 정렬
        sorted_chunks = sorted(chunks, key=lambda x: x[0])

        # 모든 PCM 데이터 합치기
        all_pcm_data = b"".join([chunk[1] for chunk in sorted_chunks])

        # WAV 파일 생성
        output = io.BytesIO()
        with wave.open(output, "wb") as out_wav:
            out_wav.setnchannels(channels)
            out_wav.setsampwidth(sampwidth)
            out_wav.setframerate(sample_rate)
            out_wav.setcomptype("NONE", "not compressed")
            out_wav.writeframes(all_pcm_data)

        output.seek(0)
        result = output.read()
        logger.info(
            f"Merged {len(sorted_chunks)} PCM chunks into WAV file ({len(result)} bytes). "
            f"Total PCM data: {len(all_pcm_data)} bytes, "
            f"Duration: ~{len(all_pcm_data) / (sample_rate * sampwidth * channels):.2f}s"
        )
        return result

    def _merge_wav_chunks(self, chunks: list[tuple[int, bytes]]) -> bytes:
        """
        WAV 파일 청크들을 올바르게 합침

        각 청크가 완전한 WAV 파일인 경우, 헤더를 제거하고
        PCM 데이터만 합친 다음 새로운 WAV 파일을 생성합니다.

        Args:
            chunks: (chunk_index, audio_data) 튜플 리스트

        Returns:
            합쳐진 WAV 파일의 bytes
        """
        if not chunks:
            raise ValueError("No chunks to merge")

        # chunk_index 순서대로 정렬
        sorted_chunks = sorted(chunks, key=lambda x: x[0])

        # 첫 번째 청크로부터 WAV 파라미터 추출
        first_chunk_data = sorted_chunks[0][1]
        first_chunk_io = io.BytesIO(first_chunk_data)

        try:
            with wave.open(first_chunk_io, "rb") as first_wav:
                # 첫 번째 파일의 파라미터 가져오기
                nchannels = first_wav.getnchannels()
                sampwidth = first_wav.getsampwidth()
                framerate = first_wav.getframerate()
                comptype = first_wav.getcomptype()
                compname = first_wav.getcompname()

                # 모든 청크의 PCM 데이터 수집
                all_frames = []

                for chunk_index, chunk_data in sorted_chunks:
                    chunk_io = io.BytesIO(chunk_data)
                    try:
                        with wave.open(chunk_io, "rb") as wav_file:
                            # 파라미터 일치 확인
                            if (
                                wav_file.getnchannels() != nchannels
                                or wav_file.getsampwidth() != sampwidth
                                or wav_file.getframerate() != framerate
                            ):
                                logger.warning(
                                    f"Chunk {chunk_index} has different audio parameters. "
                                    f"Expected: {nchannels}ch/{sampwidth}byte/{framerate}Hz, "
                                    f"Got: {wav_file.getnchannels()}ch/{wav_file.getsampwidth()}byte/{wav_file.getframerate()}Hz"
                                )

                            # PCM 데이터 읽기
                            frames = wav_file.readframes(wav_file.getnframes())
                            all_frames.append(frames)
                            logger.debug(
                                f"Chunk {chunk_index}: {len(frames)} bytes of PCM data"
                            )
                    except wave.Error as e:
                        logger.error(
                            f"Failed to read chunk {chunk_index} as WAV: {e}. "
                            f"Chunk size: {len(chunk_data)} bytes"
                        )
                        raise ValueError(
                            f"Invalid WAV chunk at index {chunk_index}: {e}"
                        ) from e

                # 모든 PCM 데이터 합치기
                combined_frames = b"".join(all_frames)

                # 새로운 WAV 파일 생성
                output = io.BytesIO()
                with wave.open(output, "wb") as out_wav:
                    out_wav.setnchannels(nchannels)
                    out_wav.setsampwidth(sampwidth)
                    out_wav.setframerate(framerate)
                    out_wav.setcomptype(comptype, compname)
                    out_wav.writeframes(combined_frames)

                output.seek(0)
                result = output.read()
                logger.info(
                    f"Merged {len(sorted_chunks)} WAV chunks into {len(result)} bytes. "
                    f"Total PCM data: {len(combined_frames)} bytes, "
                    f"Duration: ~{len(combined_frames) / (framerate * sampwidth * nchannels):.2f}s"
                )
                return result

        except wave.Error as e:
            logger.error(f"Failed to process WAV chunks: {e}")
            raise ValueError(
                f"Failed to process WAV chunks: {e}. "
                f"Chunks may be PCM data without WAV headers."
            ) from e

    def get_audio(self, session_id: str) -> bytes:
        """
        세션의 모든 청크를 합쳐서 하나의 오디오 파일로 반환

        WAV 포맷의 경우:
        1. 먼저 WAV 파일로 파싱 시도 (완전한 WAV 파일인 경우)
        2. 실패하면 PCM 데이터로 간주하고 WAV 헤더 추가
        """
        if session_id not in self.sessions:
            raise ValueError(f"Session not found: {session_id}")

        session = self.sessions[session_id]
        audio_format = session.get("audio_format", "wav").lower()
        chunks = session["chunks"]
        sample_rate = session.get("sample_rate", 16000)
        channels = session.get("channels", 1)

        if not chunks:
            raise ValueError(f"No audio chunks found for session {session_id}")

        # WAV 포맷인 경우
        if audio_format == "wav":
            # 먼저 WAV 파일로 파싱 시도
            try:
                return self._merge_wav_chunks(chunks)
            except (wave.Error, ValueError):
                # WAV 파싱 실패 시 PCM 데이터로 간주
                logger.info(
                    f"Chunks are not WAV files, treating as PCM data. "
                    f"Converting to WAV with sample_rate={sample_rate}Hz, channels={channels}"
                )
                try:
                    return self._merge_pcm_chunks(chunks, sample_rate, channels)
                except Exception as pcm_error:
                    logger.error(
                        f"Failed to merge PCM chunks for session {session_id}: {pcm_error}",
                        exc_info=True,
                    )
                    raise ValueError(
                        f"Failed to merge audio chunks: {pcm_error}. "
                        f"Please ensure all chunks are valid WAV files or PCM data."
                    ) from pcm_error
        else:
            # 다른 포맷의 경우 단순 합치기 (향후 필요시 확장 가능)
            sorted_chunks = sorted(chunks, key=lambda x: x[0])
            audio_bytes = b"".join([chunk[1] for chunk in sorted_chunks])
            logger.warning(
                f"Audio format '{audio_format}' is not fully supported. "
                f"Using simple byte concatenation which may not work correctly."
            )
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
