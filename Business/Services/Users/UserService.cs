using DataLabelProject.Application.DTOs.Common;
using DataLabelProject.Application.DTOs.Users;
using DataLabelProject.Business.Models;
using DataLabelProject.Business.Services.ActivityLogs;
using DataLabelProject.Data.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace DataLabelProject.Business.Services.Users;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IProjectMemberRepository _memberRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IActivityLogService _activityLog;

    public UserService(
        IUserRepository userRepository,
        IProjectMemberRepository memberRepository,
        IRoleRepository roleRepository,
        ICurrentUserService currentUserService,
        IRefreshTokenRepository refreshTokenRepository,
        IActivityLogService activityLog)
    {
        _userRepository = userRepository;
        _memberRepository = memberRepository;
        _roleRepository = roleRepository;
        _currentUserService = currentUserService;
        _refreshTokenRepository = refreshTokenRepository;
        _activityLog = activityLog;
    }

    public async Task<PagedResponse<UserResponse>> GetUsers(UserQueryParameters @params)
    {
        var (items, totalCount) = await _userRepository.GetAllAsync(@params);

        var userIds = items.Select(u => u.UserId);

        var projectCounts = await GetProjectCountsAsync(userIds);

        return new PagedResponse<UserResponse>
        {
            Items = items.Select(u => MapToResponse(u, projectCounts)).ToList(),
            TotalItems = totalCount,
            Page = @params.Page,
            PageSize = @params.PageSize,
        };
    }

    public async Task<UserResponse?> GetUserById(Guid id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null) return null;

        var count = await GetProjectCountAsync(id);

        return MapToResponse(user, count);
    }

    public async Task<UserResponse> CreateUser(CreateUserRequest request)
    {
        var existedUser = await _userRepository.GetByUsernameAsync(request.Username);
        if (existedUser != null)
            throw new Exception("Username already exists");

        var role = await _roleRepository.GetByIdAsync(request.RoleId);
        if (role == null) {
            throw new Exception("Role not found");
        } else if (role.RoleName == "admin")
            throw new Exception("Cannot create user with [admin] role");

        var passwordHash = Auth.AuthService.HashPassword(request.Password);

        var user = new User
        {
            UserId = Guid.NewGuid(),
            Username = request.Username,
            PasswordHash = passwordHash,
            DisplayName = request.Username,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            RoleId = request.RoleId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        await _userRepository.CreateAsync(user);
        await _userRepository.SaveChangesAsync();

        var projectCount = 0;

        return MapToResponse(user, projectCount);
    }

    public async Task<UserResponse?> UpdateUser(Guid id, UpdateUserRequest request)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null) return null;

        var currentUserId = _currentUserService.UserId!.Value;

        if (id == currentUserId && request.IsActive.HasValue)
            throw new Exception("You cannot activate/deactivate your own account");
        if (user.UserRole?.RoleName == "admin" && request.IsActive.HasValue)
            throw new Exception("You cannot activate/deactivate user with [admin] role");

        if (request.DisplayName != null) user.DisplayName = request.DisplayName;
        if (request.Email != null) user.Email = request.Email;
        if (request.PhoneNumber != null) user.PhoneNumber = request.PhoneNumber;

        // When admin disables user, revoke all refresh tokens
        if (request.IsActive.HasValue && request.IsActive.Value == false && user.IsActive == true)
        {
            user.IsActive = false;
            await _refreshTokenRepository.RevokeAllUserTokensAsync(id);
        }
        else if (request.IsActive.HasValue)
        {
            user.IsActive = request.IsActive.Value;
        }

        await _userRepository.UpdateAsync(user);
        await _userRepository.SaveChangesAsync();

        var projectCount = await GetProjectCountAsync(user.UserId);

        return MapToResponse(user, projectCount);
    }

    private async Task<Dictionary<Guid, int>> GetProjectCountsAsync(IEnumerable<Guid> userIds)
    {
        return await _memberRepository.Query()
            .Where(pm => userIds.Contains(pm.MemberId))
            .GroupBy(pm => pm.MemberId)
            .Select(g => new
            {
                UserId = g.Key,
                Count = g.Count()
            })
            .ToDictionaryAsync(x => x.UserId, x => x.Count);
    }

    private async Task<int> GetProjectCountAsync(Guid userId)
    {
        return await _memberRepository.Query()
            .CountAsync(pm => pm.MemberId == userId);
    }

    private static UserResponse MapToResponse(
        User user,
        Dictionary<Guid, int> projectCounts)
    {
        return new UserResponse
        {
            UserId = user.UserId,
            Username = user.Username,
            DisplayName = user.DisplayName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            RoleId = user.UserRole.RoleId,
            RoleName = user.UserRole.RoleName,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            JoinedProjectCount = projectCounts.TryGetValue(user.UserId, out var count)
                ? count
                : 0
        };
    }

    private static UserResponse MapToResponse(User user, int projectCount)
    {
        return new UserResponse
        {
            UserId = user.UserId,
            Username = user.Username,
            DisplayName = user.DisplayName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            RoleId = user.UserRole.RoleId,
            RoleName = user.UserRole.RoleName,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            JoinedProjectCount = projectCount
        };
    }
}
