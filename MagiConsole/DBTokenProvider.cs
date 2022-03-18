using MagiCommon;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace MagiConsole;

public class DBTokenProvider : ITokenProvider
{
    public MagiContext DbContext { get; }

    public DBTokenProvider(MagiContext context)
    {
        DbContext = context;
    }

    public async Task<string> GetTokenAsync()
    {
        var user = await DbContext.Users.FirstOrDefaultAsync();
        return user?.Token;
    }

    public async Task StoreTokenAsync(string token)
    {
        var user = await DbContext.Users.FirstOrDefaultAsync();
        user.Token = token;
        await DbContext.SaveChangesAsync();
    }
}
