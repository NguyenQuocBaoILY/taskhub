using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using DAL.Entities;
using DAL.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DAL.Repositories
{
    public class TaskRepository : ITaskRepository
    {
        private readonly IDynamoDBContext _context;

        public TaskRepository(IAmazonDynamoDB client)
        {
            _context = new DynamoDBContext(client);
        }

        public async Task<List<TaskModel>> ListTasksAsync(string projectId, string status = null)
        {
            if (string.IsNullOrEmpty(projectId))
                return new List<TaskModel>();

            var queryConfig = new QueryOperationConfig
            {
                IndexName = "GSI5-ProjectTasks",
                KeyExpression = new Expression
                {
                    ExpressionStatement = "GSI5PK = :projectId",
                    ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>
            {
                { ":projectId", $"PROJECT#{projectId}" }
            }
                }
            };

            
            var search = _context.FromQueryAsync<TaskModel>(queryConfig);
            var tasks = await search.GetRemainingAsync();

            if (!string.IsNullOrEmpty(status))
                tasks = tasks.Where(t => t.Status == status).ToList();

            return tasks;
        }
        //aa
        public async Task<TaskModel> CreateTaskAsync(TaskModel task)
        {
            if (task == null)
                return null;

            
            if (string.IsNullOrEmpty(task.TaskId))
                task.TaskId = Guid.NewGuid().ToString();

            var pk = $"TASK#{task.TaskId}";
            var sk = $"PROJECT#{task.ProjectId}";

            
            task.PK = pk;
            task.SK = sk;

            
            task.GSI5PK = sk;            
            task.GSI5SK = pk;

            if (string.IsNullOrEmpty(task.CreatedAt))
                task.CreatedAt = DateTime.UtcNow.ToString("o");

            task.UpdatedAt = DateTime.UtcNow.ToString("o");

            await _context.SaveAsync(task);

            return task;
        }

        public async Task<TaskModel> UpdateTaskAsync(TaskModel task)
        {
            if (task == null || string.IsNullOrEmpty(task.TaskId) || string.IsNullOrEmpty(task.ProjectId))
                return null;

            
            task.PK = $"TASK#{task.TaskId}";
            task.SK = $"PROJECT#{task.ProjectId}";

            
            task.GSI5PK = $"PROJECT#{task.ProjectId}";
            task.GSI5SK = $"TASK#{task.TaskId}";

            
            task.UpdatedAt = DateTime.UtcNow.ToString("o");


            await _context.SaveAsync(task);

            return task;
        }


        public async Task<bool> DeleteTaskAsync(string taskId, string projectId)
        {
            if (string.IsNullOrEmpty(taskId) || string.IsNullOrEmpty(projectId))
                return false;

            
            var pk = $"TASK#{taskId}";
            var sk = $"PROJECT#{projectId}";

            
            var existingTask = await _context.LoadAsync<TaskModel>(pk, sk);
            if (existingTask == null)
                return false;

            
            await _context.DeleteAsync(existingTask);

            return true;
        }

    }
}
