using System.Threading.Tasks;

namespace MagiCommon
{
    public interface ITokenProvider
    {
        Task<string> GetTokenAsync();
    }
}
