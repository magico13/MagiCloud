using MagiCommon;
using MagiCommon.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
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
        public async Task<IActionResult> LoginAsync(User user)
        {
            await _elastic.SetupIndicesAsync();
            if (string.IsNullOrWhiteSpace(user.Username) || string.IsNullOrWhiteSpace(user.Password))
            {
                return BadRequest(new { Message = "Invalid username or password." });
            }

            // hash their password
            user.Password = _hashService.GeneratePasswordHash(user.Password);
            var fullToken = await _elastic.LoginUserAsync(user);
            var token = fullToken.Id;
            if (!string.IsNullOrWhiteSpace(token))
            {
                return new JsonResult(new { token });
            }
            return Unauthorized();
        }
        
    }
}
