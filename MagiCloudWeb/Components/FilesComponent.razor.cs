using Blazorise.DataGrid;
using Blazorise.DataGrid.Configuration;
using MagiCommon.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MagiCloudWeb.Components
{
    public partial class FilesComponent
    {
        ElasticFileInfo selectedRow;
        FileList files;
        readonly VirtualizeOptions virtualizeOptions = new()
        {
            DataGridHeight = "600px",
            DataGridMaxHeight = "600px"
        };

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

        public async Task FilesChanged()
        {
            await GetFilesAsync();
        }

        public void RowRemoved(ElasticFileInfo file)
        {
            Logger.LogInformation("Removing file {Name}", file.Name);
            //await MagicApi.RemoveFileAsync(file.Id);
        }

        public void RowUpdated(SavedRowItem<ElasticFileInfo, Dictionary<string, object>> saved)
        {
            Logger.LogInformation("Updating file {Name}", saved.Item.Name);
        }
    }
}
