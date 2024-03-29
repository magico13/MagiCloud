using Goggles;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add Lens with the provided values
var gogglesConfig = builder
    .Configuration
    .GetSection(nameof(GogglesConfiguration))
    .Get<GogglesConfiguration>()
    ?? new();

builder.Services.AddLens(o =>
{
    o.MaxTextLength = gogglesConfig.MaxTextLength;
    o.EnableOCR = gogglesConfig.EnableOCR;
    o.EnableAudioTranscription = gogglesConfig.EnableAudioTranscription;
    o.AzureOCRConfiguration = gogglesConfig.AzureOCRConfiguration;
    o.WhisperTranscriptionConfiguration = gogglesConfig.WhisperTranscriptionConfiguration;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
