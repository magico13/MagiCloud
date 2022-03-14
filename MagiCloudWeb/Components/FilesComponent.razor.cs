using Blazorise.DataGrid;
using MagiCloudWeb.Models;
using MagiCommon.Comparers.ElasticFileInfoComparers;
using MagiCommon.Extensions;
using MagiCommon.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MagiCloudWeb.Components
{
    public partial class FilesComponent
    {
        [Parameter]
        public EventCallback<string> OnFolderChanged { get; set; }

        [Parameter]
        public List<SearchResult> FileList
        {
            get => _files;
            set
            {
                _files = value;
                SortFiles();
            }
        }

        private List<FileWrapper> Files { get; set; }
        private string _folder = "/";
        private string CurrentFolder
        {
            get => _folder;
            set
            {
                if (_folder != value)
                {
                    _folder = value;
                    OnFolderChanged.InvokeAsync(_folder);
                }
            }
        }

        private List<SearchResult> _files;

        public void SortFiles()
        {
            try
            {
                if (_files?.Any() == true)
                {
                    _files.Sort(new NameComparer());
                    FilterToFolder(CurrentFolder);
                }
                StateHasChanged();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error setting file list");
            }
        }

        private void FilterToFolder(string folder)
        {
            folder = Path.GetFullPath(folder);
            Logger.LogWarning("Filtering to folder {Folder}", folder);
            CurrentFolder = folder;
            Files = new List<FileWrapper>();
            if (folder.Length > 1)
            {
                Files.Add(new FileWrapper { Name = ".." });
            }
            Files.AddRange(GetDirectoriesInFolder(FileList, folder));
            Files.AddRange(GetFilesInFolder(FileList, folder, false));
        }

        private static List<FileWrapper> GetFilesInFolder(IEnumerable<SearchResult> files, string folder, bool includeChildren)
        {
            if (includeChildren)
            {
                return files.Where(f => !Path.GetRelativePath(folder, f.GetFullPath()).StartsWith("..")).Select(f => new FileWrapper(f)).ToList();
            }
            return files.Where(f => Path.GetRelativePath(folder, f.GetFullPath()) == f.GetFileName()).Select(f => new FileWrapper(f)).ToList();
        }

        private  List<FileWrapper> GetDirectoriesInFolder(IEnumerable<SearchResult> files, string folder)
        {
            var dirs = new HashSet<string>();
            foreach (var file in files)
            {
                var split = Path.GetRelativePath(folder, file.GetFullPath()).Split('/');
                if (split.Length > 1)
                {
                    var firstPartOfPath = split.First();
                    if (!string.IsNullOrWhiteSpace(firstPartOfPath) && !firstPartOfPath.StartsWith(".."))
                    {
                        dirs.Add(firstPartOfPath);
                    }
                }
            }
            var list = new List<FileWrapper>();
            foreach (var dir in dirs)
            {
                var filesInFolder = GetFilesInFolder(files, Path.Combine(folder, dir), true);
                //Logger.LogInformation("{Count} files in folder {Name}", filesInFolder.Count, Path.Combine(folder, dir));
                list.Add(new FileWrapper
                {
                    Name = dir,
                    IsPublic = filesInFolder.TrueForAll(f => f.IsPublic == true),
                    LastUpdated = filesInFolder.Max(f => f.LastUpdated),
                    Size = filesInFolder.Sum(f => f.Size)
                });
            }
            return list;
        }
            
        private string GetFileContentUri(string id, bool download=false)
        {
            var path = MagicApi.GetFileContentUri(id, download);
            return path.ToString();
        }

        public async Task RowRemoved(FileWrapper wrapper)
        {
            var file = wrapper.BackingFileInfo;
            if (file == null)
            {
                return;
            }
            Logger.LogInformation("Removing file {Name} ({Id})", file.Name, file.Id);
            file.IsDeleted = true;
            FileList.Remove(file);
            await MagicApi.RemoveFileAsync(file.Id, false);
            FilterToFolder(CurrentFolder);
        }

        public async Task RowUpdated(SavedRowItem<FileWrapper, Dictionary<string, object>> saved)
        {
            var file = saved.Item.BackingFileInfo;
            if (file == null)
            {
                return;
            }
            file.Name = saved.Item.Name;
            Logger.LogInformation("Updating file {Name} ({Id})", file.Name, file.Id);
            await MagicApi.UpdateFileAsync(file);
            FilterToFolder(CurrentFolder);
        }

        public async Task UpdateVisibility(FileWrapper wrapper, bool visible)
        {
            var file = wrapper.BackingFileInfo;
            if (file == null)
            {
                var fullPath = Path.Combine(CurrentFolder, wrapper.Name);
                Logger.LogInformation("Updating visibility for all items under folder {Path}", fullPath);
                foreach (var item in GetFilesInFolder(FileList, fullPath, true))
                {
                    await UpdateVisibility(item.BackingFileInfo, visible);
                }
            }
            else
            {
                await UpdateVisibility(file, visible);
            }
            FilterToFolder(CurrentFolder);
        }

        private async Task UpdateVisibility(ElasticFileInfo file, bool visible)
        {
            Logger.LogInformation("Setting visibility of {Name} ({Id}) to {Visibility}", file.Name, file.Id, visible);
            file.IsPublic = visible;
            await MagicApi.UpdateFileAsync(file);
        }
    }
}
