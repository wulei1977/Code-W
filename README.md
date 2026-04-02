# Code-W

`Code-W` 是一款面向 Visual Studio 的本地优先 AI 编码助手扩展，目标是做成类似 Codex / Amazon Q 的多模型协作插件：

- 不要求登录，用户直接配置自己的 API Key
- 支持 `OpenAI`、`Kimi`、`Qianwen`，也支持任意 `OpenAI-compatible` 网关
- 提供 `Chat` 和 `Agent` 两种交互模式
- 支持真实的 `MCP` 接入
- 支持从本地文件加载 `Skill`

## 当前能力

当前仓库已经具备一版可编译、可安装的 Visual Studio 扩展骨架，并且包含这些核心能力：

- Provider 配置
  - 支持在工具窗口中配置 Base URL、模型名、API Key
  - API Key 使用当前 Windows 用户的 `DPAPI` 做本地加密存储
- Chat 模式
  - 通过 `OpenAI-compatible /chat/completions` 发起请求
  - 支持服务端流式输出并在 UI 中逐步刷新
- Agent 模式
  - 支持真实的 MCP stdio 会话
  - 已实现 `initialize`、`notifications/initialized`、`tools/list`、`tools/call`
  - 支持模型发起工具调用，再把工具结果回灌给模型继续推理
- Skill
  - 支持在配置中注册本地 skill 文件
  - 发送请求前会读取启用的 skill 内容，并注入系统提示词
- 本地配置
  - Provider、MCP、Skill 都可以在工具窗口中编辑、启停、增删和保存

## 项目结构

- `src/CodeW`
  - Visual Studio 扩展主体
- `src/CodeW/UI`
  - Tool Window、Remote UI 和面板数据模型
- `src/CodeW/Services`
  - Provider 配置存储、模型请求、MCP 会话、Skill 加载、对话编排
- `src/CodeW/Models`
  - Provider、Conversation、MCP、Skill 等模型
- `src/CodeW/art`
  - `Code-W` 图标资源
- `skills`
  - 默认提供的本地 skill 模板

## 默认 Skill

仓库内已经附带两个示例 skill，可直接启用：

- `skills/solution-review.md`
  - 偏代码审查、风险识别和回归分析
- `skills/migration-helper.md`
  - 偏升级迁移、兼容性分析和渐进式重构

## 本地构建

在仓库根目录执行：

```powershell
dotnet build Code-W.slnx
```

构建成功后会生成 VSIX：

```text
src/CodeW/bin/Debug/net8.0-windows/CodeW.vsix
```

## 安装与发布

仓库已经附带安装和上架所需的说明与脚本：

- 安装/发布说明： [install-and-publish.md](docs/install-and-publish.md)
- 打包脚本： [package-vsix.ps1](tools/package-vsix.ps1)
- 本地安装脚本： [install-vsix.ps1](tools/install-vsix.ps1)
- Marketplace 发布脚本： [publish-vsix.ps1](tools/publish-vsix.ps1)
- 发布清单模板： [publishmanifest.json](marketplace/publishmanifest.json)
- Marketplace 介绍页模板： [overview.md](marketplace/overview.md)

## 配置存储

本地配置默认保存在：

```text
%LocalAppData%\Code-W\codew.settings.json
```

其中 Provider 的 API Key 会加密保存，MCP 和 Skill 配置会以明文元数据形式保存在同一个配置文件中。

## 使用补充

当前版本额外支持两项面板级能力：

- `MCP 测试连接`
  - 可直接基于当前 MCP 配置做一次真实握手，并把发现的工具写入会话区
- `Skill 扫描目录`
  - 可从仓库中的 `skills` 目录自动发现 `.md` 文件并注册到本地配置
