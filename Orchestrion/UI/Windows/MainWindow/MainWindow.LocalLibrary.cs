using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Bindings.ImGui;
using Orchestrion.Audio;
using Orchestrion.Persistence;
using Orchestrion.Types;

namespace Orchestrion.UI.Windows.MainWindow;

public partial class MainWindow
{
    // Single-file import state
    private string _localLibraryNewPath  = string.Empty;
    private string _localLibraryNewName  = string.Empty;
    private string _localLibraryError    = string.Empty;
    private bool   _deleteOriginalOnImport = false;

    // Bulk import state
    private string       _localLibraryImportStatus        = string.Empty;
    private List<string> _pendingBulkImportPaths          = null;
    private string       _pendingBulkSourceDesc           = string.Empty;
    private bool         _pendingBulkDeleteOriginals      = false;

    // List state
    private readonly List<int> _localLibraryRemovalList = new();
    private int    _renamingId     = -1;
    private string _renameBuffer   = string.Empty;

    private readonly FileDialogManager _fileDialogManager = new();

    // ── Tab root ────────────────────────────────────────────────────────────────

    private void DrawLocalLibraryTab()
    {
        _fileDialogManager.Draw();

        if (!Configuration.Instance.HasShownLocalLibraryIntro)
        {
            Configuration.Instance.HasShownLocalLibraryIntro = true;
            Configuration.Instance.Save();
            DalamudApi.NotificationManager.AddNotification(new Notification
            {
                Title   = "Orchestrion — Local Library",
                Content = "Import MP3 or WAV files to use as custom BGM replacements in-game.\n" +
                          "Files are copied into plugin storage by default — your originals are never deleted.\n" +
                          "You can adjust this behaviour under Settings → Local Library Settings.",
                Type    = NotificationType.Info,
            });
        }

        ImGui.BeginChild("##locallibraryroot");
        DrawLocalLibraryAddSection();
        ImGui.Separator();
        DrawLocalLibraryList();
        ImGui.EndChild();
    }

    // ── Add section ─────────────────────────────────────────────────────────────

