using System;
using UnityEngine;

namespace Cycle33.Core
{
    public enum Species { Worm, Sheep, Chicken, Wolf, Civet }
    public enum ObjectType { Grass, Cave, Tree, Stone, Pond }
    public enum EventType { None, Rain, Thunderstorm, FullMoon, BirthSeason }
    public enum PhaseType { CycleStart, EventStart, HabitatStep, AnimalUpkeep, DayPhase, NightPhase, EndOfCycle }
    
    public enum CreatureState 
    { 
        Idle, MovingToFood, Eating, MovingToMate, Mating, 
        Drinking, Sleeping, BeingEaten, Frozen, Dead 
    }

    [Serializable]
    public struct EventCalendarEntry
    {
        public int cycleIndex; // Mulai dari 1
        public EventType eventType;
    }

    [Serializable]
    public struct ObjectPlacement
    {
        public ObjectType objectType;
        public Vector2Int bottomLeftGridPos;
    }
}