# Code-W

Code-W 是一款面向 Visual Studio 的本地优先 AI 编码助手。

它的设计目标是提供类似 Codex / Amazon Q 的开发体验，但不要求用户登录平台账号，直接用自己配置的模型 API Key 即可开始使用。

## 主要特性

- 多模型接入
  - 支持 OpenAI、Kimi、Qianwen
  - 支持任意 OpenAI-compatible 网关
- 双工作模式
  - Chat：偏即时问答、代码解释和设计讨论
  - Agent：偏任务拆解、工具调用和执行式协作
- MCP 支持
  - 支持真实 stdio MCP 会话
  - 支持工具发现和工具调用结果回灌
- Skill 支持
  - 支持从本地 skill 文件加载提示模板
  - 适合团队私有流程、审查规范和迁移模板沉淀
- 本地优先
  - 不要求登录
  - Provider API Key 本地加密存储

## 适合谁

- 想在 Visual Studio 里直接使用 AI 编码助手的开发者
- 需要接入自有模型网关或团队模型配额的团队
- 需要把 MCP 工具和私有 skill 流程接入 IDE 的企业场景

## 使用方式

安装扩展后，从 `Extensions` 菜单打开 `Code-W`，在右侧面板中配置 Provider、MCP 和 Skill，然后即可在 Chat 或 Agent 模式下开始工作。

## 当前状态

当前版本已经支持基础对话、流式输出、MCP 工具调用闭环和本地 skill 注入，适合作为持续迭代和团队定制的 Visual Studio AI 插件基础版本。
