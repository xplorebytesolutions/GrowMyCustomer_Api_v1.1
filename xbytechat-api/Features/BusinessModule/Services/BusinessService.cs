using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using xbytechat.api.AuthModule.Models;
using xbytechat.api.Features.AccessControl.Models;
using xbytechat.api.Features.AuditTrail.Models;
using xbytechat.api.Features.AuditTrail.Services;
using xbytechat.api.Features.BusinessModule.DTOs;
using xbytechat.api.Features.BusinessModule.Models;
using xbytechat.api.Features.PlanManagement.Models;
using xbytechat.api.Helpers;
using xbytechat.api.Models.BusinessModel;
using xbytechat.api.Repositories.Interfaces;

namespace xbytechat.api.Features.BusinessModule.Services
{
    public class BusinessService : IBusinessService
    {
        private readonly IGenericRepository<Business> _businessRepo;
        private readonly IGenericRepository<User> _userRepo;
        private readonly IGenericRepository<Role> _roleRepo;
        private readonly IAuditLogService _auditLogService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        // inside BusinessService class (class scope, not inside a method)
        private static readonly Guid BASIC_PLAN_ID = Guid.Parse("5f9f5de1-a0b2-48ba-b03d-77b27345613f");
        private readonly IPasswordHasher<User> _passwordHasher;

        private readonly AppDbContext _db;
        public BusinessService(
            AppDbContext db,
            IGenericRepository<Business> businessRepo,
            IGenericRepository<User> userRepo,
            IGenericRepository<Role> roleRepo,
            IAuditLogService auditLogService,
            IHttpContextAccessor httpContextAccessor, IPasswordHasher<User> passwordHasher)
        {
            _db = db;
            _passwordHasher = passwordHasher;
            _businessRepo = businessRepo;
            _userRepo = userRepo;
            _roleRepo = roleRepo;
            _auditLogService = auditLogService;
            _httpContextAccessor = httpContextAccessor;
        }

        //public async Task<ResponseResult> SignupBusinessAsync(SignupBusinessDto dto)
        //{
        //    var normalizedEmail = dto.Email.Trim().ToLower();
        //    var existing = await _userRepo.FirstOrDefaultAsync(u => u.Email == normalizedEmail);
        //    if (existing != null)
        //        return ResponseResult.ErrorInfo("❌ Email already exists");

        //    var business = new Business
        //    {
        //        Id = Guid.NewGuid(),
        //        CompanyName = dto.CompanyName,
        //        BusinessName = dto.CompanyName,
        //        BusinessEmail = normalizedEmail,
        //        RepresentativeName = dto.RepresentativeName,
        //        Phone = dto.Phone,
        //        CreatedByPartnerId = dto.CreatedByPartnerId,
        //        Status = Business.StatusType.Pending,
        //        // Plan = PlanType.Basic,
        //        IsApproved = false,
        //        CreatedAt = DateTime.UtcNow,
        //        PlanId = BASIC_PLAN_ID   // ✅ hard-code Basic plan here
        //    };
        //    // STEP 2: Create Plan Info separately
        //    var planInfo = new BusinessPlanInfo
        //    {
        //        BusinessId = business.Id,
        //        Plan = PlanType.Basic,
        //        TotalMonthlyQuota = 1000,
        //        RemainingMessages = 1000,
        //        QuotaResetDate = DateTime.UtcNow.AddMonths(1),
        //        WalletBalance = 0
        //    };
        //    // STEP 3: Link them
        //    business.BusinessPlanInfo = planInfo;
        //    // STEP 4: Save both
        //    await _businessRepo.AddAsync(business);
        //    await _businessRepo.SaveAsync();

        //    var role = await _roleRepo.FirstOrDefaultAsync(r => r.Name.ToLower() == dto.RoleName.Trim().ToLower());

        //    if (role == null)
        //        return ResponseResult.ErrorInfo("❌ Invalid role specified");

        //    var user = new User
        //    {
        //        Id = Guid.NewGuid(),
        //        Name = dto.CompanyName,
        //        Email = normalizedEmail,
        //        PasswordHash = HashPassword(dto.Password),
        //        //PasswordHash = _passwordHasher.HashPassword(null!, dto.Password),
        //        Role = role,
        //        Status = "Pending",
        //        BusinessId = business.Id
        //    };

        //    await _userRepo.AddAsync(user);
        //    await _userRepo.SaveAsync();

