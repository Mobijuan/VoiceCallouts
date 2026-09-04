namespace VoiceCallouts;

/// <summary>
/// One manual mechanic note. Matched by <see cref="CreatureName"/> + <see cref="AbilityName"/>
/// together (both case-insensitive), rather than by ability name alone, since FFXIV frequently
/// reuses generic ability names (e.g. "Meteor", "Holy") across many unrelated bosses with
/// completely different mechanics.
/// </summary>
public class AbilityWarningEntry
{
    /// <summary>Stable, monotonically increasing id - see <see cref="Configuration.NextAbilityWarningId"/>.</summary>
    public int Id { get; set; }

    /// <summary>"Duty" or "Open World", captured automatically from where the cast was heard.</summary>
    public string Zone { get; set; } = "";

    public string CreatureName { get; set; } = "";

    public string AbilityName { get; set; } = "";

    public string Warning { get; set; } = "";
}
