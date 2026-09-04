using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace VoiceCallouts.Windows;

/// <summary>Sortable table of every manual ability warning, with inline editing/removal.</summary>
public class AbilityWarningsWindow : Window, IDisposable
{
    private const string EditPopupId = "EditAbilityWarning";

    private readonly Plugin plugin;

    private int editingId = -1;
    private string editingWarningText = "";

    private string newZone = "";
    private string newCreature = "";
    private string newAbility = "";
    private string newWarning = "";

    public AbilityWarningsWindow(Plugin plugin) : base("Voice Callouts - Ability Warnings###VoiceCalloutsWarningsWindow")
    {
        Size = new Vector2(760, 440);
        SizeCondition = ImGuiCond.FirstUseEver;

        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        var entries = plugin.Configuration.AbilityWarnings;

        if (entries.Count == 0)
        {
            ImGui.TextDisabled("No manual warnings yet. Add one below, or click a recent callout on the main window.");
        }
        else
        {
            const ImGuiTableFlags flags = ImGuiTableFlags.Sortable | ImGuiTableFlags.RowBg |
                                           ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable |
                                           ImGuiTableFlags.ScrollY;

            if (ImGui.BeginTable("AbilityWarningsTable", 6, flags, new Vector2(0, -32), 0f))
            {
                ImGui.TableSetupColumn("ID", ImGuiTableColumnFlags.WidthFixed, 40f, 0);
                ImGui.TableSetupColumn("Zone", ImGuiTableColumnFlags.WidthFixed, 90f, 0);
                ImGui.TableSetupColumn("Creature", ImGuiTableColumnFlags.WidthStretch, 0f, 0);
                ImGui.TableSetupColumn("Ability", ImGuiTableColumnFlags.WidthStretch, 0f, 0);
                ImGui.TableSetupColumn("Warning", ImGuiTableColumnFlags.WidthStretch, 0f, 0);
                ImGui.TableSetupColumn("##Actions", ImGuiTableColumnFlags.NoSort | ImGuiTableColumnFlags.WidthFixed, 130f, 0);
                ImGui.TableHeadersRow();

                ApplySort(entries);

                for (var i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
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
                    var removeThis = ImGui.SmallButton("Remove");

                    ImGui.PopID();

                    if (removeThis)
                    {
                        entries.RemoveAt(i);
                        plugin.Configuration.Save();
                        i--;
                    }
                }

                DrawEditPopup(entries);

                ImGui.EndTable();
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
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
