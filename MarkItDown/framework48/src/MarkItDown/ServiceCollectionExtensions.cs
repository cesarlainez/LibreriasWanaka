using MarkItDown.Converters.Docx;
using MarkItDown.Converters.Pdf;
using MarkItDown.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MarkItDown;

/// <summary>Registro de la librería en el contenedor de inyección de dependencias.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra los convertidores integrados (.docx y .pdf) y <see cref="MarkItDownConverter"/>
    /// como singletons. Se pueden agregar convertidores propios registrando más
    /// implementaciones de <see cref="IDocumentConverter"/> antes o después de esta llamada.
    /// </summary>
    public static IServiceCollection AddMarkItDown(this IServiceCollection services)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDocumentConverter, DocxToMarkdownConverter>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDocumentConverter, PdfToMarkdownConverter>());
        services.TryAddSingleton(provider =>
        {
            // Los integrados van al final: un convertidor propio para la misma extensión
            // gana siempre, sin importar si se registró antes o después de AddMarkItDown.
            var converters = provider.GetServices<IDocumentConverter>()
                .OrderBy(c => c is DocxToMarkdownConverter or PdfToMarkdownConverter ? 1 : 0);
            return new MarkItDownConverter(converters);
        });

        return services;
    }
}
