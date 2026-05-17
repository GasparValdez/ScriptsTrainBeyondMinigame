using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controla toda la interfaz grafica del minijuego BOP Torque Sequence.
/// Se suscribe a los eventos del BoltGameManager para actualizar en tiempo real
/// el contador de pasos, la puntuacion, el temporizador y los mensajes de feedback.
/// Tambien gestiona la navegacion entre los paneles de inicio, selector de modo,
/// controles y resultados, ademas de coordinar las animaciones de camara.
/// Debe estar adjunto al Canvas principal de la escena.
/// </summary>
public class BoltGameHUD : MonoBehaviour
{
    [Header("UI del juego")]
    // Texto que muestra el progreso de pasos completados sobre el total
    public TextMeshProUGUI txt_StepCounter;

    // Texto que muestra la puntuacion actual del jugador
    public TextMeshProUGUI txt_Score;

    // Texto que muestra el tiempo restante en formato minutos:segundos
    public TextMeshProUGUI txt_Timer;

    // Boton para reiniciar la partida desde la pantalla de resultados
    public Button btn_Restart;

    [Header("Botones de navegacion y paneles")]
    // Boton que regresa al selector de modo desde la barra superior
    public Button btn_BackModeSelector;

    // Boton que regresa al menu principal desde la barra superior
    public Button btn_BackTopBar;

    // Boton que regresa al menu de controles
    public Button btn_BackControlsMenu;

    // Panel de la barra superior con el contador, puntuacion y temporizador
    public GameObject panel_TopBar;

    // Panel del selector de modo de juego
    public GameObject panel_SelectMode;

    // Panel con la informacion de controles del juego
    public GameObject panel_Controls;

    // Panel del menu principal
    public GameObject panel_MainMenu;

    [Header("Flash de retroalimentacion")]
    // CanvasGroup del panel de flash para controlar su transparencia globalmente
    public CanvasGroup feedbackFlashGroup;

    // Imagen de fondo del flash que cambia de color segun el resultado
    public Image img_FlashBg;

    // Texto que aparece sobre el flash con el mensaje de correcto o incorrecto
    public TextMeshProUGUI txt_Flash;

    // Color verde semitransparente para el flash de tuerca correcta
    public Color colorCorrect = new Color(0.2f, 0.85f, 0.3f, 0.5f);

    // Color rojo semitransparente para el flash de tuerca incorrecta
    public Color colorWrong = new Color(0.9f, 0.15f, 0.15f, 0.5f);

    [Header("Pantalla de inicio")]
    // Boton que inicia la partida en modo Asistido con indicaciones visuales
    [Tooltip("Boton que inicia en modo Assisted")]
    public Button btn_StartAssisted;

    // Boton que inicia la partida en modo Desafio sin indicaciones visuales
    [Tooltip("Boton que inicia en modo Challenge")]
    public Button btn_StartChallenge;

    [Header("Animaciones de camara")]
    // Controlador de animacion que se asigna a la camara al entrar al juego
    [Tooltip("Animator Controller que se reproduce al entrar al juego")]
    public RuntimeAnimatorController camAnimEnter;

    // Controlador de animacion que se asigna a la camara al regresar al selector
    [Tooltip("Animator Controller que se reproduce al regresar al selector")]
    public RuntimeAnimatorController camAnimBack;

    private void Awake()
    {
        // Conecta el boton de reinicio para iniciar una nueva partida con el modo actual
        if (btn_Restart != null)
            btn_Restart.onClick.AddListener(() =>
            {
                BoltGameManager.Instance?.StartGame();
            });

        // Conecta el boton de inicio en modo Asistido
        if (btn_StartAssisted != null)
            btn_StartAssisted.onClick.AddListener(() => StartWithMode(GameMode.Assisted));

        // Conecta el boton de inicio en modo Desafio
        if (btn_StartChallenge != null)
            btn_StartChallenge.onClick.AddListener(() => StartWithMode(GameMode.Challenge));

        // Asegura que el panel de flash comience invisible
        if (feedbackFlashGroup != null) feedbackFlashGroup.alpha = 0f;

        // Inicializa el contador de pasos en cero
        UpdateStepCounter(0);

        // Inicializa la puntuacion en cero
        UpdateScore(0);
    }

