using MagiCommon.Comparers.ElasticFileInfoComparers;
using MagiCommon.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MagiCloudWeb.Pages
{
    public partial class TrashCan
    {
        private List<ElasticFileInfo> files;

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            await GetFilesAsync();
        }

        private async Task GetFilesAsync()
        {
            try
            {
                files = null;
                var fileList = await MagicApi.GetFilesAsync(true);
                if (fileList?.Files?.Any() == true)
                {
                    fileList.Files.Sort(new NameComparer());
                    files = fileList.Files;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting file list");
            }
        }

        private async Task PermanentlyDeleteAsync(string id)
        {
            await MagicApi.RemoveFileAsync(id, true);
            files.RemoveAll(f => f.Id == id);
        }

        private async Task UndeleteAsync(ElasticFileInfo fileInfo)
        {
            fileInfo.IsDeleted = false;
            await MagicApi.UpdateFileAsync(fileInfo);
            files.Remove(fileInfo);
        }

        private async Task EmptyTrash()
        {
            foreach (var file in new List<ElasticFileInfo>(files) ?? new())
            {
                await MagicApi.RemoveFileAsync(file.Id, true);
                files.Remove(file);
            }
        }
    }
}
