namespace Domain.Vetis;

public interface IVetisIncomingLoader
{
    Task LoadAsync(CancellationToken cancellationToken);
}

public interface IVetisApplicationCleaner
{
    Task<int> CleanAsync(CancellationToken cancellationToken);
}
