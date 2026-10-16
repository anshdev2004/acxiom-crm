using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services.Interfaces;

namespace AcxiomCRM.Services
{
    public class AuditLogService : IAuditLogService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AuditLogService> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            WriteIndented = false
        };

        public AuditLogService(ApplicationDbContext context, ILogger<AuditLogService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task LogAsync(
            string? userId,
            string? userEmail,
            string action,
            string entityName,
            string? recordId,
            object? oldValue,
            object? newValue,
            string result = "Success",
            string? details = null,
            string? ipAddress = null)
        {
            try
            {
                var audit = new AuditLog
                {
                    UserId = userId,
                    UserEmail = userEmail,
                    Action = action,
                    EntityName = entityName,
                    RecordId = recordId,
                    OldValue = SanitizePayload(oldValue),
                    NewValue = SanitizePayload(newValue),
                    Result = result,
                    Details = details,
                    IpAddress = ipAddress,
                    CreatedDate = DateTime.UtcNow
                };

                _context.AuditLogs.Add(audit);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write audit log entry for Action: {Action}, Entity: {EntityName}", action, entityName);
            }
        }

        public async Task<List<AuditLog>> GetLogsAsync(
            string? searchTerm = null,
            string? action = null,
            string? entityName = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            int page = 1,
            int pageSize = 50)
        {
            var query = BuildQuery(searchTerm, action, entityName, startDate, endDate);
            return await query
                .OrderByDescending(a => a.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetLogsCountAsync(
            string? searchTerm = null,
            string? action = null,
            string? entityName = null,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            var query = BuildQuery(searchTerm, action, entityName, startDate, endDate);
            return await query.CountAsync();
        }

        private IQueryable<AuditLog> BuildQuery(
            string? searchTerm,
            string? action,
            string? entityName,
            DateTime? startDate,
            DateTime? endDate)
        {
            var query = _context.AuditLogs.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                query = query.Where(a =>
                    (a.UserEmail != null && a.UserEmail.ToLower().Contains(term)) ||
                    (a.Details != null && a.Details.ToLower().Contains(term)) ||
                    (a.RecordId != null && a.RecordId.ToLower().Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(action))
                query = query.Where(a => a.Action == action);

            if (!string.IsNullOrWhiteSpace(entityName))
                query = query.Where(a => a.EntityName == entityName);

            if (startDate.HasValue)
                query = query.Where(a => a.CreatedDate >= startDate.Value.Date);

            if (endDate.HasValue)
                query = query.Where(a => a.CreatedDate <= endDate.Value.Date.AddDays(1).AddTicks(-1));

            return query;
        }

        private static string? SanitizePayload(object? value)
        {
            if (value == null) return null;
            if (value is string str)
            {
                return MaskSensitiveTerms(str);
            }

            try
            {
                var json = JsonSerializer.Serialize(value, JsonOptions);
                return MaskSensitiveTerms(json);
            }
            catch
            {
                return value.ToString();
            }
        }

        private static string MaskSensitiveTerms(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            // Mask password fields and auth tokens in audit data
            var pattern = @"(""(?:password|passwordHash|confirmPassword|token|securityStamp|concurrencyStamp)""\s*:\s*"")[^""]*("")";
            return Regex.Replace(input, pattern, "$1***REDACTED***$2", RegexOptions.IgnoreCase);
        }
    }
}
