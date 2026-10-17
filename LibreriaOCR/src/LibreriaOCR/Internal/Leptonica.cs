using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LibreriaOCR.Internal;

/// <summary>
/// Interop mínimo con la Leptonica que ya trae Tesseract, para decodificar imágenes que SkiaSharp no soporta
/// (sobre todo TIFF) convirtiéndolas a PNG en memoria. Si por cualquier razón no está disponible, devuelve null
/// y el llamador puede usar SkiaSharp como alternativa.
/// </summary>
internal static class Leptonica
{
    private const int IFF_PNG = 3; // formato PNG en Leptonica

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr PixReadMemDelegate(byte[] data, UIntPtr size);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int PixWriteMemDelegate(out IntPtr pdata, out UIntPtr psize, IntPtr pix, int format);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void PixDestroyDelegate(ref IntPtr ppix);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void LeptFreeDelegate(IntPtr ptr);

    private static readonly object Gate = new();
    private static bool _initialized;
    private static PixReadMemDelegate? _pixReadMem;
    private static PixWriteMemDelegate? _pixWriteMem;
    private static PixDestroyDelegate? _pixDestroy;
    private static LeptFreeDelegate? _leptFree;

    public static bool IsAvailable
    {
        get
        {
            EnsureInitialized();
            return _pixReadMem != null && _pixWriteMem != null && _pixDestroy != null;
        }
    }

    /// <summary>Convierte una imagen (TIFF/PNG/JPG/BMP) a PNG en memoria. Devuelve null si no fue posible.</summary>
    public static byte[]? ConvertToPng(byte[] imageBytes)
    {
        EnsureInitialized();
        if (_pixReadMem == null || _pixWriteMem == null || _pixDestroy == null)
            return null;

        var pix = _pixReadMem(imageBytes, (UIntPtr)imageBytes.Length);
        if (pix == IntPtr.Zero)
            return null;

        try
        {
            if (_pixWriteMem(out var pdata, out var psize, pix, IFF_PNG) != 0 || pdata == IntPtr.Zero)
                return null;

            try
            {
                var length = (int)psize;
                var buffer = new byte[length];
                Marshal.Copy(pdata, buffer, 0, length);
                return buffer;
            }
            finally
            {
                _leptFree?.Invoke(pdata);
            }
        }
        finally
        {
            _pixDestroy(ref pix);
        }
    }

    private static void EnsureInitialized()
    {
        if (_initialized) return;
        lock (Gate)
        {
            if (_initialized) return;
            _initialized = true;
            try
            {
                var handle = ResolveLeptonicaModule();
                if (handle == IntPtr.Zero) return;

                _pixReadMem = Marshal.GetDelegateForFunctionPointer<PixReadMemDelegate>(
                    NativeLibrary.GetExport(handle, "pixReadMem"));
                _pixWriteMem = Marshal.GetDelegateForFunctionPointer<PixWriteMemDelegate>(
                    NativeLibrary.GetExport(handle, "pixWriteMem"));
                _pixDestroy = Marshal.GetDelegateForFunctionPointer<PixDestroyDelegate>(
                    NativeLibrary.GetExport(handle, "pixDestroy"));

                if (NativeLibrary.TryGetExport(handle, "lept_free", out var freePtr))
                    _leptFree = Marshal.GetDelegateForFunctionPointer<LeptFreeDelegate>(freePtr);
            }
            catch
            {
                // Queda no disponible; el llamador usará SkiaSharp.
                _pixReadMem = null;
                _pixWriteMem = null;
                _pixDestroy = null;
            }
        }
    }

    private static IntPtr ResolveLeptonicaModule()
    {
        // ¿Ya la cargó Tesseract en el proceso?
        foreach (ProcessModule module in Process.GetCurrentProcess().Modules)
        {
            if (module.ModuleName.StartsWith("leptonica", StringComparison.OrdinalIgnoreCase))
                return NativeLibrary.Load(module.FileName);
        }

        // Si no, buscar el nativo junto al ensamblado (x64/x86 que copia TesseractOCR).
        var arch = Environment.Is64BitProcess ? "x64" : "x86";
        var dir = Path.Combine(AppContext.BaseDirectory, arch);
        if (Directory.Exists(dir))
        {
            foreach (var file in Directory.GetFiles(dir, "leptonica*.dll"))
                return NativeLibrary.Load(file);
        }

        return IntPtr.Zero;
    }
}
