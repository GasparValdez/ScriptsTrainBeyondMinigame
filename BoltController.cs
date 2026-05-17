using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controla el comportamiento individual de cada tuerca en la escena.
/// Gestiona la deteccion de clics del jugador, los estados visuales mediante
/// materiales y animaciones, el movimiento de bajada y subida al mantener
/// presionado el clic, los efectos de sonido en loop y el sistema de particulas
/// de chispas. Se comunica con BoltGameManager para validar cada interaccion.
/// Debe estar adjunto al GameObject de cada tuerca junto con un MeshCollider y este mismo
/// debe de ser un prefab.
/// </summary>

// Define los posibles estados visuales de una tuerca
public enum BoltState { Idle, Highlighted, Completed, Error }

[RequireComponent(typeof(Collider))]
public class BoltController : MonoBehaviour
{
    [Header("Identidad")]
    // Numero de esta tuerca segun la Figura 5-1, del 1 al 8
    public int boltId = 1;

    [Header("Materiales de retroalimentacion visual")]
    // Material cuando la tuerca esta en reposo sin ninguna indicacion
    public Material materialIdle;

    // Material que se aplica brevemente para indicar que es la siguiente a ajustar
    public Material materialHighlight;

    // Material que se aplica cuando la tuerca fue ajustada correctamente
    public Material materialCompleted;

    // Material que se aplica brevemente al seleccionar una tuerca incorrecta
    public Material materialError;

    [Header("Pulso de resaltado")]
    // Velocidad del efecto de escala pulsante cuando la tuerca esta resaltada
    public float pulseSpeed = 2f;

    // Escala minima durante el pulso
    public float pulseMinScale = 0.95f;

    // Escala maxima durante el pulso
    public float pulseMaxScale = 1.05f;

    // Tiempo en segundos que dura el resaltado antes de volver al estado inactivo
    [Tooltip("Segundos que dura el indicador visual en modo Assisted antes de volver a Idle")]
    public float hintDuration = 1.5f;

    [Header("Ajuste con clic sostenido")]
    // Posicion Y local que la tuerca debe alcanzar para considerarse correctamente ajustada
    [Tooltip("Posicion Y local objetivo al ajustar la tuerca")]
    public float targetLocalY = -0.2786f;

    // Velocidad en unidades por segundo a la que baja la tuerca mientras se mantiene el clic
    [Tooltip("Velocidad de descenso mientras se mantiene presionado el clic")]
    public float descendSpeed = 0.15f;

    // Velocidad en unidades por segundo a la que sube la tuerca al soltar el clic
    [Tooltip("Velocidad de ascenso al soltar el clic antes de llegar al fondo")]
    public float ascendSpeed = 0.3f;

    [Header("Etiqueta opcional")]
    // Texto opcional que muestra el numero de la tuerca en la escena
    public TMPro.TextMeshPro labelText;

    [Header("Cursor")]
    // Textura del taladro que reemplaza el cursor del sistema al pasar sobre la tuerca
    public Texture2D cursorHover;

    // Punto de origen del cursor dentro de la textura, en pixeles desde la esquina superior izquierda
    public Vector2 cursorHotspot = Vector2.zero;

    // Textura nula usada para restaurar el cursor del sistema al salir de la tuerca
    private static Texture2D defaultCursorTexture = null;

    // Estado visual actual de la tuerca
    private BoltState currentState = BoltState.Idle;

    // Todos los Renderer hijos del GameObject para aplicar materiales
    private Renderer[] renderers;

    // Animator del GameObject para controlar las animaciones de la tuerca
    private Animator animator;

    // Posicion local original antes de cualquier movimiento de ajuste
    private Vector3 originalLocalPos;

    // Escala local original para restaurar despues del pulso
    private Vector3 originalScale;

    // Rotacion local original para restaurar al reiniciar
    private Quaternion originalRotation;

    // Indica si el BoltSpawner ya asigno la rotacion correcta mediante InitRotation
    private bool rotationInitialized = false;

    // Referencia a la corrutina del pulso de escala para poder detenerla
    private Coroutine pulseCoroutine;

    // Referencia a la corrutina de movimiento activa para poder detenerla
    private Coroutine moveCoroutine;

    // Referencia a la corrutina del temporizador de hint para poder detenerla
    private Coroutine hintCoroutine;

