using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AIPredictionHub.Data;
using AIPredictionHub.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace AIPredictionHub.Services
{
    public class AuthService
    {
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AuthService> _logger;

        public AuthService(IConfiguration configuration, ApplicationDbContext context, ILogger<AuthService> logger) //dependency injection
        {
            _configuration = configuration;
            _context = context;
            _logger = logger;
        }

        public string GenerateJwtToken(User user)
        {
            _logger.LogInformation("Generating JWT token for user: {Username}", user.Username);
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var key = Encoding.ASCII.GetBytes(jwtSettings["Secret"]!);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Email, user.Email)
                }),
                Expires = DateTime.UtcNow.AddDays(double.Parse(jwtSettings["ExpiryInDays"] ?? "7")),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
                Issuer = jwtSettings["Issuer"],
                Audience = jwtSettings["Audience"]
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        public User? Authenticate(string email, string password)
        {
            _logger.LogInformation("Attempting to authenticate user: {Email}", email);
            var user = _context.Users.FirstOrDefault(u => u.Email == email);
            
            if (user == null)
            {
                _logger.LogWarning("Authentication failed: User not found.");
                return null;
            }

            bool isValid = false;

            try
            {
                // 1. Try BCrypt verification (for new/hashed users)
                isValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
            }
            catch (BCrypt.Net.SaltParseException)
            {
                // 2. Legacy fallback: If salt is invalid, the password in DB is likely plain text
                _logger.LogWarning("Legacy plain-text password detected for user: {Email}. Attempting fallback.", email);
                isValid = (password == user.PasswordHash);

                if (isValid)
                {
                    // 3. Automatic Migration: Hash the password now that we know it's correct
                    _logger.LogInformation("Upgrading legacy password to hash for user: {Email}", email);
                    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
                    _context.SaveChanges();
                }
            }

            if (isValid)
            {
                _logger.LogInformation("Successfully authenticated user: {Username}", user.Username);
                return user;
            }
            
            _logger.LogWarning("Authentication failed: Invalid credentials.");
            return null;
        }

        public bool Register(RegisterModel model)
        {
            _logger.LogInformation("Processing registration for user: {Username}", model.Username);
            if (_context.Users.Any(u => u.Username == model.Username))
            {
                _logger.LogWarning("Registration failed: Username {Username} already exists", model.Username);
                return false;
            }

            var newUser = new User
            {
                Username = model.Username,
                Email = model.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "System/Registration"
            };

            try
            {
                _context.Users.Add(newUser);
                _context.SaveChanges();
                _logger.LogInformation("Successfully registered new user: {Username}", model.Username);
                return true;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database update failed during registration for {Username}", model.Username);
                throw new InvalidOperationException("User registration failed while saving data.", ex);
            }
        }
    }
}
