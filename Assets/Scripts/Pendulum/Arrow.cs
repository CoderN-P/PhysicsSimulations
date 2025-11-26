using UnityEngine;

namespace Pendulum
{
    public class Arrow : MonoBehaviour
    {
        
        public SpriteRenderer arrowInstance;
        public LineRenderer lineInstance;
        
        
        public void Draw(Vector2 start, Vector2 end)
        {
            lineInstance.SetPosition(0, start);
            lineInstance.SetPosition(1, end);
            
            arrowInstance.transform.position = end;
            Vector2 direction = (end - start).normalized;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            arrowInstance.transform.rotation = Quaternion.Euler(0, 0, angle-90f);
        }
    }
}