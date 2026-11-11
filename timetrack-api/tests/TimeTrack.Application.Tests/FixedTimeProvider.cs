namespace TimeTrack.Application.Tests;

/// <summary>Deterministic clock: tests never depend on the real date.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
