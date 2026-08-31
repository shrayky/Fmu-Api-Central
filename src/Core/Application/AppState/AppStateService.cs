using System.Collections.Concurrent;
using Domain.AppState.Interfaces;
using Domain.Attributes;
using Domain.TrueApiIntegration;
using Microsoft.Extensions.DependencyInjection;

namespace Application.AppState
{
    [AutoRegisterService(ServiceLifetime.Singleton)]
    public class AppStateService : IApplicationState
    {
        private bool _dbState { get; set; } = false;
        private bool _needRessart { get; set; } = false;
        private readonly ConcurrentDictionary<string, TrueApiToken> _trueApiTokens = new();

        public bool DbState() => _dbState;

        public void DbStateUpdate(bool isOnLine) => _dbState = isOnLine;

        public bool NeedRestart() => _needRessart;

        public void UpdateNeedRestart(bool need) => _needRessart = need;

        public void UpdateTrueApiToken(string inn, string token, DateTime lifeUntil)
        {
            _trueApiTokens[inn] = new TrueApiToken
            {
                Inn = inn,
                Token = token,
                LiveUntil = lifeUntil
            };
        }

        public TrueApiToken TrueApiToken(string inn)
        {
            if (!_trueApiTokens.TryGetValue(inn, out var data))
                return new TrueApiToken();

            if (data.LiveUntil < DateTime.Now)
                return new TrueApiToken();

            return data;
        }
    }
}
