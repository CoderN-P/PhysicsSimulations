using System.Collections.Generic;
using UnityEngine;

namespace FluidSim
{
    public class FluidSolver
    {
        private int width;
        private int height;
        private float cellSize;
        
        // Staggered uv grid for velocity
        public float[,] uGrid;
        public float[,] vGrid;
        public int[,] solid;
        public float[,] pressure;
        public float[,] density;
        public float[,] rGrid;
        public float[,] gGrid;
        public float[,] bGrid;

        public float[,] omega; // Vorticity grid
        
        // Temp grids for advection
        public float[,] tempUGrid;
        public float[,] tempVGrid;
        public float[,] tempDensity;
        public float[,] tempRGrid;
        public float[,] tempGGrid;
        public float[,] tempBGrid;
        
        public struct BrushForce
        {
            public int i;
            public int j;
            public Vector2 force;
        }
        private List<BrushForce> pendingBrushForces = new List<BrushForce>();
        
        private bool vortexShedding;
        
        public FluidSolver(int width, int height, float cellSize, bool vortexShedding = false, bool initializeRandom = false)
        {
            this.width = width;
            this.height = height;
            this.cellSize = cellSize;
            this.vortexShedding = vortexShedding;

            uGrid = new float[width + 1, height];
            vGrid = new float[width, height + 1];
            tempUGrid = new float[width + 1, height];
            tempVGrid = new float[width, height + 1];
            tempDensity = new float[width, height];
            solid = new int[width, height];
            pressure = new float[width, height];
            density = new float[width, height];
            omega = new float[width, height];
            rGrid = new float[width, height];
            gGrid = new float[width, height];
            bGrid = new float[width, height];
            tempRGrid = new float[width, height];
            tempGGrid = new float[width, height];
            tempBGrid = new float[width, height];
            
            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    
                    if ((i == 0 && !vortexShedding) || i == width - 1 || j == 0 || j == height - 1)
                        solid[i, j] = 1; // Boundary cells are solid
                    else
                        solid[i, j] = 0; // 0 = fluid, 1 = solid
                    
                }
            }
            
