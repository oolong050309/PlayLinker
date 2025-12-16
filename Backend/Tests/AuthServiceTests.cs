using Xunit;
using Moq;
using PlayLinker.Data;
using PlayLinker.Models.DTOs;
using PlayLinker.Models.Entities;
using PlayLinker.Services;
using Microsoft.Extensions.Logging;

namespace PlayLinker.Tests;

public class AuthServiceTests
{
    private readonly Mock<PlayLinkerDbContext> _mockDbContext;
    private readonly Mock<IPasswordHasher> _mockPasswordHasher;
    private readonly Mock<ILogger<AuthService>> _mockLogger;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        _mockDbContext = new Mock<PlayLinkerDbContext>();
        _mockPasswordHasher = new Mock<IPasswordHasher>();
        _mockLogger = new Mock<ILogger<AuthService>>();
        _authService = new AuthService(_mockDbContext.Object, _mockPasswordHasher.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task RegisterAsync_WithValidData_ReturnsSuccess()
    {
        // Arrange
        var request = new RegisterRequestDto
        {
            Username = "testuser",
            Password = "TestPass123!",
            Email = "test@example.com",
            Phone = "13800138000"
        };

        var defaultRole = new Role { RoleId = 1, RoleName = "user" };

        // Mock the database queries
        var usersDbSet = new Mock<Microsoft.EntityFrameworkCore.DbSet<User>>();
        var rolesDbSet = new Mock<Microsoft.EntityFrameworkCore.DbSet<Role>>();

        usersDbSet.Setup(x => x.FirstOrDefault(It.IsAny<System.Linq.Expressions.Expression<System.Func<User, bool>>>()))
            .Returns((User)null);

        rolesDbSet.Setup(x => x.FirstOrDefault(It.IsAny<System.Linq.Expressions.Expression<System.Func<Role, bool>>>()))
            .Returns(defaultRole);

        _mockDbContext.Setup(x => x.Users).Returns(usersDbSet.Object);
        _mockDbContext.Setup(x => x.Roles).Returns(rolesDbSet.Object);
        _mockDbContext.Setup(x => x.SaveChangesAsync(It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(1);

        _mockPasswordHasher.Setup(x => x.Hash(It.IsAny<string>()))
            .Returns("hashed_password");

        // Act
        var (success, message, user) = await _authService.RegisterAsync(request);

        // Assert
        Assert.True(success);
        Assert.Equal("注册成功", message);
        Assert.NotNull(user);
        Assert.Equal(request.Username, user.Username);
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsSuccess()
    {
        // Arrange
        var request = new LoginRequestDto
        {
            Username = "testuser",
            Password = "TestPass123!"
        };

        var user = new User
        {
            UserId = 1,
            Username = "testuser",
            HashedPassword = "hashed_password",
            Status = "active",
            Role = new Role { RoleId = 1, RoleName = "user" }
        };

        var usersDbSet = new Mock<Microsoft.EntityFrameworkCore.DbSet<User>>();
        usersDbSet.Setup(x => x.FirstOrDefault(It.IsAny<System.Linq.Expressions.Expression<System.Func<User, bool>>>()))
            .Returns(user);

        _mockDbContext.Setup(x => x.Users).Returns(usersDbSet.Object);
        _mockDbContext.Setup(x => x.SaveChangesAsync(It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(1);

        _mockPasswordHasher.Setup(x => x.Verify(request.Password, user.HashedPassword))
            .Returns(true);

        // Act
        var (success, message, returnedUser) = await _authService.LoginAsync(request);

        // Assert
        Assert.True(success);
        Assert.Equal("登录成功", message);
        Assert.NotNull(returnedUser);
        Assert.Equal(user.UserId, returnedUser.UserId);
    }

    [Fact]
    public async Task LoginAsync_WithInvalidPassword_ReturnsFail()
    {
        // Arrange
        var request = new LoginRequestDto
        {
            Username = "testuser",
            Password = "WrongPassword"
        };

        var user = new User
        {
            UserId = 1,
            Username = "testuser",
            HashedPassword = "hashed_password",
            Status = "active"
        };

        var usersDbSet = new Mock<Microsoft.EntityFrameworkCore.DbSet<User>>();
        usersDbSet.Setup(x => x.FirstOrDefault(It.IsAny<System.Linq.Expressions.Expression<System.Func<User, bool>>>()))
            .Returns(user);

        _mockDbContext.Setup(x => x.Users).Returns(usersDbSet.Object);

        _mockPasswordHasher.Setup(x => x.Verify(request.Password, user.HashedPassword))
            .Returns(false);

        // Act
        var (success, message, returnedUser) = await _authService.LoginAsync(request);

        // Assert
        Assert.False(success);
        Assert.Equal("ERR_INVALID_CREDENTIALS", message);
        Assert.Null(returnedUser);
    }
}

