# roslyn-rename

A .NET global tool that performs semantic C# symbol rename using Roslyn's rename engine — the same engine Visual Studio uses. Renames a symbol and all its references across an entire solution without text-searching files.

## Why

AI coding tools that rename symbols by grepping files get it wrong. They miss references in generated code, hit false positives in comments and strings, and don't understand C# semantics. This tool loads your solution into a Roslyn workspace and calls `Renamer.RenameSymbolAsync`, so the result is identical to what VS or Rider would produce.

It's designed to be called by a Claude Code skill (`/rename`) so the AI never has to read and edit individual files to perform a rename.

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

## Claude Code skill

A `/rename` skill is included for use with [Claude Code](https://claude.ai/code). Copy it to your global commands directory:

```
copy .claude\commands\rename.md %USERPROFILE%\.claude\commands\rename.md
```

Then in any C# project session:

```
/rename OldName NewName
/rename OldName NewName --kind class
/rename ProcessOrder HandleOrder --kind method --dry-run
```

Claude will find the solution file, run the tool, and report results. No file scanning, no token-expensive grep loops.

## How it works

1. `MSBuildLocator.RegisterDefaults()` locates the installed MSBuild/SDK.
2. `MSBuildWorkspace.OpenSolutionAsync` loads the full solution. Project load failures are surfaced immediately.
3. `compilation.GetSymbolsWithName` searches for source-defined symbols matching the name across all projects.
4. `Renamer.RenameSymbolAsync` computes the full set of edits needed across all documents.
5. Changed documents are written back to disk with their original encoding preserved.

## License

MIT
