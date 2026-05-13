using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Configuration;
using Dalamud.Game.Text;
using Newtonsoft.Json;
using Orchestrion.Types;

namespace Orchestrion.Persistence;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool ShowSongInTitleBar { get; set; } = true;
    public bool ShowSongInChat { get; set; } = true;
    public bool ShowIdInNative { get; set; } = false;
    public bool ShowSongInNative { get; set; } = true;
    public bool HandleSpecialModes { get; set; } = true;
    public bool ShowFilePaths { get; set; } = true;
    public bool PlaylistPaneOpen { get; set; } = true;
    public bool ShowMiniPlayer { get; set; } = false;
    public bool MiniPlayerLock { get; set; } = false;
    public float MiniPlayerOpacity { get; set; } = 1.0f;

    public bool ChatChannelMatchDalamud { get; set; } = true;
    public bool ShowAltLangTitles { get; set; } = false;
    public bool UserInterfaceLanguageMatchDalamud { get; set; } = true;
    public string UserInterfaceLanguageCode { get; set; } = DalamudApi.PluginInterface.UiLanguage;
    public string AltTitleLanguageCode { get; set; } = "ja";
    public string ServerInfoLanguageCode { get; set; } = "en";
    public string ChatLanguageCode { get; set; } = "en";
    public XivChatType ChatType { get; set; } = DalamudApi.PluginInterface.GeneralChatType;
    public bool DisableTooltips { get; set; } = false;
    public bool DisableFurnishingMessages { get; set; } = false;
    public bool DisableInCutscenes { get; set; } = false;
    public bool DisableReplacementsInCutscenes { get; set; } = false;

    public string LastSelectedPlaylist { get; set; } = "Favorites";

    public Dictionary<int, SongReplacementEntry> SongReplacements { get; private set; } = new();

    public Dictionary<int, LocalSong> LocalSongs { get; set; } = new();

    /// <summary>
    /// When true (default), audio files are copied into the plugin's own storage folder on import
    /// so they keep working even if the original file is moved or deleted.
    /// When false, only the original file path is stored (legacy behaviour).
    /// </summary>
    public bool CopyLocalSongsToStorage { get; set; } = true;
    public bool LocalSongsAtTop { get; set; } = false;

    /// <summary>
    /// Set to true while a local song is muting the game BGM, persisted so that if
    /// the plugin is killed mid-playback (crash, abrupt unload) we can detect on the
    /// next startup that we left the game BGM muted and restore it automatically.
    /// </summary>
    public bool LocalAudioMutedGameBgm { get; set; } = false;

    /// <summary>
    /// Set to true after the Local Library tab has been opened for the first time.
    /// Used to show a one-time introduction notification.
    /// </summary>
    public bool HasShownLocalLibraryIntro { get; set; } = false;

    /// <summary>
    /// Set to true once the startup import-available notification has been shown,
    /// so it does not repeat on every subsequent launch.
    /// </summary>
    public bool HasShownImportNotice { get; set; } = false;

    /// <summary>
    /// Set to true after a successful import from a predecessor config,
    /// so the import section in Settings is permanently hidden once done.
    /// </summary>
    public bool HasCompletedImport { get; set; } = false;

    [JsonIgnore]
    public static string LocalSongsStorageDir =>
        Path.Combine(DalamudApi.PluginInterface.ConfigDirectory.FullName, "LocalSongs");
    
    [Obsolete("Favorites are gone in favor of playlists.")]
    public HashSet<int> FavoriteSongs { get; internal set; } = new();
    
    public Dictionary<string, Playlist> Playlists { get; set; } = new();

    private Configuration() { }

    [JsonIgnore]
    private static Configuration _instance;
    
    [JsonIgnore]
    public static Configuration Instance {
        get
        {
            _instance ??= DalamudApi.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Migrate(_instance);
            return _instance;
        }
    }

    public bool TryGetPlaylist(string playlistName, out Playlist foundPlaylist)
    {
        foundPlaylist = null;
        foreach (var pInfo in Playlists) {
            if (playlistName.Equals(pInfo.Key, StringComparison.InvariantCultureIgnoreCase))
            {
                foundPlaylist = pInfo.Value;
                return true;
            }
        }
        return false;
    }

    public void DeletePlaylist(string playlistName)
    {
        Playlists.Remove(playlistName);
        Save();
    }

    private static void Migrate(Configuration c)
    {
        switch (c.Version)
        {
            case 1:
                c.Version = 2;
                c.Playlists = new Dictionary<string, Playlist>
                {
                    {"Favorites", new Playlist("Favorites", c.FavoriteSongs.ToList())},
                };
                c.Save();
                break;
        }
    }

    /// <summary>
    /// Adds a local song, auto-assigns a negative ID, persists, and returns the new ID.
    /// If CopyLocalSongsToStorage is true the file is copied into the plugin's storage folder first.
    /// </summary>
    public int AddLocalSong(string name, string filePath, TimeSpan duration)
    {
        var storedPath = filePath;
        if (CopyLocalSongsToStorage)
            storedPath = CopyToStorage(filePath);

        var id = LocalSongs.Count == 0 ? LocalSong.LocalIdMin : LocalSongs.Keys.Min() - 1;
        var displayId = FindNextAvailableDisplayId();
        LocalSongs[id] = new LocalSong { Id = id, DisplayId = displayId, Name = name, FilePath = storedPath, Duration = duration };
        Save();
        return id;
    }

    /// <summary>
    /// Copies a file into the plugin's LocalSongs storage folder, handling filename collisions.
    /// Returns the destination path.
    /// </summary>
    private static string CopyToStorage(string sourcePath)
    {
        Directory.CreateDirectory(LocalSongsStorageDir);
        var fileName  = Path.GetFileNameWithoutExtension(sourcePath);
        var extension = Path.GetExtension(sourcePath);
        var destPath  = Path.Combine(LocalSongsStorageDir, fileName + extension);

        // Handle filename collisions
        var counter = 1;
        while (File.Exists(destPath))
        {
            destPath = Path.Combine(LocalSongsStorageDir, $"{fileName}_{counter}{extension}");
            counter++;
        }

        File.Copy(sourcePath, destPath);
        return destPath;
    }

    /// <summary>
    /// Returns true if the given path is inside the plugin's LocalSongs storage folder.
    /// </summary>
    public static bool IsInStorage(string filePath) =>
        filePath.StartsWith(LocalSongsStorageDir, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns the number of local songs whose files exist but are not yet in plugin storage.
    /// </summary>
    public int CountSongsOutsideStorage() =>
        LocalSongs.Values.Count(s => !IsInStorage(s.FilePath) && File.Exists(s.FilePath));

    /// <summary>
    /// Copies all local songs not already in storage into the plugin's storage folder and
    /// updates their stored paths. Optionally deletes the original files afterwards.
    /// Returns the number of songs successfully migrated.
    /// </summary>
    public int MigrateLocalSongsToStorage(bool deleteOriginals)
    {
        var migrated = 0;
        foreach (var song in LocalSongs.Values)
        {
            if (IsInStorage(song.FilePath)) continue;
            if (!File.Exists(song.FilePath)) continue;
            try
            {
                var newPath = CopyToStorage(song.FilePath);
                if (deleteOriginals)
                {
                    try { File.Delete(song.FilePath); }
                    catch (Exception ex) { DalamudApi.PluginLog.Warning(ex, $"[LocalLibrary] Could not delete original '{song.FilePath}'"); }
                }
                song.FilePath = newPath;
                migrated++;
            }
            catch (Exception ex)
            {
                DalamudApi.PluginLog.Warning(ex, $"[LocalLibrary] Could not migrate '{song.FilePath}'");
            }
        }
        if (migrated > 0) Save();
        return migrated;
    }

    private int FindNextAvailableDisplayId()
    {
        var used = LocalSongs.Values.Select(ls => ls.DisplayId).ToHashSet();
        var candidate = LocalSong.DisplayIdStart;
        while (SongList.Instance.TryGetSong(candidate, out _) || used.Contains(candidate))
            candidate++;
        return candidate;
    }

    /// <summary>
    /// Ensures every local song has a unique DisplayId that doesn't clash with a game song.
    /// Call this once after both SongList and Configuration are loaded.
    /// </summary>
    public void ValidateLocalSongIds()
    {
        var reassigned = false;
        var used = new HashSet<int>();
        foreach (var song in LocalSongs.Values)
        {
            if (song.DisplayId == 0 || SongList.Instance.TryGetSong(song.DisplayId, out _) || used.Contains(song.DisplayId))
            {
                var candidate = LocalSong.DisplayIdStart;
                while (SongList.Instance.TryGetSong(candidate, out _) || used.Contains(candidate))
                    candidate++;
                song.DisplayId = candidate;
                reassigned = true;
            }
            used.Add(song.DisplayId);
        }
        if (reassigned) Save();
    }

    public void RemoveLocalSong(int id)
    {
        // If the file was copied into storage, delete it
        if (LocalSongs.TryGetValue(id, out var song) && IsInStorage(song.FilePath))
        {
            try { File.Delete(song.FilePath); }
            catch (Exception ex) { DalamudApi.PluginLog.Warning(ex, $"[LocalLibrary] Could not delete stored file '{song.FilePath}'"); }
        }

        LocalSongs.Remove(id);
        // Clean up any replacements that pointed to this local song
        var toRemove = SongReplacements.Where(kv => kv.Value.ReplacementId == id).Select(kv => kv.Key).ToList();
        foreach (var key in toRemove)
            SongReplacements.Remove(key);
        Save();
    }

    public void Save()
    {
        DalamudApi.PluginInterface.SavePluginConfig(this);
    }

    // -------------------------------------------------------------------------
    // Import from the official Orchestrion plugin
    // -------------------------------------------------------------------------

    /// <summary>
    /// A read-only snapshot of what PeekOrchestrionImport found in orchestrion.json,
    /// used to populate the confirmation popup before the user commits to importing.
    /// </summary>
    public record OrchestrionImportPreview(int ReplacementCount, List<string> PlaylistNames);

    /// <summary>
    /// Returns true if the original Orchestrion plugin's config file exists on disk.
    /// Used to conditionally show the import button in Settings.
    /// </summary>
    public static bool OrchestrionConfigExists() =>
        File.Exists(Path.Combine(
            DalamudApi.PluginInterface.ConfigDirectory.Parent!.FullName, "orchestrion.json"));

    /// <summary>
    /// Reads orchestrion.json and returns a lightweight preview of what would be
    /// imported (replacement count, playlist names) without modifying anything.
    /// Returns null if the file is missing or cannot be parsed.
    /// </summary>
    public static OrchestrionImportPreview? PeekOrchestrionImport()
    {
        var path = Path.Combine(
            DalamudApi.PluginInterface.ConfigDirectory.Parent!.FullName, "orchestrion.json");
        if (!File.Exists(path)) return null;

        try
        {
            var obj = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path));

            var replacements = obj["SongReplacements"]?
                .ToObject<Dictionary<int, SongReplacementEntry>>();

            var playlists = obj["Playlists"]?
                .ToObject<Dictionary<string, Newtonsoft.Json.Linq.JObject>>();

            return new OrchestrionImportPreview(
                replacements?.Count ?? 0,
                playlists?.Keys.ToList() ?? new List<string>());
        }
        catch (Exception ex)
        {
            DalamudApi.PluginLog.Warning(ex, "[Import] Could not peek orchestrion.json");
            return null;
        }
    }

    /// <summary>
    /// Reads orchestrion.json from the sibling plugin config directory and merges its
    /// general settings, song replacements, and playlists into this configuration.
    /// Local Library songs are intentionally not imported.
    /// Returns the number of song replacements imported, or -1 on failure.
    /// </summary>
    public int ImportFromOrchestrion(bool importSettings = true, bool importReplacements = true, bool importPlaylists = true)
    {
        var oldConfigPath = Path.Combine(
            DalamudApi.PluginInterface.ConfigDirectory.Parent!.FullName, "orchestrion.json");
        if (!File.Exists(oldConfigPath)) return -1;

        try
        {
            var json = File.ReadAllText(oldConfigPath);
            var obj  = Newtonsoft.Json.Linq.JObject.Parse(json);

            if (importSettings)
            {
                void TryBool(string key, Action<bool> set)
                {
                    var t = obj[key];
                    if (t != null) set(t.ToObject<bool>());
                }

                void TryString(string key, Action<string> set)
                {
                    var t = obj[key];
                    if (t != null && t.Type != Newtonsoft.Json.Linq.JTokenType.Null) set(t.ToObject<string>()!);
                }

                TryBool("ShowSongInTitleBar",                v => ShowSongInTitleBar                = v);
                TryBool("ShowSongInChat",                    v => ShowSongInChat                    = v);
                TryBool("ShowSongInNative",                  v => ShowSongInNative                  = v);
                TryBool("ShowIdInNative",                    v => ShowIdInNative                    = v);
                TryBool("HandleSpecialModes",                v => HandleSpecialModes                = v);
                TryBool("ChatChannelMatchDalamud",           v => ChatChannelMatchDalamud           = v);
                TryBool("ShowAltLangTitles",                 v => ShowAltLangTitles                 = v);
                TryBool("UserInterfaceLanguageMatchDalamud", v => UserInterfaceLanguageMatchDalamud = v);
                TryBool("DisableTooltips",                   v => DisableTooltips                   = v);
                TryBool("ShowMiniPlayer",                    v => ShowMiniPlayer                    = v);
                TryBool("MiniPlayerLock",                    v => MiniPlayerLock                    = v);

                TryString("UserInterfaceLanguageCode", v => UserInterfaceLanguageCode = v);
                TryString("AltTitleLanguageCode",      v => AltTitleLanguageCode      = v);
                TryString("ServerInfoLanguageCode",    v => ServerInfoLanguageCode    = v);
                TryString("ChatLanguageCode",          v => ChatLanguageCode          = v);

                var opacityToken = obj["MiniPlayerOpacity"];
                if (opacityToken != null) MiniPlayerOpacity = opacityToken.ToObject<float>();

                var chatTypeToken = obj["ChatType"];
                if (chatTypeToken != null) ChatType = (XivChatType)chatTypeToken.ToObject<int>();
            }

            // Song replacements — merge (existing entries are overwritten)
            var importedCount = 0;
            if (importReplacements)
            {
                var replacementsToken = obj["SongReplacements"];
                if (replacementsToken != null)
                {
                    var dict = replacementsToken.ToObject<Dictionary<int, SongReplacementEntry>>();
                    if (dict != null)
                    {
                        foreach (var (k, v) in dict)
                            SongReplacements[k] = v;
                        importedCount = dict.Count;
                    }
                }
            }

            // Playlists — merge (existing playlists are overwritten by name)
            if (importPlaylists)
            {
                var playlistsToken = obj["Playlists"];
                if (playlistsToken != null)
                {
                    var dict = playlistsToken.ToObject<Dictionary<string, Playlist>>();
                    if (dict != null)
                        foreach (var (k, v) in dict)
                            Playlists[k] = v;
                }
            }

            Save();
            return importedCount;
        }
        catch (Exception ex)
        {
            DalamudApi.PluginLog.Error(ex, "[Import] Failed to import settings from orchestrion.json");
            return -1;
        }
    }
}