using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Config;
using Dalamud.Interface.Windowing;

namespace VoiceCallouts.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private readonly Configuration configuration;
    private string[] voiceNames = [];

    public ConfigWindow(Plugin plugin) : base("Voice Callouts Settings###VoiceCalloutsConfigWindow")
    {
        Flags = ImGuiWindowFlags.NoCollapse;

        Size = new Vector2(440, 520);
        SizeCondition = ImGuiCond.FirstUseEver;

        this.plugin = plugin;
        configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void Draw()
    {
        var enabled = configuration.Enabled;
        if (ImGui.Checkbox("Enable voice callouts", ref enabled))
        {
            configuration.Enabled = enabled;
            configuration.Save();
        }

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
        ImGui.TextUnformatted("Game volume");
        ImGui.Spacing();
        ImGui.TextWrapped("Game master volume slider. Adjust if the voice callouts are being drowned out");

        if (Plugin.GameConfig.TryGet(SystemConfigOption.SoundMaster, out uint masterVolume) &&
            Plugin.GameConfig.TryGet(SystemConfigOption.SoundMaster, out UIntConfigProperties? masterVolumeProps) &&
            masterVolumeProps != null)
        {
            var masterVolumeInt = (int)masterVolume;
            if (ImGui.SliderInt("Master volume", ref masterVolumeInt, (int)masterVolumeProps.Minimum, (int)masterVolumeProps.Maximum))
                Plugin.GameConfig.Set(SystemConfigOption.SoundMaster, (uint)masterVolumeInt);
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
            ImGui.SetTooltip("How long to wait before the same ability can be announced again, regardless of which enemy casts it (prevents spam when several identical adds cast the same thing at once).");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextUnformatted("Announcement");
        ImGui.Spacing();

        var announceBossName = configuration.AnnounceBossName;
        if (ImGui.Checkbox("Boss name", ref announceBossName))
        {
            configuration.AnnounceBossName = announceBossName;
            configuration.Save();
        }

        var announceAbilityName = configuration.AnnounceAbilityName;
        if (ImGui.Checkbox("Ability name", ref announceAbilityName))
        {
            configuration.AnnounceAbilityName = announceAbilityName;
            configuration.Save();
        }

        var announceWarning = configuration.AnnounceWarning;
        if (ImGui.Checkbox("Warning", ref announceWarning))
        {
            configuration.AnnounceWarning = announceWarning;
            configuration.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Mechanic text from the sources below, and your manual entries. Only spoken when there's something to say.");

        ImGui.Indent();
        ImGui.BeginDisabled(!announceAbilityName);
        var onlyAbilityNameIfNoWarning = configuration.OnlyAnnounceAbilityNameIfNoWarning;
        if (ImGui.Checkbox("Only say ability name if no warning was found", ref onlyAbilityNameIfNoWarning))
        {
            configuration.OnlyAnnounceAbilityNameIfNoWarning = onlyAbilityNameIfNoWarning;
            configuration.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("When on, the ability name is skipped whenever a warning was found for it (e.g. just \"FRONTAL\" instead of \"Sidewise Spark, FRONTAL\") - only enabled while Ability name above is on.");
        ImGui.EndDisabled();
        ImGui.Unindent();

        ImGui.Spacing();
        ImGui.TextUnformatted("Warning sources");
        ImGui.Spacing();

        var warningsEnabled = configuration.WarningsEnabled;
        if (ImGui.Checkbox("Enabled##Warnings", ref warningsEnabled))
        {
            configuration.WarningsEnabled = warningsEnabled;
            configuration.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Master switch for the Warning announcement above.");

        ImGui.BeginDisabled(!warningsEnabled);

        var useManual = configuration.UseManualWarnings;
        if (ImGui.Checkbox("Manual", ref useManual))
        {
            configuration.UseManualWarnings = useManual;
            configuration.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Your own entries from the Custom Warnings window. Always wins over the sources below.");

        ImGui.SameLine();
        var useCactbot = configuration.UseCactbotWarnings;
        if (ImGui.Checkbox($"Cactbot ({plugin.CactbotWarnings.Count})", ref useCactbot))
        {
            configuration.UseCactbotWarnings = useCactbot;
            configuration.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("A bundled, offline-extracted snapshot of cactbot's community fight data. Covers a real chunk of raids/trials/dungeons, but only where the callout is a fixed string - directional/conditional mechanics are left out rather than guessed at.");

        ImGui.SameLine();
        var useLumina = configuration.UseLuminaShapeWarnings;
        if (ImGui.Checkbox("Game data", ref useLumina))
        {
            configuration.UseLuminaShapeWarnings = useLumina;
            configuration.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Live best-effort guess (FRONTAL/CIRCLE/LINE/CROSS) from the game's own Action data. Thin coverage - most casts won't get a shape from this. Off by default since it's the least reliable source.");

        ImGui.EndDisabled();

        ImGui.Spacing();
        ImGui.TextWrapped($"Manual ability warnings always override the Cactbot/Game data sources for the (creature, ability) pairs listed there - {configuration.AbilityWarnings.Count} entries currently.");
        if (ImGui.Button("Custom Warnings"))
            plugin.ToggleAbilityWarningsUi();

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
