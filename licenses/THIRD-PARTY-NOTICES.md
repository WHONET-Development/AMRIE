# Third-Party Software Notices & Information

**AMRIE** (Antimicrobial Resistance Test Interpretation Engine) incorporates third-party open-source and redistributable software components. The notices and license terms for each component are provided below and in the accompanying license files in this directory.

---

## Summary of Included Third-Party Components

| Component / Package | Version | Author / Copyright Holder | License | License File |
| :--- | :--- | :--- | :--- | :--- |
| **CsvHelper** | `34.0.0` | Copyright © 2009–2026 Josh Close | Apache-2.0 OR MS-PL | [`LICENSE-CsvHelper.txt`](LICENSE-CsvHelper.txt) |
| **CommunityToolkit.Mvvm & DataGrid** | `8.4.2` / `7.1.2` | Copyright © .NET Foundation and Contributors | MIT | [`LICENSE-CommunityToolkit.txt`](LICENSE-CommunityToolkit.txt) |
| **Microsoft.WindowsAppSDK** *(and transitive packages)* | `2.5.4-experimental` | Copyright © Microsoft Corporation | Microsoft Software License | [`LICENSE-WindowsAppSDK.txt`](LICENSE-WindowsAppSDK.txt) |
| **Microsoft.Windows.SDK.BuildTools** | `10.0.28000.2705` | Copyright © Microsoft Corporation | Microsoft Software License | [`LICENSE-WindowsAppSDK.txt`](LICENSE-WindowsAppSDK.txt) |
| **Microsoft.Web.WebView2** | `1.0.3719.77` | Copyright © Microsoft Corporation | BSD 3-Clause | [`LICENSE-WebView2.txt`](LICENSE-WebView2.txt) |
| **PolyfillLib** | `11.4.5` | Copyright © 2026 Simon Cropp and Contributors | MIT | [`LICENSE-PolyfillLib.txt`](LICENSE-PolyfillLib.txt) |
| **System.Text.Json & Microsoft.Bcl.\*** | `10.0.14` | Copyright © .NET Foundation and Contributors | MIT | [`LICENSE-DotNetRuntime.txt`](LICENSE-DotNetRuntime.txt) |
| **Microsoft .NET 10 Runtime** *(Self-Contained WinUI MSI)* | `10.0.x` | Copyright © .NET Foundation and Contributors | MIT | [`LICENSE-DotNetRuntime.txt`](LICENSE-DotNetRuntime.txt) |
| **Uno Platform & Uno SDK** *(Desktop Target)* | `7.0.x` | Copyright © 2018–2026 Uno Platform Inc. | Apache-2.0 | [`LICENSE-UnoPlatform.txt`](LICENSE-UnoPlatform.txt) |
| **SkiaSharp & Google Skia** *(Desktop Target)* | `3.x / 2.88.x` | Copyright © Microsoft Corporation & Google Inc. | MIT / BSD 3-Clause | [`LICENSE-SkiaSharp.txt`](LICENSE-SkiaSharp.txt) |
| **xUnit.net** *(Test Suite Only)* | `2.5.3` | Copyright © .NET Foundation and Contributors | Apache-2.0 | Test-only dependency (not distributed in MSI) |
| **Coverlet** *(Test Suite Only)* | `6.0.0` | Copyright © Toni Solarin-Sodara | MIT | Test-only dependency (not distributed in MSI) |

---

## 1. CsvHelper (v34.0.0)

