namespace api.Dtos.Health;

public static class HealthMapper
{
    public static CheckHealthResponseDto ToCheckHealthResponseDto(long allocatedBytes, long maxAllocatedBytes)
    {
        var isHealthy = allocatedBytes < maxAllocatedBytes;
        var status = isHealthy ? "Healthy" : "Unhealthy";
        return new CheckHealthResponseDto
        {
            Status = status,
            Checks =
            [
                new HealthCheckEntryDto
                {
                    Name = "self",
                    Status = status,
                    Description = isHealthy
                        ? "Application is responding and within memory thresholds."
                        : $"Allocated memory ({allocatedBytes} bytes) exceeds threshold ({maxAllocatedBytes} bytes)."
                }
            ]
        };
    }
}
