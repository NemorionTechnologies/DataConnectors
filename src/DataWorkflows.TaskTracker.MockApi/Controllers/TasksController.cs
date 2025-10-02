using DataWorkflows.TaskTracker.MockApi.Models;
using DataWorkflows.TaskTracker.MockApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace DataWorkflows.TaskTracker.MockApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TasksController : ControllerBase
{
    private readonly TaskStore _taskStore;
    private readonly AuthService _authService;
    private readonly ILogger<TasksController> _logger;

    public TasksController(
        TaskStore taskStore,
        AuthService authService,
        ILogger<TasksController> logger)
    {
        _taskStore = taskStore;
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Get all tasks (requires authentication).
    /// </summary>
    [HttpGet]
    public IActionResult GetAll()
    {
        if (!IsAuthorized())
            return Unauthorized(new { message = "Authorization token required" });

        var tasks = _taskStore.GetAll();
        return Ok(tasks);
    }

    /// <summary>
    /// Get a specific task by ID (requires authentication).
    /// </summary>
    [HttpGet("{id}")]
    public IActionResult GetById(string id)
    {
        if (!IsAuthorized())
            return Unauthorized(new { message = "Authorization token required" });

        var task = _taskStore.GetById(id);
        if (task == null)
            return NotFound(new { message = $"Task {id} not found" });

        return Ok(task);
    }

    /// <summary>
    /// Create a new task (requires authentication).
    /// </summary>
    [HttpPost]
    public IActionResult Create([FromBody] CreateTaskRequest request)
    {
        if (!IsAuthorized())
            return Unauthorized(new { message = "Authorization token required" });

        var task = new TaskItem
        {
            Title = request.Title,
            Description = request.Description,
            Status = request.Status
        };

        var created = _taskStore.AddTask(task);
        _logger.LogInformation("Created task: {TaskId}", created.Id);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Update an existing task (requires authentication).
    /// </summary>
    [HttpPut("{id}")]
    public IActionResult Update(string id, [FromBody] UpdateTaskRequest request)
    {
        if (!IsAuthorized())
            return Unauthorized(new { message = "Authorization token required" });

        var task = new TaskItem
        {
            Title = request.Title,
            Description = request.Description,
            Status = request.Status
        };

        var success = _taskStore.UpdateTask(id, task);
        if (!success)
            return NotFound(new { message = $"Task {id} not found" });

        _logger.LogInformation("Updated task: {TaskId}", id);
        return Ok(task);
    }

    /// <summary>
    /// Delete a task (requires authentication).
    /// </summary>
    [HttpDelete("{id}")]
    public IActionResult Delete(string id)
    {
        if (!IsAuthorized())
            return Unauthorized(new { message = "Authorization token required" });

        var success = _taskStore.DeleteTask(id);
        if (!success)
            return NotFound(new { message = $"Task {id} not found" });

        _logger.LogInformation("Deleted task: {TaskId}", id);
        return NoContent();
    }


    private bool IsAuthorized()
    {
        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        if (authHeader?.StartsWith("Bearer ") == true)
        {
            var token = authHeader.Substring("Bearer ".Length).Trim();
            return _authService.ValidateToken(token);
        }
        return false;
    }
}
