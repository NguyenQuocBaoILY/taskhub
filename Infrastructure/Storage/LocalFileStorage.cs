using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting; // thay IWebHostEnvironment bằng IHostEnvironment

namespace Infrastructure.Storage
{
    public class LocalFileStorage : IFileStorage
    {
        private readonly string _rootPath;
        private readonly string _uploadsPath;

        public LocalFileStorage(IHostEnvironment env)
        {
            _rootPath = Path.Combine(env.ContentRootPath ?? Directory.GetCurrentDirectory(), "wwwroot");
            _uploadsPath = Path.Combine(_rootPath, "uploads");
            Directory.CreateDirectory(_uploadsPath);
        }

        public async Task<(string storeName, string url, long size)> SaveAsync(Stream stream, string originalFileName, string contentType)
        {
            if (stream == null || stream.Length == 0)
                throw new InvalidOperationException("Empty stream");

            var ext = Path.GetExtension(originalFileName);
            var storeName = $"{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(_uploadsPath, storeName);

            await using var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, useAsync: true);
            await stream.CopyToAsync(fs);
            await fs.FlushAsync();

            var size = fs.Length;
            var url = $"/uploads/{storeName}";
            return (storeName, url, size);
        }

        public async Task<bool> DeleteAsync(string storeName)
        {
            if (string.IsNullOrWhiteSpace(storeName)) return false;
            var fullPath = Path.Combine(_uploadsPath, storeName);
            if (!File.Exists(fullPath)) return false;
            await Task.Run(() => File.Delete(fullPath));
            return true;
        }

        public string GetUrl(string storeName) => $"/uploads/{storeName}";
    }
}