using UnityEngine;

namespace FluidSim
{
    public class FluidSim : MonoBehaviour
    {
        // Singleton instance
        public static FluidSim Instance;
        
        [Header("Grid Sizing")]
        public int width;
        public int height;
        public float cellSize;
        
        [Header("Simulation Parameters")]
        public float overrelaxationFactor = 1.9f;
        public int pressureSolveIterations = 20;
        public float timeStep = 1f / 60f;
        public bool project;
        public bool advect;
        public bool initializeRandom;
        public bool paused;
        public bool stepping;
        
        
        [Header("Vortex Shedding Parameters")]
        public float leftWallForce;
        public float sigma;
        public float injectionDensity;
        public bool vortexShedding;
        
        
        public FluidSolver fluidSolver;
        public FluidRenderer fluidRenderer;
        
        void Awake()
        {
            // Setup singleton instance
            if (Instance == null)
                Instance = this;
            else
                Destroy(gameObject);
            
            // Initialize fluid solver
            fluidSolver = new FluidSolver(width, height, cellSize, overrelaxationFactor, pressureSolveIterations, vortexShedding, initializeRandom);
            fluidRenderer.Initialize();
        }

        void LateUpdate()
        {
            fluidRenderer.GetKeyboardInput();
            fluidRenderer.Render(
                fluidSolver.uGrid,
                fluidSolver.vGrid,
                fluidSolver.solid
            );
        }

        void FixedUpdate()
        {
            if (paused) return;
            fluidSolver.Step(project, advect, leftWallForce);
        }
    }
}