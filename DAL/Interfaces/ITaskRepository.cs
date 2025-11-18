using DAL.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DAL.Interfaces
{
    public interface ITaskRepository
    {
        Task<List<TaskModel>> ListTasksAsync(string projectId, string status);
        Task<TaskModel> CreateTaskAsync(TaskModel task);
        Task<TaskModel> UpdateTaskAsync(TaskModel task);
        Task<bool> DeleteTaskAsync(string taskId, string projectId);
    }
}
