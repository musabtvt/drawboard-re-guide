$ErrorActionPreference = "Stop"

$Scratch = "C:\Users\<USERNAME>\drawboard_build\scratch"
$PayloadStage = Join-Path $Scratch "exe_payload_stage"
$ZipPath = Join-Path $Scratch "payload.zip"
$ExeOut = "C:\Users\<USERNAME>\Downloads\drawboard_build\DrawboardPDF_Pro_Setup.exe"
$Csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

Write-Host "[1/3] Creating payload.zip..."
if (Test-Path $PayloadStage) { Remove-Item $PayloadStage -Recurse -Force }
New-Item -ItemType Directory -Path $PayloadStage | Out-Null
Copy-Item (Join-Path $Scratch "DrawboardPDF_Pro.msix") $PayloadStage
Copy-Item (Join-Path $Scratch "DrawboardPro.cer") $PayloadStage
Copy-Item "C:\Users\<USERNAME>\Downloads\drawboard_build\Dependencies\Microsoft.UI.Xaml.2.8.appx" $PayloadStage

if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($PayloadStage, $ZipPath, [System.IO.Compression.CompressionLevel]::NoCompression, $false)

Write-Host "[2/3] Compiling GUI DrawboardPDF_Pro_Setup.exe (zero console window)..."
if (Test-Path $ExeOut) { Remove-Item $ExeOut -Force }
$ManifestFile = Join-Path $Scratch "setup.manifest"
$SourceFile = Join-Path $Scratch "SetupGuiInstaller.cs"
& $Csc `
    /nologo `
    /target:winexe `
    /platform:x64 `
    /optimize+ `
    "/out:$ExeOut" `
    "/win32manifest:$ManifestFile" `
    /reference:System.IO.Compression.FileSystem.dll `
    /reference:System.IO.Compression.dll `
    /reference:System.Windows.Forms.dll `
    /reference:System.Drawing.dll `
    "/resource:$ZipPath,payload.zip" `
    "$SourceFile"

if ($LASTEXITCODE -ne 0) { throw "csc.exe failed with exit code $LASTEXITCODE" }

Write-Host "[3/3] Done!"
Get-Item $ExeOut | Select-Object FullName, @{Name="SizeMB";Expression={[Math]::Round($_.Length/1MB,2)}}
