# Patched Files — Simple Beginner's Guide

This folder has the patched files to unlock **Drawboard PDF (v6.92.3) Pro Unlimited**.

## What You Need to Download
1. **Original App Files:** Download **`DrawboardPDF_v6.92.3_Original.zip`** from the [Releases page (v1.0.0)](https://github.com/musabtvt/drawboard-re-guide/releases/tag/v1.0.0).
2. **Patched Files (in this folder):**
   * `Db.App.dll.zip`
   * `AppxManifest.xml`

---

## Step 1: Extract the Files
1. Extract **`DrawboardPDF_v6.92.3_Original.zip`** into a folder (for example, `C:\DrawboardPatched`).
   Inside it, you will see `AppFiles` and `Dependencies`.
2. Extract **`Db.App.dll.zip`** (right-click -> **Extract All...**) to get `Db.App.dll`.
3. Copy **`Db.App.dll`** and **`AppxManifest.xml`** into `C:\DrawboardPatched\AppFiles\`, and click **Replace** when Windows asks.

---

## Step 2: Install (Choose One Option)

### Option A: Quick Install (Easiest — Uses Developer Mode)
1. Open Windows **Settings** -> **System** -> **For developers** -> turn **Developer Mode** **On**.
2. Open `C:\DrawboardPatched\AppFiles`, right-click inside the empty space, and click **Open in Terminal** (or PowerShell).
3. Copy and paste this command and press **Enter**:
   ```powershell
   Add-AppxPackage -Register .\AppxManifest.xml
   ```
4. Done! Open **Drawboard PDF** from your Start Menu.

---

### Option B: Build a Single `.exe` Installer (No Developer Mode Needed)
If you want a single `Setup.exe` file that works on any Windows 10/11 PC without Developer Mode:
1. Make sure `C:\DrawboardPatched\AppFiles` has the replaced `Db.App.dll` and `AppxManifest.xml`.
2. Run [`../scripts/build_signed_msix.ps1`](../scripts/build_signed_msix.ps1) to create a signed `.msix` package and `.cer` certificate.
3. Run [`../scripts/build_gui_exe.ps1`](../scripts/build_gui_exe.ps1) to bundle everything into a single **`DrawboardPDF_Pro_Setup.exe`** with a simple graphical installer.
4. Double-click `DrawboardPDF_Pro_Setup.exe` and click **Install**.
