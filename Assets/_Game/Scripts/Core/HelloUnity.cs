using UnityEngine; // traz Debug, MonoBehaviour, Vector2, Time...

// Um MonoBehaviour é um Component que você pode pôr num GameObject.
public class HelloUnity : MonoBehaviour
{
    // [SerializeField] faz um campo privado aparecer no Inspector.
    // Equivale ao @export do Godot.
    [SerializeField] private float spinSpeed = 90f; // graus por segundo

    // Awake: roda 1x quando o objeto é criado (≈ _init / _enter_tree)
    private void Awake()
    {
        Debug.Log("Awake: " + gameObject.name);
    }

    // Start: roda 1x antes do primeiro Update (≈ _ready)
    private void Start()
    {
        Debug.Log($"Start! spinSpeed = {spinSpeed}"); // $"..." = string com {variáveis}
    }

    // Update: todo frame (≈ _process). Time.deltaTime ≈ delta
    private void Update()
    {
        transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
    }
}