"""
Apply verified Pro Unlimited (300) + feature-unlock patches to Db.App.dll
Restores from .original_backup (or verifies exact original bytes) before patching.
"""
import shutil
import os

targets = [
    r"C:\Users\<USERNAME>\Downloads\DrawboardPDF_Offline_Package\AppFiles\Db.App.dll",
    r"C:\Users\<USERNAME>\Downloads\drawboard_build\AppFiles\Db.App.dll",
]
backup_src = r"C:\Users\<USERNAME>\Downloads\DrawboardPDF_Offline_Package\AppFiles\Db.App.dll.original_backup"

patches = [
    # Revert previous incorrect Patch 1 (was an async state machine MoveNext, not LicensePlan)
    # Wait: since we start from .original_backup (or verify), 0x04D494CD stays original 0F 84 9A 01 00 00.
    {
        "name": "Verify 0x04D494CD is original (async MoveNext)",
        "fo": 0x04D494CD,
        "orig": bytes.fromhex("0f849a010000"),
        "patch": bytes.fromhex("0f849a010000"),
    },
    {
        "name": "LicenseController.SynchroniseApplicableLicensePlan: force r12d=300 (Pro Unlimited)",
        "fo": 0x06D2574A,
        "orig": bytes.fromhex("440f44e7"),       # cmove r12d, edi
        "patch": bytes.fromhex("90909090"),      # nop x4
    },
    {
        "name": "LicenseController.SynchroniseApplicableLicensePlan: always use r12d (300)",
        "fo": 0x06D25777,
        "orig": bytes.fromhex("7405"),           # je 0x186d2757e
        "patch": bytes.fromhex("9090"),          # nop x2
    },
    {
        "name": "LicenseController.SynchroniseApplicableLicensePlan: AppActivationType=2 (WindowsStore)",
        "fo": 0x06D257A8,
        "orig": bytes.fromhex("7407"),           # je 0x186d275b1
        "patch": bytes.fromhex("eb13"),          # jmp 0x186d275bd (mov edi, 2)
    },
    {
        "name": "LicenseController.IsProPlanFeatureEnabled: always return true (1)",
        "fo": 0x06D259D0,
        "orig": bytes.fromhex("53574883ec288bfa"),
        "patch": bytes.fromhex("b001c39090909090"),  # mov al, 1; ret
    },
    {
        "name": "ProPlanFeatureExtensions.GetMinimumLicensePlan: always return 0 (Unpaid)",
        "fo": 0x06AB7E00,
        "orig": bytes.fromhex("53574883ec284863d983fb34"),
        "patch": bytes.fromhex("31c0c3909090909090909090"),  # xor eax, eax; ret
    },
]

assert os.path.exists(backup_src), f"Missing backup: {backup_src}"
with open(backup_src, "rb") as f:
    orig_bytes = bytearray(f.read())

for p in patches:
    actual = bytes(orig_bytes[p["fo"] : p["fo"] + len(p["orig"])])
    assert actual == p["orig"], f"Mismatch in backup at 0x{p['fo']:X}: got {actual.hex()}, expected {p['orig'].hex()}"
    orig_bytes[p["fo"] : p["fo"] + len(p["patch"])] = p["patch"]
    print(f"[OK] {p['name']} @ 0x{p['fo']:08X}")

for t in targets:
    if os.path.exists(os.path.dirname(t)):
        with open(t, "wb") as f:
            f.write(orig_bytes)
        print(f"Wrote patched DLL -> {t}")
