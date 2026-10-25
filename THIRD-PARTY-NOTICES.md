# Third-party notices

Snappy source code is MIT licensed. Its portable Windows distribution bundles
Microsoft .NET and Windows Desktop runtime components. These are open source
under the MIT license with additional third-party notices; the runtime's full
LICENSE and ThirdPartyNotices files are copied into `runtime-notices/` by the
packaging script from the exact restored runtime packs being published.

- .NET runtime: https://github.com/dotnet/runtime
- WPF: https://github.com/dotnet/wpf
- Windows Forms (notification area icon only): https://github.com/dotnet/winforms

Development-only dependencies are not bundled in the portable app:

- xUnit.net: Apache-2.0, https://github.com/xunit/xunit
- xUnit Visual Studio runner: Apache-2.0, https://github.com/xunit/visualstudio.xunit
- Microsoft.NET.Test.Sdk: MIT, https://github.com/microsoft/vstest

Snappy does not include Divvy code, graphics, or branding. Windows APIs are
provided by the operating system. Snappy's approved window-and-cursor icon master
and exports are project assets, distributed under the project's MIT license.
Selected application icons remain owned by their respective applications and are
displayed at runtime using the Windows icon APIs; they are not bundled assets.
