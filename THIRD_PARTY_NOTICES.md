# Third-Party Notices

`DalamudMCP` redistributes several third-party components in its plugin package and bundled local server.

The following list covers the primary redistributed dependencies visible in the current release package.

## Runtime Dependencies

### MemoryPack.Core

- Project: https://github.com/Cysharp/MemoryPack
- Package: https://www.nuget.org/packages/MemoryPack
- License: MIT

### Microsoft.Extensions.DependencyInjection

- Project: https://github.com/dotnet/runtime
- Package: https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection
- License: MIT

### Microsoft.Extensions.DependencyInjection.Abstractions

- Project: https://github.com/dotnet/runtime
- Package: https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection.Abstractions
- License: MIT

### ModelContextProtocol

- Project: https://github.com/modelcontextprotocol/csharp-sdk
- Package: https://www.nuget.org/packages/ModelContextProtocol
- License: Apache-2.0

### ModelContextProtocol.Core

- Project: https://github.com/modelcontextprotocol/csharp-sdk
- Package: https://www.nuget.org/packages/ModelContextProtocol.Core
- License: Apache-2.0

### Microsoft.Extensions.AI.Abstractions

- Project: https://github.com/dotnet/extensions
- Package: https://www.nuget.org/packages/Microsoft.Extensions.AI.Abstractions
- License: MIT

## Build-Time Dependencies

These components are used to build or package the plugin but are not intended to be redistributed as part of the plugin package itself.

### Dalamud.NET.Sdk

- Project: https://github.com/goatcorp/Dalamud.NET.Sdk
- Package: https://www.nuget.org/packages/Dalamud.NET.Sdk
- License: MIT

### DalamudPackager

- Project: https://github.com/goatcorp/DalamudPackager
- Package: https://www.nuget.org/packages/DalamudPackager
- License: EUPL-1.2

## Notes

- This notice file is informational and should be shipped with the release package alongside the root `LICENSE`.
- Additional transitive dependencies may appear depending on target framework or packaging mode.
- Refer to each package's NuGet metadata and source repository for the full license text and attribution requirements.
