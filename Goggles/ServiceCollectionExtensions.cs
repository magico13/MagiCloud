using Goggles.OCR;
using Goggles.TextExtraction;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Goggles;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLens(
        this IServiceCollection services,
        Action<GogglesConfiguration> configureOptions)
    {
        services.Configure(configureOptions);

        services.AddScoped<ILens, Lens>();

        // OCR engine(s)
        services.AddSingleton<IOcrEngine, TesseractOcrEngine>();
       // services.AddHttpClient<IOcrEngine, AzureOcrEngine>();

        // Text extractors
        services.AddScoped<ITextExtractor, PlainTextExtractor>();
        services.AddScoped<ITextExtractor, PdfExtractor>();
        services.AddScoped<ITextExtractor, ImageExtractor>();

        return services;
    }
}
