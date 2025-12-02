using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using DAL.Entities;
using DAL.Interfaces;

namespace DAL.Repositories
{
    public class TaskRepository : ITaskRepository
    {
        private readonly IDynamoDBContext _context;

        public TaskRepository(IDynamoDBContext context)
        {
            _context = context;
        }

        public async Task<TaskModel> CreateTaskAsync(TaskModel task)
        {
            // Always auto-generate TaskId on create
            task.TaskId = Guid.NewGuid().ToString();

            var now = DateTime.UtcNow.ToString("o");
            task.CreatedAt = now;
            task.UpdatedAt = now;
            if (string.IsNullOrEmpty(task.AssignedAt))
                task.AssignedAt = now;
            if (string.IsNullOrEmpty(task.Status))
                task.Status = "TODO";

            task.InitializeKeys();
            await _context.SaveAsync(task);
            return task;
        }

        public async Task<TaskModel> GetTaskAsync(string projectId, string taskId)
        {
            if (string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(taskId))
                return null;

            var pk = $"PROJECT#{projectId}";
            var sk = $"TASK#{taskId}";
            var task = await _context.LoadAsync<TaskModel>(pk, sk);
            if (task != null)
            {
                // map back IDs
                task.ProjectId = projectId;
                task.TaskId = taskId;
            }
            return task;
        }

        public async Task<List<TaskModel>> ListTasksAsync(string projectId, string status = null, string assignee = null)
        {
            if (string.IsNullOrEmpty(projectId))
                return new List<TaskModel>();

            // Ưu tiên filter chuyên biệt:
            // 1. Nếu có assignee -> dùng GSI3 (retrieval nhanh) rồi filter projectId + status.
            if (!string.IsNullOrEmpty(assignee))
            {
                var byAssignee = await ListTasksByAssigneeAsync(assignee, projectId, status);
                return byAssignee;
            }

            // 2. Nếu có status -> dùng GSI4
            if (!string.IsNullOrEmpty(status))
            {
                var gsi4 = new QueryOperationConfig
                {
                    IndexName = "GSI4-TasksByStatus",
                    KeyExpression = new Expression
                    {
                        ExpressionStatement = "GSI4PK = :pk",
                        ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>
                        {
                            {":pk", $"PROJECT#{projectId}#STATUS#{status}"}
                        }
                    }
                };
                var search = _context.FromQueryAsync<TaskModel>(gsi4);
                var items = await search.GetRemainingAsync();
                return MapIds(items);
            }

            // 3. Mặc định: liệt kê task theo project -> dùng GSI5-ProjectTasks
            var gsi5 = new QueryOperationConfig
            {
                IndexName = "GSI5-ProjectTasks",
                KeyExpression = new Expression
                {
                    ExpressionStatement = "GSI5PK = :pk",
                    ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>
                    {
                        {":pk", $"PROJECT#{projectId}"}
                    }
                }
            };
            var searchAll = _context.FromQueryAsync<TaskModel>(gsi5);
            var all = await searchAll.GetRemainingAsync();
            return MapIds(all);
        }

        public async Task<List<TaskModel>> ListTasksByAssigneeAsync(string assignee, string projectId = null, string status = null)
        {
            if (string.IsNullOrEmpty(assignee))
                return new List<TaskModel>();

            var gsi3 = new QueryOperationConfig
            {
                IndexName = "GSI3-UserAssignedTasks",
                KeyExpression = new Expression
                {
                    ExpressionStatement = "GSI3PK = :pk",
                    ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>
                    {
                        {":pk", $"USER#{assignee}"}
                    }
                }
            };

            var search = _context.FromQueryAsync<TaskModel>(gsi3);
            var items = await search.GetRemainingAsync();

            var filtered = items.Where(t =>
                (projectId == null || t.PK == $"PROJECT#{projectId}") &&
                (status == null || t.Status == status)
            ).ToList();

            return MapIds(filtered);
        }

        public async Task<TaskModel> UpdateTaskAsync(TaskModel task)
        {
            if (string.IsNullOrEmpty(task.ProjectId) || string.IsNullOrEmpty(task.TaskId))
                return null;

            task.UpdatedAt = DateTime.UtcNow.ToString("o");
            task.InitializeKeys();
            await _context.SaveAsync(task);
            return task;
        }

        public async Task<bool> UpdateStatusAsync(string projectId, string taskId, string newStatus)
        {
            var existing = await GetTaskAsync(projectId, taskId);
            if (existing == null) return false;

            existing.Status = newStatus;
            existing.UpdatedAt = DateTime.UtcNow.ToString("o");
            existing.InitializeKeys();
            await _context.SaveAsync(existing);
            return true;
        }

        public async Task<bool> DeleteTaskAsync(string projectId, string taskId)
        {
            if (string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(taskId))
                return false;

            var pk = $"PROJECT#{projectId}";
            var sk = $"TASK#{taskId}";
            await _context.DeleteAsync<TaskModel>(pk, sk);
            return true;
        }

        private List<TaskModel> MapIds(IEnumerable<TaskModel> items)
        {
            var list = new List<TaskModel>();
            foreach (var t in items)
            {
                if (t.SK?.StartsWith("TASK#") == true && t.PK?.StartsWith("PROJECT#") == true)
                {
                    t.TaskId = t.SK.Substring("TASK#".Length);
                    t.ProjectId = t.PK.Substring("PROJECT#".Length);
                }
                list.Add(t);
            }
            return list;
        }
    }
}