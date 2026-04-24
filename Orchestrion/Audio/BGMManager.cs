using CheapLoc;
using Dalamud.Plugin.Services;
using Orchestrion.BGMSystem;
using Orchestrion.InnSystem;
using Orchestrion.Ipc;
using Orchestrion.Persistence;
using Orchestrion.Types;

namespace Orchestrion.Audio;

public static class BGMManager
{
    private static readonly BGMController _bgmController;
    private static readonly OrchestrionInnController _innController;
    private static readonly OrchestrionIpcManager _ipcManager;
    
    private static bool _isPlayingReplacement;
    private static string _ddPlaylist;

    // Local audio state
    private static bool _isPlayingLocalSong;
    private static int _playingLocalSongId;
    private static bool _mutedGameBgmForLocal;
    private static bool _originalGameBgmMute;

    // Local audio fade
    private enum LocalFadeMode { None, FadeIn, FadeOut }
    private static LocalFadeMode _localFadeMode = LocalFadeMode.None;
    private static long _localFadeStartMs;
    private const int LocalFadeDurationMs = 750;
    private static float _localFadeMultiplier = 1.0f;

    public delegate void SongChanged(int oldSong, int currentSong, int oldSecondSong, int oldCurrentSong, bool oldPlayedByOrch, bool playedByOrchestrion);
    public static event SongChanged OnSongChanged;

    public delegate void InnSongPlayed(string trackDtrName, string trackChatName);
    public static event InnSongPlayed OnInnSongPlayed;
    
    public static int CurrentSongId => _bgmController.CurrentSongId;

    /// <summary>
    /// Returns the local song ID when a local file is active, otherwise delegates to BGMController.
    /// </summary>
    public static int PlayingSongId => _isPlayingLocalSong ? _playingLocalSongId : _bgmController.PlayingSongId;

    /// <summary>
    /// The song the user actually hears: local ID when local is active, otherwise game's audible song.
    /// </summary>
    public static int CurrentAudibleSong => _isPlayingLocalSong ? _playingLocalSongId : _bgmController.CurrentAudibleSong;

    public static int PlayingScene => _bgmController.PlayingScene;
    
    static BGMManager()
    {
        _bgmController = new BGMController();
        _innController = new OrchestrionInnController();
        _ipcManager = new OrchestrionIpcManager();

        DalamudApi.Framework.Update += Update;
        _bgmController.OnSongChanged += HandleSongChanged;
        _innController.OnPlayingSongChanged += HandleInnSongChanged;
        OnSongChanged += IpcUpdate;
    }

    public static void Dispose()
    {
        DalamudApi.Framework.Update -= Update;
        // Force-complete any pending fade before full shutdown
        if (_localFadeMode != LocalFadeMode.None)
        {
            _localFadeMode = LocalFadeMode.None;
            StopLocalMute();
        }
        Stop();
        LocalAudioPlayer.Dispose();
        OnSongChanged -= IpcUpdate;
        _innController.OnPlayingSongChanged -= HandleInnSongChanged;
        _bgmController.OnSongChanged -= HandleSongChanged;
        _bgmController.Dispose();
    }

    private static void IpcUpdate(int oldSong, int newSong, int oldSecondSong, int oldCurrentSong, bool oldPlayedByOrch, bool playedByOrch)
    {
        _ipcManager.InvokeSongChanged(newSong);
        if (playedByOrch) _ipcManager.InvokeOrchSongChanged(newSong);
    }
    
    public static void Update(IFramework ignored)
    {
        _innController.Update();
        _bgmController.Update();
        UpdateLocalFade();
        if (_isPlayingLocalSong && _localFadeMode == LocalFadeMode.None)
        {
            if (!LocalAudioPlayer.IsPlaying)
                LocalAudioPlayer.Restart();
            else
                LocalAudioPlayer.SyncVolume();
        }
    }

    private static void UpdateLocalFade()
    {
        if (_localFadeMode == LocalFadeMode.None) return;
        var elapsed = Environment.TickCount64 - _localFadeStartMs;
        var progress = Math.Clamp((float)elapsed / LocalFadeDurationMs, 0f, 1f);

        if (_localFadeMode == LocalFadeMode.FadeIn)
        {
            _localFadeMultiplier = progress;
            LocalAudioPlayer.SyncVolume(_localFadeMultiplier);
            if (progress >= 1f)
            {
                _localFadeMode = LocalFadeMode.None;
                _localFadeMultiplier = 1f;
            }
        }
        else // FadeOut
        {
            _localFadeMultiplier = 1f - progress;
            LocalAudioPlayer.SyncVolume(_localFadeMultiplier);
            if (progress >= 1f)
            {
                LocalAudioPlayer.Stop();
                StopLocalMute();
                _localFadeMode = LocalFadeMode.None;
                _localFadeMultiplier = 1f;
            }
        }
    }
    
