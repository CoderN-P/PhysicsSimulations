using UnityEngine;

namespace SoftBodySim
{
    public class Spring
    {
        public float restLength;
        public float springConstant;
        public float dampingFactor;
        public Point pointA, pointB;

        public Spring(Point pointA, Point pointB, float restLength, float springConstant, float dampingFactor)
        {
            this.pointA = pointA;
            this.pointB = pointB;
            this.restLength = restLength;
            this.springConstant = springConstant;
            this.dampingFactor = dampingFactor;
        }

        public Vector2 GetForceOnA(Vector2 worldPos)
        {
            Vector2 aPos = pointA.position + worldPos;
            Vector2 bPos = pointB.position + worldPos;
            Vector2 delta = bPos - aPos;
            float dist = delta.magnitude;

            // Normalize safely
            if (dist < 1e-6f) return Vector2.zero;
            Vector2 dir = delta / dist;

            // Spring force: Hooke’s law
            float springForce = springConstant * (dist - restLength);

            // Damping force
            Vector2 vRel = pointB.velocity - pointA.velocity;
            float dampingForce = dampingFactor * Vector2.Dot(vRel, dir);

            float forceMag = springForce + dampingForce;

            // Force on A from the spring
            return dir * forceMag;
        }

        public Vector2 GetForceOnB(Vector2 worldPos)
        {
            // Equal and opposite
            return -GetForceOnA(worldPos);
        }

        public void Step(Vector2 worldPos)
        {
            pointA.springForce += GetForceOnA(worldPos);
            pointB.springForce += GetForceOnB(worldPos);
        }
    }
}