using System.Text.Json;

namespace WindowsMacroManager;

internal enum MacroKind { None, MoveClickRestore, AltAutoClick }

internal sealed record Settings
{
    public bool Enabled { get; init; } = true;
    public MacroKind Mouse4 { get; init; } = MacroKind.AltAutoClick;
    public MacroKind Mouse5 { get; init; } = MacroKind.MoveClickRestore;
    public int? TargetX { get; init; }
    public int? TargetY { get; init; }
    public int DownUpMs { get; init; } = 1;
    public int BetweenClicksMs { get; init; } = 1;
    public int ClicksPerCycle { get; init; } = 17;
    public bool SuppressButtons { get; init; }

    public Settings Validate()
    {
        if (!Enum.IsDefined(Mouse4) || !Enum.IsDefined(Mouse5)) throw new InvalidDataException("Invalid button assignment.");
        if (DownUpMs is < 1 or > 10000 || BetweenClicksMs is < 1 or > 10000) throw new InvalidDataException("Delays must be between 1 and 10000 ms.");
        if (ClicksPerCycle is < 1 or > 100) throw new InvalidDataException("Clicks per cycle must be between 1 and 100.");
        if (TargetX.HasValue != TargetY.HasValue) throw new InvalidDataException("Target coordinates are incomplete.");
        return this;
    }
}

internal static class SettingsStore
{
    internal static string PathName => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WindowsMacroManager", "settings.json");
    internal static Settings Load(out string? warning)
    {
        warning=null;
        try
        {
            if (!File.Exists(PathName)) return new Settings();
            return (JsonSerializer.Deserialize<Settings>(File.ReadAllText(PathName)) ?? throw new InvalidDataException("Empty settings.")).Validate();
        }
        catch (Exception ex) { warning=$"Settings could not be loaded: {ex.Message}. Defaults are active."; return new Settings(); }
    }
    internal static void Save(Settings settings)
    {
        settings.Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(PathName)!);
        var temp=PathName+".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented=true }));
        File.Move(temp, PathName, true);
    }
}
