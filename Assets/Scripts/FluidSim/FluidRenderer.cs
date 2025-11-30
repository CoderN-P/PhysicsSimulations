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
        public bool usePixelInterpolation;
        public bool useGPU;
        public int texWidth = 1280;
        public int texHeight = 720;
        
        [Header("Colors")]
        public Gradient velocityGradient;
        public Gradient divergenceGradient;
        public Gradient pressureGradient;
        public Gradient vorticityGradient;
        public Color backgroundColor;
        public Color solidCellColor;
        
        [Header("Interpolation")]
        public GameObject interpolationQuad;
        public ComputeShader fluidTextureShader;
        
        [Header("Value Ranges")]
        public float maxVelocityMagnitude;
        public float maxDivergenceMagnitude;
        public float maxVelocityArrowLength;
        public float maxPressureMagnitude;
        public float maxVorticityMagnitude;
        public float maxDensity;
        
        [Header("Brush Settings")]
        public float brushVelocityStrength;
        public float brushDensityStrength;
        public bool brushEnabled;
        
        public Vector2 bottomLeft; // Bottom-left position of the grid in world coordinates
        private ArrowManager[,] vectorArrows;
        private SpriteRenderer[,] cells;

        private int width => FluidSim.Instance.width;
        private int height => FluidSim.Instance.height;
        private float cellSize => FluidSim.Instance.cellSize;
        private float brushRadius = 0f;

        private Texture2D cellTexture;
        private RenderTexture outputTexture;
        private Color[] pixels;
        
        private Texture2D velocityLUT;
        private Texture2D pressureLUT;
        private Texture2D vorticityLUT;
        
        ComputeBuffer uBuf, vBuf, densityBuf, pressureBuf, vorticityBuf, solidBuf, redBuf, greenBuf, blueBuf;
        public int kernel;
        
        
        // When use GPU changes, modify the quad texture while in play mode
        
        #if UNITY_EDITOR
        private void OnValidate()
        {
            if (interpolationQuad != null && Application.isPlaying)
            {
                if (useGPU && outputTexture != null)
                {
                    interpolationQuad.GetComponent<Renderer>().material.mainTexture = outputTexture;
                }
                else if (!useGPU && cellTexture != null)
                {
                    interpolationQuad.GetComponent<Renderer>().material.mainTexture = cellTexture;
                }
            }
        }
        #endif
        
        public void Initialize()
        {

            GetBottomLeft();
            vectorArrows = new ArrowManager[width, height];

            if (!usePixelInterpolation) // Only initialize cells if not using pixel interpolation
            {
                cells = new SpriteRenderer[width, height];
                InitializeCells();
            }
            else
            {
                cellTexture = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false);
                cellTexture.filterMode = FilterMode.Point;
                pixels = new Color[texWidth * texHeight];
                
                float w = width * cellSize;
                float h = height * cellSize;

                interpolationQuad.transform.localScale = new Vector3(w, h, 1);
                Vector2 center = bottomLeft + new Vector2(w/2f, h/2f);
                interpolationQuad.transform.position = center;

                if (useGPU)
                {
                    interpolationQuad.GetComponent<Renderer>().material.mainTexture = outputTexture;
                }
                else
                {
                    interpolationQuad.GetComponent<Renderer>().material.mainTexture = cellTexture;
                }

                // Generate lookup textures for gradients
                velocityLUT = GradientToTexture(velocityGradient);
                pressureLUT = GradientToTexture(pressureGradient);
                vorticityLUT = GradientToTexture(vorticityGradient);
                
                kernel = fluidTextureShader.FindKernel("CSMain");

                int count = width * height;

                uBuf        = new ComputeBuffer((width+1)*height, sizeof(float));
                vBuf        = new ComputeBuffer((height+1)*width, sizeof(float));
                densityBuf  = new ComputeBuffer(count, sizeof(float));
                pressureBuf = new ComputeBuffer(count, sizeof(float));
                vorticityBuf= new ComputeBuffer(count, sizeof(float));
                redBuf      = new ComputeBuffer(count, sizeof(float));
                greenBuf    = new ComputeBuffer(count, sizeof(float));
                blueBuf     = new ComputeBuffer(count, sizeof(float));
                solidBuf    = new ComputeBuffer(count, sizeof(int));
                

                outputTexture = new RenderTexture(texWidth, texHeight, 0,
                    RenderTextureFormat.ARGB32);
                outputTexture.enableRandomWrite = true;
                outputTexture.Create();
            }

            if (enableArrows)
                InitializeArrows();
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
        
        Texture2D GradientToTexture(Gradient g, int resolution = 256)
        {
            Texture2D tex = new Texture2D(resolution, 1, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            for (int i = 0; i < resolution; i++)
            {
                float t = i / (resolution - 1f);
                tex.SetPixel(i, 0, g.Evaluate(t));
            }
            tex.Apply();
            return tex;
        }

        void RenderGPU()
        {
            var sim = FluidSim.Instance.fluidSolver;
            
            float[] uFlat = new float[(width + 1) * height];
            float[] vFlat = new float[(height + 1) * width];
            float[] densityFlat = new float[width * height];
            float[] pressureFlat = new float[width * height];
            float[] vorticityFlat = new float[width * height];
            float[] redFlat = new float[width * height];
            float[] greenFlat = new float[width * height];
            float[] blueFlat = new float[width * height];
            int[] solidFlat = new int[width * height];
            
            for (int y = 0; y < height; y++)
            for (int x = 0; x <= width; x++)
                uFlat[x + y * (width + 1)] = sim.uGrid[x,y];
            
            for (int y = 0; y <= height; y++)
            for (int x = 0; x < width; x++)
                vFlat[x + y * width] = sim.vGrid[x,y];
            
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int id = x + y * width; // row-major, matches HLSL
                    
                    densityFlat[id]  = sim.density[x, y];
                    pressureFlat[id] = sim.pressure[x, y];
                    vorticityFlat[id]= sim.omega[x, y];
                    solidFlat[id]    = sim.solid[x, y];
                    redFlat[id]      = sim.rGrid[x, y];
                    greenFlat[id]    = sim.gGrid[x, y];
                    blueFlat[id]     = sim.bGrid[x, y];
                }
            }
            
            uBuf.SetData(uFlat);
            vBuf.SetData(vFlat);
            densityBuf.SetData(densityFlat);
            pressureBuf.SetData(pressureFlat);
            vorticityBuf.SetData(vorticityFlat);
            solidBuf.SetData(solidFlat);
            redBuf.SetData(redFlat);
            greenBuf.SetData(greenFlat);
            blueBuf.SetData(blueFlat);

            // Bind buffers
            fluidTextureShader.SetBuffer(kernel, "U", uBuf);
            fluidTextureShader.SetBuffer(kernel, "V", vBuf);
            fluidTextureShader.SetBuffer(kernel, "Density", densityBuf);
            fluidTextureShader.SetBuffer(kernel, "Pressure", pressureBuf);
            fluidTextureShader.SetBuffer(kernel, "Vorticity", vorticityBuf);
            fluidTextureShader.SetBuffer(kernel, "R", redBuf);
            fluidTextureShader.SetBuffer(kernel, "G", greenBuf);
            fluidTextureShader.SetBuffer(kernel, "B", blueBuf);
            fluidTextureShader.SetBuffer(kernel, "Solid", solidBuf);

            // Bind LUTs
            fluidTextureShader.SetTexture(kernel, "VelocityLUT", velocityLUT);
            fluidTextureShader.SetTexture(kernel, "PressureLUT", pressureLUT);
            fluidTextureShader.SetTexture(kernel, "VorticityLUT", vorticityLUT);

            // Bind output target
            fluidTextureShader.SetTexture(kernel, "Result", outputTexture);

            // Set parameters
            fluidTextureShader.SetInt("Width", width);
            fluidTextureShader.SetInt("Height", height);
            fluidTextureShader.SetInt("textureWidth", texWidth);
            fluidTextureShader.SetInt("textureHeight", texHeight);
            fluidTextureShader.SetFloat("CellSize", cellSize);

            fluidTextureShader.SetFloat("MaxVelocityMag", maxVelocityMagnitude);
            fluidTextureShader.SetFloat("MaxDensity", maxDensity);
            fluidTextureShader.SetFloat("MaxPressureMag", maxPressureMagnitude);
            fluidTextureShader.SetFloat("MaxVorticityMag", maxVorticityMagnitude);

            fluidTextureShader.SetVector("solidCellColor", solidCellColor);
            fluidTextureShader.SetVector("backgroundColor", backgroundColor);

            fluidTextureShader.SetInt("visualizationMode", (int)visualizationMode);

            // Dispatch
            int groupsX = Mathf.CeilToInt(texWidth / 8f);
            int groupsY = Mathf.CeilToInt(texHeight / 8f);

            fluidTextureShader.Dispatch(kernel, groupsX, groupsY, 1);
        }

        void RenderPixel(int px, int py, int index)
        {
            float gridWorldHeight = height * cellSize;
            float gridWorldWidth = width * cellSize;
            float worldX = (px + 0.5f) * (gridWorldWidth / texWidth);
            float worldY = (py + 0.5f) * (gridWorldHeight / texHeight);
            Vector2 worldPos = new Vector2(worldX, worldY);
            
            // Check if pixel is inside a solid cell
            int cellI = Mathf.FloorToInt((worldPos.x) / cellSize);
            int cellJ = Mathf.FloorToInt((worldPos.y) / cellSize);
            
            if (cellI >= 0 && cellI < width && cellJ >= 0 && cellJ < height && visualizationMode != VisualizationMode.Debug)
            {
                if (FluidSim.Instance.fluidSolver.solid[cellI, cellJ] == 1)
                {
                    pixels[index] = solidCellColor;
                    return;
                }
            }
            
            if (visualizationMode == VisualizationMode.SmokeWithVelocity)
            {
                Vector2 velocity = FluidSim.Instance.fluidSolver.VelocityAtWorldPos(worldPos);
                float density = FluidSim.Instance.fluidSolver.SampleBillinear(FluidSim.Instance.fluidSolver.density, worldPos);
                float velMagnitude = velocity.magnitude;
                float t = Mathf.Clamp01(Mathf.Abs(velMagnitude) / maxVelocityMagnitude);
                Color velColor = velocityGradient.Evaluate(t);
            
                float densityClamped = Mathf.Clamp01(density/maxDensity);
                pixels[index] = Color.Lerp(backgroundColor, velColor, densityClamped);
            } else if (visualizationMode == VisualizationMode.Velocity)
            {
                Vector2 velocity = FluidSim.Instance.fluidSolver.VelocityAtWorldPos(worldPos);
                float velMagnitude = velocity.magnitude;
                float t = Mathf.Clamp01(Mathf.Abs(velMagnitude) / maxVelocityMagnitude);
                pixels[index] = velocityGradient.Evaluate(t);
            } else if (visualizationMode == VisualizationMode.Smoke)
            {
                float density = FluidSim.Instance.fluidSolver.SampleBillinear(FluidSim.Instance.fluidSolver.density, worldPos);
                pixels[index] = Color.Lerp(backgroundColor, Color.white, Mathf.Clamp01(density / maxDensity));
            } else if (visualizationMode == VisualizationMode.Pressure)
            {
                float pressure = FluidSim.Instance.fluidSolver.SampleBillinear(FluidSim.Instance.fluidSolver.pressure, worldPos);
                pixels[index] = pressureGradient.Evaluate(Mathf.InverseLerp(-maxPressureMagnitude, maxPressureMagnitude, pressure));
            } else if (visualizationMode == VisualizationMode.Vorticity)
            {
                float vorticity = FluidSim.Instance.fluidSolver.SampleBillinear(FluidSim.Instance.fluidSolver.omega, worldPos);
                pixels[index] = vorticityGradient.Evaluate(Mathf.InverseLerp(-maxVorticityMagnitude, maxVorticityMagnitude, vorticity));
            } else if (visualizationMode == VisualizationMode.SmokeWithPressure)
            {
                float pressure = FluidSim.Instance.fluidSolver.SampleBillinear(FluidSim.Instance.fluidSolver.pressure, worldPos);
                float density = FluidSim.Instance.fluidSolver.SampleBillinear(FluidSim.Instance.fluidSolver.density, worldPos);
                Color pressureColor = pressureGradient.Evaluate(Mathf.InverseLerp(-maxPressureMagnitude, maxPressureMagnitude, pressure));
            
                float densityClamped = Mathf.Clamp01(density/maxDensity);
                pixels[index] = Color.Lerp(backgroundColor, pressureColor, densityClamped);
                
            } else if (visualizationMode == VisualizationMode.Color)
            {
                float density = FluidSim.Instance.fluidSolver.SampleBillinear(FluidSim.Instance.fluidSolver.density, worldPos);
                float r = FluidSim.Instance.fluidSolver.SampleBillinear(FluidSim.Instance.fluidSolver.rGrid, worldPos);
                float g = FluidSim.Instance.fluidSolver.SampleBillinear(FluidSim.Instance.fluidSolver.gGrid, worldPos);
                float b = FluidSim.Instance.fluidSolver.SampleBillinear(FluidSim.Instance.fluidSolver.bGrid, worldPos);

                Color c = new Color(r, g, b);
                pixels[index] = Color.Lerp(backgroundColor, c, Mathf.Clamp01(density / maxDensity));
            }
            else if (visualizationMode == VisualizationMode.Debug)
            {
                float r = (float)cellI / (float)width;
                float g = (float)cellJ / (float)height;

                if (FluidSim.Instance.fluidSolver.solid[cellI, cellJ] == 1)
                {
                    pixels[index] = new Color(1, 1, 1);
                }
                else
                {
                    pixels[index] = new Color(r, g, 0f);
                }
            }
            
        }

        void RenderTexture()
        {
            if (useGPU)
            {
                RenderGPU();
                return;
            }
            
            if (visualizationMode == VisualizationMode.Divergence)
                return;

            for (int px = 0; px < texWidth; px++)
            {
                for (int py = 0; py < texHeight; py++)
                {
                    int index = py * texWidth + px;
                    RenderPixel(px, py, index);
                }
            }
            cellTexture.SetPixels(pixels);
            cellTexture.Apply();
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
                cells[i, j].color = Color.Lerp(backgroundColor, Color.white, Mathf.Clamp01(value/maxDensity));
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
            
            float densityClamped = Mathf.Clamp01(density/maxDensity);
            
            cells[i, j].color = Color.Lerp(backgroundColor, velColor, densityClamped);
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

        public void Render(float[,] uGrid, float[,] vGrid, int[,] solid)
        {
            if (usePixelInterpolation)
            {
                RenderTexture();
                return;
            }
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