// SPDX-License-Identifier: AGPL-3.0-or-later
using System.IO.Compression;
using Microsoft.Extensions.Logging;
using TkSharp;
using SarcLibrary;
using RstbLibrary;
using TkSharp.Core;
using TkSharp.Core.IO;
using TkSharp.Core.IO.Caching;
using TkSharp.Core.Models;
using TkSharp.Data.Embedded;
using TkSharp.Extensions.LibHac;
using TkSharp.IO.Readers;
using TkSharp.Merging;

using System.Text.Json;
using LibHac.Common.Keys;

public record OptionInfo(string Name, string Description, bool Selected);
public record GroupInfo(string Name, string Description, int Type, OptionInfo[] Options);
public record PackageInfo(string Name, string Version, string Author, string Description, GroupInfo[] Groups);

public static partial class Converter
{
    private static readonly SemaphoreSlim Gate = new(1);
    private static readonly MergeLogger Logger = new();
    static Converter() => TkLog.Instance.Register(Logger);

    public static async Task<int> Run(string[] args, string projectRoot, Action<string> report, Action<string>? stage = null, Action<PackageInfo>? inspected = null)
    {
        await Gate.WaitAsync();
        Logger.Warnings = 0;
        Logger.Report = report;
        try
        {
            if (args.Length == 0 || args[0] == "--help")
            {
                report("inspect PACKAGE.zip|tkcl\nmerge PACKAGE --romfs ROMFS\nmerge PACKAGE --base XCI|NSP --update NSP --keys-folder DIR\nmerge PACKAGE --base XCI|NSP --update NSP --prod-keys FILE [--title-keys FILE]\nOutput: ./output/<unique-id>/romfs (game must be 1.4.3). Uses packaged default options; add --option NAME to select one option.");
                return 0;
            }
#if STUDIO_TESTS
    if (args[0] == "self-test") return SelfTest(projectRoot, report);
#endif
            if (args.Length < 2 || args[0] is not ("inspect" or "merge")) throw new ArgumentException("Run --help for usage.");
            var options = new Dictionary<string, string>();
            string? selections = null;
            string? selectedOption = null;
            for (int i = 2; i < args.Length; i += 2)
            {
                if (i + 1 >= args.Length || args[i] is not ("--romfs" or "--base" or "--update" or "--keys-folder" or "--option" or "--selections" or "--prod-keys" or "--title-keys"))
                    throw new ArgumentException("Unknown or incomplete option.");
                if (args[i] == "--selections")
                {
                    if (selections != null) throw new ArgumentException("Supply selections only once.");
                    selections = args[i + 1];
                }
                else if (args[i] == "--option")
                {
                    if (selectedOption != null) throw new ArgumentException("Supply --option only once.");
                    selectedOption = args[i + 1];
                }
                else options.Add(args[i], Path.GetFullPath(args[i + 1]));
            }
            var project = projectRoot;
            SafeFiles.CheckAncestors(project);
            var job = Guid.NewGuid().ToString("N");
            var work = SafeFiles.Resolve(project, "work/" + job);
            Directory.CreateDirectory(work);
            try
            {
                stage?.Invoke("Reading mod package");
                using var package = OpenPackage(args[1]);
                var provider = new SafeFiles(work);
                var context = new TkModContext(input: args[1], stream: package);
                var mod = await new TkPackReader(provider).ReadMod(context, package) ?? throw new InvalidDataException("No mod found.");
                report($"{mod.Name} | {mod.Version} | {mod.Author}");
                report($"Source game version: {mod.Changelog.GameVersion}; base entries: {mod.Changelog.ChangelogFiles.Count}");
                var selection = new TkProfileMod(mod);
                selection.EnsureOptionSelection(applyPackagedDefaults: true);
                if (selectedOption != null && selections != null) throw new ArgumentException("Choose one option selection method.");
                if (selectedOption != null) SelectOption(selection, selectedOption);
                if (selections != null) ApplySelections(selection, selections);
                inspected?.Invoke(new PackageInfo(mod.Name, mod.Version, mod.Author, mod.Description,
                    mod.OptionGroups.Select(g => new GroupInfo(g.Name, g.Description, (int)g.Type,
                        g.Options.Select(o => new OptionInfo(o.Name, o.Description, selection.SelectedOptions[g].Contains(o))).ToArray())).ToArray()));
                foreach (var group in mod.OptionGroups)
                {
                    report($"Option {group.Name}: [{string.Join(", ", selection.SelectedOptions[group].Select(o => o.Name))}] (available: {string.Join(", ", group.Options.Select(o => o.Name))})");
                    foreach (var option in group.Options)
                        report($"  {option.Name}: {option.Description}");
                }
                var profile = new TkProfile();
                profile.Mods.Add(selection);
                var targets = TkModManager.GetMergeTargets(profile, _ => true).ToArray();
                foreach (var entry in targets.SelectMany(c => c.ChangelogFiles))
                    SafeFiles.Resolve(work, entry.Canonical);
                report($"Selected code patches: {targets.Sum(c => c.PatchFiles.Count)}");
                if (args[0] == "inspect")
                {
                    foreach (var entry in targets.SelectMany(c => c.ChangelogFiles))
                        report($"  {entry.Type}: {entry.Canonical}");
                    return 0;
                }
                if (mod.Dependencies.Count > 0 || mod.OptionGroups.Any(g => g.Dependencies.Count > 0))
                    throw new InvalidDataException("Mod dependencies are not supported.");
                if (targets.Any(c => c.SubSdkFiles.Count > 0 || c.ExeFiles.Count > 0))
                    throw new InvalidDataException("Package contains game code modules unsupported by Recomp.");
                stage?.Invoke("Opening local game files");
                using var sums = TkEmbeddedDataSource.GetChecksumsBin();
                using var lookupData = TkEmbeddedDataSource.GetPackFileLookup();
                var checksums = TkChecksums.FromStream(sums);
                using var lookup = new TkPackFileLookup(lookupData);
                ITkRom rom;
                if (options.TryGetValue("--romfs", out var romfs))
                {
                    if (options.Count != 1) throw new ArgumentException("Use --romfs alone.");
                    rom = new ExtractedTkRom(romfs, checksums, lookup);
                }
                else
                {
                    if (!options.ContainsKey("--base") || !options.ContainsKey("--update"))
                        throw new ArgumentException("Choose your base game and 1.4.3 update.");
                    if (options.TryGetValue("--keys-folder", out var folder))
                    {
                        if (options.ContainsKey("--prod-keys") || options.ContainsKey("--title-keys"))
                            throw new ArgumentException("Use a keys folder or explicit key files, not both.");
                        options["--prod-keys"] = Path.Combine(folder, "prod.keys");
                        var title = Path.Combine(folder, "title.keys");
                        if (File.Exists(title)) options["--title-keys"] = title;
                    }
                    if (!options.TryGetValue("--prod-keys", out var prod) || !File.Exists(prod))
                        throw new FileNotFoundException("Choose a readable prod.keys file.");
                    options.TryGetValue("--title-keys", out var titleKeys);
                    if (titleKeys != null && !File.Exists(titleKeys)) throw new FileNotFoundException("The selected title.keys file no longer exists.");
                    var keySet = new KeySet();
                    ExternalKeyReader.ReadKeyFile(keySet, prodKeysFilename: prod, devKeysFilename: null,
                        titleKeysFilename: titleKeys, consoleKeysFilename: null);
                    rom = TkExtensibleRomProviderBuilder.Create(checksums, lookup).WithPreferredVersion("1.4.3")
                        .WithKeySet(keySet).WithPackagedBaseGame([options["--base"]])
                        .WithPackagedUpdate([options["--update"]]).Build().GetRom();
                }
                using (rom)
                {
                    if (rom.GameVersion != 143) throw new InvalidDataException($"Expected game 143; found {rom.GameVersion}.");
                    var output = SafeFiles.Resolve(project, "output/" + job);
                    var staging = SafeFiles.Resolve(work, "merged");
                    stage?.Invoke("Merging selected options");
                    var logger = Logger;
                    new TkMerger(new SafeFiles(staging), rom).Merge(targets);
                    if (logger.Warnings > 0) throw new InvalidDataException("Merge reported warnings/errors; output not published. Check game/mod compatibility.");
                    if (!Directory.Exists(Path.Combine(staging, "romfs"))) throw new InvalidDataException("Merge produced no RomFS.");
                    // TkSharp always adds its own shop-limit IPS, even for a pure RomFS mod.
                    if (targets.All(c => c.PatchFiles.Count == 0))
                    {
                        var ips = Path.Combine(staging, "exefs", rom.NsoBinaryId.ToUpperInvariant() + ".ips");
                        if (File.Exists(ips)) File.Delete(ips);
                        var exefs = Path.Combine(staging, "exefs");
                        if (Directory.Exists(exefs) && !Directory.EnumerateFileSystemEntries(exefs).Any()) Directory.Delete(exefs);
                    }
                    stage?.Invoke("Checking output files");
                    foreach (var file in Directory.EnumerateFiles(staging, "*.zs", SearchOption.AllDirectories))
                    {
                        var raw = rom.Zstd.Decompress(File.ReadAllBytes(file));
                        if (file.EndsWith(".blarc.zs"))
                        {
                            var sarc = Sarc.FromBinary(new ArraySegment<byte>(raw));
                            if (sarc.Count == 0) throw new InvalidDataException("Empty output layout archive.");
                            var relative = Path.GetRelativePath(Path.Combine(staging, "romfs"), file);
                            using var vanilla = rom.GetVanilla(relative, out _);
                            if (vanilla.IsEmpty) throw new InvalidDataException("Output layout not found in source game.");
                            var original = Sarc.FromBinary(vanilla.Segment);
                            var changed = sarc.Count(e => !original.TryGetValue(e.Key, out var data) || !data.AsSpan().SequenceEqual(e.Value.AsSpan()));
                            report($"Verified {Path.GetFileName(file)}: {sarc.Count} archive entries, {changed} added/changed");
                        }
                        else if (file.EndsWith(".rsizetable.zs"))
                        {
                            _ = Rstb.FromBinary(raw);
                            report("Verified resource size table.");
                        }
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                    Directory.Move(staging, output);
                    stage?.Invoke("Build complete");
                    report($"Output: {output}");
                    report($"Files: {Directory.EnumerateFiles(output, "*", SearchOption.AllDirectories).Count()}");
                }
            }
            finally { Directory.Delete(work, recursive: true); }
            return 0;
        }
        catch (Exception ex)
        {
            report($"Error ({ex.GetType().Name}): {ex.Message}");
            return 1;
        }
        finally { Logger.Report = null; Gate.Release(); }
    }

    static void ApplySelections(TkProfileMod selection, string json)
    {
        var groups = JsonSerializer.Deserialize<int[][]>(json) ?? throw new ArgumentException("Invalid option selections.");
        if (groups.Length != selection.Mod.OptionGroups.Count) throw new ArgumentException("Mod option groups have changed. Choose the package again.");
        for (int i = 0; i < groups.Length; i++)
        {
            var group = selection.Mod.OptionGroups[i];
            var indices = groups[i] ?? throw new ArgumentException("Missing option group.");
            if (indices.Distinct().Count() != indices.Length || indices.Any(n => n < 0 || n >= group.Options.Count))
                throw new ArgumentException("Invalid mod option index.");
            if ((group.Type is OptionGroupType.Single or OptionGroupType.SingleRequired) && indices.Length > 1)
                throw new ArgumentException($"Choose at most one option for {group.Name}.");
            if ((group.Type is OptionGroupType.SingleRequired or OptionGroupType.MultiRequired) && indices.Length == 0)
                throw new ArgumentException($"Choose an option for {group.Name}.");
            selection.SelectedOptions[group] = indices.Select(n => group.Options[n]).ToHashSet();
        }
    }

    static void SelectOption(TkProfileMod selection, string name)
    {
        var matches = selection.Mod.OptionGroups.SelectMany(g => g.Options.Select(o => (Group: g, Option: o)))
            .Where(x => x.Option.Name == name).ToArray();
        if (matches.Length != 1) throw new ArgumentException("Option name must match exactly one packaged option.");
        var (group, option) = matches[0];
        if (group.Type is OptionGroupType.Single or OptionGroupType.SingleRequired)
            selection.SelectedOptions[group].Clear();
        selection.SelectedOptions[group].Add(option);
    }

    static MemoryStream OpenPackage(string path)
    {
        const long limit = 256 * 1024 * 1024;
        if (new FileInfo(path).Length > limit) throw new InvalidDataException("Package exceeds 256 MiB limit.");
        byte[] data;
        if (Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            using var zip = ZipFile.OpenRead(path);
            var entries = zip.Entries.Where(e => e.FullName.EndsWith(".tkcl", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (entries.Length != 1 || entries[0].Length > limit) throw new InvalidDataException("ZIP must contain exactly one TKCL under 256 MiB.");
            using var input = entries[0].Open();
            using var buffer = new MemoryStream();
            input.CopyTo(buffer);
            data = buffer.ToArray();
        }
        else data = File.ReadAllBytes(path);
        if (data.Length < 8 || BitConverter.ToUInt32(data, 0) != 0x504d4b54 || BitConverter.ToUInt32(data, 4) != 0x20)
            throw new InvalidDataException("Expected TKMP format version 0x20.");
        return new MemoryStream(data, writable: false);
    }

}

sealed class SafeFiles(string root) : ITkSystemProvider, ITkSystemSource, ITkModWriter
{
    private string relativeRoot = "";
    public static void CheckAncestors(string path)
    {
        for (var p = Path.GetFullPath(path); p != null; p = Path.GetDirectoryName(p))
            if (new FileInfo(p).LinkTarget != null || new DirectoryInfo(p).LinkTarget != null)
                throw new InvalidDataException("Symbolic links are not allowed in output paths.");
    }
    public static string Resolve(string root, string relative)
    {
        relative = relative.Replace('\\', '/');
        if (Path.IsPathRooted(relative) || relative.Contains(':')) throw new InvalidDataException("Absolute path in package.");
        var path = Path.GetFullPath(Path.Combine(root, relative));
        if (!path.StartsWith(Path.GetFullPath(root) + "/", StringComparison.Ordinal)) throw new InvalidDataException("Path escapes output directory.");
        CheckAncestors(path);
        return path;
    }
    public ITkModWriter GetSystemWriter(TkModContext context, bool enableRomfsBucketing = true) => new SafeFiles(Resolve(root, context.Id.ToString()));
    public ITkSystemSource GetSystemSource(string relativeFolderPath) => new SafeFiles(Resolve(root, relativeFolderPath));
    public ITkSystemSource GetRelative(string path) => new SafeFiles(Resolve(root, path));
    public Stream OpenRead(string path) => File.OpenRead(Resolve(root, path));
    public bool Exists(string path) => File.Exists(Resolve(root, path));
    public void SetRelativeFolder(string folder) { if (folder != "") Resolve(root, folder); relativeRoot = folder; }
    public Stream OpenWrite(string path)
    {
        var resolved = Resolve(root, Path.Combine(relativeRoot, path));
        Directory.CreateDirectory(Path.GetDirectoryName(resolved)!);
        return File.Open(resolved, FileMode.Create, FileAccess.Write, FileShare.None);
    }
}

sealed class MergeLogger : ILogger
{
    public int Warnings;
    public Action<string>? Report;
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel level) => level >= LogLevel.Warning;
    public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> format)
    {
        if (!IsEnabled(level)) return;
        Interlocked.Increment(ref Warnings);
        Report?.Invoke($"TkSharp {level}: {format(state, error)}");
    }
}
