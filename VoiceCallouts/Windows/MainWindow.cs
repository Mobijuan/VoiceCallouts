using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace VoiceCallouts.Windows;

public class MainWindow : Window, IDisposable
{
    private const string AddWarningPopupId = "AddAbilityWarning";

    private readonly Plugin plugin;
    private string editingAbility = "";
    private string editingWarningText = "";

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
            ImGui.SetTooltip("Master switch for the Warning announcement - see Settings > Announcement.");

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
        ImGui.TextDisabled("Click one to add/edit a manual warning for that ability.");
        ImGui.Spacing();

        using (var child = ImRaii.Child("RecentCallouts", Vector2.Zero, true))
        {
            if (child.Success)
            {
                if (plugin.RecentCallouts.Count == 0)
                {
                    ImGui.TextDisabled("No callouts yet.");
                }
                else
                {
                    for (var i = 0; i < plugin.RecentCallouts.Count; i++)
                    {
                        var record = plugin.RecentCallouts[i];
                        var warningSuffix = string.IsNullOrEmpty(record.Warning) ? "" : $" ({record.Warning})";
                        if (ImGui.Selectable($"[{record.Time:HH:mm:ss}] {record.BossName}: {record.Ability}{warningSuffix}##callout{i}"))
                        {
                            editingAbility = record.Ability;
                            editingWarningText = FindExistingWarning(record.Ability) ?? "";
                            ImGui.OpenPopup(AddWarningPopupId);
                        }
                    }
                }

                // OpenPopup/BeginPopup resolve their id relative to the *current* window, so
                // this has to run inside the same child region OpenPopup was called from above -
                // calling it after the child closes (back in the parent window's id scope) means
                // the two calls hash to different popup ids and the popup silently never opens.
                DrawAddWarningPopup();
            }
        }
    }

    private void DrawAddWarningPopup()
    {
        if (!ImGui.BeginPopup(AddWarningPopupId))
            return;

        ImGui.TextUnformatted($"Warning for: {editingAbility}");
        ImGui.Spacing();
        ImGui.SetNextItemWidth(200);
        ImGui.InputText("##EditWarningText", ref editingWarningText, 64);

        var existingKey = FindExistingWarningKey(editingAbility);

        if (ImGui.Button("Save"))
        {
            if (!string.IsNullOrWhiteSpace(editingWarningText))
            {
                plugin.Configuration.AbilityWarnings[existingKey ?? editingAbility] = editingWarningText.Trim();
                plugin.Configuration.Save();
            }
            ImGui.CloseCurrentPopup();
        }

        if (existingKey != null)
        {
            ImGui.SameLine();
            if (ImGui.Button("Remove"))
            {
                plugin.Configuration.AbilityWarnings.Remove(existingKey);
                plugin.Configuration.Save();
                ImGui.CloseCurrentPopup();
            }
        }

        ImGui.SameLine();
        if (ImGui.Button("Cancel"))
            ImGui.CloseCurrentPopup();

        ImGui.EndPopup();
    }

    private string? FindExistingWarningKey(string abilityName)
    {
        foreach (var key in plugin.Configuration.AbilityWarnings.Keys)
        {
            if (string.Equals(key, abilityName, StringComparison.OrdinalIgnoreCase))
                return key;
        }

        return null;
    }

    private string? FindExistingWarning(string abilityName)
    {
        var key = FindExistingWarningKey(abilityName);
        return key != null ? plugin.Configuration.AbilityWarnings[key] : null;
    }
}