    private static void HandleInnSongChanged(uint oldInnPlayingTrackId, uint newInnPlayingTrackId, string oldInnTrackDtrName, string newInnTrackDtrName, string newInnTrackChatName)
    {
        if (newInnPlayingTrackId > 0)
        {
            // Estate orchestrions play over BGM, so trying to play BGM now is useless
            Stop();
            DalamudApi.PluginLog.Debug($"[BGMManager::HandleInnSongChanged] Inn song changed from {oldInnPlayingTrackId} - '{oldInnTrackDtrName}' to {newInnPlayingTrackId} - '{newInnTrackChatName}'");
            OnInnSongPlayed.Invoke(newInnTrackDtrName, newInnTrackChatName);
        }
        else
        {
            // Force update for BGM Manager
            HandleSongChanged(0, _bgmController.CurrentSongId, _bgmController.OldSecondSongId, _bgmController.SecondSongId);
        }
    }

    private static void HandleSongChanged(int oldSong, int newSong, int oldSecondSong, int newSecondSong)
    {
        // Estate orchestrions play over BGM, so trying to handle BGM now is useless
        if (InnMusicActive()) return;

        var currentChanged = oldSong != newSong;
        var secondChanged = oldSecondSong != newSecondSong;
        
        var newHasReplacement = Configuration.Instance.SongReplacements.TryGetValue(newSong, out var newSongReplacement);
        var newSecondHasReplacement = Configuration.Instance.SongReplacements.TryGetValue(newSecondSong, out var newSecondSongReplacement);
        
        if (currentChanged)
            DalamudApi.PluginLog.Debug($"[HandleSongChanged] Current Song ID changed from {_bgmController.OldSongId} to {_bgmController.CurrentSongId}");
        
        if (secondChanged)
            DalamudApi.PluginLog.Debug($"[HandleSongChanged] Second song ID changed from {_bgmController.OldSecondSongId} to {_bgmController.SecondSongId}");
        
        if (PlayingSongId != 0 && DeepDungeonModeActive())
        {
            if (!string.IsNullOrEmpty(_ddPlaylist))
            {
                if (!Configuration.Instance.Playlists.ContainsKey(_ddPlaylist)) // user deleted playlist
                    Stop();
                else
                    PlayRandomSong(_ddPlaylist);
            }
            return;
        }

        if (PlayingSongId != 0 && !_isPlayingReplacement) return; // manually playing track (includes local songs)
        if (secondChanged && !currentChanged && !_isPlayingReplacement) return; // don't care about behind song if not playing replacement

        if (!newHasReplacement) // user isn't playing and no replacement at all
        {
            if (PlayingSongId != 0)
                Stop();
            else
                // Play and Stop invoke OnSongChanged themselves
                InvokeSongChanged(oldSong, newSong, oldSecondSong, newSecondSong, oldPlayedByOrch: false, playedByOrch: false);
            return;
        }

        var toPlay = 0;
        
        DalamudApi.PluginLog.Debug($"[HandleSongChanged] Song ID {newSong} has a replacement of {newSongReplacement.ReplacementId}");
        
        // Handle 2nd changing when 1st has replacement of NoChangeId, only time it matters
        if (newSongReplacement.ReplacementId == SongReplacementEntry.NoChangeId)
        {
            if (secondChanged)
            {
                toPlay = newSecondSong;
                if (newSecondHasReplacement) toPlay = newSecondSongReplacement.ReplacementId;
                if (toPlay == SongReplacementEntry.NoChangeId) return; // give up
            }    
        }
            
        if (newSongReplacement.ReplacementId == SongReplacementEntry.NoChangeId && PlayingSongId == 0)
        {
            toPlay = oldSong; // no net BGM change
        }
        else if (newSongReplacement.ReplacementId != SongReplacementEntry.NoChangeId)
        {
            toPlay = newSongReplacement.ReplacementId;
        }
        
        Play(toPlay, isReplacement: true); // we only ever play a replacement here
    }

    public static void Play(int songId, bool isReplacement = false)
    {
        // Estate orchestrions play over BGM, so trying to play BGM now is useless
        if (InnMusicActive()) return;

        var wasPlaying = PlayingSongId != 0;
        var oldSongId = CurrentAudibleSong;
        var secondSongId = _bgmController.SecondSongId;

        if (LocalSong.IsLocalId(songId))
        {
            if (!Configuration.Instance.LocalSongs.TryGetValue(songId, out var localSong))
            {
                DalamudApi.PluginLog.Warning($"[Play] Local song ID {songId} not found in library");
                return;
            }

            // Stop any current game BGM override
            if (_bgmController.PlayingSongId != 0)
                _bgmController.SetSong(0);

            // Cancel any in-progress fade and clean up previous local audio
            if (_localFadeMode != LocalFadeMode.None)
            {
                LocalAudioPlayer.Stop();
                StopLocalMute();
                _localFadeMode = LocalFadeMode.None;
            }
            else if (_isPlayingLocalSong)
            {
                StopLocalMute();
            }

            DalamudApi.PluginLog.Debug($"[Play] Playing local song {songId} '{localSong.Name}'");
            MuteGameBgm();
            LocalAudioPlayer.Play(localSong.FilePath, 0f);
            _localFadeMode = LocalFadeMode.FadeIn;
            _localFadeStartMs = Environment.TickCount64;
            _localFadeMultiplier = 0f;
            _isPlayingLocalSong = true;
            _playingLocalSongId = songId;
            _isPlayingReplacement = isReplacement;
            InvokeSongChanged(oldSongId, songId, secondSongId, oldSongId, oldPlayedByOrch: wasPlaying, playedByOrch: true);
            return;
        }

        // Switching away from a local song to an in-game song (abort any fade)
        if (_isPlayingLocalSong || _localFadeMode != LocalFadeMode.None)
        {
            LocalAudioPlayer.Stop();
            StopLocalMute();
            _isPlayingLocalSong = false;
            _playingLocalSongId = 0;
            _localFadeMode = LocalFadeMode.None;
            _localFadeMultiplier = 1f;
        }

        DalamudApi.PluginLog.Debug($"[Play] Playing {songId}");
        InvokeSongChanged(oldSongId, songId, secondSongId, oldSongId, oldPlayedByOrch: wasPlaying, playedByOrch: true);
        _bgmController.SetSong((ushort)songId);
        _isPlayingReplacement = isReplacement;
    }

