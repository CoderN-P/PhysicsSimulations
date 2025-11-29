using UnityEngine;

namespace FluidSim
{
    public class RectangleObstacleManager : MonoBehaviour
    {
        public SpriteRenderer innerRect;
        public SpriteRenderer outerRect;

        public void Draw(Vector2 size, float thickness, Vector2 position, Quaternion rotation)
        {
            innerRect.transform.localScale = new Vector3(size.x, size.y, 1);
            outerRect.transform.localScale = new Vector3(size.x + thickness * 2, size.y + thickness * 2, 1);
            innerRect.transform.position = position;
            outerRect.transform.position = position;
            
            innerRect.transform.rotation = rotation;
            outerRect.transform.rotation = rotation;
        }
    }
}