    /// <summary>
    /// Oculta el selector de modo, muestra la barra superior, lanza la animacion
    /// de camara de entrada y comunica el modo elegido al BoltGameManager.
    /// </summary>
    private void StartWithMode(GameMode mode)
    {
        // Oculta el panel del selector de modo
        if (panel_SelectMode != null) panel_SelectMode.SetActive(false);

        // Muestra la barra superior con el contador, puntuacion y temporizador
        if (panel_TopBar != null) panel_TopBar.SetActive(true);

        // Asigna y activa el controlador de animacion de entrada en la camara principal
        var camAnimator = Camera.main.GetComponent<Animator>();
        if (camAnimator != null && camAnimEnter != null)
        {
            camAnimator.runtimeAnimatorController = camAnimEnter;
            camAnimator.enabled = true;
        }

        // Notifica al BoltGameManager el modo elegido para iniciar la partida
        BoltGameManager.Instance?.StartGame(mode);
    }

    private void Start()
    {
        // Se suscribe a los eventos del BoltGameManager en Start para garantizar
        // que la instancia del singleton ya exista cuando se ejecuta esta linea
        var gm = BoltGameManager.Instance;
        if (gm != null)
        {
            gm.OnGameStart.AddListener(OnGameStart);
            gm.OnGameComplete.AddListener(OnGameComplete);
            gm.OnGameFail.AddListener(OnGameFail);
            gm.OnScoreChanged.AddListener(OnScoreChanged);
            gm.OnBoltCorrect.AddListener(OnBoltCorrect);
            gm.OnBoltWrong.AddListener(OnBoltWrong);
            gm.OnTimerTick.AddListener(OnTimerTick);
            Debug.Log("[HUD] Suscrito a eventos del BoltGameManager correctamente.");
        }
        else
        {
            Debug.LogError("[HUD] BoltGameManager.Instance es null en Start(). Verifica que el GameManager exista en la escena.");
        }
    }

    /// <summary>
    /// Se ejecuta cuando el BoltGameManager dispara OnGameStart.
    /// Reinicia el contador y la puntuacion, y configura la UI segun el modo activo.
    /// </summary>
    private void OnGameStart()
    {
        // Reinicia visualmente el contador de pasos
        UpdateStepCounter(0);

        // Reinicia visualmente la puntuacion
        UpdateScore(0);

        // Oculta el boton de reinicio durante la partida
        if (btn_Restart != null)
            btn_Restart.gameObject.SetActive(false);

        // Oculta el boton de regreso al selector durante la partida
        if (btn_BackModeSelector != null)
            btn_BackModeSelector.gameObject.SetActive(false);

        // Verifica si el modo activo es Asistido para mostrar instrucciones
        bool isAsistido = BoltGameManager.Instance?.gameMode == GameMode.Assisted;

        // En modo Asistido muestra la instruccion de la primera tuerca
        if (isAsistido)
            SetInstruction(1);
    }

    /// <summary>
    /// Se ejecuta cuando el BoltGameManager dispara OnGameComplete.
    /// Muestra el mensaje de exito, la puntuacion final y los botones de navegacion.
    /// </summary>
    private void OnGameComplete()
    {
        // Fuerza el contador a mostrar el total completo al finalizar
        UpdateStepCounter(BoltGameManager.CorrectSequence.Length);

        if (panel_TopBar != null)
        {
            // Muestra el boton de reinicio en la pantalla de resultados
            if (btn_Restart != null)
                btn_Restart.gameObject.SetActive(true);

            // Muestra el boton de regreso al selector en la pantalla de resultados
            if (btn_BackModeSelector != null)
                btn_BackModeSelector.gameObject.SetActive(true);

            // Mantiene visible la barra superior para mostrar los resultados
            panel_TopBar.SetActive(true);

            // Reemplaza el contador de pasos con el mensaje de exito
            if (txt_StepCounter != null)
                txt_StepCounter.text = "Secuencia completada";

            // Muestra la puntuacion final en el campo de puntuacion
            if (txt_Score != null)
                txt_Score.text = $"Puntuacion final: {BoltGameManager.Instance.GetCurrentScore()} pts";
        }
    }

