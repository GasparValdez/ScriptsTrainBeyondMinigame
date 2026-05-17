using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Controlador principal del minijuego BOP Torque Sequence.
/// Se encarga de gestionar el estado de la partida, validar el orden de ajuste
/// de las tuercas, llevar la puntuacion, controlar el temporizador y notificar
/// a la interfaz grafica mediante eventos de Unity.
/// </summary>

// Define los modos de juego disponibles
public enum GameMode
{
    Assisted,  // Muestra indicaciones visuales sobre la siguiente tuerca a ajustar
    Challenge  // Sin ayudas visuales, el jugador debe memorizar el orden
}

public class BoltGameManager : MonoBehaviour
{
    // Referencia estatica para acceder al manager desde cualquier script sin buscarlo
    public static BoltGameManager Instance { get; private set; }

    [Header("Configuracion del juego")]
    // Modo de juego activo, configurable desde el Inspector
    [Tooltip("Assisted: muestra indicaciones visuales. Challenge: sin ayudas.")]
    public GameMode gameMode = GameMode.Assisted;

    // Referencia al panel superior de la UI para mostrarlo u ocultarlo segun el estado
    public GameObject panel_TopBar;

    // Numero total de tuercas en el ensamble
    [Tooltip("Numero total de tuercas en el ensamble (por defecto 8 para el patron octagonal del BOP)")]
    public int totalBolts = 8;

    // Tiempo limite de la partida en segundos, 0 significa sin limite
    [Tooltip("Tiempo limite en segundos. Poner 0 para tiempo ilimitado.")]
    public float timeLimitSeconds = 120f;

    // Duracion de la animacion de apriete por tuerca
    [Tooltip("Duracion en segundos de la animacion de apriete de cada tuerca")]
    public float tighteningAnimDuration = 0.8f;

    [Header("Puntuacion")]
    // Puntos que se suman por cada tuerca ajustada correctamente
    public int pointsPerCorrectBolt = 100;

    // Puntos que se restan por cada tuerca incorrecta
    public int penaltyPerWrongBolt = 25;

    [Header("Audio")]
    // Sonido que se reproduce al ajustar una tuerca correctamente
    public AudioClip correctTightenSound;

    // Sonido que se reproduce al seleccionar una tuerca incorrecta
    public AudioClip wrongBoltSound;

    // Sonido que se reproduce al completar toda la secuencia
    public AudioClip completionSound;

    // Sonido de beep durante la cuenta regresiva final
    public AudioClip countdownBeepSound;

    [Header("Eventos")]
    // Evento que se dispara cuando inicia la partida
    public UnityEvent OnGameStart;

    // Evento que se dispara cuando el jugador completa toda la secuencia
    public UnityEvent OnGameComplete;

    // Evento que se dispara cuando se acaba el tiempo
    public UnityEvent OnGameFail;

    // Evento que envia la puntuacion actual y el cambio ocurrido
    public UnityEvent<int, int> OnScoreChanged;

    // Evento que envia el ID de la tuerca ajustada correctamente
    public UnityEvent<int> OnBoltCorrect;

    // Evento que envia el ID de la tuerca seleccionada incorrectamente
    public UnityEvent<int> OnBoltWrong;

    // Evento que envia el tiempo restante cada frame para actualizar la UI
    public UnityEvent<float> OnTimerTick;

    // Secuencia correcta de ajuste segun la Figura 5-1, patron en estrella para distribucion uniforme de presion
    public static readonly int[] CorrectSequence = { 1, 2, 3, 4, 5, 6, 7, 8 };

    // Indice del paso actual dentro de CorrectSequence
    private int currentStep = 0;

    // Puntuacion acumulada de la partida
    private int score = 0;

    // Tiempo restante en segundos
    private float timeRemaining;

    // Indica si la partida esta en curso
    private bool gameActive = false;

    // Indica si la partida ya fue completada exitosamente
    private bool gameCompleted = false;

    // Diccionario que asocia cada ID de tuerca con su BoltController correspondiente
    private Dictionary<int, BoltController> boltRegistry = new Dictionary<int, BoltController>();

    // Componente de audio del GameManager para reproducir sonidos globales
    private AudioSource audioSource;

