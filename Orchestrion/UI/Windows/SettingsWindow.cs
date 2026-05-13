using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CheapLoc;
using Dalamud.Game.Text;
using Dalamud.Interface.Colors;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Orchestrion.BGMSystem;
using Orchestrion.Persistence;

namespace Orchestrion.UI.Windows;

public class SettingsWindow : Window
{
    private bool _deleteOriginalsOnMigrate = false;
    private Configuration.OrchestrionImportPreview? _importPreview;
    private bool _importPreviewLoaded = false;
    private bool _importEditMode = false;
    private bool _importSettings = true;
    private bool _importReplacements = true;
    private bool _importPlaylists = true;
    private string? _importSuccessMessage = null;

	public SettingsWindow() : base("Orchestrion Settings###orchsettings", ImGuiWindowFlags.NoCollapse)
    {
        SizeCondition = ImGuiCond.FirstUseEver;
    }
    
    public override void PreDraw()
    {
        Size = ImGuiHelpers.ScaledVector2(720, 520);
    }

    private static void Checkbox(string text, Func<bool> get, Action<bool> set, Action<bool> onChange = null)
    {
        var value = get();
        var backup = value;
        if (ImGui.Checkbox($"##orch_{text}", ref value))
        {
            set(value);
            Configuration.Instance.Save();
        }
        if (value != backup)
            onChange?.Invoke(value);
        ImGui.SameLine();
        ImGui.TextWrapped(text);
    }

    private static void DropDown(string text,
        Func<string> get,
        Action<string> set, 
        Func<string, bool> isSelected, 
        List<string> items, 
        Func<string, string> displayFunc = null,
        Action<bool> onChange = null)
    {
        var value = get();
        ImGui.SetNextItemWidth(100f * ImGuiHelpers.GlobalScale);
        using var combo = ImRaii.Combo(text, value);
        if (!combo.Success)
        {
            // ImGui.PopItemWidth();
            return;
        }
        foreach (var item in items) {
            var display = displayFunc != null ? displayFunc(item) : item;
            if (ImGui.Selectable(display, isSelected(item)))
            {
                set(item);
                Configuration.Instance.Save();
            }
        }
        if (get() != value)
            onChange?.Invoke(true);
        // ImGui.PopItemWidth();
    }
    
    public override void Draw()
	{
        using (OrchestrionPlugin.LargeFont.Push())
        {
            ImGui.Text(Loc.Localize("GeneralSettings", "General Settings"));    
        }
        
        ImGui.PushItemWidth(500f);
        
        Checkbox(Loc.Localize("ShowSongTitleBar",
            "Show current song in player title bar"),
            () => Configuration.Instance.ShowSongInTitleBar,
            b => Configuration.Instance.ShowSongInTitleBar = b);
        
        Checkbox(Loc.Localize("ShowNowPlayingChat",
            "Show \"Now playing\" messages in game chat when the current song changes"), 
            () => Configuration.Instance.ShowSongInChat,
            b => Configuration.Instance.ShowSongInChat = b);

        Checkbox(Loc.Localize("ShowSongServerInfo",
            "Show current song in the \"server info\" UI element in-game"),
            () => Configuration.Instance.ShowSongInNative, 
            b => Configuration.Instance.ShowSongInNative = b);

        if (!Configuration.Instance.ShowSongInNative)
            ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.5f);
        
        Checkbox(Loc.Localize("ShowSongIdServerInfo", 
            "Show song ID in the \"server info\" UI element in-game"), 
            () => Configuration.Instance.ShowIdInNative, 
            b => Configuration.Instance.ShowIdInNative = b); 
        
        if (!Configuration.Instance.ShowSongInNative)
            ImGui.PopStyleVar();
        
        Checkbox(Loc.Localize("HandleSpecialModes", 
            "Handle special \"in-combat\" and mount movement BGM modes"), 
            () => Configuration.Instance.HandleSpecialModes, 
            b => Configuration.Instance.HandleSpecialModes = b);
        
        Checkbox(Loc.Localize("DisableTooltips",
                "Disable tooltips throughout the plugin (Song List, Server Info)"),
            () => Configuration.Instance.DisableTooltips,
            b => Configuration.Instance.DisableTooltips = b);

        Checkbox("Stop all BGM playback when entering a cutscene",
            () => Configuration.Instance.DisableInCutscenes,
            b => Configuration.Instance.DisableInCutscenes = b);

        Checkbox("Stop song replacements when entering a cutscene (lets natural BGM play through)",
            () => Configuration.Instance.DisableReplacementsInCutscenes,
            b => Configuration.Instance.DisableReplacementsInCutscenes = b);

