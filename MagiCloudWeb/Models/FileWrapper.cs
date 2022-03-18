using MagiCommon.Models;
using System;

namespace MagiCloudWeb.Models;

public class FileWrapper
{
    public FileWrapper() { }
    public FileWrapper(SearchResult backing)
    {
        Name = backing.Name;
        BackingFileInfo = backing;
    }

    public string Name { get; set; }
    //public string Path { get; set; }
    public SearchResult BackingFileInfo { get; set; }

    private DateTimeOffset? _lastUpdated;
    public DateTimeOffset? LastUpdated
    {
        get => BackingFileInfo?.LastUpdated ?? _lastUpdated;
        set => _lastUpdated = value;
    }
    public string MimeType => BackingFileInfo?.MimeType ?? "folder";

    private long? _size;
    public long? Size
    {
        get => BackingFileInfo?.Size ?? _size;
        set => _size = value;
    }

    private bool? _isPublic;
    public bool? IsPublic
    {
        get => BackingFileInfo?.IsPublic ?? _isPublic;
        set => _isPublic = value;
    }
}
