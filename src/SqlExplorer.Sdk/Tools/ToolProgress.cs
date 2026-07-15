namespace SqlExplorer.Sdk.Tools;

/// <summary>A single progress/log line a tool reports while running; the host appends it to the tool
/// dialog's log panel. <paramref name="Fraction"/> (0..1), when supplied, also drives the dialog's
/// progress bar as a determinate value; leave it null to keep the bar indeterminate (just "busy").</summary>
public sealed record ToolProgress(string Message, double? Fraction = null);
