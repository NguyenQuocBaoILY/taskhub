using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Infrastructure.Storage
{
    public class S3FileStorage : IFileStorage
    {
        private readonly IAmazonS3 _s3;
        private readonly string _bucketName;
        private readonly ILogger<S3FileStorage> _logger;

        public S3FileStorage(IAmazonS3 s3, IConfiguration config, ILogger<S3FileStorage> logger)
        {
            _s3 = s3;
            _logger = logger;
            _bucketName = config["AWS:S3:BucketName"];   // taskhub-files-prod
            
            _logger.LogInformation($"[S3FileStorage] BucketName: {_bucketName ?? "NULL"}");
            
            if (string.IsNullOrEmpty(_bucketName))
            {
                _logger.LogError("[S3FileStorage] BucketName is null or empty! Check AWS:S3:BucketName configuration.");
                throw new InvalidOperationException("S3 BucketName configuration is missing. Check AWS:S3:BucketName in appsettings.");
            }
        }

        public async Task<(string storeName, string url, long size)> SaveAsync(Stream stream, string originalFileName, string contentType)
        {
            try
            {
                var ext = Path.GetExtension(originalFileName);
                var storeName = $"{Guid.NewGuid():N}{ext}";
                
                _logger.LogInformation($"[S3FileStorage] Starting upload - File: {originalFileName}, StoreName: {storeName}, Bucket: {_bucketName}, Size: {stream.Length}");

                var request = new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = storeName,
                    InputStream = stream,
                    ContentType = contentType,
                    // Đảm bảo metadata được lưu
                    ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256
                };

                await _s3.PutObjectAsync(request);
                
                _logger.LogInformation($"[S3FileStorage] Upload successful - StoreName: {storeName}");

                var url = $"https://{_bucketName}.s3.ap-southeast-1.amazonaws.com/{storeName}";

                return (storeName, url, stream.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[S3FileStorage] Upload failed - File: {originalFileName}, Bucket: {_bucketName}");
                throw;
            }
        }

        public async Task<bool> DeleteAsync(string storeName)
        {
            await _s3.DeleteObjectAsync(_bucketName, storeName);
            return true;
        }

        public string GetUrl(string storeName)
        {
            return $"https://{_bucketName}.s3.ap-southeast-1.amazonaws.com/{storeName}";
        }

        public async Task<Stream> DownloadAsync(string storeName)
        {
            try
            {
                _logger.LogInformation($"[S3FileStorage] Starting download - StoreName: {storeName}, Bucket: {_bucketName}");

                var request = new GetObjectRequest
                {
                    BucketName = _bucketName,
                    Key = storeName
                };

                var response = await _s3.GetObjectAsync(request);
                
                // Đọc toàn bộ stream để đảm bảo decryption hoàn tất
                var memoryStream = new MemoryStream();
                await response.ResponseStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;
                
                _logger.LogInformation($"[S3FileStorage] Download successful - StoreName: {storeName}, Size: {memoryStream.Length}");
                
                return memoryStream;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[S3FileStorage] Download failed - StoreName: {storeName}, Bucket: {_bucketName}");
                throw;
            }
        }

        public async Task<string> GetPreSignedDownloadUrlAsync(string storeName, string fileName, int expirationMinutes = 15)
        {
            try
            {
                // Giới hạn thời gian tối đa 1 giờ để bảo mật
                var safeExpiration = Math.Min(expirationMinutes, 60);
                
                // Lấy content-type từ file extension
                var contentType = GetContentTypeFromFileName(fileName);
                
                var request = new GetPreSignedUrlRequest
                {
                    BucketName = _bucketName,
                    Key = storeName,
                    Verb = HttpVerb.GET,
                    Expires = DateTime.UtcNow.AddMinutes(safeExpiration),
                    ResponseHeaderOverrides = new ResponseHeaderOverrides
                    {
                        ContentType = contentType,
                        ContentDisposition = $"attachment; filename=\"{fileName}\""
                    }
                };

                var url = await _s3.GetPreSignedURLAsync(request);
                _logger.LogInformation($"[S3FileStorage] Generated pre-signed URL with ContentType: {contentType}, expires in {safeExpiration} minutes");
                return url;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[S3FileStorage] Failed to generate pre-signed URL for {storeName}");
                throw;
            }
        }
        
        private string GetContentTypeFromFileName(string fileName)
        {
            var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
            return extension switch
            {
                ".txt" => "text/plain",
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".zip" => "application/zip",
                ".json" => "application/json",
                ".xml" => "application/xml",
                _ => "application/octet-stream"
            };
        }
    }
}