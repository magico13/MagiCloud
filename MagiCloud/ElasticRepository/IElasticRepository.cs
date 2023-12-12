using MagiCommon.Models;
using Nest;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MagiCloud.Services;

public interface IElasticRepository
{
    // The elastic client
    IElasticClient Client { get; set; }

    // Creates the indices if necessary
    Task<bool> SetupIndicesAsync();
}

public interface IElasticFileRepo : IElasticRepository
{
    // Delete a file if the user has permission
    Task<FileAccessResult> DeleteFileAsync(string userId, string id);

    // Search for files by name
    Task<(FileAccessResult, ElasticFileInfo)> FindDocumentByNameAsync(string userId, string filename, string extension, string parentId);

    // Get a file by id if the user has permission
    Task<(FileAccessResult, ElasticFileInfo)> GetDocumentAsync(string userId, string id, bool includeText);

    // Get a file by id, does not check permissions (system use)
    Task<ElasticFileInfo> GetDocumentByIdAsync(string id, bool includeText);

    // Get a list of files that the user has permission to see
    Task<List<SearchResult>> GetDocumentsAsync(string userId, bool? deleted);

    // Index, or Upsert, a file
    Task<string> IndexDocumentAsync(string userId, ElasticFileInfo file);

    // Run a search for files, including content, matching the query
    Task<List<SearchResult>> SearchAsync(string userId, string query);

    // Search within a file
    Task<SearchResult> SearchWithinAsync(string userId, string query, string docId);

    // Update the file attributess
    Task UpdateFileAttributesAsync(ElasticFileInfo file);
}

public interface IElasticFolderRepo : IElasticRepository
{
    // Delete a folder if the user has permission
    Task<FileAccessResult> DeleteFolderAsync(string userId, string folderId);

    // Create a folder if the user has permission to create it there
    Task<string> UpsertFolderAsync(string userId, ElasticFolder folder);

    // Get a list of ElasticFileInfo that's in the folder
    Task<List<ElasticFileInfo>> GetFilesInFolderAsync(string folderId);
    
    // Get a folder by userId and folder id
    Task<(FileAccessResult accessLevel, ElasticFolder folder)> GetFolderAsync(string userId, string folderId);

    // Get all folders for a user
    Task<List<ElasticFolder>> GetFoldersAsync(string userId, bool? deleted = null);

    // Get a folder by id
    Task<ElasticFolder> GetFolderByIdAsync(string id);

    // Get a list of ElasticFolders in the folder
    Task<List<ElasticFolder>> GetFoldersInFolderAsync(string folderId);
}
