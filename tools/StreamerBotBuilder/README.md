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

`csprojSubstitutions` lets you pull values out of the project's `.csproj` and inject them into the merged code at build time. For each name listed, the builder reads the matching XML element (e.g. `<Version>1.0.0</Version>`) and replaces every occurrence of `{Name}` in the merged output with its value. Substitution happens before CSharpier formatting. See the [example](#example) below for a demonstration.

## Example

Given a project with this layout:

```
MyProject/
  MyProject.csproj
  BuildConfig.json
  CPHInline.cs
  Utility/
    AppInfo.cs
  Models/
    Greeting.cs
```

**`MyProject.csproj`**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>net48</TargetFrameworks>
    <RootNamespace>MyProject</RootNamespace>
    <Version>1.0.0</Version>
  </PropertyGroup>
  <!-- ... StreamerBot assembly references ... -->
</Project>
```

**`BuildConfig.json`**
```json
{
  "namespaceOrder": ["*.Utility", "*.Models*"],
  "copyToClipboard": true,
  "csprojSubstitutions": ["Version"],
  "additionalLogComments": ["Version: {Version}"]
}
```

**`CPHInline.cs`** — the StreamerBot entry point; always placed first in the merged output
```cs
using System;
using Streamer.bot.Plugin.Interface;
using MyProject.Models;

public class CPHInline
#if OUTSIDE_STREAMERBOT
    : CPHInlineBase
#endif
{
    public bool Execute()
    {
        CPH.SendMessage(new Greeting("world").Text);
        return true;
    }
}
```

**`Utility/AppInfo.cs`**
```cs
using System;

namespace MyProject.Utility;

public static class AppInfo
{
    public static readonly string StartedAt = DateTime.Now.ToString("HH:mm");

    // {Version} is replaced by StreamerBotBuilder at build time using <Version> from the .csproj.
    // In local builds this is the literal string "{Version}" — expected, since this code
    // only runs inside StreamerBot.
    public const string Version = "{Version}";
}
```

**`Models/Greeting.cs`**
```cs
using MyProject.Utility;

namespace MyProject.Models;

public class Greeting(string name)
{
    public string Text => $"Hello, {name}! App v{AppInfo.Version}, started at {AppInfo.StartedAt}.";
}
```

**Merged output**
```cs
// =====================================================
// AUTO-GENERATED — Do not edit directly.
// Merged from 3 source files by StreamerBotBuilder.
// To make changes, edit the source files and rebuild.
// =====================================================
// Version: 1.0.0
// Built: 2026-05-15 12:00:00
// =====================================================

using System;

public class CPHInline
#if OUTSIDE_STREAMERBOT
    : CPHInlineBase
#endif
{
    public bool Execute()
    {
        CPH.SendMessage(new Greeting("world").Text);
        return true;
    }
}

public static class AppInfo
{
    public static readonly string StartedAt = DateTime.Now.ToString("HH:mm");

    // {Version} is replaced by StreamerBotBuilder at build time using <Version> from the .csproj.
    // In local builds this is the literal string "{Version}" — expected, since this code
    // only runs inside StreamerBot.
    public const string Version = "1.0.0";
}

public class Greeting(string name)
{
    public string Text => $"Hello, {name}! App v{AppInfo.Version}, started at {AppInfo.StartedAt}.";
}
```

A few things to note in the output:

- **Namespace declarations are stripped** — all types appear at the top level, as StreamerBot requires.
- **`using System;`** appears once, deduplicated from `CPHInline.cs` and `AppInfo.cs`.
- **`using Streamer.bot.Plugin.Interface;`** is excluded — Streamer.bot interface namespaces are always available in the StreamerBot runtime without a using statement, so they are stripped.
- **`using MyProject.*`** is excluded — internal project namespaces are never needed in the flat merged output.
- **File ordering** follows `namespaceOrder`: `CPHInline` is always first, then `*.Utility` (`AppInfo`), then `*.Models*` (`Greeting`).
- **`{Version}`** in both the header comment and `AppInfo.Version` has been replaced with `1.0.0`.

## Known limitations

The builder makes some assumptions about how source files are structured. These cover the current use case but may not suit all projects.

- **File-scoped namespaces only.** Each file must use a file-scoped namespace declaration (`namespace Foo.Bar;`). Block-scoped namespaces (`namespace Foo.Bar { ... }`) are not recognized and will cause the build to fail.
- **One namespace per file.** The builder uses the first namespace declaration as the split point; everything before it is treated as `using` statements and everything after as the file body. Multiple namespace declarations in one file are not supported.
- **Compiler directives are not accounted for.** Directives like `#if`, `#else`, and `#endif` are passed through verbatim into the merged output. Simple cases may work, but directives that span or precede the namespace declaration line will break parsing, and their behavior in the merged context is generally untested. The only directive with explicit handling is `#nullable enable` (see above).
