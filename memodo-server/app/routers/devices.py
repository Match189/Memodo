"""设备注册与心跳（任务书 §5 设备标识）。"""
from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from ..db import get_db
from ..deps import CurrentUser, get_current_user
from ..models import Device
from ..schemas import DeviceOut, DeviceRegisterIn

router = APIRouter(prefix="/devices", tags=["devices"])


@router.post("/register", response_model=DeviceOut)
async def register_device(
    body: DeviceRegisterIn,
    user: CurrentUser = Depends(get_current_user),
    db: AsyncSession = Depends(get_db),
):
    existing = (await db.execute(
        select(Device).where(
            Device.user_id == user.id, Device.device_id == body.device_id
        )
    )).scalar_one_or_none()
    if existing is not None:
        return DeviceOut(id=str(existing.id), device_id=existing.device_id, name=existing.name)

    device = Device(user_id=user.id, device_id=body.device_id, name=body.name)
    db.add(device)
    await db.commit()
    await db.refresh(device)
    return DeviceOut(id=str(device.id), device_id=device.device_id, name=device.name)


@router.post("/heartbeat")
async def heartbeat(
    body: DeviceRegisterIn,
    user: CurrentUser = Depends(get_current_user),
    db: AsyncSession = Depends(get_db),
):
    device = (await db.execute(
        select(Device).where(
            Device.user_id == user.id, Device.device_id == body.device_id
        )
    )).scalar_one_or_none()
    if device is None:
        raise HTTPException(404, "device not registered")
    return {"ok": True}
