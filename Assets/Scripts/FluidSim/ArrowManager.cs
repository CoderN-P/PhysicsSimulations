using UnityEngine;

public class ArrowManager : MonoBehaviour
{
    // Start is called before the first frame update
    public SpriteRenderer arrowRenderer;
    public LineRenderer lineRenderer;
    public bool disabled = false;

    // Update is called once per frame
    public void Draw(Vector2 start, Vector2 end, float scale)
    {
        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, end);
        
        Vector2 direction = end - start;

        if (start == end)
        {
            Disable();
        } 
        
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        
        arrowRenderer.transform.position = end;
        arrowRenderer.transform.rotation = Quaternion.Euler(0, 0, angle - 90);
        
        transform.localScale = new Vector3(scale, scale, 1);
    }

    public void Disable()
    {
        disabled = true;
        arrowRenderer.enabled = false;
        lineRenderer.enabled = false;
    }
    
    public void Enable()
    {
        disabled = false;
        arrowRenderer.enabled = true;
        lineRenderer.enabled = true;
    }
}