        Checkbox("Suppress the in-game furnishing notification when an estate orchestrion changes track",
            () => Configuration.Instance.DisableFurnishingMessages,
            b => Configuration.Instance.DisableFurnishingMessages = b);
        
        if (!BGMAddressResolver.StreamingEnabled)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudRed);
            ImGui.TextWrapped(Loc.Localize("AudioStreamingDisabledWarning" , 
                "Audio streaming is disabled. This may be due to Sound Filter or a third-party plugin. The above setting may not work as " +
                              "expected and you may encounter other audio issues such as popping or tracks not swapping channels. This is not" +
                              " related to the Orchestrion Plugin."));
            ImGui.PopStyleColor();
        }
        
        Checkbox(Loc.Localize("UseDalamudChannelSetting", 
                "Use the chat channel selected in Dalamud's settings for Orchestrion's chat messages (default)"),
            () => Configuration.Instance.ChatChannelMatchDalamud, 
            b => Configuration.Instance.ChatChannelMatchDalamud = b,
            b =>
            {
                if (!b) return;
                Configuration.Instance.ChatType = DalamudApi.PluginInterface.GeneralChatType;
                Configuration.Instance.Save();
            });

        ImGui.BeginDisabled(Configuration.Instance.ChatChannelMatchDalamud);
        ImGui.Indent(30f * ImGuiHelpers.GlobalScale);
        DropDown(Loc.Localize("ChatChannelSetting", "Chat channel used for Orchestrion messages"), 
            () => Configuration.Instance.ChatType.ToString(), 
            s => Configuration.Instance.ChatType = Enum.Parse<XivChatType>(s), 
            s => s == Configuration.Instance.ChatType.ToString(), 
            Enum.GetValues<XivChatType>().Select(c => c.ToString()).ToList());
        ImGui.Indent(-1 * 30f * ImGuiHelpers.GlobalScale);
        ImGui.EndDisabled();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        using (OrchestrionPlugin.LargeFont.Push())
        {
            ImGui.Text(Loc.Localize("LocSettings", "Localization Settings"));
        }
        
        Checkbox(Loc.Localize("UseDalamudLanguageSetting", 
                "Use the language selected in Dalamud's settings for the Orchestrion Plugin's UI"),
            () => Configuration.Instance.UserInterfaceLanguageMatchDalamud, 
            b => Configuration.Instance.UserInterfaceLanguageMatchDalamud = b,
            b =>
            {
                if (!b) return;
                Configuration.Instance.UserInterfaceLanguageCode = DalamudApi.PluginInterface.UiLanguage;
                Configuration.Instance.Save();
                OrchestrionPlugin.LanguageChanged(Configuration.Instance.UserInterfaceLanguageCode);
            });

        ImGui.BeginDisabled(Configuration.Instance.UserInterfaceLanguageMatchDalamud);
        ImGui.Indent(30f * ImGuiHelpers.GlobalScale);
        DropDown(Loc.Localize("UILanguageSetting",
                "Language used for the Orchestrion Plugin's UI"),
            () => Util.LangCodeToLanguage(Configuration.Instance.UserInterfaceLanguageCode),
            s => Configuration.Instance.UserInterfaceLanguageCode = s,
            s => s == Configuration.Instance.UserInterfaceLanguageCode,
            Util.AvailableLanguages,
            Util.LangCodeToLanguage,
            _ =>
            {
                OrchestrionPlugin.LanguageChanged(Configuration.Instance.UserInterfaceLanguageCode);
            });
        ImGui.Indent(-1 * 30f * ImGuiHelpers.GlobalScale);
        ImGui.EndDisabled();
        
        Checkbox(Loc.Localize("ShowAltLangTitles", 
                "Show alternate language song titles in tooltips"), 
            () => Configuration.Instance.ShowAltLangTitles, 
            b => Configuration.Instance.ShowAltLangTitles = b);
        
        ImGui.BeginDisabled(!Configuration.Instance.ShowAltLangTitles);
        ImGui.Indent(30f * ImGuiHelpers.GlobalScale);
        DropDown(Loc.Localize("AltLangLanguageSetting", 
            "Alternate language for song titles in tooltips"), 
            () => Util.LangCodeToLanguage(Configuration.Instance.AltTitleLanguageCode), 
            s => Configuration.Instance.AltTitleLanguageCode = s, 
            s => s == Configuration.Instance.AltTitleLanguageCode, 
            Util.AvailableTitleLanguages,
            Util.LangCodeToLanguage);
        ImGui.Indent(-1 * 30f * ImGuiHelpers.GlobalScale);
        ImGui.EndDisabled();
        
        DropDown(Loc.Localize("ServerInfoLanguageSetting", 
                "Language used for song titles in the \"server info\" UI element in-game"), 
            () => Util.LangCodeToLanguage(Configuration.Instance.ServerInfoLanguageCode), 
            s => Configuration.Instance.ServerInfoLanguageCode = s, 
            s => s == Configuration.Instance.ServerInfoLanguageCode, 
            Util.AvailableTitleLanguages,
            Util.LangCodeToLanguage);
        
        DropDown(Loc.Localize("ChatMessageLanguageSetting", 
                "Language used for song titles in Orchestrion chat messages in-game"), 
            () => Util.LangCodeToLanguage(Configuration.Instance.ChatLanguageCode), 
            s => Configuration.Instance.ChatLanguageCode = s, 
            s => s == Configuration.Instance.ChatLanguageCode, 
            Util.AvailableTitleLanguages,
            Util.LangCodeToLanguage);
        
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        using (OrchestrionPlugin.LargeFont.Push())
        {
            ImGui.Text("Local Library Settings");
        }

        Checkbox("Copy imported audio files into the plugin's storage folder (recommended)\nKeeps songs working even if the original file is moved or deleted.",
            () => Configuration.Instance.CopyLocalSongsToStorage,
            b => Configuration.Instance.CopyLocalSongsToStorage = b);

        Checkbox("Show local songs at the top of the All Songs list",
            () => Configuration.Instance.LocalSongsAtTop,
            b => Configuration.Instance.LocalSongsAtTop = b);

        if (!Configuration.Instance.CopyLocalSongsToStorage)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudOrange);
            ImGui.TextWrapped("Warning: songs will only play from their original file location. If a file is moved, renamed, or deleted, it will no longer work in the Local Library.");
            ImGui.PopStyleColor();
        }

        var songsOutside = Configuration.Instance.CountSongsOutsideStorage();
        if (songsOutside > 0)
        {
            ImGui.Spacing();
            ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudGrey);
            ImGui.TextUnformatted($"{songsOutside} song(s) are stored outside the plugin folder.");
            ImGui.PopStyleColor();

            var del = _deleteOriginalsOnMigrate;
            if (ImGui.Checkbox("##orchdeloriginals", ref del))
                _deleteOriginalsOnMigrate = del;
            ImGui.SameLine();
            ImGui.TextUnformatted("Remove original files after copying");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("If enabled, the original file will be deleted from its source location\nonce it has been safely copied into the plugin's storage folder.");

            if (ImGui.Button("Migrate to storage##orchmigrate"))
            {
                if (_deleteOriginalsOnMigrate)
                    ImGui.OpenPopup("Confirm migration##orchmigrateconfirm");
                else
                {
                    var count = Configuration.Instance.MigrateLocalSongsToStorage(false);
                    DalamudApi.NotificationManager.AddNotification(new Notification
                    {
                        Title = "Orchestrion — Local Library",
                        Content = count > 0 ? $"Migrated {count} song(s) to plugin storage." : "All songs are already in plugin storage.",
                        Type = count > 0 ? NotificationType.Success : NotificationType.Info,
                    });
                }
            }

            if (ImGui.BeginPopupModal("Confirm migration##orchmigrateconfirm", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse))
            {
                ImGui.TextUnformatted($"This will copy {songsOutside} song(s) into the plugin storage folder");
                ImGui.TextUnformatted("and permanently delete the originals from their source locations.");
                ImGui.Spacing();
                ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudOrange);
                ImGui.TextUnformatted("This cannot be undone.");
                ImGui.PopStyleColor();
                ImGui.Spacing();
                if (ImGui.Button("Yes, migrate and delete originals"))
                {
                    var count = Configuration.Instance.MigrateLocalSongsToStorage(true);
                    DalamudApi.NotificationManager.AddNotification(new Notification
                    {
                        Title = "Orchestrion — Local Library",
                        Content = count > 0 ? $"Migrated {count} song(s), originals deleted." : "Nothing to migrate.",
                        Type = count > 0 ? NotificationType.Success : NotificationType.Info,
                    });
                    ImGui.CloseCurrentPopup();
                }
                ImGui.SameLine();
                if (ImGui.Button("Cancel"))
                    ImGui.CloseCurrentPopup();
                ImGui.EndPopup();
            }
        }

        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudGrey);
        ImGui.TextUnformatted($"Storage folder: {Configuration.LocalSongsStorageDir}");
        ImGui.PopStyleColor();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Audio files copied by the plugin are stored here.\nDeleting a song from the Local Library also removes its copy from this folder.");
        ImGui.SameLine();
        if (ImGui.SmallButton("Open##openstorage"))
        {
            var dir = Configuration.LocalSongsStorageDir;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Lazy-load the import preview once
        if (!_importPreviewLoaded)
        {
            _importPreview = Configuration.OrchestrionConfigExists()
                ? Configuration.PeekOrchestrionImport()
                : null;
            _importPreviewLoaded = true;
        }

        var orchConflict = DalamudApi.PluginInterface.InstalledPlugins
            .Any(p => (p.InternalName == "orchestrion" || p.InternalName == "orchestrion2") && p.IsLoaded);

        // Show the section only when there is something actionable:
        // a conflict that needs resolving, or data that hasn't been imported yet
        var hasImportableData = _importPreview is { ReplacementCount: > 0 } ||
                                (_importPreview?.PlaylistNames.Count ?? 0) > 0;
        var showImportSection = _importSuccessMessage != null ||
                                (!Configuration.Instance.HasCompletedImport && (orchConflict || hasImportableData));

        if (showImportSection)
        {
            using (OrchestrionPlugin.LargeFont.Push())
                ImGui.Text("Import from Orchestrion");

            if (_importSuccessMessage != null)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.HealerGreen);
                ImGui.TextUnformatted(_importSuccessMessage);
                ImGui.PopStyleColor();
            }
            else if (orchConflict)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudOrange);
                ImGui.TextWrapped(
                    "An older version of Orchestrion is currently active. " +
                    "Running both plugins at the same time will cause audio conflicts. " +
                    "Please disable it in /xlplugins first, then return here to import your settings.");
                ImGui.PopStyleColor();
            }
            else
            {
                ImGui.TextWrapped(
                    "A config file from a previous Orchestrion installation was found. " +
                    "Import your song replacements, playlists, and general settings into Orchestrion Aria.");

                ImGui.Spacing();

                ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudGrey);
                ImGui.TextUnformatted("What will be imported:");
                ImGui.PopStyleColor();

                ImGui.Indent(16f * ImGuiHelpers.GlobalScale);

                ImGui.Bullet();
                ImGui.SameLine();
                ImGui.TextUnformatted($"{_importPreview!.ReplacementCount} song replacement(s)");

                if (_importPreview.PlaylistNames.Count > 0)
                {
                    ImGui.Bullet();
                    ImGui.SameLine();
                    ImGui.TextUnformatted(
                        $"{_importPreview.PlaylistNames.Count} playlist(s): " +
                        string.Join(", ", _importPreview.PlaylistNames));
                }

                ImGui.Bullet();
                ImGui.SameLine();
                ImGui.TextUnformatted("General settings (chat notifications, language, display preferences)");

                ImGui.Unindent(16f * ImGuiHelpers.GlobalScale);
                ImGui.Spacing();

                if (ImGui.Button("Import settings & song replacements##orchimport"))
                {
                    // Reset to defaults every time the popup opens
                    _importEditMode     = false;
                    _importSettings     = true;
                    _importReplacements = _importPreview!.ReplacementCount > 0;
                    _importPlaylists    = _importPreview.PlaylistNames.Count > 0;
                    ImGui.OpenPopup("Confirm import##orchimportconfirm");
                }

                if (ImGui.BeginPopupModal("Confirm import##orchimportconfirm",
                        ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse))
                {
                    ImGui.TextUnformatted("The following will be imported from Orchestrion:");
                    ImGui.Spacing();

                    ImGui.Indent(12f * ImGuiHelpers.GlobalScale);

                    if (_importEditMode)
                    {
                        // Editable checkboxes
                        var s = _importSettings;
                        if (ImGui.Checkbox("##importSettings", ref s)) _importSettings = s;
                        ImGui.SameLine();
                        ImGui.TextUnformatted("General settings (chat, language, display preferences)");

                        if (_importPreview!.ReplacementCount > 0)
                        {
                            var r = _importReplacements;
                            if (ImGui.Checkbox("##importReplacements", ref r)) _importReplacements = r;
                            ImGui.SameLine();
                            ImGui.TextUnformatted($"{_importPreview.ReplacementCount} song replacement(s)");
                        }

                        if (_importPreview.PlaylistNames.Count > 0)
                        {
                            var p = _importPlaylists;
                            if (ImGui.Checkbox("##importPlaylists", ref p)) _importPlaylists = p;
                            ImGui.SameLine();
                            ImGui.TextUnformatted(
                                $"{_importPreview.PlaylistNames.Count} playlist(s): " +
                                string.Join(", ", _importPreview.PlaylistNames));
                        }

                        ImGui.Unindent(12f * ImGuiHelpers.GlobalScale);
                        ImGui.Spacing();

                        if (ImGui.Button("Done##importdone"))
                            _importEditMode = false;
                    }
                    else
                    {
                        // Read-only summary of what is selected
                        if (_importSettings)
                        {
                            ImGui.Bullet(); ImGui.SameLine();
                            ImGui.TextUnformatted("General settings");
                        }
                        if (_importReplacements && _importPreview!.ReplacementCount > 0)
                        {
                            ImGui.Bullet(); ImGui.SameLine();
                            ImGui.TextUnformatted($"{_importPreview.ReplacementCount} song replacement(s)");
                        }
                        if (_importPlaylists && _importPreview!.PlaylistNames.Count > 0)
                        {
                            ImGui.Bullet(); ImGui.SameLine();
                            ImGui.TextUnformatted(
                                $"{_importPreview.PlaylistNames.Count} playlist(s): " +
                                string.Join(", ", _importPreview.PlaylistNames));
                        }

                        ImGui.Unindent(12f * ImGuiHelpers.GlobalScale);
                        ImGui.Spacing();

                        if (ImGui.SmallButton("Edit##importedit"))
                            _importEditMode = true;
                    }

                    ImGui.Spacing();
                    ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudOrange);
                    ImGui.TextUnformatted("Existing Orchestrion Aria settings will be overwritten.");
                    ImGui.TextUnformatted("This cannot be undone.");
                    ImGui.PopStyleColor();
                    ImGui.Spacing();

                    var nothingSelected = !_importSettings && !_importReplacements && !_importPlaylists;
                    ImGui.BeginDisabled(_importEditMode || nothingSelected);
                    if (ImGui.Button("Import##orchimportyes"))
                    {
                        var count = Configuration.Instance.ImportFromOrchestrion(
                            _importSettings, _importReplacements, _importPlaylists);
                        if (count >= 0)
                        {
                            var parts = new List<string>();
                            if (_importSettings)     parts.Add("general settings");
                            if (_importReplacements) parts.Add($"{count} song replacement(s)");
                            if (_importPlaylists)    parts.Add("playlists");
                            var summary = string.Join(", ", parts);
                            _importSuccessMessage = $"Import complete: {summary}.";
                            DalamudApi.NotificationManager.AddNotification(new Notification
                            {
                                Title   = "Orchestrion Aria — Import",
                                Content = _importSuccessMessage,
                                Type    = NotificationType.Success,
                            });
                            Configuration.Instance.HasCompletedImport = true;
                            Configuration.Instance.Save();
                        }
                        else
                        {
                            DalamudApi.NotificationManager.AddNotification(new Notification
                            {
                                Title   = "Orchestrion Aria — Import",
                                Content = "Import failed. Check the Dalamud log for details.",
                                Type    = NotificationType.Error,
                            });
                        }
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.EndDisabled();

                    ImGui.SameLine();
                    if (ImGui.Button("Cancel##orchimportcancel"))
                    {
                        _importEditMode = false;
                        ImGui.CloseCurrentPopup();
                    }

                    ImGui.EndPopup();
                }
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
        }

        using (OrchestrionPlugin.LargeFont.Push())
        {
            ImGui.Text(Loc.Localize("MiniPlayerSettings", "Mini Player Settings"));
        }
        
        Checkbox(Loc.Localize("ShowMiniPlayer", "Show mini player"),
            () => Configuration.Instance.ShowMiniPlayer, 
            b => Configuration.Instance.ShowMiniPlayer = b);

        Checkbox(Loc.Localize("LockMiniPlayer", "Lock mini player"), 
            () => Configuration.Instance.MiniPlayerLock, 
            b => Configuration.Instance.MiniPlayerLock = b);
        
        var miniPlayerOpacity = Configuration.Instance.MiniPlayerOpacity;
        ImGui.PushItemWidth(200f);
        if (ImGui.SliderFloat($"##orch_MiniPlayerOpacity", ref miniPlayerOpacity, 0.01f, 1.0f))
        {
            Configuration.Instance.MiniPlayerOpacity = miniPlayerOpacity;
            Configuration.Instance.Save();
        }
        ImGui.SameLine();
        ImGui.TextWrapped(Loc.Localize("MiniPlayerOpacity", "Mini player opacity"));
        ImGui.PopItemWidth();
        ImGui.PopItemWidth();
    }
}