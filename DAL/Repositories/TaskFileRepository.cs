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
                        {":pk", $"TASK#{taskId}"}
                    }
                }
            };
            var search = _context.FromQueryAsync<TaskFileModel>(q);
            var items = await search.GetRemainingAsync();

            return items.Select(m =>
            {
                if (m.PK?.StartsWith("FILE#") == true) m.FileId = m.PK.Substring("FILE#".Length);
                if (m.SK?.StartsWith("TASK#") == true)
                {
                    // SK = TASK#<taskId>#FILE...
                    var skVal = m.SK.Substring("TASK#".Length); // <taskId>#FILE...
                    var hashIndex = skVal.IndexOf('#');
                    m.TaskId = hashIndex > 0 ? skVal[..hashIndex] : skVal;
                }
                return m;
            }).ToList();
        }

        public async Task<TaskFileModel> GetFileAsync(string fileId)
        {
            if (string.IsNullOrEmpty(fileId)) return null;
            var pk = $"FILE#{fileId}";

            var scan = _context.ScanAsync<TaskFileModel>(new List<ScanCondition>
            {
                new ScanCondition("PK", ScanOperator.Equal, pk)
            });
            var all = await scan.GetRemainingAsync();
            var m = all.FirstOrDefault();
            if (m == null) return null;

            // Map lại FileId, TaskId giống ListFilesAsync
            if (m.PK?.StartsWith("FILE#") == true)
                m.FileId = m.PK.Substring("FILE#".Length);

            if (m.SK?.StartsWith("TASK#") == true)
            {
                var skVal = m.SK.Substring("TASK#".Length);
                var hashIndex = skVal.IndexOf('#');
                m.TaskId = hashIndex > 0 ? skVal[..hashIndex] : skVal;
            }

            return m;
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