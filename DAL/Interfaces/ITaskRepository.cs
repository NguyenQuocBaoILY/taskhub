using DAL.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DAL.Interfaces
{
    public interface ITaskRepository
    {
        Task<TaskModel> CreateTaskAsync(TaskModel task);
        Task<TaskModel> GetTaskAsync(string projectId, string taskId);
        Task<List<TaskModel>> ListTasksAsync(string projectId, string status = null, string assignee = null);
        Task<List<TaskModel>> ListTasksByAssigneeAsync(string assignee, string projectId = null, string status = null);
        Task<TaskModel> UpdateTaskAsync(TaskModel task);
        Task<bool> UpdateStatusAsync(string projectId, string taskId, string newStatus);
        Task<bool> DeleteTaskAsync(string projectId, string taskId);
    }
}
