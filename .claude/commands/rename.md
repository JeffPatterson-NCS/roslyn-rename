Perform a semantic C# rename using Roslyn. Do NOT grep, text-search, or edit files manually — the `roslyn-rename` tool resolves symbols and updates all references via Roslyn's rename engine.

## Arguments

$ARGUMENTS

Expected format: `OldSymbolName NewSymbolName [--kind class|interface|struct|enum|delegate|type|method|property|field|event|namespace] [--dry-run] [--allow-partial]`

## Steps

1. **Check the tool is installed.** Run:
   ```
   roslyn-rename --help
   ```
   If the command is not found, tell the user to install it:
   ```
   cd C:\Users\jpatterson\Source\Repos\RoslynRename
   dotnet pack -o nupkg
   dotnet tool install --global --add-source ./nupkg RoslynRename
   ```
   Then stop and wait for them to install it.

2. **Find the solution file.** Use Glob to find `*.slnx` first, then fall back to `*.sln`, searching up from the current working directory. If multiple solution files are found, prefer `.slnx` over `.sln` and show the user if there is still ambiguity.

3. **Run the rename:**
   ```
   roslyn-rename --solution "<sln-path>" --old "OldName" --new "NewName" [--kind <kind>] [--dry-run] [--allow-partial]
   ```

4. **Handle ambiguity.** If the tool exits with multiple matches listed, show them to the user and ask them to re-run with `--kind` to narrow the match.

5. **Handle partial load failures.** If the tool reports project load errors and exits, tell the user which projects failed. They can add `--allow-partial` to proceed, but warn that references in unloaded projects will be missed.

6. **Report results.** State how many files were modified (or would be modified with `--dry-run`). If the rename touched `.csproj` or assembly-level files, remind the user to rebuild.