    /// <summary>
    /// Se ejecuta cuando el BoltGameManager dispara OnGameFail por tiempo agotado.
    /// Muestra el mensaje de fallo, la puntuacion obtenida y los botones de navegacion.
    /// </summary>
    private void OnGameFail()
    {
        if (panel_TopBar != null)
        {
            // Muestra el boton de reinicio en la pantalla de fallo
            if (btn_Restart != null)
                btn_Restart.gameObject.SetActive(true);

            // Muestra el boton de regreso al selector en la pantalla de fallo
            if (btn_BackModeSelector != null)
                btn_BackModeSelector.gameObject.SetActive(true);

            // Mantiene visible la barra superior para mostrar los resultados
            panel_TopBar.SetActive(true);

            // Reemplaza el contador de pasos con el mensaje de tiempo agotado
            if (txt_StepCounter != null)
                txt_StepCounter.text = "Tiempo agotado";

            // Muestra la puntuacion obtenida hasta el momento del fallo
            if (txt_Score != null)
                txt_Score.text = $"Puntuacion final: {BoltGameManager.Instance.GetCurrentScore()} pts";
        }
    }

    /// <summary>
    /// Oculta la barra superior, muestra el selector de modo y activa
    /// la animacion de camara de regreso.
    /// </summary>
    public void BackToModeSelector()
    {
        // Oculta la barra superior del juego
        if (panel_TopBar != null) panel_TopBar.gameObject.SetActive(false);

        // Muestra el panel del selector de modo
        if (panel_SelectMode != null) panel_SelectMode.gameObject.SetActive(true);

        // Asigna y activa el controlador de animacion de regreso en la camara principal
        var camAnimator = Camera.main.GetComponent<Animator>();
        if (camAnimator != null && camAnimBack != null)
        {
            camAnimator.runtimeAnimatorController = camAnimBack;
            camAnimator.enabled = true;
        }
    }

    // Navega desde el menu principal al selector de modo
    public void GoToModeSelector()
    {
        if (panel_MainMenu != null) panel_MainMenu.gameObject.SetActive(false);
        if (panel_SelectMode != null) panel_SelectMode.gameObject.SetActive(true);
    }

    // Navega desde el selector de modo de vuelta al menu principal
    public void BackToMainMenu()
    {
        if (panel_SelectMode != null) panel_SelectMode.gameObject.SetActive(false);
        if (panel_MainMenu != null) panel_MainMenu.gameObject.SetActive(true);
    }

    // Navega desde el menu de controles de vuelta al menu principal
    public void BackToControlsMenu()
    {
        if (panel_Controls != null) panel_Controls.gameObject.SetActive(false);
        if (panel_MainMenu != null) panel_MainMenu.gameObject.SetActive(true);
    }

    // Navega desde el menu principal al menu de controles
    public void GoToControlsMenu()
    {
        if (panel_Controls != null) panel_Controls.gameObject.SetActive(true);
        if (panel_MainMenu != null) panel_MainMenu.gameObject.SetActive(false);
    }

    /// <summary>
    /// Se ejecuta cuando el BoltGameManager dispara OnScoreChanged.
    /// Actualiza el texto de puntuacion con el nuevo total.
    /// </summary>
    private void OnScoreChanged(int total, int delta)
    {
        UpdateScore(total);
    }

