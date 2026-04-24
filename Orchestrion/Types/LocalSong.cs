namespace Orchestrion.Types;

public class LocalSong
{
    /// <summary>
    /// Local song IDs are negative integers at or below this threshold,
    /// leaving -1 (NoChangeId) and 0 untouched.
    /// </summary>
    public const int LocalIdMin = -100;

    /// <summary>
    /// Starting value for display IDs. Chosen to be well above current game song IDs.
    /// Validated against the live song list on startup so conflicts are detected and reassigned.
    /// </summary>
    public const int DisplayIdStart = 9001;

    public int Id { get; set; }

    /// <summary>
    /// Human-readable 4-digit display ID (e.g. 9001). Never overlaps a game song ID at
    /// time of assignment; reassigned automatically if the game later claims the number.
    /// </summary>
    public int DisplayId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }

    public static bool IsLocalId(int id) => id <= LocalIdMin;
}
