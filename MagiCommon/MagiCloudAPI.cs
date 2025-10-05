using MagiCommon.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace MagiCommon
{
    public interface IMagiCloudAPI
    {
        Task<List<SearchResult>> GetFilesAsync(bool? deleted);
        Task<List<SearchResult>> SearchAsync(string query);
        Task<ElasticFileInfo?> UploadFileAsync(ElasticFileInfo fileInfo, Stream fileStream);
        Task<ElasticFileInfo?> UpdateFileAsync(ElasticFileInfo fileInfo);
        Task<ElasticFileInfo?> GetFileInfoAsync(string id);
        Task<Stream> GetFileContentAsync(string id);
        Uri GetFileContentUri(string id, bool download);
        Task RemoveFileAsync(string id, bool permanent);
    }

    public class MagiCloudAPI(HttpClient client) : IMagiCloudAPI
    {
        public async Task<List<SearchResult>> GetFilesAsync(bool? deleted = null)
        {
            var url = "api/files";
            if (deleted.HasValue)
            {
                url += $"?deleted={deleted}";
            }
            return await client.GetFromJsonAsync<List<SearchResult>>(url) ?? [];
        }

        public async Task<List<SearchResult>> SearchAsync(string query)
        {
            var url = $"api/search?query={query}";
            return await client.GetFromJsonAsync<List<SearchResult>>(url) ?? [];
        }

        public async Task<ElasticFileInfo?> UploadFileAsync(ElasticFileInfo fileInfo, Stream fileStream)
        {
            var response = await client.PostAsJsonAsync("api/files", fileInfo);
            response.EnsureSuccessStatusCode();
            var returnedInfo = await response.Content.ReadFromJsonAsync<ElasticFileInfo>() ?? throw new Exception("Failed to get file info after upload.");
            if (string.IsNullOrWhiteSpace(returnedInfo?.Hash) || fileInfo.Hash != returnedInfo.Hash)
            {
                // if file is less than the chunk size, upload it all at once
                var fileSize = fileStream.Length;
                var chunkSize = Constants.UPLOAD_CHUNK_SIZE;
                var chunks = fileSize / chunkSize + 1;
                if (chunks == 1)
                {
                    var content = new MultipartFormDataContent
                    {
                        { new StreamContent(fileStream), "file", $"{returnedInfo!.Name}.{returnedInfo.Extension}" }
                    };

                    response = await client.PutAsync("api/filecontent/" + returnedInfo.Id, content);
                    response.EnsureSuccessStatusCode();
                }
                else
                {
                    // If > chunksize upload in parts of chunksize each
                    for (var i = 0; i < chunks; i++)
                    {
                        var final = i + 1 == chunks;

                        using var partialStream = new MemoryStream(chunkSize);
                        var buffer = new byte[chunkSize];
                        var actual = await fileStream.ReadAsync(buffer.AsMemory(0, chunkSize));
                        var content = new MultipartFormDataContent
                            {
                                { new ByteArrayContent(buffer, 0, actual), "file", $"{returnedInfo!.Name}.{returnedInfo.Extension}" }
                            };


                        response = await client.PutAsync($"api/filecontent/{returnedInfo.Id}/{i}?final={final}", content);
                        response.EnsureSuccessStatusCode();
                    }
                }
                
            }
            
            return await GetFileInfoAsync(returnedInfo!.Id);
        }

        public async Task<ElasticFileInfo?> UpdateFileAsync(ElasticFileInfo fileInfo)
        {
            var response = await client.PostAsJsonAsync("api/files", fileInfo);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ElasticFileInfo>();
        }

        public async Task<ElasticFileInfo?> GetFileInfoAsync(string id) => await client.GetFromJsonAsync<ElasticFileInfo>($"api/files/{id}");

        public async Task<Stream> GetFileContentAsync(string id) => await client.GetStreamAsync($"api/filecontent/{id}");

        public Uri GetFileContentUri(string id, bool download)
        {
            var builder = new UriBuilder(new Uri(client.BaseAddress ?? new(""), $"api/filecontent/{id}"))
            {
                Query = $"download={download}"
            };
            return builder.Uri;
        }

        public async Task RemoveFileAsync(string id, bool permanent) => await client.DeleteAsync($"api/files/{id}?permanent={permanent}");
    }
}
