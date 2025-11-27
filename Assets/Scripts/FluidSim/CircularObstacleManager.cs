using UnityEngine;

namespace FluidSim
{
    public class CircularObstacleManager : MonoBehaviour
    {
        public SpriteRenderer innerCircle;
        public SpriteRenderer outerCircle;

        public void Draw(float radius, float thickness, Vector2 position)
        {
            innerCircle.transform.localScale = new Vector3(radius * 2, radius * 2, 1);
            outerCircle.transform.localScale = new Vector3((radius + thickness) * 2, (radius + thickness) * 2, 1);
            innerCircle.transform.position = position;
            outerCircle.transform.position = position;
        }
    }
}