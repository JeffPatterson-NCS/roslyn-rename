Perform a semantic C# symbol rename using Roslyn. Do NOT grep, text-search, or edit files manually — the `roslyn-rename` tool resolves all references via Roslyn's rename engine, identical to what VS or Rider would produce.

## Arguments

$ARGUMENTS

Expected format: `OldSymbolName NewSymbolName [--kind class|interface|struct|enum|delegate|type|method|property|field|event|namespace] [--dry-run] [--allow-partial]`

## Steps

1. **Check the tool is installed.** Run:
   ```
   roslyn-rename --help
   ```
   If the command is not found, tell the user to install it from https://github.com/JeffPatterson-NCS/roslyn-rename and stop.

2. **Find the solution file.** Use Glob to find `*.slnx` first, then fall back to `*.sln`, searching up from the current working directory. Prefer `.slnx` over `.sln`. If still ambiguous, show the list and ask the user to specify.

3. **Run the rename:**
   ```
   roslyn-rename --solution "<sln-path>" --old "OldName" --new "NewName" [--kind <kind>] [--dry-run] [--allow-partial]
   ```

4. **Handle ambiguity.** If the tool exits listing multiple matches, show them to the user and ask them to re-run with `--kind` to narrow it.

5. **Handle partial load failures.** If the tool reports project load errors, tell the user which projects failed. They can add `--allow-partial` to proceed, but warn that references in unloaded projects will be missed.

6. **Report results.** State how many files were modified (or would be with `--dry-run`). If any `.csproj` or assembly-level files changed, remind the user to rebuild.
