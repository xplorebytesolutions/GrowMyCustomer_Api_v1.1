//using Microsoft.EntityFrameworkCore;
//using System.Collections.Generic;
//using System.Security.Claims;
//using System.Threading.Tasks;

//using xbytechat.api.Features.AccessControl.Models;

//namespace xbytechat.api.Features.AccessControl.Services
//{
//    public class AccessControlService : IAccessControlService
//    {
//        private readonly AppDbContext _context;

//        public AccessControlService(AppDbContext context)
//        {
//            _context = context;
//        }

//        public async Task<IEnumerable<Permission>> GetAllPermissionsAsync()
//        {
//            return await _context.Permissions
//                .AsNoTracking()
//                .Where(p => p.IsActive)
//                .ToListAsync();
//        }
//        //public async Task<IEnumerable<Permission>> GetPermissionsAsync(Guid userId)
//        //{
//        //    // First, check if the user has direct permissions
//        //    var userPermissions = await _context.UserPermissions
//        //        .Where(up => up.UserId == userId && up.IsGranted && !up.IsRevoked)
//        //        .Select(up => up.Permission)
//        //        .Where(p => p.IsActive)
//        //        .ToListAsync();

//        //    // If no direct permissions, fall back to role permissions
//        //    if (!userPermissions.Any())
//        //    {
//        //        userPermissions = await _context.RolePermissions
//        //            .Where(rp => rp.Role.Users.Any(u => u.Id == userId) && rp.IsActive && !rp.IsRevoked)
//        //            .Select(rp => rp.Permission)
//        //            .Where(p => p.IsActive)
//        //            .ToListAsync();
//        //    }

//        //    return userPermissions;
//        //}
       
        
       
//    }
//}


using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using xbytechat.api.AuthModule.Models;
using xbytechat.api.Features.AccessControl.Models;
using xbytechat.api.Repositories.Interfaces;
using System.Linq.Expressions;


namespace xbytechat.api.Features.AccessControl.Services
{
    public class AccessControlService : IAccessControlService
    {
        private readonly IGenericRepository<User> _userRepo;
        private readonly IGenericRepository<RolePermission> _rolePermissionRepo;
        private readonly IGenericRepository<UserPermission> _userPermissionRepo;
        private readonly IGenericRepository<Permission> _permissionRepo;
        private readonly AppDbContext _context;
        public AccessControlService(
            IGenericRepository<User> userRepo,
            IGenericRepository<RolePermission> rolePermissionRepo,
            IGenericRepository<UserPermission> userPermissionRepo,
            IGenericRepository<Permission> permissionRepo, AppDbContext context
        )
        {
            _userRepo = userRepo;
            _rolePermissionRepo = rolePermissionRepo;
            _userPermissionRepo = userPermissionRepo;
            _permissionRepo = permissionRepo;
            _context = context;
        }

        /// <summary>
        /// ✅ Fetch all permissions (Role-based + User-specific) for a given user
        /// </summary>
        //public async Task<List<string>> GetPermissionsAsync(Guid userId)
        //{
        //    var user = await _userRepo.FindByIdAsync(userId);
        //    if (user == null || user.RoleId == null)
        //        return new List<string>();

        //    // 🔐 Get Role-based permissions
        //    var rolePerms = await _rolePermissionRepo
        //        .WhereAsync(rp => rp.RoleId == user.RoleId && !rp.IsRevoked);

        //    // 🔐 Get User-specific extra permissions
        //    var userPerms = await _userPermissionRepo
        //        .WhereAsync(up => up.UserId == userId && !up.IsRevoked);

        //    // 🧠 Merge permission IDs
        //    var permissionIds = rolePerms.Select(r => r.PermissionId)
        //        .Union(userPerms.Select(u => u.PermissionId))
        //        .Distinct()
        //        .ToList();

        //    // 🎯 Get full permission names from Permission table
        //    var allPerms = await _permissionRepo
        //        .WhereAsync(p => permissionIds.Contains(p.Id));

        //    return allPerms.Select(p => p.Code).Distinct().ToList(); // Use Code (standard)
        //}

        public async Task<IEnumerable<Permission>> GetAllPermissionsAsync()
        {
            return await _permissionRepo.WhereAsync(p => p.IsActive);
        }


        public async Task<List<string>> GetPermissionsAsync(Guid userId)
        {
            var user = await _userRepo.FindByIdAsync(userId);

            if (user == null || user.RoleId == null)
                return new List<string>();

            // 🚀 Bypass: SuperAdmin always gets full access
            if (user.Role != null && user.Role.Name.Equals("superadmin", StringComparison.OrdinalIgnoreCase))
            {
                var allPerms = await _permissionRepo.GetAllAsync();
                return allPerms.Select(p => p.Code).Distinct().ToList();
            }

            // 🔐 Get Role-based permissions
            var rolePerms = await _rolePermissionRepo
                .WhereAsync(rp => rp.RoleId == user.RoleId && !rp.IsRevoked);

            // 🔐 Get User-specific extra permissions
            var userPerms = await _userPermissionRepo
                .WhereAsync(up => up.UserId == userId && !up.IsRevoked);

            // 🧠 Merge permission IDs
            var permissionIds = rolePerms.Select(r => r.PermissionId)
                .Union(userPerms.Select(u => u.PermissionId))
                .Distinct()
                .ToList();

            // 🎯 Get full permission names from Permission table
            var allAllowedPerms = await _permissionRepo
                .WhereAsync(p => permissionIds.Contains(p.Id));

            return allAllowedPerms.Select(p => p.Code).Distinct().ToList();
        }

        public bool HasPermission(ClaimsPrincipal user, string requiredPermission)
        {
            // 🚀 Bypass: SuperAdmin always passes
            //var roleClaim = user.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
            var roleClaim = user.Claims.FirstOrDefault(c =>
                c.Type == ClaimTypes.Role || c.Type.Equals("role", StringComparison.OrdinalIgnoreCase)
            )?.Value;

            if (!string.IsNullOrEmpty(roleClaim) && roleClaim.Equals("superadmin", StringComparison.OrdinalIgnoreCase))
                return true;

            var perms = user.Claims
                .Where(c => c.Type == "permissions")
                .Select(c => c.Value)
                .ToList();

            return perms.Contains(requiredPermission);
        }

        /// <summary>
        /// ✅ Runtime permission checker (for controller/middleware)
        /// </summary>
        //public bool HasPermission(ClaimsPrincipal user, string requiredPermission)
        //{
        //    var perms = user.Claims
        //        .Where(c => c.Type == "permissions")
        //        .Select(c => c.Value)
        //        .ToList();

        //    return perms.Contains(requiredPermission);
        //}

        public async Task<List<string>> GetPermissionsByPlanIdAsync(Guid? planId)
        {
            if (!planId.HasValue)
                return new List<string>();

            return await _context.PlanPermissions
                .Where(pp => pp.PlanId == planId.Value && pp.IsActive)
                .Select(pp => pp.Permission.Code)
                .ToListAsync();
        }


    }
}
