using System.Security.Cryptography;
using System.Text;
using STS2RitsuLib.Audio;

namespace DohnaDohna.Code.Visuals;

public static class SquadAudio
{
    public const string Bank = "res://DohnaDohna/audio/fmod/DohnaDohna.bank";
    public const string Guids = "res://DohnaDohna/audio/fmod/GUIDs.txt";
    public const float GainDb = -20f;
    private static readonly float PlaybackVolume = MathF.Pow(10f, GainDb / 20f);
    public static void Register()
    {
        if (!Godot.FileAccess.FileExists(Bank) || !Godot.FileAccess.FileExists(Guids))
            throw new InvalidDataException("Required DohnaDohna FMOD bank/GUID map missing.");
        FmodStudioDeferredBankRegistration.RegisterBank(Bank);
        FmodStudioDeferredBankRegistration.RegisterStudioGuidMappings(Guids);
    }
    public static IAudioHandle Play(string reference) => CreatePlayback(reference, AudioLifecycleScope.Combat);

    // No combat visual owns event-room deaths. RitsuLib retains and releases
    // these one-shot handles at the native room boundary, even without a poster.
    internal static IAudioHandle PlayInRoom(string reference) => CreatePlayback(reference, AudioLifecycleScope.Room);

    private static IAudioHandle CreatePlayback(string reference, AudioLifecycleScope scope)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(reference))).ToLowerInvariant()[..20];
        var result = GameAudioService.Shared.Play(AudioSource.Event("event:/DohnaDohna/" + key),
            new AudioPlaybackOptions { Scope = scope, Volume = PlaybackVolume });
        return result.Handle ?? throw new InvalidOperationException($"DohnaDohna audio failed ({result.Status}): {reference}");
    }
}