    // Indica si el jugador esta manteniendo presionado el clic sobre esta tuerca
    private bool isHolding = false;

    // Indica si esta tuerca ya fue ajustada correctamente y no debe responder mas
    private bool isCompleted = false;

    // Indica si hay una animacion de error en curso para bloquear nuevas interacciones
    private bool isAnimating = false;

    // Hash del parametro del Animator calculado una vez para mejor rendimiento
    private static readonly int AnimIsTightening = Animator.StringToHash("IsTightening");

    [Header("Sonido de ajuste")]
    // Clip de audio que se reproduce en loop mientras el jugador mantiene presionado el clic
    [Tooltip("Clip que se reproduce en bucle mientras se ajusta la tuerca")]
    public AudioClip tighteningLoopClip;

    // AudioSource de esta tuerca para el sonido de ajuste en loop
    private AudioSource audioSource;

    // Sistema de particulas de chispas hijo del prefab
    private ParticleSystem sparks;

    private void Awake()
    {
        // Obtiene todos los Renderer del GameObject y sus hijos para cambiar materiales
        renderers = GetComponentsInChildren<Renderer>();

        // Obtiene el Animator para controlar las animaciones de giro
        animator = GetComponent<Animator>();

        // Guarda la escala original para restaurarla despues del pulso
        originalScale = transform.localScale;

        // Guarda la rotacion original como fallback si el spawner no llama a InitRotation
        originalRotation = transform.localRotation;

        // Muestra el numero de la tuerca en la etiqueta 3D si esta asignada
        if (labelText != null)
            labelText.text = boltId.ToString();

        // Busca o crea el AudioSource para el sonido de ajuste en loop
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        // Configura el AudioSource para reproduccion en loop sin iniciarse automaticamente
        audioSource.loop = true;
        audioSource.playOnAwake = false;

        // Busca el sistema de particulas de chispas por nombre dentro de los hijos
        Transform sparksTransform = transform.Find("vfx_Sparks_01");
        if (sparksTransform != null)
            sparks = sparksTransform.GetComponent<ParticleSystem>();
        else
            Debug.LogWarning($"[Tuerca {boltId}] No se encontro el hijo 'vfx_Sparks_01'.");
    }

    /// <summary>
    /// Llamado por BoltSpawner inmediatamente despues de posicionar e instanciar la tuerca.
    /// Garantiza que la rotacion original sea la correcta sin depender del orden de Start.
    /// </summary>
    public void InitRotation(Quaternion finalLocalRotation)
    {
        // Guarda la rotacion final asignada por el spawner
        originalRotation = finalLocalRotation;

        // Marca que la rotacion ya fue inicializada correctamente
        rotationInitialized = true;
    }

    private void Start()
    {
        // Guarda la posicion local inicial para poder restaurarla al reiniciar o al soltar el clic
        originalLocalPos = transform.localPosition;

        // Si el spawner no asigno la rotacion, usa la rotacion actual como fallback
        if (!rotationInitialized)
            originalRotation = transform.localRotation;

        // Se auto-registra en el BoltGameManager para que pueda gestionarla
        BoltGameManager.Instance?.RegisterBolt(this);

        // Aplica el material de reposo inicial
        ApplyMaterial(materialIdle);

        // Asegura que el Animator comience en estado Idle y no en animacion de giro
        SetAnimatorTightening(false);
    }

    private void OnMouseDown()
    {
        // Ignora el clic si la tuerca ya fue completada o hay una animacion de error activa
        if (isCompleted || isAnimating) return;

        // Ignora el clic si el juego no esta activo
        if (BoltGameManager.Instance == null || !BoltGameManager.Instance.IsGameActive()) return;

        if (BoltGameManager.Instance.gameMode == GameMode.Challenge)
        {
            // En modo Desafio cualquier tuerca puede intentarse, el manager valida si es correcta
            StartHolding();
        }
        else
        {
            // En modo Asistido solo inicia el ajuste si es el turno de esta tuerca
            if (!IsMyTurn())
            {
                // Notifica al manager que se intento una tuerca incorrecta
                BoltGameManager.Instance?.OnBoltInteracted(boltId);
                return;
            }
            StartHolding();
        }
    }

    private void OnMouseUp()
    {
        // Solo reacciona si el jugador estaba manteniendo presionado sobre esta tuerca
        if (!isHolding) return;
        StopHolding();
    }