        //    await _auditLogService.SaveLogAsync(new AuditLog
        //    {
        //        BusinessId = business.Id,
        //        PerformedByUserId = user.Id,
        //        PerformedByUserName = user.Name,
        //        RoleAtTime = "business",
        //        ActionType = "business.signup",
        //        Description = $"New business signup: {business.CompanyName}",
        //        IPAddress = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString(),
        //        UserAgent = _httpContextAccessor.HttpContext?.Request?.Headers["User-Agent"].ToString()
        //    });

        //    return ResponseResult.SuccessInfo("✅ Signup successful. Pending approval.", new { BusinessId = business.Id });
        //}

        public async Task<ResponseResult> SignupBusinessAsync(SignupBusinessDto dto)
        {
            var normalizedEmail = dto.Email.Trim().ToLower();
            var existing = await _userRepo.FirstOrDefaultAsync(u => u.Email == normalizedEmail);
            if (existing != null)
                return ResponseResult.ErrorInfo("❌ Email already exists");

            var basicPlanId = Guid.Parse("5f9f5de1-a0b2-48ba-b03d-77b27345613f");
            var business = new Business
            {
                Id = Guid.NewGuid(),
                CompanyName = dto.CompanyName,
                BusinessName = dto.CompanyName,
                BusinessEmail = normalizedEmail,
                RepresentativeName = dto.RepresentativeName,
                Phone = dto.Phone,
                CreatedByPartnerId = dto.CreatedByPartnerId,
                Status = Business.StatusType.Pending,
                IsApproved = false,
                CreatedAt = DateTime.UtcNow,

                // ❌ IMPORTANT: no plan yet at signup
                PlanId = basicPlanId,              // if Guid?  (or just omit setting it)
                                            // BusinessPlanInfo = null   // will be created at plan-selection time
            };

            // Only save the business now, without plan info
            await _businessRepo.AddAsync(business);
            await _businessRepo.SaveAsync();

            var role = await _roleRepo.FirstOrDefaultAsync(
                r => r.Name.ToLower() == dto.RoleName.Trim().ToLower()
            );

            if (role == null)
                return ResponseResult.ErrorInfo("❌ Invalid role specified");

            var user = new User
            {
                Id = Guid.NewGuid(),
                Name = dto.CompanyName,
                Email = normalizedEmail,
                PasswordHash = HashPassword(dto.Password),
                Role = role,
                Status = "Pending",
                BusinessId = business.Id
            };

            await _userRepo.AddAsync(user);
            await _userRepo.SaveAsync();

            await _auditLogService.SaveLogAsync(new AuditLog
            {
                BusinessId = business.Id,
                PerformedByUserId = user.Id,
                PerformedByUserName = user.Name,
                RoleAtTime = "business",
                ActionType = "business.signup",
                Description = $"New business signup: {business.CompanyName}",
                IPAddress = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString(),
                UserAgent = _httpContextAccessor.HttpContext?.Request?.Headers["User-Agent"].ToString()
            });

            return ResponseResult.SuccessInfo(
                "✅ Signup successful. Pending approval.",
                new { BusinessId = business.Id }
            );
        }



