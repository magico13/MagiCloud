namespace MagiCommon.Models
{
    public class SearchResult : ElasticFileInfo
    {
        public SearchResult() { }
        public SearchResult(ElasticFileInfo fileInfo)
        {
            Extension = fileInfo.Extension;
            Hash = fileInfo.Hash;
            Id = fileInfo.Id;
            IsDeleted = fileInfo.IsDeleted;
            IsPublic = fileInfo.IsPublic;
            LastModified = fileInfo.LastModified;
            LastUpdated = fileInfo.LastUpdated;
            MimeType = fileInfo.MimeType;
            Name = fileInfo.Name;
            Size = fileInfo.Size;
            Text = fileInfo.Text;
            UserId = fileInfo.UserId;
        }

        public string[]? Highlights { get; set; }
    }
}
