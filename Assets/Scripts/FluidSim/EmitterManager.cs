using UnityEngine;

namespace FluidSim
{
    public class EmitterManager : MonoBehaviour
    {
        public SpriteRenderer innerCircle;
        public SpriteRenderer outerCircle;
        
        public float rate = 1f; // Particles per second
        public float falloff = 1f; // Intensity falloff
        public float radius = 1f;
        public float speed;
        public float angle;
        public Color c;
        
        public float outlineThickness = 0.1f;
        public bool visible = true;
        public bool enabled = true;
        
        private bool dragging = false;
        private Vector2 offset;
        
        
        public void Start()
        {
            innerCircle.transform.localScale = new Vector3(radius * 2, radius * 2, 1);
            outerCircle.transform.localScale = new Vector3((radius + outlineThickness) * 2, (radius + outlineThickness) * 2, 1);
        }
        
        public void Update()
        {
            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            
            // Change position if dragged
            
            if (Mathf.Pow(mousePos.x - transform.position.x, 2) + Mathf.Pow(mousePos.y - transform.position.y, 2) <= radius * radius &&
                Input.GetMouseButton(0) && visible)
            {
                FluidSim.Instance.fluidRenderer.brushEnabled = false; // Disable velocity brush while dragging emitter
                dragging = true;
            }
            
            if (dragging && Input.GetMouseButton(0))
            {
                transform.position = mousePos;
                innerCircle.transform.position = mousePos;
                outerCircle.transform.position = mousePos;
            }
            else if (dragging && !Input.GetMouseButton(0))
            {
                dragging = false;
                FluidSim.Instance.fluidRenderer.brushEnabled = true; // Re-enable velocity brush when not dragging
            }
            
            innerCircle.transform.localScale = new Vector3(radius * 2, radius * 2, 1);
            outerCircle.transform.localScale = new Vector3((radius + outlineThickness) * 2, (radius + outlineThickness) * 2, 1);

            if (enabled)
            {
                Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                Vector2 worldPos = transform.position;
                Vector2 shiftedPos = worldPos - FluidSim.Instance.fluidRenderer.bottomLeft;
                
                Emitter cur = new Emitter(
                    shiftedPos,
                    radius,
                    rate,
                    falloff,
                    direction,
                    speed,
                    c
                );

                FluidSim.Instance.fluidSolver.ApplyEmitter(cur);
            }

            innerCircle.enabled = visible;
            outerCircle.enabled = visible;
        }
    }
}