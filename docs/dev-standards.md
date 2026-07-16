# 开发规范

## 命名规范

- **类名/方法名**：PascalCase（`RecorderService`、`StartRecording`）
- **私有字段**：camelCase + 下划线前缀（`_recorder`、`_isRecording`）
- **局部变量**：camelCase（`outputPath`、`frameRate`）
- **常量**：PascalCase（`WM_HOTKEY`）
- **UI 控件**：前缀 + 用途（`btnStart`、`txtStatus`、`cmbResolution`）

## 文件组织

```
Models/      → 数据模型、枚举（纯数据，无逻辑）
Services/    → 业务逻辑（可测试、可复用）
Views/       → XAML + code-behind（UI 逻辑）
ViewModels/  → MVVM ViewModel（当前为空，按需添加）
Helpers/     → 工具类、扩展方法
Resources/   → 图标、图片等资源
docs/        → 项目文档
devlog/      → 开发日志
```

## 开发流程

### 每次会话开始
1. 读取 `devlog/` 最新日志，了解上下文
2. 读取 `docs/execution-plan.md` 确认当前阶段
3. 确认本次要做的具体任务

### 开发过程中
- **一次只做一个任务**，不做多余的事
- 每次修改后运行 `dotnet build` 验证编译
- 遇到问题记录到 devlog，不强行推进
- 不添加用户未要求的功能

### 每次会话结束
1. 更新 `devlog/` 当日日志：
   - 已完成事项
   - 遗留问题 / 待办事项
   - 下次继续的方向
2. 如有功能变更，更新 `docs/requirements.md`

## 代码规范

- 不添加不必要的注释（代码即文档）
- 不添加不必要的错误处理（信任内部调用）
- 不添加不必要的抽象（三行重复代码优于过早抽象）
- 优先编辑现有文件，不新建文件（除非确实需要）
- WPF 事件处理在 code-behind 中（不强制 MVVM）

## 提交规范

- 提交信息格式：`[类型] 简要描述`
- 类型：feat / fix / refactor / docs / chore
- 示例：`[feat] 添加区域录制选择器`

## 测试要求

- 每次修改后必须 `dotnet build` 编译通过
- 关键功能手动测试验证（录制、暂停、停止、快捷键）
