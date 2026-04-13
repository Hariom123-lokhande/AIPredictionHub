using AIPredictionHub.Models;
using AIPredictionHub.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

namespace AIPredictionHub.Controllers
{
    public class AccountController : Controller
    {
        private readonly AuthService _authService;
        private readonly ILogger<AccountController> _logger;

        public AccountController(AuthService authService, ILogger<AccountController> logger) //dependency injection
        {
            _authService = authService;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginModel model)
        {
            _logger.LogInformation("Login attempt for a user.");
            var user = _authService.Authenticate(model.Email, model.Password);
            if (user != null)
            {
                _logger.LogInformation("Successful login for user: {Username}", user.Username);
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Email, user.Email)
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties { IsPersistent = true };

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);

                return RedirectToAction("Index", "Rainfall");
            }

            _logger.LogWarning("Failed login attempt.");
            ModelState.AddModelError("", "Invalid username or password");
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            _logger.LogInformation("User {Username} logging out", User.Identity?.Name);
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Register(RegisterModel model)
        {
            if (ModelState.IsValid)
            {
                _logger.LogInformation("Registration attempt for a new account.");
                var success = _authService.Register(model);
                if (success)
                {
                    _logger.LogInformation("Successful registration for user: {Username}", model.Username);
                    return RedirectToAction("Login");
                }
                _logger.LogWarning("Registration failed for user: {Username} (already exists)", model.Username);
                ModelState.AddModelError("", "Username already exists");
            }
            return View(model);
        }

        [HttpPost("api/login")]
        public IActionResult ApiLogin([FromBody] LoginModel model)
        {
            _logger.LogInformation("API Login attempt.");
            var user = _authService.Authenticate(model.Email, model.Password);
            if (user != null)
            {
                _logger.LogInformation("Successful API login for user: {Username}", user.Username);
                var token = _authService.GenerateJwtToken(user);
                return Ok(new { token });
            }
            _logger.LogWarning("Failed API login attempt.");
            return Unauthorized();
        }

        [HttpPost("api/register")]
        public IActionResult ApiRegister([FromBody] RegisterModel model)
        {
            _logger.LogInformation("API Registration attempt.");
            var success = _authService.Register(model);
            if (success)
            {
                _logger.LogInformation("Successful API registration for user: {Username}", model.Username);
                return Ok();
            }
            _logger.LogWarning("API Registration failed for user: {Username} (already exists)", model.Username);
            return BadRequest("User already exists");
        }
    }
}
