using MagiCommon;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MagiConsole
{
    public class SyncManager
    {
        public MagiContext DbAccess { get; set; }
        public Settings Settings { get; set; }
        public IMagiCloudAPI ApiManager { get; set; }
        public IHashService HashService { get; set; }

        public SyncManager(MagiContext dbContext, IOptions<Settings> config, IMagiCloudAPI apiManager, IHashService hashService)
        {
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

            var folderFiles = Directory.GetFiles(Settings.FolderPath);
            var knownFiles = new Dictionary<string, FileData>();
            foreach (var file in folderFiles)
            {
                var name = Path.GetFileNameWithoutExtension(file);
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

            //check removed files by finding files in the db but not in the folder
            var removedFiles = DbAccess.Files.Where(f => !knownFiles.Keys.Contains(f.Id));// .Except(knownFiles.Values);

            // server files
            var remoteFiles = (await ApiManager.GetFilesAsync()).Files.Select(f => f.ToFileData()).ToList();
            
            //loop through remotes, if lastmodified is newer or not known locally then download it

            foreach (var remote in remoteFiles)
            {
                if (!knownFiles.TryGetValue(remote.Id, out FileData known) || remote.LastModified > known.LastModified) //todo, check for different hashes (if no, just update lastmodified, else CONFLICT)
                {
                    //download it
                    await DownloadFileAsync(remote.Id);
                }
            }

            foreach (var kvp in knownFiles)
            {
                var info = kvp.Value;
                //if new, upload as new, if changed, upload as changed
                if (info.Status == FileStatus.New || info.Status == FileStatus.Updated)
                {
                    var path = Path.Combine(Settings.FolderPath, $"{info.Name}.{info.Extension}");
                    using var stream = File.OpenRead(path);
                    info.Hash = HashService.GenerateHash(stream, true);
                    var updatedInfo = (await ApiManager.UploadFileAsync(info.ToElasticFileInfo(), stream)).ToFileData();
                    if (info.Status == FileStatus.New)
                    { //nothing exists with the new id
                        DbAccess.Add(updatedInfo);
                    }
                    else
                    { //something already exists with this id
                        DbAccess.Entry(info).CurrentValues.SetValues(updatedInfo);
                    }
                }
            }

            foreach (var file in removedFiles)
            {
                // these have been removed locally, remove them from the server
                await ApiManager.RemoveFileAsync(file.Id);
                DbAccess.Remove(file);
            }

            await DbAccess.SaveChangesAsync();
        }


        private async Task DownloadFileAsync(string id)
        {
            var info = await ApiManager.GetFileInfoAsync(id);
            using var stream = await ApiManager.GetFileContentAsync(id);
            string path = Path.Combine(Settings.FolderPath, $"{info.Name}.{info.Extension}");

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
        }
    }
}
