using NAudio.Wave;

namespace Orchestrion.Audio;

/// <summary>
/// Plays local audio files (MP3, WAV) via NAudio, with volume kept in
/// sync with the game's master × BGM volume settings.
/// </summary>
public static class LocalAudioPlayer
{
    private static WaveOutEvent? _waveOut;
    private static AudioFileReader? _audioReader;

    public static bool IsPlaying => _waveOut?.PlaybackState == PlaybackState.Playing;

    public static void Play(string filePath, float initialVolume = 1.0f)
    {
        Stop();
        _audioReader = new AudioFileReader(filePath);
        _waveOut = new WaveOutEvent();
        _waveOut.Init(_audioReader);
        SyncVolume(initialVolume);
        _waveOut.Play();
    }

    /// <summary>
    /// Seeks back to the start and resumes playback. Called by BGMManager when it
    /// detects natural end-of-file so looping stays on the main thread.
    /// </summary>
    public static void Restart()
    {
        if (_audioReader == null || _waveOut == null) return;
        _audioReader.Position = 0;
        _waveOut.Play();
    }

    public static void Stop()
    {
        _waveOut?.Stop();
        _waveOut?.Dispose();
        _waveOut = null;
        _audioReader?.Dispose();
        _audioReader = null;
    }

    /// <summary>
    /// Reads the current game master × BGM volume and applies it to the audio reader.
    /// Call this every frame while a local song is playing.
    /// </summary>
    /// <param name="fadeMultiplier">0–1 multiplier applied on top of the game volume for fade in/out.</param>
    public static void SyncVolume(float fadeMultiplier = 1.0f)
    {
        if (_audioReader == null) return;

        DalamudApi.GameConfig.System.TryGet("IsSndMaster", out bool isMasterMuted);
        DalamudApi.GameConfig.System.TryGet("SoundMaster", out uint masterVol);
        DalamudApi.GameConfig.System.TryGet("SoundBgm", out uint bgmVol);

        _audioReader.Volume = isMasterMuted ? 0f : (masterVol / 100f) * (bgmVol / 100f) * fadeMultiplier;
    }

    public static void Dispose() => Stop();

    /// <summary>
    /// Opens the file just long enough to read its duration, then closes it.
    /// </summary>
    public static TimeSpan ReadDuration(string filePath)
    {
        using var reader = new AudioFileReader(filePath);
        return reader.TotalTime;
    }
}
