# Quick VSIX Content Check
$vsixPath = "bin\Release\net472\WolvePack.VS.Extensions.ProjectReferrerVersioning.vsix"

Write-Host "?? Checking VSIX Contents..." -ForegroundColor Cyan
Write-Host "VSIX Path: $vsixPath" -ForegroundColor Gray

if (-not (Test-Path $vsixPath)) {
    Write-Host "? VSIX not found. Please build in Release mode first." -ForegroundColor Red
    exit 1
}

$tempDir = Join-Path $env:TEMP "VsixQuickCheck_$(Get-Date -Format 'HHmmss')"
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

try {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::ExtractToDirectory($vsixPath, $tempDir)
    
    Write-Host "`n?? VSIX Content Summary:" -ForegroundColor Green
    
    # Check critical files
    $criticalFiles = @(
        @{ Name = "LICENSE"; Required = $true },
        @{ Name = "icon.png"; Required = $true },
        @{ Name = "preview.png"; Required = $false },
        @{ Name = "extension.vsixmanifest"; Required = $true }
    )
    
    foreach ($file in $criticalFiles) {
        $path = Join-Path $tempDir $file.Name
        if (Test-Path $path) {
            $size = (Get-Item $path).Length
            Write-Host "? $($file.Name) ($size bytes)" -ForegroundColor Green
        } else {
            if ($file.Required) {
                Write-Host "? $($file.Name) - MISSING (Required)" -ForegroundColor Red
            } else {
                Write-Host "?? $($file.Name) - Missing (Optional)" -ForegroundColor Yellow
            }
        }
    }
    
    $allFiles = Get-ChildItem $tempDir -Recurse -File
    Write-Host "`n?? Total files in VSIX: $($allFiles.Count)" -ForegroundColor Cyan
    
    Write-Host "`n?? Marketplace Readiness:" -ForegroundColor Magenta
    $hasLicense = Test-Path (Join-Path $tempDir "LICENSE")
    $hasIcon = Test-Path (Join-Path $tempDir "icon.png")
    $hasPreview = Test-Path (Join-Path $tempDir "preview.png")
    
    if ($hasLicense -and $hasIcon) {
        if ($hasPreview) {
            Write-Host "?? FULLY READY for marketplace! All assets included." -ForegroundColor Green
        } else {
            Write-Host "?? ALMOST READY - Only missing preview.png screenshot" -ForegroundColor Yellow
        }
    } else {
        Write-Host "?? NOT READY - Missing critical files" -ForegroundColor Red
    }
    
} catch {
    Write-Host "? Error checking VSIX: $($_.Exception.Message)" -ForegroundColor Red
} finally {
    if (Test-Path $tempDir) {
        Remove-Item $tempDir -Recurse -Force
    }
}

Write-Host "`n? Next steps:" -ForegroundColor Cyan
if (-not (Test-Path "preview.png")) {
    Write-Host "1. Create preview.png (600x400 screenshot)" -ForegroundColor White
    Write-Host "2. Rebuild to include preview.png in VSIX" -ForegroundColor White
    Write-Host "3. Upload to Visual Studio Marketplace" -ForegroundColor White
} else {
    Write-Host "1. Your extension is ready for marketplace upload!" -ForegroundColor White
}