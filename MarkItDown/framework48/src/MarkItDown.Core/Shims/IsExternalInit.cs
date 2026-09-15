// Shim necesario para poder usar propiedades con acccesor "init" (característica de
// C# 9) al compilar contra .NET Framework 4.8, cuyo runtime no incluye este tipo.
// El compilador de Roslyn solo requiere que el tipo exista; no aporta lógica.

using System.ComponentModel;

namespace System.Runtime.CompilerServices
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit
    {
    }
}