    private void DrawLocalLibraryAddSection()
    {
        // ── Single file ──────────────────────────────────────────────────────
        ImGui.Spacing();
        ImGui.TextUnformatted("Single file");
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(
            "The Local Library lets you import audio files from your PC and play them as in-game BGM.\n\n" +
            "Imported files are copied into plugin storage by default, so they keep working\n" +
            "even if the original file is moved or deleted.\n\n" +
            "You can rename or remove songs using the list below, and add them to playlists.\n" +
            "Visit Settings → Local Library Settings to adjust the storage behaviour.");

        ImGui.Spacing();

        var width = ImGui.GetWindowWidth() * 0.60f;

        ImGui.SetNextItemWidth(width);
        ImGui.InputText("File path##localpath", ref _localLibraryNewPath, 512);
        ImGui.SameLine();
        if (ImGui.Button("Browse...##localbrowse"))
            BrowseForLocalFile();

        ImGui.SetNextItemWidth(width);
        ImGui.InputText("Display name##localname", ref _localLibraryNewName, 128);

        if (!string.IsNullOrEmpty(_localLibraryError))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudRed);
            ImGui.TextUnformatted(_localLibraryError);
            ImGui.PopStyleColor();
        }

        // Delete-original checkbox — only shown once a path is filled in
        if (!string.IsNullOrWhiteSpace(_localLibraryNewPath))
        {
            ImGui.Spacing();
            var delSingle = _deleteOriginalOnImport;
            if (ImGui.Checkbox("##orchdelsingle", ref delSingle))
                _deleteOriginalOnImport = delSingle;
            ImGui.SameLine();
            ImGui.TextUnformatted("Delete original file after adding");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("The original file will be permanently deleted after being safely\ncopied into the plugin's storage folder.");
        }

        ImGui.Spacing();

        // Add button — right-aligned
        var addText = "Add to library";
        RightAlignButton(ImGui.GetCursorPosY(), addText);
        var canAdd = !string.IsNullOrWhiteSpace(_localLibraryNewPath) && !string.IsNullOrWhiteSpace(_localLibraryNewName);
        ImGui.BeginDisabled(!canAdd);
        if (ImGui.Button(addText))
        {
            if (_deleteOriginalOnImport)
                ImGui.OpenPopup("##orchsingledelconfirm");
            else
                TryAddLocalSong(deleteOriginal: false);
        }
        ImGui.EndDisabled();

        // Single-file delete confirmation modal
        if (ImGui.BeginPopupModal("##orchsingledelconfirm", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoTitleBar))
        {
            ImGui.TextUnformatted("This will permanently delete the original file:");
            ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudGrey);
            ImGui.TextUnformatted(_localLibraryNewPath);
            ImGui.PopStyleColor();
            ImGui.Spacing();
            ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudOrange);
            ImGui.TextUnformatted("This cannot be undone.");
            ImGui.PopStyleColor();
            ImGui.Spacing();
            if (ImGui.Button("Yes, add and delete original"))
            {
                TryAddLocalSong(deleteOriginal: true);
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel##singledelcancel"))
                ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // ── Bulk import ──────────────────────────────────────────────────────
        ImGui.TextUnformatted("Bulk import");
        ImGui.Spacing();

        ImGui.BeginDisabled(_pendingBulkImportPaths != null);
        if (ImGui.Button("Multiple files...##localmulti"))
            BrowseForMultipleFiles();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Select multiple audio files at once.\nFilenames are used as display names — you can rename them individually afterwards.");
        ImGui.SameLine();
        if (ImGui.Button("Folder...##localfolder"))
            BrowseForFolder();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Select a folder to scan for audio files.\nAll MP3 and WAV files found inside (including subfolders) are imported.\nFilenames are used as display names.");
        ImGui.EndDisabled();

        // Inline preview — shown after files are selected, replaces the buttons area
        if (_pendingBulkImportPaths != null)
        {
            ImGui.Spacing();

            // Summary line with hover showing first 10 filenames
            var previewLines = _pendingBulkImportPaths
                .Take(10)
                .Select(Path.GetFileName)
                .ToList();
            if (_pendingBulkImportPaths.Count > 10)
                previewLines.Add($"... and {_pendingBulkImportPaths.Count - 10} more");
            var tooltipText = string.Join("\n", previewLines);

            ImGui.TextUnformatted($"Ready to import {_pendingBulkImportPaths.Count} file(s)");
            ImGui.SameLine();
            ImGuiComponents.HelpMarker(tooltipText);

            ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudGrey);
            ImGui.TextUnformatted($"From: {_pendingBulkSourceDesc}");
            ImGui.PopStyleColor();

            ImGui.Spacing();

            // Delete toggle — only visible now that files are chosen
            var del = _pendingBulkDeleteOriginals;
            if (ImGui.Checkbox("##orchdelbulkpending", ref del))
                _pendingBulkDeleteOriginals = del;
            ImGui.SameLine();
            ImGui.TextUnformatted("Delete original files after importing");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Original files will be permanently deleted from their source\nlocations after being safely copied into plugin storage.");

            if (_pendingBulkDeleteOriginals)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudOrange);
                ImGui.TextUnformatted("Original files will be permanently deleted. This cannot be undone.");
                ImGui.PopStyleColor();
            }

            ImGui.Spacing();

            var importLabel = _pendingBulkDeleteOriginals ? "Import and delete originals" : "Import";
            if (ImGui.Button(importLabel))
            {
                var paths = _pendingBulkImportPaths;
                var doDelete = _pendingBulkDeleteOriginals;
                _pendingBulkImportPaths     = null;
                _pendingBulkDeleteOriginals = false;
                var added = BulkAddFiles(paths, deleteOriginals: doDelete);
                _localLibraryImportStatus = doDelete
                    ? $"Imported {added} song(s), originals deleted."
                    : $"Imported {added} song(s).";
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel##bulkcancel"))
            {
                _pendingBulkImportPaths     = null;
                _pendingBulkDeleteOriginals = false;
            }
        }

        if (!string.IsNullOrEmpty(_localLibraryImportStatus))
        {
            ImGui.Spacing();
            ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.HealerGreen);
            ImGui.TextUnformatted(_localLibraryImportStatus);
            ImGui.PopStyleColor();
        }

        ImGui.Spacing();
    }

    // ── Song list ────────────────────────────────────────────────────────────────

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

            // Name — editable when renaming, plain text otherwise
            if (_renamingId == id)
            {
                ImGui.SetNextItemWidth(ImGui.GetWindowWidth() * 0.55f);
                if (ImGui.InputText($"##rename{id}", ref _renameBuffer, 128, ImGuiInputTextFlags.EnterReturnsTrue))
                    CommitRename(id);
                ImGui.SameLine();
                if (ImGui.SmallButton($"Save##renameSave{id}"))
                    CommitRename(id);
                ImGui.SameLine();
                if (ImGui.SmallButton($"Cancel##renameCancel{id}"))
                    _renamingId = -1;
            }
            else
            {
                ImGui.TextUnformatted(localSong.Name);
            }

            ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudGrey);
            if (!Configuration.IsInStorage(localSong.FilePath))
                ImGui.TextUnformatted(localSong.FilePath);
            ImGui.TextUnformatted($"Duration: {localSong.Duration:mm\\:ss}");
            ImGui.PopStyleColor();

            var playText   = "Play";
            var addToText  = "Add to...";
            var renameText = "Rename";
            var deleteText = "Delete";
            RightAlignButtons(ImGui.GetCursorPosY(), new[] { playText, addToText, renameText, deleteText });

            if (ImGui.Button($"{playText}##{id}"))
                BGMManager.Play(id);
            ImGui.SameLine();
            if (ImGui.Button($"{addToText}##{id}"))
                ImGui.OpenPopup($"localaddto##{id}");
            ImGui.SameLine();
            if (ImGui.Button($"{renameText}##{id}"))
            {
                _renamingId   = id;
                _renameBuffer = localSong.Name;
            }
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

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private bool IsDuplicateName(string name) =>
        Configuration.Instance.LocalSongs.Values.Any(s =>
            s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private string GetUniqueName(string baseName)
    {
        if (!IsDuplicateName(baseName)) return baseName;
        var counter = 2;
        while (IsDuplicateName($"{baseName} ({counter})"))
            counter++;
        return $"{baseName} ({counter})";
    }

    private void CommitRename(int id)
    {
        var name = _renameBuffer.Trim();
        if (!string.IsNullOrWhiteSpace(name) && Configuration.Instance.LocalSongs.TryGetValue(id, out var song))
        {
            song.Name = name;
            Configuration.Instance.Save();
        }
        _renamingId = -1;
    }

    private void TryAddLocalSong(bool deleteOriginal = false)
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

        if (IsDuplicateName(name))
        {
            _localLibraryError = $"A song named \"{name}\" already exists. Please choose a different name.";
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

        if (deleteOriginal)
        {
            try { File.Delete(path); }
            catch (Exception ex) { DalamudApi.PluginLog.Warning(ex, $"[LocalLibrary] Could not delete original '{path}'"); }
        }

        _localLibraryNewPath       = string.Empty;
        _localLibraryNewName       = string.Empty;
        _deleteOriginalOnImport    = false;
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
                _pendingBulkImportPaths   = paths;
                _pendingBulkSourceDesc    = Path.GetDirectoryName(paths[0]) ?? string.Empty;
                _pendingBulkDeleteOriginals = false;
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
                _pendingBulkImportPaths     = files;
                _pendingBulkSourceDesc      = folder;
                _pendingBulkDeleteOriginals = false;
            });
    }

    private int BulkAddFiles(List<string> paths, bool deleteOriginals = false)
    {
        var added = 0;
        foreach (var path in paths)
        {
            if (!File.Exists(path)) continue;
            try
            {
                var duration = LocalAudioPlayer.ReadDuration(path);
                var name     = GetUniqueName(Path.GetFileNameWithoutExtension(path));
                Configuration.Instance.AddLocalSong(name, path, duration);
                if (deleteOriginals)
                {
                    try { File.Delete(path); }
                    catch (Exception ex) { DalamudApi.PluginLog.Warning(ex, $"[LocalLibrary] Could not delete original '{path}'"); }
                }
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
