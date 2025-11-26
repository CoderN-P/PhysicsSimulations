using UnityEngine;

namespace Pendulum
{
    public class PendulumManager : MonoBehaviour
    {
        public float sceneHeight;
        public float length;
        public float theta;
        public float damping;
        public float pointSize;
        public float maxArrowLength;
        
        public SpriteRenderer pointRenderer;
        public LineRenderer rodRenderer;
        public GameObject velocityArrow;
        
        
        public Vector2 position;
        public Vector2 gravity = new Vector2(0, -9.81f);
        
        public float timeStep = 1f / 60f;
        
        private SpriteRenderer pointInstance;
        private LineRenderer rodInstance;
        private Arrow velocityArrowInstance;
        private float sceneWidth;
        
        private float thetaVel = 0f;
        
        void Awake()
        {
            InitializeCamera();
            Initialize();
        }
        
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
        
        void Initialize()
        {
            rodInstance = Instantiate(rodRenderer, position, Quaternion.identity);
            pointInstance = Instantiate(pointRenderer, position, Quaternion.identity);
            pointInstance.transform.localScale = new Vector3(pointSize, pointSize, 1);
            velocityArrowInstance = Instantiate(velocityArrow, position, Quaternion.identity).GetComponent<Arrow>();
        }

        void Render()
        {
            Vector2 bobPosition = position + length * new Vector2(Mathf.Sin(theta), -Mathf.Cos(theta));
            rodInstance.SetPosition(0, position);
            rodInstance.SetPosition(1, bobPosition);
            
            pointInstance.transform.position = bobPosition;
            
            // Render velocity arrow
            Vector2 bobVelocity = length * thetaVel * new Vector2(Mathf.Cos(theta), Mathf.Sin(theta));
            float velocityMag = bobVelocity.magnitude;
            
            if (velocityMag > maxArrowLength)
            {
                bobVelocity = bobVelocity.normalized * maxArrowLength;
            }
            
            velocityArrowInstance.Draw(bobPosition, bobPosition + bobVelocity);
        }
        
        void Update()
        {
            Render();
        }

        void FixedUpdate()
        {
            float thetaAccel = (gravity.y/length) * Mathf.Sin(theta);
            thetaAccel -= damping * thetaVel;
            
            thetaVel += thetaAccel * timeStep;
            theta += thetaVel * timeStep;
        }
    }
}