using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;

public enum TipoDispositivoInput { TecladoMouse, Gamepad, Joystick, XRController }

public class InputDeviceTracker : MonoBehaviour
{
    public static InputDeviceTracker Instance { get; private set; }

    public TipoDispositivoInput DispositivoAtual { get; private set; } = TipoDispositivoInput.TecladoMouse;
    public bool HouveInputEsteFrame { get; private set; }

    public event System.Action<TipoDispositivoInput> OnDispositivoAlterado;

    [Tooltip("Magnitude mínima de eixo/stick para contar como input real (evita drift/ruído).")]
    [SerializeField] private float deadZoneAnalogico = 0.3f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        bool teclado = DetectouTecladoMouse();
        bool gamepad = !teclado && DetectouGenerico(Gamepad.current);
        bool joystick = !teclado && !gamepad && DetectouGenerico(Joystick.current);
        bool xr = !teclado && !gamepad && !joystick && DetectouXR();

        HouveInputEsteFrame = teclado || gamepad || joystick || xr;

        if (teclado) DefinirDispositivo(TipoDispositivoInput.TecladoMouse);
        else if (gamepad) DefinirDispositivo(TipoDispositivoInput.Gamepad);
        else if (joystick) DefinirDispositivo(TipoDispositivoInput.Joystick);
        else if (xr) DefinirDispositivo(TipoDispositivoInput.XRController);
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

    /// <summary>
    /// Generic detection for any device exposing buttons/sticks/2D axes (Gamepad, Joystick, XRController).
    /// </summary>
    private bool DetectouGenerico(InputDevice device)
    {
        if (device == null) return false;

        foreach (var control in device.allControls)
        {
            if (control is ButtonControl botao && botao.wasPressedThisFrame) return true;
            if (control is StickControl stick && stick.ReadValue().sqrMagnitude > deadZoneAnalogico * deadZoneAnalogico) return true;
            if (control is Vector2Control eixo2D && eixo2D.ReadValue().sqrMagnitude > deadZoneAnalogico * deadZoneAnalogico) return true;
        }

        return false;
    }

    private bool DetectouXR()
    {
        foreach (var device in InputSystem.devices)
        {
            if (device is XRController && DetectouGenerico(device))
                return true;
        }
        return false;
    }

    private void DefinirDispositivo(TipoDispositivoInput novo)
    {
        if (novo == DispositivoAtual) return;
        DispositivoAtual = novo;
        OnDispositivoAlterado?.Invoke(novo);
    }
}
