using System.Collections;
using UnityEngine;

public class PlatformMovment : MonoBehaviour
{
    [Header("Motion")]
    [Tooltip("Movement speed in units per second.")]
    [SerializeField] private float speed = 2f;

    [Tooltip("Distance to move upward from the start position.")]
    [SerializeField] private float distanceUp = 2f;

    [Tooltip("Distance to move downward from the start position.")]
    [SerializeField] private float distanceDown = 2f;

    [Header("Wait times")]
    [Tooltip("Seconds to wait when the platform reaches the top position.")]
    [SerializeField] private float waitAtTop = 0.5f;

    [Tooltip("Seconds to wait when the platform reaches the bottom position.")]
    [SerializeField] private float waitAtBottom = 0.5f;

    [Tooltip("If true uses localPosition, otherwise uses world position.")]
    [SerializeField] private bool useLocalSpace = true;

    [Tooltip("If true the platform will first move up, otherwise it will first move down.")]
    [SerializeField] private bool startMovingUp = true;

    // runtime positions
    private Vector3 _startPos;
    private Vector3 _topPos;
    private Vector3 _bottomPos;
    private Coroutine _routine;

    private void Start()
    {
        _startPos = useLocalSpace ? transform.localPosition : transform.position;
        _topPos = _startPos + Vector3.up * distanceUp;
        _bottomPos = _startPos - Vector3.up * distanceDown;

        // ensure sensible non-negative distances and non-negative waits
        distanceUp = Mathf.Max(0f, distanceUp);
        distanceDown = Mathf.Max(0f, distanceDown);
        waitAtTop = Mathf.Max(0f, waitAtTop);
        waitAtBottom = Mathf.Max(0f, waitAtBottom);
        speed = Mathf.Max(0.0001f, speed);

        _routine = StartCoroutine(MoveRoutine());
    }

    private IEnumerator MoveRoutine()
    {
        bool movingUp = startMovingUp;
        Vector3 target = movingUp ? _topPos : _bottomPos;

        while (true)
        {
            yield return StartCoroutine(MoveTo(target));

            // wait at the reached end
            yield return new WaitForSeconds(movingUp ? waitAtTop : waitAtBottom);

            // flip direction and set next target
            movingUp = !movingUp;
            target = movingUp ? _topPos : _bottomPos;
        }
    }

    private IEnumerator MoveTo(Vector3 target)
    {
        // Move smoothly using MoveTowards until target reached (with small epsilon)
        while (true)
        {
            Vector3 current = useLocalSpace ? transform.localPosition : transform.position;
            float dist = Vector3.Distance(current, target);
            if (dist <= 0.001f) break;

            Vector3 next = Vector3.MoveTowards(current, target, speed * Time.deltaTime);

            if (useLocalSpace) transform.localPosition = next;
            else transform.position = next;

            yield return null;
        }

        // snap exactly to target to avoid tiny differences
        if (useLocalSpace) transform.localPosition = target;
        else transform.position = target;
    }

    // helpful visualization in the editor
    private void OnDrawGizmosSelected()
    {
        Vector3 start = useLocalSpace ? transform.localPosition : transform.position;
        Vector3 top = start + Vector3.up * distanceUp;
        Vector3 bottom = start - Vector3.up * distanceDown;

        Gizmos.color = Color.green;
        Gizmos.DrawSphere(top, 0.05f);
        Gizmos.DrawLine(start, top);

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(bottom, 0.05f);
        Gizmos.DrawLine(start, bottom);
    }
}