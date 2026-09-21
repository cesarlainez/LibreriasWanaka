using QRCoder;

namespace LibreriaQR.Contenidos;

/// <summary>(10) Evento de calendario (VEVENT / iCalendar). Al escanear, ofrece agendar el evento.</summary>
public sealed class ContenidoEvento : ContenidoQR
{
    /// <summary>Titulo del evento.</summary>
    public string Titulo { get; set; } = string.Empty;

    /// <summary>Descripcion (opcional).</summary>
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>Lugar (opcional).</summary>
    public string Ubicacion { get; set; } = string.Empty;

    /// <summary>Fecha y hora de inicio.</summary>
    public DateTime Inicio { get; set; }

    /// <summary>Fecha y hora de fin.</summary>
    public DateTime Fin { get; set; }

    /// <summary>Si el evento dura todo el dia (ignora la parte horaria). Por defecto false.</summary>
    public bool TodoElDia { get; set; }

    public override TipoQR Tipo => TipoQR.EventoCalendario;

    public override string ConstruirPayload()
    {
        Exigir(Titulo, nameof(Titulo));
        if (Fin < Inicio)
            throw new QrContenidoInvalidoException("La fecha de fin no puede ser anterior a la de inicio.");

        return new PayloadGenerator.CalendarEvent(
            Titulo.Trim(),
            Descripcion ?? string.Empty,
            Ubicacion ?? string.Empty,
            Inicio,
            Fin,
            TodoElDia).ToString();
    }
}
