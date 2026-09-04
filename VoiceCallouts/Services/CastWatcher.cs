using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;

namespace VoiceCallouts.Services;

/// <summary>
/// Polls the current set of "boss" battle NPCs (via <see cref="BossDetector"/>) on every
/// framework tick, and raises <see cref="AbilityAnnounced"/> the first time a new cast is
/// observed on each of them, subject to the filters in <see cref="Configuration"/>.
///
/// This is deliberately poll-based (checked once per framework tick from Plugin) rather than
/// hooked into a network/cast event, since IBattleChara already exposes live cast state
/// (IsCasting/CastActionId/TotalCastTime) that's cheap to read every frame and avoids taking
/// on a game-network hook.
/// </summary>
public class CastWatcher(BossDetector bossDetector, IDataManager dataManager, IPluginLog log, Configuration configuration)
{
    // The action id we last announced for each game object, so a single ongoing cast isn't
    // re-announced every tick for as long as it's being cast.
    private readonly Dictionary<ulong, uint> lastAnnouncedActionByObject = new();

    // When each (object, action) pair was last announced, so the same enemy re-using the same
    // ability shortly after doesn't spam TTS.
    private readonly Dictionary<(ulong ObjectId, uint ActionId), DateTime> lastAnnouncedAt = new();

    public event Action<string, uint, IBattleNpc>? AbilityAnnounced;

    /// <summary>Call once per framework update.</summary>
    public void Tick()
    {
        if (!configuration.Enabled)
            return;

        var seenThisTick = new HashSet<ulong>();

        foreach (var npc in bossDetector.GetActiveBosses())
        {
            seenThisTick.Add(npc.GameObjectId);

            if (!npc.IsCasting || npc.CastActionId == 0)
            {
                lastAnnouncedActionByObject.Remove(npc.GameObjectId);
                continue;
            }

            if (lastAnnouncedActionByObject.TryGetValue(npc.GameObjectId, out var lastActionId) &&
                lastActionId == npc.CastActionId)
            {
                // Same cast we already announced - wait for it to end or change.
                continue;
            }

            if (!PassesFilters(npc))
                continue;

            var key = (npc.GameObjectId, npc.CastActionId);
            if (lastAnnouncedAt.TryGetValue(key, out var lastTime) &&
                (DateTime.UtcNow - lastTime).TotalSeconds < configuration.RepeatSuppressionSeconds)
            {
                // Too soon to repeat - remember we "saw" it so we don't keep re-checking every tick.
                lastAnnouncedActionByObject[npc.GameObjectId] = npc.CastActionId;
                continue;
            }

            var abilityName = ResolveActionName(npc.CastActionId);
            if (string.IsNullOrWhiteSpace(abilityName))
                continue;

            lastAnnouncedActionByObject[npc.GameObjectId] = npc.CastActionId;
            lastAnnouncedAt[key] = DateTime.UtcNow;

            LogActionDiagnostics(npc.CastActionId, abilityName);
            AbilityAnnounced?.Invoke(abilityName, npc.CastActionId, npc);
        }

        // Forget bookkeeping for NPCs that are no longer considered active bosses, so
        // dictionaries don't grow unbounded over a long play session.
        if (lastAnnouncedActionByObject.Count > 0)
        {
            foreach (var id in new List<ulong>(lastAnnouncedActionByObject.Keys))
            {
                if (!seenThisTick.Contains(id))
                    lastAnnouncedActionByObject.Remove(id);
            }
        }
    }

    private bool PassesFilters(IBattleNpc npc) => npc.TotalCastTime >= configuration.MinimumCastTimeSeconds;

    /// <summary>
    /// Diagnostic aid: dumps every field the Action sheet row has for this ability to the
    /// plugin log, via reflection so it works regardless of exactly which properties your
    /// installed Lumina schema exposes. Enable via Configuration.LogActionDiagnostics to confirm
    /// an ability's exact name/id (e.g. when building an entry for Configuration.AbilityWarnings)
    /// or to poke at its raw game data for other reasons.
    /// </summary>
    private void LogActionDiagnostics(uint actionId, string abilityName)
    {
        if (!configuration.LogActionDiagnostics)
            return;

        try
        {
            var sheet = dataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>();
            if (sheet == null || !sheet.TryGetRow(actionId, out var row))
                return;

            var lines = new List<string> { $"[VoiceCallouts] Action fields for '{abilityName}' (id {actionId}):" };

            foreach (var prop in row.GetType().GetProperties())
            {
                string valueText;
                try
                {
                    valueText = prop.GetValue(row)?.ToString() ?? "null";
                }
                catch (Exception ex)
                {
                    valueText = $"<error reading: {ex.Message}>";
                }

                lines.Add($"  {prop.Name} = {valueText}");
            }

            log.Information(string.Join("\n", lines));
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Failed to log action diagnostics for action id {ActionId}", actionId);
        }
    }

    private string? ResolveActionName(uint actionId)
    {
        try
        {
            // Matches the GetExcelSheet<T>().TryGetRow(id, out row) pattern Dalamud's own
            // SamplePlugin template uses for TerritoryType lookups.
            var sheet = dataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>();
            if (sheet == null || !sheet.TryGetRow(actionId, out var row))
                return null;

            var name = row.Name.ToString();
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Failed to resolve action name for action id {ActionId}", actionId);
            return null;
        }
    }
}
