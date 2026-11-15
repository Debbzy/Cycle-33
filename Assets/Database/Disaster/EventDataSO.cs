using UnityEngine;
using Cycle33.Core;

namespace Cycle33.Data
{
    [CreateAssetMenu(fileName = "EventData", menuName = "Cycle33/EventData")]
    public class EventDataSO : ScriptableObject
    {
        public Cycle33.Core.EventType eventType;
        public string displayName;
        [TextArea] public string description;
        public bool activeAllDay = true;
        public bool activeNightOnly = false;
    }
}