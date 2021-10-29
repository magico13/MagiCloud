using Cloudcrate.AspNetCore.Blazor.Browser.Storage;
using MagiCommon;
using System.Threading.Tasks;

namespace MagiCloudWeb
{
    public class BlazorStorageTokenProvider : ITokenProvider
    {
        public StorageBase Storage { get; }

        public BlazorStorageTokenProvider(LocalStorage storage)
        {
            this.Storage = storage;
        }

        public async Task<string> GetTokenAsync()
        {
            return await Storage.GetItemAsync("authToken");
        }

        public async Task StoreTokenAsync(string token)
        {
            await Storage.SetItemAsync("authToken", token);
        }
    }
}
