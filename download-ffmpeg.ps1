# Download FFmpeg portable version script
# Run this script to download FFmpeg to ffmpeg/ directory

$ffmpegDir = "$PSScriptRoot\ffmpeg"

# Multiple download sources (with GitHub proxy mirror)
$urls = @(
    "https://ghproxy.net/https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip",
    "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip",
    "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip"
)

Write-Host "FFmpeg Download Script" -ForegroundColor Cyan
Write-Host "======================" -ForegroundColor Cyan
Write-Host ""

# Create directory
if (!(Test-Path $ffmpegDir)) {
    New-Item -ItemType Directory -Path $ffmpegDir -Force | Out-Null
}

# Check if FFmpeg already exists
if (Test-Path "$ffmpegDir\ffmpeg.exe") {
    Write-Host "FFmpeg already exists at $ffmpegDir\ffmpeg.exe" -ForegroundColor Green
    $version = & "$ffmpegDir\ffmpeg.exe" -version 2>&1 | Select-Object -First 1
    Write-Host "Version: $version" -ForegroundColor Green
    exit 0
}

# Try to download
$downloaded = $false
foreach ($url in $urls) {
    Write-Host "Trying: $url" -ForegroundColor Yellow
    
    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        $ProgressPreference = 'SilentlyContinue'
        
        $zipFile = "$env:TEMP\ffmpeg-$([System.Guid]::NewGuid().ToString('N')).zip"
        Invoke-WebRequest -Uri $url -OutFile $zipFile -UseBasicParsing -TimeoutSec 300
        
        if (Test-Path $zipFile) {
            $fileSize = (Get-Item $zipFile).Length
            if ($fileSize -gt 1MB) {
                Write-Host "Download complete ($([math]::Round($fileSize/1MB, 1)) MB), extracting..." -ForegroundColor Green
                
                Expand-Archive -Path $zipFile -DestinationPath $env:TEMP\ffmpeg-extract -Force
                
                # Find exe files in bin directory
                $binPath = Get-ChildItem -Path "$env:TEMP\ffmpeg-extract" -Recurse -Filter "ffmpeg.exe" | Select-Object -First 1
                
                if ($binPath) {
                    Copy-Item "$($binPath.DirectoryName)\ffmpeg.exe" -Destination $ffmpegDir -Force
                    Copy-Item "$($binPath.DirectoryName)\ffprobe.exe" -Destination $ffmpegDir -Force
                    Write-Host "FFmpeg installed to $ffmpegDir" -ForegroundColor Green
                    $downloaded = $true
                    
                    # Cleanup
                    Remove-Item $zipFile -Force -ErrorAction SilentlyContinue
                    Remove-Item "$env:TEMP\ffmpeg-extract" -Recurse -Force -ErrorAction SilentlyContinue
                    break
                } else {
                    Write-Host "ffmpeg.exe not found in archive" -ForegroundColor Red
                }
            } else {
                Write-Host "Downloaded file too small, trying next source..." -ForegroundColor Yellow
            }
        }
    } catch {
        Write-Host "Failed: $($_.Exception.Message)" -ForegroundColor Red
    } finally {
        # Cleanup temp files
        Remove-Item $zipFile -Force -ErrorAction SilentlyContinue
        Remove-Item "$env:TEMP\ffmpeg-extract" -Recurse -Force -ErrorAction SilentlyContinue
    }
    
    Write-Host ""
}

if (-not $downloaded) {
    Write-Host "Automatic download failed." -ForegroundColor Red
    Write-Host ""
    Write-Host "Manual installation instructions:" -ForegroundColor Yellow
    Write-Host "1. Download FFmpeg from one of these links:" -ForegroundColor Yellow
    Write-Host "   - https://github.com/BtbN/FFmpeg-Builds/releases" -ForegroundColor Cyan
    Write-Host "   - https://www.gyan.dev/ffmpeg/builds/" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "2. Extract ffmpeg.exe and ffprobe.exe to:" -ForegroundColor Yellow
    Write-Host "   $ffmpegDir" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "3. Run this script again to verify installation" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Done!" -ForegroundColor Green
