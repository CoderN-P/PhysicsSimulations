using UnityEngine;

namespace FluidSim
{
    public class EmitterManager : MonoBehaviour
    {
        public SpriteRenderer emitterInstance;
        public Vector2 position;
        public float rate = 1f; // Particles per second
        public float falloff = 1f; // Intensity falloff
        public float radius = 1f;
        public bool visible = true;
        
        public void Start()
        {
            emitterInstance.transform.localScale = new Vector3(radius * 2, radius * 2, 1);
            emitterInstance.transform.position = position;
        }
        
        public void Update()
        {
            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            
            // Change position if dragged
            
            if (Mathf.Pow(mousePos.x - position.x, 2) + Mathf.Pow(mousePos.y - position.y, 2) <= radius * radius &&
                Input.GetMouseButton(0) && visible)
            {
                FluidSim.Instance.fluidRenderer.brushEnabled = false; // Disable velocity brush while dragging emitter
                position = mousePos;
                emitterInstance.transform.position = position;
            }
            else
            {
                if (!FluidSim.Instance.fluidRenderer.brushEnabled)
                {
                    FluidSim.Instance.fluidRenderer.brushEnabled = true; // Re-enable velocity brush
                }
            }
            
            emitterInstance.transform.localScale = new Vector3(radius * 2, radius * 2, 1);
            FluidSim.Instance.fluidRenderer.RegisterEmitter(position, radius, rate, falloff);
            
            emitterInstance.enabled = visible;
        }
    }
}