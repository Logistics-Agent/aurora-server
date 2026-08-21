namespace Shared.AI;

public interface IApiKeyPool<out T>
{
    T GetNext();
}
