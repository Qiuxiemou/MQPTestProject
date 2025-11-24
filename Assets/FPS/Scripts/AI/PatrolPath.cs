using System.Collections.Generic;
using UnityEngine;

namespace Unity.FPS.AI
{
    public class PatrolPath : MonoBehaviour
    {
        [Tooltip("Enemies that will be assigned to this path on Start")]
        public List<EnemyController> EnemiesToAssign = new List<EnemyController>();

        [Tooltip("The Nodes making up the path")]
        public List<Transform> PathNodes = new List<Transform>();

        [Tooltip("LayerMask determining what is considered an obstacle for view checks")]
        public LayerMask ObstacleMask;

        [Tooltip("Radius around each node to check for view obstruction")]
        public float ViewCheckRadius = 5f;

        void Start()
        {
            foreach (var enemy in EnemiesToAssign)
            {
                enemy.PatrolPath = this;
            }
        }

        public Vector3 GetVisiblePointNearNode(int nodeIndex, Transform ai)
        {
            if (!PathNodes[nodeIndex]) return Vector3.zero;

            Vector3 nodePos = PathNodes[nodeIndex].position;

            // Try multiple directions around the node
            int resolution = 6;
            for (int i = 0; i < resolution; i++)
            {
                float angle = i * (360f / resolution);
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * ViewCheckRadius;
                Vector3 testPos = nodePos + offset;

                // Check if NavMesh can reach this point
                if (UnityEngine.AI.NavMesh.SamplePosition(testPos, out UnityEngine.AI.NavMeshHit navHit, 2f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    // Check visibility from this position
                    if (!Physics.Linecast(navHit.position, nodePos, ObstacleMask))
                    {
                        return navHit.position;
                    }
                }
            }

            return nodePos; // fallback: move to node if no viewpoint found
        }

        public float GetDistanceToNode(Vector3 origin, int destinationNodeIndex)
        {
            if (destinationNodeIndex < 0 || destinationNodeIndex >= PathNodes.Count ||
                PathNodes[destinationNodeIndex] == null)
            {
                return -1f;
            }

            return (PathNodes[destinationNodeIndex].position - origin).magnitude;
        }

        public Vector3 GetPositionOfPathNode(int nodeIndex)
        {
            if (nodeIndex < 0 || nodeIndex >= PathNodes.Count || PathNodes[nodeIndex] == null)
            {
                return Vector3.zero;
            }

            return PathNodes[nodeIndex].position;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            for (int i = 0; i < PathNodes.Count; i++)
            {
                int nextIndex = i + 1;
                if (nextIndex >= PathNodes.Count)
                {
                    nextIndex -= PathNodes.Count;
                }

                Gizmos.DrawLine(PathNodes[i].position, PathNodes[nextIndex].position);
                Gizmos.DrawSphere(PathNodes[i].position, 0.1f);
            }
        }
    }
}