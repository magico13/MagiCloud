using Blazorise.DataGrid;
using MagiCommon.Comparers.ElasticFileInfoComparers;
using MagiCommon.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MagiCloudWeb.Components
{
    public partial class FilesComponent
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
                var fileList = await MagicApi.GetFilesAsync(false);
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

        private string GetFileContentUri(string id)
        {
            var path = MagicApi.GetFileContentUri(id);
            return path.ToString();
        }

        public async Task FilesChanged()
        {
            await Task.Delay(1000); //takes time to propagate
            await GetFilesAsync();
        }

        public async Task RowRemoved(ElasticFileInfo file)
        {
            Logger.LogInformation("Removing file {Name} ({Id})", file.Name, file.Id);
            file.IsDeleted = true;
            await MagicApi.RemoveFileAsync(file.Id);
            await FilesChanged();
        }

        public async Task RowUpdated(SavedRowItem<ElasticFileInfo, Dictionary<string, object>> saved)
        {
            Logger.LogInformation("Updating file {Name} ({Id})", saved.Item.Name, saved.Item.Id);
            await MagicApi.UpdateFileAsync(saved.Item);
            await FilesChanged();
        }
    }
}
