using System.Collections;
using UnityEngine;

public class PlatformMovment : MonoBehaviour
{
    private enum Axis { Vertical, Horizontal }

    [Header("Motion")]
    [Tooltip("Movement axis: Vertical = up/down, Horizontal = right/left.")]
    [SerializeField] private Axis movementAxis = Axis.Vertical;

    [Tooltip("Movement speed in units per second.")]
    [SerializeField] private float speed = 2f;

    [Tooltip("Distance to move in the positive direction (up when Vertical, right when Horizontal).")]
    [SerializeField] private float distanceUp = 2f;

    [Tooltip("Distance to move in the negative direction (down when Vertical, left when Horizontal).")]
    [SerializeField] private float distanceDown = 2f;

    [Header("Wait times")]
    [Tooltip("Seconds to wait when the platform reaches the positive end.")]
    [SerializeField] private float waitAtTop = 0.5f;

    [Tooltip("Seconds to wait when the platform reaches the negative end.")]
    [SerializeField] private float waitAtBottom = 0.5f;

    [Tooltip("If true uses localPosition, otherwise uses world position.")]
    [SerializeField] private bool useLocalSpace = true;

    [Tooltip("If true the platform will first move toward the positive direction (up or right depending on axis).")]
    [SerializeField] private bool startMovingUp = true;

    // runtime positions
    private Vector3 _startPos;
    private Vector3 _topPos;
    private Vector3 _bottomPos;
    private Coroutine _routine;

    private void Start()
    {
        // ensure sensible non-negative distances and non-negative waits BEFORE computing positions
        distanceUp = Mathf.Max(0f, distanceUp);
        distanceDown = Mathf.Max(0f, distanceDown);
        waitAtTop = Mathf.Max(0f, waitAtTop);
        waitAtBottom = Mathf.Max(0f, waitAtBottom);
        speed = Mathf.Max(0.0001f, speed);

        _startPos = useLocalSpace ? transform.localPosition : transform.position;

        Vector3 axisDir = movementAxis == Axis.Vertical ? Vector3.up : Vector3.right;
        _topPos = _startPos + axisDir * distanceUp;
        _bottomPos = _startPos - axisDir * distanceDown;

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
        Vector3 axisDir = movementAxis == Axis.Vertical ? Vector3.up : Vector3.right;
        Vector3 top = start + axisDir * distanceUp;
        Vector3 bottom = start - axisDir * distanceDown;

        Gizmos.color = Color.green;
        Gizmos.DrawSphere(top, 0.05f);
        Gizmos.DrawLine(start, top);

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(bottom, 0.05f);
        Gizmos.DrawLine(start, bottom);
    }
}