        public async Task<ResponseResult> UpdateBusinessAsync(Guid businessId, UpdateBusinessDto dto)
        {
            if (dto == null)
                return ResponseResult.ErrorInfo("❌ Invalid business update payload.");

            var business = await _businessRepo.AsQueryable()
                .FirstOrDefaultAsync(b => b.Id == businessId && !b.IsDeleted);

            if (business == null)
                return ResponseResult.ErrorInfo("❌ Business not found.");

            // ---- Authorization: who is allowed to edit this business? ----

            var httpContext = _httpContextAccessor.HttpContext;
            var user = httpContext?.User;

            if (user == null || !user.Identity?.IsAuthenticated == true)
                return ResponseResult.ErrorInfo("❌ Unauthorized. Please login again.");

            var role =
                user.FindFirst(ClaimTypes.Role)?.Value ??
                user.FindFirst("role")?.Value ??
                user.FindFirst("roles")?.Value ??
                string.Empty;

            var userIdStr =
                user.FindFirst("id")?.Value ??
                user.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                user.FindFirst("sub")?.Value ??
                string.Empty;

            Guid.TryParse(userIdStr, out var userId);

            var roleLc = (role ?? string.Empty).ToLowerInvariant();
            var isAdmin = roleLc == "admin" || roleLc == "superadmin";
            var isPartner = roleLc == "partner";
            var isAssignedPartner = isPartner && business.CreatedByPartnerId.HasValue &&
                                    business.CreatedByPartnerId.Value == userId;

            var isBusinessUser = false;
            if (userId != Guid.Empty)
            {
                var userEntity = await _userRepo.AsQueryable()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (userEntity != null && userEntity.BusinessId == business.Id)
                    isBusinessUser = true;
            }

            if (!isAdmin && !isAssignedPartner && !isBusinessUser)
            {
                return ResponseResult.ErrorInfo("⛔ You are not allowed to update this business.");
            }

            // ---- Apply only allowed fields ----

            if (!string.IsNullOrWhiteSpace(dto.CompanyName))
                business.CompanyName = dto.CompanyName.Trim();

            if (!string.IsNullOrWhiteSpace(dto.BusinessName))
                business.BusinessName = dto.BusinessName.Trim();

            if (!string.IsNullOrWhiteSpace(dto.BusinessEmail))
                business.BusinessEmail = dto.BusinessEmail.Trim().ToLowerInvariant();

            if (!string.IsNullOrWhiteSpace(dto.Phone))
                business.Phone = dto.Phone.Trim();

            if (!string.IsNullOrWhiteSpace(dto.CompanyPhone))
                business.CompanyPhone = dto.CompanyPhone.Trim();

            if (!string.IsNullOrWhiteSpace(dto.Website))
                business.Website = dto.Website.Trim();

            if (!string.IsNullOrWhiteSpace(dto.Address))
                business.Address = dto.Address.Trim();

            if (!string.IsNullOrWhiteSpace(dto.Industry))
                business.Industry = dto.Industry.Trim();

            if (!string.IsNullOrWhiteSpace(dto.LogoUrl))
                business.LogoUrl = dto.LogoUrl.Trim();

            if (!string.IsNullOrWhiteSpace(dto.Tags))
                business.Tags = dto.Tags.Trim();

            if (!string.IsNullOrWhiteSpace(dto.Notes))
                business.Notes = dto.Notes.Trim();

            // ❌ DO NOT touch:
            // - business.Status
            // - business.IsApproved
            // - business.PlanId
            // - business.BusinessPlanInfo
            // - business.CreatedByPartnerId
            // - deletion flags
            // These are controlled only by dedicated admin/partner flows.

            try
            {
                _businessRepo.Update(business);
                await _businessRepo.SaveAsync();
                return ResponseResult.SuccessInfo("✅ Business updated successfully.");
            }
            catch (Exception ex)
            {
                return ResponseResult.ErrorInfo("❌ Failed to update business: " + ex.Message);
            }
        }


        public async Task<List<PendingBusinessDto>> GetPendingBusinessesAsync(string role, string userId, string? status = null)
        {
            try
            {
                var roleLc = (role ?? "").ToLowerInvariant();

                // base: not deleted
                IQueryable<Business> q = _businessRepo.AsQueryable()
                    .AsNoTracking()
                    .Include(b => b.BusinessPlanInfo)
                    .Where(b => !b.IsDeleted);

                // Filtering by status
                if (!string.IsNullOrEmpty(status))
                {
                    var statusLc = status.ToLowerInvariant();
                    if (statusLc == "pending")
                        q = q.Where(b => b.Status == Business.StatusType.Pending);
                    else if (statusLc == "rejected" || statusLc == "reject")
                        q = q.Where(b => b.Status == Business.StatusType.Rejected);
                    else if (statusLc == "hold" || statusLc == "on-hold" || statusLc == "on_hold")
                        q = q.Where(b => b.Status == Business.StatusType.Hold);
                    else if (statusLc == "approved")
                        q = q.Where(b => b.Status == Business.StatusType.Approved);
                }
                else
                {
                    // Default to Pending if no status filter provided
                    q = q.Where(b => b.Status == Business.StatusType.Pending);
                }

                // scope for partner
                if (roleLc == "partner")
                {
                    if (!Guid.TryParse(userId, out var partnerId)) return new();
                    q = q.Where(b => b.CreatedByPartnerId == partnerId);
                }
                else if (roleLc != "admin" && roleLc != "superadmin")
                {
                    return new(); // anyone else: nothing
                }

                var items = await q.OrderByDescending(b => b.CreatedAt).ToListAsync();

                // Map to your existing DTO
                return items.Select(b => new PendingBusinessDto
                {
                    BusinessId = b.Id,
                    CompanyName = b.CompanyName ?? "",
                    BusinessEmail = b.BusinessEmail ?? "",
                    RepresentativeName = b.RepresentativeName ?? "",
                    Phone = b.Phone ?? "",
                    Plan = b.BusinessPlanInfo?.Plan.ToString() ?? "Unknown",
                    CreatedAt = b.CreatedAt,
                    IsApproved = b.IsApproved,
                    Status = b.Status.ToString(),
                    ApprovedAt = b.ApprovedAt
                }).ToList();
            }
            catch
            {
                return new();
            }
        }

