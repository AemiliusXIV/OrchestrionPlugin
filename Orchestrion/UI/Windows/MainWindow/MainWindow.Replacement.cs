using System.Collections.Generic;
using System.Numerics;
using CheapLoc;
using Dalamud.Bindings.ImGui;
using Orchestrion.Audio;
using Orchestrion.Persistence;
using Orchestrion.Types;
using Orchestrion.UI.Components;

namespace Orchestrion.UI.Windows.MainWindow;

public partial class MainWindow
{
    private SongReplacementEntry _tmpReplacement;
    private readonly List<int> _removalList = new();
    
	private void DrawReplacementsTab()
    {
        ImGui.BeginChild("##replacementlist");
        DrawCurrentReplacement();
        DrawReplacementList();
        ImGui.EndChild();
    }

    private void DrawReplacementList()
    {
        foreach (var replacement in Configuration.Instance.SongReplacements.Values)
        {
            if (!SongList.Instance.TryGetSong(replacement.TargetSongId, out var targetSong)) continue;

            var isLocalReplacement = LocalSong.IsLocalId(replacement.ReplacementId);
            Song replacementSong = default;
            if (!isLocalReplacement && !SongList.Instance.TryGetSong(replacement.ReplacementId, out replacementSong) && replacement.ReplacementId != SongReplacementEntry.NoChangeId) continue;
            if (!Util.SearchMatches(_searchText, targetSong) && !Util.SearchMatches(_searchText, replacementSong)) continue;

            ImGui.Spacing();

            var targetText = $"{replacement.TargetSongId} - {targetSong.Name}";
            string replText;
            if (replacement.ReplacementId == SongReplacementEntry.NoChangeId)
                replText = _noChange;
            else if (isLocalReplacement)
                replText = Configuration.Instance.LocalSongs.TryGetValue(replacement.ReplacementId, out var ls) ? $"{ls.DisplayId} - [Local] {ls.Name}" : "[Local] (missing)";
            else
                replText = $"{replacement.ReplacementId} - {replacementSong.Name}";

            ImGui.TextWrapped($"{targetText}");
            if (ImGui.IsItemHovered())
                BgmTooltip.DrawBgmTooltip(targetSong);

            ImGui.Text(Loc.Localize("ReplaceWith", "will be replaced with"));
            ImGui.TextWrapped($"{replText}");
            if (ImGui.IsItemHovered() && replacement.ReplacementId != SongReplacementEntry.NoChangeId && !isLocalReplacement)
                BgmTooltip.DrawBgmTooltip(SongList.Instance.GetSong(replacement.ReplacementId));

            // Buttons in bottom right of area
            var editText = Loc.Localize("Edit", "Edit");
            var deleteText = Loc.Localize("Delete", "Delete");
            RightAlignButtons(ImGui.GetCursorPosY(), new[] {editText, deleteText});
            if (ImGui.Button($"{editText}##{replacement.TargetSongId}"))
            {
                _removalList.Add(replacement.TargetSongId);
                _tmpReplacement.TargetSongId = replacement.TargetSongId;
                _tmpReplacement.ReplacementId = replacement.ReplacementId;
            }
            ImGui.SameLine();
            if (ImGui.Button($"{deleteText}##{replacement.TargetSongId}"))
                _removalList.Add(replacement.TargetSongId);

            ImGui.Separator();
        }

        if (_removalList.Count > 0)
        {
            foreach (var toRemove in _removalList)
                Configuration.Instance.SongReplacements.Remove(toRemove);
            _removalList.Clear();
            Configuration.Instance.Save();
        }
    }
    
