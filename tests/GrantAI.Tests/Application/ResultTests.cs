using GrantAI.Application.Common.Results;
using Xunit;

namespace GrantAI.Tests.Application;

public class ResultTests
{
    [Fact]
    public void Success_HasValue_NoError()
    {
        Result<int> result = 42;

        Assert.True(result.IsSuccess);
        Assert.False(result.IsError);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Failure_HasError_NoValue()
    {
        Result<int> result = Error.NotFound("X", "missing");

        Assert.True(result.IsError);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.NotFound, result.Error.Kind);
        Assert.Equal("X", result.Error.Code);
    }

    [Fact]
    public void TryGetValue_ReportsSuccessBranch()
    {
        Result<string> result = "ok";

        Assert.True(result.TryGetValue(out var value, out var error));
        Assert.Equal("ok", value);
        Assert.Null(error);
    }

    [Fact]
    public void TryGetValue_ReportsFailureBranch()
    {
        Result<string> result = Error.Validation("code", "bad");

        Assert.False(result.TryGetValue(out var value, out var error));
        Assert.Null(value);
        Assert.NotNull(error);
        Assert.Equal(ErrorKind.Validation, error.Kind);
    }

    [Fact]
    public void Value_OnFailure_Throws()
    {
        Result<string> result = Error.Unexpected("boom");
        Assert.Throws<InvalidOperationException>(() => _ = result.Value);
    }

    [Fact]
    public void Error_OnSuccess_Throws()
    {
        Result<string> result = "ok";
        Assert.Throws<InvalidOperationException>(() => _ = result.Error);
    }
}
