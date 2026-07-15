using System;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using VibeCity_API.Data;

namespace VibeCity_API.Services
{
    public interface IJwtTokenService
    {
        string CreateStudentToken(Student student, TimeSpan? lifetime = null);
    }

    public sealed class JwtTokenService : IJwtTokenService
    {
        private readonly IConfiguration _configuration;

        public JwtTokenService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string CreateStudentToken(Student student, TimeSpan? lifetime = null)
        {
            ArgumentNullException.ThrowIfNull(student);

            string jwtKey = _configuration["Jwt:Key"]
                ?? throw new InvalidOperationException("Thiếu cấu hình Jwt:Key tại Server.");

            string jwtIssuer = _configuration["Jwt:Issuer"]
                ?? throw new InvalidOperationException("Thiếu cấu hình Jwt:Issuer.");

            string jwtAudience = _configuration["Jwt:Audience"]
                ?? throw new InvalidOperationException("Thiếu cấu hình Jwt:Audience.");

            DateTime now = DateTime.UtcNow;

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, student.StudentId),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
                new Claim(ClaimTypes.Name, student.StudentId),
                new Claim("StudentId", student.StudentId),
                new Claim("FullName", student.FullName ?? string.Empty),
                new Claim("Major", student.Major ?? string.Empty),
                new Claim("Gpa", student.Gpa.ToString(CultureInfo.InvariantCulture))
            };

            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: jwtIssuer,
                audience: jwtAudience,
                claims: claims,
                notBefore: now,
                expires: now.Add(lifetime ?? TimeSpan.FromDays(7)),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}