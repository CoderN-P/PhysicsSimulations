using UnityEngine;

namespace FluidSim
{
    public class FluidRenderer : MonoBehaviour
    {
        [Header("Prefabs")]
        public GameObject arrowPrefab;
        public GameObject cellPrefab;
        
        [Header("Visualization Settings")]
        public VisualizationMode visualizationMode;
        public float cellSpacing; // Percent of cell to be shrunk for spacing
        public bool showArrows;
        public bool enableArrows;
        
        [Header("Colors")]
        public Gradient velocityGradient;
        public Gradient divergenceGradient;
        public Gradient pressureGradient;
        public Gradient smokeDensityGradient;
        public Gradient vorticityGradient;
        public Color solidCellColor;
        
        [Header("Value Ranges")]
        public float maxVelocityMagnitude;
        public float maxDivergenceMagnitude;
        public float maxVelocityArrowLength;
        public float maxPressureMagnitude;
        public float maxVorticityMagnitude;
        
        [Header("Brush Settings")]
        public float brushVelocityStrength;
        public float brushDensityStrength;
        public bool brushEnabled;
        
        [Header("Vortex Shedding")]
        public Vector2 obstaclePosition;
        public float obstacleRadius;
        
        private Vector2 bottomLeft; // Bottom-left position of the grid in world coordinates
        private ArrowManager[,] vectorArrows;
        private SpriteRenderer[,] cells;

        private int width => FluidSim.Instance.width;
        private int height => FluidSim.Instance.height;
        private float cellSize => FluidSim.Instance.cellSize;
        private float brushRadius = 0f;

        private bool draggingObstacle;
        

        public void Initialize()
        {
            
            GetBottomLeft();
            cells = new SpriteRenderer[width, height];
            vectorArrows = new ArrowManager[width, height];
            InitializeCells();
            if (enableArrows)
                InitializeArrows();
            if (FluidSim.Instance.vortexShedding)
                InitializeSolidObstacleCells();
        }

        public void GetKeyboardInput()
        {
            // If p is pressed, pause/unpause simulation
            if (Input.GetKeyDown(KeyCode.P))
            {
                FluidSim.Instance.paused = !FluidSim.Instance.paused;
            }
            
            // Horizontal Arrows to go through visualization modes
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                visualizationMode = (VisualizationMode)(((int)visualizationMode + 1) % System.Enum.GetNames(typeof(VisualizationMode)).Length);
            }
            else if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                visualizationMode = (VisualizationMode)(((int)visualizationMode - 1 + System.Enum.GetNames(typeof(VisualizationMode)).Length) % System.Enum.GetNames(typeof(VisualizationMode)).Length);
            }
            
            // If s is pressed, step simulation one step
            
            if (Input.GetKeyDown(KeyCode.S)) 
            {
                FluidSim.Instance.stepping = true;
            } else if (Input.GetKeyUp(KeyCode.S))
            {
                FluidSim.Instance.stepping = false;
            }
            
