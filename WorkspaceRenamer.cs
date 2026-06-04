using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Rename;

internal static class WorkspaceRenamer
{
    public static async Task<int> RunAsync(RenameOptions options, CancellationToken ct = default)
    {
        // CreateWorkspace is a separate call so its body (which references MSBuildWorkspace) is
        // JIT-compiled only after MSBuildLocator.RegisterDefaults() has already run in Program.cs.
        using var workspace = CreateWorkspace();

        Console.WriteLine($"Loading solution: {options.SolutionPath}");
        var solution = await workspace.OpenSolutionAsync(options.SolutionPath, cancellationToken: ct);

        var failures = workspace.Diagnostics
            .Where(d => d.Kind == WorkspaceDiagnosticKind.Failure)
            .ToList();

        if (failures.Count > 0)
        {
            Console.Error.WriteLine($"{failures.Count} project(s) failed to load:");
            foreach (var f in failures)
                Console.Error.WriteLine($"  {f.Message}");

            if (!options.AllowPartial)
            {
                Console.Error.WriteLine("Rename aborted — references in unloaded projects would be missed.");
                Console.Error.WriteLine("Use --allow-partial to proceed anyway.");
                return 1;
            }

            Console.Error.WriteLine("Proceeding with partial solution (--allow-partial).");
        }

        var candidates = await FindCandidatesAsync(solution, options.OldName, options.Kind, ct);

        if (candidates.Count == 0)
        {
            Console.Error.WriteLine($"No source-defined symbol named '{options.OldName}' found.");
            return 1;
        }

        if (candidates.Count > 1)
        {
            Console.Error.WriteLine($"Multiple symbols named '{options.OldName}' — use --kind to narrow:");
            foreach (var (sym, proj) in candidates)
            {
                var location = sym.DeclaringSyntaxReferences.FirstOrDefault()?.SyntaxTree.FilePath ?? "?";
                Console.Error.WriteLine($"  {sym.Kind,-12} {sym.ToDisplayString(),-60} [{proj}]");
                Console.Error.WriteLine($"             {location}");
            }
            return 1;
        }

        var symbol = candidates[0].Symbol;
        Console.WriteLine($"Renaming {symbol.Kind} '{symbol.ToDisplayString()}' → '{options.NewName}'");

        var newSolution = await Renamer.RenameSymbolAsync(
            solution, symbol, new SymbolRenameOptions(), options.NewName, ct);

        return await WriteChangesAsync(solution, newSolution, options.DryRun, ct);
    }

    private static MSBuildWorkspace CreateWorkspace()
    {
        var workspace = MSBuildWorkspace.Create();
        workspace.WorkspaceFailed += (_, e) =>
        {
            var tag = e.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure ? "[error]" : "[warn]";
            Console.Error.WriteLine($"{tag} {e.Diagnostic.Message}");
        };
        return workspace;
    }

    private static async Task<List<(ISymbol Symbol, string ProjectName)>> FindCandidatesAsync(
        Solution solution, string name, string? kindFilter, CancellationToken ct)
    {
        var candidates = new List<(ISymbol, string)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var filter = GetSymbolFilter(kindFilter);

        foreach (var project in solution.Projects)
        {
            if (!project.SupportsCompilation) continue;
            var compilation = await project.GetCompilationAsync(ct);
            if (compilation is null) continue;

            foreach (var sym in compilation.GetSymbolsWithName(name, filter, ct))
            {
                // Skip metadata-only symbols — we need source to rename
                if (sym.DeclaringSyntaxReferences.Length == 0) continue;
                if (kindFilter is not null && !MatchesKind(sym, kindFilter)) continue;

                // Deduplicate: same symbol appears in both the declaring project and
                // any project that references it via project reference
                if (seen.Add(sym.ToDisplayString()))
                    candidates.Add((sym, project.Name));
            }
        }

        return candidates;
    }

    private static SymbolFilter GetSymbolFilter(string? kind) => kind switch
    {
        "method" or "property" or "field" or "event" => SymbolFilter.Member,
        "namespace" => SymbolFilter.Namespace,
        "class" or "interface" or "struct" or "enum" or "delegate" or "type" => SymbolFilter.Type,
        _ => SymbolFilter.All
    };

    private static bool MatchesKind(ISymbol symbol, string kind) => kind switch
    {
        "class"     => symbol is INamedTypeSymbol { TypeKind: TypeKind.Class },
        "interface" => symbol is INamedTypeSymbol { TypeKind: TypeKind.Interface },
        "struct"    => symbol is INamedTypeSymbol { TypeKind: TypeKind.Struct },
        "enum"      => symbol is INamedTypeSymbol { TypeKind: TypeKind.Enum },
        "delegate"  => symbol is INamedTypeSymbol { TypeKind: TypeKind.Delegate },
        "type"      => symbol is INamedTypeSymbol,
        "method"    => symbol.Kind is SymbolKind.Method,
        "property"  => symbol.Kind is SymbolKind.Property,
        "field"     => symbol.Kind is SymbolKind.Field,
        "event"     => symbol.Kind is SymbolKind.Event,
        "namespace" => symbol.Kind is SymbolKind.Namespace,
        _           => true
    };

    private static async Task<int> WriteChangesAsync(
        Solution original, Solution newSolution, bool dryRun, CancellationToken ct)
    {
        var solutionChanges = newSolution.GetChanges(original);
        int fileCount = 0;
        var tag = dryRun ? "[dry-run] " : "";

        foreach (var projectChange in solutionChanges.GetProjectChanges())
        {
            foreach (var docId in projectChange.GetChangedDocuments())
            {
                var newDoc = newSolution.GetDocument(docId)!;
                if (newDoc.FilePath is null) continue;

                var text = await newDoc.GetTextAsync(ct);
                Console.WriteLine($"  {tag}Modified: {newDoc.FilePath}");

                if (!dryRun)
                    WriteText(newDoc.FilePath, text.ToString(), text.Encoding);

                fileCount++;
            }

            foreach (var docId in projectChange.GetAddedDocuments())
            {
                var newDoc = newSolution.GetDocument(docId)!;
                if (newDoc.FilePath is null) continue;

                var text = await newDoc.GetTextAsync(ct);
                Console.WriteLine($"  {tag}Added:    {newDoc.FilePath}");

                if (!dryRun)
                    WriteText(newDoc.FilePath, text.ToString(), text.Encoding);

                fileCount++;
            }

            foreach (var docId in projectChange.GetRemovedDocuments())
            {
                var oldDoc = original.GetDocument(docId)!;
                if (oldDoc.FilePath is null) continue;

                Console.WriteLine($"  {tag}Removed:  {oldDoc.FilePath}");

                if (!dryRun)
                    File.Delete(oldDoc.FilePath);
            }
        }

        Console.WriteLine($"{(dryRun ? "Would modify" : "Modified")} {fileCount} file(s).");
        return 0;
    }

    private static void WriteText(string filePath, string content, Encoding? encoding)
    {
        // Preserve original encoding; avoid BOM on files that didn't have one
        encoding ??= new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        File.WriteAllText(filePath, content, encoding);
    }
}
