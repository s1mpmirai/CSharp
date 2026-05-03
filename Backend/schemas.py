"""Pydantic request schemas separated from route handlers for clarity."""

from __future__ import annotations

from typing import Optional

from pydantic import BaseModel


class LoginRequest(BaseModel):
    username: str
    password: str


class CreateOwnerRequest(BaseModel):
    username: str
    password: str
    full_name: str
    email: Optional[str] = None


class UpdateUserRequest(BaseModel):
    username: str
    full_name: str
    email: Optional[str] = None
    password: Optional[str] = None


class CreateAdminNotificationRequest(BaseModel):
    title: str
    message: str
    recipient_scope: str = "selected_users"
    user_ids: list[int] = []


class UserLocation(BaseModel):
    lat: float
    lng: float


class SearchRequest(BaseModel):
    query: str
    lat: Optional[float] = None
    lng: Optional[float] = None
    limit: int = 20


class RatingRequest(BaseModel):
    rating: int
    lat: Optional[float] = None
    lng: Optional[float] = None
