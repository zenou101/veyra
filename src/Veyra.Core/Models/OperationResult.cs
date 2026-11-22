namespace Veyra.Core.Models;

/// <summary>Outcome of every system-changing operation. Never reported before the work completes.</summary>
public enum OperationStatus
{
    Success,
    Unsupported,
    InvalidConfiguration,
    DriverUnavailable,
    DisplayDisconnected,
    PermissionDenied,
    Timeout,
    RecoveryCompleted,
    RecoveryFailed,
    Cancelled,
    NotFound,
    UnknownFailure
}

public sealed record OperationResult(OperationStatus Status, string Message, string? Diagnostic = null)
{
    public bool IsSuccess => Status == OperationStatus.Success;

    public static OperationResult Success(string message) => new(OperationStatus.Success, message);
    public static OperationResult Unsupported(string message, string? diagnostic = null) => new(OperationStatus.Unsupported, message, diagnostic);
    public static OperationResult Invalid(string message, string? diagnostic = null) => new(OperationStatus.InvalidConfiguration, message, diagnostic);
    public static OperationResult DriverUnavailable(string message, string? diagnostic = null) => new(OperationStatus.DriverUnavailable, message, diagnostic);
    public static OperationResult Disconnected(string message, string? diagnostic = null) => new(OperationStatus.DisplayDisconnected, message, diagnostic);
    public static OperationResult Denied(string message, string? diagnostic = null) => new(OperationStatus.PermissionDenied, message, diagnostic);
    public static OperationResult TimedOut(string message, string? diagnostic = null) => new(OperationStatus.Timeout, message, diagnostic);
    public static OperationResult Cancelled(string message = "Operation cancelled.") => new(OperationStatus.Cancelled, message);
    public static OperationResult NotFound(string message) => new(OperationStatus.NotFound, message);
    public static OperationResult Failure(string message, string? diagnostic = null) => new(OperationStatus.UnknownFailure, message, diagnostic);

    public override string ToString() => $"{Status}: {Message}";
}
