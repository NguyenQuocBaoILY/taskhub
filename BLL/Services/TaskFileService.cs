using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DAL.Entities;
using DAL.Interfaces;
using Infrastructure.Storage;

namespace BLL.Services
{
    public class TaskFileService
    {
        private readonly IFileStorage _storage;
        private readonly ITaskFileRepository _repo;

        public TaskFileService(IFileStorage storage, ITaskFileRepository repo)
        {
            _storage = storage;
            _repo = repo;
        }

        public async Task<TaskFileModel> UploadAsync(
            string taskId,
            string projectId,
            string userId,
            string originalFileName,
            string contentType,
            Stream inputStream)
        {
            if (string.IsNullOrWhiteSpace(taskId))
                throw new ArgumentException("taskId required");
            if (string.IsNullOrWhiteSpace(projectId))
                throw new ArgumentException("projectId required");
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("userId required");
            if (inputStream == null || inputStream.Length == 0)
                throw new InvalidOperationException("Empty file stream");

            var (storeName, url, size) = await _storage.SaveAsync(inputStream, originalFileName, contentType);

            var model = new TaskFileModel
            {
                FileId = Guid.NewGuid().ToString(),
                TaskId = taskId,
                ProjectId = projectId,
                FileName = originalFileName,
                StoreName = storeName,
                Url = url,
                ContentType = contentType,
                Size = size,
                UploadedBy = userId,
                UploadedAt = DateTime.UtcNow.ToString("o")
            };

            return await _repo.AddFileAsync(model);
        }

        public Task<List<TaskFileModel>> ListAsync(string taskId)
        {
            return string.IsNullOrEmpty(taskId)
                ? Task.FromResult(new List<TaskFileModel>())
                : _repo.ListFilesAsync(taskId);
        }

        public async Task<bool> DeleteAsync(string fileId)
        {
            if (string.IsNullOrWhiteSpace(fileId))
                return false;

            var found = await _repo.GetFileAsync(fileId);
            if (found == null) return false;

            await _storage.DeleteAsync(found.StoreName);
            return await _repo.DeleteFileAsync(fileId);
        }
    }
}