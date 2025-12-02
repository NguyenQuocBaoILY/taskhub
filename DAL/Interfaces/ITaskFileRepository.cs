using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading.Tasks;
using DAL.Entities;

namespace DAL.Interfaces
{
    public interface ITaskFileRepository
    {
        Task<TaskFileModel> AddFileAsync(TaskFileModel file);
        Task<List<TaskFileModel>> ListFilesAsync(string taskId);
        Task<bool> DeleteFileAsync(string fileId);
        Task<TaskFileModel> GetFileAsync(string fileId);
    }
}
