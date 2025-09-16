using Domain.AppState.Interfaces;
using Domain.Attributes;
using Microsoft.Extensions.DependencyInjection;

namespace Application.AppState
{
    [AutoRegisterService(ServiceLifetime.Singleton)]
    public class AppStateService : IApplicationState
    {
        private bool _dbState {  get; set; } = false;
        private bool _needRessart {  get; set; } = false;
        public bool DbState() => _dbState;

        public void DbStateUpdate(bool isOnLine) => _dbState = isOnLine;

        public bool NeedRestart() => _needRessart;

        public void UpdateNeedRestart(bool need) => _needRessart = need;
    }
}
