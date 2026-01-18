.PHONY: lint fix run

# 서버 실행
run:
	uv run uvicorn interaction.server.app:app --host 0.0.0.0 --port 8000 --reload

# 코드 문법 체크
lint:
	uv run ruff check interaction/

# 코드 자동 수정
fix:
	uv run ruff check interaction/ --fix
	uv run ruff format interaction/