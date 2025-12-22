using Dalamud.Game;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;

namespace Orchestrion.InnSystem;

internal class OrchestrionInnAddressResolver : BaseAddressResolver
{
    [Signature("88 05 ?? ?? ?? ?? 89 1D ?? ?? ?? ??", ScanType = ScanType.StaticAddress)]
    public IntPtr PlaybackMode { get; private set; }

    [Signature("C7 05 ?? ?? ?? ?? ?? ?? ?? ?? 41 0F B7 CF", ScanType = ScanType.StaticAddress)]
    public IntPtr PlaybackState { get; private set; }

    [Signature("66 44 89 3D ?? ?? ?? ?? 4D 8B E0", ScanType = ScanType.StaticAddress)]
    public IntPtr PlayingTrackId { get; private set; }

    [Signature("66 89 35 ?? ?? ?? ?? F3 0F 10 02", ScanType = ScanType.StaticAddress)]
    public IntPtr SamplingTrackId { get; private set; }

    protected override void Setup64Bit(ISigScanner scanner) => DalamudApi.Hooks.InitializeFromAttributes(this);

    internal OrchestrionInnPlaybackMode GetInnPlaybackMode() => (OrchestrionInnPlaybackMode)GetInnPlaybackModeRaw();
    internal OrchestrionInnPlaybackState GetInnPlaybackState() => (OrchestrionInnPlaybackState)GetInnPlaybackStateRaw();
    internal uint GetInnPlayingTrackId() => GetInnPlayingTrackIdRaw();
    internal uint GetInnSamplingTrackId() => GetInnSamplingTrackIdRaw();

    private uint GetInnPlaybackModeRaw()
    {
        uint returnValue = (uint)Marshal.ReadInt32(PlaybackMode);
        return returnValue;
    }
    private uint GetInnPlaybackStateRaw()
    {
        uint returnValue = (uint)Marshal.ReadInt32(PlaybackState);
        return returnValue;
    }
    private ushort GetInnPlayingTrackIdRaw()
    {
        ushort returnValue = (ushort)Marshal.ReadInt16(PlayingTrackId);
        return returnValue;
    }
    private ushort GetInnSamplingTrackIdRaw()
    {
        ushort returnValue = (ushort)Marshal.ReadInt16(SamplingTrackId);
        return returnValue;
    }
}