from __future__ import annotations

import os
from dataclasses import dataclass

from app.contracts import ExecuteRequest, FallbackModelConfig


class RetriableModelError(Exception):
    """Raised when a model call fails in a way that should trigger fallback."""


SUPPORTED_PROVIDERS = frozenset({"openai", "openai-v1", "openai-compatible"})

DEFAULT_OPENAI_COMPATIBLE_BASE_URL = "http://host.docker.internal:1234/v1"


@dataclass(frozen=True)
class ModelCandidate:
    provider_key: str
    model_id: str
    is_fallback: bool
    trigger_reason: str | None = None


def _normalize_provider_key(provider_key: str) -> str:
    return provider_key.strip().lower()


def _openai_compatible_base_url() -> str:
    return os.environ.get("OPENAI_BASE_URL", DEFAULT_OPENAI_COMPATIBLE_BASE_URL).strip()


def provider_has_api_key(provider_key: str) -> bool:
    normalized = _normalize_provider_key(provider_key)
    if normalized == "openai-compatible":
        return bool(_openai_compatible_base_url()) or bool(
            os.environ.get("OPENAI_API_KEY", "").strip()
        )
    if normalized in {"openai", "openai-v1"}:
        return bool(os.environ.get("OPENAI_API_KEY", "").strip())
    return False


def should_use_deterministic_mock(provider_key: str) -> bool:
    normalized = _normalize_provider_key(provider_key)
    if normalized not in SUPPORTED_PROVIDERS:
        return False
    if normalized == "openai-compatible" and _openai_compatible_base_url():
        return False
    return not provider_has_api_key(provider_key)


def build_model_chain(request: ExecuteRequest) -> list[ModelCandidate]:
    chain: list[ModelCandidate] = [
        ModelCandidate(
            provider_key=request.primary_model_provider_key,
            model_id=request.primary_model_id,
            is_fallback=False,
        )
    ]
    for fallback in request.fallback_models:
        chain.append(_to_candidate(fallback))
    return chain


def _to_candidate(fallback: FallbackModelConfig) -> ModelCandidate:
    return ModelCandidate(
        provider_key=fallback.provider_key,
        model_id=fallback.model_id,
        is_fallback=True,
        trigger_reason=fallback.trigger_reason,
    )


def format_model_used(candidate: ModelCandidate) -> str:
    return f"{candidate.provider_key}:{candidate.model_id}"


def create_pydantic_ai_model(candidate: ModelCandidate):
    normalized = _normalize_provider_key(candidate.provider_key)
    if normalized not in SUPPORTED_PROVIDERS:
        raise RetriableModelError(
            f"Unsupported model provider '{candidate.provider_key}'."
        )

    from pydantic_ai.models.openai import OpenAIChatModel
    from pydantic_ai.providers.openai import OpenAIProvider

    api_key = os.environ.get("OPENAI_API_KEY", "").strip()
    if normalized == "openai-compatible":
        base_url = _openai_compatible_base_url()
        provider = OpenAIProvider(
            base_url=base_url,
            api_key=api_key or "lm-studio",
        )
        return OpenAIChatModel(candidate.model_id, provider=provider)

    provider = OpenAIProvider(api_key=api_key or None)
    return OpenAIChatModel(candidate.model_id, provider=provider)
