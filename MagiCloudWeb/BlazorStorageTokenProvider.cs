using Cloudcrate.AspNetCore.Blazor.Browser.Storage;
using MagiCommon;
using System.Threading.Tasks;

namespace MagiCloudWeb
{
    public class BlazorStorageTokenProvider : ITokenProvider
    {
        const string KEY = "authToken";
        public StorageBase Storage { get; }

        public BlazorStorageTokenProvider(LocalStorage storage)
        {
            this.Storage = storage;
        }

        public async Task<string> GetTokenAsync()
        {
            return await Storage.GetItemAsync(KEY);
        }

        public async Task StoreTokenAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                await Storage.RemoveItemAsync(KEY);
            }
            else
            {
                await Storage.SetItemAsync(KEY, token);
            }
        }
    }
}
