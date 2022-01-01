using MagiCommon;
using MagiCommon.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace MagiCloud.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly ILogger<UsersController> _logger;
        private readonly IElasticManager _elastic;
        private readonly IHashService _hashService;

        public UsersController(ILogger<UsersController> logger, IElasticManager elastic, IHashService hashService)
        {
            _logger = logger;
            _elastic = elastic;
            _hashService = hashService;
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetUserAsync()
        {
            var userId = User.Identity.Name;
            var user = await _elastic.GetUserAsync(userId);
            if (user == null)
            {
                return NotFound();
            }
            return new JsonResult(user);
        }
        
        [HttpPost]
        public async Task<IActionResult> CreateUserAsync(User user)
        {
            await _elastic.SetupIndicesAsync();
            if (!string.IsNullOrEmpty(user.Id))
            {
                return BadRequest(new { Message = "User Id should not be provided when creating a new user." });
            }
            if (string.IsNullOrWhiteSpace(user.Username) || string.IsNullOrWhiteSpace(user.Password))
            {
                return BadRequest(new { Message = "Invalid username or password." });
            }

            // hash their password
            user.Password = _hashService.GeneratePasswordHash(user.Password);

            var createdUser = await _elastic.CreateUserAsync(user);
            if (createdUser is null)
            {
                return Conflict();
            }
            return Ok(createdUser);
        }

        [HttpPost]
        [Route("login")]
        public async Task<IActionResult> LoginAsync(LoginRequest request)
        {
            await _elastic.SetupIndicesAsync();
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { Message = "Invalid username or password." });
            }

            // hash their password
            request.Password = _hashService.GeneratePasswordHash(request.Password);
            var fullToken = await _elastic.LoginUserAsync(request);
            var token = fullToken?.Id;
            if (!string.IsNullOrWhiteSpace(token))
            {
                await SignIn(fullToken);
                return Ok(fullToken);
            }
            return Unauthorized();
        }

        [HttpPut]
        [Route("reauth")]
        public async Task<IActionResult> ReauthAsync(string token)
        {
            await _elastic.SetupIndicesAsync();
            if (string.IsNullOrWhiteSpace(token) )
            {
                return BadRequest(new { Message = "Invalid token." });
            }

            var fullToken = await _elastic.VerifyTokenAsync(token);
            
            if (!string.IsNullOrWhiteSpace(fullToken?.Id))
            {
                await SignIn(fullToken);
                return Ok(fullToken);
            }
            return Unauthorized();
        }

        [HttpGet]
        [Route("reauth")]
        public async Task<IActionResult> ReauthCookieAsync(string returnUrl)
        {
            string token = User.FindFirst("Token").Value;
            if (string.IsNullOrWhiteSpace(token))
            {
                return BadRequest(new { Message = "Invalid token." });
            }

            var fullToken = await _elastic.VerifyTokenAsync(token);

            if (!string.IsNullOrWhiteSpace(fullToken?.Id))
            {
                await SignIn(fullToken);
                return Redirect(returnUrl);
            }
            return Unauthorized();
        }


        private async Task SignIn(AuthToken token)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, token.LinkedUserId),
                new Claim("Token", token.Id),
                new Claim(ClaimTypes.NameIdentifier, token.Name)
            };

            var claimsIdentity = new ClaimsIdentity(
                claims, CookieAuthenticationDefaults.AuthenticationScheme);

            var authProperties = new AuthenticationProperties
            {
                ExpiresUtc = token.Expiration,
                // The time at which the authentication ticket expires. A 
                // value set here overrides the ExpireTimeSpan option of 
                // CookieAuthenticationOptions set with AddCookie.

                IssuedUtc = token.Creation,
                // The time at which the authentication ticket was issued.
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);
        }
        
    }
}
