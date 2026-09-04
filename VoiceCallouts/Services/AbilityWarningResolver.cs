using Dalamud.Plugin.Services;

namespace VoiceCallouts.Services;

/// <summary>
/// Resolves the {warning} text for an announced ability by checking, in order, whichever of
/// these sources are enabled in <see cref="Configuration"/>:
///
/// 1. Manual (<see cref="Configuration.AbilityWarnings"/>) - your own entries, matched by
///    creature + ability name together (FFXIV reuses generic ability names like "Meteor" across
///    unrelated bosses, so ability name alone isn't a safe key). Always wins when present, since
///    it's an explicit correction/preference.
/// 2. Cactbot (<see cref="CactbotWarnings"/>) - a bundled, offline-extracted snapshot of
///    cactbot's community-maintained fight data, matched by ability id.
/// 3. Lumina (<see cref="AbilityShapeClassifier"/>) - a live, on-the-fly guess from the game's
///    own Action sheet data, matched by ability id. Weakest signal (thin coverage, see that
///    class's docs) so it's checked last.
///
/// Returns "" when nothing matched or the relevant source(s) are disabled.
/// </summary>
public sealed class AbilityWarningResolver(IDataManager dataManager, CactbotWarnings cactbotWarnings, Configuration configuration)
{
    public string Resolve(string abilityName, string creatureName, uint actionId)
    {
        if (!configuration.WarningsEnabled)
            return "";

        if (configuration.UseManualWarnings)
        {
            var manual = configuration.FindAbilityWarning(creatureName, abilityName);
            if (manual != null)
                return manual.Warning;
        }

        if (configuration.UseCactbotWarnings && cactbotWarnings.TryGet(actionId, out var cactbotEntry))
            return cactbotEntry.Text;

        if (configuration.UseLuminaShapeWarnings)
        {
            var sheet = dataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>();
            if (sheet != null && sheet.TryGetRow(actionId, out var row))
            {
                var shape = AbilityShapeClassifier.Classify(row);
                if (shape != AbilityShape.None)
                    return shape.ToCalloutLabel();
            }
        }

        return "";
    }
}
