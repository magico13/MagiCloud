using Goggles.OCR;
using Goggles.TextExtraction;
using Goggles.Transcription;
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
        var config = new GogglesConfiguration();
        configureOptions(config);

        services.AddScoped<ILens, Lens>();

        // OCR engine(s)
        if (!string.IsNullOrWhiteSpace(config.AzureOCRConfiguration?.SubscriptionKey)
            && !string.IsNullOrWhiteSpace(config.AzureOCRConfiguration?.VisionEndpoint))
        {
            // If Azure settings provided, use Azure OCR
            services.AddHttpClient<IOcrEngine, AzureOcrEngine>();
        }
        else
        {
            // Fall back to Tesseract OCR engine if no others provided
            services.AddSingleton<IOcrEngine, TesseractOcrEngine>();
        }

        // Audio Transcriber(s)
        services.AddHttpClient<ITranscriptionService, WhisperTranscriptionService>(c =>
        {
            c.Timeout = TimeSpan.FromMinutes(30);
            c.BaseAddress = new Uri(config.WhisperTranscriptionConfiguration.Url);
        });

        // Text extractors
        services.AddScoped<ITextExtractor, PlainTextExtractor>();
        services.AddScoped<ITextExtractor, PdfExtractor>();
        services.AddScoped<ITextExtractor, ImageExtractor>();
        services.AddScoped<ITextExtractor, AudioExtractor>();

        return services;
    }
}
