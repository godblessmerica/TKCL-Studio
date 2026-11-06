// SPDX-License-Identifier: AGPL-3.0-or-later
using System.Diagnostics;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Platform.Storage;

public partial class MainWindow : Window
{
    private PackageInfo? package;
    private bool busy;
    private string? output;
    private readonly List<Func<int[]>> selections = [];
    private TextBox Mod => Find<TextBox>("ModPath");
    private TextBox Base => Find<TextBox>("BasePath");
    private TextBox Update => Find<TextBox>("UpdatePath");
    private TextBox Prod => Find<TextBox>("ProdPath");
    private TextBox TitleKeys => Find<TextBox>("TitlePath");
    private CheckBox Remember => Find<CheckBox>("RememberPaths");
    private Button BuildButton => Find<Button>("BuildButton");
    private TextBlock Status => Find<TextBlock>("StatusText");
    private TextBlock Stage => Find<TextBlock>("StageText");
    private ProgressBar Progress => Find<ProgressBar>("BuildProgress");
    private T Find<T>(string name) where T : Control => this.FindControl<T>(name)!;

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        Find<Button>("PickMod").Click += async (_, _) =>
        {
            var path = await Pick("Choose a TKCL mod package", ["*.tkcl", "*.zip"]);
            if (path != null) await Scan(path);
        };
        BindPicker("PickBase", Base, "Choose your TotK base game", ["*.nsp", "*.xci"]);
        BindPicker("PickUpdate", Update, "Choose your TotK 1.4.3 update", ["*.nsp"]);
        BindPicker("PickProd", Prod, "Choose prod.keys", ["*.keys"]);
        BindPicker("PickTitle", TitleKeys, "Choose title.keys (optional)", ["*.keys"]);
        Find<Button>("ClearTitle").Click += (_, _) => { SetPath(TitleKeys, ""); SavePaths(); RefreshReady(); };
        Remember.IsCheckedChanged += (_, _) => SavePaths();
        BuildButton.Click += async (_, _) => await Build();
        Find<Button>("OpenOutput").Click += (_, _) =>
        {
            if (output == null || !Directory.Exists(output)) return;
            try
            {
                var start = new ProcessStartInfo("xdg-open") { UseShellExecute = false };
                start.ArgumentList.Add(output);
                Process.Start(start)?.Dispose();
            }
            catch (Exception ex) { ShowError("Could not open the output folder: " + ex.Message); }
        };
        Closing += (_, e) =>
        {
            if (!busy) return;
            e.Cancel = true;
            Status.Text = "Wait for the current operation to finish before closing.";
        };
        Opened += async (_, _) =>
        {
#if STUDIO_TESTS
            if (await RunTestsIfRequested()) return;
#endif
            try
            {
                var saved = AppPaths.Load();
                if (saved == null) return;
                SetPath(Base, saved.Base); SetPath(Update, saved.Update);
                SetPath(Prod, saved.Prod); SetPath(TitleKeys, saved.Title);
                if (File.Exists(saved.Mod)) await Scan(saved.Mod);
                else if (!string.IsNullOrEmpty(saved.Mod)) Status.Text = "Your last mod package moved. Choose it again.";
            }
            catch (Exception ex) { ShowError("Saved paths could not be loaded. Choose your files again. " + ex.Message); }
        };
    }

    private void BindPicker(string name, TextBox field, string title, string[] patterns)
    {
        Find<Button>(name).Click += async (_, _) =>
        {
            var path = await Pick(title, patterns);
            if (path == null) return;
            SetPath(field, path);
            SavePaths();
            RefreshReady();
        };
    }

    private async Task<string?> Pick(string title, string[] patterns)
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new()
            {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter = [new("Supported files") { Patterns = patterns }]
            });
            var path = files.FirstOrDefault()?.TryGetLocalPath();
            if (files.Count > 0 && path == null) ShowError("Choose a file on this computer.");
            return path;
        }
        catch (Exception ex) { ShowError("Could not open the file picker: " + ex.Message); return null; }
    }

    private static void SetPath(TextBox field, string path)
    {
        field.Tag = path;
        field.Text = string.IsNullOrEmpty(path) ? "" : Path.GetFileName(path);
        ToolTip.SetTip(field, path);
    }

    private static string PathOf(TextBox field) => field.Tag as string ?? "";

    private async Task Scan(string path)
    {
        SetPath(Mod, path);
        package = null;
        selections.Clear();
        Find<StackPanel>("OptionsPanel").Children.Clear();
        Find<TextBlock>("ModName").Text = "Reading package…";
        Find<TextBlock>("ModDetails").Text = "Scanning the mod's own options.";
        Find<TextBox>("LogBox").Text = "";
        SetBusy(true);
        output = null;
        Find<Button>("OpenOutput").IsVisible = false;
        PackageInfo? scanned = null;
        var result = await RunTool(["inspect", path], info => scanned = info);
        // Inspection can publish metadata before later validation fails.
        if (result == 0 && scanned != null)
        {
            package = scanned;
            RenderOptions(scanned);
            SavePaths();
            Status.Text = "Package scanned. Choose your game files and build.";
            Stage.Text = "Ready";
        }
        else
        {
            Find<TextBlock>("ModName").Text = "Package could not be read";
            Find<TextBlock>("ModDetails").Text = "Choose a TKCL package or ZIP containing one TKCL file.";
            Status.Text = "Could not scan the package. Open Build details for the error.";
            Stage.Text = "Error";
            Find<Expander>("DetailsExpander").IsExpanded = true;
        }
        SetBusy(false);
    }

    private void RenderOptions(PackageInfo info)
    {
        Find<TextBlock>("ModName").Text = info.Name;
        Find<TextBlock>("ModDetails").Text = $"{info.Author} · v{info.Version}";
        ToolTip.SetTip(Find<TextBlock>("ModName"), info.Description);
        var panel = Find<StackPanel>("OptionsPanel");
        foreach (var group in info.Groups)
        {
            var content = new StackPanel { Spacing = 6 };
            content.Children.Add(new TextBlock { Text = group.Name, FontWeight = FontWeight.Medium, TextWrapping = TextWrapping.Wrap });
            if (!string.IsNullOrWhiteSpace(group.Description))
                content.Children.Add(new TextBlock { Text = group.Description, FontSize = 12, Foreground = Brush.Parse("#ADB0B7"), TextWrapping = TextWrapping.Wrap });
            if (group.Type is 2 or 3 && !(group.Type == 2 && group.Options.Length == 1))
            {
                var optional = group.Type == 2;
                var names = (optional ? new[] { "None" } : Array.Empty<string>()).Concat(group.Options.Select(o => o.Name)).ToArray();
                var selected = Array.FindIndex(group.Options, o => o.Selected);
                var combo = new ComboBox { ItemsSource = names, SelectedIndex = selected < 0 ? (optional ? 0 : -1) : selected + (optional ? 1 : 0) };
                combo.SelectionChanged += (_, _) => RefreshReady();
                content.Children.Add(combo);
                selections.Add(() => combo.SelectedIndex < (optional ? 1 : 0) ? [] : [combo.SelectedIndex - (optional ? 1 : 0)]);
            }
            else
            {
                var checks = group.Options.Select(option => new CheckBox
                {
                    Content = new TextBlock { Text = option.Name, TextWrapping = TextWrapping.Wrap },
                    IsChecked = option.Selected
                }).ToArray();
                foreach (var (check, option) in checks.Zip(group.Options))
                {
                    ToolTip.SetTip(check, option.Description);
                    check.IsCheckedChanged += (_, _) => RefreshReady();
                    content.Children.Add(check);
                }
                selections.Add(() => checks.Select((c, i) => (c, i)).Where(x => x.c.IsChecked == true).Select(x => x.i).ToArray());
            }
            panel.Children.Add(content);
        }
        if (info.Groups.Length == 0)
            panel.Children.Add(new TextBlock { Text = "This package has no optional features.", TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse("#ADB0B7") });
    }

    private void RefreshReady()
    {
        var valid = package != null && selections.Count == package.Groups.Length &&
            new[] { PathOf(Mod), PathOf(Base), PathOf(Update), PathOf(Prod) }.All(p => !string.IsNullOrEmpty(p) && File.Exists(p)) &&
            (string.IsNullOrEmpty(PathOf(TitleKeys)) || File.Exists(PathOf(TitleKeys)));
        if (valid)
            valid = package!.Groups.Select((g, i) => g.Type is not (1 or 3) || selections[i]().Length > 0).All(x => x);
        BuildButton.IsEnabled = !busy && valid;
        ToolTip.SetTip(BuildButton, valid ? "Build the selected mod for game 1.4.3" : "Choose the required files and any required mod options first.");
    }

    private void SetBusy(bool value)
    {
        busy = value;
        Find<StackPanel>("FilesPanel").IsEnabled = !value;
        Find<StackPanel>("OptionsPanel").IsEnabled = !value;
        Remember.IsEnabled = !value;
        Find<Button>("OpenOutput").IsEnabled = !value;
        Progress.IsIndeterminate = value;
        if (value) Progress.Value = 0;
        RefreshReady();
    }

    private async Task Build()
    {
        RefreshReady();
        if (!BuildButton.IsEnabled || package == null) return;
        var args = new List<string> { "merge", PathOf(Mod)!, "--base", PathOf(Base)!, "--update", PathOf(Update)!,
            "--prod-keys", PathOf(Prod)!, "--selections", JsonSerializer.Serialize(selections.Select(s => s()).ToArray()) };
        if (!string.IsNullOrEmpty(PathOf(TitleKeys))) args.AddRange(["--title-keys", PathOf(TitleKeys)]);
        Find<TextBox>("LogBox").Text = "";
        Find<Button>("OpenOutput").IsVisible = false;
        output = null;
#if STUDIO_TESTS
        stages.Clear();
#endif
        SetBusy(true);
        SavePaths();
        var result = await RunTool(args.ToArray());
        // Flush queued output/progress events before deciding whether the output exists.
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        SetBusy(false);
        if (result == 0 && output != null && Directory.Exists(output))
        {
            Progress.Value = 4;
            Status.Text = "Done. Copy the output folder into Recomp's mods folder.";
            Stage.Text = "Complete";
            Find<Button>("OpenOutput").IsVisible = true;
        }
        else
        {
            Progress.Value = 0;
            Status.Text = "Build failed. Check the game/update and key files; see Build details.";
            Stage.Text = "Error";
            Find<Expander>("DetailsExpander").IsExpanded = true;
        }
    }

    private async Task<int> RunTool(string[] args, Action<PackageInfo>? inspected = null)
    {
        try { return await Task.Run(() => Converter.Run(args, AppPaths.Root, Log, OnStage, inspected)); }
        catch (Exception ex) { Log("Error: " + ex.GetBaseException().Message); return 1; }
    }

    private void OnStage(string text) => Dispatcher.UIThread.Post(() =>
    {
#if STUDIO_TESTS
        stages.Add(text);
#endif
        Status.Text = text + (text == "Build complete" ? "" : "…");
        Stage.Text = text switch
        {
            "Reading mod package" => "1 / 4",
            "Opening local game files" => "2 / 4",
            "Merging selected options" => "3 / 4",
            "Checking output files" => "4 / 4",
            _ => "Complete"
        };
    });

    private void Log(string text) => Dispatcher.UIThread.Post(() =>
    {
        var log = Find<TextBox>("LogBox");
        log.Text = (log.Text + text + "\n");
        if (log.Text.Length > 24000) log.Text = log.Text[^24000..];
        log.CaretIndex = log.Text.Length;
        if (text.StartsWith("Output: ", StringComparison.Ordinal)) output = text[8..];
    });

    private void SavePaths()
    {
        try
        {
            AppPaths.Save(Remember.IsChecked == true ? new(PathOf(Mod), PathOf(Base), PathOf(Update), PathOf(Prod), PathOf(TitleKeys)) : null);
        }
        catch (Exception ex) { ShowError("Could not save file paths: " + ex.Message); }
    }

    private void ShowError(string message) { Status.Text = message; Stage.Text = "Error"; }

}
