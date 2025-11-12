.PHONY: lint fix

# 코드 문법 체크
lint:
	uv run ruff check interaction/

# 코드 자동 수정
fix:
	uv run ruff check interaction/ --fix
	uv run ruff format interaction/