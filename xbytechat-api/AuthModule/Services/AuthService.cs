using System.Security.Cryptography;
using System.Text;
using xbytechat.api.AuthModule.DTOs;
using xbytechat.api.AuthModule.Models;
using xbytechat.api.Helpers;
using xbytechat.api.Repositories.Interfaces;
using xbytechat.api.Features.AccessControl.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using xbytechat.api.Features.BusinessModule.DTOs;
using xbytechat.api.Features.BusinessModule.Models;
using xbytechat.api.Features.BusinessModule.Services;

using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Logging;

namespace xbytechat.api.AuthModule.Services
{
    public class AuthService : IAuthService
    {
        private readonly IGenericRepository<User> _userRepo;
        private readonly IBusinessService _businessService;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly IAccessControlService _accessControlService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AuthService> _logger;
        private readonly AppDbContext _dbContext;
        public AuthService(
            IGenericRepository<User> userRepo,
            IBusinessService businessService,
            IJwtTokenService jwtTokenService,
            IAccessControlService accessControlService,
            IHttpContextAccessor httpContextAccessor,
            ILogger<AuthService> logger,
            AppDbContext dbContext)
        {
            _userRepo = userRepo;
            _businessService = businessService;
            _jwtTokenService = jwtTokenService;
            _accessControlService = accessControlService;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _dbContext = dbContext;
        }


        public async Task<ResponseResult> LoginAsync(UserLoginDto dto)
        {
            _logger.LogInformation("🔑 Login attempt for email: {Email}", dto.Email);
            var hashedPassword = HashPassword(dto.Password);

            var user = await _userRepo
                .AsQueryable()
                .Where(u => u.Email == dto.Email && u.PasswordHash == hashedPassword && !u.IsDeleted)
                .Include(u => u.Role)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                _logger.LogWarning("❌ Login failed: Invalid email or password for {Email}", dto.Email);
                return ResponseResult.ErrorInfo("❌ Invalid email or password");
            }

            var roleName = user.Role?.Name?.Trim().ToLower() ?? "unknown";
            var isAdminType = roleName is "admin" or "superadmin" or "partner" or "reseller";

            // ✅ NEW: Block deactivated/non-active users (important for TeamStaff)
            if (!isAdminType)
            {
                var st = (user.Status ?? "").Trim().ToLowerInvariant();

                // You can add/remove statuses here as per your system rules
                if (st is "hold" or "inactive" or "rejected" or "pending")
                {
                    _logger.LogWarning("🚫 Login blocked due to user status. Email={Email}, Status={Status}", dto.Email, user.Status);
                    return ResponseResult.ErrorInfo("❌ Your account is not active. Please contact your administrator.");
                }
            }

            if (user.BusinessId == null && !isAdminType)
            {
                _logger.LogWarning("❌ Login denied for {Email}: No BusinessId and not admin", dto.Email);
                return ResponseResult.ErrorInfo("❌ Your account approval is pending. Please contact your administrator or support.");
            }

            Business? business = null;

            // This is the REAL plan id from DB (can be internal)
            Guid? planId = null;

            // This is what we expose to the FRONTEND (hide internal plans)
            Guid? exposedPlanId = null;

            string companyName = string.Empty;
            string businessId = user.BusinessId?.ToString() ?? string.Empty;

            if (user.BusinessId != null)
            {
                business = await _businessService.Query()
                    .Include(b => b.Plan) // ensure Plan loaded so we can check IsInternal
                    .FirstOrDefaultAsync(b => b.Id == user.BusinessId.Value);

                if (business == null)
                    return ResponseResult.ErrorInfo("❌ Associated business not found.");

                if (business.Status == Business.StatusType.Pending)
                    return ResponseResult.ErrorInfo("⏳ Your business is under review. Please wait for approval.");

                planId = business.PlanId;
                companyName = business.CompanyName ?? string.Empty;

                // 🧠 Decide whether to expose plan to frontend
                var isInternalPlan = business.Plan?.IsInternal == true;
                exposedPlanId = isInternalPlan ? null : planId;

                _logger.LogInformation(
                    "Business {BusinessId} login. Status: {Status}, DbPlanId: {DbPlanId}, ExposedPlanId: {ExposedPlanId}, IsInternal: {IsInternal}",
                    business.Id, business.Status, planId, exposedPlanId, isInternalPlan
                );
            }

