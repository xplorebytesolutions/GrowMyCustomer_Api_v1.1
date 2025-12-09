// 📄 Infrastructure/Flows/FlowClickTokenService.cs
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace xbytechat.api.Infrastructure.Flows
{
    public record FlowClickPayload(
        Guid biz, Guid fid, int ver, Guid sid, short bi,
        Guid mlid, string cp, long iat, long exp
    );

    public interface IFlowClickTokenService
    {
        string Create(FlowClickPayload p);
        FlowClickPayload Validate(string token);
        string BuildUrl(FlowClickPayload p);
    }

    public class FlowClickTokenService : IFlowClickTokenService
    {
        private readonly FlowClickTokenOptions _opt;
        private readonly JwtSecurityTokenHandler _handler = new();

        public FlowClickTokenService(IOptions<FlowClickTokenOptions> opt)
        {
            _opt = opt.Value;
        }

        public string Create(FlowClickPayload p)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.Secret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim("biz", p.biz.ToString()),
                new Claim("fid", p.fid.ToString()),
                new Claim("ver", p.ver.ToString()),
                new Claim("sid", p.sid.ToString()),
                new Claim("bi",  p.bi.ToString()),
                new Claim("mlid",p.mlid.ToString()),
                new Claim("cp",  p.cp),
                new Claim("iat", p.iat.ToString()),
                new Claim("exp", p.exp.ToString())
            };

            var token = new JwtSecurityToken(claims: claims, signingCredentials: creds);
            return _handler.WriteToken(token);
        }

        public FlowClickPayload Validate(string token)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.Secret));

            var parameters = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = false, // we’ll check manually
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key
            };

            _handler.ValidateToken(token, parameters, out var validated);
            var jwt = (JwtSecurityToken)validated;

            long iat = long.Parse(jwt.Claims.First(c => c.Type == "iat").Value);
            long exp = long.Parse(jwt.Claims.First(c => c.Type == "exp").Value);
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (now > exp) throw new SecurityTokenExpiredException("Token expired");

            return new FlowClickPayload(
                biz: Guid.Parse(jwt.Claims.First(c => c.Type == "biz").Value),
                fid: Guid.Parse(jwt.Claims.First(c => c.Type == "fid").Value),
                ver: int.Parse(jwt.Claims.First(c => c.Type == "ver").Value),
                sid: Guid.Parse(jwt.Claims.First(c => c.Type == "sid").Value),
                bi: short.Parse(jwt.Claims.First(c => c.Type == "bi").Value),
                mlid: Guid.Parse(jwt.Claims.First(c => c.Type == "mlid").Value),
                cp: jwt.Claims.First(c => c.Type == "cp").Value,
                iat: iat,
                exp: exp
            );
        }

        public string BuildUrl(FlowClickPayload p)
        {
            var token = Create(p);
            return $"{_opt.BaseUrl.TrimEnd('/')}/r/flow/{token}";
        }
    }
}
