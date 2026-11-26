# Patched Files — Beginner's Guide

This folder contains the pre-patched files needed to run **Drawboard PDF (v6.92.3)** with **Pro Unlimited** features unlocked.

## What's Inside
1. **`Db.App.dll.zip`** — Compressed version of the patched `Db.App.dll` (compressed to 42 MB to fit GitHub's file size limits).
2. **`AppxManifest.xml`** — The updated Windows app manifest.

---

## Step 1: Unzip `Db.App.dll.zip`
Before doing anything else, extract `Db.App.dll.zip`:
* **Using Windows Explorer:** Right-click `Db.App.dll.zip` -> **Extract All...** -> extract it into this same `patched/` folder so you have `Db.App.dll`.
* **Using PowerShell:**
  ```powershell
  Expand-Archive -Path .\Db.App.dll.zip -DestinationPath . -Force
  ```

---

## Step 2: Choose How You Want to Install

### Option A: Quick Install (Easiest, requires Developer Mode)
Use this method if you just want to install it quickly on your own PC.

1. **Get the original Drawboard PDF v6.92.3** `.msix` / `.appx` package and extract its contents into a folder (for example, `C:\DrawboardPatched`).
   *(Tip: You can rename `.msix` to `.zip` and extract it, or use 7-Zip.)*
2. **Copy the patched files:** Copy `Db.App.dll` and `AppxManifest.xml` from this `patched/` folder into `C:\DrawboardPatched`, and click **Replace** when asked.
3. **Turn on Developer Mode:**
   * Open Windows **Settings** -> **System** -> **For developers** (or search "Developer settings" in the Start menu).
   * Turn **Developer Mode** **On**.
4. **Install:**
   * Open PowerShell inside `C:\DrawboardPatched` (Right-click inside the folder -> *Open in Terminal*).
   * Run this command:
     ```powershell
     Add-AppxPackage -Register .\AppxManifest.xml
     ```
5. Open **Drawboard PDF** from the Start Menu — Pro Unlimited is now unlocked!

---

### Option B: Build a Standalone `.exe` Installer (No Developer Mode Needed)
Use this method if you want a single `Setup.exe` file that anyone can double-click to install without turning on Developer Mode.

1. Prepare the `C:\DrawboardPatched` folder just like in **Option A (Steps 1 & 2)** (original files + `Db.App.dll` and `AppxManifest.xml` replaced).
2. Look in the [`../scripts/`](../scripts/) folder of this repository:
   * **`build_signed_msix.ps1`**: Packages `C:\DrawboardPatched` into a `.msix` installer and creates a self-signed `.cer` certificate.
   * **`build_gui_exe.ps1`**: Bundles the `.msix`, the `.cer` certificate, and `SetupGuiInstaller.cs` into a single standalone **`DrawboardPDF_Pro_Setup.exe`** with a clean graphical installer (no black terminal window).
3. Double-click `DrawboardPDF_Pro_Setup.exe` on any Windows 10/11 PC and click **Install**.

---

## Want to Know How This Works?
Check out the [`../docs/`](../docs/) folder for the full step-by-step technical breakdown of how the binary was reverse-engineered, patched, and packaged.
