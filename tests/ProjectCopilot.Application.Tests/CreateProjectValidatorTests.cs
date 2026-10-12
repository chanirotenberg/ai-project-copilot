using System.Text.Json;
using ProjectCopilot.Application.Projects.CreateProject;

namespace ProjectCopilot.Application.Tests;

// AUDIT-1 regression coverage: Deadline validation must compare against the UTC-normalized
// instant, not the raw deserialized DateTime, so that an explicit numeric-offset wire format
// (Kind = Local after deserialization) cannot slip a genuinely-past instant past the
// "no past dates" rule. See CreateProjectValidator.Deadline rule and DateTimeNormalization.
public class CreateProjectValidatorTests
{
    private static CreateProjectCommand CommandWithDeadline(DateTime? deadline) =>
        new("Valid Name", null, deadline);

    private static DateTime? ParseWireDate(string isoString) =>
        JsonSerializer.Deserialize<DateTime?>($"\"{isoString}\"");

    [Fact]
    public async Task ValidateAsync_WhenDeadlineIsYesterday_ZFormat_ShouldFail()
    {
        var yesterday = DateTime.UtcNow.Date.AddDays(-1);
        var deadline = ParseWireDate($"{yesterday:yyyy-MM-dd}T23:30:00Z");

        var validator = new CreateProjectValidator();
        var result = await validator.ValidateAsync(CommandWithDeadline(deadline));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateProjectCommand.Deadline));
    }

    [Fact]
    public async Task ValidateAsync_WhenDeadlineIsYesterday_ExplicitOffsetFormat_ShouldFail()
    {
        // This is the AUDIT-1 case: the same underlying past instant as the Z-format test
        // above, but expressed with an explicit numeric offset. Before the fix, the validator
        // compared the raw deserialized value's .Date (Kind = Local) directly against
        // DateTime.UtcNow.Date, which on a host ahead of UTC could read as "today" and wrongly
        // pass. After the fix, the value is normalized to UTC before comparing.
        var yesterday = DateTime.UtcNow.Date.AddDays(-1);
        var deadline = ParseWireDate($"{yesterday:yyyy-MM-dd}T23:30:00+00:00");

        Assert.Equal(DateTimeKind.Local, deadline!.Value.Kind);

        var validator = new CreateProjectValidator();
        var result = await validator.ValidateAsync(CommandWithDeadline(deadline));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateProjectCommand.Deadline));
    }

    [Fact]
    public async Task ValidateAsync_WhenDeadlineIsYesterday_ExplicitNonZeroOffsetFormat_ShouldFail()
    {
        var yesterday = DateTime.UtcNow.Date.AddDays(-1);
        var deadline = ParseWireDate($"{yesterday:yyyy-MM-dd}T23:30:00+02:00");

        Assert.Equal(DateTimeKind.Local, deadline!.Value.Kind);

        var validator = new CreateProjectValidator();
        var result = await validator.ValidateAsync(CommandWithDeadline(deadline));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateProjectCommand.Deadline));
    }

    [Fact]
    public async Task ValidateAsync_WhenDeadlineIsToday_ZFormat_ShouldPass()
    {
        var today = DateTime.UtcNow.Date;
        var deadline = ParseWireDate($"{today:yyyy-MM-dd}T06:00:00Z");

        var validator = new CreateProjectValidator();
        var result = await validator.ValidateAsync(CommandWithDeadline(deadline));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenDeadlineIsToday_ExplicitOffsetFormat_ShouldPass()
    {
        // Offset chosen so the normalized UTC instant's calendar date is genuinely today.
        var today = DateTime.UtcNow.Date;
        var deadline = ParseWireDate($"{today:yyyy-MM-dd}T12:00:00+00:00");

        var validator = new CreateProjectValidator();
        var result = await validator.ValidateAsync(CommandWithDeadline(deadline));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenDeadlineIsTomorrow_ZFormat_ShouldPass()
    {
        var tomorrow = DateTime.UtcNow.Date.AddDays(1);
        var deadline = ParseWireDate($"{tomorrow:yyyy-MM-dd}T00:00:00Z");

        var validator = new CreateProjectValidator();
        var result = await validator.ValidateAsync(CommandWithDeadline(deadline));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenLocalReinterpretationWouldSayToday_ButNormalizedUtcDateIsYesterday_ShouldFail()
    {
        // Construct a genuinely past-UTC instant (yesterday 23:59 UTC) and convert it to
        // Kind = Local, mirroring what System.Text.Json produces for an explicit-offset wire
        // value. On a host ahead of UTC, the raw (un-normalized) local wall-clock date reads as
        // "today" - exactly the misreading AUDIT-1 exploited. The fix must still reject it,
        // because the true UTC calendar date is yesterday.
        var utcInstant = DateTime.SpecifyKind(
            DateTime.UtcNow.Date.AddDays(-1).AddHours(23).AddMinutes(59),
            DateTimeKind.Utc);
        var localValue = utcInstant.ToLocalTime();

        var validator = new CreateProjectValidator();
        var result = await validator.ValidateAsync(CommandWithDeadline(localValue));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateProjectCommand.Deadline));
    }

    [Fact]
    public async Task ValidateAsync_WhenNormalizedUtcDateIsToday_ButNaiveLocalReadingWouldSayYesterday_ShouldPass()
    {
        // Inverse boundary: a genuinely valid UTC instant (today, 00:01 UTC) converted to
        // Kind = Local. On a host behind UTC, the raw local wall-clock date would misread as
        // "yesterday" and wrongly reject. The fix must still accept it, because the true UTC
        // calendar date is today.
        var utcInstant = DateTime.SpecifyKind(
            DateTime.UtcNow.Date.AddMinutes(1),
            DateTimeKind.Utc);
        var localValue = utcInstant.ToLocalTime();

        var validator = new CreateProjectValidator();
        var result = await validator.ValidateAsync(CommandWithDeadline(localValue));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenDeadlineIsNull_ShouldPass()
    {
        var validator = new CreateProjectValidator();
        var result = await validator.ValidateAsync(CommandWithDeadline(null));

        Assert.True(result.IsValid);
    }
}
