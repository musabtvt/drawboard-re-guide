# 01 — Infrastructure, Build-Server, REA & Ghidra 12.2 Setup

## 1. Multi-Hop SSH Architecture & Quoting Fix
Per `agent_handover_guide.md`, infrastructure access goes through **Jump Host (VPS)** (`<VPS_IP>`, port `<SSH_PORT>`, user `root`, ed25519 key `<SSH_KEY>`) and then jumps to **`build-server`** (`<BUILD_SERVER_IP>`, port `<SSH_PORT>`, user `root`) defined in `/root/.ssh/config` on Jump Host.

### Crucial Lesson: Nested SSH Quoting from Windows PowerShell
Passing complex double-quoted commands through `PowerShell -> OpenSSH -> bash (Jump Host) -> ssh build-server "cmd1; cmd2"` causes quote stripping where `cmd1` executes on `build-server` (`<BUILD_SERVER_IP>`) and `cmd2` falls back onto Jump Host (`<VPS_INTERNAL_IP>`).
**Reliable Solution**: Base64-encode every remote script in PowerShell and decode into `bash` on `build-server`:
```powershell
$b64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($cmd))
ssh -i "<key>" -p <SSH_PORT> root@<VPS_IP> "ssh build-server 'echo $b64 | base64 -d | bash'"
```

## 2. Build-Server Hardware Constraints
- **Architecture**: `x86_64`, 2 vCPUs, Ubuntu 24.04
- **Memory**: `952 MiB` physical RAM + `4.0 GiB` Swap
- **Disk**: `45 GiB` root ext4 (`~29–31 GiB` free)

Because `Db.App.dll` is **123.5 MiB** (`129,478,656` bytes) with **274,688** `.pdata` function entries, running full Ghidra auto-analysis on 952 MiB RAM would thrash swap and take hours. Instead, we used **targeted `-noanalysis` import + on-demand decompilation** (explained in Section 4).

## 3. How REA (`rea-agents 6.10.1`) Works
`build-server` already had Node.js `v22.23.5` (`/usr/bin/node`), `/usr/bin/rea` (`rea-agents@6.10.1`), and the repository source at `/home/ubuntu/rea` and `/home/ubuntu/rea_repo`.

Key components inspected in `rea`:
- **`src/application/` & `src/domain/`**: TypeScript MCP server and CLI (`scripts/rea.mjs`) exposing 204 CLI commands (`inspect-pe-imports`, `inspect-binary-layout`, `decompile`, `search`, `trace`, etc.).
- **`bridge/ghidra/`**: `ReaGhidraBridge.java` and `ReaGhidraAnalysisOptions.java`, which drive Ghidra headless (`analyzeHeadless`).
- **`bridge/nativeaot/`**: Four Java wrapper classes (`NativeAotDirectory.java`, `NativeAotExtension.java`, `NativeAotTypeMetadata.java`, `NativeAotMethodConventions.java`) wrapping upstream `ghidra-nativeaot` (`ReadyToRunDirectory`, `MetadataRehydratorNet80`, `MethodTableCrawler`).

### Why REA's NativeAOT Adapter Did Not Directly Apply
1. `third_party/ghidra-nativeaot` in the repo is an empty uninitialized submodule.
2. `NativeAotDirectory.java` checks for **.NET 8.0 NativeAOT RTR 9.1** headers (`RTR\0` + `DEHYDRATED_DATA` sections).
3. `Db.App.dll` returned **0 `RTR\0` headers** because Drawboard PDF v6.92.3 is compiled with **UWP .NET Native (Project N / Redhawk)**, not .NET 8 NativeAOT.
4. Additionally, REA's pure-JS `inspect-pe-imports` enforces a `67,108,864` byte (`64 MiB`) default read limit on 124 MiB binaries (`Db.App.exe` succeeded as a 42 KB shim importing `RHBinder__ShimExeMain` from `Db.App.dll`).

## 4. Installing OpenJDK 25 & Ghidra 12.2 on Build-Server
Running `rea doctor` showed Ghidra 12.2 (`>=12.2`) is required, and Ghidra 12.2's `Ghidra/application.properties` specifies `application.java.min=25`.

1. **Ghidra 12.2 Installation**:
   Downloaded `ghidra_12.2_PUBLIC_20261016.zip` from `NationalSecurityAgency/ghidra` releases and extracted to `/opt/ghidra` (`GHIDRA_INSTALL_DIR="/opt/ghidra"` in `/etc/environment`).
2. **OpenJDK 25 Installation**:
   Installed `openjdk-25-jdk-headless` via `apt-get` (`JAVA_HOME="/usr/lib/jvm/java-25-openjdk-amd64"` in `/etc/environment`).
3. **One-Time Project Import (`-noanalysis`)**:
   ```bash
   export GHIDRA_HEADLESS_MAXMEM=1400M
   /opt/ghidra/support/analyzeHeadless /tmp/db_ghidra_proj DbApp \
     -import /home/ubuntu/DrawboardPDF_Offline_Package/AppFiles/Db.App.dll \
     -noanalysis -overwrite
   ```
4. **On-Demand Headless Decompilation (`DecompChain.java`)**:
   Instead of analyzing all 274k functions, Python scripts pinpointed exact target addresses, and `analyzeHeadless ... -process Db.App.dll -noanalysis -postScript DecompChain.java <addrs>` disassembled and decompiled only those exact functions via `DecompInterface` in ~20 seconds per batch.
