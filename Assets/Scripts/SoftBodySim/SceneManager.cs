using UnityEngine;

namespace SoftBodySim
{
    public class SceneManager : MonoBehaviour
    {
        public SoftBodyManager[] softBodies;
        public GameObject softBodyPrefab;
        public int numSoftBodies = 1;
        public float sceneHeight = 20.0f;
        public float sceneWidth;
        

        void InitializeCamera()
        {
            Camera cam = Camera.main;

            // Force the camera's height
            cam.orthographicSize = sceneHeight / 2f;

            // Compute width given aspect ratio
            sceneWidth = sceneHeight * cam.aspect;

            // Position camera so bottom-left = (0,0)
            cam.transform.position = new Vector3(
                sceneWidth * 0.5f,
                sceneHeight * 0.5f,
                cam.transform.position.z
            );
        }

        void Reset()
        {
            Debug.Log("Resetting scene");
            // Destroy existing soft bodies
            if (softBodies != null)
            {
                foreach (SoftBodyManager sb in softBodies)
                {
                    if (sb != null)
                    {
                        sb.Restart();
                    }
                }
            }
            
        }

        void Initialize()
        {
            softBodies = new SoftBodyManager[numSoftBodies];
            for (int i = 0; i < numSoftBodies; i++)
            {
                Vector2 position = new Vector2(sceneWidth/2, sceneHeight / 2);
                SoftBodyManager softBody = Instantiate(softBodyPrefab, position, Quaternion.identity).GetComponent<SoftBodyManager>();
                softBodies[i] = softBody;
            }
        }
        void Awake()
        {
            InitializeCamera();
            Reset();
            Initialize();
        }
        
        [ContextMenu("Reset Simulation")]
        public void ResetSimulationFromMenu()
        {
            Reset();  
        }
    }
}