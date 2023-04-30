using Microsoft.Extensions.Logging;
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

    // Handle things like moving files/folders, deleting files/folders, creating? files/folders
    // since those all require updating the parent folder

    public async Task MoveFile(string userId, string fileId, string newFolderId)
    {
        // Get the current file, check if the user has access to it
        // tell the old folder it moved, update the parentId on the file, tell the new folder it exists there

        var (access, currentFile) = await FileRepo.GetDocumentAsync(userId, fileId, false);
        if (access != FileAccessResult.FullAccess || currentFile is null)
        {
            // No access to the file, cancel now
            return;
        }

        // Get the current folder, check if the user has access to it
        var (folderAccess, currentFolder) = await FolderRepo.GetFolderAsync(userId, currentFile.ParentId);
        if (folderAccess != FileAccessResult.FullAccess || currentFolder is null)
        {
            // No access to the folder, cancel now
            return;
        }
    }
}
