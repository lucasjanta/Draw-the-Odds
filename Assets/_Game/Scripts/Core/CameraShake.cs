using UnityEngine;

// Tremida de câmera: desloca a câmera em volta da posição original.
public class CameraShake : MonoBehaviour
{
    private Vector3 origin;
    private float strength, timeLeft, totalTime;
    private bool shaking;

    public void Shake(float strength, float duration)
    {
        if (!shaking) origin = transform.localPosition;
        this.strength = Mathf.Max(this.strength * (timeLeft / Mathf.Max(totalTime, 0.01f)), strength);
        timeLeft = totalTime = duration;
        shaking = true;
    }

    private void LateUpdate()
    {
        if (!shaking) return;
        timeLeft -= Time.unscaledDeltaTime;
        if (timeLeft <= 0f)
        {
            transform.localPosition = origin;
            shaking = false;
            strength = 0f;
            return;
        }
        float k = timeLeft / totalTime;
        Vector2 off = Random.insideUnitCircle * strength * k;
        transform.localPosition = origin + new Vector3(off.x, off.y, 0f);
    }
}
