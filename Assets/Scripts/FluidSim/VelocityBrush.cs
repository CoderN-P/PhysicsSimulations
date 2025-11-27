using UnityEngine;


namespace FluidSim
{
    public class VelocityBrush : MonoBehaviour
    {
        public SpriteRenderer brushInstance;
        public Color defaultVelocityColor;
        public Color defaultDensityColor;
        public Color activeColor;
        public float brushSize = 1f; // Radius of the brush
        
        public enum BrushMode
        {
            Velocity, Density
        }
        
        public BrushMode brushMode = BrushMode.Velocity;
        private bool isActive = false;
        private Vector2 lastMousePos;

        public void Start()
        {
            brushInstance.sortingOrder = 10; // Ensure brush is on top
            brushInstance.transform.localScale = new Vector3(brushSize * 2, brushSize * 2, 1);
        }
        
        public void SetActive(bool active)
        {
            isActive = active;
            brushInstance.color = isActive ? activeColor : (brushMode == BrushMode.Velocity ? defaultVelocityColor : defaultDensityColor);
        }

        public void Update()
        {
            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            
            brushInstance.transform.position = mousePos;
            
            float scroll = Input.mouseScrollDelta.y;
            
            if (scroll != 0f)
            {
                brushSize += scroll * 0.1f;
                brushSize = Mathf.Clamp(brushSize, 0.1f, 5f);
                brushInstance.transform.localScale = new Vector3(brushSize * 2, brushSize * 2, 1);
            }
            
            
            // On right click switch brush mode
            
            if (Input.GetMouseButtonDown(1))
            {
                if (brushMode == BrushMode.Velocity)
                {
                    brushMode = BrushMode.Density;
                    
                }
                else
                {
                    brushMode = BrushMode.Velocity;
                }
            }
            
            // If dragging and active, apply velocity
            
            
            if (Input.GetMouseButton(0))
            {
                SetActive(true);
            }
            else
            {
                SetActive(false);
            }
            
            if (isActive)
            {
                if (brushMode == BrushMode.Velocity)
                {
                    FluidSim.Instance.fluidRenderer.ApplyVelocityBrush(lastMousePos, mousePos, brushSize);
                }
                else if (brushMode == BrushMode.Density)
                {
                    FluidSim.Instance.fluidRenderer.ApplyDensityBrush(mousePos, brushSize);
                }
            }
            
            lastMousePos = mousePos;
        }
        
        
    }
}