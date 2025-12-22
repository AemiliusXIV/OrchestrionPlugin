using System.Threading;
using Orchestrion.Persistence;

namespace Orchestrion.InnSystem;

internal class OrchestrionInnController
{
    private readonly OrchestrionInnAddressResolver InnAddressResolver = new OrchestrionInnAddressResolver();
    private readonly Lock InnTrackDataAccessLock = new Lock();

    private OrchestrionInnPlaybackMode NewInnPlaybackMode = OrchestrionInnPlaybackMode.None;
    private OrchestrionInnPlaybackState NewInnPlaybackState = OrchestrionInnPlaybackState.None;
    private uint NewInnSamplingTrackId = 0;
    private uint NewInnPlayingTrackId = 0;
    private string NewInnTrackDtrName = string.Empty;
    private string NewInnTrackChatName = string.Empty;

    private OrchestrionInnPlaybackMode OldInnPlaybackMode = OrchestrionInnPlaybackMode.None;
    private OrchestrionInnPlaybackState OldInnPlaybackState = OrchestrionInnPlaybackState.None;
    private uint OldInnSamplingTrackId = 0;
    private uint OldInnPlayingTrackId = 0;
    private string OldInnTrackDtrName = string.Empty;
    private string OldInnTrackChatName = string.Empty;

    internal delegate void InnPlayingSongChangedHandler(
        uint oldPlayingTrackId, uint newPlayingTrackId,
        string oldInnTrackDtrName, string newInnTrackDtrName, string newInnTrackChatName
    );
    internal event InnPlayingSongChangedHandler? OnPlayingSongChanged;

    internal OrchestrionInnController()
    {
        InnAddressResolver.Setup(DalamudApi.SigScanner);
        ResetAllFieldValues();
    }

    internal bool IsInnTrackPlaying() => NewInnPlaybackState != OrchestrionInnPlaybackState.None;
    internal bool IsInnTrackSampling() => NewInnPlaybackState == OrchestrionInnPlaybackState.Sampling;

    internal void Update()
    {
        lock (InnTrackDataAccessLock)
        {
            bool innTrackChanged = UpdateFieldValues();

            if (innTrackChanged)
            {
                HandleNewInnTrack();
            }

            RecordOldFieldValues();
        }
    }

    private void HandleNewInnTrack()
    {
        string ServerInfoLanguageCode = Configuration.Instance.ServerInfoLanguageCode;
        string ChatLanguageCode = Configuration.Instance.ChatLanguageCode;

        if (IsInnTrackPlaying())
        {
            NewInnTrackDtrName = GetInnTrackName(NewInnPlayingTrackId, ServerInfoLanguageCode);
            NewInnTrackChatName = GetInnTrackName(NewInnPlayingTrackId, ChatLanguageCode);
        }

        OnPlayingSongChanged?.Invoke(
            OldInnPlayingTrackId, NewInnPlayingTrackId,
            OldInnTrackDtrName, NewInnTrackDtrName, NewInnTrackChatName
        );
    }

    private string GetInnTrackName(uint trackId, string languageCode)
    {
        string innTrackName;

        try
        {
            innTrackName = DalamudApi.DataManager.Excel.GetSheet<Lumina.Excel.Sheets.Orchestrion>(
                language: Util.LangCodeToLuminaLanguage(languageCode)
            ).GetRow(trackId).Name.ToString();
        }
        catch (Exception ex)
        {
            DalamudApi.PluginLog.Warning(ex, $"[OrchestrionInnController::GetInnTrackName] Exception thrown while looking up chat name for new inn song with track ID {trackId} and language code {languageCode}, falling back to English");
            innTrackName = DalamudApi.DataManager.Excel.GetSheet<Lumina.Excel.Sheets.Orchestrion>(
                language: Lumina.Data.Language.English
            ).GetRow(trackId).Name.ToString();
        }

        return innTrackName;
    }

    private bool UpdateFieldValues()
    {
        NewInnPlaybackMode = InnAddressResolver.GetInnPlaybackMode();
        NewInnPlaybackState = InnAddressResolver.GetInnPlaybackState();
        NewInnPlayingTrackId = IsInnTrackPlaying() ? InnAddressResolver.GetInnPlayingTrackId() : OldInnPlayingTrackId;
        NewInnSamplingTrackId = IsInnTrackSampling() ? InnAddressResolver.GetInnSamplingTrackId() : OldInnSamplingTrackId;

        if (OldInnPlaybackState != OrchestrionInnPlaybackState.None && NewInnPlaybackState == OrchestrionInnPlaybackState.None)
        {
            ResetNewFieldValues();
        }

        bool innTrackChanged = NewInnPlayingTrackId != OldInnPlayingTrackId;
        return innTrackChanged;
    }

    private void ResetNewFieldValues()
    {
        NewInnPlaybackMode = OrchestrionInnPlaybackMode.None;
        NewInnPlaybackState = OrchestrionInnPlaybackState.None;
        NewInnSamplingTrackId = 0;
        NewInnPlayingTrackId = 0;
        NewInnTrackDtrName = string.Empty;
        NewInnTrackChatName = string.Empty;
    }

    private void ResetOldFieldValues()
    {
        OldInnPlaybackMode = OrchestrionInnPlaybackMode.None;
        OldInnPlaybackState = OrchestrionInnPlaybackState.None;
        OldInnSamplingTrackId = 0;
        OldInnPlayingTrackId = 0;
        OldInnTrackDtrName = string.Empty;
        OldInnTrackChatName = string.Empty;
    }

    private void ResetAllFieldValues()
    {
        ResetOldFieldValues();
        ResetNewFieldValues();
    }

    private void RecordOldFieldValues()
    {
        OldInnPlaybackMode = NewInnPlaybackMode;
        OldInnPlaybackState = NewInnPlaybackState;
        OldInnSamplingTrackId = NewInnSamplingTrackId;
        OldInnPlayingTrackId = NewInnPlayingTrackId;
        OldInnTrackDtrName = NewInnTrackDtrName;
        OldInnTrackChatName = NewInnTrackChatName;
    }
}
