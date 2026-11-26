import ghidra.app.decompiler.DecompInterface;
import ghidra.app.decompiler.DecompileResults;
import ghidra.app.util.headless.HeadlessScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Function;
import ghidra.app.cmd.disassemble.DisassembleCommand;
import ghidra.app.cmd.function.CreateFunctionCmd;

public class DecompAddr extends HeadlessScript {
    @Override
    public void run() throws Exception {
        String[] args = getScriptArgs();
        DecompInterface ifc = new DecompInterface();
        ifc.openProgram(currentProgram);
        for (String a : args) {
            Address addr = toAddr(a);
            Function f = getFunctionAt(addr);
            if (f == null) {
                runCommand(new DisassembleCommand(addr, null, true));
                runCommand(new CreateFunctionCmd(addr));
                f = getFunctionAt(addr);
            }
            println("=== FUNC " + a + " (" + (f != null ? f.getName() : "null") + ") ===");
            if (f != null) {
                DecompileResults res = ifc.decompileFunction(f, 30, monitor);
                if (res.decompileCompleted()) {
                    println(res.getDecompiledFunction().getC());
                } else {
                    println("Decompile failed: " + res.getErrorMessage());
                }
            }
        }
        ifc.dispose();
    }
}
