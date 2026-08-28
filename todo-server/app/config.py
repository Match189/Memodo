"""应用配置（SPD §15：密钥只从环境变量读取，严禁入库）。"""
from functools import lru_cache

from pydantic_settings import BaseSettings


class Settings(BaseSettings):
    # 生产必须显式提供；开发/测试可用默认值（compose 里注入）
    secret_key: str = "dev-only-secret-change-me"
    database_url: str = "sqlite+aiosqlite:///./todo-server.db"
    access_token_minutes: int = 60 * 12
    refresh_token_days: int = 30
    pull_page_size: int = 500

    model_config = {"env_file": ".env", "env_prefix": "TODOLIST_"}


@lru_cache
def get_settings() -> Settings:
    return Settings()
