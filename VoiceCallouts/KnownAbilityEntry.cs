namespace VoiceCallouts;

/// <summary>
/// One distinct (zone, creature, ability) combination that's actually been heard, accumulated
/// across sessions in <see cref="Configuration.KnownAbilities"/>. Purely a reference cache for
/// populating the Zone/Creature/Ability pickers in the Custom Warnings window - unlike
/// <see cref="AbilityWarningEntry"/>, this carries no warning text and isn't spoken.
/// </summary>
public class KnownAbilityEntry
{
    public string Zone { get; set; } = "";
    public string CreatureName { get; set; } = "";
    public string AbilityName { get; set; } = "";
}
