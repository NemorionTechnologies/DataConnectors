using DataWorkflows.TaskTracker.MockApi.Models;

namespace DataWorkflows.TaskTracker.MockApi.Services;

/// <summary>
/// In-memory task storage for mock API.
/// </summary>
public class TaskStore
{
    private readonly Dictionary<string, TaskItem> _tasks = new();
    private int _nextId = 1;

    public TaskStore()
    {
        // Seed with sample data
        AddTask(new TaskItem
        {
            Id = GetNextId(),
            Title = "Sample Task 1",
            Description = "This is a sample task",
            Status = "In Progress"
        });
        AddTask(new TaskItem
        {
            Id = GetNextId(),
            Title = "Sample Task 2",
            Description = "Another sample task",
            Status = "New"
        });
    }

    public IEnumerable<TaskItem> GetAll() => _tasks.Values;

    public TaskItem? GetById(string id) => _tasks.GetValueOrDefault(id);

    public TaskItem AddTask(TaskItem task)
    {
        if (string.IsNullOrEmpty(task.Id))
        {
            task.Id = GetNextId();
        }
        _tasks[task.Id] = task;
        return task;
    }

    public bool UpdateTask(string id, TaskItem task)
    {
        if (!_tasks.ContainsKey(id)) return false;
        task.Id = id;
        task.UpdatedAt = DateTime.UtcNow;
        _tasks[id] = task;
        return true;
    }

    public bool DeleteTask(string id) => _tasks.Remove(id);

    private string GetNextId() => (_nextId++).ToString();
}
