# StreamerBotBuilder

A build tool that merges a multi-file C# project into a single formatted output suitable for pasting into a StreamerBot action.

## Background

StreamerBot actions are written as a single block of C# code. StreamerBotBuilder lets you develop the project across as many files and namespaces as you like, then merges everything into one output at build time.

## How it works

1. Scans all `.cs` files under the project root (excluding `bin`, `obj`, and any configured directories).
2. Strips each file's namespace declaration and `using` statements.
3. Collects all external `using` statements, deduplicates them, and places them at the top of the output. Internal project namespaces and Streamer.bot interface namespaces are automatically excluded.
4. Orders the files using the `namespaceOrder` patterns from `BuildConfig.json`. Files matching no pattern are sorted last. The `CPHInline` class (the StreamerBot entry point) is always first.
5. Applies `{Token}` substitutions — values resolved from the project's `.csproj` file replace matching `{Token}` placeholders throughout the merged code.
6. Formats the result with CSharpier.
7. Prepends an auto-generated header with build metadata, then writes the output to clipboard and/or a file.

### Nullable handling

If a file contains `#nullable enable`, the builder appends `#nullable restore` at the end of that file's section in the merged output. This prevents the nullable context from leaking into subsequent files. Files without `#nullable enable` are left untouched.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A `.csproj` file in or under the project root

## Running

```
dotnet run --project tools/StreamerBotBuilder/StreamerBotBuilder.csproj -- [options]
```

| Option | Short | Description |
|---|---|---|
| `--path` | `-p` | Path to the C# project root. Defaults to the current directory. |
| `--copy-to-clipboard` | `-c` | Copy merged output to clipboard. Overrides `BuildConfig.json`. |
| `--output-file` | `-o` | Write merged output to this path. Overrides `BuildConfig.json`. |

## BuildConfig.json

Place a `BuildConfig.json` in the project root to configure the build. All fields are optional.

```json
{
  "namespaceOrder": [
    "*.Utility",
    "MyProject.Utility.Http",
    "*.Models*"
  ],
  "excludeSubDirectories": ["Tests"],
  "copyToClipboard": true,
  "outputFileName": "output/merged.cs",
  "csprojSubstitutions": ["Version"],
  "additionalLogComments": [
    "Version: {Version}",
    "https://github.com/you/your-repo"
  ]
}
```

| Field | Type | Description |
|---|---|---|
| `namespaceOrder` | `string[]` | Wildcard patterns controlling file order in the merged output. Supports `*` as a wildcard segment (e.g. `*.Utility`, `MyProject.*.Http`). Files matching earlier patterns appear first; files matching no pattern appear last, sorted by filename. |
| `excludeSubDirectories` | `string[]` | Subdirectory names to skip when scanning for `.cs` files. `bin` and `obj` are always excluded. |
| `copyToClipboard` | `bool` | Copy the merged output to the clipboard after building. |
| `outputFileName` | `string` | Write the merged output to this file. Relative paths are resolved from the project root. |
| `csprojSubstitutions` | `string[]` | Names of `.csproj` XML elements whose values should be resolved and used to replace `{Name}` tokens in the merged output. See [Token substitution](#token-substitution) below. |
| `additionalLogComments` | `string[]` | Extra lines appended to the build header as `// comments`. Token substitution applies here too, so `"Version: {Version}"` will show the resolved version. |

## Token substitution

`csprojSubstitutions` lets you pull values out of the project's `.csproj` and inject them into the merged code at build time.

For each name listed, the builder reads the matching XML element from the `.csproj` (e.g. `<Version>1.0.0</Version>`) and replaces every occurrence of `{Name}` in the merged output with its value.

This is useful for values that have a single source of truth in the project file but also need to appear in runtime code. For example, to surface the project version:

**`MyProject.csproj`**
```xml
<Version>1.0.0</Version>
```

**`BuildConfig.json`**
```json
{
  "csprojSubstitutions": ["Version"]
}
```

**Source code**
```csharp
// {Version} is replaced by StreamerBotBuilder at build time with the value of <Version>
// from the .csproj. In local builds this reads as the literal string "{Version}", which
// is expected — this code is never executed outside of StreamerBot.
public const string AppVersion = "{Version}";
```

**Merged output**
```csharp
public const string AppVersion = "1.0.0";
```

Substitution happens before CSharpier formatting, so the formatter sees the final values.

## Known limitations

The builder makes some assumptions about how source files are structured. These cover the current use case but may not suit all projects.

- **File-scoped namespaces only.** Each file must use a file-scoped namespace declaration (`namespace Foo.Bar;`). Block-scoped namespaces (`namespace Foo.Bar { ... }`) are not recognized and will cause the build to fail.
- **One namespace per file.** The builder uses the first namespace declaration as the split point; everything before it is treated as `using` statements and everything after as the file body. Multiple namespace declarations in one file are not supported.
- **Compiler directives are not accounted for.** Directives like `#if`, `#else`, and `#endif` are passed through verbatim into the merged output. Simple cases may work, but directives that span or precede the namespace declaration line will break parsing, and their behavior in the merged context is generally untested. The only directive with explicit handling is `#nullable enable` (see above).
