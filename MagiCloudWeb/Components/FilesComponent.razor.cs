using BlazorDownloadFile;
using MagiCommon.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace MagiCloudWeb.Components
{
    public partial class FilesComponent
    {
        [Inject]
        IBlazorDownloadFileService Downloader { get; set; }
        FileList files;

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            await GetFilesAsync();
        }

        private async Task GetFilesAsync()
        {
            try
            {
                files = await MagicApi.GetFilesAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting file list");
            }
        }

        private async Task GetFileContent(string id)
        {
            var info = await MagicApi.GetFileInfoAsync(id);
            var stream = await MagicApi.GetFileContentAsync(id);
            await Downloader.DownloadFile(info.Name + "." + info.Extension, stream, info.MimeType);
        }
    }
}
