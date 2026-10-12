using api.Repositories.Logs.Dtos;

namespace api.Repositories.Logs.Interfaces;

public interface ILogRepository
{
    Task<LogPageDto> SearchAsync(SearchLogsDto query, CancellationToken cancellationToken = default);
}
