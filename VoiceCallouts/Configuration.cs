using Dalamud.Configuration;
using System;
using System.Collections.Generic;

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

    /// <summary>If true, only abilities with a visible cast bar (a nonzero cast time) are announced.</summary>
    public bool OnlyAnnounceCastsWithCastTime { get; set; } = true;

    /// <summary>Casts shorter than this are skipped, even when they have a cast time.</summary>
    public float MinimumCastTimeSeconds { get; set; } = 0f;

    /// <summary>How long to wait before the same enemy's same ability can be announced again.</summary>
    public float RepeatSuppressionSeconds { get; set; } = 2.5f;


    // --- Announcement text ---

    /// <summary>
    /// Template for the spoken text. Supports {ability} and {name} placeholders, plus {warning}
    /// (mechanic text from whichever source in <see cref="Services.AbilityWarningResolver"/>
    /// matched first, blank if none did).
    /// </summary>
    public string AnnouncementFormat { get; set; } = "{ability} {warning}";

    /// <summary>Master toggle for the {warning} feature as a whole - see <see cref="Services.AbilityWarningResolver"/>.</summary>
    public bool WarningsEnabled { get; set; } = true;

    /// <summary>Use your own <see cref="AbilityWarnings"/> entries. Always takes priority over the other sources.</summary>
    public bool UseManualWarnings { get; set; } = true;

    /// <summary>Use the bundled, offline-extracted snapshot of cactbot's fight data (<see cref="Services.CactbotWarnings"/>).</summary>
    public bool UseCactbotWarnings { get; set; } = true;

    /// <summary>Use the live guess from the game's own Action sheet data (<see cref="Services.AbilityShapeClassifier"/>). Thin coverage, checked last.</summary>
    public bool UseLuminaShapeWarnings { get; set; } = true;

    /// <summary>
    /// Your own mechanic notes, keyed by ability name (matched case-insensitively) - e.g.
    /// "Sidewise Spark" -> "FRONTAL", "Bad Breath" -> "GET OUT". Fill these in for anything the
    /// automatic sources above miss or get wrong for the specific fight you're in - this always
    /// overrides them. Spoken via the {warning} placeholder in <see cref="AnnouncementFormat"/>.
    /// </summary>
    public Dictionary<string, string> AbilityWarnings { get; set; } = new();

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

    // --- Window state ---

    public bool IsConfigWindowMovable { get; set; } = true;

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
