# Voice Callouts

A [Dalamud](https://dalamud.dev) plugin for FINAL FANTASY XIV that reads boss ability names
aloud via text-to-speech, so you can keep your eyes on the fight instead of the cast bar.

## How it works

While you're in combat - either inside an instanced duty (dungeon, trial, raid, alliance
raid) or against a marked open-world boss (FATE bosses, S-ranks, etc.) - Voice Callouts
watches nearby hostile enemies for casts and speaks the ability name out loud as soon as it
starts casting.

There's no maintained database of fights or abilities: it works generically off whatever the
game reports an enemy is currently casting, so it works out of the box in new content without
needing per-fight data entry. You control what gets announced through the settings window:

* Which contexts to listen in (instanced duties, open world, or both).
* Whether to only announce abilities with a visible cast bar, or every ability including
  instants.
* A minimum cast time, so short/spammy casts can be filtered out.
* How long to wait before the same enemy's same ability can be announced again, to avoid
  repeat spam on channeled or frequently-reused abilities.
* The TTS voice, rate, and volume (uses the Windows voices already installed on your system).
* The spoken text format, via `{ability}` and `{name}` placeholders.

There's no first-class "boss" flag exposed by the game/Dalamud, so this doesn't try to tell
bosses apart from regular enemies: it announces casts from any hostile enemy you're actively
fighting, filtered by the settings above. Inside duties that's exactly the enemy roster you'd
want callouts for. In the open world it means any fight you're in gets callouts, not just
notable/marked targets - see the comments in `VoiceCallouts/Services/BossDetector.cs` for the
reasoning and what a future rank/level-based filter would need.

## Commands

* `/vcallouts` - toggles the status window, which shows whether it's currently listening and
  a log of recent callouts.

## Prerequisites

* XIVLauncher, FINAL FANTASY XIV, and Dalamud installed, with the game run at least once with
  Dalamud enabled.
* .NET 10 SDK (in most cases your IDE will handle this for you).

## Building

1. Open `VoiceCallouts.slnx` in Visual Studio or JetBrains Rider.
2. Build the solution (`Debug` or `Release`).
3. The built plugin is at `VoiceCallouts/bin/x64/Debug/VoiceCallouts.dll` (or `Release`).

## Loading as a dev plugin

1. In-game, run `/xlsettings`, go to `Experimental`, and add the full path to
   `VoiceCallouts.dll` under Dev Plugin Locations.
2. Run `/xlplugins`, go to `Dev Tools > Installed Dev Plugins`, and enable `Voice Callouts`.
3. Use `/vcallouts` to open the status window.

## Status

Early work in progress. The core loop (detect boss encounter → watch casts → speak the
ability name) is implemented; the open-world boss detection heuristic in particular hasn't
been validated against real fights yet and may need tuning.