    private void DrawCurrentReplacement()
    {
        ImGui.Spacing();

        var targetText = $"{SongList.Instance.GetSong(_tmpReplacement.TargetSongId).Id} - {SongList.Instance.GetSong(_tmpReplacement.TargetSongId).Name}";
        string replacementText;
        if (_tmpReplacement.ReplacementId == SongReplacementEntry.NoChangeId)
            replacementText = _noChange;
        else if (LocalSong.IsLocalId(_tmpReplacement.ReplacementId))
            replacementText = Configuration.Instance.LocalSongs.TryGetValue(_tmpReplacement.ReplacementId, out var selectedLocal) ? $"{selectedLocal.DisplayId} - [Local] {selectedLocal.Name}" : "[Local] (missing)";
        else
            replacementText = $"{SongList.Instance.GetSong(_tmpReplacement.ReplacementId).Id} - {SongList.Instance.GetSong(_tmpReplacement.ReplacementId).Name}";

        // This fixes the ultra-wide combo boxes, I guess
        var width = ImGui.GetWindowWidth() * 0.60f;

        if (ImGui.BeginCombo(Loc.Localize("TargetSong", "Target Song"), targetText))
        {
            foreach (var song in SongList.Instance.GetSongs().Values)
            {
                if (!Util.SearchMatches(_searchText, song)) continue;
                if (Configuration.Instance.SongReplacements.ContainsKey(song.Id)) continue;
                var tmpText = $"{song.Id} - {song.Name}";
                var tmpTextSize = ImGui.CalcTextSize(tmpText);
                var isSelected = _tmpReplacement.TargetSongId == song.Id;
                if (ImGui.Selectable(tmpText, isSelected, ImGuiSelectableFlags.None, new Vector2(width, tmpTextSize.Y)))
                    _tmpReplacement.TargetSongId = song.Id;
                if (ImGui.IsItemHovered())
                    BgmTooltip.DrawBgmTooltip(song);
            }

            ImGui.EndCombo();
        }

        ImGui.Spacing();

        if (ImGui.BeginCombo(Loc.Localize("ReplacementSong", "Replacement Song"), replacementText))
        {
            if (ImGui.Selectable(MainWindow._noChange))
                _tmpReplacement.ReplacementId = SongReplacementEntry.NoChangeId;

            if (Configuration.Instance.LocalSongs.Count > 0)
            {
                ImGui.Separator();
                ImGui.TextUnformatted("── Local Library ──");
                ImGui.Separator();
                foreach (var localSong in Configuration.Instance.LocalSongs.Values)
                {
                    if (!string.IsNullOrEmpty(_searchText) &&
                        !localSong.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase)) continue;
                    var tmpText = $"{localSong.DisplayId} - [Local] {localSong.Name}";
                    var isSelected = _tmpReplacement.ReplacementId == localSong.Id;
                    if (ImGui.Selectable(tmpText, isSelected))
                        _tmpReplacement.ReplacementId = localSong.Id;
                }
                ImGui.Separator();
                ImGui.TextUnformatted("── Game Songs ──");
                ImGui.Separator();
            }

            foreach (var song in SongList.Instance.GetSongs().Values)
            {
                if (!Util.SearchMatches(_searchText, song)) continue;
                var tmpText = $"{song.Id} - {song.Name}";
                var tmpTextSize = ImGui.CalcTextSize(tmpText);
                var isSelected = _tmpReplacement.ReplacementId == song.Id;
                if (ImGui.Selectable(tmpText, isSelected, ImGuiSelectableFlags.None, new Vector2(width, tmpTextSize.Y)))
                    _tmpReplacement.ReplacementId = song.Id;
                if (ImGui.IsItemHovered())
                    BgmTooltip.DrawBgmTooltip(song);
            }

            ImGui.EndCombo();
        }

        ImGui.Spacing();
        ImGui.Spacing();

        var text = Loc.Localize("AddReplacement", "Add as song replacement");
        MainWindow.RightAlignButton(ImGui.GetCursorPosY(), text);
        if (ImGui.Button(text))
        {
            Configuration.Instance.SongReplacements.Add(_tmpReplacement.TargetSongId, _tmpReplacement);
            Configuration.Instance.Save();
            ResetReplacement();
        }

        ImGui.Separator();
    }
}