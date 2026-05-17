using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rota el skybox de la escena de forma continua modificando el parametro
/// de rotacion de su material cada frame. Genera un efecto de ambiente
/// dinamico sin necesidad de animar la camara ni el entorno directamente.
/// Debe estar adjunto a cualquier GameObject activo en la escena y requiere
/// que el material del skybox tenga el parametro _Rotation disponible,
/// como sucede con los shaders de skybox panoramico de Unity.
/// </summary>
public class RotateSkybox : MonoBehaviour
{
    // Material del skybox asignado en el Inspector, debe ser el mismo que esta en Lighting Settings
    public Material skyboxMaterial;

    // Velocidad de rotacion en grados por segundo
    public float rotationSpeed = 1f;

    private void Update()
    {
        // Solo ejecuta si el material del skybox esta asignado
        if (skyboxMaterial != null)
        {
            // Multiplica el tiempo transcurrido desde el inicio por la velocidad para obtener
            // un angulo de rotacion creciente y lo aplica al parametro _Rotation del shader
            skyboxMaterial.SetFloat("_Rotation", Time.time * rotationSpeed);
        }
    }
}