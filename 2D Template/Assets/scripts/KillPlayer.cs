using UnityEngine;

public class KillPlayer : MonoBehaviour
{
    private void OnCollisionEnter2D(Collision2D collision)
    {

        if (collision.collider.GetComponent<Player>())
        {

            collision.collider.GetComponent<Player>().Die();

        }

    }
}