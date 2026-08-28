"""设备管理（SPD §9）。"""
from fastapi import APIRouter, Depends, HTTPException, status
from sqlalchemy import delete, select
from sqlalchemy.ext.asyncio import AsyncSession

from ..db import get_session
from ..deps import get_current_user
from ..models import Device, User, now_ms
from ..schemas import DeviceIn, DeviceOut

router = APIRouter(prefix="/api/v1/devices", tags=["devices"])


@router.get("", response_model=list[DeviceOut])
async def list_devices(
    user: User = Depends(get_current_user),
    session: AsyncSession = Depends(get_session),
):
    rows = (
        await session.execute(
            select(Device).where(Device.user_id == user.id).order_by(Device.id)
        )
    ).scalars().all()
    return [
        DeviceOut(
            deviceId=d.device_id,
            platform=d.platform,
            deviceName=d.device_name,
            lastSyncAt=d.last_sync_at,
        )
        for d in rows
    ]


@router.post("", response_model=DeviceOut)
async def register_device(
    body: DeviceIn,
    user: User = Depends(get_current_user),
    session: AsyncSession = Depends(get_session),
):
    device = (
        await session.execute(
            select(Device).where(
                Device.user_id == user.id, Device.device_id == body.deviceId
            )
        )
    ).scalar_one_or_none()
    if device is None:
        device = Device(user_id=user.id, device_id=body.deviceId)
        session.add(device)
    device.platform = body.platform
    device.device_name = body.deviceName
    await session.commit()
    return DeviceOut(
        deviceId=device.device_id,
        platform=device.platform,
        deviceName=device.device_name,
        lastSyncAt=device.last_sync_at,
    )


@router.delete("/{device_id}", status_code=status.HTTP_204_NO_CONTENT)
async def remove_device(
    device_id: str,
    user: User = Depends(get_current_user),
    session: AsyncSession = Depends(get_session),
):
    await session.execute(
        delete(Device).where(Device.user_id == user.id, Device.device_id == device_id)
    )
    await session.commit()
    return None


async def touch_device(
    session: AsyncSession, user_id: int, device_id: str
) -> None:
    """push/pull 时更新 lastSyncAt（不存在的设备自动注册）。"""
    device = (
        await session.execute(
            select(Device).where(
                Device.user_id == user_id, Device.device_id == device_id
            )
        )
    ).scalar_one_or_none()
    if device is None:
        device = Device(user_id=user_id, device_id=device_id, last_sync_at=now_ms())
        session.add(device)
    else:
        device.last_sync_at = now_ms()