    private void Awake()
    {
        // Si ya existe una instancia del manager en escena, destruye este duplicado
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // Registra esta instancia como el singleton activo
        Instance = this;

        // Busca el AudioSource adjunto o lo crea si no existe
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    private void Start()
    {
        // Las tuercas se auto-registran desde BoltController.Start() via RegisterBolt()
        Debug.Log("[BoltGame] Manager listo. Esperando registro de tuercas...");
    }

    private void Update()
    {
        // Solo ejecuta la logica del temporizador si el juego esta activo y no completado
        if (!gameActive || gameCompleted) return;

        if (timeLimitSeconds > 0)
        {
            // Guarda el segundo entero antes de restar para detectar el cruce entre segundos
            int secondBefore = Mathf.CeilToInt(timeRemaining);

            // Resta el tiempo transcurrido desde el ultimo frame
            timeRemaining -= Time.deltaTime;

            // Si el tiempo llego a cero, notifica el tick con 0 y activa el fallo
            if (timeRemaining <= 0f)
            {
                timeRemaining = 0f;
                OnTimerTick?.Invoke(timeRemaining);
                TriggerGameFail("Tiempo agotado");
                return;
            }

            // Notifica el tiempo restante a la UI cada frame
            OnTimerTick?.Invoke(timeRemaining);

            // En los ultimos 10 segundos, reproduce un beep cada vez que cambia el segundo entero
            if (timeRemaining < 11f && countdownBeepSound != null)
            {
                int secondAfter = Mathf.CeilToInt(timeRemaining);

                // Solo suena cuando el segundo entero disminuyo, evitando beeps multiples por frame
                if (secondAfter < secondBefore)
                    audioSource.PlayOneShot(countdownBeepSound, 0.5f);
            }
        }
    }

    /// <summary>
    /// Registra una tuerca en el diccionario para que el manager pueda
    /// acceder a ella por ID durante la partida.
    /// </summary>
    public void RegisterBolt(BoltController bolt)
    {
        // Solo registra si la ID no esta ya en el diccionario para evitar duplicados
        if (!boltRegistry.ContainsKey(bolt.boltId))
            boltRegistry[bolt.boltId] = bolt;
    }

    /// <summary>
    /// Inicia o reinicia la partida asignando primero el modo de juego indicado.
    /// </summary>
    public void StartGame(GameMode mode)
    {
        // Asigna el modo recibido y luego llama al metodo principal de inicio
        gameMode = mode;
        StartGame();
    }

    /// <summary>
    /// Inicia o reinicia la partida usando el modo de juego actualmente configurado.
    /// Reinicia todas las tuercas, la puntuacion y el temporizador.
    /// </summary>
    public void StartGame()
    {
        // Muestra el panel superior de la UI
        panel_TopBar.SetActive(true);

        // Reinicia el paso actual al primero de la secuencia
        currentStep = 0;

        // Reinicia la puntuacion
        score = 0;

        // Marca la partida como no completada
        gameCompleted = false;

        // Establece el tiempo restante segun la configuracion, o maximo si es ilimitado
        timeRemaining = timeLimitSeconds > 0 ? timeLimitSeconds : float.MaxValue;

        // Reinicia el estado visual y de posicion de todas las tuercas registradas
        foreach (var kv in boltRegistry)
            kv.Value.ResetBolt();

        // Activa el indicador visual de la primera tuerca a ajustar
        HighlightNextBolt();

        // Marca la partida como activa
        gameActive = true;

        // Notifica a la UI que el juego inicio
        OnGameStart?.Invoke();

        // Notifica la puntuacion inicial de cero
        OnScoreChanged?.Invoke(score, 0);

        Debug.Log("[BoltGame] Partida iniciada.");
    }

    /// <summary>
    /// Llamado por BoltController cuando el jugador interactua con una tuerca.
    /// Valida si la tuerca es la correcta en el paso actual y delega el resultado.
    /// </summary>
    public void OnBoltInteracted(int boltId)
    {
        // Ignora interacciones si el juego no esta activo o ya fue completado
        if (!gameActive || gameCompleted) return;

        // Obtiene el ID esperado en el paso actual de la secuencia
        int expectedBoltId = CorrectSequence[currentStep];

        // Compara el ID interactuado con el esperado y delega al metodo correspondiente
        if (boltId == expectedBoltId)
            HandleCorrectBolt(boltId);
        else
            HandleWrongBolt(boltId);
    }

    // Devuelve la puntuacion actual
    public int GetCurrentScore() => score;

    // Devuelve el paso actual dentro de la secuencia
    public int GetCurrentStep() => currentStep;

    // Devuelve el numero total de pasos en la secuencia
    public int GetTotalSteps() => CorrectSequence.Length;

    // Devuelve el tiempo restante en segundos
    public float GetTimeRemaining() => timeRemaining;

    // Devuelve si la partida esta activa en este momento
    public bool IsGameActive() => gameActive;

    /// <summary>
    /// Gestiona el resultado de ajustar una tuerca correcta.
    /// Incrementa el paso, suma puntos, actualiza visuals y verifica si se completo la secuencia.
    /// </summary>
    private void HandleCorrectBolt(int boltId)
    {
        Debug.Log($"[BoltGame] Correcto. Tuerca {boltId} ({currentStep + 1}/{CorrectSequence.Length})");

        // Incrementa el paso antes de disparar eventos para que la UI lea el valor actualizado
        currentStep++;

        // Calcula y aplica el incremento de puntuacion
        int delta = pointsPerCorrectBolt;
        score += delta;

        // Notifica el cambio de puntuacion a la UI
        OnScoreChanged?.Invoke(score, delta);

        // Notifica que una tuerca fue ajustada correctamente
        OnBoltCorrect?.Invoke(boltId);

        // Reproduce el sonido de exito
        PlaySound(correctTightenSound);

        // Marca la tuerca como completada visualmente, incluyendo la ultima de la secuencia
        if (boltRegistry.TryGetValue(boltId, out var doneBolt))
            doneBolt.SetState(BoltState.Completed);

        // Si se completaron todos los pasos, inicia la corrutina de finalizacion
        if (currentStep >= CorrectSequence.Length)
            StartCoroutine(TriggerCompletionNextFrame());
        else
            // Si quedan pasos, resalta la siguiente tuerca
            HighlightNextBolt();
    }

    /// <summary>
    /// Gestiona el resultado de intentar ajustar una tuerca incorrecta.
    /// Resta puntos, activa el feedback visual de error y notifica a la UI.
    /// </summary>
    private void HandleWrongBolt(int boltId)
    {
        Debug.Log($"[BoltGame] Incorrecto. Tuerca {boltId}. Se esperaba {CorrectSequence[currentStep]}.");

        // Activa la animacion de error en la tuerca seleccionada
        if (boltRegistry.TryGetValue(boltId, out BoltController bolt))
            StartCoroutine(bolt.PlayErrorFeedback());

        // Calcula y aplica la penalizacion, sin bajar de cero
        int delta = -penaltyPerWrongBolt;
        score = Mathf.Max(0, score + delta);

        // Notifica el cambio de puntuacion a la UI
        OnScoreChanged?.Invoke(score, delta);

        // Notifica que se selecciono una tuerca incorrecta
        OnBoltWrong?.Invoke(boltId);

        // Reproduce el sonido de error
        PlaySound(wrongBoltSound);
    }

    /// <summary>
    /// Activa el indicador visual en la siguiente tuerca de la secuencia.
    /// En modo Desafio no hace nada para no dar pistas al jugador.
    /// </summary>
    private void HighlightNextBolt()
    {
        // No resalta si ya se completaron todos los pasos
        if (currentStep >= CorrectSequence.Length) return;

        // En modo Desafio no se muestran indicaciones visuales
        if (gameMode == GameMode.Challenge) return;

        // Obtiene el ID de la siguiente tuerca y activa su estado de resaltado
        int nextId = CorrectSequence[currentStep];
        if (boltRegistry.TryGetValue(nextId, out BoltController nextBolt))
            nextBolt.SetState(BoltState.Highlighted);
    }

    /// <summary>
    /// Espera un breve momento antes de declarar la partida como completada,
    /// para que la animacion de la ultima tuerca termine antes de mostrar resultados.
    /// </summary>
    private IEnumerator TriggerCompletionNextFrame()
    {
        // Espera la duracion de la animacion mas un margen de seguridad
        yield return new WaitForSeconds(tighteningAnimDuration + 0.1f);
        TriggerGameComplete();
    }

    /// <summary>
    /// Declara la partida como completada, detiene el juego,
    /// reproduce el sonido de exito y notifica a la UI.
    /// </summary>
    private void TriggerGameComplete()
    {
        // Detiene el bucle de juego
        gameActive = false;

        // Marca la partida como completada para bloquear interacciones posteriores
        gameCompleted = true;

        // Reproduce el sonido de completado
        PlaySound(completionSound);

        // Notifica a la UI para mostrar la pantalla de resultados
        OnGameComplete?.Invoke();

        Debug.Log($"[BoltGame] Secuencia completada. Puntuacion final: {score}");
    }

    /// <summary>
    /// Declara la partida como fallida, detiene el juego,
    /// quita el resaltado de la tuerca pendiente y notifica a la UI.
    /// </summary>
    private void TriggerGameFail(string reason)
    {
        // Detiene el bucle de juego
        gameActive = false;

        // Si habia una tuerca resaltada pendiente, la regresa a estado inactivo
        if (currentStep < CorrectSequence.Length)
        {
            int pendingId = CorrectSequence[currentStep];
            if (boltRegistry.TryGetValue(pendingId, out var pendingBolt))
                pendingBolt.SetState(BoltState.Idle);
        }

        // Reproduce el sonido de error como indicador de fallo
        PlaySound(wrongBoltSound);

        // Notifica a la UI para mostrar la pantalla de fallo
        OnGameFail?.Invoke();

        Debug.Log($"[BoltGame] Partida fallida: {reason}");
    }

    /// <summary>
    /// Reproduce un AudioClip como un sonido puntual sin interrumpir otros sonidos activos.
    /// </summary>
    private void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
            audioSource.PlayOneShot(clip);
    }
}