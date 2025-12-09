using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using xbytechat.api.Features.AccessControl.Models;

namespace xbytechat.api.Features.AccessControl.Services
{
    public interface IPermissionCacheService
    {
        Task<List<Permission>> GetPlanPermissionsAsync(Guid planId);
        void ClearPlanPermissionsCache(Guid planId);
    }

    public class PermissionCacheService : IPermissionCacheService
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private const string CacheKeyPrefix = "plan_permissions_";

        public PermissionCacheService(AppDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<List<Permission>> GetPlanPermissionsAsync(Guid planId)
        {
            var cacheKey = $"{CacheKeyPrefix}{planId}";

            // Try to get from cache
            if (_cache.TryGetValue(cacheKey, out List<Permission> cachedPermissions))
                return cachedPermissions;

            // Fetch from DB
            var permissions = await _context.PlanPermissions
                .Where(pp => pp.PlanId == planId && pp.IsActive)
                .Select(pp => pp.Permission)
                .ToListAsync();

            // Store in cache
            _cache.Set(cacheKey, permissions, TimeSpan.FromHours(1));

            return permissions;
        }

        public void ClearPlanPermissionsCache(Guid planId)
        {
            _cache.Remove($"{CacheKeyPrefix}{planId}");
        }
    }
}
