using MagiCommon.Models;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace MagiCloud
{
    public static class Extensions
    {
        public static async Task<AuthToken> VerifyAuthToken(this HttpRequest request, IElasticManager elastic)
        {
            if (request.Headers.TryGetValue("Token", out var authHeader))
            {
                string token = authHeader.ToString();
                return await elastic.VerifyTokenAsync(token);
            }
            return null;
        }
    }
}
