using UnityEngine;
using System.Collections;

namespace ComeSailAwayMod
{
    public class RudderAnimationEventListener : MonoBehaviour
    {
        public void OarEvent_In()
        {
            ComeSailAway.Instance.OarEvent_In();
        }
        public void OarEvent_Sweep()
        {
            ComeSailAway.Instance.OarEvent_Sweep();
        }
        public void OarEvent_Out()
        {
            ComeSailAway.Instance.OarEvent_Out();
        }
    }
}