        public async Task<ResponseResult> ApproveBusinessAsync(Guid businessId)
        {
            var business = await _businessRepo
                .AsQueryable()
                .Include(b => b.Users)
                .FirstOrDefaultAsync(b => b.Id == businessId);

            if (business == null)
                return ResponseResult.ErrorInfo("❌ Business not found.");

            // ✅ Current Logged-in User Details
            var httpContext = _httpContextAccessor.HttpContext;
            var currentUserId = httpContext?.User?.FindFirst("id")?.Value;
            var currentUserRole = httpContext?.User?.Claims
    .FirstOrDefault(c => c.Type.Contains("role"))?.Value;
            //httpContext?.User?.FindFirst("role")?.Value;

            // var currentUserName = httpContext?.User?.FindFirst("name")?.Value ?? "Unknown";
            var currentUserName = httpContext?.User?.Claims
    .FirstOrDefault(c => c.Type.Contains("name"))?.Value ?? "Unknown";
            if (string.IsNullOrEmpty(currentUserId) || string.IsNullOrEmpty(currentUserRole))
                return ResponseResult.ErrorInfo("❌ Unauthorized access. Please login again.");

            var currentGuid = Guid.Parse(currentUserId);

            // ✅ Authorization Logic
            var isSuperAdmin = currentUserRole.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
                               currentUserRole.Equals("superadmin", StringComparison.OrdinalIgnoreCase);

            var isAssignedPartner = business.CreatedByPartnerId.HasValue &&
                                     business.CreatedByPartnerId.Value == currentGuid;

            if (!isSuperAdmin && !isAssignedPartner)
            {
                return ResponseResult.ErrorInfo("⛔ You are not authorized to approve this business.");
            }

            // ✅ Approve Business

            business.IsApproved = true;
            business.Status = Business.StatusType.Approved;
            business.ApprovedAt = DateTime.UtcNow;
            business.ApprovedBy = currentUserName;
            _businessRepo.Update(business);

            // ✅ Update all Users to "ProfilePending"
            foreach (var user in business.Users)
            {
                user.Status = "Active";
                _userRepo.Update(user);
            }

            await _businessRepo.SaveAsync();
            await _userRepo.SaveAsync();

            // ✅ Audit Log
            await _auditLogService.SaveLogAsync(new AuditLog
            {
                BusinessId = business.Id,
                PerformedByUserId = currentGuid,
                PerformedByUserName = currentUserName,
                RoleAtTime = currentUserRole,
                ActionType = "business.approved",
                Description = $"Business approved: {business.CompanyName}",
                IPAddress = httpContext?.Connection?.RemoteIpAddress?.ToString(),
                UserAgent = httpContext?.Request?.Headers["User-Agent"].ToString()
            });

            return ResponseResult.SuccessInfo("✅ Business approved successfully.");
        }

        public async Task<ResponseResult> RejectBusinessAsync(Guid businessId)
        {
            var business = await _businessRepo.FindByIdAsync(businessId);
            if (business is null)
                return ResponseResult.ErrorInfo("❌ Business not found");

            business.Status = Business.StatusType.Rejected;
            business.IsApproved = false;
            // business.IsDeleted = true; // removed soft-delete to allow showing in Rejected tab
            business.DeletedAt = null;

            _businessRepo.Update(business);
            await _businessRepo.SaveAsync();

            await _auditLogService.SaveLogAsync(new AuditLog
            {
                BusinessId = business.Id,
                PerformedByUserId = Guid.TryParse(_httpContextAccessor.HttpContext?.User?.FindFirst("id")?.Value, out var userId) ? userId : Guid.Empty,
                PerformedByUserName = _httpContextAccessor.HttpContext?.User?.FindFirst("email")?.Value,
                RoleAtTime = _httpContextAccessor.HttpContext?.User?.FindFirst("role")?.Value,
                ActionType = "business.rejected",
                Description = $"Business rejected: {business.CompanyName}",
                IPAddress = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString(),
                UserAgent = _httpContextAccessor.HttpContext?.Request?.Headers["User-Agent"].ToString()
            });

            return ResponseResult.SuccessInfo("✅ Business rejected and marked as deleted");
        }

