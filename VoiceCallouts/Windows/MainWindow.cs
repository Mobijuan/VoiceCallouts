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
                    ImGui.TextUnformatted($"[{record.Time:HH:mm:ss}] {record.BossName}: {record.Ability}");
                }
            }
        }
    }
}
