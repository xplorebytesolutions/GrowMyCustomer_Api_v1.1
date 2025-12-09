using System;
using Microsoft.AspNetCore.Mvc;

namespace xbytechat.api.Shared
{
    // Do NOT add [ApiController] here; keep it on concrete controllers.
    public abstract class BusinessControllerBase : ControllerBase
    {
        protected Guid BusinessId => User.GetBusinessId();
        protected Guid UserId => User.GetUserId();
    }
}
