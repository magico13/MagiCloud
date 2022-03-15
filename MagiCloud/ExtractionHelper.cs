using Goggles;
using MagiCloud.DataManager;
using System.IO;
using System.Threading.Tasks;

namespace MagiCloud
{
    public class ExtractionHelper
    {
        private readonly IElasticManager _elasticManager;
        private readonly IDataManager _dataManager;
        private readonly ILens _lens;

        public ExtractionHelper(
            ILens lens,
            IElasticManager elasticManager,
            IDataManager dataManager)
        {
            _lens = lens;
            _elasticManager = elasticManager;
            _dataManager = dataManager;
        }

        public Task<string> ExtractTextAsync(Stream stream, string contentType)
        {
            return _lens.ExtractTextAsync(stream, contentType);
        }

        public async Task<(bool, string)> ExtractTextAsync(string userId, string docId, bool force = false)
        {
            var (permission, doc) = await _elasticManager.GetDocumentAsync(userId, docId, !force);
            // get document. If we are forcing an update then we don't care about the current text
            // if not forcing, then we might return the existing text instead
            if (permission == FileAccessResult.FullAccess)
            {
                if (!string.IsNullOrWhiteSpace(doc.Text))
                {
                    return (false, doc.Text);
                }
                using var fileStream = _dataManager.GetFile(doc.Id);
                return (true, await ExtractTextAsync(fileStream, doc.MimeType));
            }
            else
            {
                return (false, null);
            }
        }
    }
}
