# Third-party components

TKCL Studio’s original code is AGPL-3.0-or-later. It preserves upstream project names, dependency versions and license notices. These identify the components used to build the app.

| Component | Version or revision | License | Source |
| --- | --- | --- | --- |
| TkSharp | ee83fb1e70541600fe7a44a376ffe4bd3486f622 | MIT | https://github.com/TKMM-Team/TkSharp |
| Tkmm.BymlLibrary | 1.2.3; source 6f6ac2d38410d8e797262b7132fde08982938e53 | AGPL-3.0-or-later | https://github.com/TKMM-Team/Tkmm.BymlLibrary |
| Avalonia | 11.3.22 | MIT | https://github.com/AvaloniaUI/Avalonia |
| uruntime | 0.8.1 | MIT | https://github.com/VHSgunzo/uruntime |
| .NET | 10 | MIT and third-party notices | https://github.com/dotnet/runtime |

TkSharp's dependencies are declared in its project files and restored by NuGet. The AGPL dependency's terms remain applicable to the combined application; selecting a different license for original app code does not remove them.

The small TkSharp modification is recorded in [packaging/tksharp-explicit-keys.patch](packaging/tksharp-explicit-keys.patch). It allows an explicitly loaded in-memory key set, avoiding automatic key-folder searches and temporary copies of key files. Upstream notices remain intact in the fetched source.

The bundled runtime notice is [packaging/Uruntime-License.txt](packaging/Uruntime-License.txt). TkSharp's notice is included from its fetched source when packaging. Other components retain their own licenses; no ownership of those components is claimed.
