using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Desactiva el panel al terminar la animacion que este reproduciendo
/// en ese momento. Se usa para una transición de efecto de entrada
/// que deben desaparecer automaticamente al concluir su animacion sin
/// necesidad de gestionarlo manualmente desde otro script.
/// Debe estar adjunto al mismo panel que tiene el Animator.
/// </summary>
public class DisableAnimation : MonoBehaviour
{
    // Referencia al Animator del mismo panel para consultar la duracion de la animacion activa
    private Animator animator;

    private void Start()
    {
        // Obtiene el Animator adjunto al mismo panel
        animator = GetComponent<Animator>();

        // Solo inicia la corrutina si el Animator existe
        if (animator != null)
            StartCoroutine(WaitForAnimation());
    }

    /// <summary>
    /// Espera exactamente la duracion del estado de animacion actual
    /// y luego desactiva el panel completo.
    /// </summary>
    private IEnumerator WaitForAnimation()
    {
        // Obtiene la duracion del estado de animacion activo en la capa 0 del Animator
        float duration = animator.GetCurrentAnimatorStateInfo(0).length;

        // Espera ese tiempo antes de continuar
        yield return new WaitForSeconds(duration);

        // Desactiva el panel, ocultandolo y deteniendo todos sus componentes
        gameObject.SetActive(false);
    }
}