    private void OnMouseEnter()
    {
        // Cambia el cursor del sistema a un taladro al pasar sobre una tuerca no completada con el juego activo
        if (isCompleted || !BoltGameManager.Instance.IsGameActive()) return;
        if (cursorHover != null)
            Cursor.SetCursor(cursorHover, cursorHotspot, CursorMode.Auto);
    }

    private void OnMouseExit()
    {
        // Restaura el cursor del sistema al salir del area de la tuerca
        Cursor.SetCursor(defaultCursorTexture, Vector2.zero, CursorMode.Auto);

        // Si el jugador salio del collider mientras mantenia presionado, detiene el ajuste
        if (isHolding) StopHolding();
    }

    /// <summary>
    /// Inicia la secuencia de ajuste: activa el Animator, los efectos de sonido
    /// y particulas, y lanza la corrutina de descenso de la tuerca.
    /// </summary>
    
    private void StartHolding()
    {
        // Marca que el jugador esta manteniendo presionado
        isHolding = true;

        // Activa la animacion de giro en el Animator
        SetAnimatorTightening(true);

        // Activa el sonido en loop y las particulas de chispas
        PlayTighteningEffects(true);

        // Cancela cualquier movimiento anterior y lanza la corrutina de descenso
        if (moveCoroutine != null) StopCoroutine(moveCoroutine);
        moveCoroutine = StartCoroutine(DescendRoutine());
    }

    /// <summary>
    /// Detiene la secuencia de ajuste: desactiva el Animator, los efectos
    /// y lanza la corrutina de ascenso si la tuerca no llego al fondo.
    /// </summary>
    private void StopHolding()
    {
        // Marca que el jugador solto el clic
        isHolding = false;

        // Desactiva la animacion de giro
        SetAnimatorTightening(false);

        // Detiene el sonido y las particulas
        PlayTighteningEffects(false);

        // Cancela la corrutina de descenso
        if (moveCoroutine != null) StopCoroutine(moveCoroutine);

        // Si la tuerca no llego al fondo, la regresa a su posicion original
        if (!isCompleted)
            moveCoroutine = StartCoroutine(AscendRoutine());
    }

    /// <summary>
    /// Mueve la tuerca hacia abajo cada frame mientras el jugador mantiene presionado.
    /// Al llegar al objetivo notifica al manager o sube si era incorrecta en modo Desafio.
    /// </summary>
    private IEnumerator DescendRoutine()
    {
        while (isHolding)
        {
            // Si el juego se desactivo mientras bajaba, aborta y sube
            if (BoltGameManager.Instance == null || !BoltGameManager.Instance.IsGameActive())
            {
                isHolding = false;
                SetAnimatorTightening(false);
                PlayTighteningEffects(false);
                moveCoroutine = StartCoroutine(AscendRoutine());
                yield break;
            }

            float currentY = transform.localPosition.y;

            // Verifica si la tuerca alcanzo la posicion objetivo
            if (currentY <= targetLocalY)
            {
                // Ajusta la posicion exactamente al objetivo para evitar imprecisiones
                Vector3 pos = transform.localPosition;
                pos.y = targetLocalY;
                transform.localPosition = pos;

                // Detiene el ajuste y los efectos
                isHolding = false;
                SetAnimatorTightening(false);
                PlayTighteningEffects(false);

                // En modo Desafio, si era la tuerca incorrecta, sube de vuelta
                if (BoltGameManager.Instance?.gameMode == GameMode.Challenge && !IsMyTurn())
                {
                    // Notifica al manager para disparar el sonido de error y el efecto flash del canvas
                    BoltGameManager.Instance?.OnBoltInteracted(boltId);
                    moveCoroutine = StartCoroutine(AscendRoutine());
                    yield break;
                }

                // Notifica al manager que esta tuerca fue interactuada
                BoltGameManager.Instance?.OnBoltInteracted(boltId);
                yield break;
            }

            // Mueve la tuerca hacia abajo segun la velocidad configurada y el tiempo del frame
            float newY = Mathf.MoveTowards(currentY, targetLocalY, descendSpeed * Time.deltaTime);
            transform.localPosition = new Vector3(
                transform.localPosition.x,
                newY,
                transform.localPosition.z);

            yield return null;
        }
    }

