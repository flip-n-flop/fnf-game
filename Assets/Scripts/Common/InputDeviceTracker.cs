using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public enum TipoDispositivoInput { TecladoMouse, Controle }

/// <summary>
/// Tracks which input device group (keyboard/mouse vs gamepad) the player last used,
/// updated live every frame. Will be consumed later by TutorialUI to decide which
/// binding (keyboard or gamepad) to display in tutorial text.
/// </summary>
public class InputDeviceTracker : MonoBehaviour
{
    public static InputDeviceTracker Instance { get; private set; }

    public TipoDispositivoInput DispositivoAtual { get; private set; } = TipoDispositivoInput.Controle;
    // Change to TecladoMouse after testing current code

    public event System.Action<TipoDispositivoInput> OnDispositivoAlterado;

    [Tooltip("Magnitude mínima do analógico para contar como input real (evita drift/ruído do controle).")]
    [SerializeField] private float deadZoneAnalogico = 0.3f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (DetectouTecladoMouse())
            DefinirDispositivo(TipoDispositivoInput.TecladoMouse);
        else if (DetectouControle())
            DefinirDispositivo(TipoDispositivoInput.Controle);
    }

    private bool DetectouTecladoMouse()
    {
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
            return true;

        if (Mouse.current != null &&
            (Mouse.current.leftButton.wasPressedThisFrame ||
             Mouse.current.rightButton.wasPressedThisFrame ||
             Mouse.current.middleButton.wasPressedThisFrame))
            return true;

        return false;
    }

    private bool DetectouControle()
    {
        var gamepad = Gamepad.current;
        if (gamepad == null) return false;

        foreach (var control in gamepad.allControls)
        {
            if (control is ButtonControl botao && botao.wasPressedThisFrame)
                return true;
        }

        if (gamepad.leftStick.ReadValue().sqrMagnitude > deadZoneAnalogico * deadZoneAnalogico) return true;
        if (gamepad.rightStick.ReadValue().sqrMagnitude > deadZoneAnalogico * deadZoneAnalogico) return true;

        return false;
    }

    private void DefinirDispositivo(TipoDispositivoInput novo)
    {
        if (novo == DispositivoAtual) return;
        DispositivoAtual = novo;
        Debug.Log($"InputDeviceTracker: Dispositivo alterado para {novo}");
        OnDispositivoAlterado?.Invoke(novo);
    }
}
