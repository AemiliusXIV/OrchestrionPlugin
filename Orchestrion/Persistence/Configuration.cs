using System.Collections.Generic;
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
    /// </summary>
    public int AddLocalSong(string name, string filePath, TimeSpan duration)
    {
        var id = LocalSongs.Count == 0 ? LocalSong.LocalIdMin : LocalSongs.Keys.Min() - 1;
        var displayId = FindNextAvailableDisplayId();
        LocalSongs[id] = new LocalSong { Id = id, DisplayId = displayId, Name = name, FilePath = filePath, Duration = duration };
        Save();
        return id;
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