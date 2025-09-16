using UnityEngine;
using System.Collections.Generic;

public class ToggleMeshRenderers : MonoBehaviour
{
    [SerializeField] private MeshRenderer[] meshRenderers;
    [SerializeField] private Color offColor = new Color(0x37/255f, 0x37/255f, 0x37/255f); // Hex: 373737
    [SerializeField] private Color onColor = Color.white; // Hex: FFFFFF

    private bool isEmissionOn = false;

    public void InitializeEmissionState()
    {
        isEmissionOn = false;
        SetEmissionState(false);
    }

    public void ToggleEmissionStates()
    {
        isEmissionOn = !isEmissionOn;
        SetEmissionState(isEmissionOn);
    }

    private void SetEmissionState(bool state)
    {
        foreach (MeshRenderer renderer in meshRenderers)
        {
            Material[] materials = renderer.materials;

            foreach (Material material in materials)
            {
                if (material.HasProperty("_EmissionColor"))
                {
                    if (state)
                    {
                        material.EnableKeyword("_EMISSION");
                        material.SetColor("_Color", onColor);
                    }
                    else
                    {
                        material.DisableKeyword("_EMISSION");
                        material.SetColor("_Color", offColor);
                    }
                }
            }

            renderer.materials = materials;
        }
    }
}
