using UnityEngine;

namespace FluidSim.Obstacles
{
    public class ObstacleManager : MonoBehaviour
    {
        public ObstacleType type;
        public float obstacleOutlineThickness;
        
        [Header("Circle Settings")] 
        public float radius;
        public GameObject circularObstaclePrefab;
        private CircularObstacleManager circularObstacleManager;
        
        [Header("Rectangle Settings")]
        public Vector2 size;
        public GameObject rectangleObstaclePrefab;
        private RectangleObstacleManager rectangleObstacleManager;

        private bool dragging;
        
        public void Start()
        {
            if (type == ObstacleType.Circle)
            {
                circularObstacleManager = Instantiate(circularObstaclePrefab, transform)
                    .GetComponent<CircularObstacleManager>();
                ModifySolidMapForCircle(transform.position);
            } else if (type == ObstacleType.Rectangle)
            {
                rectangleObstacleManager = Instantiate(rectangleObstaclePrefab, transform)
                    .GetComponent<RectangleObstacleManager>();
                
                Matrix4x4 transformMatrix = this.transform.localToWorldMatrix;
                ModifySolidMapForRectangle(transformMatrix);
            }
        }

        public void Update()
        {
            DrawObstacle();
            UpdateObstaclePosition();
        }

        public void UpdateObstaclePosition()
        {
            if (type == ObstacleType.Circle)
                UpdateCircularObstaclePosition();
            else if (type == ObstacleType.Rectangle)
                UpdateRectangleObstaclePosition();
        }
        
        public void UpdateCircularObstaclePosition()
        {
            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            
            // Check if mouse is within obstacle radius and left mouse button is held
            
            if (Vector2.Distance(mousePos, transform.position) <= radius && Input.GetMouseButton(0))
            {
                FluidSim.Instance.fluidRenderer.brushEnabled = false; // Disable brush while dragging obstacle
                dragging = true;
            }
            
            if (dragging && Input.GetMouseButton(0))
            {
                Vector2 oldPos = transform.position;
                transform.position = mousePos;
                ModifySolidMapForCircle(oldPos);
            }
            else if (dragging && !Input.GetMouseButton(0))
            {
                dragging = false;
                FluidSim.Instance.fluidRenderer.brushEnabled = true; // Re-enable brush after dragging
            }
        }
        
        bool InsideRect(Transform rectTransform, Vector2 worldPoint)
        {
            Vector2 local = rectTransform.InverseTransformPoint(worldPoint);

            Vector2 half = size / 2;

            return Mathf.Abs(local.x) <= half.x &&
                   Mathf.Abs(local.y) <= half.y;
        }
        
        public void UpdateRectangleObstaclePosition()
        {
            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            
            // Check if mouse is within obstacle bounds and left mouse button is held
            if (InsideRect(transform, mousePos) && Input.GetMouseButton(0))
            {
                FluidSim.Instance.fluidRenderer.brushEnabled = false; // Disable brush while dragging obstacle
                dragging = true;
            }
            
            if (dragging && Input.GetMouseButton(0))
            {
                Matrix4x4 oldMatrix = transform.localToWorldMatrix;
                transform.position = mousePos;
                ModifySolidMapForRectangle(oldMatrix);
            }
            else if (dragging && !Input.GetMouseButton(0))
            {
                dragging = false;
                FluidSim.Instance.fluidRenderer.brushEnabled = true; // Re-enable brush after dragging
            }
        }
        
        public void DrawObstacle()
        {
            if (type == ObstacleType.Circle)
            {
                circularObstacleManager.Draw(radius, obstacleOutlineThickness, transform.position);
            }
            else if (type == ObstacleType.Rectangle)
            {
                rectangleObstacleManager.Draw(size, obstacleOutlineThickness, transform.position, transform.rotation);
            }
        }
        