    /// <summary>
    /// Mueve la tuerca hacia arriba cada frame hasta recuperar su posicion original.
    /// Se activa al soltar el clic antes de llegar al objetivo.
    /// </summary>
    private IEnumerator AscendRoutine()
    {
        while (true)
        {
            float currentY = transform.localPosition.y;

            // Verifica si la tuerca ya llego o supero su posicion original
            if (Mathf.Approximately(currentY, originalLocalPos.y) || currentY >= originalLocalPos.y)
            {
                // Ajusta exactamente a la posicion original para evitar imprecisiones
                transform.localPosition = originalLocalPos;
                yield break;
            }

            // Mueve la tuerca hacia arriba segun la velocidad de ascenso y el tiempo del frame
            float newY = Mathf.MoveTowards(currentY, originalLocalPos.y, ascendSpeed * Time.deltaTime);
            transform.localPosition = new Vector3(
                transform.localPosition.x,
                newY,
                transform.localPosition.z);

            yield return null;
        }
    }

    /// <summary>
    /// Aplica el estado visual indicado a la tuerca.
    /// Gestiona materiales, pulso de escala e indicador de hint segun el modo de juego.
    /// </summary>
    public void SetState(BoltState newState)
    {
        // Detiene el pulso de escala si habia uno activo y restaura la escala original
        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
            pulseCoroutine = null;
            transform.localScale = originalScale;
        }

        // Actualiza el estado interno
        currentState = newState;

