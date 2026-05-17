using UnityEngine;

/// <summary>
/// Instancia y posiciona las 8 tuercas en el patron octagonal del BOP
/// siguiendo la numeracion de la Figura 5-1. Calcula los angulos y coordenadas
/// de cada posicion, instancia el prefab de la tuerca, aplica la rotacion
/// correcta preservando la del prefab y notifica a cada BoltController su ID
/// y su rotacion final antes de que Start sea llamado.
/// Debe estar adjunto a un GameObject hijo del modelo del BOP en la escena.
/// </summary>
public class BoltSpawner : MonoBehaviour
{
    [Header("Prefab")]
    // Prefab de la tuerca que debe tener el componente BoltController adjunto
    [Tooltip("Prefab de la tuerca con BoltController adjunto")]
    public GameObject nutPrefab;

    [Header("Distribucion")]
    // Radio del circulo de tuercas en unidades de mundo, debe coincidir con el modelo del BOP
    [Tooltip("Radio del circulo de tuercas en unidades de mundo")]
    public float boltCircleRadius = 1.5f;

    // Desplazamiento en el eje Y para ajustar la altura de las tuercas sobre la brida
    [Tooltip("Desplazamiento en Y para alinear las tuercas con la superficie de la brida")]
    public float heightOffset = 0f;

    // Si es verdadero, orienta cada tuerca para que su eje vertical coincida con la normal del BOP
    [Tooltip("Si esta activo, las tuercas se orientan segun la normal de la superficie del BOP")]
    public bool alignToSurface = true;

    // Angulos en grados de cada tuerca, medidos en sentido horario desde las 12 en punto
    // El indice 0 corresponde a la tuerca con ID 1, el indice 1 a la ID 2, y asi sucesivamente
    private static readonly float[] BoltAngles =
    {
        0f,    // Tuerca 1 - posicion 12 en punto
        180f,  // Tuerca 2 - posicion 6 en punto
        90f,   // Tuerca 3 - posicion 3 en punto
        270f,  // Tuerca 4 - posicion 9 en punto
        45f,   // Tuerca 5 - posicion 1:30
        225f,  // Tuerca 6 - posicion 7:30
        135f,  // Tuerca 7 - posicion 4:30
        315f,  // Tuerca 8 - posicion 10:30
    };

    private void Start()
    {
        // Instancia todas las tuercas al iniciar la escena
        SpawnBolts();
    }

    // Permite ejecutar el spawner desde el menu contextual del Inspector en modo Editor
    [ContextMenu("Instanciar tuercas (Editor)")]
    public void SpawnBolts()
    {
        // Verifica que el prefab este asignado antes de continuar
        if (nutPrefab == null)
        {
            Debug.LogError("[BoltSpawner] El campo nutPrefab no esta asignado.");
            return;
        }

        // Elimina cualquier tuerca instanciada previamente para evitar duplicados al re-ejecutar
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        // Itera sobre los 8 angulos para posicionar cada tuerca
        for (int i = 0; i < BoltAngles.Length; i++)
        {
            float angleDeg = BoltAngles[i];

            // Convierte el angulo de grados a radianes para las funciones trigonometricas
            float angleRad = angleDeg * Mathf.Deg2Rad;

            // Calcula la posicion local usando seno y coseno para distribucion en circulo
            // X = sin(angulo) * radio, Z = cos(angulo) * radio, sentido horario desde +Z
            Vector3 localPos = new Vector3(
                Mathf.Sin(angleRad) * boltCircleRadius,
                heightOffset,
                Mathf.Cos(angleRad) * boltCircleRadius
            );

            // Convierte la posicion local del spawner a coordenadas de mundo
            Vector3 worldPos = transform.TransformPoint(localPos);

            // Captura la rotacion del prefab para preservarla al instanciar
            Quaternion prefabRot = nutPrefab.transform.rotation;

            // Instancia la tuerca con la rotacion del prefab y como hijo del spawner
            GameObject nutObj = Instantiate(nutPrefab, worldPos, prefabRot, transform);

            // Nombra cada tuerca con su numero de orden para identificarla en la Hierarchy
            nutObj.name = $"Tuerca_{i + 1:D2}";

            if (alignToSurface)
            {
                // Combina la orientacion de la superficie del BOP con la rotacion original del prefab
                // para que la tuerca quede alineada a la superficie sin perder su rotacion de disenio
                nutObj.transform.rotation = Quaternion.FromToRotation(Vector3.up, transform.up)
                                            * prefabRot;
            }

            // Busca el BoltController en la tuerca instanciada
            BoltController bc = nutObj.GetComponent<BoltController>();
            if (bc != null)
            {
                // Asigna el ID de la tuerca segun su posicion en el array, comenzando desde 1
                bc.boltId = i + 1;

                // Notifica la rotacion final al BoltController antes de que su Start sea llamado
                // para evitar que la rotacion sea sobreescrita por el orden de ejecucion
                bc.InitRotation(nutObj.transform.localRotation);
            }
            else
            {
                Debug.LogWarning($"[BoltSpawner] El prefab no tiene BoltController en la tuerca {i + 1}.");
            }
        }

        Debug.Log($"[BoltSpawner] Se instanciaron {BoltAngles.Length} tuercas en patron octagonal.");
    }
}