"""Thin entrypoint kept for existing startup commands such as `uvicorn Backend.main:app`."""

from Backend.app_factory import create_app

app = create_app()
