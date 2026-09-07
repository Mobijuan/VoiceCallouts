using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace VoiceCallouts.Windows;

/// <summary>Sortable, filterable table of every manual ability warning, with inline editing/removal.</summary>
public class AbilityWarningsWindow : Window, IDisposable
{
    private const string EditPopupId = "EditAbilityWarning";
    private const string PickAbilityPopupId = "PickKnownAbility";
    private const string ImportConfirmPopupId = "ImportWarningsConfirm";

    private readonly Plugin plugin;

    private int editingId = -1;
    private string editingWarningText = "";

    private string newZone = "";
    private string newCreature = "";
    private string newAbility = "";
    private string newWarning = "";

    private string filterId = "";
    private string filterZone = "";
    private string filterCreature = "";
    private string filterAbility = "";
    private string filterWarning = "";

    private string statusMessage = "";
    private List<AbilityWarningEntry>? pendingImport;
    private int pendingOverwriteCount;

    /// <summary>
    /// Pre-fills the "Add new" row and opens the window - used by the right-click "Add Voice
    /// Callouts Warning" context menu item. This is the exact same window instance as the
    /// "Custom Warnings" buttons open (Plugin owns a single instance, registered once in the
    /// WindowSystem) - if it's already open behind another window, just setting IsOpen wouldn't
    /// visibly do anything, so this also brings it to front and focuses it.
    /// </summary>
    public void PrefillNewEntry(string zone, string creature)
    {
        newZone = zone;
        newCreature = creature;
        newAbility = "";
        newWarning = "";
        IsOpen = true;
        RequestFocus = true;
        BringToFront();
    }

    public AbilityWarningsWindow(Plugin plugin) : base("Voice Callouts - Custom Warnings###VoiceCalloutsWarningsWindow")
    {
        Size = new Vector2(760, 460);
        SizeCondition = ImGuiCond.FirstUseEver;

        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        var entries = plugin.Configuration.AbilityWarnings;

        DrawAddNewSection();

        ImGui.Spacing();
        ImGui.Separator();

        if (ImGui.Button("Export to Clipboard"))
            ExportToClipboard();

        ImGui.SameLine();
        if (ImGui.Button("Import from Clipboard"))
            BeginImportFromClipboard();

        DrawImportConfirmPopup();

        if (!string.IsNullOrEmpty(statusMessage))
        {
            ImGui.SameLine();
            ImGui.TextDisabled(statusMessage);
        }

        ImGui.Spacing();

        if (entries.Count == 0)
        {
            ImGui.TextDisabled("No manual warnings yet. Add one above, or click a recent callout on the main window.");
            return;
        }

        const ImGuiTableFlags flags = ImGuiTableFlags.Sortable | ImGuiTableFlags.RowBg |
                                       ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable |
                                       ImGuiTableFlags.ScrollY;

        if (!ImGui.BeginTable("AbilityWarningsTable", 6, flags, new Vector2(0, 0), 0f))
            return;

        ImGui.TableSetupColumn("ID", ImGuiTableColumnFlags.WidthFixed, 40f, 0);
        ImGui.TableSetupColumn("Zone", ImGuiTableColumnFlags.WidthFixed, 90f, 0);
        ImGui.TableSetupColumn("Creature", ImGuiTableColumnFlags.WidthStretch, 0f, 0);
        ImGui.TableSetupColumn("Ability", ImGuiTableColumnFlags.WidthStretch, 0f, 0);
        ImGui.TableSetupColumn("Warning", ImGuiTableColumnFlags.WidthStretch, 0f, 0);
        ImGui.TableSetupColumn("##Actions", ImGuiTableColumnFlags.NoSort | ImGuiTableColumnFlags.WidthFixed, 130f, 0);
        ImGui.TableHeadersRow();

        DrawFilterRow();

        ApplySort(entries);

        var visibleEntries = entries.Where(MatchesFilters).ToList();
        AbilityWarningEntry? toRemove = null;

        foreach (var entry in visibleEntries)
        {
            ImGui.PushID(entry.Id);
            ImGui.TableNextRow();

            ImGui.TableSetColumnIndex(0);
            ImGui.TextUnformatted(entry.Id.ToString());

            ImGui.TableSetColumnIndex(1);
            ImGui.TextUnformatted(entry.Zone);

            ImGui.TableSetColumnIndex(2);
            ImGui.TextUnformatted(entry.CreatureName);

            ImGui.TableSetColumnIndex(3);
            ImGui.TextUnformatted(entry.AbilityName);

            ImGui.TableSetColumnIndex(4);
            ImGui.TextUnformatted(entry.Warning);

            ImGui.TableSetColumnIndex(5);
            if (ImGui.SmallButton("Edit"))
            {
                editingId = entry.Id;
                editingWarningText = entry.Warning;
                ImGui.OpenPopup(EditPopupId);
            }

            ImGui.SameLine();
            if (ImGui.SmallButton("Remove"))
                toRemove = entry;

            ImGui.PopID();
        }

        if (toRemove != null)
        {
            entries.Remove(toRemove);
            plugin.Configuration.Save();
        }

        DrawEditPopup(entries);

        ImGui.EndTable();

        if (visibleEntries.Count != entries.Count)
            ImGui.TextDisabled($"Showing {visibleEntries.Count} of {entries.Count} - filtered (Export only copies what's shown).");
    }