        public async Task<ResponseResult> HoldBusinessAsync(Guid businessId)
        {
            var business = await _businessRepo.FindByIdAsync(businessId);
            if (business is null)
                return ResponseResult.ErrorInfo("❌ Business not found");

            business.IsApproved = false;
            business.Status = Business.StatusType.Hold;

            _businessRepo.Update(business);
            await _businessRepo.SaveAsync();

            await _auditLogService.SaveLogAsync(new AuditLog
            {
                BusinessId = business.Id,
                PerformedByUserId = Guid.TryParse(_httpContextAccessor.HttpContext?.User?.FindFirst("id")?.Value, out var userId) ? userId : Guid.Empty,
                PerformedByUserName = _httpContextAccessor.HttpContext?.User?.FindFirst("email")?.Value,
                RoleAtTime = _httpContextAccessor.HttpContext?.User?.FindFirst("role")?.Value,
                ActionType = "business.hold",
                Description = $"Business put on hold: {business.CompanyName}",
                IPAddress = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString(),
                UserAgent = _httpContextAccessor.HttpContext?.Request?.Headers["User-Agent"].ToString()
            });

            return ResponseResult.SuccessInfo("⏸ Business put on hold");
        }

        public async Task<ResponseResult> CompleteProfileAsync(Guid businessId, ProfileCompletionDto dto)
        {
            var business = await _businessRepo.FindByIdAsync(businessId);
            if (business is null)
                return ResponseResult.ErrorInfo("❌ Business not found");

            if (!string.IsNullOrEmpty(dto.BusinessName)) business.BusinessName = dto.BusinessName;
            if (!string.IsNullOrEmpty(dto.CompanyPhone)) business.CompanyPhone = dto.CompanyPhone;
            if (!string.IsNullOrEmpty(dto.Website)) business.Website = dto.Website;
            if (!string.IsNullOrEmpty(dto.Address)) business.Address = dto.Address;
            if (!string.IsNullOrEmpty(dto.Industry)) business.Industry = dto.Industry;
            if (!string.IsNullOrEmpty(dto.LogoUrl)) business.LogoUrl = dto.LogoUrl;
            if (!string.IsNullOrEmpty(dto.ReperesentativeName)) business.RepresentativeName = dto.ReperesentativeName;
            if (!string.IsNullOrEmpty(dto.Phone)) business.Phone = dto.Phone;
            _businessRepo.Update(business);
            await _businessRepo.SaveAsync();
            return ResponseResult.SuccessInfo("✅ Profile updated successfully");
        }

        public async Task<Business?> GetBusinessByEmailAsync(string email)
        {
            return await _businessRepo.FirstOrDefaultAsync(b => b.BusinessEmail.ToLower() == email.Trim().ToLower());
        }

        private string HashPassword(string password)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = sha.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        public async Task<Business?> GetByIdAsync(Guid businessId)
        {
            return await _businessRepo.FindByIdAsync(businessId);
        }

        public async Task<List<Business>> GetApprovedBusinessesAsync()
        {
            return await _businessRepo.AsQueryable()
               .Where(b => b.IsApproved && !b.IsDeleted)
               .OrderBy(b => b.CompanyName)
               .ToListAsync();
        }
        public async Task<ResponseResult> HardDeleteBusinessAsync(Guid businessId)
        {
            try
            {
                var business = await _businessRepo.AsQueryable()
                    .Include(b => b.Users)
                    .Include(b => b.BusinessPlanInfo)
                    .FirstOrDefaultAsync(b => b.Id == businessId);

                if (business == null)
                    return ResponseResult.ErrorInfo("❌ Business not found");

                // Audit log before deletion
                await _auditLogService.SaveLogAsync(new AuditLog
                {
                    BusinessId = business.Id,
                    ActionType = "business.hard_deleted",
                    Description = $"Business permanently deleted: {business.CompanyName}",
                    IPAddress = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString(),
                    UserAgent = _httpContextAccessor.HttpContext?.Request?.Headers["User-Agent"].ToString()
                });

                // Remove related users first
                if (business.Users != null && business.Users.Any())
                {
                    foreach (var user in business.Users.ToList())
                    {
                        _userRepo.Delete(user);
                    }
                    await _userRepo.SaveAsync();
                }

                if (business.BusinessPlanInfo != null)
                {
                    _db.BusinessPlanInfos.Remove(business.BusinessPlanInfo);
                }

                _businessRepo.Delete(business);
                await _businessRepo.SaveAsync();

                return ResponseResult.SuccessInfo("🗑️ Business permanently deleted");
            }
            catch (Exception ex)
            {
                return ResponseResult.ErrorInfo("❌ Failed to delete business: " + ex.Message);
            }
        }

        public IQueryable<Business> Query()
        {
            return _businessRepo.AsQueryable();
        }
    }
}