            if (isAdminType)
            {
                // Admins don’t get plan restrictions and shouldn't show a plan in UI
                companyName = "xByte Admin";
                businessId = string.Empty;
                planId = null;
                exposedPlanId = null;
            }

            // 🔥 Compute EFFECTIVE permissions (plan ∩ role) and derive features
            var (permCodes, featureKeys) = isAdminType
                ? (await GetAllActivePermissions(), new List<string> { "Dashboard", "Messaging", "CRM", "Campaigns", "Catalog", "AdminPanel" })
                : await GetEffectivePermissionsAndFeaturesAsync(user.Id);

            // 🎫 Generate JWT → use EXPOSED plan id (hide internal/system plans)
            var token = _jwtTokenService.GenerateToken(
                userId: user.Id.ToString(),
                role: roleName,
                userName: user.Name ?? string.Empty,
                email: user.Email ?? string.Empty,
                status: user.Status ?? "unknown",
                businessId: businessId,
                companyName: companyName,
                permissions: permCodes ?? new List<string>(),
                planId: exposedPlanId?.ToString() ?? string.Empty,
                features: featureKeys,
                hasAllAccess: isAdminType
            );

            try
            {
                var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
                var pid = jwt.Claims.FirstOrDefault(c => c.Type == "plan_id")?.Value;
                _logger.LogInformation("🔎 JWT includes plan_id (exposed): {PlanId}", pid ?? "<null>");
            }
            catch
            {
                // ignore
            }

            var userDto = new UserDto
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Role = roleName,
                Status = user.Status,
                CreatedAt = user.CreatedAt,
                BusinessId = string.IsNullOrEmpty(businessId) ? Guid.Empty : Guid.Parse(businessId),
                CompanyName = companyName,

                // 👇 important: return ONLY the exposed plan id to the frontend
                PlanId = exposedPlanId,

                AccessToken = null
            };

            _logger.LogInformation(
                "✅ Login successful for {Email}, Role: {Role}, DbPlanId: {DbPlanId}, ExposedPlanId: {ExposedPlanId}",
                dto.Email, roleName, planId, exposedPlanId
            );

