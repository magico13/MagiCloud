using MagiCommon.Models;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace MagiCommon
{
    public interface IMagiCloudAPI
    {
        Task<FileList> GetFilesAsync(bool? deleted);
        Task<ElasticFileInfo> UploadFileAsync(ElasticFileInfo fileInfo, Stream fileStream);
        Task<ElasticFileInfo> UpdateFileAsync(ElasticFileInfo fileInfo);
        Task<ElasticFileInfo> GetFileInfoAsync(string id);
        Task<Stream> GetFileContentAsync(string id);
        Uri GetFileContentUri(string id);
        Task RemoveFileAsync(string id);


        Task<User> GetUserAsync();
        Task<User> CreateUserAsync(User user);
        Task<AuthToken> GetAuthTokenAsync(LoginRequest request);
        Task<AuthToken> ReauthTokenAsync(string token);
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

        public async Task<FileList> GetFilesAsync(bool? deleted = null)
        {
            var url = "api/files";
            if (deleted.HasValue)
            {
                url += $"?deleted={deleted}";
            }
            return await Client.GetFromJsonAsync<FileList>(url);
        }

        public async Task<ElasticFileInfo> UploadFileAsync(ElasticFileInfo fileInfo, Stream fileStream)
        {
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

        public async Task<ElasticFileInfo> UpdateFileAsync(ElasticFileInfo fileInfo)
        {
            var response = await Client.PostAsJsonAsync("api/files", fileInfo);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ElasticFileInfo>();
        }

        public async Task<ElasticFileInfo> GetFileInfoAsync(string id)
        {
            return await Client.GetFromJsonAsync<ElasticFileInfo>($"api/files/{id}");
        }

        public async Task<Stream> GetFileContentAsync(string id)
        {
            return await Client.GetStreamAsync($"api/filecontent/{id}");
        }

        public Uri GetFileContentUri(string id)
        {
            return new Uri(Client.BaseAddress, $"api/filecontent/{id}");
        }

        public async Task RemoveFileAsync(string id)
        {
            await Client.DeleteAsync($"api/files/{id}");
        }

        public async Task<User> GetUserAsync()
        {
            var response = await Client.GetAsync($"api/users");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound || response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return null;
            }
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<User>();
        }

        public async Task<User> CreateUserAsync(User user)
        {
            var response = await Client.PostAsJsonAsync("api/users", user);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<User>();
        }

        public async Task<AuthToken> GetAuthTokenAsync(LoginRequest request)
        {
            var response = await Client.PostAsJsonAsync("api/users/login", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<AuthToken>();
        }

        public async Task<AuthToken> ReauthTokenAsync(string token)
        {
            var response = await Client.PutAsync($"api/users/reauth?token={token}", null);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<AuthToken>();
        }
    }
}
