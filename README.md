# TKCL Studio

A Linux desktop tool that converts TKCL mod packages into ready-to-use mods for [valentderah’s TotK PC Recomp](https://github.com/valentderah/totk-pc-recomp). Supports Tears of the Kingdom v1.4.3.

> [!NOTE]
> **Compatibility:** TKCL Studio supports only *The Legend of Zelda: Tears of the Kingdom*, game version **1.4.3**, and prepares mods for [**TotK Recomp**](https://github.com/valentderah/totk-pc-recomp). It does not support *Breath of the Wild*, other games, or other TotK game versions. Mod compatibility can vary; a successful conversion does not guarantee that every mod feature will work in Recomp.

> [!IMPORTANT]
> **TKCL Studio is developed almost entirely using AI.** Its written using [OpenAI’s Codex](https://openai.com/codex/), and directed and tested by a human. The project also uses existing open-source libraries, which retain their own authorship and licenses.
>
> Please keep discussions on the tool rather than AI. If you disagree with use of AI or don't trust it feel free to not use this tool.

## Features

- **Clean UI**
- **Builds `.tkcl` mod packages** into mod folders for TotK Recomp
- **Remembers file path**, doesnt copy keys, only reads from uploaded.
- No emulator neededs.
- No game files, keys or mods are included.

## Usage

Download `.appimage` from [latest release](https://github.com/godblessmerica/TKCL-Studio/releases/latest) and launch it.

1. Choose a `.tkcl` or `.zip` (must contain only one `.tkcl` file in it)
2. Choose base game, update, and `prod.keys` and or `title.keys` but `title.keys`  is only necessary if required title key cannot be read from your game package.
3. Select **mod options**.
4. **Build mod.**
5. Press **"Open output"** and move the mod folder imto Recomp's `mods/` folder. Restart is needed fro mods to load.

## Compatibility

- Currently only **TotK 1.4.3** is supported.
- Mods might fail because code patches don't match with TotK Recomp, or mods that require other mods or Switch code plugins, among other unknown things.

## Credit

[TkSharp](https://github.com/TKMM-Team/TkSharp) · [TotK Recomp](https://github.com/valentderah/totk-pc-recomp) · [Avalonia](https://avaloniaui.net/) · [uruntime](https://github.com/VHSgunzo/uruntime)

## License

[AGPL-3.0-or-later](LICENSE). Dependency notices: [THIRD_PARTY.md](THIRD_PARTY.md). Binary distributions must include access to corresponding source.