        public void ModifySolidMapForCircle(Vector2 oldPos)
        {
            float cellSize = FluidSim.Instance.cellSize;
            int width = FluidSim.Instance.width;
            int height = FluidSim.Instance.height;
            Vector2 bottomLeft = FluidSim.Instance.fluidRenderer.bottomLeft;

            int radiusInCells = Mathf.CeilToInt(radius / cellSize);
            
            int oldCenterI = Mathf.FloorToInt((oldPos.x - bottomLeft.x) / cellSize);
            int oldCenterJ = Mathf.FloorToInt((oldPos.y - bottomLeft.y) / cellSize);
            
            for (int i = oldCenterI - radiusInCells; i <= oldCenterI + radiusInCells; i++)
            {
                for (int j = oldCenterJ - radiusInCells; j <= oldCenterJ + radiusInCells; j++)
                {
                    if (i >= 0 && i < width && j >= 0 && j < height)
                    {
                        float maxDist = 0f;

                        Vector2 C = new(oldCenterI, oldCenterJ);

                        Vector2 c0 = new(i,   j);
                        Vector2 c1 = new(i+1, j);
                        Vector2 c2 = new(i,   j+1);
                        Vector2 c3 = new(i+1, j+1);

                        maxDist = Mathf.Max(
                            Vector2.Distance(c0, C),
                            Vector2.Distance(c1, C),
                            Vector2.Distance(c2, C),
                            Vector2.Distance(c3, C)
                        );
                        
                        if (maxDist <= radiusInCells-4*cellSize)
                        {
                            FluidSim.Instance.fluidSolver.solid[i, j] = 0; // Mark cell as fluid
                        }
                    }
                }
            }
            
            int newCenterI = Mathf.FloorToInt((transform.position.x - bottomLeft.x) / cellSize);
            int newCenterJ = Mathf.FloorToInt((transform.position.y - bottomLeft.y) / cellSize);
            
            for (int i = newCenterI - radiusInCells; i <= newCenterI + radiusInCells; i++)
            {
                for (int j = newCenterJ - radiusInCells; j <= newCenterJ + radiusInCells; j++)
                {
                    if (i >= 0 && i < width && j >= 0 && j < height)
                    {

                        float maxDist = 0f;

                        Vector2 C = new(newCenterI, newCenterJ);

                        Vector2 c0 = new(i,   j);
                        Vector2 c1 = new(i+1, j);
                        Vector2 c2 = new(i,   j+1);
                        Vector2 c3 = new(i+1, j+1);

                        maxDist = Mathf.Max(
                            Vector2.Distance(c0, C),
                            Vector2.Distance(c1, C),
                            Vector2.Distance(c2, C),
                            Vector2.Distance(c3, C)
                        );
                        
                        if (maxDist <= radiusInCells-4*cellSize)
                        {
                            FluidSim.Instance.fluidSolver.solid[i, j] = 1; // Mark cell as solid
                        }
                    }
                }
            }
            
            FluidSim.Instance.fluidSolver.pressure = new float[width, height]; // Reset pressure field
        }
        
        public void ModifySolidMapForRectangle(Matrix4x4 oldMatrix)
        {
            float halfW = size.x * 0.5f;
            float halfH = size.y * 0.5f;
            
            float cellSize = FluidSim.Instance.cellSize;
            int width = FluidSim.Instance.width;
            int height = FluidSim.Instance.height;
            Vector2 bottomLeft = FluidSim.Instance.fluidRenderer.bottomLeft;

            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    Vector2 worldPos = new Vector2(
                        (i + 0.5f) * cellSize,
                        (j + 0.5f) * cellSize
                    ) + bottomLeft;
                    
                    Vector2 local = oldMatrix.inverse.MultiplyPoint3x4(worldPos);

  
                    bool insideOld =
                        Mathf.Abs(local.x) <= halfW &&
                        Mathf.Abs(local.y) <= halfH;

                    if (insideOld) 
                    {
                        FluidSim.Instance.fluidSolver.solid[i, j] = 0; 
                    }
                    
                    // Repeat for new position
                    local = transform.InverseTransformPoint(worldPos);
                    bool insideNew =
                        Mathf.Abs(local.x) <= halfW &&
                        Mathf.Abs(local.y) <= halfH;

                    if (insideNew)
                    {
                        FluidSim.Instance.fluidSolver.solid[i, j] = 1; 
                    }
                        
                }
            }
            FluidSim.Instance.fluidSolver.pressure = new float[width, height]; // Reset pressure field
        }  
        
        void OnDrawGizmos()
        {
            if (type == ObstacleType.Circle)
            {
                DrawCircleGizmo();
            }
            else if (type == ObstacleType.Rectangle)
            {
                DrawRectangleGizmo();
            }
        }

        void DrawRectangleGizmo()
        {
            Gizmos.color = Color.black;
            Matrix4x4 rotationMatrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.matrix = rotationMatrix;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(size.x, size.y, 0));
            
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(size.x + obstacleOutlineThickness * 2, size.y + obstacleOutlineThickness * 2, 0));
        }
        
        void DrawCircleGizmo()
        {
            Gizmos.color = Color.black;
            Gizmos.DrawWireSphere(transform.position, radius);
            
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(transform.position, radius + obstacleOutlineThickness);
        }
    }
}