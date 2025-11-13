def detect_provider(model: str) -> str:
    """
    모델명에서 LLM provider를 추출.
    - openai: openai/gpt, azure-openai
    - 기타: other
    """
    m = (model or "").lower().replace(" ", "")

    # OpenAI (gpt, openai, o4, gpt-4o, gpt-4, gpt-3.5)
    openai_keywords = [
        "openai",
        "gpt-",
        "gpt_",
        "gpt3",
        "gpt4",
        "gpt-4o",
        "o4",
        "gpt-5",
    ]
    if any(k in m for k in openai_keywords):
        return "openai"

    # 기타
    return "other"
