//using System.Collections.Generic;
//using System.Threading.Tasks;
//using xbytechat.api.Features.AccessControl.Models;

//namespace xbytechat.api.Features.AccessControl.Services
//{
//    public interface IAccessControlService
//    {
//        Task<IEnumerable<Permission>> GetAllPermissionsAsync();
//        Task<IEnumerable<Permission>> GetPermissionsAsync(Guid userId);

//    }
//}


using System.Security.Claims;
using xbytechat.api.Features.AccessControl.Models;

namespace xbytechat.api.Features.AccessControl.Services
{
    public interface IAccessControlService
    {
        Task<List<string>> GetPermissionsAsync(Guid userId);
        bool HasPermission(ClaimsPrincipal user, string permission);
        Task<IEnumerable<Permission>> GetAllPermissionsAsync();
        Task<List<string>> GetPermissionsByPlanIdAsync(Guid? planId);
    }
}
