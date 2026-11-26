# 02 — Binary Architecture & Reverse Engineering Findings

## 1. PE Layout of `Db.App.dll`
- **File Size**: `129,478,656` bytes (`123.5 MiB`)
- **Image Base**: `0x180000000`
- **Key Sections**:
  - `.rdata`: `VA = 0x1000`, `RawOff = 0x600`, `RawSize = 0x39E4800` (metadata strings, `EEType`s, vtable RVA tables)
  - `.data`: `VA = 0x39E6000`, `RawOff = 0x39E4E00`, `RawSize = 0x6F7E00` (**`FrozenString` objects**, static fields, Redhawk/Project N call thunks)
  - `.pdata`: `VA = 0x40DE000`, `RawOff = 0x40DCC00`, `RawSize = 0x324C00` (`274,688` `RUNTIME_FUNCTION` entries for exact function boundaries)
  - `.text`: `VA = 0x440D000`, `RawOff = 0x440B200`, `RawSize = 0x3379600`
- **PE Exports**:
  - `RHBinder__ShimExeMain`: `0x1844D0B30` (called by `Db.App.exe`)
  - `DllGetActivationFactory`: `0x184F7EC90`
  - `DllGetClassObject`: `0x184F7EBE0`

## 2. Why the Earlier Heuristic Patch (`0x04D494CD`) Failed
Before Ghidra decompilation, a byte-pattern scan for `test r12d, r12d; je ...` (`45 85 E4 0F 84`) flagged `fo=0x04D494CD` inside `0x184D4B250` as `LicensePlanToAnythingConverter.Convert`.
When we decompiled `0x184D4B250` and its caller `0x184D4B0C0` in Ghidra, we proved:
- `0x184D4B0C0` initializes `uStack_58 = 0xffffffff` (`-1`) and calls `FUN_184d4b250(&uStack_58)`.
- `FUN_184d4b250` checks `iVar3 = *param_1`, transitions `*param_1 = 0`, `*param_1 = 1`, `*param_1 = -2`, and throws `"Bounds"` / `"Anonymous task"` (`0x183b19900`).
- **Conclusion**: `0x184D4B250` was an unrelated C# `async` state machine `MoveNext()` checking `<>1__state`, NOT a license check. It was immediately reverted to its original bytes (`0F 84 9A 01 00 00`).

## 3. Discovering `FrozenString` Objects in `.data`
Ghidra's decompilation of `0x184d4b250` revealed how UWP .NET Native stores C# string literals:
- Instead of raw UTF-16 or UTF-8 pointers in `.rdata`, managed string literals are **`FrozenString` heap objects in `.data`** (`0x39E4E00 .. 0x40DCC00`):
  - `+0x00` (8 bytes): `System.String` `EEType` pointer = **`0x1801EA700`**
  - `+0x08` (4 bytes): `uint32` character length (`slen`)
  - `+0x0C` (`slen * 2` bytes): UTF-16LE string payload
- Scanning `.data` for `0x1801EA700` recovered **`38,032` `FrozenString` objects**, including:
  - Embedded C# compiler source paths (`C:\a\1\s\Pdf\Db.DataModel\Extensions\ProPlanFeatureExtensions.cs`, `WindowsStoreService.cs`, `StoreContextLicenseService.cs`, `ProUpgradeDetailsViewModel.cs`, etc.)
  - Exact license property/tier names (`ApplicableLicensePlan`, `WindowsStoreLicensePlan`, `UnpaidLicensePlanValue`, `PaidLicensePlanTier1Value`, `PaidLicensePlanTier2Value`, `PaidLicensePlanTier3Value`, `IsProUpgradeRequired`, `IsProFeatureEnabled`, `Pro Plus`).

## 4. Mapping `.text` RIP-Relative `LEA` Xrefs to Ghidra Decompilation
Scanning `.text` for `48 8D` / `4C 8D` (`LEA reg, [RIP + disp32]`) targeting those `FrozenString` VAs and resolving function boundaries via `.pdata` pinpointed the exact licensing subsystem:

### A. `LicensePlan` Enum Values (Decoded from `FUN_186a0b460` & `FUN_186d1e7b0`)
| Value | Hex | UI / Plan Name | Internal Constant |
|---|---|---|---|
| `0` | `0x0` | `UserWithLicensePlanUnpaid` | `UnpaidLicensePlanValue` |
| `30` | `0x1e` | `Legacy` | `LicensePlanProLegacyValue` |
| `100` | `0x64` | `Pro Lite` | `PaidLicensePlanTier1Value` |
| `190` | `0xbe` | `Pro Edu` | `LicensePlanProEduValue` |
| `200` | `0xc8` | `Pro Plus` | `PaidLicensePlanTier2Value` |
| `300` | `0x12c` | `Pro Unlimited` | `PaidLicensePlanTier3Value` |

### B. `ProPlanFeatureExtensions.GetMinimumLicensePlan` (`0x186ab9c00`)
Referenced `C:\a\1\s\Pdf\Db.DataModel\Extensions\ProPlanFeatureExtensions.cs`.
A jump-table `switch(ProPlanFeature param_1)` covering cases `0..0x33` returning:
- `0x1e` (`30`) for cases `0x1c, 0x1f, 0x20`
- `100` (`0x64`) for cases `0xc, 0xd, 0xf, 0x10, 0x11, 0x13, ...`
- `0xbe` (`190`) for cases `0..0xb, ...`
- `200` (`0xc8`) for case `0x30`

### C. `LicenseController` (`0x186d26940 .. 0x186d2883c`)
1. **`LicenseController.IsProPlanFeatureEnabled` (`0x186d277d0`)**:
   ```c
   bool IsProPlanFeatureEnabled(this, ProPlanFeature feature) {
     if (IsOverride()) return true;
     int userPlan = *(int *)(this + 0x44); // _applicableLicensePlan
     int minPlan  = FUN_186ab9c00(feature); // GetMinimumLicensePlan
     return minPlan <= userPlan;
   }
   ```
   Confirmed via callers such as `FUN_1866c0240` (checking features `0x22, 0x18, 0x19, 0x26, 0x2d, 0x25, 0x28, 0x32, 0x1a, 9`) and `FUN_18616cea0` (setting `IsProUpgradeRequired = !IsProPlanFeatureEnabled(0x13)`).

2. **`LicenseController.SynchroniseApplicableLicensePlan` (`0x186d274a0`)**:
   Computes the active license from Windows Store (`WindowsStoreLicensePlan`), Drawboard Account (`AccountLicensePlan`), and Enterprise override:
   - Checks Enterprise license (`r12d = 0x12c` = `300` = **Pro Unlimited**, cleared to `0` via `cmove r12d, edi` if absent).
   - If Enterprise override is false (`je 0x186d2757e`), falls back to `max(AccountPlan, WindowsStorePlan)`.
   - Writes `*(int *)(this + 0x44)` (`ApplicableLicensePlan`), `*(int *)(this + 0x40)` (`AppActivationType`: `2`=WindowsStore, `4`=Enterprise, `5`=Account, `6`=Trial), and `*(bool *)(this + 0x48)` (`IsPaidLicense = (0 < *piVar1)`), firing `PropertyChanged` events for all UI subscribers.
