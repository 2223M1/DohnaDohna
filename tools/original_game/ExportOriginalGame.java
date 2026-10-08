// Export static analysis only. This script never launches the imported program.
// @category DohnaDohna.Reference
import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.DecompInterface;
import ghidra.app.decompiler.DecompileResults;
import ghidra.program.model.listing.*;
import ghidra.program.model.symbol.*;
import ghidra.program.model.address.Address;
import java.io.*;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.util.*;
import com.google.gson.GsonBuilder;

public class ExportOriginalGame extends GhidraScript {
    private String csv(String value) { return "\"" + value.replace("\"", "\"\"").replace("\r", " ").replace("\n", " ") + "\""; }

    public void run() throws Exception {
        String[] args = getScriptArgs();
        if (args.length != 1) throw new IllegalArgumentException("Expected one output directory");
        Path root = Paths.get(args[0]).toAbsolutePath().normalize();
        Path code = root.resolve("functions"), asm = root.resolve("assembly");
        Files.createDirectories(code); Files.createDirectories(asm);
        DecompInterface decompiler = new DecompInterface();
        decompiler.setSimplificationStyle("decompile");
        if (!decompiler.openProgram(currentProgram)) throw new IOException(decompiler.getLastMessage());
        int total = 0, complete = 0, failed = 0, external = 0;
        List<Map<String, String>> errors = new ArrayList<>();
        try (BufferedWriter index = Files.newBufferedWriter(root.resolve("functions.csv"), StandardCharsets.UTF_8);
             BufferedWriter calls = Files.newBufferedWriter(root.resolve("cross_references.csv"), StandardCharsets.UTF_8)) {
            index.write("address,name,body_bytes,external,thunk,pseudocode_status,code,assembly,error\n");
            calls.write("from_address,to_address,type,from_function\n");
            FunctionIterator iterator = currentProgram.getFunctionManager().getFunctions(true);
            while (iterator.hasNext()) {
                monitor.checkCancelled();
                Function function = iterator.next(); total++;
                String address = function.getEntryPoint().toString();
                String stem = "f_" + address;
                String status, error = "", cPath = "", aPath = "assembly/" + stem + ".asm";
                try (BufferedWriter output = Files.newBufferedWriter(asm.resolve(stem + ".asm"), StandardCharsets.UTF_8)) {
                    output.write("; " + function.getName(true) + " @ " + address + "\n");
                    InstructionIterator instructions = currentProgram.getListing().getInstructions(function.getBody(), true);
                    while (instructions.hasNext()) {
                        Instruction instruction = instructions.next();
                        output.write(instruction.getAddress() + "  " + instruction + "\n");
                        for (Reference reference : instruction.getReferencesFrom()) {
                            if (reference.getReferenceType().isFlow() || reference.getReferenceType().isData()) {
                                calls.write(csv(instruction.getAddress().toString()) + "," + csv(reference.getToAddress().toString()) + "," +
                                            csv(reference.getReferenceType().toString()) + "," + csv(address) + "\n");
                            }
                        }
                    }
                }
                if (function.isExternal()) { status = "external"; external++; }
                else {
                    DecompileResults result = decompiler.decompileFunction(function, 30, monitor);
                    if (result.decompileCompleted() && result.getDecompiledFunction() != null) {
                        cPath = "functions/" + stem + ".c";
                        Files.writeString(code.resolve(stem + ".c"),
                            "/* Recovered pseudocode, not original source. " + function.getName(true) + " @ " + address + " */\n" +
                            result.getDecompiledFunction().getC(), StandardCharsets.UTF_8);
                        status = "ok"; complete++;
                    } else {
                        status = "failed"; failed++; error = result.getErrorMessage();
                        Map<String, String> item = new LinkedHashMap<>();
                        item.put("address", address); item.put("name", function.getName(true)); item.put("error", error); errors.add(item);
                    }
                }
                index.write(csv(address) + "," + csv(function.getName(true)) + "," + function.getBody().getNumAddresses() + "," +
                    function.isExternal() + "," + function.isThunk() + "," + csv(status) + "," + csv(cPath) + "," + csv(aPath) + "," + csv(error) + "\n");
                if (total % 500 == 0) println("EXPORTED " + total + " functions; " + complete + " pseudocode; " + failed + " failures");
            }
        } finally { decompiler.dispose(); }
        try (BufferedWriter symbols = Files.newBufferedWriter(root.resolve("symbols.csv"), StandardCharsets.UTF_8)) {
            symbols.write("address,name,type,source\n");
            SymbolIterator iterator = currentProgram.getSymbolTable().getAllSymbols(true);
            while (iterator.hasNext()) {
                Symbol symbol = iterator.next();
                symbols.write(csv(symbol.getAddress().toString()) + "," + csv(symbol.getName(true)) + "," +
                              csv(symbol.getSymbolType().toString()) + "," + csv(symbol.getSource().toString()) + "\n");
            }
        }
        Map<String, Object> report = new LinkedHashMap<>();
        report.put("program", currentProgram.getName()); report.put("executable_sha256", currentProgram.getExecutableSHA256());
        report.put("language", currentProgram.getLanguageID().toString()); report.put("identified_functions", total);
        report.put("pseudocode_ok", complete); report.put("pseudocode_failed", failed); report.put("external_functions", external);
        report.put("errors", errors); report.put("original_source_claimed", false); report.put("game_executed", false);
        Files.writeString(root.resolve("coverage.json"), new GsonBuilder().setPrettyPrinting().create().toJson(report) + "\n", StandardCharsets.UTF_8);
        println("COMPLETE " + currentProgram.getName() + ": " + total + " identified functions");
    }
}
