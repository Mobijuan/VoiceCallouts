using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using VoiceCallouts.Services;
using VoiceCallouts.Windows;

namespace VoiceCallouts;

/// <summary>A single entry in the recent-callouts log shown in the main window.</summary>
public readonly record struct CalloutRecord(DateTime Time, string BossName, string Ability, string Warning);

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IGameConfig GameConfig { get; private set; } = null!;

    private const string CommandName = "/vcallouts";
    private const int MaxRecentCallouts = 50;

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("VoiceCallouts");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    internal BossDetector BossDetector { get; }
    internal CastWatcher CastWatcher { get; }
    internal TtsService TtsService { get; }
    internal CactbotWarnings CactbotWarnings { get; }
    internal AbilityWarningResolver WarningResolver { get; }

    /// <summary>Most-recent-first rolling log of callouts, for display in the main window.</summary>
    internal readonly List<CalloutRecord> RecentCallouts = [];

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        BossDetector = new BossDetector(ClientState, Condition, ObjectTable, Configuration);
        CastWatcher = new CastWatcher(BossDetector, DataManager, Log, Configuration);
        TtsService = new TtsService(Log, Configuration);
        CactbotWarnings = new CactbotWarnings(Log);
        WarningResolver = new AbilityWarningResolver(DataManager, CactbotWarnings, Configuration);

        CastWatcher.AbilityAnnounced += OnAbilityAnnounced;

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Toggles the Voice Callouts status window."
        });

        // Tell the UI system that we want our windows to be drawn through the window system
        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;

        // This adds a button to the plugin installer entry of this plugin which allows
        // toggling the display status of the configuration ui
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;

        // Adds another button doing the same but for the main ui of the plugin
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        Framework.Update += OnFrameworkUpdate;

        Log.Information("VoiceCallouts loaded.");
    }

    public void Dispose()
    {
        Framework.Update -= OnFrameworkUpdate;

        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();
        TtsService.Dispose();

        CastWatcher.AbilityAnnounced -= OnAbilityAnnounced;

        CommandManager.RemoveHandler(CommandName);
    }

    private void OnFrameworkUpdate(IFramework framework) => CastWatcher.Tick();

    private void OnAbilityAnnounced(string abilityName, uint actionId, IBattleNpc npc)
    {
        var bossName = npc.Name.TextValue;
        var warning = WarningResolver.Resolve(abilityName, actionId);
        var text = FormatAnnouncement(Configuration, abilityName, bossName, warning);

        TtsService.Speak(text);

        RecentCallouts.Insert(0, new CalloutRecord(DateTime.Now, bossName, abilityName, warning));
        if (RecentCallouts.Count > MaxRecentCallouts)
            RecentCallouts.RemoveRange(MaxRecentCallouts, RecentCallouts.Count - MaxRecentCallouts);
    }

    private static string FormatAnnouncement(Configuration configuration, string abilityName, string bossName, string warning)
    {
        var parts = new List<string>();

        if (configuration.AnnounceBossName)
            parts.Add(bossName);

        if (configuration.AnnounceAbilityName)
            parts.Add(abilityName);

        if (configuration.AnnounceWarning && !string.IsNullOrEmpty(warning))
            parts.Add(warning);

        return string.Join(' ', parts);
    }

    private void OnCommand(string command, string args) => MainWindow.Toggle();

    public void ToggleConfigUi() => ConfigWindow.Toggle();
    public void ToggleMainUi() => MainWindow.Toggle();
}
