using FluentAssertions;
using NSubstitute;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;
using SentientArchitect.Application.Features.Auth.Login;
using SentientArchitect.Application.Features.Auth.LogoutSession;
using SentientArchitect.Application.Features.Auth.RefreshSession;
using SentientArchitect.Application.Features.Auth.RegisterUser;

namespace SentientArchitect.UnitTests.Application.Features.Auth;

public class RegisterUserUseCaseTests
{
    private readonly IAuthIdentityService _authIdentityService = Substitute.For<IAuthIdentityService>();
    private readonly RegisterUserUseCase _sut;

    public RegisterUserUseCaseTests()
    {
        _sut = new RegisterUserUseCase(_authIdentityService);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnRegisteredUser_WhenIdentityRegistrationSucceeds()
    {
        var request = new RegisterUserRequest("dev@sentient.dev", "Password123", "Dev User");
        var registeredUser = new AuthIdentityUser(
            Guid.NewGuid(),
            request.Email,
            request.DisplayName,
            Guid.NewGuid(),
            true,
            ["User"]);

        _authIdentityService
            .RegisterAsync(Arg.Any<RegisterIdentityUserRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<AuthIdentityUser>.SuccessWith(registeredUser));

        var result = await _sut.ExecuteAsync(request);

        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.UserId.Should().Be(registeredUser.Id);
        result.Data.Email.Should().Be(request.Email);
        result.Data.DisplayName.Should().Be(request.DisplayName);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPreserveFailure_WhenIdentityRegistrationFails()
    {
        var request = new RegisterUserRequest("dev@sentient.dev", "Password123", "Dev User");

        _authIdentityService
            .RegisterAsync(Arg.Any<RegisterIdentityUserRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<AuthIdentityUser>.Failure(["Duplicate email."], ErrorType.Conflict));

        var result = await _sut.ExecuteAsync(request);

        result.Succeeded.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Conflict);
        result.Errors.Should().ContainSingle().Which.Should().Be("Duplicate email.");
    }
}

public class LoginUseCaseTests
{
    private readonly IAuthIdentityService _authIdentityService = Substitute.For<IAuthIdentityService>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly LoginUseCase _sut;

    public LoginUseCaseTests()
    {
        _tokenService.GetAccessTokenLifetimeSeconds().Returns(604800);
        _tokenService
            .CreateToken(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<IList<string>>())
            .Returns("signed-jwt-token");

        _sut = new LoginUseCase(_authIdentityService, _tokenService);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnUnauthorized_WhenCredentialsAreInvalid()
    {
        _authIdentityService
            .ValidateCredentialsAsync("dev@sentient.dev", "wrong-password", Arg.Any<CancellationToken>())
            .Returns((AuthIdentityUser?)null);

        var result = await _sut.ExecuteAsync(new LoginRequest("dev@sentient.dev", "wrong-password"));

        result.Succeeded.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnUnauthorized_WhenUserIsInactive()
    {
        var inactiveUser = new AuthIdentityUser(
            Guid.NewGuid(),
            "inactive@sentient.dev",
            "Inactive User",
            Guid.NewGuid(),
            false,
            ["User"]);

        _authIdentityService
            .ValidateCredentialsAsync(inactiveUser.Email, "Password123", Arg.Any<CancellationToken>())
            .Returns(inactiveUser);

        var result = await _sut.ExecuteAsync(new LoginRequest(inactiveUser.Email, "Password123"));

        result.Succeeded.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnSession_WhenCredentialsAreValid()
    {
        var user = new AuthIdentityUser(
            Guid.NewGuid(),
            "admin@sentient.dev",
            "Admin User",
            Guid.NewGuid(),
            true,
            ["Admin"]);

        _authIdentityService
            .ValidateCredentialsAsync(user.Email, "Password123", Arg.Any<CancellationToken>())
            .Returns(user);

        var result = await _sut.ExecuteAsync(new LoginRequest(user.Email, "Password123"));

        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Token.Should().Be("signed-jwt-token");
        result.Data.RefreshToken.Should().Be("signed-jwt-token");
        result.Data.ExpiresIn.Should().Be(604800);
        result.Data.User.Role.Should().Be("Admin");
    }
}

public class RefreshSessionUseCaseTests
{
    private readonly IAuthIdentityService _authIdentityService = Substitute.For<IAuthIdentityService>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly RefreshSessionUseCase _sut;

    public RefreshSessionUseCaseTests()
    {
        _tokenService
            .CreateToken(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<IList<string>>())
            .Returns("refreshed-jwt-token");

        _sut = new RefreshSessionUseCase(_authIdentityService, _tokenService);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnUnauthorized_WhenRefreshTokenIsInvalid()
    {
        _tokenService.GetUserIdFromToken("bad-token", true).Returns((Guid?)null);

        var result = await _sut.ExecuteAsync(new RefreshSessionRequest("bad-token"));

        result.Succeeded.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnUnauthorized_WhenUserIsInactive()
    {
        var userId = Guid.NewGuid();
        _tokenService.GetUserIdFromToken("expired-token", true).Returns(userId);
        _authIdentityService
            .FindByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new AuthIdentityUser(
                userId,
                "inactive@sentient.dev",
                "Inactive User",
                Guid.NewGuid(),
                false,
                ["User"]));

        var result = await _sut.ExecuteAsync(new RefreshSessionRequest("expired-token"));

        result.Succeeded.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNewTokens_WhenRefreshTokenIsValid()
    {
        var userId = Guid.NewGuid();
        var user = new AuthIdentityUser(
            userId,
            "dev@sentient.dev",
            "Dev User",
            Guid.NewGuid(),
            true,
            ["User"]);

        _tokenService.GetUserIdFromToken("expired-token", true).Returns(userId);
        _authIdentityService
            .FindByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        var result = await _sut.ExecuteAsync(new RefreshSessionRequest("expired-token"));

        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Token.Should().Be("refreshed-jwt-token");
        result.Data.RefreshToken.Should().Be("refreshed-jwt-token");
    }
}

public class LogoutSessionUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldReturnUnauthorized_WhenUserIdIsEmpty()
    {
        var sut = new LogoutSessionUseCase();

        var result = await sut.ExecuteAsync(new LogoutSessionRequest(Guid.Empty));

        result.Succeeded.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnSuccess_WhenUserIdIsPresent()
    {
        var sut = new LogoutSessionUseCase();

        var result = await sut.ExecuteAsync(new LogoutSessionRequest(Guid.NewGuid()));

        result.Succeeded.Should().BeTrue();
    }
}