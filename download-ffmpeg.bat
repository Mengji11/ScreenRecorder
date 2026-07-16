@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

echo FFmpeg Download Script
echo ======================
echo.

set "FFMPEG_DIR=%~dp0ffmpeg"

if exist "%FFMPEG_DIR%\ffmpeg.exe" (
    echo FFmpeg already exists at %FFMPEG_DIR%\ffmpeg.exe
    "%FFMPEG_DIR%\ffmpeg.exe" -version 2>nul | findstr /C:"ffmpeg version"
    goto :done
)

echo Creating directory...
if not exist "%FFMPEG_DIR%" mkdir "%FFMPEG_DIR%"

echo.
echo Please download FFmpeg manually:
echo.
echo 1. Go to: https://github.com/BtbN/FFmpeg-Builds/releases
echo 2. Download: ffmpeg-master-latest-win64-gpl.zip
echo 3. Extract ffmpeg.exe and ffprobe.exe to:
echo    %FFMPEG_DIR%
echo.
echo After downloading, run this script again to verify.
echo.

:done
echo.
pause
