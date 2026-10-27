using HRDB.Models;
using Microsoft.AspNetCore.Identity;
using SkladDB.Services;

namespace SkladApi.Services;

/// <summary>
/// Реализация ICurrentUserService для ASP.NET Core: берёт пользователя из
/// HttpContext и преобразует его в идентификатор AspNetUsers.Id.
/// </summary>
public sealed class HttpCurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly UserManager<ApplicationUser> _userManager;

    public HttpCurrentUserService(
        IHttpContextAccessor httpContextAccessor,
        UserManager<ApplicationUser> userManager)
    {
        _httpContextAccessor = httpContextAccessor;
        _userManager = userManager;
    }

    public string? UserId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            return user is null ? null : _userManager.GetUserId(user);
        }
    }
}
