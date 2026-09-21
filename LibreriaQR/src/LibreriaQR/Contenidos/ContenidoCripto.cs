using System.Globalization;
using System.Numerics;
using QRCoder;

namespace LibreriaQR.Contenidos;

/// <summary>
/// (13) Direccion de criptomoneda. Al escanear, la wallet abre un envio a esa direccion,
/// opcionalmente con el monto ya puesto. Soporta Bitcoin, Bitcoin Cash, Litecoin y Ethereum.
/// </summary>
public sealed class ContenidoCripto : ContenidoQR
{
    /// <summary>Moneda de la direccion.</summary>
    public Criptomoneda Moneda { get; set; } = Criptomoneda.Bitcoin;

    /// <summary>Direccion de la wallet destino.</summary>
    public string Direccion { get; set; } = string.Empty;

    /// <summary>Monto a solicitar (opcional), en la unidad principal de la moneda (BTC, ETH, etc.).</summary>
    public decimal? Monto { get; set; }

    /// <summary>Etiqueta del beneficiario (opcional; no aplica a Ethereum).</summary>
    public string? Etiqueta { get; set; }

    /// <summary>Mensaje / concepto (opcional; no aplica a Ethereum).</summary>
    public string? Mensaje { get; set; }

    public override TipoQR Tipo => TipoQR.Criptomoneda;

    public override string ConstruirPayload()
    {
        var direccion = Exigir(Direccion, nameof(Direccion));
        if (Monto is < 0)
            throw new QrContenidoInvalidoException("El monto no puede ser negativo.");

        if (Moneda == Criptomoneda.Ethereum)
            return ConstruirEthereum(direccion);

        var tipo = Moneda switch
        {
            Criptomoneda.BitcoinCash => PayloadGenerator.BitcoinLikeCryptoCurrencyAddress.BitcoinLikeCryptoCurrencyType.BitcoinCash,
            Criptomoneda.Litecoin => PayloadGenerator.BitcoinLikeCryptoCurrencyAddress.BitcoinLikeCryptoCurrencyType.Litecoin,
            _ => PayloadGenerator.BitcoinLikeCryptoCurrencyAddress.BitcoinLikeCryptoCurrencyType.Bitcoin,
        };

        return new PayloadGenerator.BitcoinLikeCryptoCurrencyAddress(
            tipo,
            direccion,
            (double?)Monto,
            Etiqueta,
            Mensaje).ToString();
    }

    private string ConstruirEthereum(string direccion)
    {
        // EIP-681: ethereum:<address>[?value=<wei>]. El value va en wei (1 ETH = 10^18 wei).
        if (Monto is null or 0)
            return $"ethereum:{direccion}";

        var wei = (BigInteger)decimal.Truncate(Monto.Value * 1_000_000_000_000_000_000m);
        return $"ethereum:{direccion}?value={wei.ToString(CultureInfo.InvariantCulture)}";
    }
}
