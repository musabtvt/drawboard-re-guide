# Drawboard PDF v6.92.3 — Complete Reverse Engineering, Patching & Standalone GUI Packaging Archive

This archive (`drawboard_archive`) preserves both the **untouched original** Drawboard PDF v6.92.3 offline package, the **surgically patched** `Db.App.dll` (unlocking **Pro Unlimited** / tier `300`), every reproducible script, and the complete technical documentation covering how it was analyzed, patched, and compiled into a single standalone GUI `.exe` installer that runs **without Developer Mode** and **without opening any terminal window**.

## Directory Layout
```text
/home/ubuntu/drawboard_archive/
├── README.md                                  # This overview
├── original/                                  # Untouched original DrawboardPDF_Offline_Package
│   ├── AppFiles/
│   │   ├── Db.App.dll                         # Original unpatched 123.5 MB binary
│   │   ├── AppxManifest.xml                   # Original Store manifest
│   │   └── ...
│   └── Dependencies/
├── patched/
│   ├── Db.App.dll                             # Final patched Db.App.dll (Pro Unlimited = 300)
│   └── AppxManifest.xml                       # Hardened offline manifest (analytics/store/internet removed)
├── scripts/
│   ├── apply_pro_unlimited_patch.py           # Exact byte-level binary patcher
│   ├── DecompAddr.java                        # Ghidra headless single-function decompiler script
│   ├── DecompChain.java                       # Ghidra headless multi-function decompiler script
│   ├── build_signed_msix.ps1                  # Stages clean AppFiles, packs MSIX, creates cert, signs
│   ├── build_gui_exe.ps1                      # Bundles signed MSIX + cert + dep into single GUI .exe
│   ├── SetupGuiInstaller.cs                   # WinForms zero-console installer source
│   └── setup.manifest                         # UAC requireAdministrator manifest
└── docs/
    ├── 01_INFRASTRUCTURE_AND_GHIDRA_SETUP.md  # SSH tunnel, build-server specs, OpenJDK 25, Ghidra 12.2, REA analysis
    ├── 02_BINARY_ARCHITECTURE_AND_RE.md       # Project N internals, FrozenStrings, LEA xrefs, LicenseController RE
    └── 03_PATCHES_AND_EXE_PACKAGING.md        # Exact byte patch table, MSIX signing, and GUI .exe compilation
```

## Summary of Results
- **Target Binary**: `Db.App.dll` (`129,478,656` bytes, x64 PE32+, UWP .NET Native / Project N)
- **Root Cause Solved**: Previous heuristic patching hit an unrelated `async MoveNext()` state machine (`0x04D494CD`). Using Ghidra 12.2 + `.data` `FrozenString` xref analysis, we located `LicenseController.SynchroniseApplicableLicensePlan` (`0x186d274a0`), `LicenseController.IsProPlanFeatureEnabled` (`0x186d277d0`), and `ProPlanFeatureExtensions.GetMinimumLicensePlan` (`0x186ab9c00`).
- **Final State**: `ApplicableLicensePlan` forced to `300` (**Pro Unlimited**), `IsPaidLicense = true`, `AppActivationType = 2` (`WindowsStore`), `IsProPlanFeatureEnabled` always returns `1`, and `GetMinimumLicensePlan` always returns `0`.
- **Distribution**: Single self-extracting WinForms GUI installer (`DrawboardPDF_Pro_Setup.exe`, `106.99 MB`) with an embedded signed MSIX and self-signed certificate (`CN=3CCF3A32-EA16-47A1-B27A-BC4D10D7E322`) — installs cleanly with **Developer Mode OFF** and **no terminal window**.
