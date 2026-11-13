using Connext.Api.Controllers;
using Connext.Api.Services;
using Connext.Shared.DTOs.Auth;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Connext.Tests;

public class AuthControllerTests
{
    private class MockAuthService : IAuthService
    {
        public Func<RegisterRequestDto, Task<AuthResponseDto>>? RegisterFunc { get; set; }
        public Func<LoginRequestDto, Task<AuthResponseDto?>>? LoginFunc { get; set; }

        public Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request)
        {
            if (RegisterFunc != null) return RegisterFunc(request);
            throw new NotImplementedException();
        }

        public Task<AuthResponseDto?> LoginAsync(LoginRequestDto request)
        {
            if (LoginFunc != null) return LoginFunc(request);
            throw new NotImplementedException();
        }

        public Task LogoutAsync(Guid userId) => Task.CompletedTask;
    }

    [Fact]
    public async Task Register_Success_ReturnsOkObjectResult()
    {
        // Arrange
        var mockService = new MockAuthService
        {
            RegisterFunc = req => Task.FromResult(new AuthResponseDto
            {
                Token = "fake-jwt-token",
                UserId = Guid.NewGuid(),
                PhoneNumber = req.PhoneNumber,
                DisplayName = req.DisplayName
            })
        };
        var controller = new AuthController(mockService);
        var request = new RegisterRequestDto
        {
            PhoneNumber = "0901234567",
            Password = "Password123!",
            DisplayName = "Test"
        };

        // Act
        var result = await controller.Register(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        var response = Assert.IsType<AuthResponseDto>(okResult.Value);
        Assert.Equal("0901234567", response.PhoneNumber);
    }

    [Fact]
    public async Task Register_DuplicatePhone_ReturnsBadRequest()
    {
        // Arrange
        var mockService = new MockAuthService
        {
            RegisterFunc = _ => throw new InvalidOperationException("Số điện thoại đã được đăng ký.")
        };
        var controller = new AuthController(mockService);
        var request = new RegisterRequestDto
        {
            PhoneNumber = "0901234567",
            Password = "Password123!",
            DisplayName = "Test"
        };

        // Act
        var result = await controller.Register(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequestResult.StatusCode);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsOkObjectResult()
    {
        // Arrange
        var mockService = new MockAuthService
        {
            LoginFunc = req => Task.FromResult<AuthResponseDto?>(new AuthResponseDto
            {
                Token = "fake-jwt-token",
                UserId = Guid.NewGuid(),
                PhoneNumber = req.PhoneNumber,
                DisplayName = "User"
            })
        };
        var controller = new AuthController(mockService);
        var request = new LoginRequestDto
        {
            PhoneNumber = "0901234567",
            Password = "Password123!"
        };

        // Act
        var result = await controller.Login(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        var response = Assert.IsType<AuthResponseDto>(okResult.Value);
        Assert.Equal("0901234567", response.PhoneNumber);
    }

    [Fact]
    public async Task Login_InvalidCredentials_ReturnsUnauthorized()
    {
        // Arrange
        var mockService = new MockAuthService
        {
            LoginFunc = _ => Task.FromResult<AuthResponseDto?>(null)
        };
        var controller = new AuthController(mockService);
        var request = new LoginRequestDto
        {
            PhoneNumber = "0901234567",
            Password = "WrongPassword!"
        };

        // Act
        var result = await controller.Login(request);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(401, unauthorizedResult.StatusCode);
    }
}
