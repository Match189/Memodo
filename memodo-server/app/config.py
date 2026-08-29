"""服务端配置（任务书 §5-§9 同步协议 + JWT）。

所有可调项走环境变量 / .env。生产环境务必覆盖 jwt_secret 与 database_url。
"""
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", extra="ignore")

    # PostgreSQL（异步驱动）。本地可用 docker-compose 一键起库。
    database_url: str = "postgresql+asyncpg://memodo:memodo@localhost:5432/memodo"

    jwt_secret: str = "change-me-in-production"
    jwt_algorithm: str = "HS256"
    access_token_expire_minutes: int = 15
    refresh_token_expire_days: int = 7

    # 允许的前端来源；生产改为具体域名
    cors_origins: list[str] = ["*"]


settings = Settings()