            if (initializeRandom)
                SetRandomVelocities();
        }
        
        void AddConstantForceToLeftWall(float force)
        {
            for (int j = 0; j < height; j++)
            {
                uGrid[0, j] = force;
            }

            for (int j = 0; j < height + 1; j++)
            {
                vGrid[0, j] = 0;
            }
        }
        
        public void ApplyEmitter(Emitter emitter)
        {
            float dt = FluidSim.Instance.timeStep;
            int centerX = Mathf.RoundToInt(emitter.position.x / cellSize);
            int centerY = Mathf.RoundToInt(emitter.position.y / cellSize);
            int radiusInCells = Mathf.CeilToInt(emitter.radius / cellSize);
            
            for (int i = centerX - radiusInCells; i <= centerX + radiusInCells; i++)
            {
                for (int j = centerY - radiusInCells; j <= centerY + radiusInCells; j++)
                {
                    if (i < 0 || i >= width || j < 0 || j >= height) continue;
                    
                    float dist = Vector2.Distance(new Vector2(i * cellSize, j * cellSize), emitter.position);
                    if (dist <= emitter.radius)
                    {
                        
                        float falloffFactor = emitter.falloff > 0 ? Mathf.Pow(1 - dist / emitter.radius, emitter.falloff) : 1f;
                        density[i, j] += emitter.rate * falloffFactor * dt;
                        rGrid[i, j] = emitter.c.r;
                        gGrid[i, j] = emitter.c.g;
                        bGrid[i, j] = emitter.c.b;
                        
                        Vector2 jetVel = emitter.direction * emitter.speed * falloffFactor * dt;
                        
                        uGrid[i, j]     += jetVel.x;
                        uGrid[i+1, j]   += jetVel.x;
                        vGrid[i, j]     += jetVel.y;
                        vGrid[i, j+1]   += jetVel.y;
                    }
                }
            }
        }

        void UpdateVorticityMap()
        {
            float invDx = 1f / cellSize;
            for (int i = 1; i < width - 1; i++)
            {
                for (int j = 1; j < height - 1; j++)
                {
                    if (solid[i, j] == 1)
                    {
                        omega[i, j] = 0;
                        continue;
                    }
                    // ∂u/∂y  ~ (u(i, j+1) - u(i, j)) / dx
                    float dudY = (uGrid[i, j+1] - uGrid[i, j]) * invDx;

                    // ∂v/∂x  ~ (v(i+1, j) - v(i, j)) / dx
                    float dvdX = (vGrid[i+1, j] - vGrid[i, j]) * invDx;
                    
                    omega[i, j] = dvdX - dudY;
                }
            }
        }

        void SetRandomVelocities()
        {
            for (int i = 0; i < width + 1; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    uGrid[i, j] = Random.Range(-15f, 15f);
                }
            }
            
            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height + 1; j++)
                {
                    vGrid[i, j] = Random.Range(-15f, 15f);
                }
            }
        }

        public float ComputeDivergenceAtCell(int i, int j)
        {
            if (i < 0 || i >= width || j < 0 || j >= height)
                return 0f;
            
            return (uGrid[i+1, j] - uGrid[i, j] + vGrid[i, j+1] - vGrid[i, j])/cellSize;
        }

        void PressureSolveCell(int i, int j)
        {
            float divergence = ComputeDivergenceAtCell(i, j);

            float pressureSum = 0;
            int neighborCount = 0;
            
            if (i > 0 && solid[i-1, j] == 0)
            {
                pressureSum += pressure[i - 1, j];
                neighborCount++;
            }
            if (i < width - 1 && solid[i+1, j] == 0)
            {
                pressureSum += pressure[i + 1, j];
                neighborCount++;
            }
            if (j > 0 && solid[i, j-1] == 0)
            {
                pressureSum += pressure[i, j - 1];
                neighborCount++;
            }
            if (j < height - 1 && solid[i, j+1] == 0)
            {
                pressureSum += pressure[i, j + 1];
                neighborCount++;
            }
            
            float pNew = (pressureSum - divergence * cellSize * cellSize) / neighborCount;
            pressure[i,j] = (1 - FluidSim.Instance.overrelaxationFactor) * pressure[i,j] + FluidSim.Instance.overrelaxationFactor * pNew;
        }

        public Vector2 VelocityAtWorldPos(Vector2 pos)
        {
            float u = UAtWorldPos(pos);
            float v = VAtWorldPos(pos);
            return new Vector2(u, v);   
        }
        
        float UAtWorldPos(Vector2 pos)
        {
            float gridX = pos.x / cellSize;
            float gridY = pos.y / cellSize - 0.5f;
            
            int i0 = Mathf.Clamp(Mathf.FloorToInt(gridX), 0, width);
            int i1 = Mathf.Clamp(i0 + 1, 0, width);
            int j0 = Mathf.Clamp(Mathf.FloorToInt(gridY), 0, height - 1);
            int j1 = Mathf.Clamp(j0 + 1, 0, height - 1);
            
            float sy = gridY - j0;
            float sx = gridX - i0;
            
            return Mathf.Lerp(
                Mathf.Lerp(uGrid[i0, j0], uGrid[i1, j0], sx),
                Mathf.Lerp(uGrid[i0, j1], uGrid[i1, j1], sx),
                sy
            );
        }
        
        float VAtWorldPos(Vector2 pos)
        {
            float gridX = pos.x / cellSize - 0.5f;
            float gridY = pos.y / cellSize;
            
            int i0 = Mathf.Clamp(Mathf.FloorToInt(gridX), 0, width - 1);
            int i1 = Mathf.Clamp(i0 + 1, 0, width - 1);
            int j0 = Mathf.Clamp(Mathf.FloorToInt(gridY), 0, height);
            int j1 = Mathf.Clamp(j0 + 1, 0, height);
            
            float sy = gridY - j0;
            float sx = gridX - i0;
            
            return Mathf.Lerp(
                Mathf.Lerp(vGrid[i0, j0], vGrid[i1, j0], sx),
                Mathf.Lerp(vGrid[i0, j1], vGrid[i1, j1], sx),
                sy
            );
        }

        public float SampleBillinear(float[,] values, Vector2 pos)
        {
            float gridX = pos.x / cellSize - 0.5f;
            float gridY = pos.y / cellSize - 0.5f;
            
            int i0 = Mathf.Clamp(Mathf.FloorToInt(gridX), 0, width - 1);
            int i1 = Mathf.Clamp(i0 + 1, 0, width - 1);
            int j0 = Mathf.Clamp(Mathf.FloorToInt(gridY), 0, height - 1);
            int j1 = Mathf.Clamp(j0 + 1, 0, height - 1);
            
            float sy = gridY - j0;
            float sx = gridX - i0;
            
            return Mathf.Lerp(
                Mathf.Lerp(values[i0, j0], values[i1, j0], sx),
                Mathf.Lerp(values[i0, j1], values[i1, j1], sx),
                sy
            );
        }

        void AdvectSmoke()
        {
            for (int i = 1; i < width-1; i++)
            {
                for (int j = 1; j < height-1; j++)
                {
                    Vector2 pos = new Vector2((i + 0.5f) * cellSize, (j + 0.5f) * cellSize);
                    Vector2 vel = VelocityAtWorldPos(pos);
                    Vector2 backPos = pos - vel * FluidSim.Instance.timeStep;
                    tempDensity[i, j] = SampleBillinear(density, backPos);
                }
            }
            
            // Swap temp and main grids
            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    density[i, j] = tempDensity[i, j];
                }
            }
        }

        void AdvectDye(int channel)
        {
            float[,] targetGrid;
            float[,] tempGrid;
            switch (channel)
            {
                case 0:
                    targetGrid = rGrid;
                    tempGrid = tempRGrid;
                    break;
                case 1:
                    targetGrid = gGrid;
                    tempGrid = tempGGrid;
                    break;
                case 2:
                    targetGrid = bGrid;
                    tempGrid = tempBGrid;
                    break;
                default:
                    return;
            }

            for (int i = 1; i < width - 1; i++)
            {
                for (int j = 1; j < height - 1; j++)
                {
                    Vector2 pos = new Vector2((i + 0.5f) * cellSize, (j + 0.5f) * cellSize);
                    Vector2 vel = VelocityAtWorldPos(pos);
                    Vector2 backPos = pos - vel * FluidSim.Instance.timeStep;
                    tempGrid[i, j] = SampleBillinear(targetGrid, backPos);
                }
            }

            // Swap temp and main grids
            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    targetGrid[i, j] = tempGrid[i, j];
                }
            }
        }

        void AdvectVelocities()
        {
            for (int i = 0; i < width + 1; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    Vector2 pos = new Vector2(i * cellSize, (j + 0.5f) * cellSize);
                    Vector2 vel = VelocityAtWorldPos(pos);
                    Vector2 backPos = pos - vel * FluidSim.Instance.timeStep;
                    tempUGrid[i, j] = UAtWorldPos(backPos);
                }
            }
            
            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height + 1; j++)
                {
                    Vector2 pos = new Vector2((i + 0.5f) * cellSize, j * cellSize);
                    Vector2 vel = VelocityAtWorldPos(pos);
                    Vector2 backPos = pos - vel * FluidSim.Instance.timeStep;
                    tempVGrid[i, j] = VAtWorldPos(backPos);
                }
            }
            
            // Swap temp and main grids
            for (int i = 0; i < width + 1; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    uGrid[i, j] = tempUGrid[i, j];
                }
            }
            
            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height + 1; j++)
                {
                    vGrid[i, j] = tempVGrid[i, j];
                }
            }
        }

        void UpdatePressureField()
        {
            if (FluidSim.Instance.resetPressureField) pressure = new float[width, height]; // Reset pressure field
            for (int n = 0; n < FluidSim.Instance.pressureSolveIterations; n++)
            {
                for (int i = 1; i < width - 1; i++)
                {
                    for (int j = 1; j < height - 1; j++)
                    {
                        if (solid[i, j] == 0)
                        {
                            PressureSolveCell(i, j);
                            // Debug.Log("pressure: " + pressure[i, j]);
                        }
                    }
                }
                
                for (int j = 0; j < height; j++)
                {
                    pressure[0, j] = pressure[1, j];
                    if (vortexShedding)
                        pressure[width - 1, j] = 0;
                    else
                        pressure[width - 1, j] = pressure[width - 2, j];
                }
            
                for (int i = 0; i < width; i++)
                {
                    pressure[i, 0] = pressure[i, 1];
                    pressure[i, height - 1] = pressure[i, height - 2];
                }
            }
            
        }

        void ProjectVelocities()
        {
            for (int i = 1; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    if (solid[i - 1, j] == 1 || solid[i, j] == 1)
                    {
                        uGrid[i, j] = 0;
                        continue;
                    }
                    uGrid[i, j] -= (pressure[i, j] - pressure[i - 1, j]) / cellSize;
                }
            }
            
            for (int i = 0; i < width; i++)
            {
                for (int j = 1; j < height; j++)
                {
                    if (solid[i, j - 1] == 1 || solid[i, j] == 1)
                    {
                        vGrid[i, j] = 0;
                        continue;
                    }
                    
                    vGrid[i, j] -= (pressure[i, j] - pressure[i, j - 1]) / cellSize;
                }
            }
            
            for (int j = 0; j < height; j++)
                if (vortexShedding)
                {
                    uGrid[width, j] = 0;
                }
                else
                {
                    uGrid[0, j] = uGrid[width, j] = 0;
                }

            for (int i = 0; i < width; i++)
                vGrid[i,0] = vGrid[i,height] = 0;
        }

        public void RegisterBrushForce(int i, int j, bool u, float change)
        {
            BrushForce force = new BrushForce
            {
                i = i,
                j = j,
                force = u ? new Vector2(change, 0) : new Vector2(0, change)
            };
            
            pendingBrushForces.Add(force);
        }
        
        void ApplyExternalForces()
        {
            foreach (var brushForce in pendingBrushForces)
            {
                uGrid[brushForce.i, brushForce.j] += brushForce.force.x;
                vGrid[brushForce.i, brushForce.j] += brushForce.force.y;
            }

            pendingBrushForces.Clear();   
        }

        void InjectDensityForVortexShedding()
        {
            for (int j = 0; j < height; j++)
            {
                float center = height * 0.5f;
                float dy = j - center;
                // float falloff = Mathf.Exp(-dy * dy / (2 * FluidSim.Instance.sigma * FluidSim.Instance.sigma));
                float falloff = Mathf.Abs(dy) <= FluidSim.Instance.sigma/cellSize ? 1 : 0;
                density[0, j] = falloff*FluidSim.Instance.injectionDensity;
            }
        }

        void InjectColorForVortexShedding()
        {
            for (int j = 0; j < height; j++)
            {
                float center = height * 0.5f;
                float dy = j - center;
                // float falloff = Mathf.Exp(-dy * dy / (2 * FluidSim.Instance.sigma * FluidSim.Instance.sigma));
                float falloff = Mathf.Abs(dy) <= FluidSim.Instance.sigma/cellSize ? 1 : 0;
                
                if (dy < 0)
                {
                    rGrid[0, j] = falloff;
                    gGrid[0, j] = 0;
                    bGrid[0, j] = 0;
                }
                else
                {
                    bGrid[0, j] = falloff;
                    rGrid[0, j] = 0;
                    gGrid[0, j] = 0;
                }
            }
        }
        
        public void Step(bool project, bool advect, float forceMagnitude = 100f)
        {
            ApplyExternalForces();
            
            if (vortexShedding)
                AddConstantForceToLeftWall(forceMagnitude);
            
            if (advect)
                AdvectVelocities();
            
            
            UpdatePressureField();
            
            
            if (project)
                ProjectVelocities();

            if (vortexShedding)
            {
                InjectDensityForVortexShedding();
                InjectColorForVortexShedding();
            }

            if (advect)
            {
                AdvectSmoke();
                if (FluidSim.Instance.enableDye)
                {
                    for (int c = 0; c < 3; c++)
                        AdvectDye(c);
                }
            }

            UpdateVorticityMap();
        }
    }
}