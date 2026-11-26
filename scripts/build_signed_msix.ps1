$ErrorActionPreference = "Stop"

$Scratch = "C:\Users\<USERNAME>\drawboard_build\scratch"
$SrcAppFiles = "C:\Users\<USERNAME>\Downloads\drawboard_build\AppFiles"
$Stage = Join-Path $Scratch "msix_stage"
$MsixOut = Join-Path $Scratch "DrawboardPDF_Pro.msix"
$CertPfx = Join-Path $Scratch "DrawboardPro.pfx"
$CertCer = Join-Path $Scratch "DrawboardPro.cer"
$CertPass = "DrawboardPro2026!"

$MakeAppx = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\makeappx.exe"
$SignTool = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe"

Write-Host "[1/4] Staging clean AppFiles..."
if (Test-Path $Stage) { Remove-Item $Stage -Recurse -Force }
New-Item -ItemType Directory -Path $Stage | Out-Null

# Copy everything except signature/blockmap/metadata/split/backup artifacts
robocopy $SrcAppFiles $Stage /E /XD "SplitScale100" "AppxMetadata" /XF "AppxSignature.p7x*" "AppxBlockMap.xml" "[Content_Types].xml" "*.original_backup" "*.bak" "05DBE9EA-EF75-43DB-8A03-27898B59D1E9" /NFL /NDL /NJH /NJS | Out-Null

Write-Host "[2/4] Packing MSIX with makeappx.exe..."
if (Test-Path $MsixOut) { Remove-Item $MsixOut -Force }
& $MakeAppx pack /o /d $Stage /p $MsixOut
if ($LASTEXITCODE -ne 0) { throw "makeappx failed with exit code $LASTEXITCODE" }

Write-Host "[3/4] Creating self-signed Code Signing Certificate..."
$Subject = "CN=3CCF3A32-EA16-47A1-B27A-BC4D10D7E322"
$Cert = New-SelfSignedCertificate `
    -Type Custom `
    -Subject $Subject `
    -KeyUsage DigitalSignature `
    -FriendlyName "Drawboard PDF Pro Offline Cert" `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}") `
    -NotAfter (Get-Date).AddYears(10)

$SecurePass = ConvertTo-SecureString -String $CertPass -Force -AsPlainText
Export-PfxCertificate -Cert $Cert -FilePath $CertPfx -Password $SecurePass | Out-Null
Export-Certificate -Cert $Cert -FilePath $CertCer | Out-Null
# Clean up CurrentUser\My cert so we don't leave private keys lying around after signing
Remove-Item $Cert.PSPath -Force

Write-Host "[4/4] Signing MSIX with signtool.exe..."
& $SignTool sign /fd SHA256 /f $CertPfx /p $CertPass $MsixOut
if ($LASTEXITCODE -ne 0) { throw "signtool failed with exit code $LASTEXITCODE" }

# Remove private key PFX after signing; only public .cer is needed for installer
Remove-Item $CertPfx -Force

$Sig = Get-AuthenticodeSignature $MsixOut
Write-Host "MSIX Built & Signed: $MsixOut"
Write-Host "Size: $([Math]::Round((Get-Item $MsixOut).Length / 1MB, 2)) MB"
Write-Host "Signature Status: $($Sig.Status) | Signer: $($Sig.SignerCertificate.Subject)"
