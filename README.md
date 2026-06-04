# roslyn-rename

A .NET global tool that performs semantic C# symbol rename using Roslyn's rename engine — the same engine Visual Studio uses. Renames a symbol and all its references across an entire solution without text-searching files.

## Why

AI coding tools that rename symbols by grepping files get it wrong. They miss references in generated code, hit false positives in comments and strings, and don't understand C# semantics. This tool loads your solution into a Roslyn workspace and calls `Renamer.RenameSymbolAsync`, so the result is identical to what VS or Rider would produce.

It's designed to be called by a Claude Code skill so the AI never has to read or edit individual files to perform a rename. Two skill aliases are included: `/cs-ren` (Windows convention, `ren` = rename) and `/cs-mv` (Unix convention, `mv` = move/rename).

## Requirements

- .NET 10 SDK or later
- A `.sln` or `.slnx` solution file

## Installation

```
git clone https://github.com/JeffPatterson-NCS/roslyn-rename
cd roslyn-rename
dotnet pack -o nupkg
dotnet tool install --global --add-source ./nupkg RoslynRename
```

Verify:

```
roslyn-rename --help
```

## Updating

After pulling changes and rebuilding:

```
dotnet pack -o nupkg
dotnet tool update --global --add-source ./nupkg RoslynRename
```

## Usage

```
roslyn-rename --solution <path> --old <name> --new <name> [options]
```

| Flag | Short | Description |
|---|---|---|
| `--solution` | `-s` | Path to the `.sln` or `.slnx` file |
| `--old` | `-o` | Symbol name to rename |
| `--new` | `-n` | New symbol name |
| `--kind` | `-k` | Narrow by kind (see below) |
| `--dry-run` | | Print what would change without writing files |
| `--allow-partial` | | Proceed even if some projects fail to load |

### --kind values

`class` `interface` `struct` `enum` `delegate` `type` `method` `property` `field` `event` `namespace`

Use `type` to match any named type regardless of kind.

## Examples

Rename a class across the solution:

```
roslyn-rename -s ./MyApp.slnx -o OldClassName -n NewClassName --kind class
```

Preview what would change without writing:

```
roslyn-rename -s ./MyApp.slnx -o ProcessOrder -n HandleOrder --kind method --dry-run
```

Rename a property shared across multiple projects:

```
roslyn-rename -s ./MyApp.sln -o IsActive -n IsEnabled --kind property
```

If the name matches multiple symbols, the tool lists them and exits:

```
Multiple symbols named 'Process' — use --kind to narrow:
  Method      MyApp.OrderService.Process()              [MyApp.Core]
               C:\src\MyApp\OrderService.cs
  Class       MyApp.Background.Process                  [MyApp.Workers]
               C:\src\MyApp\Process.cs
```

## Partial solution loads

If one or more projects in the solution fail to load (missing SDK, unrestored packages, unsupported project type), the tool will print the failures and exit rather than silently producing an incomplete rename. Pass `--allow-partial` to proceed anyway — references in the unloaded projects will not be updated.

## Claude Code skills

Two skill aliases are included. Copy them to your global commands directory:

Run the following from inside the `roslyn-rename` repo directory:

**Windows (PowerShell):**
```powershell
New-Item -ItemType Directory -Force "$env:USERPROFILE\.claude\commands"
Copy-Item .claude\commands\cs-ren.md "$env:USERPROFILE\.claude\commands\cs-ren.md"
Copy-Item .claude\commands\cs-mv.md "$env:USERPROFILE\.claude\commands\cs-mv.md"
```

**Linux / macOS:**
```bash
mkdir -p ~/.claude/commands
cp .claude/commands/cs-ren.md ~/.claude/commands/cs-ren.md
cp .claude/commands/cs-mv.md ~/.claude/commands/cs-mv.md
```

Use whichever feels natural — they are identical:

```
/cs-ren OldName NewName
/cs-mv OldName NewName --kind class
/cs-ren ProcessOrder HandleOrder --kind method --dry-run
```

Claude will find the solution file, run the tool, and report results. No file scanning, no token-expensive grep loops.

## How it works

1. `MSBuildLocator.RegisterDefaults()` locates the installed MSBuild/SDK.
2. `MSBuildWorkspace.OpenSolutionAsync` loads the full solution. Project load failures are surfaced immediately.
3. `compilation.GetSymbolsWithName` searches for source-defined symbols matching the name across all projects.
4. `Renamer.RenameSymbolAsync` computes the full set of edits needed across all documents.
5. Changed documents are written back to disk with their original encoding preserved.

## FAQ

**Does Claude automatically use this tool when I ask it to rename something?**

No. By default Claude doesn't know it exists, so it will fall back to finding references with LSP and editing files one by one. To make it the default for all C# rename requests, add this to your global `~/.claude/CLAUDE.md`:

```markdown
## C# Rename
Always use `/cs-ren` for any C# symbol rename (class, method, property, field, interface, enum).
Never grep or text-search files to find references — use the tool.
```

After that, saying "rename method `Foo` to `Bar`" will trigger the skill automatically rather than file-by-file edits.

**Can I still call it explicitly?**

Yes — `/cs-ren OldName NewName` and `/cs-mv OldName NewName` work regardless of whether the CLAUDE.md instruction is present.

**Why does it need a `.sln` or `.slnx` file?**

Roslyn loads the full project graph through MSBuild to resolve references correctly. A standalone `.csproj` doesn't give it enough context for cross-project renames. If you only have a single project with no solution file, create one: `dotnet new sln && dotnet sln add YourProject.csproj`.

**What if only some projects in my solution load?**

The tool exits with an error listing the failed projects rather than silently producing a partial rename. Fix the load errors first (usually a missing SDK or unrestored packages), or pass `--allow-partial` if you're certain the failures don't contain references to the symbol you're renaming.

## License

MIT
