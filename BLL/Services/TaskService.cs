using DAL.Entities;
using DAL.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BLL.Services
{
    public class TaskService
    {
        private readonly ITaskRepository _taskRepository;

        public TaskService(ITaskRepository taskRepository)
        {
            _taskRepository = taskRepository;
        }

        public async Task<List<TaskModel>> ListTasksAsync(string projectId, string status)
        {
            return await _taskRepository.ListTasksAsync(projectId, status);
        }

        public async Task<TaskModel> CreateTaskAsync(TaskModel task)
        {
            // TODO: validate task nếu cần
            return await _taskRepository.CreateTaskAsync(task);
        }

        public async Task<TaskModel> UpdateTaskAsync(TaskModel task)
        {
            // TODO: validate task nếu cần
            return await _taskRepository.UpdateTaskAsync(task);
        }

        public async Task<bool> DeleteTaskAsync(string taskId, string projectId)
        {
            return await _taskRepository.DeleteTaskAsync(taskId, projectId);
        }
    }
}
