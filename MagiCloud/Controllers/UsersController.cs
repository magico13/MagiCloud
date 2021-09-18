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
            // hash their password
            user.Password = _hashService.GeneratePasswordHash(user.Password);
            if (!string.IsNullOrEmpty(user.Id))
            {
                return BadRequest("User Id should not be provided when creating a new user.");
            }

            await _elastic.CreateUserAsync(user);
            return Ok();
        }
    }
}
