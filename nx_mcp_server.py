#!/usr/bin/env python3
# 代码由 AI（DeepSeek V4, Claude Code）生成，非人类手写。https://github.com/Yuxcv/nx-ai-tools
"""NX MCP Server v6 — FastMCP + 单次 Journal（不卡 UI，每步可见）"""

from __future__ import annotations

import asyncio
import functools
import json
import os
import subprocess
import time
from dataclasses import dataclass
from typing import Any

from mcp.server.fastmcp import FastMCP

CMDF = r"C:\temp\nx\command.json"
RESF = r"C:\temp\nx\result.json"
STEP = r"C:\Users\Administrator\Desktop\nx_step.py"
RUN = r"E:\NXBIN\run_journal.exe"
TIMEOUT = 30  # journal 进程总超时

@dataclass
class CommandResult:
    ok: bool; payload: Any = None; error: str | None = None
    def to_dict(self):
        return {"ok":True,"payload":self.payload} if self.ok else {"ok":False,"error":self.error}

class JournalBackend:
    """通过 run_journal.exe + nx_step.py 单次执行命令。"""
    name = "journal"

    def __init__(self):
        self._lock = asyncio.Lock()

    async def _exec(self, cmd_type: str, params: dict) -> CommandResult:
        async with self._lock:
            # 写命令
            cmd = {"type": cmd_type, "id": str(int(time.time()*1000)), "params": params}
            if os.path.exists(RESF): os.remove(RESF)
            with open(CMDF, "w") as f: json.dump(cmd, f)

            # 调用 journal
            try:
                proc = await asyncio.create_subprocess_exec(
                    RUN, STEP,
                    stdout=asyncio.subprocess.PIPE,
                    stderr=asyncio.subprocess.PIPE,
                )
                stdout, stderr = await asyncio.wait_for(proc.communicate(), timeout=TIMEOUT)
                if stderr:
                    err_text = stderr.decode("utf-8", errors="replace").strip()
                    if err_text:
                        print(f"[nx journal stderr] {err_text}", flush=True)
            except asyncio.TimeoutError:
                try: proc.kill()
                except: pass
                return CommandResult(ok=False, error="journal 超时")

            # 读结果（最多等 4 秒）
            for _ in range(40):
                if os.path.exists(RESF):
                    try:
                        with open(RESF, "r") as f:
                            r = json.load(f)
                        if r.get("success"):
                            return CommandResult(ok=True, payload=r.get("data"))
                        return CommandResult(ok=False, error=r.get("error", "?"))
                    except json.JSONDecodeError:
                        pass
                await asyncio.sleep(0.1)
            return CommandResult(ok=False, error="无结果文件")

    async def system(self, op: str, data: dict) -> CommandResult:
        if op == "clear": return await self._exec("clear", data)
        if op == "save":  return await self._exec("save", data)
        if op == "ping":  return await self._exec("ping", data)
        return CommandResult(ok=False, error=f"Unknown: system.{op}")

    async def solid(self, op: str, data: dict) -> CommandResult:
        return await self._exec(op, data) if op in ("block","cylinder","sphere","cone") \
            else CommandResult(ok=False, error=f"Unknown: solid.{op}")

    async def sketch(self, op: str, data: dict) -> CommandResult:
        return await self._exec(op, data) if op in ("extrude","pocket") \
            else CommandResult(ok=False, error=f"Unknown: sketch.{op}")

    async def boolean(self, op: str, data: dict) -> CommandResult:
        return await self._exec(op, data) if op in ("hole","subtract","unite") \
            else CommandResult(ok=False, error=f"Unknown: boolean.{op}")

# ─── Singleton ──────────────────────────────────────────────────────
_backend = None; _lock = asyncio.Lock()
async def get_backend():
    global _backend
    if _backend: return _backend
    async with _lock:
        if _backend: return _backend
        _backend = JournalBackend()
        return _backend

# ─── Helpers ──────────────────────────────────────────────────────────
def _json(d): return json.dumps(d, default=str, separators=(",", ":"))
def _safe(name):
    def d(fn):
        @functools.wraps(fn)
        async def w(*a,**kw):
            try: return await fn(*a,**kw)
            except Exception as e: return _json({"ok":False,"error":str(e)})
        return w
    return d

# ─── Tools ────────────────────────────────────────────────────────────
mcp = FastMCP("nx-mcp")

@mcp.tool()
@_safe("solid")
async def solid(operation: str, data: dict|None=None) -> str:
    """block: {x,y,z?,w,h,d} cylinder: {d,h,x,y,z?} sphere: {d,x,y,z?} cone: {bd,td,h,x,y,z?}"""
    return _json((await (await get_backend()).solid(operation, data or {})).to_dict())

@mcp.tool()
@_safe("sketch")
async def sketch(operation: str, data: dict|None=None) -> str:
    """extrude: {x,y,z?,w,h,d,sign?} pocket: {x,y,z?,r,d}"""
    return _json((await (await get_backend()).sketch(operation, data or {})).to_dict())

@mcp.tool()
@_safe("boolean")
async def boolean(operation: str, data: dict|None=None) -> str:
    """hole: {x,y,r,d} subtract: {x,y,d,h,z?} unite: (no data)"""
    return _json((await (await get_backend()).boolean(operation, data or {})).to_dict())

@mcp.tool()
@_safe("system")
async def system(operation: str, data: dict|None=None) -> str:
    """clear / save / ping"""
    return _json((await (await get_backend()).system(operation, data or {})).to_dict())

def main(): mcp.run(transport="stdio")
if __name__ == "__main__": main()
