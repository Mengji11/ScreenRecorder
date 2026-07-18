# 技术架构

## 项目概览

Windows 桌面录屏应用，C# WPF，.NET 8。

## 项目结构

```
ScreenRecorder/
├── ScreenRecorder.sln
├── ScreenRecorder.csproj
├── App.xaml / App.xaml.cs          # 应用入口、全局资源、异常处理
├── Models/                         # 数据模型
│   ├── RecordingSettings.cs        # 录制设置（分辨率、帧率、快捷键等）
│   ├── RecordingState.cs           # 录制状态枚举（Idle/Recording/Paused）
│   ├── RecordingMode.cs            # 录制模式枚举（FullScreen/Window/Region）
│   └── RecordingHistoryItem.cs     # 历史记录项
├── Services/                       # 业务逻辑层
│   ├── RecorderService.cs          # 录制核心（编排截图+音频+编码）
│   ├── ScreenCaptureService.cs     # 屏幕捕获（GDI+ CopyFromScreen）
│   ├── AudioCaptureService.cs      # 音频捕获（NAudio WasapiCapture）
│   ├── HotkeyService.cs            # 全局快捷键（RegisterHotKey P/Invoke）
│   ├── SettingsService.cs          # 设置持久化（JSON 文件）
│   └── HistoryService.cs           # 历史记录持久化（JSON 文件）
├── Views/                          # UI 层
│   ├── MainWindow.xaml/.cs         # 主窗口
│   ├── SettingsWindow.xaml/.cs     # 设置窗口
│   ├── HistoryWindow.xaml/.cs      # 历史记录窗口
│   └── RecordingControlBar.xaml/.cs # 浮动录制控制条
├── ViewModels/                     # MVVM ViewModel 层（当前为空）
├── Helpers/                        # 工具类（当前为空）
├── Resources/                      # 资源文件
├── docs/                           # 项目文档
└── devlog/                         # 开发日志
```

## NuGet 依赖

| 包名 | 版本 | 用途 |
|------|------|------|
| NAudio | 2.2.1 | 音频捕获（WasapiLoopbackCapture 系统声音，WasapiCapture 麦克风） |

## 数据流

```
用户操作 (UI)
    ↓
MainWindow (事件处理)
    ↓
RecorderService (编排)
    ├── ScreenCaptureService → GDI+ 截图 → Bitmap 列表
    └── AudioCaptureService → NAudio → WAV 文件
    ↓
SaveRecording()
    ├── SaveFramesAsVideo() → FFmpeg 管道输入 → 临时 MP4
    └── MergeVideoAndAudio() → FFmpeg 合并 → 最终 MP4
    ↓
HistoryService.AddEntry() → 记录到 JSON
```

## 编码流程

1. 截图帧通过 `DispatcherTimer` 按帧率定时捕获，存入 `List<Bitmap>`
2. 录制结束后，所有帧通过 FFmpeg stdin 管道输入编码为 H.264
3. 音频分别保存为 WAV（系统声音 + 麦克风各一个文件）
4. FFmpeg 合并视频和音频为最终 MP4
5. 清理临时文件

## 外部依赖

- **FFmpeg**：需用户自行安装并加入 PATH
- **Windows API**：P/Invoke 调用 user32.dll（快捷键、窗口枚举）、gdi32.dll
