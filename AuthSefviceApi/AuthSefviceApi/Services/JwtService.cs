using AuthSefviceApi.Data.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace AuthSefviceApi.Services
{
    public class JwtService
    {
        private readonly IConfiguration _configuration;

        public JwtService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GenerateToken(Account account)
        {
            var keyStr = _configuration["Jwt:Key"]
                ?? throw new InvalidOperationException("JWT-ключ не настроен в конфигурации.");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyStr));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var expirationMinutes = _configuration.GetValue<int?>("Jwt:ExpirationMinutes");
            DateTime expires;
            if (expirationMinutes.HasValue && expirationMinutes.Value > 0)
                expires = DateTime.UtcNow.AddMinutes(expirationMinutes.Value);
            else
                expires = DateTime.UtcNow.AddDays(_configuration.GetValue<int?>("Jwt:ExpirationDays") ?? 7);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, account.Id.ToString()),
                new Claim(ClaimTypes.Name, account.Username),
                new Claim(ClaimTypes.Email, account.Email),
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: expires,
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
