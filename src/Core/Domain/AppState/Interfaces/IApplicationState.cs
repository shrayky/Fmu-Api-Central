namespace Domain.AppState.Interfaces
{
    public interface IApplicationState
    {
        void DbStateUpdate(bool isOnline);
        bool DbState();
        void UpdateNeedRestart(bool need);
        bool NeedRestart();

    }
}
