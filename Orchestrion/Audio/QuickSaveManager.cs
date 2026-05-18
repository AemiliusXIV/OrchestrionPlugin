using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Orchestrion.Persistence;
using Orchestrion.Types;

namespace Orchestrion.Audio;

/// <summary>
/// Manages the Quick Save feature: chat link registration, saving songs to playlists,
/// confirmation echoes, and per-territory session tracking.
/// </summary>
public static class QuickSaveManager
{
    private const uint PrimaryCommandId   = 1;
    private const uint SecondaryCommandId = 2;

    private static DalamudLinkPayload _primaryPayload;
    private static DalamudLinkPayload _secondaryPayload;

    // Songs saved since the last territory change — used for the area summary.
    private static readonly List<(int Id, string Name)> _sessionSavedSongs = new();
    public static IReadOnlyList<(int Id, string Name)> SessionSavedSongs => _sessionSavedSongs;

    // -------------------------------------------------------------------------
    // Common zone exclusion list
    // -------------------------------------------------------------------------

    /// <summary>
    /// Territory IDs for main city hubs and residential districts.
    /// Quick Save is optionally hidden in these zones to reduce noise from
    /// frequently-repeating ambient tracks.
    /// </summary>
    private static readonly HashSet<uint> CommonZoneTerritoryIds = new()
    {
        // --- City hubs ---
        128, 129,       // Limsa Lominsa (Upper / Lower Decks)
        130, 131,       // Ul'dah (Steps of Nald / Steps of Thal)
        132, 133,       // Gridania (New / Old)
        418,            // Foundation (Ishgard)
        478,            // Idyllshire
        628,            // Kugane
        635,            // Rhalgr's Reach
        819, 820,       // The Crystarium / Eulmore
        962, 963,       // Old Sharlayan / Radz-at-Han
        1185, 1186,     // Tuliyollal / Solution Nine (Dawntrail)

        // --- Residential districts ---
        339,            // Mist
        340,            // The Lavender Beds
        341,            // The Goblet
        649,            // Shirogane
        979,            // Empyreum
    };

    /// <summary>
    /// Returns true when the player is in a common city or residential zone
    /// and the HideInCommonZones setting is enabled.
    /// </summary>
    public static bool IsHiddenByZone() =>
        Configuration.Instance.QuickSaveHideInCommonZones &&
        CommonZoneTerritoryIds.Contains(DalamudApi.ClientState.TerritoryType);

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    public static void Initialize()
    {
        _primaryPayload   = DalamudApi.ChatGui.AddChatLinkHandler(PrimaryCommandId,   OnPrimaryLinkClicked);
        _secondaryPayload = DalamudApi.ChatGui.AddChatLinkHandler(SecondaryCommandId, OnSecondaryLinkClicked);
    }

    public static void Dispose()
    {
        DalamudApi.ChatGui.RemoveChatLinkHandler(PrimaryCommandId);
        DalamudApi.ChatGui.RemoveChatLinkHandler(SecondaryCommandId);
    }

    // -------------------------------------------------------------------------
    // Chat link handlers
    // -------------------------------------------------------------------------

    private static void OnPrimaryLinkClicked(uint _, SeString __)
        => SaveSong(BGMManager.CurrentAudibleSong, Configuration.Instance.QuickSavePrimaryPlaylist);

    private static void OnSecondaryLinkClicked(uint _, SeString __)
        => SaveSong(BGMManager.CurrentAudibleSong, Configuration.Instance.QuickSaveSecondaryPlaylist);

    // -------------------------------------------------------------------------
    // Core save logic
    // -------------------------------------------------------------------------

    /// <summary>
    /// Saves <paramref name="songId"/> to <paramref name="playlistName"/>,
    /// auto-creating the playlist if it does not exist.
    /// Prints a confirmation echo if enabled and records the save for the area summary.
    /// Returns the resolved playlist name, or null if songId is 0 (nothing playing).
    /// </summary>
    public static string? SaveSong(int songId, string playlistName)
    {
        if (songId == 0) return null;

        if (!Configuration.Instance.TryGetPlaylist(playlistName, out var playlist))
        {
            playlist = new Playlist(playlistName);
            Configuration.Instance.Playlists[playlistName] = playlist;
            DalamudApi.PluginLog.Debug($"[QuickSave] Auto-created playlist '{playlistName}'.");
        }

        playlist.AddSong(songId); // AddSong persists via Configuration.Save()

        var name = ResolveSongName(songId);
        _sessionSavedSongs.Add((songId, name));

        if (Configuration.Instance.QuickSaveConfirmationEcho)
        {
            DalamudApi.ChatGui.Print(new XivChatEntry
            {
                Message = new SeStringBuilder()
                    .AddUiForeground("[Orchestrion] ", 35)
                    .AddText("Added ")
                    .AddItalics(name)
                    .AddText($" to {playlist.Name}.")
                    .Build(),
                Type = Configuration.Instance.ChatType,
            });
        }

        return playlist.Name;
    }

    // -------------------------------------------------------------------------
    // Chat echo injection
    // -------------------------------------------------------------------------

    /// <summary>
    /// Appends Quick Save link payload(s) to an already-built <see cref="SeString"/>.
    /// Call this immediately after constructing the echo message in OrchestrionPlugin.
    /// No-ops when QuickSaveShowInChat is off or Initialize has not been called yet.
    /// </summary>
    public static void AppendQuickSaveLinks(SeString msg)
    {
        if (!Configuration.Instance.QuickSaveShowInChat) return;
        if (_primaryPayload == null) return;
        if (IsHiddenByZone()) return;

        msg.Payloads.Add(new TextPayload(" ["));
        msg.Payloads.Add(_primaryPayload);
        msg.Payloads.Add(new UIForegroundPayload(576));
        msg.Payloads.Add(new TextPayload(Configuration.Instance.QuickSavePrimaryLabel));
        msg.Payloads.Add(new UIForegroundPayload(0));
        msg.Payloads.Add(RawPayload.LinkTerminator);
        msg.Payloads.Add(new TextPayload("]"));

        if (!Configuration.Instance.QuickSaveTwoActionMode) return;

        msg.Payloads.Add(new TextPayload(" ["));
        msg.Payloads.Add(_secondaryPayload);
        msg.Payloads.Add(new UIForegroundPayload(518));
        msg.Payloads.Add(new TextPayload(Configuration.Instance.QuickSaveSecondaryLabel));
        msg.Payloads.Add(new UIForegroundPayload(0));
        msg.Payloads.Add(RawPayload.LinkTerminator);
        msg.Payloads.Add(new TextPayload("]"));
    }

    // -------------------------------------------------------------------------
    // Session tracking
    // -------------------------------------------------------------------------

    public static void ClearSessionSaves() => _sessionSavedSongs.Clear();

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static string ResolveSongName(int songId)
    {
        if (LocalSong.IsLocalId(songId))
            return Configuration.Instance.LocalSongs.TryGetValue(songId, out var ls)
                ? ls.Name
                : $"Local Song {songId}";

        return SongList.Instance.TryGetSong(songId, out var song)
            ? song.Strings[Configuration.Instance.ChatLanguageCode].Name
            : $"Song {songId}";
    }
}
