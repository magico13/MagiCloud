using FluentAssertions;
using Goggles.TextExtraction;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Goggles.Tests.Unit;
public class LensTests
{
    private readonly ILogger<Lens> _logger;
    private readonly ITextExtractor _textExtractor;
    private readonly GogglesConfiguration _config;
    private readonly Lens _lens;

    public LensTests()
    {
        _logger = Substitute.For<ILogger<Lens>>();
        _textExtractor = Substitute.For<ITextExtractor>();
        _config = new GogglesConfiguration();
        _lens = new Lens(_logger, new[] { _textExtractor }, Options.Create(_config));
    }

    [Theory]
    [MemberData(nameof(FileExtensionAndExpectedContentTypeData))]
    public void DetermineContentType_Returns_ExpectedContentType(string extension, string expectedContentType)
    {
        // Arrange

        // Act
        var result = _lens.DetermineContentType("example"+extension);

        // Assert
        result.Should().Be(expectedContentType);
    }

    public static IEnumerable<object[]> FileExtensionAndExpectedContentTypeData 
        => new List<object[]>
        {
            new object[] { ".csv", "text/csv" },
            new object[] { ".doc", "application/msword" },
            new object[] { ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
            new object[] { ".gcode", "text/x-gcode" },
            new object[] { ".gif", "image/gif" },
            new object[] { ".htm", "text/html" },
            new object[] { ".html", "text/html" },
            new object[] { ".ico", "image/x-icon" },
            new object[] { ".ini", "text/plain" },
            new object[] { ".ino", "text/plain" },
            new object[] { ".jpeg", "image/jpeg" },
            new object[] { ".jpg", "image/jpeg" },
            new object[] { ".js", "application/javascript" },
            new object[] { ".json", "application/json" },
            new object[] { ".log", "text/plain" },
            new object[] { ".mht", "message/rfc822" },
            new object[] { ".mhtml", "message/rfc822" },
            new object[] { ".mp3", "audio/mpeg" },
            new object[] { ".mp4", "video/mp4" },
            new object[] { ".msg", "application/vnd.ms-outlook" },
            new object[] { ".odp", "application/vnd.oasis.opendocument.presentation" },
            new object[] { ".ods", "application/vnd.oasis.opendocument.spreadsheet" },
            new object[] { ".odt", "application/vnd.oasis.opendocument.text" },
            new object[] { ".ogg", "video/ogg" },
            new object[] { ".ofx", "text/plain" },
            new object[] { ".pdf", "application/pdf" },
            new object[] { ".png", "image/png" },
            new object[] { ".ppt", "application/vnd.ms-powerpoint" },
            new object[] { ".pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation" },
            new object[] { ".py", "text/x-python" },
            new object[] { ".rtf", "application/rtf" },
            new object[] { ".svg", "image/svg+xml" },
            new object[] { ".sql", "application/sql" },
            new object[] { ".txt", "text/plain" },
            new object[] { ".url", "application/internet-shortcut" },
            new object[] { ".wav", "audio/wav" },
            new object[] { ".webm", "video/webm" },
            new object[] { ".wmv", "video/x-ms-wmv" },
            new object[] { ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
            new object[] { ".yaml", "text/x-yaml" },
            new object[] { ".yml", "text/x-yaml" },
            new object[] { ".xcf", "image/x-xcf" }
        };

    [Fact]
    public async Task ExtractTextAsync_WithValidStreamAndExtractor_ReturnsExtractedText()
    {
        // Arrange
        var stream = new MemoryStream();
        var filename = "example.pdf";
        var expectedText = "Extracted text";
        _textExtractor.IsValidForContentType(Arg.Any<string>()).Returns(true);
        _textExtractor.UsesOCR.Returns(false);
        _textExtractor.ExtractTextAsync(stream, filename, Arg.Any<string>()).Returns(expectedText);

        // Act
        var actualText = await _lens.ExtractTextAsync(stream, filename);

        // Assert
        actualText.Should().Be(expectedText);
    }

    [Fact]
    public async Task ExtractTextAsync_WithNullStream_ReturnsNull()
    {
        // Arrange
        Stream? stream = null;
        var filename = "example.pdf";

        // Act
        var actualText = await _lens.ExtractTextAsync(stream, filename);

        // Assert
        actualText.Should().BeNull();
    }

    [Fact]
    public async Task ExtractTextAsync_WithInvalidExtractor_ReturnsNull()
    {
        // Arrange
        var stream = new MemoryStream();
        var filename = "example.pdf";
        _textExtractor.IsValidForContentType(Arg.Any<string>()).Returns(false);

        // Act
        var actualText = await _lens.ExtractTextAsync(stream, filename);

        // Assert
        actualText.Should().BeNull();
    }

    [Fact]
    public async Task ExtractTextAsync_WithEmptyText_ReturnsNull()
    {
        // Arrange
        var stream = new MemoryStream();
        var filename = "example.pdf";
        _textExtractor.IsValidForContentType(Arg.Any<string>()).Returns(true);
        _textExtractor.UsesOCR.Returns(false);
        _textExtractor.ExtractTextAsync(stream, filename, Arg.Any<string>()).Returns(string.Empty);

        // Act
        var actualText = await _lens.ExtractTextAsync(stream, filename);

        // Assert
        actualText.Should().BeNull();
    }

    [Fact]
    public async Task ExtractTextAsync_NullContentType_DeterminesContentType()
    {
        // Arrange
        var stream = new MemoryStream();
        var filename = "test.txt";
        var expectedContentType = "text/plain";
        _textExtractor.IsValidForContentType(expectedContentType).Returns(true);
        _textExtractor.ExtractTextAsync(stream, filename, expectedContentType).Returns("Hello, world!");

        // Act
        var result = await _lens.ExtractTextAsync(stream, filename, null);

        // Assert
        result.Should().Be("Hello, world!");
    }

    [Fact(Skip = "Not sure if I can get this one working easily without making the PlainTextExtractor virtual")]
    public async Task ExtractTextAsync_UsesPlainTextExtractor_WhenOtherExtractorsFail()
    {
        // Arrange
        var stream = new MemoryStream();
        var filename = "test.txt";
        var expectedContentType = "text/plain";
        _textExtractor.IsValidForContentType(expectedContentType).Returns(false);
        var plainTextExtractor = Substitute.For<ITextExtractor>();
        plainTextExtractor.IsValidForContentType(expectedContentType).Returns(true);
        plainTextExtractor.ExtractTextAsync(stream, filename, expectedContentType).Returns("Hello, world!");

        var lens = new Lens(_logger, new[] { _textExtractor, plainTextExtractor }, Options.Create(_config));

        // Act
        var result = await lens.ExtractTextAsync(stream, filename, expectedContentType);

        // Assert
        result.Should().Be("Hello, world!");
        _textExtractor.Received().IsValidForContentType(expectedContentType);
        await plainTextExtractor.Received().ExtractTextAsync(stream, filename, expectedContentType);
    }

    [Fact]
    public async Task ExtractTextAsync_TrimsText_ToMaxLength()
    {
        // Arrange
        var stream = new MemoryStream();
        var filename = "test.txt";
        var expectedContentType = "text/plain";
        _textExtractor.IsValidForContentType(expectedContentType).Returns(true);
        _textExtractor.ExtractTextAsync(stream, filename, expectedContentType).Returns("a".PadLeft(2000, 'a'));
        _config.MaxTextLength = 100;

        // Act
        var result = await _lens.ExtractTextAsync(stream, filename, expectedContentType);

        // Assert
        result.Should().HaveLength(100);
    }
}