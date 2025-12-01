using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using DAL.Entities;
using DAL.Interfaces;

namespace DAL.Repositories
{
    public class TaskFileRepository : ITaskFileRepository
    {
        private readonly IDynamoDBContext _context;

        public TaskFileRepository(IDynamoDBContext context)
        {
            _context = context;
        }

        public async Task<TaskFileModel> AddFileAsync(TaskFileModel file)
        {
            file.InitKeys();
            await _context.SaveAsync(file);
            return file;
        }

        public async Task<List<TaskFileModel>> ListFilesAsync(string taskId)
        {
            if (string.IsNullOrEmpty(taskId))
                return new List<TaskFileModel>();

            var q = new QueryOperationConfig
            {
                IndexName = "GSI1-TaskFiles",
                KeyExpression = new Expression
                {
                    ExpressionStatement = "GSI1PK = :pk",
                    ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>
                    {
                        {":pk", $"TASK#{taskId}#FILE"}
                    }
                }
            };
            var search = _context.FromQueryAsync<TaskFileModel>(q);
            var items = await search.GetRemainingAsync();

            return items.Select(m =>
            {
                if (m.PK?.StartsWith("FILE#") == true) m.FileId = m.PK.Substring("FILE#".Length);
                if (m.SK?.StartsWith("TASK#") == true) m.TaskId = m.SK.Substring("TASK#".Length);
                return m;
            }).ToList();
        }

        public async Task<TaskFileModel> GetFileAsync(string fileId)
        {
            if (string.IsNullOrEmpty(fileId)) return null;
            var pk = $"FILE#{fileId}";
            var sk = "TASK#"; // we don't know taskId -> need to scan? Better store a constant SK? We used SK=TASK#{taskId} so we must load with full key.
            // Alternative: query by PK only is not supported; need full range key. We will list with scan fallback:
            var scan = _context.ScanAsync<TaskFileModel>(new List<ScanCondition>
            {
                new ScanCondition("PK", ScanOperator.Equal, pk)
            });
            var all = await scan.GetRemainingAsync();
            return all.FirstOrDefault();
        }

        public async Task<bool> DeleteFileAsync(string fileId)
        {
            var file = await GetFileAsync(fileId);
            if (file == null) return false;
            await _context.DeleteAsync<TaskFileModel>(file.PK, file.SK);
            return true;
        }
    }
}