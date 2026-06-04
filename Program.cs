using Microsoft.Build.Locator;

if (args.Length == 0 || args.Contains("-h") || args.Contains("--help"))
{
    RenameOptions.PrintHelp();
    return 0;
}

var options = RenameOptions.Parse(args);
if (options is null) return 1;

// RegisterDefaults must execute before the JIT loads any Microsoft.CodeAnalysis.MSBuild type.
// WorkspaceRenamer lives in a separate type so none of its methods are compiled until after this call.
MSBuildLocator.RegisterDefaults();
return await WorkspaceRenamer.RunAsync(options);