    /// <summary>
    /// Se ejecuta cuando el BoltGameManager dispara OnBoltCorrect.
    /// Actualiza el contador de pasos, la instruccion en modo Assisted y muestra el flash verde.
    /// </summary>
    private void OnBoltCorrect(int boltId)
    {
        // GetCurrentStep ya fue incrementado antes de disparar este evento, por lo que
        // su valor actual equivale al numero de tuercas completadas
        int completed = BoltGameManager.Instance.GetCurrentStep();

        // Actualiza el contador de pasos con el nuevo total completado
        UpdateStepCounter(completed);

        // En modo Assisted actualiza la instruccion con la siguiente tuerca a ajustar
        if (completed < BoltGameManager.CorrectSequence.Length
            && BoltGameManager.Instance?.gameMode == GameMode.Assisted)
            SetInstruction(BoltGameManager.CorrectSequence[completed]);

        // Muestra el flash verde de correcto
        StartCoroutine(FlashFeedback("Correcto", colorCorrect));
    }

    /// <summary>
    /// Se ejecuta cuando el BoltGameManager dispara OnBoltWrong.
    /// Muestra el flash rojo de tuerca incorrecta.
    /// </summary>
    private void OnBoltWrong(int boltId)
    {
        StartCoroutine(FlashFeedback("Tuerca incorrecta", colorWrong));
    }

    /// <summary>
    /// Se ejecuta cada frame con el tiempo restante enviado por OnTimerTick.
    /// Actualiza el texto del temporizador y lo colorea de rojo en los ultimos 15 segundos.
    /// </summary>
    private void OnTimerTick(float remaining)
    {
        if (txt_Timer == null) return;

        // Garantiza que el tiempo no muestre valores negativos por imprecision de frames
        remaining = Mathf.Max(0f, remaining);

        // Calcula los minutos y segundos por separado para el formato mm:ss
        int minutes = Mathf.FloorToInt(remaining / 60f);
        int seconds = Mathf.FloorToInt(remaining % 60f);

        // Actualiza el texto con el tiempo formateado
        txt_Timer.text = $"{minutes}:{seconds:D2}";

        // Cambia el color del texto a rojo cuando quedan menos de 15 segundos
        txt_Timer.color = remaining < 15f ? Color.red : Color.white;
    }

    /// <summary>
    /// Actualiza el texto del contador con el numero de pasos completados sobre el total.
    /// </summary>
    private void UpdateStepCounter(int step)
    {
        if (txt_StepCounter != null)
            txt_StepCounter.text = $"{step} / {BoltGameManager.CorrectSequence.Length}";
    }

    /// <summary>
    /// Actualiza el texto de puntuacion con el valor actual.
    /// </summary>
    private void UpdateScore(int value)
    {
        if (txt_Score != null)
            txt_Score.text = $"{value} pts";
    }

    /// <summary>
    /// Corrutina que muestra un panel de flash semitransparente con un mensaje
    /// y un color determinado, hace un fade in rapido, espera y luego hace fade out.
    /// Se usa para dar retroalimentacion visual inmediata al jugador.
    /// </summary>
    private IEnumerator FlashFeedback(string message, Color color)
    {
        if (feedbackFlashGroup == null) yield break;

        // Asigna el color al fondo del flash segun si es correcto o incorrecto
        if (img_FlashBg != null) img_FlashBg.color = color;

        // Asigna el mensaje al texto del flash
        if (txt_Flash != null) txt_Flash.text = message;

        // Fade in rapido de 0 a 1 en 0.15 segundos
        float t = 0f;
        while (t < 0.15f)
        {
            t += Time.deltaTime;
            feedbackFlashGroup.alpha = Mathf.Lerp(0f, 1f, t / 0.15f);
            yield return null;
        }
        feedbackFlashGroup.alpha = 1f;

        // Mantiene el flash visible durante medio segundo
        yield return new WaitForSeconds(0.5f);

        // Fade out de 1 a 0 en 0.25 segundos
        t = 0f;
        while (t < 0.25f)
        {
            t += Time.deltaTime;
            feedbackFlashGroup.alpha = Mathf.Lerp(1f, 0f, t / 0.25f);
            yield return null;
        }
        feedbackFlashGroup.alpha = 0f;
    }
}