using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(HorizontalLayoutGroup))]
public class MainScreenSpacerHandler : MonoBehaviour
{
    private HorizontalLayoutGroup spacer;
    private float baseScale;
    
    void Awake()
     {
         spacer = GetComponent<HorizontalLayoutGroup>();
         baseScale = spacer.spacing;
     }

     void Start()
     {
         OnTransformChildrenChanged();
     }

     void OnTransformChildrenChanged()
     {
         switch (transform.childCount)
         {
             case 4:
                 spacer.spacing = baseScale;
                 break;
             case 3:
                 spacer.spacing = baseScale * 7;
                 break;
             case 2:
                 spacer.spacing = baseScale * 9.25f;
                 break;
             default:
                 spacer.spacing = baseScale;
                 break;
         }
     }
}
