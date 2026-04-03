using FluentAssertions;
using SentientArchitect.Application.Common.Results;

namespace SentientArchitect.UnitTests.Application;

public class ResultTests
{
    [Fact]
    public void Success_ShouldHaveSucceededTrue()
    {
        var result = Result.Success;

        result.Succeeded.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Failure_ShouldHaveSucceededFalse_AndContainErrors()
    {
        var result = Result.Failure(["Something went wrong"]);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Be("Something went wrong");
    }

    [Fact]
    public void ImplicitConversion_FromString_ShouldCreateFailure()
    {
        Result result = "Validation failed";

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Be("Validation failed");
    }

    [Fact]
    public void ImplicitConversion_FromTrue_ShouldCreateSuccess()
    {
        Result result = true;

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void ImplicitConversion_FromFalse_ShouldCreateFailure()
    {
        Result result = false;

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public void ResultT_SuccessWith_ShouldContainData()
    {
        var result = Result<string>.SuccessWith("hello");

        result.Succeeded.Should().BeTrue();
        result.Data.Should().Be("hello");
    }

    [Fact]
    public void ResultT_Failure_ShouldHaveNullData()
    {
        var result = Result<string>.Failure(["error"]);

        result.Succeeded.Should().BeFalse();
        result.Data.Should().BeNull();
    }

    [Fact]
    public void ResultT_ImplicitConversion_FromData_ShouldCreateSuccess()
    {
        Result<int> result = 42;

        result.Succeeded.Should().BeTrue();
        result.Data.Should().Be(42);
    }

    [Fact]
    public void ResultT_ImplicitConversion_FromErrorList_ShouldCreateFailure()
    {
        Result<int> result = new List<string> { "error one", "error two" };

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().HaveCount(2);
    }

    [Fact]
    public void Failure_WithNotFoundType_ShouldSetCorrectErrorType()
    {
        var result = Result.Failure(["Not found"], ErrorType.NotFound);

        result.ErrorType.Should().Be(ErrorType.NotFound);
    }
}
