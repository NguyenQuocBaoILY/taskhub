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

        public async Task<List<TaskModel>> ListTasksAsync(string projectId, string status,string assignee)
        {
            return await _taskRepository.ListTasksAsync(projectId, status, assignee);
        }

        public async Task<TaskModel> CreateTaskAsync(TaskModel task)
        {
            // TODO: validate task nếu cần
            return await _taskRepository.CreateTaskAsync(task);
        }

        public async Task<TaskModel> UpdateTaskAsync(TaskModel task)
        {
            // TODO: validate task 
            return await _taskRepository.UpdateTaskAsync(task);
        }

        public async Task<bool> DeleteTaskAsync(string taskId, string projectId)
        {
            return await _taskRepository.DeleteTaskAsync(taskId, projectId);
        }

        public async Task<TaskModel> GetTaskAsync(string projectId, string taskId)
        {
            return await _taskRepository.GetTaskAsync(projectId, taskId);
        }

        public async Task<bool> UpdateStatusAsync(string projectId, string taskId, string newStatus)
        {
            return await _taskRepository.UpdateStatusAsync(projectId, taskId, newStatus);
        }

        public async Task<List<TaskModel>> ListTasksByAssigneeAsync(string assignee, string projectId = null, string status = null)
        {
            return await _taskRepository.ListTasksByAssigneeAsync(assignee, projectId, status);
        }

        public async Task<List<TaskModel>> ListTasksByProjectAsync(string projectId)
        {
            return await _taskRepository.ListTasksAsync(projectId);
        }

        public async Task<List<TaskModel>> ListTasksByStatusAsync(string projectId, string status)
        {
            return await _taskRepository.ListTasksAsync(projectId, status);
        }

        public async Task<List<TaskModel>> ListTasksByAssigneeAsync(string assignee)
        {
            return await _taskRepository.ListTasksByAssigneeAsync(assignee);
        }
    }
}
