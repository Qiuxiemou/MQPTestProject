using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.FPS.Game
{
    public class GameEvent
    {
        public DateTime Timestamp { get; private set; }

        public GameEvent()
        {
            Timestamp = DateTime.Now;
        }
    }

    // A simple Event System that can be used for remote systems communication
    public static class EventManager
    {
        static readonly Dictionary<Type, Action<GameEvent>> s_Events = new Dictionary<Type, Action<GameEvent>>();

        static readonly Dictionary<Delegate, Action<GameEvent>> s_EventLookups =
            new Dictionary<Delegate, Action<GameEvent>>();

        public static void AddListener<T>(Action<T> evt) where T : GameEvent
        {
            if (!s_EventLookups.ContainsKey(evt))
            {
                Action<GameEvent> newAction = (e) => evt((T) e);
                s_EventLookups[evt] = newAction;

                if (s_Events.TryGetValue(typeof(T), out Action<GameEvent> internalAction))
                    s_Events[typeof(T)] = internalAction += newAction;
                else
                    s_Events[typeof(T)] = newAction;
            }
        }

        public static void RemoveListener<T>(Action<T> evt) where T : GameEvent
        {
            if (s_EventLookups.TryGetValue(evt, out var action))
            {
                if (s_Events.TryGetValue(typeof(T), out var tempAction))
                {
                    tempAction -= action;
                    if (tempAction == null)
                        s_Events.Remove(typeof(T));
                    else
                        s_Events[typeof(T)] = tempAction;
                }

                s_EventLookups.Remove(evt);
            }
        }

        public static void Broadcast(GameEvent evt)
        {
            if (s_Events.TryGetValue(evt.GetType(), out var action))
                action.Invoke(evt);
        }

        public static void Clear()
        {
            s_Events.Clear();
            s_EventLookups.Clear();
        }
    }


    // Event Type:
 
    // FireShot
    public class FireShotEvent : GameEvent
    {
        public string ShooterId;
        public string WeaponId;
    }

    // Hit / Damage Taken
    public class HitEvent : GameEvent
    {
        public string ShooterId;   // Who fired
        public string TargetId;    // Who got hit
        public float Damage;       // Damage dealt
    }


    // Death
    public class DeathEvent : GameEvent
    {
        
        public string VictimId;    // Who died
        public string KillerId;    // Who killed them (could be null if environment)
    }

    public class HitCsvEvent : GameEvent
    {
        public string EventType;        // "client_hit" / "world_hit" / "server_applied"
        public string ShooterId;
        public string TargetId;
        public float  Damage;
        public float  ForwardDelayMs;

        public Vector3 HitPoint;
        public bool ShotAroundCorner;
        public bool AcceptShot;
        public float HitsErrorAngle;

        public Transform ClientTf;
        public Health    ClientHealth;
        public Transform ServerTf;
        public Health    ServerHealth;
    }

    public class KeyPressEvent : GameEvent
    {
        public string PlayerId;
        public KeyCode Key;
        public bool Pressed;
    }

    public class ViewSampleEvent : GameEvent
    {
        public string PlayerId;
        public Vector3 Position;     
        public Vector3 RotationEuler; 
        public Vector3 Forward;     
    }
}