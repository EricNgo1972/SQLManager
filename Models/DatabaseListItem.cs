namespace SQLManager.Models;

public sealed record DatabaseListItem(
    string ServerKey,
    string DatabaseName,
    string State,
    decimal SizeMb)
{
    public bool IsOnline => string.Equals(State, "ONLINE", StringComparison.OrdinalIgnoreCase);

    public bool IsOffline => string.Equals(State, "OFFLINE", StringComparison.OrdinalIgnoreCase);

    public bool CanToggle => IsOnline || IsOffline;

    public string SizeDisplay => $"{SizeMb:N2} MB";
}
