using Connext.Api.Controllers;
using Connext.Api.Services;
using Connext.Shared.DTOs.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Connext.Tests;

public class AuthControllerTests
{
    private class FakeAuthService : IAuthService
    {
        public bool ShouldThrowDuplicatePhone { get; set; }
        public bool ShouldReturnNullOnLogin { get; set; }

        public Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request)
        {
            if (ShouldThrowDuplicatePhone)
            {
                throw new InvalidOperationException("Số điện thoại đã được đăng ký.");
            }

            return Task.FromResult(new AuthResponseDto
            {
                Token = "fake-jwt-token",
                UserId = Guid.NewGuid(),
                PhoneNumber = request.PhoneNumber,
                DisplayName = request.DisplayName
            });
        }

        public Task<AuthResponseDto?> LoginAsync(LoginRequestDto request)
        {
            if (ShouldReturnNullOnLogin)
            {
                return Task.FromResult<AuthResponseDto?>(null);
            }

            return Task.FromResult<AuthResponseDto?>(new AuthResponseDto
            {
                Token = "fake-jwt-token",
                UserId = Guid.NewGuid(),
                PhoneNumber = request.PhoneNumber,
                DisplayName = "Test User"
            });
        }
    }

    [Fact]
    public async Task Register_WithValidData_Returns200WithToken()
    {
        var fakeAuthService = new FakeAuthService();
        var controller = new AuthController(fakeAuthService);
        var request = new RegisterRequestDto
        {
            PhoneNumber = "0987654321",
            Password = "Password123!",
            DisplayName = "Nguyen Van A"
        };

        var result = await controller.Register(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        var response = Assert.IsType<AuthResponseDto>(okResult.Value);
        Assert.False(string.IsNullOrEmpty(response.Token));
    }

    [Fact]
    public async Task Register_WithDuplicatePhoneNumber_Returns400()
    {
        var fakeAuthService = new FakeAuthService { ShouldThrowDuplicatePhone = true };
        var controller = new AuthController(fakeAuthService);
        var request = new RegisterRequestDto
        {
            PhoneNumber = "0987654321",
            Password = "Password123!",
            DisplayName = "Nguyen Van A"
        };

        var result = await controller.Register(request);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
    }

    [Fact]
    public async Task Login_WithValidCredentials_Returns200WithToken()
    {
        var fakeAuthService = new FakeAuthService();
        var controller = new AuthController(fakeAuthService);
        var request = new LoginRequestDto
        {
            PhoneNumber = "0987654321",
            Password = "Password123!"
        };

        var result = await controller.Login(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        var response = Assert.IsType<AuthResponseDto>(okResult.Value);
        Assert.False(string.IsNullOrEmpty(response.Token));
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_Returns401()
    {
        var fakeAuthService = new FakeAuthService { ShouldReturnNullOnLogin = true };
        var controller = new AuthController(fakeAuthService);
        var request = new LoginRequestDto
        {
            PhoneNumber = "0987654321",
            Password = "WrongPassword"
        };

        var result = await controller.Login(request);

        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);
    }
}
