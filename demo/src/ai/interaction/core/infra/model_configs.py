from typing import Dict, List, Optional


class ModelConfigs:
    """모델별 설정과 폴백 정보를 관리하는 클래스"""

    # OpenAI LLM 모델들
    OPENAI_MODELS = [
        "gpt-5",
        "gpt-5-mini",
        "gpt-4.1",
        "gpt-4.1-mini",
        "gpt-4o",
        "gpt-4o-2024-05-13",
        "gpt-4o-mini",
        "gpt-4o-audio-preview",  # Speech-to-Speech 모델 (audio input, text/audio output)
    ]

    # OpenAI Speech-to-Text 모델들
    SPEECH_MODELS = [
        "whisper",
        # "whisper-tiny", # huggingface only supported
        "gpt-4o-transcribe",
        "gpt-4o-mini-transcribe",
        "gpt-4o-transcribe-diarize",
    ]

    # Speech-to-Text 응답 형식
    SPEECH_RESPONSE_FORMATS = [
        "json",
        "text",
        "srt",
        "verbose_json",
        "vtt",
        "diarized_json",
    ]

    # Speech 모델명과 실제 API 모델명 매핑
    SPEECH_MODEL_MAPPING = {
        "whisper": "whisper-1",
        "gpt-4o-transcribe": "openai/gpt-4o-transcribe",
        "gpt-4o-mini-transcribe": "openai/gpt-4o-mini-transcribe",
        "gpt-4o-transcribe-diarize": "openai/gpt-4o-transcribe-diarize",
    }

    # 모델별 상세 설정
    MODEL_CONFIGS = {
        # TODO: 각 공급사별 docs에서 pricing 확인 필요
        # OpenAI 모델 설정
        "gpt-5": {
            "max_tokens": 32768,
            "fallbacks": ["gpt-5-mini", "gpt-4o"],
            "cost_per_token": 0.02,
            "temperature": 1,  # only temperature-1 is supported
            "provider": "openai",
        },
        "gpt-5-mini": {
            "max_tokens": 16384,
            "fallbacks": ["gpt-4o"],
            "cost_per_token": 0.005,
            "temperature": 1,  # only temperature-1 is supported
            "provider": "openai",
        },
        "gpt-4.1": {
            "max_tokens": 32768,
            "fallbacks": ["gpt-4.1-mini", "gpt-4o"],
            "cost_per_token": 0.01,
            "provider": "openai",
        },
        "gpt-4.1-mini": {
            "max_tokens": 16384,
            "fallbacks": ["gpt-4o"],
            "cost_per_token": 0.003,
            "provider": "openai",
        },
        "gpt-4o": {
            "max_tokens": 4096,
            "fallbacks": ["gpt-4o-mini"],
            "cost_per_token": 0.005,
            "provider": "openai",
        },
        "gpt-4o-2024-05-13": {
            "max_tokens": 4096,
            "fallbacks": ["gpt-4o-mini"],
            "cost_per_token": 0.005,
            "provider": "openai",
        },
        "gpt-4o-mini": {
            "max_tokens": 16384,
            "fallbacks": [],
            "cost_per_token": 0.00015,
            "provider": "openai",
        },
        "whisper": {
            "max_tokens": 4096,
            "fallbacks": [],
            "cost_per_token": 0.00015,
            "provider": "openai",
        },
        # Speech-to-Speech 모델 (audio input → text/audio output)
        "gpt-4o-audio-preview": {
            "max_tokens": 16384,
            "fallbacks": ["gpt-4o"],  # audio 지원 안되면 일반 모델로 폴백
            "cost_per_token": 0.005,  # TODO: 실제 pricing 확인 필요
            "provider": "openai",
        },
    }

    @classmethod
    def get_model_config(cls, model_name: str) -> Optional[Dict]:
        """모델 설정을 반환합니다."""
        return cls.MODEL_CONFIGS.get(model_name)

    @classmethod
    def get_fallbacks(cls, model_name: str) -> List[str]:
        """모델의 폴백 리스트를 반환합니다."""
        config = cls.get_model_config(model_name)
        return config.get("fallbacks", []) if config else []