    private static void MuteGameBgm()
    {
        DalamudApi.GameConfig.System.TryGet("IsSndBgm", out _originalGameBgmMute);
        if (!_originalGameBgmMute)
        {
            DalamudApi.GameConfig.System.Set("IsSndBgm", true);
            _mutedGameBgmForLocal = true;
        }
    }

    private static void StopLocalMute()
    {
        if (!_mutedGameBgmForLocal) return;
        DalamudApi.GameConfig.System.Set("IsSndBgm", _originalGameBgmMute);
        _mutedGameBgmForLocal = false;
    }

    public static void Stop()
    {
        if (PlaylistManager.IsPlaying)
        {
            DalamudApi.PluginLog.Debug("[Stop] Stopping playlist...");
            PlaylistManager.Reset();
        }

        _ddPlaylist = null;

        if (_isPlayingLocalSong)
        {
            DalamudApi.PluginLog.Debug($"[Stop] Stopping local song {_playingLocalSongId}...");
            var wasLocalId = _playingLocalSongId;
            _isPlayingLocalSong = false;
            _playingLocalSongId = 0;
            _isPlayingReplacement = false;
            // Fade out audio then stop — StopLocalMute() is deferred to UpdateLocalFade completion
            _localFadeMode = LocalFadeMode.FadeOut;
            _localFadeStartMs = Environment.TickCount64;
            _localFadeMultiplier = 1f;
            InvokeSongChanged(wasLocalId, CurrentSongId, _bgmController.SecondSongId, _bgmController.SecondSongId, oldPlayedByOrch: true, playedByOrch: false);
            return;
        }

        if (PlayingSongId == 0) return;
        DalamudApi.PluginLog.Debug($"[Stop] Stopping playing {_bgmController.PlayingSongId}...");

        if (Configuration.Instance.SongReplacements.TryGetValue(_bgmController.CurrentSongId, out var replacement))
        {
            DalamudApi.PluginLog.Debug($"[Stop] Song ID {_bgmController.CurrentSongId} has a replacement of {replacement.ReplacementId}...");

            var toPlay = replacement.ReplacementId;
            
            if (toPlay == SongReplacementEntry.NoChangeId)
                toPlay = _bgmController.OldSongId;

            // Play will invoke OnSongChanged for us
            Play(toPlay, isReplacement: true);
            return;
        }

        var second = _bgmController.SecondSongId;
        
        // If there was no replacement involved, we don't need to do anything else, just stop
        InvokeSongChanged(PlayingSongId, CurrentSongId, second, second, oldPlayedByOrch: true, playedByOrch: false);
        _bgmController.SetSong(0);
    }

    private static void InvokeSongChanged(int oldSongId, int newSongId, int oldSecondSongId, int newSecondSongId, bool oldPlayedByOrch, bool playedByOrch)
    {
        DalamudApi.PluginLog.Debug($"[InvokeSongChanged] Invoking SongChanged event with {oldSongId} -> {newSongId}, {oldSecondSongId} -> {newSecondSongId} | {oldPlayedByOrch} {playedByOrch}");
        OnSongChanged?.Invoke(oldSongId, newSongId, oldSecondSongId, newSecondSongId, oldPlayedByOrch, playedByOrch);
    }

    public static void PlayRandomSong(string playlistName = "")
    {
        if (SongList.Instance.TryGetRandomSong(playlistName, out var randomFavoriteSong))
            Play(randomFavoriteSong);
        else
            DalamudApi.ChatGui.PrintError(Loc.Localize("NoPossibleSongs", "No possible songs found."));
    }

    public static void StartDeepDungeonMode(string playlistName = "")
    {
        _ddPlaylist = playlistName;
        PlayRandomSong(_ddPlaylist);
    }

    public static void StopDeepDungeonMode()
    {
        _ddPlaylist = null;
        Stop();
    }

    public static bool DeepDungeonModeActive()
    {
        return _ddPlaylist != null;
    }

    public static bool InnMusicActive()
    {
        return  _innController.IsInnTrackPlaying();
    }
}