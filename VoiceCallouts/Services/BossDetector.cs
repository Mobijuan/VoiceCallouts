using System.Collections.Generic;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;

namespace VoiceCallouts.Services;

/// <summary>
/// Works out whether the local player is currently in a fight worth announcing casts for -
/// either an instanced duty or open-world combat - and which nearby battle NPCs to watch.
///
/// There's no clean "this is a boss" flag exposed by Dalamud's object API on this SDK version
/// (no nameplate icon/marker property is available on IBattleNpc), so this doesn't try to
/// distinguish "boss" from "trash": it announces casts from any hostile combatant NPC you're
/// fighting, subject to the separate duty/open-world toggles in <see cref="Configuration"/>.
/// Inside duties this is exactly what was wanted (the enemy roster there is small and known
/// already). In the open world it means any nearby fight you're in gets callouts, not just
/// notable/boss targets - if that proves too noisy in practice, the natural next step would
/// be filtering by NPC rank or level, which would need its own round of verifying the right
/// Dalamud properties against your installed SDK version (the same way BattleNpcSubKind and
/// this file's original icon lookup needed verifying).
/// </summary>
public class BossDetector(IClientState clientState, ICondition condition, IObjectTable objectTable, Configuration configuration)
{
    /// <summary>True while bound to an instanced duty (dungeon, trial, raid, alliance raid, etc.).</summary>
    public bool IsInDuty =>
        condition[ConditionFlag.BoundByDuty] ||
        condition[ConditionFlag.BoundByDuty56] ||
        condition[ConditionFlag.BoundByDuty95];

    /// <summary>True while the local player is in combat.</summary>
    public bool InCombat => condition[ConditionFlag.InCombat];

    /// <summary>True when the plugin should currently be listening for casts at all.</summary>
    public bool IsEncounterActive =>
        clientState.IsLoggedIn &&
        InCombat &&
        ((IsInDuty && configuration.AnnounceInDuties) ||
         (!IsInDuty && configuration.AnnounceInOpenWorld));

    /// <summary>Enumerates the hostile combatant NPCs to watch for casts.</summary>
    public IEnumerable<IBattleNpc> GetActiveBosses()
    {
        if (!IsEncounterActive)
            yield break;

        foreach (var obj in objectTable)
        {
            if (obj is not IBattleNpc npc)
                continue;

            if (npc.SubKind != (byte)BattleNpcSubKind.Combatant)
                continue;

            yield return npc;
        }
    }
}
