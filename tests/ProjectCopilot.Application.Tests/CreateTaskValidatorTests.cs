using System.Text.Json;
using ProjectCopilot.Application.Tasks.CreateTask;

namespace ProjectCopilot.Application.Tests;

// AUDIT-1 regression coverage: DueDate validation must compare against the UTC-normalized
// instant, not the raw deserialized DateTime, so that an explicit numeric-offset wire format
// (Kind = Local after deserialization) cannot slip a genuinely-past instant past the
// "no past dates" rule. See CreateTaskValidator.DueDate rule and DateTimeNormalization.
public class CreateTaskValidatorTests
{
    private static CreateTaskCommand CommandWithDueDate(DateTime? dueDate) =>
        new(Guid.NewGuid(), "Valid Title", null, "Medium", null, dueDate);

    private static DateTime? ParseWireDate(string isoString) =>
        JsonSerializer.Deserialize<DateTime?>($"\"{isoString}\"");

    // Pre-existing coverage, moved here from UnitTest1.cs (merged to avoid a duplicate
    // CreateTaskValidatorTests class definition in the same namespace).
    [Fact]
    public async Task ValidateAsync_WhenTitleIsEmpty_ShouldFail()
    {
        var validator = new CreateTaskValidator();

        var command = new CreateTaskCommand(
            Guid.NewGuid(),
            "",
            "Test description",
            "High",
            null,
            DateTime.UtcNow.AddDays(5));

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(CreateTaskCommand.Title));
    }

    [Fact]
    public async Task ValidateAsync_WhenPriorityIsInvalid_ShouldFail()
    {
        var validator = new CreateTaskValidator();

        var command = new CreateTaskCommand(
            Guid.NewGuid(),
            "Valid title",
            "Test description",
            "SuperHigh",
            null,
            DateTime.UtcNow.AddDays(5));

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(CreateTaskCommand.Priority));
    }

    [Fact]
    public async Task ValidateAsync_WhenCommandIsValid_ShouldPass()
    {
        var validator = new CreateTaskValidator();

        var command = new CreateTaskCommand(
            Guid.NewGuid(),
            "Implement retry logic",
            "Handle failed payment retries",
            "High",
            null,
            DateTime.UtcNow.AddDays(5));

        var result = await validator.ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenDueDateIsYesterday_ZFormat_ShouldFail()
    {
        var yesterday = DateTime.UtcNow.Date.AddDays(-1);
        var dueDate = ParseWireDate($"{yesterday:yyyy-MM-dd}T23:30:00Z");

        var validator = new CreateTaskValidator();
        var result = await validator.ValidateAsync(CommandWithDueDate(dueDate));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateTaskCommand.DueDate));
    }

    [Fact]
    public async Task ValidateAsync_WhenDueDateIsYesterday_ExplicitOffsetFormat_ShouldFail()
    {
        // This is the AUDIT-1 case: the same underlying past instant as the Z-format test
        // above, but expressed with an explicit numeric offset. Before the fix, the validator
        // compared the raw deserialized value's .Date (Kind = Local) directly against
        // DateTime.UtcNow.Date, which on a host ahead of UTC could read as "today" and wrongly
        // pass. After the fix, the value is normalized to UTC before comparing.
        var yesterday = DateTime.UtcNow.Date.AddDays(-1);
        var dueDate = ParseWireDate($"{yesterday:yyyy-MM-dd}T23:30:00+00:00");

        Assert.Equal(DateTimeKind.Local, dueDate!.Value.Kind);

        var validator = new CreateTaskValidator();
        var result = await validator.ValidateAsync(CommandWithDueDate(dueDate));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateTaskCommand.DueDate));
    }

    [Fact]
    public async Task ValidateAsync_WhenDueDateIsYesterday_ExplicitNonZeroOffsetFormat_ShouldFail()
    {
        var yesterday = DateTime.UtcNow.Date.AddDays(-1);
        var dueDate = ParseWireDate($"{yesterday:yyyy-MM-dd}T23:30:00+02:00");

        Assert.Equal(DateTimeKind.Local, dueDate!.Value.Kind);

        var validator = new CreateTaskValidator();
        var result = await validator.ValidateAsync(CommandWithDueDate(dueDate));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateTaskCommand.DueDate));
    }

    [Fact]
    public async Task ValidateAsync_WhenDueDateIsToday_ZFormat_ShouldPass()
    {
        var today = DateTime.UtcNow.Date;
        var dueDate = ParseWireDate($"{today:yyyy-MM-dd}T06:00:00Z");

        var validator = new CreateTaskValidator();
        var result = await validator.ValidateAsync(CommandWithDueDate(dueDate));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenDueDateIsToday_ExplicitOffsetFormat_ShouldPass()
    {
        // Offset chosen so the normalized UTC instant's calendar date is genuinely today.
        var today = DateTime.UtcNow.Date;
        var dueDate = ParseWireDate($"{today:yyyy-MM-dd}T12:00:00+00:00");

        var validator = new CreateTaskValidator();
        var result = await validator.ValidateAsync(CommandWithDueDate(dueDate));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenDueDateIsTomorrow_ZFormat_ShouldPass()
    {
        var tomorrow = DateTime.UtcNow.Date.AddDays(1);
        var dueDate = ParseWireDate($"{tomorrow:yyyy-MM-dd}T00:00:00Z");

        var validator = new CreateTaskValidator();
        var result = await validator.ValidateAsync(CommandWithDueDate(dueDate));

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

        var validator = new CreateTaskValidator();
        var result = await validator.ValidateAsync(CommandWithDueDate(localValue));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateTaskCommand.DueDate));
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

        var validator = new CreateTaskValidator();
        var result = await validator.ValidateAsync(CommandWithDueDate(localValue));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenDueDateIsNull_ShouldPass()
    {
        var validator = new CreateTaskValidator();
        var result = await validator.ValidateAsync(CommandWithDueDate(null));

        Assert.True(result.IsValid);
    }
}
