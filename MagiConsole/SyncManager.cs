using MagiCommon;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace MagiConsole
{
    public class SyncManager
    {
        public ILogger<SyncManager> Logger { get; set; }
        public MagiContext DbAccess { get; set; }
        public Settings Settings { get; set; }
        public IMagiCloudAPI ApiManager { get; set; }
        public IHashService HashService { get; set; }

        public SyncManager(ILogger<SyncManager> logger, MagiContext dbContext, IOptions<Settings> config, IMagiCloudAPI apiManager, IHashService hashService)
        {
            Logger = logger;
            DbAccess = dbContext;
            Settings = config.Value;
            ApiManager = apiManager;
            HashService = hashService;
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

            var knownFiles = EnumerateLocalFiles();
            var extraLocalFiles = new List<FileData>(knownFiles.Values);

            //check removed files by finding files in the db but not in the folder
            var removedFiles = DbAccess.Files.Where(f => !knownFiles.Keys.Contains(f.Id));
            foreach (var file in removedFiles)
            {
                file.Status = FileStatus.Removed;
                file.LastModified = DateTimeOffset.Now;
                knownFiles[file.Id] = file;
            }

            // server files
            var remoteFiles = (await ApiManager.GetFilesAsync()).Files.Select(f => f.ToFileData()).ToList();
            
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
                    DbAccess.Remove(file);
                    var path = GetPath(file);
                    File.Delete(path);
                    removedLocal++;
                }
            }

            foreach (var kvp in knownFiles)
            {
                var info = kvp.Value;
                //if new, upload as new, if changed, upload as changed
                if (info.Status == FileStatus.New || info.Status == FileStatus.Updated)
                {
                    var path = GetPath(info);
                    using var stream = File.OpenRead(path);
                    info.Hash = HashService.GenerateHash(stream, true);
                    var updatedInfo = (await ApiManager.UploadFileAsync(info.ToElasticFileInfo(), stream)).ToFileData();
                    uploaded++;
                    if (info.Status == FileStatus.New)
                    { //nothing exists with the new id
                        DbAccess.Add(updatedInfo);
                    }
                    else
                    { //something already exists with this id
                        DbAccess.Entry(info).CurrentValues.SetValues(updatedInfo);
                    }
                }
                else if (info.Status == FileStatus.Removed)
                {
                    // these have been removed locally, remove them from the server
                    await ApiManager.RemoveFileAsync(info.Id);
                    DbAccess.Remove(info);
                    removedRemote++;
                }
            }

            await DbAccess.SaveChangesAsync();
            Logger.LogInformation("Downloaded {DownloadCount} files. Uploaded {UploadCount} files. Removed {RemoveCount} files from server. Removed {LocalCount} files locally.", 
                downloaded, uploaded, removedRemote, removedLocal);
        }

        private Dictionary<string, FileData> EnumerateLocalFiles()
        {
            var knownFiles = new Dictionary<string, FileData>();
            var folderFiles = Directory.GetFiles(Settings.FolderPath, "*", SearchOption.AllDirectories);
            foreach (var file in folderFiles)
            {
                var relativePath = Path.GetRelativePath(Settings.FolderPath, file); //get relative path from root
                relativePath = Path.GetDirectoryName(relativePath); //strip off the file info
                var name = Path.GetFileNameWithoutExtension(file);
                relativePath = Path.Combine(relativePath, name); //rejoin, minus the extension
                name = relativePath.Replace(@"\", "/"); //swap to forward slashes

                var ext = Path.GetExtension(file)?.Trim('.');
                DateTimeOffset lastModified = new DateTimeOffset(File.GetLastWriteTimeUtc(file), TimeSpan.Zero);

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
                knownFiles.Add(dbFile.Id, dbFile);
            }
            return knownFiles;
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
                string path = Path.Combine(Settings.FolderPath, $"{info.Name}.{info.Extension}");
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
            var filename = info.Name;
            if (!string.IsNullOrWhiteSpace(info.Extension))
            {
                filename = $"{info.Name}.{info.Extension}";
            }
            return Path.Combine(Settings.FolderPath, filename);
        }
    }
}
