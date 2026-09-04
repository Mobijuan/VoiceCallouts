using Dalamud.Configuration;
using System;

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

    /// <summary>Template for the spoken text. Supports {ability} and {name} placeholders.</summary>
    public string AnnouncementFormat { get; set; } = "{ability}";

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
    /// data sheet has for that ability to the plugin log (visible via /xllog). This is meant
    /// for figuring out which raw field (if any) encodes AoE shape (cone/circle/donut/etc.) -
    /// fight something with a known mechanic, check the log, and see what lines up.
    /// </summary>
    public bool LogActionDiagnostics { get; set; } = false;

    // --- Window state ---

    public bool IsConfigWindowMovable { get; set; } = true;

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
