using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(HorizontalLayoutGroup))]
public class MainScreenSpacerHandler : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        HorizontalLayoutGroup spacer = GetComponent<HorizontalLayoutGroup>();
        float baseScale = spacer.spacing;
        switch(transform.childCount)
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

        }
    }
}
