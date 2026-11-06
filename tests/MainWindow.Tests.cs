// SPDX-License-Identifier: AGPL-3.0-or-later
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;

public partial class MainWindow
{
    private readonly List<string> stages = [];
    private async Task<bool> RunTestsIfRequested()
    {
        if (Environment.GetEnvironmentVariable("TKCL_STUDIO_SMOKE") != "1") return false;
        await Smoke();
        return true;
    }
    private async Task SaveScreenshot(string name)
    {
        await Task.Delay(200);
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Bounds.Width, (int)Bounds.Height), new Vector(96, 96));
        bitmap.Render(this);
        bitmap.Save(SafeFiles.Resolve(AppPaths.Root, name));
    }

    private async Task Smoke()
    {
        try
        {
            if (AppPaths.Load() != null || !string.IsNullOrEmpty(PathOf(Base))) throw new Exception("Smoke test needs fresh settings.");
            await SaveScreenshot("ui-empty.png");
            var inputs = JsonSerializer.Deserialize<SavedPaths>(Environment.GetEnvironmentVariable("TKCL_STUDIO_SMOKE_INPUTS") ?? "")!;
            SetPath(Base, inputs.Base); SetPath(Update, inputs.Update); SetPath(Prod, inputs.Prod); SetPath(TitleKeys, inputs.Title);
            await Scan(inputs.Mod);
            if (package?.Name != "Dark Mode UI" || package.Groups.Length != 1) throw new Exception("Actual package options not loaded.");
            var checkbox = Find<StackPanel>("OptionsPanel").Children.OfType<StackPanel>().SelectMany(p => p.Children).OfType<CheckBox>().Single();
            if (checkbox.IsChecked == true) throw new Exception("Packaged default unexpectedly selected.");
            checkbox.IsChecked = true;
            await SaveScreenshot("ui-options.png");
            await Build();
            if (output == null || !Directory.Exists(output) || !Find<Button>("OpenOutput").IsVisible || !stages.Contains("Merging selected options"))
                throw new Exception("GUI build or stage reporting failed.");
            if (AppPaths.Load()?.Prod != inputs.Prod) throw new Exception("Paths were not remembered.");
            Remember.IsChecked = false;
            if (File.Exists(AppPaths.Settings)) throw new Exception("Unchecking remember did not forget paths.");
            await SaveScreenshot("ui-complete.png");
            File.WriteAllText(SafeFiles.Resolve(AppPaths.Root, "smoke-result.json"), JsonSerializer.Serialize(new { Success = true, Output = output, Stages = stages }));
        }
        catch (Exception ex)
        {
            File.WriteAllText(SafeFiles.Resolve(AppPaths.Root, "smoke-result.json"), JsonSerializer.Serialize(new { Success = false, Error = ex.ToString() }));
            Environment.ExitCode = 1;
        }
        finally { busy = false; Close(); }
    }
}
