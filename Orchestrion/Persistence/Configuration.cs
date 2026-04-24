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
    
    public string LastSelectedPlaylist { get; set; } = "Favorites";

    public Dictionary<int, SongReplacementEntry> SongReplacements { get; private set; } = new();

    public Dictionary<int, LocalSong> LocalSongs { get; set; } = new();

    /// <summary>
    /// When true (default), audio files are copied into the plugin's own storage folder on import
    /// so they keep working even if the original file is moved or deleted.
    /// When false, only the original file path is stored (legacy behaviour).
    /// </summary>
    public bool CopyLocalSongsToStorage { get; set; } = true;

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
}