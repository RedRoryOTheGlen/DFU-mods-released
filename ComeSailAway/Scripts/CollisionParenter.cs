using UnityEngine;
using System.Collections;
using DaggerfallWorkshop.Game.Entity;

namespace ComeSailAwayMod
{
    public class CollisionParenter : MonoBehaviour
    {
        private void OnCollisionEnter(Collision collision)
        {
            if (collision.collider.GetComponent<DaggerfallEntityBehaviour>())
            {
                collision.collider.transform.SetParent(this.transform.parent);
            }
        }

        private void OnCollisionExit(Collision collision)
        {
            if (collision.collider.GetComponent<DaggerfallEntityBehaviour>() && collision.collider.transform.parent == this.transform.parent)
            {
                collision.collider.transform.SetParent(null);
            }
        }
    }
}