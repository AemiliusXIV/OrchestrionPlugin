using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Bindings.ImGui;
using Orchestrion.Audio;
using Orchestrion.Persistence;
using Orchestrion.Types;

namespace Orchestrion.UI.Windows.MainWindow;

public partial class MainWindow
{
    private string _localLibraryNewPath = string.Empty;
    private string _localLibraryNewName = string.Empty;
    private string _localLibraryError = string.Empty;
    private string _localLibraryImportStatus = string.Empty;
    private readonly List<int> _localLibraryRemovalList = new();
    private readonly FileDialogManager _fileDialogManager = new();

    private void DrawLocalLibraryTab()
    {
        _fileDialogManager.Draw();

        ImGui.BeginChild("##locallibraryroot");

        DrawLocalLibraryAddSection();
        ImGui.Separator();
        DrawLocalLibraryList();

        ImGui.EndChild();
    }

    private void DrawLocalLibraryAddSection()
    {
        ImGui.Spacing();
        ImGui.TextUnformatted("Add a single file (MP3 or WAV)");
        ImGui.Spacing();

        var width = ImGui.GetWindowWidth() * 0.60f;

        ImGui.SetNextItemWidth(width);
        ImGui.InputText("File path##localpath", ref _localLibraryNewPath, 512);
        ImGui.SameLine();
        if (ImGui.Button("Browse##localbrowse"))
            BrowseForLocalFile();

        ImGui.SetNextItemWidth(width);
        ImGui.InputText("Display name##localname", ref _localLibraryNewName, 128);

        if (!string.IsNullOrEmpty(_localLibraryError))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudRed);
            ImGui.TextUnformatted(_localLibraryError);
            ImGui.PopStyleColor();
        }

        ImGui.Spacing();

        var addText = "Add to library";
        RightAlignButton(ImGui.GetCursorPosY(), addText);
        var canAdd = !string.IsNullOrWhiteSpace(_localLibraryNewPath) && !string.IsNullOrWhiteSpace(_localLibraryNewName);
        ImGui.BeginDisabled(!canAdd);
        if (ImGui.Button(addText))
            TryAddLocalSong();
        ImGui.EndDisabled();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (ImGui.Button("Import files...##localmulti"))
            BrowseForMultipleFiles();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Select multiple audio files at once.\nFilenames are used as display names.\nYou can rename them individually afterwards.");
        ImGui.SameLine();
        if (ImGui.Button("Import folder...##localfolder"))
            BrowseForFolder();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Select a folder to scan for audio files.\nAll MP3 and WAV files found inside (including subfolders) are imported.\nFilenames are used as display names.");

        if (!string.IsNullOrEmpty(_localLibraryImportStatus))
        {
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.HealerGreen);
            ImGui.TextUnformatted(_localLibraryImportStatus);
            ImGui.PopStyleColor();
        }

        ImGui.Spacing();
    }

    private void DrawLocalLibraryList()
    {
        if (Configuration.Instance.LocalSongs.Count == 0)
        {
            ImGui.Spacing();
            ImGui.TextUnformatted("No local songs added yet.");
            return;
        }

        foreach (var (id, localSong) in Configuration.Instance.LocalSongs)
        {
            ImGui.Spacing();

            var isPlaying = BGMManager.CurrentAudibleSong == id;
            if (isPlaying)
            {
                ImGui.PushFont(UiBuilder.IconFont);
                ImGui.Text(FontAwesomeIcon.Play.ToIconString());
                ImGui.PopFont();
                ImGui.SameLine();
            }

            ImGui.TextUnformatted(localSong.Name);
            ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudGrey);
            ImGui.TextUnformatted(localSong.FilePath);
            ImGui.TextUnformatted($"Duration: {localSong.Duration:mm\\:ss}");
            ImGui.PopStyleColor();

            var playText = "Play";
            var addToText = "Add to...";
            var deleteText = "Delete";
            RightAlignButtons(ImGui.GetCursorPosY(), new[] { playText, addToText, deleteText });

            if (ImGui.Button($"{playText}##{id}"))
                BGMManager.Play(id);
            ImGui.SameLine();
            if (ImGui.Button($"{addToText}##{id}"))
                ImGui.OpenPopup($"localaddto##{id}");
            ImGui.SameLine();
            if (ImGui.Button($"{deleteText}##{id}"))
                _localLibraryRemovalList.Add(id);

            if (ImGui.BeginPopup($"localaddto##{id}"))
            {
                foreach (var (pName, playlist) in Configuration.Instance.Playlists)
                    if (ImGui.MenuItem(pName))
                        playlist.AddSong(id);
                ImGui.Separator();
                if (ImGui.MenuItem("New playlist..."))
                    Orchestrion.UI.Windows.NewPlaylistModal.Instance.Show(new List<int> { id });
                ImGui.EndPopup();
            }

            ImGui.Separator();
        }

        if (_localLibraryRemovalList.Count > 0)
        {
            foreach (var id in _localLibraryRemovalList)
                Configuration.Instance.RemoveLocalSong(id);
            _localLibraryRemovalList.Clear();
        }
    }

    private void TryAddLocalSong()
    {
        _localLibraryError = string.Empty;
        var path = _localLibraryNewPath.Trim();
        var name = _localLibraryNewName.Trim();

        if (!File.Exists(path))
        {
            _localLibraryError = "File not found.";
            return;
        }

        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext != ".mp3" && ext != ".wav")
        {
            _localLibraryError = "Only MP3 and WAV files are supported.";
            return;
        }

        TimeSpan duration;
        try
        {
            duration = LocalAudioPlayer.ReadDuration(path);
        }
        catch (Exception ex)
        {
            _localLibraryError = $"Could not read file: {ex.Message}";
            DalamudApi.PluginLog.Warning(ex, $"[LocalLibrary] Failed to read duration for '{path}'");
            return;
        }

        Configuration.Instance.AddLocalSong(name, path, duration);
        _localLibraryNewPath = string.Empty;
        _localLibraryNewName = string.Empty;
    }

    private void BrowseForLocalFile()
    {
        _fileDialogManager.OpenFileDialog(
            "Select audio file",
            "Audio files{.mp3,.wav}",
            (success, path) =>
            {
                if (!success || string.IsNullOrEmpty(path)) return;
                _localLibraryNewPath = path;
                if (string.IsNullOrWhiteSpace(_localLibraryNewName))
                    _localLibraryNewName = Path.GetFileNameWithoutExtension(path);
            });
    }

    private void BrowseForMultipleFiles()
    {
        _fileDialogManager.OpenFileDialog(
            "Select audio files",
            "Audio files{.mp3,.wav}",
            (success, paths) =>
            {
                if (!success || paths == null || paths.Count == 0) return;
                _localLibraryImportStatus = string.Empty;
                var added = BulkAddFiles(paths);
                _localLibraryImportStatus = $"Imported {added} song(s).";
            },
            selectionCountMax: 0);
    }

    private void BrowseForFolder()
    {
        _fileDialogManager.OpenFolderDialog(
            "Select folder to import",
            (success, folder) =>
            {
                if (!success || string.IsNullOrEmpty(folder)) return;
                _localLibraryImportStatus = string.Empty;
                var files = Directory
                    .EnumerateFiles(folder, "*.*", SearchOption.AllDirectories)
                    .Where(f => { var ext = Path.GetExtension(f).ToLowerInvariant(); return ext == ".mp3" || ext == ".wav"; })
                    .OrderBy(f => f)
                    .ToList();
                var added = BulkAddFiles(files);
                _localLibraryImportStatus = $"Imported {added} song(s).";
            });
    }

    private int BulkAddFiles(List<string> paths)
    {
        var added = 0;
        foreach (var path in paths)
        {
            if (!File.Exists(path)) continue;
            try
            {
                var duration = LocalAudioPlayer.ReadDuration(path);
                var name = Path.GetFileNameWithoutExtension(path);
                Configuration.Instance.AddLocalSong(name, path, duration);
                added++;
            }
            catch (Exception ex)
            {
                DalamudApi.PluginLog.Warning(ex, $"[LocalLibrary] Skipping '{path}': {ex.Message}");
            }
        }
        return added;
    }
}
