using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace VoiceCallouts.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    public MainWindow(Plugin plugin)
        : base("Voice Callouts##VoiceCalloutsMainWindow")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(360, 260),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        var enabled = plugin.Configuration.Enabled;
        if (ImGui.Checkbox("Enabled", ref enabled))
        {
            plugin.Configuration.Enabled = enabled;
            plugin.Configuration.Save();
        }

        ImGui.SameLine();
        if (ImGui.Button("Settings"))
            plugin.ToggleConfigUi();

        ImGui.SameLine();
        if (ImGui.Button("Test Voice"))
            plugin.TtsService.Speak("Voice callouts test.");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextUnformatted("Warnings");
        ImGui.Spacing();

        var warningsEnabled = plugin.Configuration.WarningsEnabled;
        if (ImGui.Checkbox("Enabled##Warnings", ref warningsEnabled))
        {
            plugin.Configuration.WarningsEnabled = warningsEnabled;
            plugin.Configuration.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Master switch for the {warning} placeholder - see Settings > Announcement > Format.");

        ImGui.BeginDisabled(!warningsEnabled);

        var useManual = plugin.Configuration.UseManualWarnings;
        if (ImGui.Checkbox("Manual", ref useManual))
        {
            plugin.Configuration.UseManualWarnings = useManual;
            plugin.Configuration.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Your own entries from Settings > Ability warnings. Always wins over the sources below.");

        ImGui.SameLine();
        var useCactbot = plugin.Configuration.UseCactbotWarnings;
        if (ImGui.Checkbox($"Cactbot ({plugin.CactbotWarnings.Count})", ref useCactbot))
        {
            plugin.Configuration.UseCactbotWarnings = useCactbot;
            plugin.Configuration.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("A bundled, offline-extracted snapshot of cactbot's community fight data. Covers a real chunk of raids/trials/dungeons, but only where the callout is a fixed string - directional/conditional mechanics are left out rather than guessed at.");

        ImGui.SameLine();
        var useLumina = plugin.Configuration.UseLuminaShapeWarnings;
        if (ImGui.Checkbox("Game data", ref useLumina))
        {
            plugin.Configuration.UseLuminaShapeWarnings = useLumina;
            plugin.Configuration.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Live best-effort guess (FRONTAL/CIRCLE/LINE/CROSS) from the game's own Action data. Thin coverage - most casts won't get a shape from this.");

        ImGui.EndDisabled();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var inDuty = plugin.BossDetector.IsInDuty;
        var inCombat = plugin.BossDetector.InCombat;
        var listening = plugin.BossDetector.IsEncounterActive;

        ImGui.Text($"In combat: {(inCombat ? "yes" : "no")}");
        ImGui.Text($"Location: {(inDuty ? "instanced duty" : "open world")}");
        ImGui.Text($"Listening for boss casts: {(listening ? "yes" : "no")}");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextUnformatted("Recent callouts");
        ImGui.Spacing();

        using var child = ImRaii.Child("RecentCallouts", Vector2.Zero, true);
        if (child.Success)
        {
            if (plugin.RecentCallouts.Count == 0)
            {
                ImGui.TextDisabled("No callouts yet.");
            }
            else
            {
                foreach (var record in plugin.RecentCallouts)
                {
                    var warningSuffix = string.IsNullOrEmpty(record.Warning) ? "" : $" ({record.Warning})";
                    ImGui.TextUnformatted($"[{record.Time:HH:mm:ss}] {record.BossName}: {record.Ability}{warningSuffix}");
                }
            }
        }
    }
}
