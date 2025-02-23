using Goggles.TextExtraction;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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

    public static IEnumerable<object[]> FileExtensionAndExpectedContentTypeData =>
        [
            [".csv", "text/csv"],
            [".doc", "application/msword"],
            [".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"],
            [".gcode", "text/x-gcode"],
            [".gif", "image/gif"],
            [".htm", "text/html"],
            [".html", "text/html"],
            [".ico", "image/x-icon"],
            [".ini", "text/plain"],
            [".ino", "text/plain"],
            [".jpeg", "image/jpeg"],
            [".jpg", "image/jpeg"],
            [".js", "text/javascript"],
            [".json", "application/json"],
            [".log", "text/plain"],
            [".mht", "message/rfc822"],
            [".mhtml", "message/rfc822"],
            [".mp3", "audio/mpeg"],
            [".mp4", "video/mp4"],
            [".msg", "application/vnd.ms-outlook"],
            [".odp", "application/vnd.oasis.opendocument.presentation"],
            [".ods", "application/vnd.oasis.opendocument.spreadsheet"],
            [".odt", "application/vnd.oasis.opendocument.text"],
            [".ogg", "video/ogg"],
            [".ofx", "text/plain"],
            [".pdf", "application/pdf"],
            [".png", "image/png"],
            [".ppt", "application/vnd.ms-powerpoint"],
            [".pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation"],
            [".py", "text/x-python"],
            [".rtf", "application/rtf"],
            [".svg", "image/svg+xml"],
            [".sql", "application/sql"],
            [".txt", "text/plain"],
            [".url", "application/internet-shortcut"],
            [".wav", "audio/wav"],
            [".webm", "video/webm"],
            [".wmv", "video/x-ms-wmv"],
            [".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"],
            [".yaml", "text/x-yaml"],
            [".yml", "text/x-yaml"],
            [".xcf", "image/x-xcf"]
        ];

    [Fact]
    public async Task ExtractTextAsync_WithValidStreamAndExtractor_ReturnsExtractedText()
    {
        // Arrange
        var stream = new MemoryStream();
        var filename = "example.pdf";
        var expectedText = "Extracted text";
        var result = new ExtractionResult(expectedText, "application/pdf", null);
        _textExtractor.IsValidForContentType(Arg.Any<string>()).Returns(true);
        _textExtractor.UsesOCR.Returns(false);
        _textExtractor.ExtractTextAsync(stream, filename, Arg.Any<string>()).Returns(result);

        // Act
        var actualResult = await _lens.ExtractTextAsync(stream, filename);

        // Assert
        actualResult.Text.Should().Be(expectedText);
    }

    [Fact]
    public async Task ExtractTextAsync_WithNullStream_ReturnsNull()
    {
        // Arrange
        var stream = Stream.Null;
        var filename = "example.pdf";

        // Act
        var actualResult = await _lens.ExtractTextAsync(stream, filename);

        // Assert
        actualResult.Text.Should().BeNull();
    }

    [Fact]
    public async Task ExtractTextAsync_WithInvalidExtractor_ReturnsNull()
    {
        // Arrange
        var stream = new MemoryStream();
        var filename = "example.pdf";
        _textExtractor.IsValidForContentType(Arg.Any<string>()).Returns(false);

        // Act
        var actualResult = await _lens.ExtractTextAsync(stream, filename);

        // Assert
        actualResult.Text.Should().BeNull();
    }

    [Fact]
    public async Task ExtractTextAsync_WithEmptyText_ReturnsNull()
    {
        // Arrange
        var stream = new MemoryStream();
        var filename = "example.pdf";
        var result = new ExtractionResult(string.Empty, "application/pdf", null);
        _textExtractor.IsValidForContentType(Arg.Any<string>()).Returns(true);
        _textExtractor.UsesOCR.Returns(false);
        _textExtractor.ExtractTextAsync(stream, filename, Arg.Any<string>()).Returns(result);

        // Act
        var actualResult = await _lens.ExtractTextAsync(stream, filename);

        // Assert
        actualResult.Text.Should().BeNull();
    }

    [Fact]
    public async Task ExtractTextAsync_NullContentType_DeterminesContentType()
    {
        // Arrange
        var stream = new MemoryStream();
        var filename = "test.txt";
        var expectedContentType = "text/plain";
        var result = new ExtractionResult("Hello, world!", expectedContentType, null);
        _textExtractor.IsValidForContentType(expectedContentType).Returns(true);
        _textExtractor.ExtractTextAsync(stream, filename, expectedContentType).Returns(result);

        // Act
        var actualResult = await _lens.ExtractTextAsync(stream, filename, null);

        // Assert
        actualResult.Text.Should().Be("Hello, world!");
        actualResult.ContentType.Should().Be(expectedContentType);
    }

    //[Fact(Skip = "Not sure if I can get this one working easily without making the PlainTextExtractor virtual")]
    //public async Task ExtractTextAsync_UsesPlainTextExtractor_WhenOtherExtractorsFail()
    //{
    //    // Arrange
    //    var stream = new MemoryStream();
    //    var filename = "test.txt";
    //    var expectedContentType = "text/plain";
    //    _textExtractor.IsValidForContentType(expectedContentType).Returns(false);
    //    var plainTextExtractor = Substitute.For<ITextExtractor>();
    //    plainTextExtractor.IsValidForContentType(expectedContentType).Returns(true);
    //    plainTextExtractor.ExtractTextAsync(stream, filename, expectedContentType).Returns("Hello, world!");

    //    var lens = new Lens(_logger, new[] { _textExtractor, plainTextExtractor }, Options.Create(_config));

    //    // Act
    //    var result = await lens.ExtractTextAsync(stream, filename, expectedContentType);

    //    // Assert
    //    result.Should().Be("Hello, world!");
    //    _textExtractor.Received().IsValidForContentType(expectedContentType);
    //    await plainTextExtractor.Received().ExtractTextAsync(stream, filename, expectedContentType);
    //}

    [Fact]
    public async Task ExtractTextAsync_TrimsText_ToMaxLength()
    {
        // Arrange
        var stream = new MemoryStream();
        var filename = "test.txt";
        var expectedContentType = "text/plain";
        var result = new ExtractionResult("a".PadLeft(2000, 'a'), expectedContentType, null);
        _textExtractor.IsValidForContentType(expectedContentType).Returns(true);
        _textExtractor.ExtractTextAsync(stream, filename, expectedContentType).Returns(result);
        _config.MaxTextLength = 100;

        // Act
        var actualResult = await _lens.ExtractTextAsync(stream, filename, expectedContentType);

        // Assert
        actualResult.Text.Should().HaveLength(100);
    }
}