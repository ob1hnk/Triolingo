.PHONY: lint fix

lint:
	uv run ruff check interaction/
	uv run ruff check server/

fix:
	uv run ruff check interaction/ --fix
	uv run ruff format interaction/
	uv run ruff check server/ --fix
	uv run ruff format server/
