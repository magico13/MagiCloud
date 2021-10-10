using MagiCommon;
using MagiCommon.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;


namespace MagiConsole
{
    public interface IMagiCloudAPI
    {
        Task<FileList> GetFilesAsync();
        Task<ElasticFileInfo> UploadFileAsync(ElasticFileInfo fileInfo, Stream fileStream);
        Task<ElasticFileInfo> GetFileInfoAsync(string id);
        Task<Stream> GetFileContentAsync(string id);
        Task RemoveFileAsync(string id);

        Task<User> CreateUserAsync(User user);
        Task<string> GetAuthTokenAsync(LoginRequest request);
    }

    public class MagiCloudAPI : IMagiCloudAPI
    {
        public HttpClient Client { get; }
        public ITokenProvider TokenProvider { get; }

        public MagiCloudAPI(HttpClient client, ITokenProvider tokenProvider)
        {
            Client = client;
            this.TokenProvider = tokenProvider;
        }

        private async Task AddAuthTokenAsync()
        {
            if (!Client.DefaultRequestHeaders.Contains("Token"))
            {
                var token = await TokenProvider.GetTokenAsync();
                Client.DefaultRequestHeaders.Add("Token", token);
            }
        }

        public async Task<FileList> GetFilesAsync()
        {
            await AddAuthTokenAsync();
            return await Client.GetFromJsonAsync<FileList>("api/files");
        }

        public async Task<ElasticFileInfo> UploadFileAsync(ElasticFileInfo fileInfo, Stream fileStream)
        {
            await AddAuthTokenAsync();
            var response = await Client.PostAsJsonAsync("api/files", fileInfo);
            response.EnsureSuccessStatusCode();
            var returnedInfo = await response.Content.ReadFromJsonAsync<ElasticFileInfo>();
            if (fileInfo.Hash != returnedInfo.Hash || string.IsNullOrWhiteSpace(returnedInfo.Hash))
            {
                var content = new MultipartFormDataContent
                {
                    { new StreamContent(fileStream), "file", $"{returnedInfo.Name}.{returnedInfo.Extension}" }
                };

                response = await Client.PutAsync("api/filecontent/" + returnedInfo.Id, content);
                response.EnsureSuccessStatusCode();
            }
            
            return await GetFileInfoAsync(returnedInfo.Id);
        }

        public async Task<ElasticFileInfo> GetFileInfoAsync(string id)
        {
            await AddAuthTokenAsync();
            return await Client.GetFromJsonAsync<ElasticFileInfo>($"api/files/{id}");
        }

        public async Task<Stream> GetFileContentAsync(string id)
        {
            await AddAuthTokenAsync();
            return await Client.GetStreamAsync($"api/filecontent/{id}");
        }

        public async Task RemoveFileAsync(string id)
        {
            await AddAuthTokenAsync();
            await Client.DeleteAsync($"api/files/{id}");
        }


        public async Task<User> CreateUserAsync(User user)
        {
            var response = await Client.PostAsJsonAsync("api/users", user);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<User>();
        }

        public async Task<string> GetAuthTokenAsync(LoginRequest request)
        {
            var response = await Client.PostAsJsonAsync("api/users/login", request);
            response.EnsureSuccessStatusCode();
            var temp = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            return temp["token"];
        }
    }
}
