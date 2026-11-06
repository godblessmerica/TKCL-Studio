TKCL Studio — Linux x86_64

Double-click TKCL-Studio-x86_64.AppImage. No .NET installation or emulator is needed.

Choose a TKCL package (or ZIP containing one), your own TotK base NSP/XCI,
the game 1.4.3 update NSP, and prod.keys. Leave title.keys empty unless needed.
The app scans the mod package and displays its actual options. Choose any
required options, then press Build Mod. Use Open output when it finishes.
Copy the resulting folder into Recomp's mods folder and enable it there.

Paths are empty on first use. Remember file paths stores paths only; disabling
it deletes those saved paths. Game files and keys are read locally, never
uploaded or copied into the app. Rebuilt mod archives contain the game data
needed for the mod, so keep the resulting files local.

TKCL-Studio-data beside the AppImage holds saved paths, scratch/cache files
and completed output. Keep the AppImage in a writable directory. Temporary
package extraction is removed after each build. Output folders are unique;
old results are preserved. The AppImage contains no game files or keys.

Scope: game 1.4.3, one TKCL package per build, packaged options, no emulator
setup, no game launch, no automatic mod installation. Switch code plugins
and unresolved mod dependencies are rejected. Code-patch compatibility is
decided by Recomp. Use trusted mod packages.

This build uses Avalonia and TKMM-Team's TkSharp. The TkSharp library has a
small local change to accept explicitly selected keys in memory, avoiding
copies of key files and automatic searches for unrelated key locations.

If the desktop does not launch an AppImage, enable its executable permission
in your file manager. On systems without FUSE, the bundled uruntime 0.8.1 automatically falls back
to extraction and runs the same app. No manual extraction command is needed.

TKCL Studio is licensed under AGPL-3.0-or-later. See the included LICENSE.
Dependencies retain their own license notices.
