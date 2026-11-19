using System;
using Cycle33.Core;

namespace Cycle33.Systems
{
    public static class EventBus
    {
        public static event Action<int> OnCycleStart;
        public static event Action<PhaseType> OnPhaseChanged;
        public static event Action<EventType> OnEventStarted;
        public static event Action<int> OnMoneyChanged;
        public static event Action<int> OnCreatureDied; // Mengirim RuntimeId hewan
        public static event Action<bool> OnGameEvaluated; // true = Win, false = Lose
        public static event Action<bool> OnPauseChanged; // true = paused
        
        public static void RaiseCycleStart(int cycle) => OnCycleStart?.Invoke(cycle);
        public static void RaisePhaseChanged(PhaseType phase) => OnPhaseChanged?.Invoke(phase);
        public static void RaiseEventStarted(EventType evt) => OnEventStarted?.Invoke(evt);
        public static void RaiseMoneyChanged(int money) => OnMoneyChanged?.Invoke(money);
        public static void RaiseCreatureDied(int creatureId) => OnCreatureDied?.Invoke(creatureId);
        public static void RaiseGameEvaluated(bool isWin) => OnGameEvaluated?.Invoke(isWin);
        public static void RaisePauseChanged(bool paused) => OnPauseChanged?.Invoke(paused);
    }
}