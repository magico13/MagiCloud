using MagiCloud.Configuration;
using MagiCommon.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace MagiCloud.Services;

public class ElasticFolderRepo : BaseElasticRepo, IElasticFolderRepo
{
    public ElasticFolderRepo(
        IOptions<ElasticSettings> options,
        ILogger<ElasticFolderRepo> logger)
        : base(options, logger)
    {

    }

    public async Task<string> UpsertFolderAsync(string userId, ElasticFolder folder)
    {
        // If the folder has no name then it's not valid
        if (string.IsNullOrWhiteSpace(folder.Name) ||
            string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        Setup();
        folder.UserId = userId;

        // First check if the folder already exists and if the user has access to it
        if (!string.IsNullOrWhiteSpace(folder.Id))
        {
            var (access, _) = await GetFolderAsync(userId, folder.Id);
            if (access is not FileAccessResult.NotFound and not FileAccessResult.FullAccess)
            {
                // The folder exists but the user doesn't have full access to it
                return null;
            }
        }

        // First check the parent of the folder to verify that the user has Full Access to it
        var parentId = folder.ParentId;
        var (parentAccess, _) = await GetFolderAsync(userId, parentId);
        if (parentAccess != FileAccessResult.FullAccess)
        {
            // No access to the parent folder, cancel now
            return null;
        }
        // either adding to root or adding to a folder we have access to, keep going
        // First, check if the current name is already used by another folder in the parent,
        //      if so then we have to cancel out
        if (parentAccess is FileAccessResult.FullAccess)
        {
            var siblingFolders = (await GetFoldersInFolderAsync(parentId)).Where(f => f.UserId == userId);
            foreach (var siblingFolder in siblingFolders)
            {
                if (string.Equals(siblingFolder.Name, folder.Name, System.StringComparison.OrdinalIgnoreCase)
                    && siblingFolder.Id != folder.Id)
                {
                    // The name is already used by a folder, cancel out
                    return null;
                }
            }
            var siblingFiles = (await GetFilesInFolderAsync(parentId)).Where(f => f.UserId == userId);
            foreach (var siblingFile in siblingFiles)
            {
                if (siblingFile.Name == folder.Name
                    && string.IsNullOrWhiteSpace(siblingFile.Extension))
                {
                    // The name is already used by a file, cancel out
                    return null;
                }
            }
        }

        // We have verified that the folder object has the required fields, the user has access to the parent folder
        // and that we don't have a name conflict. Go ahead with creating/updating the folder doc in the index
        folder.LastUpdated = DateTimeOffset.Now; // Update the last updated field on the folder
        var result = await Client.IndexDocumentAsync(folder);
        ThrowIfInvalid(result);


        // If the parent is different from before, we must tell the old parent that the folder moved
        //if (existingFolder is not null && existingFolder.ParentId != folder.ParentId)
        //{
        //    var oldParent = await GetFolderByIdAsync(existingFolder.ParentId);
        //    if (oldParent.ChildFolders.Remove(folder.Id))
        //    {
        //        await UpsertFolderAsync(userId, oldParent);
        //    }
        //}

        // If the parent didn't already know about this child folder, we need to propogate the change up the chain
        //if (parentFolder?.ChildFolders.Contains(folder.Id) == false)
        //{
        //    parentFolder.ChildFolders.Add(folder.Id);
        //    await UpsertFolderAsync(userId, parentFolder);
        //}
        return result.Id;
    }

    public async Task<List<ElasticFileInfo>> GetFilesInFolderAsync(string folderId)
    {
        Setup();
        // Construct a NEST query to get all files with a parentId of the folderId.
        // ParentId should be the source of truth, so we won't get the children from the folder object itself

        var result = await Client.SearchAsync<ElasticFileInfo>(s => s
            .Size(10000)
            .Source(filter => filter.Excludes(e => e.Field(f => f.Text)))
            .Query(q =>
            {
                var qc = q.Term(t => t.Field(f => f.IsDeleted).Value(false));
                if (string.IsNullOrWhiteSpace(folderId))
                {
                    qc &= !q.Exists(p => p.Field(f => f.ParentId));
                }
                else
                {
                    qc &= q.Term(t => t.Field(f => f.ParentId.Suffix("keyword")).Value(folderId));
                }
                return qc;
            })
        );
        if (result.IsValid)
        {
            var list = new List<ElasticFileInfo>();
            foreach (var hit in result.Hits)
            {
                var source = hit.Source;
                source.Id = hit.Id;
                list.Add(source);
            }
            return list;
        }
        _logger.LogError("Invalid GetFilesInFolderAsync call. {ServerError}", result.ServerError);
        return new();
    }

    public async Task<(FileAccessResult accessLevel, ElasticFolder folder)> GetFolderAsync(string userId, string folderId)
    {
        // if no folderId then return full access but also null (that's the root)
        if (string.IsNullOrWhiteSpace(folderId))
        {
            return (accessLevel: FileAccessResult.FullAccess, folder: null);
        }

        // Get the folder by id, if the user shouldn't have access then return null
        var folder = await GetFolderByIdAsync(folderId);
        if (folder is null)
        {
            return (accessLevel: FileAccessResult.NotFound, folder: null);
        }
        var accessLevel = DetermineAccessForUser(userId, folder);
        return accessLevel switch
        {
            FileAccessResult.FullAccess or FileAccessResult.ReadOnly => (accessLevel, folder),
            _ => (accessLevel: FileAccessResult.NotPermitted, folder: null),
        };
    }
    
    public async Task<ElasticFolder> GetFolderByIdAsync(string id)
    {
        Setup();
        // If the id is null then that's the root folder
        if (string.IsNullOrWhiteSpace(id))
        {
            return new ElasticFolder
            {
                Id = null,
                ParentId = null,
                //ChildFiles = (await GetFilesInFolderAsync(id)).Select(f => f.Id).ToHashSet(),
                //ChildFolders = (await GetFoldersInFolderAsync(id)).Select(f => f.Id).ToHashSet()
            };
        }

        var result = await Client.GetAsync<ElasticFolder>(id);
        if (result.ApiCall.HttpStatusCode == (int)HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Folder with id {Id} not found.", id);
            return null;
        }
        ThrowIfInvalid(result);
        var source = result.Source;
        source.Id = result.Id;
        return source;
    }

    public async Task<List<ElasticFolder>> GetFoldersInFolderAsync(string folderId)
    {
        Setup();

        // Same as GetFilesInFolderAsync except with folders instead
        var result = await Client.SearchAsync<ElasticFolder>(s => s
            .Size(10000)
            .Query(q =>
            {
                var qc = q.Term(t => t.Field(f => f.IsDeleted).Value(false));
                if (string.IsNullOrWhiteSpace(folderId))
                {
                    qc &= !q.Exists(p => p.Field(f => f.ParentId));
                }
                else
                {
                    qc &= q.Term(t => t.Field(f => f.ParentId.Suffix("keyword")).Value(folderId));
                }
                return qc;
            })
            //.Query(q => q
            //    .Bool(bq => bq
            //        .Filter(
            //            fq => fq.Term(t => t.Verbatim()
            //                .Field(f => f.ParentId.Suffix("keyword")).Value(folderId)
            //            ),
            //            fq => fq.Term(t => t
            //                .Field(f => f.IsDeleted).Value(false)
            //            )
            //        )
            //    )
            //)
        );
        if (result.IsValid)
        {
            var list = new List<ElasticFolder>();
            foreach (var hit in result.Hits)
            {
                var source = hit.Source;
                source.Id = hit.Id;
                list.Add(source);
            }
            return list;
        }
        _logger.LogError("Invalid GetFoldersInFolderAsync call. {ServerError}", result.ServerError);
        return new();
    }

    public async Task<FileAccessResult> DeleteFolderAsync(string userId, string folderId)
    {
        Setup();
        var (getResult, doc) = await GetFolderAsync(userId, folderId);
        if (getResult != FileAccessResult.FullAccess || doc is null)
        {
            return getResult;
        }
        var result = await Client.DeleteAsync<ElasticFolder>(folderId);
        if (result.IsValid)
        {
            return FileAccessResult.FullAccess;
        }
        else if (result.ApiCall.HttpStatusCode == (int)HttpStatusCode.NotFound)
        {
            return FileAccessResult.NotFound;
        }
        ThrowIfInvalid(result);
        return FileAccessResult.Unknown;
    }
}
