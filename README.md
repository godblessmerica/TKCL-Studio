# TKCL Studio

A local Linux desktop tool for building `.tkcl` mod packages into folders that TotK Recomp can load.

- One dark window with local file pickers, packaged mod options, a Build Mod button and progress.
- Reads options and defaults directly from the selected package.
- Remembers file paths after first use. Key contents are never stored in settings.
- No TKMM setup wizard, emulator configuration or emulator launch.
- No game files, keys or mods are included or uploaded.

## Use

Launch `TKCL-Studio-x86_64.AppImage` from a writable folder. The AppImage includes .NET and automatically falls back to extraction when FUSE is unavailable.

1. Choose a `.tkcl`, or a ZIP containing exactly one TKCL file.
2. Choose your own base-game NSP/XCI, the **1.4.3** update NSP and `prod.keys`.
3. Leave `title.keys` empty unless a required title key cannot otherwise be read from your game package.
4. Select the mod's options, then press **Build Mod**.
5. Choose **Open output**. Copy that output folder into Recomp's `mods/` folder, enable it and restart Recomp.

New users start with empty file paths. Settings, caches and output live in `TKCL-Studio-data/` beside the AppImage. Unchecking **Remember file paths** deletes the saved paths. Each build gets a new output folder; existing outputs are preserved.

Game files and keys are read locally in place. Reconstructed mod archives contain the game data needed for the mod, so keep generated output local. Use packages from authors you trust.

## Build from source

Requirements: Linux x86_64, .NET 10 SDK, Git, Python 3 and network access for dependencies.

```bash
./setup
./run
```

`setup` fetches the pinned TkSharp source, applies the included key-handling patch and restores NuGet dependencies. Use `DOTNET_EXECUTABLE=/path/to/dotnet` if the SDK is not on PATH. Build caches remain inside `.cache/`.

To make the portable AppImage:

```bash
./build-appimage
```

The result is `dist/TKCL-Studio-x86_64.AppImage`. The build fetches checksum-verified, pinned AppImage tooling. Release builds exclude test-only code and generated outputs.

## Command line

```bash
./run inspect /path/to/mod.tkcl
./run merge /path/to/mod.tkcl \
  --base /path/to/base.nsp --update /path/to/update.nsp \
  --prod-keys /path/to/prod.keys
```

Optional arguments:

- `--title-keys /path/to/title.keys`
- `--option 'Exact packaged option name'`
- `--keys-folder /path/to/keys` instead of explicit key files

For an actual extracted RomFS, use `./run merge /path/to/mod.tkcl --romfs /path/to/romfs` without keys. Recomp's indexed game-data folder is not an extracted RomFS.

The source CLI writes to `output/` in its working directory. The AppImage writes to its portable data directory. The progress bar reports actual build stages and animates during work; it does not estimate percentages.

## Tests

```bash
./test
```

This builds a separate test assembly and checks path traversal, symbolic links and option selection/validation. Test code is excluded from release builds.

An optional desktop integration test requires your own local Dark Mode package and game files:

```bash
python3 tests/smoke_darkmode.py \
  --mod /path/to/mod.tkcl --base /path/to/base.nsp \
  --update /path/to/update.nsp --prod /path/to/prod.keys
```

Run `./test` first. The integration test uses the desktop test assembly, reads the supplied inputs locally and saves its evidence under `.cache/`. No game files, keys, mod packages, screenshots or test results belong in Git.

## Compatibility

The current target is TotK Recomp with game **1.4.3**. One package is processed at a time. Required/default option groups are respected. Switch code plugins and unresolved dependencies are rejected. Recomp decides whether code patches can be applied. Other game versions are rejected explicitly.

TkSharp rebuilds complete resources and the resource-size table against the local game. Its automatic shop-limit IPS is omitted for packages with no selected code patches. Some valid game-1.4.3 UI resources retain `Product.140` filenames; do not rename them.

Sources and credits: [TkSharp](https://github.com/TKMM-Team/TkSharp), [Recomp mod requirements](https://github.com/valentderah/totk-pc-recomp/blob/v0.3.10/docs/mods.md), [Avalonia](https://avaloniaui.net/), [uruntime](https://github.com/VHSgunzo/uruntime).

## Licensing

TKCL Studio is licensed under **AGPL-3.0-or-later**. See [LICENSE](LICENSE). Dependency notices and source references are listed in [THIRD_PARTY.md](THIRD_PARTY.md). When distributing a binary, provide its corresponding source and required covered-dependency source/build information alongside it.
