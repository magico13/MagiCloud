using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Security.Principal;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace MagiCloud
{
    public class TokenAuthenticationOptions : AuthenticationSchemeOptions
    {

    }

    public class TokenAuthenticationHandler : AuthenticationHandler<TokenAuthenticationOptions>
    {
        public TokenAuthenticationHandler(IOptionsMonitor<TokenAuthenticationOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock, IElasticManager elastic) 
            : base(options, logger, encoder, clock)
        {
            this.Elastic = elastic;
        }

        public IElasticManager Elastic { get; }

        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            return base.HandleChallengeAsync(properties);
        }

        protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
        {
            return base.HandleForbiddenAsync(properties);
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Request.Headers.TryGetValue("Token", out var token))
            {
                var authToken = await Elastic.VerifyTokenAsync(token);
                if (authToken == null)
                {
                    return AuthenticateResult.Fail("Token invalid");
                }
                var id = new GenericIdentity(authToken.LinkedUserId);
                id.AddClaim(new Claim("token", token));
                var ticket = new AuthenticationTicket(new ClaimsPrincipal(id), this.Scheme.Name);
                return AuthenticateResult.Success(ticket);
            }

            return AuthenticateResult.Fail("No token provided");

        }
    }
}
