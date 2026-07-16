# CLAUDE.md — 录屏软件项目指引

## 项目概述

Windows 桌面录屏应用，C# WPF，.NET 8。支持全屏/区域/窗口录制，音频捕获，快捷键控制。

## 标准文件路径

| 文件 | 路径 | 用途 |
|------|------|------|
| 功能需求 | `docs/requirements.md` | 已完成/进行中/待办功能清单 |
| 技术架构 | `docs/architecture.md` | 项目结构、依赖、数据流 |
| 设计规范 | `docs/design-spec.md` | 主题色、布局、交互规则 |
| 开发规范 | `docs/dev-standards.md` | 命名、文件组织、流程 |
| 执行计划 | `docs/execution-plan.md` | 分阶段计划和里程碑 |
| 开发日志 | `devlog/YYYY-MM-DD.md` | 每日完成事项和待办 |

## 工作流程

### 每次会话开始
1. 读取 `devlog/` 最新日志了解上下文
2. 读取 `docs/execution-plan.md` 确认当前阶段
3. 确认本次要做的具体任务（一次只做一个）

### 开发过程中
- 每次修改后 `dotnet build` 验证编译
- 遇到问题记录到 devlog，不强行推进
- 不添加用户未要求的功能

### 每次会话结束
1. 更新 `devlog/` 当日日志
2. 如有功能变更，更新 `docs/requirements.md`

## 项目结构

```
ScreenRecorder/
├── ScreenRecorder.sln
├── ScreenRecorder.csproj
├── App.xaml/.cs
├── Models/          → 数据模型
├── Services/        → 业务逻辑
├── Views/           → UI 窗口
├── ViewModels/      → MVVM（当前为空）
├── Helpers/         → 工具类（MonitorHelper, FfmpegHelper）
├── Resources/       → 资源文件
├── ffmpeg/          → FFmpeg 可执行文件（需运行 download-ffmpeg.ps1）
├── docs/            → 项目文档
└── devlog/          → 开发日志
```

## FFmpeg 依赖

FFmpeg 用于视频编码、音频合并、GIF 导出。处理方式：
1. **优先使用嵌入版本**：检查 `ffmpeg/ffmpeg.exe`
2. **其次使用系统版本**：检查 PATH 中的 ffmpeg
3. **自动下载**：运行 `download-ffmpeg.ps1` 脚本下载便携版
4. **启动检查**：应用启动时自动检测 FFmpeg 可用性

## 当前阶段

所有计划功能已完成，核心优化已完成！代码质量修复已完成（2026-07-17）。

### 已完成的优化
- 停止录制零卡顿（异步保存 + 后台线程）
- 内存恒定 ~170MB（流式磁盘写入，不随录制时长增长）
- 录制中 UI 完全流畅（帧采集在后台线程）
- H.264 编码（全平台兼容）
- 正确的像素格式（bgr24 匹配 GDI+）
- 音频设备自动检测
- 历史界面多选删除 + 刷新同步

### 已完成的代码质量修复（2026-07-17）
- 线程安全：volatile _state、_mouseClicks 加锁、音频回调本地引用
- 资源泄漏：Font 缓存、Process using、CTS 释放、COM using
- 关闭竞争：Window_Closing 等待后台保存、Dispatcher try-catch
- 性能优化：ArrayPool、帧率精度、Sleep/SpinWait 策略
- 历史记录：使用实际采集分辨率

### 可进行
1. 图片水印支持
2. GIF 参数设置界面
3. 功能测试验证
