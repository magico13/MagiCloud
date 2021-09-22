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
        Task<string> GetAuthTokenAsync(User user);
    }

    public class MagiCloudAPI : IMagiCloudAPI
    {
        public HttpClient Client { get; }
        public MagiContext DbContext { get; }

        public MagiCloudAPI(HttpClient client, MagiContext dbcontext)
        {
            Client = client;
            DbContext = dbcontext;
        }

        private void AddAuthToken()
        {
            if (!Client.DefaultRequestHeaders.Contains("Token"))
            {
                var user = DbContext.Users.First();
                Client.DefaultRequestHeaders.Add("Token", user.Token);
            }
        }

        public async Task<FileList> GetFilesAsync()
        {
            AddAuthToken();
            return await Client.GetFromJsonAsync<FileList>("api/files");
        }

        public async Task<ElasticFileInfo> UploadFileAsync(ElasticFileInfo fileInfo, Stream fileStream)
        {
            AddAuthToken();
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
            AddAuthToken();
            return await Client.GetFromJsonAsync<ElasticFileInfo>($"api/files/{id}");
        }

        public async Task<Stream> GetFileContentAsync(string id)
        {
            AddAuthToken();
            return await Client.GetStreamAsync($"api/filecontent/{id}");
        }

        public async Task RemoveFileAsync(string id)
        {
            AddAuthToken();
            await Client.DeleteAsync($"api/files/{id}");
        }


        public async Task<User> CreateUserAsync(User user)
        {
            var response = await Client.PostAsJsonAsync("api/users", user);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<User>();
        }

        public async Task<string> GetAuthTokenAsync(User user)
        {
            var response = await Client.PostAsJsonAsync("api/users/login", user);
            response.EnsureSuccessStatusCode();
            var temp = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            return temp["token"];
        }
    }
}
