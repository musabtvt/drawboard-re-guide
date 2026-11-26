# 03 — Binary Patches, MSIX Signing & Standalone GUI `.exe` Packaging

## 1. Surgical Patches Applied to `Db.App.dll`
All offsets are raw file offsets in `Db.App.dll` (`129,478,656` bytes, verified against `Db.App.dll.original_backup`):

| Patch | Function & Virtual Address | File Offset | Original Bytes | Patched Bytes | Assembly & Effect |
|---|---|---|---|---|---|
| **Revert** | `0x184d4b250` (unrelated `async MoveNext`) | `0x04D494CD` | `0F 84 9A 01 00 00` | `0F 84 9A 01 00 00` | Restored original `je` after proving it was not licensing code |
| **A1** | `LicenseController.SynchroniseApplicableLicensePlan` (`0x186d274a0`) | `0x06D2574A` | `44 0F 44 E7` | `90 90 90 90` | NOP `cmove r12d, edi` so `r12d` remains `0x12c` (`300` = **Pro Unlimited**) |
| **A2** | `LicenseController.SynchroniseApplicableLicensePlan` (`0x186d274a0`) | `0x06D25777` | `74 05` | `90 90` | NOP `je 0x186d2757e` so `ApplicableLicensePlan = 300` and `IsPaidLicense = true` |
| **A3** | `LicenseController.SynchroniseApplicableLicensePlan` (`0x186d274a0`) | `0x06D257A8` | `74 07` | `EB 13` | `jmp 0x186d275bd` to set `AppActivationType = 2` (`WindowsStore`) cleanly |
| **B** | `LicenseController.IsProPlanFeatureEnabled` (`0x186d277d0`) | `0x06D259D0` | `53 57 48 83 EC 28 8B FA` | `B0 01 C3 90 90 90 90 90` | `mov al, 1; ret` — every `ProPlanFeature` check returns `true` |
| **C** | `ProPlanFeatureExtensions.GetMinimumLicensePlan` (`0x186ab9c00`) | `0x06AB7E00` | `53 57 48 83 EC 28 48 63 D9 83 FB 34` | `31 C0 C3 90 90 90 90 90 90 90 90 90` | `xor eax, eax; ret` — every feature's minimum required plan returns `0` |

### Why Both Layer Patches Are Used
- **Patches A1–A3** ensure `LicenseController` publishes `ApplicableLicensePlan = 300` (`Pro Unlimited`) and `IsPaidLicense = true` to XAML bindings, upgrade modals, and toolbar state listeners.
- **Patches B & C** ensure direct `IsProPlanFeatureEnabled(ProPlanFeature)` and `GetMinimumLicensePlan(ProPlanFeature)` calls always succeed unconditionally.

---

## 2. Manifest & Offline Hardening (`AppxManifest.xml`)
Compared against `AppxManifest.xml.original_backup`, the manifest omits:
- `Microsoft.Services.Store.Engagement` dependency
- `GoogleAnalytics.*` and `Microsoft.BingAds.UETSdk.*` activatable classes
- `windows.appUriHandler` (`pdf.drawboard.com`)
- `internetClient` and `privateNetworkClientServer` capabilities

Identity used for signing:
```xml
<Identity Name="DRAWBOARD.DRAWBOARDPDF" Publisher="CN=3CCF3A32-EA16-47A1-B27A-BC4D10D7E322" Version="6.92.3.0" ProcessorArchitecture="x64" />
```

---

## 3. Packing & Signing the MSIX (`build_signed_msix.ps1`)
Loose-file registration (`Add-AppxPackage -Register AppxManifest.xml`) requires **Developer Mode** (`AllowDevelopmentWithoutDevLicense = 1`).
To install and run **without Developer Mode**, Windows relies on standard trusted sideloading (`AllowAllTrustedApps = 1`, enabled by default on Windows 10/11), which requires a packed, certificate-signed `.msix`:

1. **Clean Staging**: Copy `AppFiles` excluding stale store signature/split files (`SplitScale100/`, `AppxMetadata/`, `AppxSignature.p7x*`, `AppxBlockMap.xml`, `[Content_Types].xml`, `*.original_backup`, `*.bak`).
2. **Pack (`makeappx.exe`)**:
   Using Windows SDK `10.0.26100.0\x64\makeappx.exe`:
   ```powershell
   makeappx.exe pack /o /d <stage> /p DrawboardPDF_Pro.msix
   ```
   (Produces a `103.47 MB` `.msix` containing `1,523` files.)
3. **Self-Signed Certificate**:
   Subject **must** exactly match `Identity.Publisher` (`CN=3CCF3A32-EA16-47A1-B27A-BC4D10D7E322`), with EKU `1.3.6.1.5.5.7.3.3` (Code Signing):
   ```powershell
   New-SelfSignedCertificate -Type Custom -Subject "CN=3CCF3A32-EA16-47A1-B27A-BC4D10D7E322" ...
   ```
   Export public `.cer` (bundled in the installer) and temporary `.pfx` (deleted after signing).
4. **Sign (`signtool.exe`)**:
   ```powershell
   signtool.exe sign /fd SHA256 /f DrawboardPro.pfx /p <pass> DrawboardPDF_Pro.msix
   ```

---

## 4. Compiling the Zero-Console GUI `.exe` (`build_gui_exe.ps1`)
To eliminate any terminal/console window during installation, `SetupGuiInstaller.cs` uses **Windows Forms** and is compiled with `.NET Framework 4` `csc.exe` using **`/target:winexe`**:

1. **Payload Zip (`payload.zip`)**:
   Bundles `DrawboardPDF_Pro.msix`, `DrawboardPro.cer`, and `Dependencies\Microsoft.UI.Xaml.2.8.appx` (`CompressionLevel.NoCompression` since `.msix`/`.appx` are already compressed).
2. **UAC Elevation (`setup.manifest`)**:
   Embeds `<requestedExecutionLevel level="requireAdministrator" uiAccess="false"/>`.
3. **Compilation (`csc.exe`)**:
   ```powershell
   csc.exe /target:winexe /platform:x64 /optimize+ /out:DrawboardPDF_Pro_Setup.exe `
     /win32manifest:setup.manifest /reference:System.IO.Compression.FileSystem.dll `
     /reference:System.IO.Compression.dll /reference:System.Windows.Forms.dll `
     /reference:System.Drawing.dll /resource:payload.zip,payload.zip SetupGuiInstaller.cs
   ```
4. **Runtime Execution Flow**:
   - Displays a native WinForms progress window (no console).
   - Extracts `payload.zip` to `%TEMP%\DrawboardProSetup_<id>`.
   - Imports `DrawboardPro.cer` into `LocalMachine\Root` and `LocalMachine\TrustedPeople` via `X509Store`.
   - Sets `AllowAllTrustedApps = 1` (does **not** enable Developer Mode).
   - Invokes `powershell.exe` with `CreateNoWindow = true` and `WindowStyle = Hidden` to remove any older dev-mode registration, install `Microsoft.UI.Xaml.2.8.appx`, and deploy `DrawboardPDF_Pro.msix` via `Add-AppxPackage`.
   - Cleans up the temporary directory on completion.
