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
        }

        public async Task<(string storeName, string url, long size)> SaveAsync(Stream stream, string originalFileName, string contentType)
        {
            var ext = Path.GetExtension(originalFileName);
            var storeName = $"{Guid.NewGuid():N}{ext}";

            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = storeName,
                InputStream = stream,
                ContentType = contentType
            };

            await _s3.PutObjectAsync(request);

            var url = $"https://{_bucketName}.s3.ap-southeast-1.amazonaws.com/{storeName}";

            return (storeName, url, stream.Length);
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
    }
}