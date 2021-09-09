using MagiCommon.Models;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;


namespace MagiConsole
{
    public interface IMagiCloudAPI
    {
        Task<FileList> GetFilesAsync();
        Task UploadNewFileAsync(ElasticFileInfo fileInfo, Stream fileStream);
        Task<ElasticFileInfo> GetFileInfoAsync(string id);
        Task<Stream> GetFileContentAsync(string id);
    }

    public class MagiCloudAPI : IMagiCloudAPI
    {
        public HttpClient Client { get; set; }

        public MagiCloudAPI(HttpClient client)
        {
            Client = client;
        }


        public async Task<FileList> GetFilesAsync()
        {
            return await Client.GetFromJsonAsync<FileList>("api/files");
        }

        public async Task UploadNewFileAsync(ElasticFileInfo fileInfo, Stream fileStream)
        {
            var response = await Client.PostAsJsonAsync("api/files", fileInfo);
            response.EnsureSuccessStatusCode();
            var returnedInfo = await response.Content.ReadFromJsonAsync<ElasticFileInfo>();
            var content = new MultipartFormDataContent
            {
                { new StreamContent(fileStream), "file", $"{returnedInfo.Name}.{returnedInfo.Extension}" }
            };

            response = await Client.PutAsync("api/filecontent/" + returnedInfo.Id, content);
            response.EnsureSuccessStatusCode();
        }

        public async Task<ElasticFileInfo> GetFileInfoAsync(string id)
        {
            return await Client.GetFromJsonAsync<ElasticFileInfo>($"api/files/{id}");
        }

        public async Task<Stream> GetFileContentAsync(string id)
        {
            return await Client.GetStreamAsync($"api/filecontent/{id}");
        }
    }
}
