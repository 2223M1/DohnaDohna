using System.Diagnostics;
using System.Text.Json;
using Godot;
using HarmonyLib;
using DohnaDohna.Content;
using DohnaDohna.Code.Visuals;
using STS2RitsuLib.Audio;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Audio.Debug;

namespace DohnaDohna.SmokeDriver;

internal static class AudioProbe
{
    private static readonly Dictionary<string, string> VoiceRoles = [];
    private static readonly List<object> Events = [];
    internal static readonly List<IAudioHandle> RoomVoiceHandles = [];
    private static string? _directory;
    internal static string? NativeWitnessGate;
    internal static void Mark(string reference)
    {
        if (_directory != null) Events.Add(new { reference, kind = "marker", seconds = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency });
    }

    internal static void Initialize()
    {
        if (System.Environment.GetEnvironmentVariable("DOHNA_AUDIO_TRACE") != "1") return;
        _directory = System.Environment.GetEnvironmentVariable("DOHNA_CAPTURE_DIR")
            ?? throw new InvalidOperationException("Audio fixture requires process-only capture.");
        foreach (var role in RoleDefinition.All)
        {
            using var data = JsonDocument.Parse(Godot.FileAccess.GetFileAsString(role.AssetRoot + "/motions.json"));
            foreach (var category in new[] { "hurtVoice", "deathVoice" })
                foreach (var voice in data.RootElement.GetProperty(category).EnumerateArray()) VoiceRoles[voice.GetString()!] = role.Id;
            foreach (var motion in data.RootElement.GetProperty("motions").EnumerateObject())
                foreach (var frame in motion.Value.GetProperty("frames").EnumerateArray())
                    if (frame.TryGetProperty("Voice", out var voice) && voice.GetProperty("Enable").GetInt32() == 1)
                        for (int i = 1; i <= 3; i++)
                            if (voice.TryGetProperty("Voice" + i, out var name) && !string.IsNullOrEmpty(name.GetString())) VoiceRoles[name.GetString()!] = role.Id;
        }
        new Harmony("DohnaDohna.SmokeDriver.AudioTrace").Patch(AccessTools.Method(typeof(SquadAudio), nameof(SquadAudio.Play)),
            postfix: new HarmonyMethod(typeof(AudioProbe), nameof(Trace)));
        new Harmony("DohnaDohna.SmokeDriver.RoomAudioTrace").Patch(AccessTools.Method(typeof(SquadAudio), "PlayInRoom"),
            postfix: new HarmonyMethod(typeof(AudioProbe), nameof(Trace)));
        new Harmony("DohnaDohna.SmokeDriver.NativeAudioTrace").Patch(
            AccessTools.Method(typeof(NAudioManager), nameof(NAudioManager.PlayOneShot),
                [typeof(string), typeof(Dictionary<string, float>), typeof(float)]),
            prefix: new HarmonyMethod(typeof(AudioProbe), nameof(TraceNative)));
        new Harmony("DohnaDohna.SmokeDriver.TmpAudioTrace").Patch(
            AccessTools.Method(typeof(NDebugAudioManager), nameof(NDebugAudioManager.Play)),
            prefix: new HarmonyMethod(typeof(AudioProbe), nameof(TraceTemporary)));
    }

    private static void TraceTemporary(string streamName, float volume) => Events.Add(new {
        reference = streamName, kind = "native-tmp", seconds = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency, volume });

    private static bool TraceNative(string path, float volume)
    {
        bool allowed = NativeWitnessGate == null || NativeWitnessGate == path;
        Events.Add(new { reference = path, kind = "native-sfx", seconds = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency,
            volume, allowed });
        return allowed;
    }

    private static void Trace(string reference, IAudioHandle __result)
    {
        if (__result.Scope == AudioLifecycleScope.Room) RoomVoiceHandles.Add(__result);
        var raw = __result.RawInstance;
        double? volume = raw?.HasMethod("get_volume") == true ? raw.Call("get_volume").AsDouble() : null;
        if (volume.HasValue && Math.Abs(volume.Value - Math.Pow(10, SquadAudio.GainDb / 20)) > 0.00001)
            throw new Exception("FMOD event did not receive the uniform gain: " + volume);
        Events.Add(new
        {
            reference, role = VoiceRoles.GetValueOrDefault(reference),
            kind = VoiceRoles.ContainsKey(reference) ? "voice" : "sfx",
            seconds = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency,
            valid = __result.IsValid, nativeType = raw?.GetClass().ToString(), nativeVolume = volume, gainDb = SquadAudio.GainDb
        });
    }

    internal static void Save()
    {
        if (_directory == null) return;
        System.IO.File.WriteAllText(System.IO.Path.Combine(_directory, "events.json"),
            JsonSerializer.Serialize(Events, new JsonSerializerOptions { WriteIndented = true }));
        GD.Print("DOHNA_AUDIO_EVENT_TRACE count=" + Events.Count);
    }
}
