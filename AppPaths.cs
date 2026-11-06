// SPDX-License-Identifier: AGPL-3.0-or-later
using System.Text.Json;

public sealed record SavedPaths(string Mod, string Base, string Update, string Prod, string Title);

public static class AppPaths
{
    public static string Root { get; } = CreateRoot();
    public static string Settings => SafeFiles.Resolve(Root, "settings.json");

    private static string CreateRoot()
    {
        var image = Environment.GetEnvironmentVariable("APPIMAGE");
        var parent = Path.GetDirectoryName(image ?? Environment.ProcessPath) ?? AppContext.BaseDirectory;
        var root = Path.GetFullPath(Environment.GetEnvironmentVariable("TKCL_STUDIO_HOME") ?? Path.Combine(parent, "TKCL-Studio-data"));
        SafeFiles.CheckAncestors(root);
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(SafeFiles.Resolve(root, ".cache"));
        return root;
    }

    public static SavedPaths? Load()
    {
        if (!File.Exists(Settings)) return null;
        return JsonSerializer.Deserialize<SavedPaths>(File.ReadAllText(Settings));
    }

    public static void Save(SavedPaths? paths)
    {
        if (paths == null) { if (File.Exists(Settings)) File.Delete(Settings); return; }
        var temp = SafeFiles.Resolve(Root, "settings." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(paths));
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(temp, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            File.Move(temp, Settings, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
