using MagiCommon.Extensions;
using MagiCommon.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MagiCloud.Services;

public class ElasticManager
{
    private ILogger<ElasticManager> Logger { get; }
    public IElasticFileRepo FileRepo { get; }
    public IElasticFolderRepo FolderRepo { get; }

    public ElasticManager(ILogger<ElasticManager> logger,
                          IElasticFileRepo fileRepo,
                          IElasticFolderRepo folderRepo)
    {
        Logger = logger;
        FileRepo = fileRepo;
        FolderRepo = folderRepo;
    }

    public async Task<(List<ElasticFolder> folders, List<ElasticFileInfo> files)> GetFolderContentsAsync(string userId, string folderId, bool onlyOwned = true)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return (null, null);
        }

        // Get the folder
        var (access, folder) = await FolderRepo.GetFolderAsync(userId, folderId);
        if (access is not FileAccessResult.FullAccess or FileAccessResult.ReadOnly)
        {
            // No access to the folder, cancel now
            return (new(), new());
        }

        // Get the folders in the folder
        var folders = await FolderRepo.GetFoldersInFolderAsync(folderId);
        // Filter out any the user doesn't have access to
        folders = folders
            .Where(f => f.UserId == userId || 
                !onlyOwned && BaseElasticRepo.DetermineAccessForUser(userId, f) is FileAccessResult.ReadOnly)
            .ToList();

        // Get the files in the folder
        var files = await FolderRepo.GetFilesInFolderAsync(folderId);
        files = files
            .Where(f => f.UserId == userId ||
                !onlyOwned && BaseElasticRepo.DetermineAccessForUser(userId, f) is FileAccessResult.ReadOnly)
            .ToList();

        return (folders, files);
    }

    public async Task<bool> MoveFileAsync(string userId, string fileId, string newFolderId)
    {
        // Get the current file, current folder, and new folder, check if the user has access to all of them
        // update the parentId on the file to the new folder

        // Get the file so we can update it. Note that we don't need the text because we will end up getting that later
        var (access, currentFile) = await FileRepo.GetDocumentAsync(userId, fileId, false);
        if (access != FileAccessResult.FullAccess || currentFile is null)
        {
            // No access to the file, cancel now
            return false;
        }

        // Get the current folder, check if the user has access to it
        var (folderAccess, currentFolder) = await FolderRepo.GetFolderAsync(userId, currentFile.ParentId);
        if (folderAccess != FileAccessResult.FullAccess || currentFolder is null)
        {
            // No access to the folder, cancel now
            return false;
        }

        // Get the new folder, check if the user has access to it
        var (newFolderAccess, newFolder) = await FolderRepo.GetFolderAsync(userId, newFolderId);
        if (newFolderAccess != FileAccessResult.FullAccess || newFolder is null)
        {
            // No access to the folder, cancel now
            return false;
        }

        // Update the file's parent id
        currentFile.ParentId = newFolderId;
        var id = await FileRepo.IndexDocumentAsync(userId, currentFile);
        return id == fileId;
    }

    public async Task<string> GetFullPathForFile(string userId, string fileId)
    {
        var (access, currentFile) = await FileRepo.GetDocumentAsync(userId, fileId, false);
        if (access != FileAccessResult.FullAccess || currentFile is null)
        {
            // No access to the file, cancel now
            return null;
        }

        var filePath = "/"+currentFile.GetFileName();
        // Now we iterate up the folder structure by parentId, adding in each folder's name
        var parentId = currentFile.ParentId;
        while (!string.IsNullOrWhiteSpace(parentId))
        {
            var (folderAccess, folderInfo) = await FolderRepo.GetFolderAsync(userId, parentId);
            if (folderAccess is not (FileAccessResult.FullAccess or FileAccessResult.ReadOnly))
            {
                // Somehow a folder in the chain isn't permitted, stop here?
                Logger.LogWarning(
                    "Reached folder without read access while iterating tree. User {UserId} Folder {FolderId}",
                    userId,
                    parentId);
                break;
            }
            parentId = folderInfo.ParentId;
            filePath = "/" + folderInfo.Name;
        }
        return filePath;
    }

    public async Task<List<ElasticFolder>> GetParentsForObjectAsync(string userId, ElasticObject elasticObject)
    {
        List<ElasticFolder> folderList = new();
        var parentId = elasticObject.ParentId;
        while (!string.IsNullOrWhiteSpace(parentId))
        {
            var (folderAccess, folderInfo) = await FolderRepo.GetFolderAsync(userId, parentId);
            if (folderAccess is not (FileAccessResult.FullAccess or FileAccessResult.ReadOnly))
            {
                // Somehow a folder in the chain isn't permitted, stop here?
                Logger.LogWarning(
                    "Reached folder without read access while iterating tree. User {UserId} Folder {FolderId}",
                    userId,
                    parentId);
                break;
            }
            parentId = folderInfo.ParentId;
            folderList.Add(folderInfo);
        }
        // Reverse it so the top level folder is first
        folderList.Reverse();
        return folderList;
    }

    public async Task<bool> DeleteObject(string userId, ElasticObject elasticObject, bool permanentDelete = false)
    {
        if (elasticObject is null)
        {
            return false;
        }
        if (elasticObject is ElasticFileInfo)
        {
            if (permanentDelete)
            {
                // fully remove the file
                return await FileRepo.DeleteFileAsync(userId, elasticObject.Id) == FileAccessResult.FullAccess;
                // May need to also delete the file off disk
            }
            else
            {
                // just mark it soft deleted
                var (access, sourceFile) = await FileRepo.GetDocumentAsync(userId, elasticObject.Id, false);
                if (access is FileAccessResult.FullAccess)
                {
                    sourceFile.IsDeleted = true;
                    var id = await FileRepo.IndexDocumentAsync(userId, sourceFile);
                    return id == elasticObject.Id;
                }
                else
                {
                    return false;
                }
            }
        }
        else if (elasticObject is ElasticFolder)
        {
            var sourceFolder = await FolderRepo.GetFolderByIdAsync(elasticObject.Id);
            var (folders, files) = await GetFolderContentsAsync(userId, elasticObject.Id, true);
            if (BaseElasticRepo.DetermineAccessForUser(userId, sourceFolder) is FileAccessResult.FullAccess)
            {
                if (permanentDelete)
                {
                    // It's not safe to permanent delete a folder that still has children
                    if (folders.Any() || files.Any())
                    {
                        Logger.LogWarning("Cannot delete folder with ID {FolderId} because it still has children", elasticObject.Id);
                        return false;
                    }

                    // fully remove the folder
                    return await FolderRepo.DeleteFolderAsync(userId, elasticObject.Id) == FileAccessResult.FullAccess;
                }
                else
                {
                    // Delete every child of the folder, recursively, using the same settings
                    foreach (var file in files)
                    {
                        Logger.LogInformation("Recursively deleting file {Id} from folder {FolderId}", file.Id, elasticObject.Id);
                        await DeleteObject(userId, file, false);
                    }
                    foreach (var folder in folders)
                    {
                        Logger.LogInformation("Recursively deleting folder {Id} from folder {FolderId}", folder.Id, elasticObject.Id);
                        await DeleteObject(userId, folder, false);
                    }

                    // just mark it soft deleted
                    sourceFolder.IsDeleted = true;
                    var id = await FolderRepo.UpsertFolderAsync(userId, sourceFolder);
                    return id == elasticObject.Id;
                }
            }

            return false;
        }
        else
        {
            return false;
        }
    }
}