            return new ResponseResult
            {
                Success = true,
                Message = "✅ Login successful",
                Data = userDto,
                Token = token
            };
        }

        public async Task<ResponseResult> SignupAsync(SignupBusinessDto dto)
        {
            _logger.LogInformation("🟢 Signup attempt: {Email}", dto.Email);
            var result = await _businessService.SignupBusinessAsync(dto);

            if (!result.Success)
            {
                _logger.LogWarning("❌ Signup failed for {Email}: {Msg}", dto.Email, result.Message);
                return ResponseResult.ErrorInfo(result.Message);
            }

            var business = await _businessService.GetBusinessByEmailAsync(dto.Email);

            if (business == null)
            {
                _logger.LogError("❌ Signup succeeded but business retrieval failed for {Email}", dto.Email);
                return ResponseResult.ErrorInfo("❌ Signup succeeded but business retrieval failed.");
            }

            // No extra UpdateBusinessAsync call here.
            // CreatedByPartnerId is set inside SignupBusinessAsync.

            _logger.LogInformation("✅ Signup successful for {Email} (BusinessId: {BusinessId})", dto.Email, business.Id);
            return ResponseResult.SuccessInfo("✅ Signup successful. Pending approval.", new { BusinessId = business.Id });
        }

        public async Task<ResponseResult> RefreshTokenAsync(string refreshToken)
        {
            _logger.LogInformation("🔄 RefreshToken attempt");

            var user = await _userRepo
                .AsQueryable()
                .Include(u => u.Role)
                .Include(u => u.Business)
                    .ThenInclude(b => b.BusinessPlanInfo)
                .Include(u => u.Business)
                    .ThenInclude(b => b.Plan) // 👈 ensure Plan is loaded
                .FirstOrDefaultAsync(u =>
                    u.RefreshToken == refreshToken &&
                    u.RefreshTokenExpiry > DateTime.UtcNow
                );

            if (user == null)
            {
                _logger.LogWarning("❌ Invalid or expired refresh token.");
                return ResponseResult.ErrorInfo("❌ Invalid or expired refresh token.");
            }

            var roleName = user.Role?.Name?.Trim().ToLower() ?? "unknown";
            var isAdminType = roleName is "admin" or "superadmin" or "partner" or "reseller";

            // ✅ NEW: Block refresh for non-active users (prevents silent re-login for disabled staff)
            if (!isAdminType)
            {
                var st = (user.Status ?? "").Trim().ToLowerInvariant();
                if (st is "hold" or "inactive" or "rejected" or "pending")
                {
                    _logger.LogWarning("🚫 RefreshToken blocked due to user status. UserId={UserId}, Email={Email}, Status={Status}",
                        user.Id, user.Email, user.Status);

                    return ResponseResult.ErrorInfo("❌ Your account is not active. Please contact your administrator.");
                }
            }

            string companyName;
            string businessId;

            // REAL plan from DB
            Guid? dbPlanId = user.Business?.PlanId;

            // What we expose to frontend (null for internal plans)
            string? exposedPlanIdForClaim = null;

            if (isAdminType)
            {
                companyName = "xByte Admin";
                businessId = string.Empty;
                dbPlanId = null;
                exposedPlanIdForClaim = null;
            }
            else
            {
                companyName = user.Business?.CompanyName ?? string.Empty;
                businessId = user.BusinessId?.ToString() ?? string.Empty;

                if (dbPlanId.HasValue)
                {
                    var isInternalPlan = user.Business?.Plan?.IsInternal == true;
                    if (!isInternalPlan)
                    {
                        exposedPlanIdForClaim = dbPlanId.Value.ToString();
                    }

                    _logger.LogInformation(
                        "🔄 RefreshToken: Business {BusinessId}, DbPlanId: {DbPlanId}, ExposedPlanId: {ExposedPlanId}, IsInternal: {IsInternal}",
                        businessId, dbPlanId, exposedPlanIdForClaim, isInternalPlan
                    );
                }
            }

            var (permCodes, featureKeys) = isAdminType
                ? (await GetAllActivePermissions(), new List<string> { "Dashboard", "Messaging", "CRM", "Campaigns", "Catalog", "AdminPanel" })
                : await GetEffectivePermissionsAndFeaturesAsync(user.Id);

            var claims = new List<Claim>
    {
        new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
        new Claim("id", user.Id.ToString()),
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim("email", user.Email ?? ""),
        new Claim("name", user.Name ?? ""),
        new Claim("status", user.Status ?? "unknown"),
        new Claim("businessId", businessId),
        new Claim("companyName", companyName),
        new Claim("permissions", string.Join(",", permCodes ?? new List<string>())),
        new Claim("features", string.Join(",", featureKeys ?? new List<string>())),
        new Claim("hasAllAccess", isAdminType ? "true" : "false"),
        new Claim("role", roleName),
        new Claim(ClaimTypes.Role, roleName)
    };

            // 👉 Only add plan_id claim if we want the frontend to see it
            if (!string.IsNullOrWhiteSpace(exposedPlanIdForClaim))
            {
                claims.Add(new Claim("plan_id", exposedPlanIdForClaim));
            }

            var token = _jwtTokenService.GenerateToken(claims);

            // 🔁 Rotate refresh token
            var newRefreshToken = Guid.NewGuid().ToString("N");
            user.RefreshToken = newRefreshToken;
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(30);
            _userRepo.Update(user);

            _logger.LogInformation(
                "🔄 Token refreshed for user {UserId}, role {Role}, DbPlanId: {DbPlanId}, ExposedPlanId: {ExposedPlanId}",
                user.Id, roleName, dbPlanId, exposedPlanIdForClaim
            );

            return ResponseResult.SuccessInfo("🔄 Token refreshed", new
            {
                accessToken = token,
                refreshToken = newRefreshToken
            });
        }




        public async Task<ResponseResult> ResendConfirmationAsync(ResendConfirmationDto dto)
        {
            _logger.LogInformation("🔁 Resend confirmation attempt for {Email}", dto.Email);
            var business = await _businessService.GetBusinessByEmailAsync(dto.Email);
            if (business == null)
            {
                _logger.LogWarning("❌ Resend confirmation failed: No business for {Email}", dto.Email);
                return ResponseResult.ErrorInfo("❌ No business registered with this email");
            }

            _logger.LogInformation("✅ Resend confirmation request processed for {Email}", dto.Email);
            return ResponseResult.SuccessInfo("📨 Confirmation request resent.");
        }

        // 🔒 Reset password
        public async Task<ResponseResult> ResetPasswordAsync(ResetPasswordDto dto)
        {
            _logger.LogInformation("🔒 Reset password attempt for {Email}", dto.Email);
            var user = await _userRepo.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null)
            {
                _logger.LogWarning("❌ Reset password failed: No user for {Email}", dto.Email);
                return ResponseResult.ErrorInfo("❌ No user found with this email");
            }

            user.PasswordHash = HashPassword(dto.NewPassword);
            _userRepo.Update(user);

            _logger.LogInformation("✅ Password reset successfully for {Email}", dto.Email);
            return ResponseResult.SuccessInfo("✅ Password reset successfully");
        }

        public async Task<ResponseResult> ChangePasswordAsync(Guid userId, ChangePasswordDto dto)
        {
            if (userId == Guid.Empty)
                return ResponseResult.ErrorInfo("Invalid user.");

            if (dto == null)
                return ResponseResult.ErrorInfo("Invalid request.");

            if (string.IsNullOrWhiteSpace(dto.CurrentPassword) || string.IsNullOrWhiteSpace(dto.NewPassword))
                return ResponseResult.ErrorInfo("Current password and new password are required.");

            if (dto.NewPassword.Length < 6)
                return ResponseResult.ErrorInfo("Password must be at least 6 characters long.");

            if (dto.NewPassword == dto.CurrentPassword)
                return ResponseResult.ErrorInfo("New password must be different.");

            var user = await _userRepo.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);
            if (user == null)
                return ResponseResult.ErrorInfo("User not found.");

            var currentHash = HashPassword(dto.CurrentPassword);
            if (!string.Equals(user.PasswordHash, currentHash, StringComparison.Ordinal))
            {
                _logger.LogWarning("⚠️ Change password failed: invalid current password. UserId={UserId}", userId);
                return ResponseResult.ErrorInfo("Current password is incorrect.");
            }

            user.PasswordHash = HashPassword(dto.NewPassword);

            // Rotate refresh token so existing refresh tokens become invalid.
            user.RefreshToken = Guid.NewGuid().ToString("N");
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(30);

            _userRepo.Update(user);
            await _userRepo.SaveAsync();
            _logger.LogInformation("✅ Password changed successfully. UserId={UserId}", userId);

            return ResponseResult.SuccessInfo("✅ Password updated successfully.");
        }

        private async Task<(List<string> Perms, List<string> Features)> GetEffectivePermissionsAndFeaturesAsync(Guid userId)
        {
            var user = await _dbContext.Users
                .Include(u => u.Role)
                .Include(u => u.Business)
                    .ThenInclude(b => b.Plan)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null || user.Business == null || user.Role == null)
                return (new List<string>(), new List<string>());

            var businessId = user.Business.Id;
            var planId = user.Business.PlanId;
            var roleId = user.Role.Id;

            // 1) Plan permissions (active)
            var planPerms = await _dbContext.PlanPermissions
                .Where(pp => pp.PlanId == planId && pp.IsActive)
                .Select(pp => pp.Permission.Code)
                .ToListAsync();

            var planEffectiveForBusiness = new HashSet<string>(planPerms, StringComparer.OrdinalIgnoreCase);

            // 2) Apply BUSINESS overrides (grant/deny)
            var now = DateTime.UtcNow;

            var bizOverrides = await _dbContext.BusinessPermissionOverrides
                .Where(o =>
                    o.BusinessId == businessId &&
                    !o.IsRevoked &&
                    (o.ExpiresAtUtc == null || o.ExpiresAtUtc > now))
                .Select(o => new { Code = o.Permission.Code, o.IsGranted })
                .ToListAsync();

            foreach (var o in bizOverrides)
            {
                if (string.IsNullOrWhiteSpace(o.Code)) continue;
                if (o.IsGranted) planEffectiveForBusiness.Add(o.Code);
                else planEffectiveForBusiness.Remove(o.Code);
            }

            // 3) Role permissions (active + not revoked)
            var rolePerms = await _dbContext.RolePermissions
                .Where(rp => rp.RoleId == roleId && rp.IsActive && !rp.IsRevoked)
                .Select(rp => rp.Permission.Code)
                .ToListAsync();

            var roleName = (user.Role.Name ?? "").Trim();

            // ✅ Business owner: do NOT require RolePermissions rows to exist.
            var isBusinessOwnerRole =
                roleName.Equals("business", StringComparison.OrdinalIgnoreCase) ||
                roleName.Equals("owner", StringComparison.OrdinalIgnoreCase);

            HashSet<string> effective;

            if (isBusinessOwnerRole)
            {
                effective = new HashSet<string>(planEffectiveForBusiness, StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                // Staff: must be within plan and within role
                effective = new HashSet<string>(
                    planEffectiveForBusiness.Intersect(rolePerms, StringComparer.OrdinalIgnoreCase),
                    StringComparer.OrdinalIgnoreCase
                );
            }

            // 4) Apply USER overrides (grant/deny)
            // NOTE: Grants should never exceed the plan ceiling. So only add if it's in planEffectiveForBusiness.
            var userOverrides = await _dbContext.UserPermissions
                .Where(up => up.UserId == userId && !up.IsRevoked)
                .Select(up => new { Code = up.Permission.Code, up.IsGranted })
                .ToListAsync();

            foreach (var ov in userOverrides)
            {
                if (string.IsNullOrWhiteSpace(ov.Code)) continue;

                if (ov.IsGranted)
                {
                    if (planEffectiveForBusiness.Contains(ov.Code))
                        effective.Add(ov.Code);
                }
                else
                {
                    effective.Remove(ov.Code);
                }
            }

            // 5) Map Permission.Group to feature keys (your current behavior)
            var featureKeys = await _dbContext.Permissions
                .Where(p => p.IsActive && effective.Contains(p.Code))
                .Select(p => p.Group)
                .Where(g => g != null && g != "")
                .Distinct()
                .ToListAsync();

            return (effective.OrderBy(x => x).ToList(), featureKeys!);
        }


        private async Task<List<string>> GetAllActivePermissions() =>
            await _dbContext.Permissions
                .Where(p => p.IsActive)
                .Select(p => p.Code)
                .OrderBy(c => c)
                .ToListAsync();

        private static string? GroupToFeature(string? g) => g switch
        {
            "Messaging" => "Messaging",
            "Contacts" => "CRM",
            "Campaign" => "Campaigns",
            "Product" => "Catalog",
            "Dashboard" => "Dashboard",
            "Admin" => "AdminPanel",
            _ => null
        };

        private string HashPassword(string password)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = sha.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }
    }
}

