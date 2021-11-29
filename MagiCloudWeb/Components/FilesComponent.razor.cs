using MagiCommon.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace MagiCloudWeb.Components
{
    public partial class FilesComponent
    {
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
                files?.Files?.Sort(new ElasticFileInfo.NameComparer());
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting file list");
            }
        }

        private string GetFileContentUri(string id)
        {
            var path = MagicApi.GetFileContentUri(id);
            return path.ToString();
        }
    }
}
