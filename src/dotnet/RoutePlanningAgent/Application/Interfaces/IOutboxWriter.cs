namespace RoutePlanningAgent.Application.Interfaces;

/// <summary>
/// Ghi event vào Outbox trong CÙNG transaction với dữ liệu nghiệp vụ.
/// KHÔNG tự SaveChanges — handler sở hữu transaction và gọi SaveChangesAsync một lần.
/// </summary>
public interface IOutboxWriter
{
    void Enqueue<TEvent>(TEvent evt) where TEvent : class;
}