- **Project URL:** [https://joshclose.github.io/CsvHelper/](https://joshclose.github.io/CsvHelper/)
- **Source Repository:** [https://github.com/JoshClose/CsvHelper](https://github.com/JoshClose/CsvHelper)
- **Copyright:** Copyright © 2009–2026 Josh Close
- **License:** Dual-licensed under the Apache License, Version 2.0 OR the Microsoft Public License (MS-PL).
- **Full License Text:** See [`LICENSE-CsvHelper.txt`](LICENSE-CsvHelper.txt).
- **Notice of Modifications:** Used unmodified as a NuGet library dependency in `AMRIE.Library`, `AMRIE.CLI`, `AMRIE.WinUI`, and `AMRIE.WinForms`.

---

## 2. .NET Community Toolkit — CommunityToolkit.Mvvm (v8.4.2) & DataGrid (v7.1.2)

- **Packages:**
  - `CommunityToolkit.Mvvm` (`8.4.2`)
  - `CommunityToolkit.WinUI.UI.Controls.DataGrid` (`7.1.2`)
  - `Uno.CommunityToolkit.WinUI.UI.Controls.DataGrid` (`7.2.1-dev.2`)
- **Source Repository:** [https://github.com/CommunityToolkit/dotnet](https://github.com/CommunityToolkit/dotnet)
- **Copyright:** Copyright © .NET Foundation and Contributors. All rights reserved.
- **License:** MIT License
- **Full License Text:** See [`LICENSE-CommunityToolkit.txt`](LICENSE-CommunityToolkit.txt).

---

## 3. Microsoft Windows App SDK & Windows SDK BuildTools

- **Packages:**
  - `Microsoft.WindowsAppSDK` (`2.5.4-experimental`)
  - `Microsoft.WindowsAppSDK.AI`, `.Base`, `.DWrite`, `.Foundation`, `.InteractiveExperiences`, `.ML`, `.Runtime`, `.Search`, `.Widgets`, `.WinUI`
  - `Microsoft.Windows.AI.MachineLearning` (`2.6.74-rc`)
  - `Microsoft.Windows.SDK.BuildTools` (`10.0.28000.2705`) & `Microsoft.Windows.SDK.BuildTools.MSIX` (`1.7.251221100`)
- **Copyright:** Copyright © Microsoft Corporation. All rights reserved.
- **License:** Microsoft Software License Terms for Microsoft Windows App SDK.
- **Full License Text:** See [`LICENSE-WindowsAppSDK.txt`](LICENSE-WindowsAppSDK.txt).

---

## 4. Microsoft Edge WebView2 SDK (v1.0.3719.77)

- **Package:** `Microsoft.Web.WebView2` (`1.0.3719.77`)
- **Copyright:** Copyright © Microsoft Corporation. All rights reserved.
- **License:** BSD 3-Clause License
- **Full License Text:** See [`LICENSE-WebView2.txt`](LICENSE-WebView2.txt).

---

## 5. PolyfillLib (v11.4.5)

- **Source Repository:** [https://github.com/SimonCropp/Polyfill](https://github.com/SimonCropp/Polyfill)
- **Copyright:** Copyright © 2026 Simon Cropp and Contributors. All rights reserved.
- **License:** MIT License (compile-time polyfills for the `.NET Framework 4.8` target of `AMRIE.Library`).
- **Full License Text:** See [`LICENSE-PolyfillLib.txt`](LICENSE-PolyfillLib.txt).

---

## 6. Microsoft .NET 10 Runtime & Base Class Libraries

- **Packages:**
  - `System.Text.Json` (`10.0.14`)
  - `Microsoft.Bcl.AsyncInterfaces` (`10.0.14`), `Microsoft.Bcl.Memory` (`10.0.12`)
  - `System.IO.Pipelines` (`10.0.14`), `System.Text.Encodings.Web` (`10.0.14`)
  - `System.Buffers` (`4.6.1`), `System.Memory` (`4.6.3`), `System.Numerics.Vectors` (`4.6.1`), `System.Numerics.Tensors` (`9.0.0`), `System.Runtime.CompilerServices.Unsafe` (`6.1.2`), `System.Threading.Tasks.Extensions` (`4.6.3`), `System.ValueTuple` (`4.6.2`)
  - Self-contained .NET 10 Runtime libraries distributed with `AMRIE.WinUI`
- **Source Repository:** [https://github.com/dotnet/runtime](https://github.com/dotnet/runtime)
- **Copyright:** Copyright © .NET Foundation and Contributors. All rights reserved.
- **License:** MIT License
- **Full License Text:** See [`LICENSE-DotNetRuntime.txt`](LICENSE-DotNetRuntime.txt).

---

## 7. Uno Platform & Uno SDK (v7.0.x)

- **Packages & Tools:**
  - `Uno.WinUI`
  - `Uno.Sdk` (`7.0.22`)
  - `Uno.UI`
  - `Uno.CommunityToolkit.WinUI.UI.Controls.DataGrid` (`7.2.1-dev.2`)
  - `Uno.WinUI.Runtime.Skia.X11`, `Uno.WinUI.Runtime.Skia.Linux.FrameBuffer`, `Uno.WinUI.Runtime.Skia.MacOS`
- **Project URL:** [https://platform.uno/](https://platform.uno/)
- **Source Repository:** [https://github.com/unoplatform/uno](https://github.com/unoplatform/uno)
- **Copyright:** Copyright © 2018–2026 Uno Platform Inc. All rights reserved.
- **License:** Apache License, Version 2.0
- **Full License Text:** See [`LICENSE-UnoPlatform.txt`](LICENSE-UnoPlatform.txt).

---

## 8. SkiaSharp & Google Skia 2D Graphics Engine

- **Packages:**
  - `SkiaSharp`
  - `SkiaSharp.NativeAssets.Linux`
  - `SkiaSharp.NativeAssets.macOS`
  - `SkiaSharp.Views`
- **Source Repositories:**
  - SkiaSharp: [https://github.com/mono/SkiaSharp](https://github.com/mono/SkiaSharp)
  - Google Skia: [https://skia.googlesource.com/skia](https://skia.googlesource.com/skia)
- **Copyright:**
  - SkiaSharp: Copyright © 2015–2016 Xamarin Inc., Copyright © 2017–2026 Microsoft Corporation. All rights reserved.
  - Google Skia: Copyright © 2011 Google Inc. All rights reserved.
- **License:** SkiaSharp is licensed under the MIT License; Google Skia is licensed under the BSD 3-Clause License.
- **Full License Text:** See [`LICENSE-SkiaSharp.txt`](LICENSE-SkiaSharp.txt).


