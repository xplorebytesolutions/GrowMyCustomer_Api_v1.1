using System.Security.Claims;
using xbytechat.api.AuthModule.DTOs;
using xbytechat.api.Features.BusinessModule.DTOs;
using xbytechat.api.Helpers;

namespace xbytechat.api.AuthModule.Services
{
    public interface IAuthService
    {
        Task<ResponseResult> LoginAsync(UserLoginDto dto);
        Task<ResponseResult> SignupAsync(SignupBusinessDto dto);                  // ✅ Add this
        Task<ResponseResult> ResetPasswordAsync(ResetPasswordDto dto);           // ✅ Add this
        Task<ResponseResult> ResendConfirmationAsync(ResendConfirmationDto dto); // ✅ Add this
        Task<ResponseResult> RefreshTokenAsync(string refreshToken);
    
    }
}

