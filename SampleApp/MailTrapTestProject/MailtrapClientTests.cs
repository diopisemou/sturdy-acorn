namespace MailTrapTestProject;

using Xunit;
using Moq;
using MailKit.Net.Smtp;
using MailtrapClient;
using MimeKit;
using MailKit;

public class MailtrapClientTests
{
    private Mock<SmtpClient> _smtpClientMock;
    private MailTrapClient _mailtrapClient;
    private MailtrapConfig _config;

    public MailtrapClientTests()
    {
        
        var config = new MailtrapConfig
        {
            Username = "d6617ce2801b98",
            Password = "2644d9c3897df4",
            Host = "sandbox.smtp.mailtrap.io",
            Port = 2525, //25 or 465 or 587 or 2525
            UseSsl = false,
        };
        _config = config;
        var client = new SmtpClient();
        client.Connect(config.Host, config.Port, config.UseSsl);
        _smtpClientMock = new Mock<SmtpClient>(MockBehavior.Loose);
        _smtpClientMock.SetReturnsDefault(client);

        //Or
        // var config = new MailtrapConfig
        // {
        //     apiKey = "d6617ce2801b98",
        //     baseUrl = "https://sandbox.api.mailtrap.io",
        // };
        _mailtrapClient = new MailTrapClient(config);
        // Inject the mocked SmtpClient into your MailtrapClient
        // This assumes you have a way to inject dependencies in your MailtrapClient
        _mailtrapClient.SetSmtpClient(_smtpClientMock.Object); 
    }

    [Fact]
    public async Task SendAsync_Success_SendsEmail()
    {
        // Arrange
        var message = new MailtrapEmail
        {
            SenderName = "Test Sender",
            SenderEmail = "sender@example.com",
            RecipientName = "Test Recipient",
            RecipientEmail = "recipient@example.com",
            Subject = "Test Subject",
            Text = "Test Body"
        };

        // Act
        var result = await _mailtrapClient.SendAsync(message);

        // Assert
        Assert.True(result);
        _smtpClientMock.Verify(client => client.SendAsync(It.IsAny<MimeMessage>(), It.IsAny<CancellationToken>(), It.IsAny<ITransferProgress>()), Times.Once);
        _smtpClientMock.Verify(client => client.DisconnectAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }


    [Fact]
    public async Task SendAsync_ErrorDuringConnect_ReturnsFalseAndLogsError()
    {
        // Arrange
        _smtpClientMock.Setup(client => client.ConnectAsync(_config.baseUrl, 0000, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Connection failed"));
        var message = new MailtrapEmail { 
            SenderEmail = String.Empty,
            RecipientEmail = String.Empty,
            Subject = String.Empty, };

        // Act
        var result = await _mailtrapClient.SendAsync(message);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task SendAsync_TransientError_RetriesAndSucceeds()
    {
        // Arrange
        var attempt = 0;
        _smtpClientMock.Setup(client => client.SendAsync(It.IsAny<MimeMessage>(), It.IsAny<CancellationToken>(), It.IsAny<ITransferProgress>()))
            .ThrowsAsync(new Exception("Transient error"))
            .Callback(() =>
            {
                if (++attempt == 2) // Succeed on the 3rd attempt (2 retries)
                {
                    _smtpClientMock.Setup(client => client.SendAsync(It.IsAny<MimeMessage>(), It.IsAny<CancellationToken>(), It.IsAny<ITransferProgress>()))
                        .Returns((Task<string>)Task.CompletedTask);
                }
            });
        var message = new MailtrapEmail { 
            SenderEmail = String.Empty,
            RecipientEmail = String.Empty,
            Subject = String.Empty,
            Text = String.Empty, };

        // Act
        var result = await _mailtrapClient.SendAsync(message);

        // Assert
        Assert.True(result);
        _smtpClientMock.Verify(client => client.SendAsync(It.IsAny<MimeMessage>(), It.IsAny<CancellationToken>(), It.IsAny<ITransferProgress>()), Times.Exactly(3));
    }

    [Fact]
    public async Task SendAsync_HtmlContent_SendsHtmlEmail()
    {
        // Arrange
        var message = new MailtrapEmail
        {
            SenderName = "Test Sender",
            SenderEmail = "sender@example.com",
            RecipientName = "Test Recipient",
            RecipientEmail = "recipient@example.com",
            Subject = "Test Subject",
            Html = "<p>This is an HTML email.</p>"
        };

        // Act
        await _mailtrapClient.SendAsync(message);

        // Assert
        _smtpClientMock.Verify(client => client.SendAsync(It.Is<MimeMessage>(m => m.HtmlBody != null), It.IsAny<CancellationToken>(), It.IsAny<ITransferProgress>()), Times.Once);
    }

    // Add more tests for different content types (e.g., attachments)
}