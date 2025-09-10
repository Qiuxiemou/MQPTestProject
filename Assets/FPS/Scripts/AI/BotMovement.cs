using UnityEngine;
using UnityEngine.Events;
using UnityEngine.AI;

namespace Unity.FPS.Game
{
    public class BotMovement : MonoBehaviour
    {

        [Tooltip("Waypoints")]
        public Transform[] waypoints;  // Assign your waypoints in inspector

        [Tooltip("How close the bot needs to get to a waypoint")]
        public float waypointTolerance = 0.5f; // How close the bot needs to get to a waypoint
        private int currentWaypoint = 0;

        [Header("Movement Settings")]
        [Tooltip("Speed of the Bot")]
        public float speed = 3.5f;

        [Tooltip("How close the bot stops to the target")]
        public float stoppingDistance = 0.1f; // How close the bot stops to the target
        private NavMeshAgent agent;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            agent = GetComponent<NavMeshAgent>();

            // Set agent properties
            agent.speed = speed;
            agent.stoppingDistance = stoppingDistance;
            agent.autoBraking = true; // Slows down near waypoints
            agent.updateRotation = true;

            // Move to the first waypoint
            if (waypoints.Length > 0)
            {
                agent.SetDestination(waypoints[currentWaypoint].position);
            }
        }

        // Update is called once per frame
        void Update()
        {
            if (waypoints.Length == 0)
                return;

            // Check if the bot is close enough to the current waypoint
            if (!agent.pathPending && agent.remainingDistance <= waypointTolerance)
            {
                // Advance to the next waypoint
                currentWaypoint = (currentWaypoint + 1) % waypoints.Length;
                agent.SetDestination(waypoints[currentWaypoint].position);
            }
        }
    }
}