using System.Numerics;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Bindings.ImGui;
using Orchestrion.Persistence;

namespace Orchestrion.UI.Windows.MainWindow;

public partial class MainWindow
{
	private void DrawSongListTab()
	{
		var localCount = Configuration.Instance.LocalSongs.Count;
		if (localCount != _knownLocalSongCount)
		{
			_localSongList.SetListSource(BuildLocalSongListSource());
			_knownLocalSongCount = localCount;
		}

		// to keep the tab bar always visible and not have it get scrolled out
		ImGui.BeginChild("##_songList_internal", ImGuiHelpers.ScaledVector2(-1f, -25f));

		void DrawLocalSection()
		{
			if (localCount == 0) return;
			ImGui.Spacing();
			ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudGrey);
			ImGui.Separator();
			ImGui.Text("  Local Library");
			ImGui.Separator();
			ImGui.PopStyleColor();
			ImGui.Spacing();
			_localSongList.Draw();
		}

		if (Configuration.Instance.LocalSongsAtTop)
		{
			DrawLocalSection();
			if (localCount > 0)
			{
				ImGui.Spacing();
				ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudGrey);
				ImGui.Separator();
				ImGui.Text("  Game Library");
				ImGui.Separator();
				ImGui.PopStyleColor();
				ImGui.Spacing();
			}
			_mainSongList.Draw();
		}
		else
		{
			_mainSongList.Draw();
			DrawLocalSection();
		}

		ImGui.EndChild();
		DrawFooter();
	}
}