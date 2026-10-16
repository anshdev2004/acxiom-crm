using AcxiomCRM.Models;

namespace AcxiomCRM.Services.Interfaces
{
    public interface IAuditLogService
    {
        Task LogAsync(string? userId, string? userEmail, string action, string entityName, string? recordId, object? oldValue, object? newValue, string result = "Success", string? details = null, string? ipAddress = null);
        Task<List<AuditLog>> GetLogsAsync(string? searchTerm = null, string? action = null, string? entityName = null, DateTime? startDate = null, DateTime? endDate = null, int page = 1, int pageSize = 50);
        Task<int> GetLogsCountAsync(string? searchTerm = null, string? action = null, string? entityName = null, DateTime? startDate = null, DateTime? endDate = null);
    }
}
