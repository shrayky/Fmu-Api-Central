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

        IReadOnlyList<TrueApiToken> TrueApiTokens();

        void UpdateXApiKey(string inn, string key, DateTime lifeUntil);

        TrueApiToken XApiKey(string inn);

        IReadOnlyList<TrueApiToken> XApiKeys();

        void PostponeXApiKey(string inn, DateTime nextAttempt);

        DateTime? XApiKeyPostponedUntil(string inn);

        void ClearXApiKeyPostpone(string inn);

        void MarkGisMtPushPending();
        bool GisMtPushPending();
        void ClearGisMtPushPending();
    }
}
