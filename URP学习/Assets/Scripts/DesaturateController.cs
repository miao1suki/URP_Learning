using UnityEngine;

public class DesaturateController : MonoBehaviour
{
    static readonly int SaturationId = Shader.PropertyToID("_Saturation");

    [SerializeField] private Material desaturateMaterial;
    [SerializeField] private float transitionPeriod = 1f;
    [SerializeField, Range(0f, 1f)] private float normalSaturation = 1f;
    
    private bool transitioning;
    private float startTime;
    private float startSaturation;
    private float targetSaturation;

    private void OnEnable()
    {
        DesaturateInputNotifier.FadeRequested += StartFadeTo;
    }

    private void OnDisable()
    {
        DesaturateInputNotifier.FadeRequested -= StartFadeTo;
    }

    private void Start()
    {
        SetSaturation(normalSaturation);
    }

    private void Update()
    {
        if (!transitioning || desaturateMaterial == null)
            return;

        float transitionTime = Mathf.Max(transitionPeriod, 0.0001f);
        float t = Mathf.Clamp01((Time.time - startTime) / transitionTime);

        SetSaturation(Mathf.Lerp(startSaturation, targetSaturation, t));

        if (t >= 1f)
            transitioning = false;
    }

    private void StartFadeTo(float saturation)
    {
        if (desaturateMaterial == null)
            return;

        startSaturation = desaturateMaterial.GetFloat(SaturationId);
        targetSaturation = saturation;
        startTime = Time.time;
        transitioning = true;
    }

    private void SetSaturation(float saturation)
    {
        if (desaturateMaterial != null)
            desaturateMaterial.SetFloat(SaturationId, saturation);
    }
}
