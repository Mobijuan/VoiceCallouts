using Dalamud.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace VoiceCallouts;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    /// <summary>Master on/off switch for the whole plugin.</summary>
    public bool Enabled { get; set; } = true;

    // --- Where to listen ---

    /// <summary>Announce boss casts while inside an instanced duty (dungeon/trial/raid/alliance raid).</summary>
    public bool AnnounceInDuties { get; set; } = true;

    /// <summary>Announce boss casts in the open world (FATEs, field/notorious bosses, etc.).</summary>
    public bool AnnounceInOpenWorld { get; set; } = true;

    // --- Which abilities ---

    /// <summary>Casts shorter than this are skipped (instant casts have a cast time of 0).</summary>
    public float MinimumCastTimeSeconds { get; set; } = 0f;

    /// <summary>
    /// How long to wait before the same ability can be announced again, regardless of which
    /// enemy casts it - so a pull with several identical adds casting the same ability around
    /// the same time is announced once, not once per add.
    /// </summary>
    public float RepeatSuppressionSeconds { get; set; } = 2.5f;


    // --- Announcement text ---

    /// <summary>Include the boss's name in the spoken announcement (e.g. "Ifrit").</summary>
    public bool AnnounceBossName { get; set; } = false;

    /// <summary>Include the ability's name in the spoken announcement (e.g. "Sidewise Spark").</summary>
    public bool AnnounceAbilityName { get; set; } = true;

    /// <summary>
    /// Include mechanic text in the spoken announcement, from whichever source in
    /// <see cref="Services.AbilityWarningResolver"/> matched first. Has no effect when there's
    /// nothing to say for that ability.
    /// </summary>
    public bool AnnounceWarning { get; set; } = true;

    /// <summary>Master toggle for the warning feature as a whole - see <see cref="Services.AbilityWarningResolver"/>.</summary>
    public bool WarningsEnabled { get; set; } = true;

    /// <summary>Use your own <see cref="AbilityWarnings"/> entries. Always takes priority over the other sources.</summary>
    public bool UseManualWarnings { get; set; } = true;

    /// <summary>Use the bundled, offline-extracted snapshot of cactbot's fight data (<see cref="Services.CactbotWarnings"/>).</summary>
    public bool UseCactbotWarnings { get; set; } = true;

    /// <summary>Use the live guess from the game's own Action sheet data (<see cref="Services.AbilityShapeClassifier"/>). Thin coverage, checked last.</summary>
    public bool UseLuminaShapeWarnings { get; set; } = true;

    /// <summary>
    /// Your own mechanic notes. Fill these in for anything the automatic sources above miss or
    /// get wrong for the specific fight you're in - this always overrides them. Only spoken when
    /// <see cref="AnnounceWarning"/> is on. Manage these from the Ability Warnings window (button
    /// in Settings), or by clicking a recent callout on the main window.
    /// </summary>
    public List<AbilityWarningEntry> AbilityWarnings { get; set; } = new();

    /// <summary>Next id to hand out in <see cref="AddOrUpdateAbilityWarning"/> - never reused, even after deletions.</summary>
    public int NextAbilityWarningId { get; set; } = 1;

    /// <summary>Finds a manual warning entry by creature + ability name (both case-insensitive).</summary>
    public AbilityWarningEntry? FindAbilityWarning(string creatureName, string abilityName) =>
        AbilityWarnings.FirstOrDefault(e =>
            string.Equals(e.CreatureName, creatureName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(e.AbilityName, abilityName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Updates the warning text of the matching (creature, ability) entry if one exists, or adds
    /// a new one with the next auto-incremented id. Does not save - call <see cref="Save"/>.
    /// </summary>
    public AbilityWarningEntry AddOrUpdateAbilityWarning(string zone, string creatureName, string abilityName, string warning)
    {
        var existing = FindAbilityWarning(creatureName, abilityName);
        if (existing != null)
        {
            existing.Warning = warning;
            return existing;
        }

        var entry = new AbilityWarningEntry
        {
            Id = NextAbilityWarningId++,
            Zone = zone,
            CreatureName = creatureName,
            AbilityName = abilityName,
            Warning = warning,
        };
        AbilityWarnings.Add(entry);
        return entry;
    }

    // --- Text-to-speech ---

    /// <summary>Installed SAPI voice name to use, or null for the system default voice.</summary>
    public string? TtsVoiceName { get; set; } = null;

    /// <summary>SAPI speech rate, -10 (slowest) to 10 (fastest).</summary>
    public int TtsRate { get; set; } = 0;

    /// <summary>SAPI speech volume, 0 to 100.</summary>
    public int TtsVolume { get; set; } = 100;

    // --- Diagnostics ---

    /// <summary>
    /// When true, every announced cast also dumps the full set of fields the game's Action
    /// data sheet has for that ability to the plugin log (visible via /xllog). Useful for
    /// confirming an ability's exact name (for a new <see cref="AbilityWarnings"/> entry) or
    /// otherwise poking at its raw game data.
    /// </summary>
    public bool LogActionDiagnostics { get; set; } = false;

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
