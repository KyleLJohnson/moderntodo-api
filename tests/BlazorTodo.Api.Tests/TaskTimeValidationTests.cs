using BlazorTodo.Api.Functions;

namespace BlazorTodo.Api.Tests;

public class TaskTimeValidationTests
{
    // Null / empty values are valid (field is optional)
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValidTaskTime_NullOrWhitespace_ReturnsTrue(string? taskTime)
    {
        Assert.True(TaskFunctions.IsValidTaskTime(taskTime));
    }

    // Valid HH:mm formats
    [Theory]
    [InlineData("00:00")]
    [InlineData("09:30")]
    [InlineData("12:00")]
    [InlineData("23:59")]
    public void IsValidTaskTime_ValidHHmm_ReturnsTrue(string taskTime)
    {
        Assert.True(TaskFunctions.IsValidTaskTime(taskTime));
    }

    // Valid HH:mm:ss formats
    [Theory]
    [InlineData("00:00:00")]
    [InlineData("08:45:30")]
    [InlineData("23:59:59")]
    public void IsValidTaskTime_ValidHHmmss_ReturnsTrue(string taskTime)
    {
        Assert.True(TaskFunctions.IsValidTaskTime(taskTime));
    }

    // Invalid formats
    [Theory]
    [InlineData("25:00")]        // hour out of range
    [InlineData("12:60")]        // minute out of range
    [InlineData("12:30:60")]     // second out of range
    [InlineData("9:30")]         // missing leading zero
    [InlineData("noon")]         // not a time
    [InlineData("12-30")]        // wrong separator
    [InlineData("12:30:00:00")]  // too many parts
    public void IsValidTaskTime_InvalidFormat_ReturnsFalse(string taskTime)
    {
        Assert.False(TaskFunctions.IsValidTaskTime(taskTime));
    }
}
