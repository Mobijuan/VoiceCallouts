using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace VoiceCallouts.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private readonly Configuration configuration;
    private string[] voiceNames = [];
    private string newWarningAbility = "";
    private string newWarningText = "";

    public ConfigWindow(Plugin plugin) : base("Voice Callouts Settings###VoiceCalloutsConfigWindow")
    {
        Flags = ImGuiWindowFlags.NoCollapse;

        Size = new Vector2(440, 520);
        SizeCondition = ImGuiCond.FirstUseEver;

        this.plugin = plugin;
        configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        // Flags must be added or removed before Draw() is being called, or they won't apply
        if (configuration.IsConfigWindowMovable)
            Flags &= ~ImGuiWindowFlags.NoMove;
        else
            Flags |= ImGuiWindowFlags.NoMove;
    }

    public override void Draw()
    {
        var enabled = configuration.Enabled;
        if (ImGui.Checkbox("Enable voice callouts", ref enabled))
        {
            configuration.Enabled = enabled;
            configuration.Save();
        }

        var movable = configuration.IsConfigWindowMovable;
        if (ImGui.Checkbox("Movable settings window", ref movable))
        {
            configuration.IsConfigWindowMovable = movable;
            configuration.Save();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextUnformatted("Where to listen");
        ImGui.Spacing();

        var inDuties = configuration.AnnounceInDuties;
        if (ImGui.Checkbox("Instanced duties (dungeons, trials, raids)", ref inDuties))
        {
            configuration.AnnounceInDuties = inDuties;
            configuration.Save();
        }

        var openWorld = configuration.AnnounceInOpenWorld;
        if (ImGui.Checkbox("Open world (FATEs, field bosses, etc.)", ref openWorld))
        {
            configuration.AnnounceInOpenWorld = openWorld;
            configuration.Save();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextUnformatted("Which abilities");
        ImGui.Spacing();

        var onlyCastTime = configuration.OnlyAnnounceCastsWithCastTime;
        if (ImGui.Checkbox("Only announce abilities with a cast bar", ref onlyCastTime))
        {
            configuration.OnlyAnnounceCastsWithCastTime = onlyCastTime;
            configuration.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("When off, instant-cast abilities are announced too.");

        var minCastTime = configuration.MinimumCastTimeSeconds;
        if (ImGui.SliderFloat("Minimum cast time (seconds)", ref minCastTime, 0f, 10f, "%.1f"))
        {
            configuration.MinimumCastTimeSeconds = minCastTime;
            configuration.Save();
        }

        var repeatSuppression = configuration.RepeatSuppressionSeconds;
        if (ImGui.SliderFloat("Repeat suppression (seconds)", ref repeatSuppression, 0f, 15f, "%.1f"))
        {
            configuration.RepeatSuppressionSeconds = repeatSuppression;
            configuration.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("How long to wait before the same enemy's same ability can be announced again.");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextUnformatted("Announcement");
        ImGui.Spacing();

        var format = configuration.AnnouncementFormat;
        if (ImGui.InputText("Format", ref format, 128))
        {
            configuration.AnnouncementFormat = format;
            configuration.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Available placeholders: {ability}, {name}, {warning} (mechanic text - see the Warnings section on the main window for sources, and the manual list below)");

        ImGui.Spacing();
        ImGui.TextUnformatted("Manual ability warnings");
        ImGui.TextWrapped("These always override the Cactbot/Game data sources (toggled on the main window) for the abilities listed here. Use this for anything those get wrong or miss for the fight you're in. Match is by the ability's exact spoken name (case-insensitive).");
        ImGui.Spacing();

        string? toRemove = null;
        foreach (var (name, warning) in configuration.AbilityWarnings)
        {
            ImGui.TextUnformatted($"{name}: {warning}");
            ImGui.SameLine();
            if (ImGui.SmallButton($"Remove##{name}"))
                toRemove = name;
        }

        if (toRemove != null)
        {
            configuration.AbilityWarnings.Remove(toRemove);
            configuration.Save();
        }

        ImGui.Spacing();
        ImGui.SetNextItemWidth(160);
        ImGui.InputText("##NewWarningAbility", ref newWarningAbility, 128);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(120);
        ImGui.InputText("##NewWarningText", ref newWarningText, 64);
        ImGui.SameLine();
        var canAdd = !string.IsNullOrWhiteSpace(newWarningAbility) && !string.IsNullOrWhiteSpace(newWarningText);
        ImGui.BeginDisabled(!canAdd);
        if (ImGui.Button("Add"))
        {
            configuration.AbilityWarnings[newWarningAbility.Trim()] = newWarningText.Trim();
            configuration.Save();
            newWarningAbility = "";
            newWarningText = "";
        }
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Ability name, then the text to speak/show after it");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextUnformatted("Voice");
        ImGui.Spacing();

        if (voiceNames.Length == 0)
            voiceNames = plugin.TtsService.GetAvailableVoiceNames();

        var currentVoiceLabel = configuration.TtsVoiceName ?? "(System default)";
        if (ImGui.BeginCombo("Voice", currentVoiceLabel))
        {
            if (ImGui.Selectable("(System default)", configuration.TtsVoiceName == null))
            {
                configuration.TtsVoiceName = null;
                configuration.Save();
            }

            foreach (var voice in voiceNames)
            {
                if (ImGui.Selectable(voice, configuration.TtsVoiceName == voice))
                {
                    configuration.TtsVoiceName = voice;
                    configuration.Save();
                }
            }

            ImGui.EndCombo();
        }

        var rate = configuration.TtsRate;
        if (ImGui.SliderInt("Rate", ref rate, -10, 10))
        {
            configuration.TtsRate = rate;
            configuration.Save();
        }

        var volume = configuration.TtsVolume;
        if (ImGui.SliderInt("Volume", ref volume, 0, 100))
        {
            configuration.TtsVolume = volume;
            configuration.Save();
        }

        ImGui.Spacing();
        if (ImGui.Button("Test Voice"))
            plugin.TtsService.Speak("Voice callouts test.");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextUnformatted("Diagnostics");
        ImGui.Spacing();

        var logDiagnostics = configuration.LogActionDiagnostics;
        if (ImGui.Checkbox("Log raw ability data to /xllog", ref logDiagnostics))
        {
            configuration.LogActionDiagnostics = logDiagnostics;
            configuration.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Dumps every field the game's Action data has for each announced ability to the plugin log. Useful for confirming an ability's exact name/id - leave off for normal use, it's noisy.");
    }
}
