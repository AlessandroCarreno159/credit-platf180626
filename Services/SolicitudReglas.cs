namespace credit_platf.Services;

// Reglas de negocio del dominio de credito (P1).
// P5 (panel Analista) reutiliza EsAprobable; P3 reutiliza ValidarRegistro.
// Nada aqui toca la DB: solo calculos puros, testeables sin EF.
public static class SolicitudReglas
{
    // Tope de aprobacion: no aprobar si Monto > 5 x Ingresos.
    public const decimal MultiploMaximoAprobacion = 5m;

    // Tope de registro: el monto no puede superar 10 x Ingresos (P3).
    public const decimal MultiploMaximoRegistro = 10m;

    public static bool EsAprobable(decimal montoSolicitado, decimal ingresosMensuales)
        => montoSolicitado > 0
           && ingresosMensuales > 0
           && montoSolicitado <= MultiploMaximoAprobacion * ingresosMensuales;

    public static string? ValidarRegistro(
        decimal montoSolicitado,
        decimal ingresosMensuales,
        bool clienteActivo,
        bool tienePendiente)
    {
        if (!clienteActivo)
            return "El cliente no esta activo.";
        if (montoSolicitado <= 0)
            return "El monto solicitado debe ser mayor a 0.";
        if (ingresosMensuales <= 0)
            return "Los ingresos mensuales deben ser mayores a 0.";
        if (tienePendiente)
            return "El cliente ya tiene una solicitud en estado Pendiente.";
        if (montoSolicitado > MultiploMaximoRegistro * ingresosMensuales)
            return $"El monto no puede superar 10 veces los ingresos ({MultiploMaximoRegistro * ingresosMensuales:C}).";
        return null;
    }
}