    private void DrawAddNewSection()
    {
        ImGui.TextUnformatted("Add new");

        if (string.IsNullOrEmpty(newZone))
            newZone = plugin.BossDetector.CurrentZoneName;

        ImGui.SetNextItemWidth(130);
        ImGui.InputTextWithHint("##NewZone", "Zone", ref newZone, 128);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Defaults to your current zone - edit freely if you're adding this for somewhere else.");

        ImGui.SameLine();
        ImGui.SetNextItemWidth(150);
        ImGui.InputTextWithHint("##NewCreature", "Creature", ref newCreature, 128);

        ImGui.SameLine();
        ImGui.SetNextItemWidth(150);
        ImGui.InputTextWithHint("##NewAbility", "Ability", ref newAbility, 128);

        ImGui.SameLine();
        var knownAbilities = GetKnownAbilities(newCreature);
        ImGui.BeginDisabled(knownAbilities.Count == 0);
        if (ImGui.SmallButton("Pick..."))
            ImGui.OpenPopup(PickAbilityPopupId);
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(knownAbilities.Count == 0
                ? "No abilities heard yet for this creature - type one in manually, or wait until you've heard it cast."
                : "Pick from abilities you've already heard this creature cast.");

        if (ImGui.BeginPopup(PickAbilityPopupId))
        {
            foreach (var ability in knownAbilities)
            {
                if (ImGui.Selectable(ability))
                {
                    newAbility = ability;
                    ImGui.CloseCurrentPopup();
                }
            }

            ImGui.EndPopup();
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(120);
        ImGui.InputTextWithHint("##NewWarning", "Warning", ref newWarning, 64);

        ImGui.SameLine();
        var canAdd = !string.IsNullOrWhiteSpace(newCreature) && !string.IsNullOrWhiteSpace(newAbility) && !string.IsNullOrWhiteSpace(newWarning);
        ImGui.BeginDisabled(!canAdd);
        if (ImGui.Button("Add"))
        {
            plugin.Configuration.AddOrUpdateAbilityWarning(newZone, newCreature.Trim(), newAbility.Trim(), newWarning.Trim());
            plugin.Configuration.Save();
            newCreature = "";
            newAbility = "";
            newWarning = "";
        }
        ImGui.EndDisabled();
    }

    private void DrawFilterRow()
    {
        ImGui.TableNextRow();

        ImGui.TableSetColumnIndex(0);
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##FilterId", "id", ref filterId, 16);

        ImGui.TableSetColumnIndex(1);
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##FilterZone", "zone", ref filterZone, 128);

        ImGui.TableSetColumnIndex(2);
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##FilterCreature", "creature", ref filterCreature, 128);

        ImGui.TableSetColumnIndex(3);
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##FilterAbility", "ability", ref filterAbility, 128);

        ImGui.TableSetColumnIndex(4);
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##FilterWarning", "warning", ref filterWarning, 64);

        ImGui.TableSetColumnIndex(5);
        if (ImGui.SmallButton("Clear filter"))
        {
            filterId = "";
            filterZone = "";
            filterCreature = "";
            filterAbility = "";
            filterWarning = "";
        }
    }

    private bool MatchesFilters(AbilityWarningEntry entry) =>
        Matches(entry.Id.ToString(), filterId) &&
        Matches(entry.Zone, filterZone) &&
        Matches(entry.CreatureName, filterCreature) &&
        Matches(entry.AbilityName, filterAbility) &&
        Matches(entry.Warning, filterWarning);

    private static bool Matches(string value, string filter) =>
        string.IsNullOrEmpty(filter) || value.Contains(filter, StringComparison.OrdinalIgnoreCase);

    private void DrawEditPopup(List<AbilityWarningEntry> entries)
    {
        if (!ImGui.BeginPopup(EditPopupId))
            return;

        var entry = entries.Find(e => e.Id == editingId);
        if (entry == null)
        {
            ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
            return;
        }

        ImGui.TextUnformatted($"{entry.CreatureName}: {entry.AbilityName}");
        ImGui.Spacing();
        ImGui.SetNextItemWidth(220);
        ImGui.InputText("##EditWarningText", ref editingWarningText, 64);

        if (ImGui.Button("Save"))
        {
            entry.Warning = editingWarningText;
            plugin.Configuration.Save();
            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();
        if (ImGui.Button("Cancel"))
            ImGui.CloseCurrentPopup();

        ImGui.EndPopup();
    }

    /// <summary>Distinct ability names seen for this creature, from recent callouts and existing warnings.</summary>
    private List<string> GetKnownAbilities(string creatureName)
    {
        if (string.IsNullOrWhiteSpace(creatureName))
            return [];

        var abilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var record in plugin.RecentCallouts)
        {
            if (string.Equals(record.BossName, creatureName, StringComparison.OrdinalIgnoreCase))
                abilities.Add(record.Ability);
        }

        foreach (var entry in plugin.Configuration.AbilityWarnings)
        {
            if (string.Equals(entry.CreatureName, creatureName, StringComparison.OrdinalIgnoreCase))
                abilities.Add(entry.AbilityName);
        }

        var list = abilities.ToList();
        list.Sort(StringComparer.OrdinalIgnoreCase);
        return list;
    }

    private void ExportToClipboard()
    {
        try
        {
            var visible = plugin.Configuration.AbilityWarnings.Where(MatchesFilters).ToList();
            var json = JsonSerializer.Serialize(visible, new JsonSerializerOptions { WriteIndented = true });
            ImGui.SetClipboardText(json);
            statusMessage = $"Copied {visible.Count} entries to clipboard.";
        }
        catch (Exception ex)
        {
            statusMessage = $"Export failed: {ex.Message}";
        }
    }

    private void BeginImportFromClipboard()
    {
        string clipboard;
        try
        {
            clipboard = ImGui.GetClipboardText();
        }
        catch (Exception ex)
        {
            statusMessage = $"Couldn't read the clipboard: {ex.Message}";
            return;
        }

        List<AbilityWarningEntry>? imported;
        try
        {
            imported = JsonSerializer.Deserialize<List<AbilityWarningEntry>>(clipboard);
        }
        catch (Exception ex)
        {
            statusMessage = $"Clipboard doesn't contain valid warning data: {ex.Message}";
            return;
        }

        imported = imported?
            .Where(e => !string.IsNullOrWhiteSpace(e.CreatureName) && !string.IsNullOrWhiteSpace(e.AbilityName) && !string.IsNullOrWhiteSpace(e.Warning))
            .ToList();

        if (imported == null || imported.Count == 0)
        {
            statusMessage = "Clipboard doesn't contain any usable warnings.";
            return;
        }

        pendingImport = imported;
        pendingOverwriteCount = imported.Count(e => plugin.Configuration.FindAbilityWarning(e.CreatureName, e.AbilityName) != null);

        if (pendingOverwriteCount > 0)
            ImGui.OpenPopup(ImportConfirmPopupId);
        else
            ApplyPendingImport();
    }

    private void ApplyPendingImport()
    {
        if (pendingImport == null)
            return;

        foreach (var entry in pendingImport)
            plugin.Configuration.AddOrUpdateAbilityWarning(entry.Zone, entry.CreatureName, entry.AbilityName, entry.Warning);

        plugin.Configuration.Save();
        statusMessage = $"Imported {pendingImport.Count} entries.";
        pendingImport = null;
    }

    private void DrawImportConfirmPopup()
    {
        if (!ImGui.BeginPopup(ImportConfirmPopupId))
            return;

        ImGui.TextUnformatted($"This will overwrite {pendingOverwriteCount} existing warning(s) sharing the same creature + ability. Continue?");

        if (ImGui.Button("Import"))
        {
            ApplyPendingImport();
            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();
        if (ImGui.Button("Cancel"))
        {
            pendingImport = null;
            ImGui.CloseCurrentPopup();
        }

        ImGui.EndPopup();
    }

    private static void ApplySort(List<AbilityWarningEntry> entries)
    {
        var sortSpecs = ImGui.TableGetSortSpecs();
        if (sortSpecs.IsNull || sortSpecs.SpecsCount == 0 || !sortSpecs.SpecsDirty)
            return;

        var spec = sortSpecs.Specs[0];
        var ascending = spec.SortDirection != ImGuiSortDirection.Descending;

        Comparison<AbilityWarningEntry> comparison = spec.ColumnIndex switch
        {
            0 => (a, b) => a.Id.CompareTo(b.Id),
            1 => (a, b) => string.Compare(a.Zone, b.Zone, StringComparison.OrdinalIgnoreCase),
            2 => (a, b) => string.Compare(a.CreatureName, b.CreatureName, StringComparison.OrdinalIgnoreCase),
            3 => (a, b) => string.Compare(a.AbilityName, b.AbilityName, StringComparison.OrdinalIgnoreCase),
            4 => (a, b) => string.Compare(a.Warning, b.Warning, StringComparison.OrdinalIgnoreCase),
            _ => (a, b) => 0,
        };

        entries.Sort((a, b) => ascending ? comparison(a, b) : comparison(b, a));
        sortSpecs.SpecsDirty = false;
    }
}
