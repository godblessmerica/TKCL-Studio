// SPDX-License-Identifier: AGPL-3.0-or-later
using TkSharp.Core.Models;

public static partial class Converter
{
    private static int SelfTest(string projectRoot, Action<string> report)
    {
        foreach (var bad in new[] { "../escape", "/escape", "a/../../escape", "a\\..\\..\\escape", "C:/escape" })
        {
            try { SafeFiles.Resolve(projectRoot, bad); throw new Exception("Traversal accepted"); }
            catch (InvalidDataException) { }
        }
        var link = Path.Combine(projectRoot, ".cache/test-link");
        Directory.CreateSymbolicLink(link, Path.Combine(projectRoot, ".cache"));
        try
        {
            try { SafeFiles.Resolve(projectRoot, ".cache/test-link/file"); throw new Exception("Symlink accepted"); }
            catch (InvalidDataException) { }
        }
        finally { Directory.Delete(link); }
        report("PASS: traversal, absolute paths, Windows paths, symlink escape rejected.");
        var testMod = new TkMod();
        var testGroup = new TkModOptionGroup { Type = OptionGroupType.Single };
        var oldOption = new TkModOption { Name = "Old" };
        var newOption = new TkModOption { Name = "New" };
        testGroup.Options.Add(oldOption);
        testGroup.Options.Add(newOption);
        testMod.OptionGroups.Add(testGroup);
        var testSelection = new TkProfileMod(testMod);
        testSelection.EnsureOptionSelection();
        testSelection.SelectedOptions[testGroup].Add(oldOption);
        SelectOption(testSelection, "New");
        if (!testSelection.SelectedOptions[testGroup].SetEquals([newOption])) throw new Exception("Single option replacement failed.");
        testGroup.Type = OptionGroupType.Multi;
        SelectOption(testSelection, "Old");
        if (!testSelection.SelectedOptions[testGroup].SetEquals([oldOption, newOption])) throw new Exception("Multi option selection failed.");
        try { SelectOption(testSelection, "Missing"); throw new Exception("Unknown option accepted."); }
        catch (ArgumentException) { }
        ApplySelections(testSelection, "[[1]]");
        if (!testSelection.SelectedOptions[testGroup].SetEquals([newOption])) throw new Exception("Indexed selection failed.");
        ApplySelections(testSelection, "[[]]");
        if (testSelection.SelectedOptions[testGroup].Count != 0) throw new Exception("Explicit deselection failed.");
        testGroup.Type = OptionGroupType.MultiRequired;
        try { ApplySelections(testSelection, "[[]]"); throw new Exception("Missing required option accepted."); }
        catch (ArgumentException) { }
        report("PASS: named/indexed option selection, explicit deselection and required-option validation.");
        return 0;
    }
}
