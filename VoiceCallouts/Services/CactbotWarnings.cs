using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dalamud.Plugin.Services;

namespace VoiceCallouts.Services;

/// <summary>
/// A single (ability id -> player-facing warning text) fact, as extracted from one of
/// cactbot's per-encounter trigger definitions. See tools/cactbot-sync/sync.py for how these
/// are produced and <see cref="CactbotWarnings"/> for how they're loaded.
/// </summary>
public sealed record CactbotWarningEntry(
    [property: JsonPropertyName("actionId")] uint ActionId,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("fight")] string? Fight,
    [property: JsonPropertyName("trigger")] string? Trigger,
    [property: JsonPropertyName("file")] string? File);

/// <summary>
/// Loads a bundled, offline-extracted snapshot of cactbot's (https://github.com/OverlayPlugin/cactbot)
/// per-fight trigger data, keyed by ability id. Cactbot's own trigger files are TypeScript with
/// a lot of conditional/computed logic (directional callouts, role-dependent text, etc.) that
/// can't be resolved without actually running the fight, so the snapshot only includes triggers
/// whose displayed text is a single static string not dependent on any of that - see
/// tools/cactbot-sync/sync.py for the exact extraction rules. This means coverage is real but
/// partial: most "this specific direction/role does X" mechanics are intentionally left out
/// rather than guessed at.
///
/// The snapshot is a point-in-time export (see its generatedAt field) - re-run
/// tools/cactbot-sync/sync.py and rebuild to pick up cactbot's latest fights/patches.
/// </summary>
public sealed class CactbotWarnings
{
    private const string ResourceName = "VoiceCallouts.Data.cactbot-warnings.json";

    private readonly Dictionary<uint, CactbotWarningEntry> byActionId = new();

    public int Count => byActionId.Count;

    public CactbotWarnings(IPluginLog log)
    {
        try
        {
            using var stream = typeof(CactbotWarnings).Assembly.GetManifestResourceStream(ResourceName);
            if (stream == null)
            {
                log.Warning("Cactbot warnings data ({ResourceName}) not found in assembly - that source will be empty.", ResourceName);
                return;
            }

            var entries = JsonSerializer.Deserialize<List<CactbotWarningEntry>>(stream);
            if (entries == null)
                return;

            foreach (var entry in entries)
                byActionId[entry.ActionId] = entry;

            log.Information("Loaded {Count} cactbot-derived ability warnings.", byActionId.Count);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Failed to load cactbot warnings data - that source will be empty.");
        }
    }

    public bool TryGet(uint actionId, out CactbotWarningEntry entry) =>
        byActionId.TryGetValue(actionId, out entry!);
}
