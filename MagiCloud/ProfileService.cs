using IdentityModel;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using System.Threading.Tasks;

namespace MagiCloud;

public class ProfileService : IProfileService
{
    public ProfileService()
    {
    }

    public async Task GetProfileDataAsync(ProfileDataRequestContext context)
    {
        var nameClaim = context.Subject.FindAll(JwtClaimTypes.Name);
        context.IssuedClaims.AddRange(nameClaim);

        await Task.CompletedTask;
    }

    public async Task IsActiveAsync(IsActiveContext context) => await Task.CompletedTask;
}
