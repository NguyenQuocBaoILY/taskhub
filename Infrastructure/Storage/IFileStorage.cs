using System.IO;
using System.Threading.Tasks;

namespace Infrastructure.Storage
{
    public interface IFileStorage
    {
        Task<(string storeName, string url, long size)> SaveAsync(Stream stream, string originalFileName, string contentType);
        Task<bool> DeleteAsync(string storeName);
        string GetUrl(string storeName);
        Task<Stream> DownloadAsync(string storeName);
    }
}