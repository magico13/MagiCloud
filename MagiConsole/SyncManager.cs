using MagiCommon;
using MagiCommon.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MagiConsole
{
    public class SyncManager
    {
        public ILogger<SyncManager> Logger { get; }
        public MagiContext DbAccess { get; }
        public Settings Settings { get; }
        public IMagiCloudAPI ApiManager { get; }
        public IHashService HashService { get; }
        public FileSystemWatcher Watcher { get; }
        public SemaphoreSlim Semaphore { get; }

        private bool Syncing { get; set; }

        public SyncManager(ILogger<SyncManager> logger, MagiContext dbContext, IOptions<Settings> config, IMagiCloudAPI apiManager, IHashService hashService)
        {
            Logger = logger;
            DbAccess = dbContext;
            Settings = config.Value;
            ApiManager = apiManager;
            HashService = hashService;

            Directory.CreateDirectory(Settings.FolderPath);
            Watcher = new FileSystemWatcher(Settings.FolderPath)
            {
                NotifyFilter =    NotifyFilters.FileName
                                | NotifyFilters.DirectoryName
                                | NotifyFilters.Attributes
                                | NotifyFilters.Size
                                | NotifyFilters.LastWrite
                                | NotifyFilters.CreationTime,
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };
            Watcher.Changed += OnChanged;
            Watcher.Created += OnChanged;
            Watcher.Renamed += OnRenamed;
            Watcher.Deleted += OnDeleted;
            Semaphore = new SemaphoreSlim(1);
        }

        private async void OnRenamed(object sender, RenamedEventArgs e)
        {
            if (Syncing)
            {
                return;
            }
            try
            {
                Logger.LogDebug("Renamed {OldPath} to {FullPath}", e.OldFullPath, e.FullPath);
                if (Directory.Exists(e.FullPath))
                { //is directory
                  //remove old files
                    var oldFiles = GetFilesBelowFolder(e.OldName);
                    foreach (var fileData in oldFiles)
                    {
                        fileData.Status = FileStatus.Removed;
                        await DoFileUpdateAsync(fileData);
                    }
                    await DbAccess.SaveChangesAsync();

                    //add files as new
                    var files = Directory.GetFiles(e.FullPath, "*", SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        await ProcessIndividualFile(file);
                    }
                }
                else
                {
                    await ProcessIndividualFile(e.OldFullPath, FileStatus.Removed);
                    await ProcessIndividualFile(e.FullPath, FileStatus.New);
                }
                Logger.LogDebug("Completed - Renamed {OldPath} to {FullPath}", e.OldFullPath, e.FullPath);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while processing file rename, skipping for now.");
            }
        }

        private async void OnDeleted(object sender, FileSystemEventArgs e)
        {
            if (Syncing)
            {
                return;
            }
            try
            {
                Logger.LogDebug("Deleted: {FullPath}", e.FullPath);
                //deleted locally means we should delete it remotely
                //if it was a directory, delete everything that was below it
                var oldFiles = GetFilesBelowFolder(e.FullPath);
                foreach (var fileData in oldFiles)
                {
                    fileData.Status = FileStatus.Removed;
                    await DoFileUpdateAsync(fileData);
                }
                await DbAccess.SaveChangesAsync();

                await ProcessIndividualFile(e.FullPath, FileStatus.Removed);
                Logger.LogDebug("Completed - Deleted: {FullPath}", e.FullPath);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while processing file deletion, skipping for now.");
            }
        }

        private async void OnChanged(object sender, FileSystemEventArgs e)
        {
            if (Syncing)
            {
                return;
            }
            try
            {
                Logger.LogDebug("{ChangeType}: {FullPath}", e.ChangeType, e.FullPath);
                if (Directory.Exists(e.FullPath))
                { //is directory, process all the files inside
                    var files = Directory.GetFiles(e.FullPath, "*", SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        await ProcessIndividualFile(file);
                    }
                }
                else
                {
                    await ProcessIndividualFile(e.FullPath);
                }
                Logger.LogDebug("Completed - {ChangeType}: {FullPath}", e.ChangeType, e.FullPath);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while processing file change, skipping for now.");
            }
        }

        public async Task SyncAsync()
        {
            // compare files in folder to db: add new files, mark removed files, check if any files modified (last modified by filesystem)
            // get files from server
            // compare local to remote, pull down then push up
            int uploaded = 0;
            int downloaded = 0;
            int removedRemote = 0;
            int removedLocal = 0;

            if (Syncing)
            {
                return;
            }

            Syncing = true;
            try
            {
                var knownFiles = EnumerateLocalFiles(Settings.FolderPath);
                var extraLocalFiles = new List<FileData>(knownFiles.Values);

                //check removed files by finding files in the db but not in the folder
#pragma warning disable CA1841 // Prefer Dictionary.Contains methods. Justification: EFCore cannot translate ContainsKey but can translate Keys.Contains
                var removedFiles = DbAccess.Files.Where(f => !knownFiles.Keys.Contains(f.Id));
#pragma warning restore CA1841 // Prefer Dictionary.Contains methods
                foreach (var file in removedFiles)
                {
                    file.Status = FileStatus.Removed;
                    file.LastModified = DateTimeOffset.Now;
                    knownFiles[file.Id] = file;
                }

                // server files
                var remoteFiles = (await ApiManager.GetFilesAsync())
                    .Files
                    .Where(f => !f.IsDeleted)
                    .Select(f => f.ToFileData());

                //loop through remotes, if lastmodified is newer or not known locally then download it

                foreach (var remote in remoteFiles)
                {
                    bool foundLocally = knownFiles.TryGetValue(remote.Id, out FileData known);
                    if (foundLocally)
                    {
                        extraLocalFiles.Remove(known);
                    }
                    if (!foundLocally || remote.LastModified > known.LastModified) //todo, check for different hashes (if no, just update lastmodified, else CONFLICT)
                    {
                        //download it
                        var success = await DownloadFileAsync(remote.Id);
                        if (!success)
                        { //not found on the server, likely invalid/incomplete upload
                          // Tell it to upload current data instead, if we can find it locally
                            if (known != null || knownFiles.TryGetValue($"{remote.Name}.{remote.Extension}", out known))
                            {
                                remote.Status = FileStatus.New;
                                knownFiles.Remove(known.Id);
                                remote.LastModified = known.LastModified;
                                knownFiles[remote.Id] = remote;
                            }
                        }
                        else
                        {
                            downloaded++;
                        }
                    }
                }

                foreach (var file in extraLocalFiles)
                {
                    if (file.Status == FileStatus.Unmodified)
                    {
                        //this is an extra file, remove it locally
                        try
                        {
                            var path = GetPath(file);
                            File.Delete(path);
                            DbAccess.Remove(file);
                            removedLocal++;
                            Logger.LogInformation("Deleted local file: {Path}", file);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(ex, "Error while processing file deletion for file {Path}, skipping for now.", file);
                        }
                    }
                }

                foreach (var kvp in knownFiles)
                {
                    //if new, upload as new, if changed, upload as changed
                    (bool up, bool rem) = await DoFileUpdateAsync(kvp.Value);
                    if (up)
                    {
                        uploaded++;
                    }
                    if (rem)
                    {
                        removedRemote++;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unhandled error while syncing data. Will attempt again later.");
            }
            finally
            {
                Logger.LogInformation("Downloaded {DownloadCount} files. Uploaded {UploadCount} files. Removed {RemoveCount} files from server. Removed {LocalCount} files locally.",
                    downloaded, uploaded, removedRemote, removedLocal);
                await DbAccess.SaveChangesAsync();
                Syncing = false;
            }
        }

        private Dictionary<string, FileData> EnumerateLocalFiles(string path)
        {
            var knownFiles = new Dictionary<string, FileData>();
            var folderFiles = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
            foreach (var file in folderFiles)
            {
                var dbFile = GetDbFile(file);   
                knownFiles.Add(dbFile.Id, dbFile);
            }
            return knownFiles;
        }

        private FileData GetDbFile(string path)
        {
            var exists = File.Exists(path);
            var relativePath = Path.GetRelativePath(Settings.FolderPath, path); //get relative path from root
            relativePath = Path.GetDirectoryName(relativePath); //strip off the file info
            var name = Path.GetFileNameWithoutExtension(path);
            relativePath = Path.Combine(relativePath, name); //rejoin, minus the extension
            name = relativePath.Replace(@"\", "/"); //swap to forward slashes

            var ext = Path.GetExtension(path)?.Trim('.');

            DateTimeOffset lastModified = exists ? new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero) : DateTimeOffset.MinValue;

            var dbFile = DbAccess.Files.FirstOrDefault(f => f.Name == name && f.Extension == ext);
            if (dbFile is null)
            {
                dbFile = new FileData
                {
                    Id = $"{name}.{ext}",
                    Name = name,
                    Extension = ext,
                    LastModified = lastModified,
                    Status = FileStatus.New
                };
            }
            else if (dbFile.LastModified != lastModified) //could check hash but would be expensive
            {
                dbFile.Status = FileStatus.Updated;
                dbFile.LastModified = lastModified;
            }
            return dbFile;
        }

        private async Task<(bool uploaded, bool removed)> DoFileUpdateAsync(FileData file)
        {
            bool uploaded = false;
            bool removed = false;
            if (file.Status == FileStatus.New || file.Status == FileStatus.Updated)
            {
                var path = GetPath(file);
                using var stream = File.OpenRead(path);
                file.Hash = HashService.GenerateContentHash(stream, true);
                var updatedInfo = (await ApiManager.UploadFileAsync(file.ToElasticFileInfo(), stream)).ToFileData();
                uploaded = true;
                if (file.Status == FileStatus.New)
                { //nothing exists with the new id
                    DbAccess.Add(updatedInfo);
                    Logger.LogInformation("Uploaded new file: {Path} ({DocId})", updatedInfo, updatedInfo.Id);
                }
                else
                { //something already exists with this id
                    DbAccess.Entry(file).CurrentValues.SetValues(updatedInfo);
                    Logger.LogInformation("Updated remote file: {Path} ({DocId})", updatedInfo, updatedInfo.Id);
                }
            }
            else if (file.Status == FileStatus.Removed)
            {
                // these have been removed locally, remove them from the server
                await ApiManager.RemoveFileAsync(file.Id);
                DbAccess.Remove(file);
                removed = true;
                Logger.LogInformation("Removed file from server: {Path} ({DocId})", file, file.Id);
            }
            return (uploaded, removed);
        }

        private async Task<bool> DownloadFileAsync(string id)
        {
            try
            {
                var info = await ApiManager.GetFileInfoAsync(id);

                using var stream = await ApiManager.GetFileContentAsync(id);
                if (stream == Stream.Null)
                {
                    return false;
                }
                string path = Path.Combine(Settings.FolderPath, info.GetFullPath()?.TrimStart('/'));
                Directory.CreateDirectory(Path.GetDirectoryName(path));

                using (var filestream = new FileStream(path, FileMode.Create))
                {
                    await stream.CopyToAsync(filestream);
                }

                File.SetLastWriteTimeUtc(path, info.LastModified.UtcDateTime);

                var existing = await DbAccess.Files.FindAsync(info.Id);
                if (existing == null)
                {
                    DbAccess.Add(info.ToFileData());
                }
                else
                {
                    existing = info.ToFileData();
                }
                Logger.LogInformation("Downloaded remote file: {File} ({DocId})", info.GetFullPath(), info.Id);
                return true;
            }
            catch (HttpRequestException ex)
            {
                Logger.LogWarning(ex, "Exception while downloading document {DocId}", id);
                return false;
            }
        }

        private string GetPath(FileData info)
        {
            //return info.ToElasticFileInfo().GetFullPath();
            var filename = info.Name;
            if (!string.IsNullOrWhiteSpace(info.Extension))
            {
                filename = $"{info.Name}.{info.Extension}";
            }
            return Path.Combine(Settings.FolderPath, filename);
        }

        private async Task ProcessIndividualFile(string path, FileStatus? setStatus = null)
        {
            try
            {
                await Semaphore.WaitAsync();
                var dbFile = GetDbFile(path);
                if (dbFile != null)
                {
                    if (setStatus.HasValue)
                    {
                        if (setStatus.Value == FileStatus.Removed && dbFile.LastUpdated == default)
                        { // never was uploaded as far as we know, we can just ignore it
                            return;
                        }
                        dbFile.Status = setStatus.Value;
                    }
                    await DoFileUpdateAsync(dbFile);
                    DbAccess.SaveChanges();
                }
            }
            finally
            {
                Semaphore.Release();
            }
        }

        private List<FileData> GetFilesBelowFolder(string folder)
        {
            var relative = Path.GetRelativePath(Settings.FolderPath, folder);
            return DbAccess.Files.Where(f => f.Name.Contains(relative + "/")).ToList();
        }
    }
}
