namespace Lionear.SqlExplorer.App.ViewModels;

/// <summary>Severity of an <see cref="OutputLogEntry"/>; drives the badge colour in the output panel.</summary>
public enum OutputLevel
{
    Info,
    Error
}

/// <summary>One line in the output/log panel: when it happened, how bad it was, which connection it
/// came from, and the message. Errors also raise the inline banner (see MainViewModel).</summary>
public sealed record OutputLogEntry(DateTime Time, OutputLevel Level, string? Source, string Message)
{
    public bool IsError => Level == OutputLevel.Error;
    public string TimeText => Time.ToString("HH:mm:ss");
    public string LevelText => IsError ? "ERROR" : "OK";
}