            if (FluidSim.Instance.stepping)
            {
                FluidSim.Instance.paused = true;
                FluidSim.Instance.fluidSolver.Step(FluidSim.Instance.project, FluidSim.Instance.advect, FluidSim.Instance.leftWallForce);
            }
        }

        public void UpdateObstaclePosition()
        {
            if (!FluidSim.Instance.vortexShedding) // Only enabled for vortex shedding
                return;
            
            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            
            // Check if mouse is within obstacle radius and left mouse button is held
            
            if (Vector2.Distance(mousePos, obstaclePosition) <= obstacleRadius && Input.GetMouseButton(0))
            {
                brushEnabled = false; // Disable brush while dragging obstacle
                draggingObstacle = true;
            }
            
            if (draggingObstacle && Input.GetMouseButton(0))
            {
                Vector2 oldPos = obstaclePosition;
                obstaclePosition = mousePos;
                ModifySolidMapForObstacle(oldPos, obstaclePosition);
            }
            else if (draggingObstacle && !Input.GetMouseButton(0))
            {
                draggingObstacle = false;
                brushEnabled = true; // Re-enable brush after dragging
            }
        }
        
        public void ModifySolidMapForObstacle(Vector2 oldPos, Vector2 newPos)
        {
            if (!FluidSim.Instance.vortexShedding)
                return;

            var solver = FluidSim.Instance.fluidSolver;

            int radiusCells = Mathf.CeilToInt(obstacleRadius / cellSize);

            // --- NEW CENTER ---
            int newCI = Mathf.FloorToInt((newPos.x - bottomLeft.x) / cellSize);
            int newCJ = Mathf.FloorToInt((newPos.y - bottomLeft.y) / cellSize);

            Vector2 obstacleVel = (newPos - oldPos) / Time.deltaTime;

            for (int i = 1; i < width - 1; i++)
            {
                for (int j = 1; j < height - 1; j++)
                {
                    solver.solid[i, j] = 0;
                    
                    // Check if within obstacle radius
                    float dx = i + 0.5f - newCI;
                    float dy = j + 0.5f - newCJ;
                    if (dx*dx + dy*dy <= radiusCells * radiusCells)
                    {
                        solver.solid[i, j] = 1;

                        // Set velocity to obstacle velocity
                        solver.uGrid[i, j] = obstacleVel.x;
                        solver.vGrid[i, j] = obstacleVel.y;
                        solver.uGrid[i+1, j] = obstacleVel.x;
                        solver.vGrid[i, j+1] = obstacleVel.y;
                        
                        solver.density[i, j] = 1f;
                    }
                }
            }
        }
        
        void GetBottomLeft()
        {
            Camera cam = Camera.main;
            // Camera centered at (0, 0) by default
            float sceneHeight = cam.orthographicSize;  // Half of true height
            float sceneWidth = sceneHeight * cam.aspect; // Half of true width
            
            float gridWidth = width * cellSize;
            float gridHeight = height * cellSize;
            
            bottomLeft = new Vector2(
                -sceneWidth + (sceneWidth * 2 - gridWidth) / 2,
                -sceneHeight + (sceneHeight * 2 - gridHeight) / 2
            );
        }
        
        Vector2 IndexToWorldPos(float i, float j)
        {
            // Converts grid indices (i, j) to world position (x, y)
            // Indices (i, j) is at center of cell;
            
            Vector2 pos = new Vector2();
            
            pos.x = bottomLeft.x + (i + 0.5f) * cellSize ;
            pos.y = bottomLeft.y + (j + 0.5f) * cellSize;
            
            return pos;
        }
        
        void InitializeCells()
        {
            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    Vector2 cellPos = IndexToWorldPos(i, j);
                    cells[i, j] = Instantiate(cellPrefab, cellPos, Quaternion.identity).GetComponent<SpriteRenderer>();
                    cells[i, j].transform.localScale = new Vector3(
                        cellSize * (1 - cellSpacing),
                        cellSize * (1 - cellSpacing),
                        1
                    );
                }
            }
        }

        void InitializeSolidObstacleCells()
        {
            // Initialize solid cells for vortex shedding obstacle
            ModifySolidMapForObstacle(obstaclePosition, obstaclePosition);
        }

        void InitializeArrows()
        {
            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    Vector2 arrowPos = IndexToWorldPos(i, j);
                    vectorArrows[i, j] = Instantiate(arrowPrefab, arrowPos, Quaternion.identity).GetComponent<ArrowManager>();
                }
            }
        }

        void RenderCell(float value, int i, int j, VisualizationMode mode)
        {
            // Implementation for rendering a single cell at grid position (i, j) with given density
            float t;
            float clampedValue;
            if (mode == VisualizationMode.Pressure)
            {
                clampedValue = value;
                if (Mathf.Abs(value) > maxPressureMagnitude)
                {
                    clampedValue = Mathf.Sign(value) * maxPressureMagnitude;
                }
                
                t = Mathf.InverseLerp(-1f, 1f, clampedValue / maxPressureMagnitude);
                cells[i, j].color = pressureGradient.Evaluate(t);
            } else if (mode == VisualizationMode.Divergence)
            {
                clampedValue = value;
                if (Mathf.Abs(value) > maxDivergenceMagnitude)
                {
                    clampedValue = Mathf.Sign(value) * maxDivergenceMagnitude;
                }

                t = Mathf.InverseLerp(-1f, 1f, clampedValue / maxDivergenceMagnitude);
                cells[i, j].color = divergenceGradient.Evaluate(t);
            } else if (mode == VisualizationMode.Smoke)
            {
                clampedValue = Mathf.Clamp01(value);
                t = clampedValue;
                cells[i, j].color = smokeDensityGradient.Evaluate(t);
            } else if (mode == VisualizationMode.Vorticity)
            {
                clampedValue = value;
                if (Mathf.Abs(value) > maxVorticityMagnitude)
                {
                    clampedValue = Mathf.Sign(value) * maxVorticityMagnitude;
                }

                t = Mathf.InverseLerp(-1f, 1f, clampedValue / maxVorticityMagnitude);
                cells[i, j].color = vorticityGradient.Evaluate(t);
            }
        }

        public void ApplyDensityBrush(Vector2 mousePos, float radius)
        {
            if (!brushEnabled)
                return;
            
            // Convert mouse positions to grid indices
            
            int brushRadiusInCells = Mathf.CeilToInt(radius / cellSize);
            
            Vector2 brushCenterWorld = mousePos;
            int centerI = Mathf.FloorToInt((brushCenterWorld.x - bottomLeft.x) / cellSize);
            int centerJ = Mathf.FloorToInt((brushCenterWorld.y - bottomLeft.y) / cellSize);
            
            for (int i = centerI - brushRadiusInCells; i <= centerI + brushRadiusInCells; i++)
            {
                for (int j = centerJ - brushRadiusInCells; j <= centerJ + brushRadiusInCells; j++)
                {
                    if (i >= 0 && i < width && j >= 0 && j < height)
                    {
                        float distance = Vector2.Distance(
                            new Vector2(i + 0.5f, j + 0.5f),
                            new Vector2(centerI + 0.5f, centerJ + 0.5f)
                        );
                        
                        if (distance <= brushRadiusInCells)
                        {
                            // float strength = (radius - distance) / radius; // Linear falloff

                            FluidSim.Instance.fluidSolver.density[i, j] += brushDensityStrength;
                        }
                    }
                }
            }
        }
        
        void RenderCell(Vector2 velocity, float density, int i, int j)
        {
            // Implementation for rendering a single cell at grid position (i, j) with velocity and density
            float velMagnitude = velocity.magnitude;
            float t = Mathf.Clamp01(Mathf.Abs(velMagnitude) / maxVelocityMagnitude);
            Color velColor = velocityGradient.Evaluate(t);
            
            float densityClamped = Mathf.Clamp01(density);
            velColor.a = densityClamped;
            cells[i, j].color = velColor;
        }
        
        void RenderCell(Vector2 velocity, int i, int j)
        {
            // Implementation for rendering a single cell at grid position (i, j) with given density
            float velMagnitude = velocity.magnitude;
            float t = Mathf.Clamp01(Mathf.Abs(velMagnitude) / maxVelocityMagnitude);
            cells[i, j].color = velocityGradient.Evaluate(t);
        }

        void RenderArrow(float u, float v, int i, int j, int solidCell)
        {
            if (!showArrows) // If arrows are disabled, disable
                vectorArrows[i, j].Disable();
            else if (solidCell == 1 && !vectorArrows[i, j].disabled) // If solid and active, disable
                vectorArrows[i, j].Disable();
            else if (vectorArrows[i, j].disabled) // If not solid and not active, enable
                vectorArrows[i, j].Enable();
            
            // Implementation for rendering a single arrow at grid position (i, j) with velocity components (u, v)
            Vector2 velocityDir = new Vector2(u, v).normalized;
            float velMagnitude = new Vector2(u, v).magnitude;
            
            if (velMagnitude > maxVelocityArrowLength)
            {
                velMagnitude = maxVelocityArrowLength;
            }
            
            Vector2 velocity = velocityDir * velMagnitude;
            
            Vector2 startPos = IndexToWorldPos(i, j);
            Vector2 endPos = startPos + velocity;
            
            vectorArrows[i, j].Draw(startPos, endPos, 1);
        }

        public void RegisterEmitter(Vector2 worldPos, float radius, float rate, float falloff)
        {
            Vector2 simulationPos = worldPos - bottomLeft;
            FluidSim.Instance.fluidSolver.RegisterEmitter(simulationPos, radius, rate, falloff);
        }

        public void Render(float[,] uGrid, float[,] vGrid, int[,] solid)
        {
            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    if (solid[i, j] == 1)
                    {
                        // Solid cell
                        cells[i, j].color = solidCellColor;
                    }
                    else
                    {
                        Vector2 velocity = new Vector2(uGrid[i, j], vGrid[i, j]);

                        if (visualizationMode == VisualizationMode.Velocity)
                        {
                            RenderCell(velocity, i, j);
                        }
                        else if (visualizationMode == VisualizationMode.Divergence)
                        {
                            float divergence = FluidSim.Instance.fluidSolver.ComputeDivergenceAtCell(i, j);
                            RenderCell(divergence, i, j, VisualizationMode.Divergence);
                        } else if (visualizationMode == VisualizationMode.Pressure)
                        {
                            float pressure = FluidSim.Instance.fluidSolver.pressure[i, j];
                            RenderCell(pressure, i, j, VisualizationMode.Pressure);
                        } else if (visualizationMode == VisualizationMode.Smoke)
                        {
                            float density = FluidSim.Instance.fluidSolver.density[i, j];
                            RenderCell(density, i, j, VisualizationMode.Smoke);
                        } else if (visualizationMode == VisualizationMode.SmokeWithVelocity)
                        {
                            float density = FluidSim.Instance.fluidSolver.density[i, j];
                            RenderCell(velocity, density, i, j);
                        } else if (visualizationMode == VisualizationMode.Vorticity)
                        {
                            float vorticity = FluidSim.Instance.fluidSolver.omega[i, j];
                            RenderCell(vorticity, i, j, VisualizationMode.Vorticity);
                        }

                        if (enableArrows)
                        {
                            RenderArrow(uGrid[i, j], vGrid[i, j], i, j, solid[i, j]);
                        }
                    }
                }
            }
        }
        
        public void ApplyVelocityBrush(Vector2 lastMousePos, Vector2 mousePos, float radius)
        {
            if (!brushEnabled)
                return;
            // Convert mouse positions to grid indices
            Vector2 worldDelta = mousePos - lastMousePos;
            Vector2 gridDelta = worldDelta / cellSize;
            
            int brushRadiusInCells = Mathf.CeilToInt(radius / cellSize);
            
            Vector2 brushCenterWorld = mousePos;
            int centerI = Mathf.FloorToInt((brushCenterWorld.x - bottomLeft.x) / cellSize);
            int centerJ = Mathf.FloorToInt((brushCenterWorld.y - bottomLeft.y) / cellSize);
            
            for (int i = centerI - brushRadiusInCells; i <= centerI + brushRadiusInCells; i++)
            {
                for (int j = centerJ - brushRadiusInCells; j <= centerJ + brushRadiusInCells; j++)
                {
                    if (i >= 0 && i < width && j >= 0 && j < height)
                    {
                        float distance = Vector2.Distance(
                            new Vector2(i + 0.5f, j + 0.5f),
                            new Vector2(centerI + 0.5f, centerJ + 0.5f)
                        );
                        
                        if (distance <= brushRadiusInCells)
                        {
                            float strength = (radius - distance) / radius; // Linear falloff

                            float forceX = Mathf.Clamp(-gridDelta.x * strength * brushVelocityStrength, -maxVelocityMagnitude, maxVelocityMagnitude);
                            float forceY = Mathf.Clamp(-gridDelta.y * strength * brushVelocityStrength, -maxVelocityMagnitude,
                                maxVelocityMagnitude);
                            FluidSim.Instance.fluidSolver.RegisterBrushForce(i, j, true, forceX);
                            FluidSim.Instance.fluidSolver.RegisterBrushForce(i, j, false, forceY);
                        }
                    }
                }
            }
        }
    }
}