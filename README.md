# NX AI Tools — Siemens NX 12.0 自动化建模工具链

> **注意：此项目全部代码由 AI（DeepSeek V4 模型，运行于 Claude Code 内）生成，非人类手写。**
> 使用者 [@Yuxcv](https://github.com/Yuxcv) 提出需求并测试验证，代码由 AI 完成。

通过 AI（Claude Code / MCP）实时控制 NX 12.0 画图，支持草图→拉伸→布尔操作的完整建模流程。

## 架构

```
Claude Code (MCP)
    ↓ stdio
nx_mcp_server.py (FastMCP)
    ↓ file IPC (cmd_new.txt / res.txt)
C.exe (NX Remoting daemon)
    ↓ HTTP Remoting :4567
NX 12.0 (AutoServer → Server.dll → NXOpen API)
```

## 一键部署

```bash
# 1. 设环境变量
set UGII_USER_DIR=C:\nx-ai-tools\deploy

# 2. 注册到 NX
echo C:\nx-ai-tools\deploy >> E:\UGII\menus\custom_dirs.dat

# 3. 编译 C.exe
csc.exe /target:exe /out:C:\nx-ai-tools\C.exe \
    /reference:E:\NXBIN\managed\NXOpen.dll \
    /reference:E:\NXBIN\managed\NXOpen.UF.dll \
    /reference:E:\NXBIN\managed\NXOpen.Utilities.dll \
    C:\nx-ai-tools\src\C.cs

# 4. 编译 AutoServer.dll
csc.exe /target:library /out:C:\nx-ai-tools\deploy\startup\AutoServer.dll \
    /reference:E:\NXBIN\managed\NXOpen.dll \
    /reference:E:\NXBIN\managed\NXOpen.Utilities.dll \
    C:\nx-ai-tools\src\AutoServer.cs

# 5. 放 Server.dll 到 startup/
copy Server.dll C:\nx-ai-tools\deploy\startup\

# 6. 配置 MCP (.mcp.json)
{
    "mcpServers": {
        "nx": {
            "command": "python.exe",
            "args": ["C:\\nx-ai-tools\\nx_mcp_server.py"]
        }
    }
}
```

## 使用

```
打开 NX → C.exe 自动连接 → 发命令画图

# MCP 工具（4个合并工具）
solid(operation="block", data={w:100, h:50, d:30})
solid(operation="cylinder", data={d:50, h:100})
sketch(operation="extrude", data={x:-35, y:-15, w:70, h:30, d:10})
sketch(operation="pocket", data={x:0, y:0, r:8, d:15})
boolean(operation="hole", data={x:0, y:0, r:6, d:15})
system(operation="clear")
system(operation="save")
```

## C.exe 命令（直接模式）

```
extrude  x y z w h d [sign]  — 草图矩形→拉伸
pocket   x y z r depth       — 草图圆→拉伸切除
block    x y w h d [z]       — 方块
cylinder d h x y [z]         — 圆柱
sphere   d x y [z]           — 球体
cone     d1 d2 h x y [z]     — 圆锥
hole     cx cy r depth       — 孔
subtract cx cy d h [z]       — 布尔减圆柱
unite                         — 合并
clear                         — 清空画布
save                          — 保存到桌面
pause N                       — 暂停 N 毫秒
```

## 技术要点

- **NX DLL 自动加载**：custom_dirs.dat + startup/ + Startup() + GetUnloadOption()
- **布尔操作**：UF Tag 层绕过 Remoting 透明代理限制
- **Builder 生命周期**：必须 try/finally Destroy()
- **显示刷新**：`_uf.Disp.Refresh()` 每步强制重绘
- **C# 5 限制**：不支持 `=>` 表达式体、`?.` 运算符

## 许可

MIT
