using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Storage
{
    public class LocalFileStorage : IFileStorage
    {
        private readonly string _rootPath;
        private readonly string _uploadsPath;
        private readonly ILogger<LocalFileStorage> _logger;
        private readonly IConfiguration _configuration;

        public LocalFileStorage(IHostEnvironment env, IConfiguration configuration, ILogger<LocalFileStorage> logger)
        {
            _configuration = configuration;
            _logger = logger;

            // ✅ Nếu là Local/Dev → dùng wwwroot/uploads
            if (env.IsDevelopment())
            {
                _rootPath = Path.Combine(env.ContentRootPath ?? Directory.GetCurrentDirectory(), "wwwroot");
                _uploadsPath = Path.Combine(_rootPath, "uploads");
            }
            else
            {
                // ✅ Nếu là AWS Lambda / Production → dùng /tmp (write được)
                _rootPath = Path.GetTempPath(); // = /tmp trên Linux/Lambda
                _uploadsPath = Path.Combine(_rootPath, "uploads");
            }

            InitializeUploadsDirectory();
        }

        private void InitializeUploadsDirectory()
        {
            try
            {
                Directory.CreateDirectory(_uploadsPath);

                var testFile = Path.Combine(_uploadsPath, "test_write_permission.tmp");
                using (var fs = File.Create(testFile)) { }
                File.Delete(testFile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Cannot initialize uploads directory: {_uploadsPath}");

                // ✅ CHỈ throw khi local để dễ debug
                throw new InvalidOperationException(
                    $"Cannot initialize uploads directory: {_uploadsPath}", ex);
            }
        }


        private void TestWritePermission()
        {
            var testFile = Path.Combine(_uploadsPath, "test_write_permission.tmp");
            try
            {
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
            }
            catch (Exception ex)
            {
                throw new UnauthorizedAccessException($"No write permission to uploads directory: {_uploadsPath}. Error: {ex.Message}");
            }
        }

        private void SetUnixPermissions(string path)
        {
            try
            {
                // ✅ Set directory permissions 755 (rwxr-xr-x) for Unix systems
                if (File.Exists("/bin/chmod"))
                {
                    var process = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "/bin/chmod",
                            Arguments = $"755 {path}",
                            UseShellExecute = false,
                            RedirectStandardOutput = true
                        }
                    };
                    process.Start();
                    process.WaitForExit();
                    _logger.LogInformation($"Set Unix permissions for: {path}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set Unix permissions");
            }
        }

        public async Task<(string storeName, string url, long size)> SaveAsync(Stream stream, string originalFileName, string contentType)
        {
            try
            {
                if (stream == null || stream.Length == 0)
                    throw new InvalidOperationException("Empty stream");

                var ext = Path.GetExtension(originalFileName);
                var storeName = $"{Guid.NewGuid():N}{ext}";
                var fullPath = Path.Combine(_uploadsPath, storeName);

                _logger.LogDebug($"Saving file: {originalFileName} -> {storeName}");

                // ✅ Ensure directory still exists
                if (!Directory.Exists(_uploadsPath))
                {
                    Directory.CreateDirectory(_uploadsPath);
                }

                // ✅ Use more robust file creation with better error handling
                using var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await stream.CopyToAsync(fs);
                await fs.FlushAsync();

                var size = fs.Length;
                var baseUrl = _configuration["FileUpload:BaseUrl"] ?? "/uploads";
                var url = $"{baseUrl}/{storeName}";

                _logger.LogInformation($"File saved: {storeName}, Size: {size} bytes");
                return (storeName, url, size);
            }
            catch (DirectoryNotFoundException ex)
            {
                _logger.LogError(ex, "Upload directory not found");
                throw new InvalidOperationException($"Upload directory not accessible: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Permission denied to upload directory");
                throw new InvalidOperationException($"Permission denied: {ex.Message}");
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "IO error during file save");
                throw new InvalidOperationException($"File save failed: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in SaveAsync");
                throw;
            }
        }

        public async Task<bool> DeleteAsync(string storeName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(storeName)) return false;
                
                var fullPath = Path.Combine(_uploadsPath, storeName);
                if (!File.Exists(fullPath)) return false;
                
                await Task.Run(() => File.Delete(fullPath));
                _logger.LogInformation($"File deleted: {storeName}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to delete file: {storeName}");
                return false;
            }
        }

        public string GetUrl(string storeName) 
        {
            var baseUrl = _configuration["FileUpload:BaseUrl"] ?? "/uploads";
            return $"{baseUrl}/{storeName}";
        }
    }
}