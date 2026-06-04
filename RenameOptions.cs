internal sealed record RenameOptions(
    string SolutionPath,
    string OldName,
    string NewName,
    string? Kind,
    bool DryRun,
    bool AllowPartial)
{
    public static RenameOptions? Parse(string[] args)
    {
        string? solutionPath = null;
        string? oldName = null;
        string? newName = null;
        string? kind = null;
        bool dryRun = false;
        bool allowPartial = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--solution" or "-s" when i + 1 < args.Length:
                    solutionPath = args[++i];
                    break;
                case "--old" or "-o" when i + 1 < args.Length:
                    oldName = args[++i];
                    break;
                case "--new" or "-n" when i + 1 < args.Length:
                    newName = args[++i];
                    break;
                case "--kind" or "-k" when i + 1 < args.Length:
                    kind = args[++i].ToLowerInvariant();
                    if (!IsValidKind(kind))
                    {
                        Console.Error.WriteLine($"Unknown --kind value '{args[i]}'. Valid: class, interface, struct, enum, delegate, type, method, property, field, event, namespace");
                        return null;
                    }
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--allow-partial":
                    allowPartial = true;
                    break;
                default:
                    Console.Error.WriteLine($"Unknown argument '{args[i]}'.");
                    PrintHelp();
                    return null;
            }
        }

        if (solutionPath is null || oldName is null || newName is null)
        {
            Console.Error.WriteLine("--solution, --old, and --new are required.");
            PrintHelp();
            return null;
        }

        if (!File.Exists(solutionPath))
        {
            Console.Error.WriteLine($"Solution file not found: {solutionPath}");
            return null;
        }

        return new RenameOptions(solutionPath, oldName, newName, kind, dryRun, allowPartial);
    }

    public static void PrintHelp()
    {
        Console.WriteLine("""
            Usage: roslyn-rename --solution <path> --old <name> --new <name> [options]

            Options:
              --solution, -s    Path to the .sln file
              --old, -o         Symbol name to rename
              --new, -n         New symbol name
              --kind, -k        Narrow by kind: class, interface, struct, enum, delegate,
                                type, method, property, field, event, namespace
              --dry-run         Print what would change without writing files
              --allow-partial   Proceed even if some projects fail to load
            """);
    }

    private static bool IsValidKind(string kind) => kind is
        "class" or "interface" or "struct" or "enum" or "delegate" or "type" or
        "method" or "property" or "field" or "event" or "namespace";
}
