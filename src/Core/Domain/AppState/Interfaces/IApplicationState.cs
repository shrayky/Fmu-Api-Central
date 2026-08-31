using Domain.TrueApiIntegration;

namespace Domain.AppState.Interfaces
{
    public interface IApplicationState
    {
        void DbStateUpdate(bool isOnline);
        bool DbState();
        void UpdateNeedRestart(bool need);
        bool NeedRestart();

        void UpdateTrueApiToken(string inn, string token, DateTime lifeUntil);

        TrueApiToken TrueApiToken(string inn);
    }
}
