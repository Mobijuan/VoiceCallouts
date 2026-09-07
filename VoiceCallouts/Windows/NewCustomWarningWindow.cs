using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace VoiceCallouts.Windows;

/// <summary>
/// Small dialog, centered on screen, opened by "/vcallouts newcustom" for quickly adding a
/// warning for whatever ability was most recently heard - without needing to open the full
/// Custom Warnings window and type the creature/ability names by hand.
/// </summary>
public class NewCustomWarningWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    private string zone = "";
    private string creature = "";
    private string ability = "";
    private string warningText = "";
    private bool focusPending;

    public NewCustomWarningWindow(Plugin plugin) : base("New Custom Warning###VoiceCalloutsNewCustomWarning")
    {
        Flags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize;
        this.plugin = plugin;
    }

    public void Dispose() { }

    /// <summary>
    /// Pre-fills from the most recently heard callout, centers, and opens the window. Returns
    /// false (without opening anything) if no ability has been heard yet this session.
    /// </summary>
    public bool TryOpenForMostRecentCallout()
    {
        if (plugin.RecentCallouts.Count == 0)
            return false;

        var record = plugin.RecentCallouts[0];
        zone = record.Zone;
        creature = record.BossName;
        ability = record.Ability;
        warningText = plugin.Configuration.FindAbilityWarning(creature, ability)?.Warning ?? "";

        IsOpen = true;
        RequestFocus = true;
        BringToFront();
        focusPending = true;
        return true;
    }

    public override void PreDraw()
    {
        var viewport = ImGui.GetMainViewport();
        var size = new Vector2(420, 130);

        Size = size;
        SizeCondition = ImGuiCond.Appearing;
        Position = viewport.Pos + ((viewport.Size - size) / 2f);
        PositionCondition = ImGuiCond.Appearing;
    }

    public override void Draw()
    {
        ImGui.TextUnformatted($"Creature: {creature}");
        ImGui.TextUnformatted($"Ability: {ability}");
        ImGui.Spacing();

        if (focusPending)
        {
            ImGui.SetKeyboardFocusHere();
            focusPending = false;
        }

        ImGui.SetNextItemWidth(-1);
        var submitted = ImGui.InputText("##NewCustomWarningText", ref warningText, 64, ImGuiInputTextFlags.EnterReturnsTrue);

        ImGui.TextDisabled("Press Enter to save.");

        if (submitted && !string.IsNullOrWhiteSpace(warningText))
        {
            plugin.Configuration.AddOrUpdateAbilityWarning(zone, creature, ability, warningText.Trim());
            plugin.Configuration.Save();
            IsOpen = false;
        }
    }
}
