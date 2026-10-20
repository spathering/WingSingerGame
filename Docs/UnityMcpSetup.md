# Unity MCP 本机连接

本项目使用 CoplayDev 的 MCP for Unity 10.3.0（第三方开源插件），通过 Unity Package Manager 的 Git 依赖安装。Unity 插件和 Python 服务均固定为 10.3.0。

- Unity 项目：`H:\PolyU\PM\WingSingerGame`
- Unity 编辑器：2022.3.62f2
- 本机服务：`http://127.0.0.1:8080`
- Codex MCP 名称：`unityMCP`
- Codex MCP URL：`http://127.0.0.1:8080/mcp`
- Codex 配置位置：`%USERPROFILE%\.codex\config.toml`

Unity 菜单 `Window > MCP for Unity` 可查看连接状态。已启用插件的 Auto-Start on Editor Load；重新打开 Unity 时，插件会尝试启动本机服务并连接。场景和玩法资源不因 MCP 安装而修改。

若服务未运行，可在 PowerShell 启动：

```powershell
uvx --python 3.12 --from mcpforunityserver==10.3.0 mcp-for-unity --transport http --http-host 127.0.0.1 --http-port 8080
```

Codex 配置可通过 `codex mcp get unityMCP` 查看。新增配置后，在 Codex 的 MCP 设置中重启服务；若当前聊天仍未出现 Unity 工具，则重启 Codex 应用后继续本聊天。

排查时先确认 Unity 编译完成、插件窗口已经连接，再检查 `http://127.0.0.1:8080/health` 和 `/api/instances`。安装期间的本机日志位于 `Logs/UnityMcpEditor.log`、`Logs/UnityMcpServer.log` 和 `Logs/UnityMcpServer.err.log`。

上游说明：https://github.com/CoplayDev/unity-mcp

## 后续开发的刷新与编译

日常开发遵循 `DevelopmentGuidelines.md` 第 5 节。默认复用本编辑器并等待自动增量编译，先读取就绪状态和控制台；无需每次开发后都请求刷新或编译。

仅在变更已导入但未编译时，按需调用一次 `refresh_unity(scope="scripts", compile="request", wait_for_ready=true)`。不默认使用 `mode="force", scope="all"`；10.3.0 的 `if_dirty` 在资源刷新分支也可能走强制刷新路径。资源刷新、域重载和全量编译应分别判断，不能只凭工具名称或等待时长判断。

## 连接核验（2026-10-20）

已从当前聊天的 Unity MCP 工具直接读取项目与 `BreathDemo` 场景，确认场景未被修改。移除一次性安装脚本并请求重编译后，连接已自动恢复，编辑器重新就绪。当前聊天已经载入 Unity 工具，无需为本次连接额外重启 Codex。
