# 安装与发布

这份文档说明如何把 `Code-W` 安装到本机 Visual Studio，以及如何发布到 Visual Studio Marketplace。

## 1. 本地打包

在仓库根目录执行：

```powershell
.\tools\package-vsix.ps1 -Configuration Release
```

也可以直接使用 `dotnet build`：

```powershell
dotnet build .\Code-W.slnx -c Release
```

构建完成后，VSIX 默认位于：

```text
src\CodeW\bin\Release\net8.0-windows\CodeW.vsix
```

当前这份 VSIX 是按项目现状为 Visual Studio 2022 `17.14+` 生成的，并包含 `amd64` / `arm64` 安装目标。

## 2. 安装到 Visual Studio

### 方式 A：直接双击 VSIX

找到生成的 `CodeW.vsix`，双击后会启动 Visual Studio 的 VSIX 安装器。

### 方式 B：使用仓库脚本安装

```powershell
.\tools\install-vsix.ps1 -Configuration Release
```

如果本地还没有生成 VSIX，可以加上：

```powershell
.\tools\install-vsix.ps1 -Configuration Release -BuildIfMissing
```

### 安装后验证

1. 关闭并重新打开 Visual Studio。
2. 在 `Extensions` 菜单中找到 `打开 Code-W`。
3. 打开后确认右侧工具窗口正常出现。

## 3. 更新本地安装包

如果你修改了代码并希望覆盖安装：

1. 提升扩展版本号。
2. 重新打包生成新的 VSIX。
3. 再次运行安装器覆盖安装。

当前版本号位于：

- [CodeWExtension.cs](/d:/work/git/Code-W/src/CodeW/CodeWExtension.cs)

如果 `Id` 和 `Publisher` 不变，Visual Studio 会把新版本视为同一扩展的升级包；如果这两个值改变，则会被当成一个全新的扩展。

## 4. 发布到 Marketplace 前要做什么

正式上架前，建议先确认以下内容：

1. 固定扩展标识
   - 保持 [CodeWExtension.cs](/d:/work/git/Code-W/src/CodeW/CodeWExtension.cs) 里的 `id` 稳定不变。
   - 确定长期使用的 `publisherName`，后续不要频繁修改。
2. 提升版本号
   - 每次发新版前都要增加 `version`。
3. 检查展示资产
   - 图标： [code-w-icon-32.png](/d:/work/git/Code-W/src/CodeW/art/code-w-icon-32.png)
   - 预览图： [code-w-preview-200.png](/d:/work/git/Code-W/src/CodeW/art/code-w-preview-200.png)
4. 补全上架文案
   - 发布清单： [publishmanifest.json](/d:/work/git/Code-W/marketplace/publishmanifest.json)
   - Marketplace 介绍页： [overview.md](/d:/work/git/Code-W/marketplace/overview.md)

## 5. 首次上架

### 第一步：创建发布者

先在 Visual Studio Marketplace 后台创建你的 Publisher。创建完成后，把发布者名称填入：

- [publishmanifest.json](/d:/work/git/Code-W/marketplace/publishmanifest.json)

把其中的：

```json
"publisher": "YOUR_PUBLISHER"
```

改成你的真实发布者名称。

同时建议把仓库地址也一并改掉：

```json
"repo": "https://github.com/YOUR_ORG/Code-W"
```

### 第二步：生成 Release 包

```powershell
.\tools\package-vsix.ps1 -Configuration Release
```

### 第三步：使用命令行发布

准备好 Publisher 名称和 Marketplace 的 Personal Access Token 后执行：

```powershell
.\tools\publish-vsix.ps1 `
  -Publisher YourPublisher `
  -PersonalAccessToken YOUR_PAT `
  -Configuration Release
```

脚本会自动：

1. 确认 VSIX 已生成。
2. 查找 `VsixPublisher.exe`。
3. 读取发布清单。
4. 调用命令行上传到 Marketplace。

## 6. 后续更新上架

发布更新版本时，流程和首次发布基本一样：

1. 修改版本号。
2. 重新生成 Release VSIX。
3. 运行 `publish-vsix.ps1` 上传新包。

注意不要随意更换扩展 `id`、Marketplace `publisher` 和 `internalName`，否则会影响已有安装用户的升级链路。

## 7. 当前项目的产物位置

常用文件如下：

- 安装包： [CodeW.vsix](/d:/work/git/Code-W/src/CodeW/bin/Release/net8.0-windows/CodeW.vsix)
- 扩展入口： [CodeWExtension.cs](/d:/work/git/Code-W/src/CodeW/CodeWExtension.cs)
- 发布清单： [publishmanifest.json](/d:/work/git/Code-W/marketplace/publishmanifest.json)
- Marketplace 文案： [overview.md](/d:/work/git/Code-W/marketplace/overview.md)
- 打包脚本： [package-vsix.ps1](/d:/work/git/Code-W/tools/package-vsix.ps1)
- 安装脚本： [install-vsix.ps1](/d:/work/git/Code-W/tools/install-vsix.ps1)
- 发布脚本： [publish-vsix.ps1](/d:/work/git/Code-W/tools/publish-vsix.ps1)
