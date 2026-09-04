using LuminaAction = Lumina.Excel.Sheets.Action;

namespace VoiceCallouts.Services;

/// <summary>The AoE delivery shape of an ability, as best as it can be decoded from game data.</summary>
public enum AbilityShape
{
    /// <summary>Not a directional/shaped AoE, or the shape couldn't be confidently determined.</summary>
    None,
    Cone,
    Circle,
    Line,
    Cross,
}

/// <summary>
/// Decodes an ability's AoE shape from the Action sheet's <c>CastType</c> field, corroborated
/// against the <c>Omen</c> ground-telegraph asset (whose path names frequently spell the shape
/// out directly - e.g. "gl_fan180_1bf" for a 180-degree cone, "general_x02f" for a cross).
///
/// None of this is documented by the game - the CastType -> shape mapping below was reverse
/// engineered by sampling real monster abilities against their known in-game mechanics. Only the
/// values that came back unambiguous across many samples are mapped; everything else (including
/// CastType 12, which looked like it might be a donut but had inconsistent EffectRange/
/// XAxisModifier relationships in samples) is deliberately left as <see cref="AbilityShape.None"/>
/// rather than guessed. This is one of several warning sources combined by
/// <see cref="AbilityWarningResolver"/> - many mechanics never surface a shape here at all
/// because the visible cast-bar action id is a separate "trigger" id from the one that actually
/// carries the telegraph, which this can't see.
/// </summary>
public static class AbilityShapeClassifier
{
    public static AbilityShape Classify(LuminaAction row) => row.CastType switch
    {
        2 or 10 => AbilityShape.Circle,
        13 => AbilityShape.Cone,
        3 => AbilityShape.Line,
        11 => AbilityShape.Cross,
        _ => AbilityShape.None,
    };

    /// <summary>The word to speak/display for this shape, or "" when there's nothing to say.</summary>
    public static string ToCalloutLabel(this AbilityShape shape) => shape switch
    {
        AbilityShape.Cone => "FRONTAL",
        AbilityShape.Circle => "CIRCLE",
        AbilityShape.Line => "LINE",
        AbilityShape.Cross => "CROSS",
        _ => "",
    };
}