        switch (newState)
        {
            case BoltState.Idle:
                // Aplica el material de reposo
                ApplyMaterial(materialIdle);
                break;

            case BoltState.Highlighted:
                // En modo Desafio no se muestra ningun indicador visual
                if (BoltGameManager.Instance?.gameMode == GameMode.Assisted)
                {
                    // Aplica el material de resaltado
                    ApplyMaterial(materialHighlight);

                    // Inicia el efecto de pulso de escala
                    pulseCoroutine = StartCoroutine(PulseLoop());

                    // Cancela cualquier temporizador de hint anterior
                    if (hintCoroutine != null) StopCoroutine(hintCoroutine);

                    // Inicia el temporizador que apagara el hint despues de hintDuration segundos
                    hintCoroutine = StartCoroutine(HintTimeout());
                }
                break;

            case BoltState.Completed:
                // Marca la tuerca como ajustada para bloquear futuras interacciones
                isCompleted = true;

                // Aplica el material de completado
                ApplyMaterial(materialCompleted);

                // Restaura la escala original por si el pulso la habia modificado
                transform.localScale = originalScale;
                break;

            case BoltState.Error:
                // El estado de error es transitorio y se gestiona en PlayErrorFeedback
                break;
        }
    }

    /// <summary>
    /// Reinicia la tuerca a su estado inicial de posicion, rotacion, escala y material.
    /// Se llama al iniciar o reiniciar la partida.
    /// </summary>
    
    public void ResetBolt()
    {
        // Reinicia las banderas de estado
        isCompleted = false;
        isHolding = false;
        isAnimating = false;

        // Detiene cualquier corrutina de movimiento activa
        if (moveCoroutine != null) { StopCoroutine(moveCoroutine); moveCoroutine = null; }

        // Detiene el temporizador de hint si estaba activo
        if (hintCoroutine != null) { StopCoroutine(hintCoroutine); hintCoroutine = null; }

        // Detiene la animacion de giro y los efectos de sonido y particulas
        SetAnimatorTightening(false);
        PlayTighteningEffects(false);

        // Aplica el material de reposo
        SetState(BoltState.Idle);

        // Restaura la posicion, rotacion y escala originales
        transform.localPosition = originalLocalPos;
        transform.localRotation = originalRotation;
        transform.localScale = originalScale;
    }

    /// <summary>
    /// Corrutina que reproduce un parpadeo de material de error tres veces
    /// para indicar visualmente que la tuerca seleccionada era incorrecta.
    /// </summary>
    public IEnumerator PlayErrorFeedback()
    {
        // Bloquea nuevas interacciones durante la animacion de error
        isAnimating = true;

        // Guarda el estado anterior para restaurarlo al finalizar
        BoltState previousState = currentState;

        // Parpadea entre el material de error y el material anterior tres veces
        for (int i = 0; i < 3; i++)
        {
            ApplyMaterial(materialError);
            yield return new WaitForSeconds(0.12f);
            ApplyMaterial(previousState == BoltState.Highlighted ? materialHighlight : materialIdle);
            yield return new WaitForSeconds(0.12f);
        }

        // Restaura el estado visual anterior
        SetState(previousState);

        // Permite nuevas interacciones
        isAnimating = false;
    }

    /// <summary>
    /// Verifica si es el turno de esta tuerca segun el paso actual de la secuencia.
    /// </summary>
    private bool IsMyTurn()
    {
        var gm = BoltGameManager.Instance;
        if (gm == null) return false;

        int step = gm.GetCurrentStep();

        // Si ya se superaron todos los pasos, no es turno de nadie
        if (step >= BoltGameManager.CorrectSequence.Length) return false;

        // Compara el ID de esta tuerca con el esperado en el paso actual
        return BoltGameManager.CorrectSequence[step] == boltId;
    }

    /// <summary>
    /// Activa o desactiva el parametro IsTightening del Animator
    /// para cambiar entre la animacion Idle y Turn_And_Down.
    /// </summary>
    private void SetAnimatorTightening(bool value)
    {
        if (animator != null)
            animator.SetBool(AnimIsTightening, value);
    }

    /// <summary>
    /// Corrutina que apaga el resaltado despues de hintDuration segundos
    /// y regresa la tuerca al estado Idle para no dar ventaja permanente al jugador.
    /// </summary>
    private IEnumerator HintTimeout()
    {
        // Espera el tiempo configurado para el hint
        yield return new WaitForSeconds(hintDuration);

        // Solo apaga el hint si la tuerca sigue resaltada y no fue completada o reseteada
        if (currentState == BoltState.Highlighted)
        {
            // Detiene el pulso de escala
            if (pulseCoroutine != null)
            {
                StopCoroutine(pulseCoroutine);
                pulseCoroutine = null;
            }

            // Restaura la escala original
            transform.localScale = originalScale;

            // Aplica el material de reposo sin llamar a SetState para no relanzar el hint
            ApplyMaterial(materialIdle);

            // Actualiza el estado interno a Idle
            currentState = BoltState.Idle;
        }

        // Limpia la referencia a la corrutina al finalizar
        hintCoroutine = null;
    }

    /// <summary>
    /// Activa o desactiva el sonido en loop y el sistema de particulas de chispas
    /// segun si el jugador esta manteniendo presionado el clic.
    /// </summary>
    private void PlayTighteningEffects(bool active)
    {
        // Gestiona el audio en loop
        if (audioSource != null && tighteningLoopClip != null)
        {
            if (active)
            {
                // Solo inicia la reproduccion si no estaba ya sonando
                if (!audioSource.isPlaying)
                {
                    audioSource.clip = tighteningLoopClip;
                    audioSource.Play();
                }
            }
            else
            {
                // Detiene el audio al soltar o completar
                audioSource.Stop();
            }
        }

        // Gestiona el sistema de particulas de chispas
        if (sparks != null)
        {
            if (active && !sparks.isPlaying)
                // Inicia las particulas si no estaban activas
                sparks.Play();
            else if (!active && sparks.isPlaying)
                // Detiene las particulas y limpia las existentes inmediatamente
                sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    /// <summary>
    /// Aplica el material indicado a todos los Renderer del GameObject excepto
    /// los ParticleSystemRenderer para no alterar el material de las chispas.
    /// </summary>
    private void ApplyMaterial(Material mat)
    {
        if (mat == null) return;
        foreach (var r in renderers)
        {
            // Salta los renderers que pertenecen a sistemas de particulas
            if (r is ParticleSystemRenderer) continue;
            r.material = mat;
        }
    }

    /// <summary>
    /// Corrutina que hace pulsar la escala de la tuerca de forma ondulada
    /// para indicar visualmente que es la siguiente en ser ajustada.
    /// </summary>
    private IEnumerator PulseLoop()
    {
        while (true)
        {
            // Calcula un valor entre 0 y 1 usando una onda ondulada basada en el tiempo
            float t = (Mathf.Sin(Time.time * pulseSpeed * Mathf.PI) + 1f) * 0.5f;

            // Interpola entre la escala minima y maxima segun el valor ondulado
            float scaleFactor = Mathf.Lerp(pulseMinScale, pulseMaxScale, t);

            // Aplica la escala resultante al transform de la tuerca
            transform.localScale = originalScale * scaleFactor;

            yield return null;
        }
    }
}