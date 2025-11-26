using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SoftBodySim
{
    public class SoftBodyManager : MonoBehaviour
    {
        public float circleRadius;
        public float springConstant;
        public float dampingFactor;
        public float restLength;
        public int height;
        public int width;
        public float pointMass;
        public float diagonalKFactor;
        public Vector2 gravity;


        public GameObject pointPrefab;
        public GameObject springPrefab;

        // Soft body mass made of points and springs
        private Spring[] springs;
        private Point[,] points;

        private LineRenderer[] springRenderers;
        private SpriteRenderer[,] pointRenderers;
        private const float DT = 1f / 60f;
        private bool dragging = false;
        // Start is called before the first frame update

        Vector3 GetWorldPosition(Vector2 position)
        {
            Vector3 worldPos = new Vector3(position.x, position.y, 0);

            // transform.position is center of the softbody

            worldPos += transform.position;
            return worldPos;
        }

        void InitializeMass()
        {
            int numHorizontalSprings = (width - 1) * height;
            int numVerticalSprings = (height - 1) * width;
            int numDiagonalSprings = 2 * (width - 1) * (height - 1);
            springs = new Spring[numHorizontalSprings + numVerticalSprings + numDiagonalSprings];
            points = new Point[width, height];
            springRenderers = new LineRenderer[springs.Length];
            pointRenderers = new SpriteRenderer[width, height];

            float totalWidth = (width - 1) * restLength;
            float totalHeight = (height - 1) * restLength;
            Vector2 offset = new Vector2(-totalWidth / 2f, -totalHeight / 2f);
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Vector2 position = new Vector2(x * restLength, y * restLength) + offset;
                    Vector3 worldPos = GetWorldPosition(position);
                    pointRenderers[x, y] = Instantiate(pointPrefab, worldPos, Quaternion.identity)
                        .GetComponent<SpriteRenderer>();
                    pointRenderers[x, y].sortingOrder = 1;
                    pointPrefab.transform.localScale = new Vector3(circleRadius * 2, circleRadius * 2, 1);
                    Point point = new Point(pointMass, circleRadius, gravity, position);
                    points[x, y] = point;
                }
            }

            int springIndex = 0;

            // Horizontal springs
            for (int y = 0; y < height; y++)
            {
                for (int x = 1; x < width; x++)
                {
                    Point pointA = points[x - 1, y];
                    Point pointB = points[x, y];
                    Spring s = new Spring(pointA, pointB, restLength, springConstant, dampingFactor);
                    springRenderers[springIndex] = Instantiate(springPrefab, transform).GetComponent<LineRenderer>();
                    springRenderers[springIndex].sortingOrder = 0;
                    springs[springIndex] = s;
                    springIndex++;
                }
            }

            // Vertical Springs

            for (int x = 0; x < width; x++)
            {
                for (int y = 1; y < height; y++)
                {
                    Point pointA = points[x, y - 1];
                    Point pointB = points[x, y];
                    Spring s = new Spring(pointA, pointB, restLength, springConstant, dampingFactor);
                    springRenderers[springIndex] = Instantiate(springPrefab, transform).GetComponent<LineRenderer>();
                    springRenderers[springIndex].sortingOrder = 0;
                    springs[springIndex] = s;
                    springIndex++;
                }
            }

            // Diagonal Springs
            for (int y = 1; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (x < width - 1)
                    {
                        Point pointA = points[x, y];
                        Point pointB = points[x + 1, y - 1];
                        Spring s = new Spring(pointA, pointB, restLength * Mathf.Sqrt(2),
                            springConstant * diagonalKFactor, dampingFactor);
                        springRenderers[springIndex] =
                            Instantiate(springPrefab, transform).GetComponent<LineRenderer>();
                        springRenderers[springIndex].sortingOrder = 0;
                        springs[springIndex] = s;
                        springIndex++;
                    }

                    if (x > 0)
                    {
                        Point pointA = points[x, y];
                        Point pointB = points[x - 1, y - 1];
                        Spring s = new Spring(pointA, pointB, restLength * Mathf.Sqrt(2),
                            springConstant * diagonalKFactor, dampingFactor);
                        springRenderers[springIndex] =
                            Instantiate(springPrefab, transform).GetComponent<LineRenderer>();
                        springRenderers[springIndex].sortingOrder = 0;
                        springs[springIndex] = s;
                        springIndex++;
                    }
                }

            }
        }

        void Start()
        {
            InitializeMass();
        }

        void RenderBody()
        {
            for (int i = 0; i < springs.Length; i++)
            {
                if (springs[i] == null) continue;
                Spring s = springs[i];
                LineRenderer lr = springRenderers[i];
                lr.SetPosition(0, GetWorldPosition(s.pointA.position));
                lr.SetPosition(1, GetWorldPosition(s.pointB.position));
            }

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Point p = points[x, y];
                    SpriteRenderer sr = pointRenderers[x, y];
                    Vector3 worldPos = GetWorldPosition(p.position);
                    sr.transform.position = worldPos;
                }
            }
        }

        void Step()
        {
            for (int i = 0; i < springs.Length; i++)
            {
                if (springs[i] == null) continue;
                springs[i].Step(transform.position);
            }

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    points[x, y].Step(DT, transform.position);
                }
            }
        }

        public void CalculateCenterOfMass()
        {
            Vector2 com = Vector2.zero;
            float totalMass = 0f;
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Point p = points[x, y];
                    com += p.position * p.mass;
                    totalMass += p.mass;
                }
            }

            com /= totalMass;
            transform.position = GetWorldPosition(com);
        }

        public void Restart()
        {
            for (int i = 0; i < springRenderers.Length; i++)
            {
                if (springRenderers[i] != null)
                {
                    Destroy(springRenderers[i].gameObject);
                }
            }

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (pointRenderers[x, y] != null)
                    {
                        Destroy(pointRenderers[x, y].gameObject);
                    }
                }
            }

            InitializeMass();
        }

        // Update is called once per frame
        void Update()
        {
            RenderBody();
        }

        void FixedUpdate()
        {
            Step();
        }
    }
}
