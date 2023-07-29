using MagiCommon.Models;
using System;

namespace MagiCloud.Models;

public class FileWrapper
{
    public FileWrapper() { }
    public FileWrapper(ElasticObject backing)
    {
        Name = backing.Name;
        BackingObject = backing;
    }

    public string Name { get; set; }
    //public string Path { get; set; }
    public ElasticObject BackingObject { get; set; }

    private DateTimeOffset? _lastUpdated;
    public DateTimeOffset? LastUpdated
    {
        get => BackingObject?.LastUpdated ?? _lastUpdated;
        set => _lastUpdated = value;
    }
    public string MimeType => BackingObject is ElasticFileInfo info ? info?.MimeType : "folder";

    private long? _size;
    public long? Size
    {
        get => BackingObject is ElasticFileInfo info ? info?.Size : _size;
        set => _size = value;
    }

    private bool? _isPublic;
    public bool? IsPublic
    {
        get => BackingObject?.IsPublic ?? _isPublic;
        set => _isPublic = value;
    }
}
