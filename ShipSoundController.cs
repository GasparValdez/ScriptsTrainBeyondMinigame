using UnityEngine;

/// <summary>
/// Reproduce un sonido de un barco a intervalos regulares de tiempo.
/// Acumula el tiempo transcurrido solo cuando el sonido no se esta reproduciendo,
/// evitando superposiciones
/// sin necesidad de gestionarlos manualmente desde otros scripts.
/// Requiere un AudioSource en el mismo GameObject del barco.
/// </summary>

[RequireComponent(typeof(AudioSource))]
public class ShipSoundController : MonoBehaviour
{
    [Header("Configuracion")]
    // Clip de audio del barco que se reproduce cada vez que se cumple el intervalo
    [Tooltip("Sonido que se reproducira cada intervalo")]
    public AudioClip clip;

    // Tiempo en segundos entre cada reproduccion del sonido del barco
    [Tooltip("Intervalo en segundos entre cada reproduccion (por defecto 180 = 3 minutos)")]
    public float intervalSeconds = 180f;

    // Si es verdadero, reproduce el sonido inmediatamente al iniciar la escena
    [Tooltip("Reproduce el sonido al iniciar la escena sin esperar el intervalo completo")]
    public bool playOnStart = false;

    // AudioSource del mismo GameObject del barco para reproducir el clip
    private AudioSource _audioSource;

    // Acumulador de tiempo transcurrido desde la ultima reproduccion
    private float _timer;

    private void Awake()
    {
        // Obtiene el AudioSource del mismo GameObject del barco
        _audioSource = GetComponent<AudioSource>();

        // Desactiva la reproduccion automatica al iniciar para controlarlo manualmente
        _audioSource.playOnAwake = false;
    }

    private void Start()
    {
        // Si no hay clip asignado, registra una advertencia y detiene la ejecucion del script
        if (clip == null)
        {
            Debug.LogWarning("[ShipSound] No hay ningun AudioClip asignado.");
            return;
        }

        // Si playOnStart esta activo, inicializa el timer al maximo para que el Update
        // no dispare otra reproduccion inmediata justo despues del sonido inicial
        _timer = playOnStart ? intervalSeconds : 0f;

        // Reproduce el sonido al inicio si esta configurado para ello
        if (playOnStart)
            PlaySound();
    }

    private void Update()
    {
        // No hace nada si no hay clip asignado
        if (clip == null) return;

        // Solo acumula tiempo mientras el sonido no se esta reproduciendo
        // esto evita que el contador avance y se dispare un nuevo sonido encima del actual
        if (!_audioSource.isPlaying)
        {
            // Suma el tiempo del frame actual al acumulador
            _timer += Time.deltaTime;

            // Cuando el acumulador supera el intervalo configurado, reproduce el sonido
            if (_timer >= intervalSeconds)
            {
                // Reinicia el acumulador para comenzar a contar el siguiente intervalo
                _timer = 0f;

                PlaySound();
            }
        }
    }

    /// <summary>
    /// Reproduce el clip asignado como un sonido puntual sin interrumpir
    /// otros sonidos activos en el mismo AudioSource.
    /// </summary>
    private void PlaySound()
    {
        // Reproduce el clip sin cortar otros sonidos que puedan estar activos
        _audioSource.PlayOneShot(clip);

        // Registra en consola la hora exacta de reproduccion para facilitar el debug
        Debug.Log($"[ShipSound] Sonido reproducido a las {System.DateTime.Now:HH:mm:ss}");
    }

    /// <summary>
    /// Reinicia el acumulador de tiempo a cero para retrasar la siguiente reproduccion.
    /// Util para llamarlo desde otros scripts cuando ocurre algun evento en la escena.
    /// </summary>
    public void ResetTimer()
    {
        // Pone el temporizador a cero para reiniciar la cuenta del intervalo
        _timer = 0f;
    }
}