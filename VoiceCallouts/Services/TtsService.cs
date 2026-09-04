using System;
using System.Linq;
using Dalamud.Plugin.Services;

namespace VoiceCallouts.Services;

/// <summary>
/// Thin wrapper around Windows SAPI (System.Speech.Synthesis) for speaking callouts.
///
/// Speech happens via SpeakAsync, which queues work on SAPI's own thread rather than
/// blocking the caller, so this is safe to call from the game's framework thread.
/// Initialization is lazy and Windows-only: if System.Speech can't start (no SAPI voices
/// installed, or running in an environment without a working speech engine), it fails soft,
/// logs once, and every subsequent call becomes a no-op instead of throwing.
/// </summary>
public sealed class TtsService(IPluginLog log, Configuration configuration) : IDisposable
{
    private System.Speech.Synthesis.SpeechSynthesizer? synthesizer;
    private bool initFailed;

    /// <summary>Names of installed, enabled SAPI voices, for populating the settings dropdown.</summary>
    public string[] GetAvailableVoiceNames()
    {
        EnsureInitialized();
        if (synthesizer == null)
            return [];

        try
        {
            return synthesizer.GetInstalledVoices()
                .Where(v => v.Enabled)
                .Select(v => v.VoiceInfo.Name)
                .ToArray();
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Failed to enumerate installed TTS voices");
            return [];
        }
    }

    /// <summary>Speaks the given text, interrupting anything currently being spoken.</summary>
    public void Speak(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        EnsureInitialized();
        if (synthesizer == null)
            return;

        try
        {
            ApplySettings();
            synthesizer.SpeakAsyncCancelAll();
            synthesizer.SpeakAsync(text);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Failed to speak callout text");
        }
    }

    private void ApplySettings()
    {
        if (synthesizer == null)
            return;

        synthesizer.Rate = Math.Clamp(configuration.TtsRate, -10, 10);
        synthesizer.Volume = Math.Clamp(configuration.TtsVolume, 0, 100);

        if (!string.IsNullOrEmpty(configuration.TtsVoiceName))
        {
            try
            {
                synthesizer.SelectVoice(configuration.TtsVoiceName);
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Could not select configured TTS voice '{Voice}', using current default", configuration.TtsVoiceName);
            }
        }
    }

    private void EnsureInitialized()
    {
        if (synthesizer != null || initFailed)
            return;

        try
        {
            synthesizer = new System.Speech.Synthesis.SpeechSynthesizer();
            synthesizer.SetOutputToDefaultAudioDevice();
        }
        catch (Exception ex)
        {
            initFailed = true;
            log.Error(ex, "Failed to initialize text-to-speech (System.Speech). Voice callouts will be silent.");
        }
    }

    public void Dispose()
    {
        synthesizer?.Dispose();
        synthesizer = null;
    }
}
