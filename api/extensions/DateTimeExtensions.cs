namespace api.Extensions;

public static class DateTimeExtensions
{
    // Normalizes a client-supplied timestamp to UTC. A value without offset/kind is taken as UTC already.
    public static DateTime AsUtc(this DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    public static DateTime? AsUtc(this DateTime? value) => value?.AsUtc();
}
