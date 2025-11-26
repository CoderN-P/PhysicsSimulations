using UnityEngine;

namespace SoftBodySim
{
    public class Point
    {
        public float mass;
        public float radius;
        public Vector2 gravity;
        public Vector2 position = Vector2.zero;
        public Vector2 velocity = Vector2.zero;
        public Vector2 springForce = Vector2.zero;

        public Point(float mass, float radius, Vector2 gravity, Vector2 position)
        {
            this.mass = mass;
            this.radius = radius;
            this.gravity = gravity;
            this.position = position;
        }

        public void Step(float DT, Vector2 worldPos)
        {
            Vector2 force = springForce;
            Vector2 acceleration = force / mass + gravity;

            velocity += acceleration * DT;

            Vector2 tempPos = position + worldPos;
            tempPos += velocity * DT;


            if (tempPos.y - radius < 0)
            {
                tempPos.y = radius;
                velocity.y *= -0.5f; // simple bounce with energy loss
            }

            position = tempPos - worldPos;
            springForce = Vector2.zero; // reset for next step
        }
    }
}