using UnityEngine;

namespace FluidSim
{
    public struct Emitter
    {
        public Vector2 position;
        public float radius;
        public float rate;
        public float falloff;
        public Vector2 direction;
        public float speed;
        public Color c;
        
        public Emitter(Vector2 position, float radius, float rate, float falloff, Vector2 direction, float speed, Color color)
        {
            this.position = position;
            this.radius = radius;
            this.rate = rate;
            this.falloff = falloff;
            this.direction = direction;
            this.speed = speed;
            this.c = color;
        }
    }
}